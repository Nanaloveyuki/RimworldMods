// Oxygen type checks, collections, and struct operations for the existing evaluator.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private readonly Dictionary<ArtiTypeReference, OxygenType> _resolvedTypes =
            new Dictionary<ArtiTypeReference, OxygenType>();
        private readonly Dictionary<ArtiFunctionDeclarationStatement, OxygenType[]> _parameterTypes =
            new Dictionary<ArtiFunctionDeclarationStatement, OxygenType[]>();

        internal RuntimeScope CreateLibraryScope()
        {
            return new RuntimeScope(null);
        }

        internal void SetModuleIdentity(RuntimeScope scope, string packageId, string modulePath)
        {
            if (scope == null) return;
            scope.ModulePackageId = packageId;
            scope.ModulePath = modulePath;
        }

        internal bool IsReservedName(string name)
        {
            if (string.IsNullOrEmpty(name) || ReservedNames.Contains(name))
            {
                return true;
            }

            IArtiCallable ignored;
            return ArtiStringFunctions.TryGetCallable(name, out ignored);
        }

        private static bool IsReservedDeclarationName(string name)
        {
            return string.IsNullOrEmpty(name) || ReservedNames.Contains(name);
        }

        private void ValidateAnnotations(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            if (statements == null) return;
            foreach (ArtiStatement statement in statements)
            {
                ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
                if (variable != null) ResolveValueType(variable.Type, scope, variable.Span);
                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    if (function.TypeParameters.Count == 0)
                    {
                        RuntimeScope functionScope;
                        if (!_functionScopes.TryGetValue(function, out functionScope)) functionScope = scope;
                        _returnTypes[function] = ResolveReturnType(function, functionScope);
                        CacheParameterTypes(function, functionScope);
                        ValidateAnnotations(function.Body == null ? null : function.Body.Statements, functionScope);
                    }
                }
                ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
                if (declaration != null)
                    foreach (ArtiFunctionDeclarationStatement method in declaration.Methods)
                    {
                        RuntimeScope methodScope;
                        if (_functionScopes.TryGetValue(method, out methodScope))
                            ValidateAnnotations(method.Body == null ? null : method.Body.Statements, methodScope);
                    }
                ArtiBlockStatement block = statement as ArtiBlockStatement;
                if (block != null) ValidateAnnotations(block.Statements, scope);
                ArtiIfStatement conditional = statement as ArtiIfStatement;
                if (conditional != null)
                {
                    foreach (ArtiIfBranch branch in conditional.Branches) ValidateAnnotations(branch.Body.Statements, scope);
                    if (conditional.ElseBody != null) ValidateAnnotations(conditional.ElseBody.Statements, scope);
                }
                ArtiForStatement forLoop = statement as ArtiForStatement;
                if (forLoop != null) ValidateAnnotations(forLoop.Body.Statements, scope);
                ArtiWhileStatement whileLoop = statement as ArtiWhileStatement;
                if (whileLoop != null) ValidateAnnotations(whileLoop.Body.Statements, scope);
            }
        }

        internal OxygenType BoolType { get { return OxygenTypes.Bool; } }

        internal OxygenType NativeInt32Type { get; private set; }

        internal OxygenType GetBuiltinType(string name)
        {
            return OxygenTypes.Builtin(name);
        }

        internal OxygenType ListOf(OxygenType element, bool nullableCollection)
        {
            return OxygenTypes.Collection(OxygenTypeKind.List, element ?? OxygenTypes.Any, nullableCollection);
        }

        internal bool IsNominalStruct(OxygenType type)
        {
            return type != null && type.Kind == OxygenTypeKind.Struct;
        }

        internal OxygenType GetStructElementType(OxygenType type)
        {
            return IsNominalStruct(type) ? type : null;
        }

        internal string GetStructIdentity(OxygenType type)
        {
            return IsNominalStruct(type) ? type.Identity : null;
        }

        internal OxygenType CreateNominalStructType(string packageId, string modulePath, string declarationName)
        {
            string identity = StructIdentity(packageId, modulePath, declarationName);
            OxygenType existing;
            if (_nominalTypes.TryGetValue(identity, out existing))
            {
                return existing;
            }

            OxygenType type = new OxygenType(OxygenTypeKind.Struct, declarationName ?? string.Empty, null, false, identity);
            _nominalTypes[identity] = type;
            _structs[identity] = new OxygenStructShape(type, declarationName);
            return type;
        }

        internal void DefineStruct(OxygenType type, ArtiStructDeclarationStatement declaration, RuntimeScope scope)
        {
            if (type == null || declaration == null)
            {
                RuntimeError(declaration == null ? ArtiSourceSpan.Empty : declaration.Span, "The struct declaration is missing.");
            }

            OxygenStructShape shape;
            if (!_structs.TryGetValue(type.Identity, out shape))
            {
                shape = new OxygenStructShape(type, declaration.Name);
                _structs[type.Identity] = shape;
            }

            if (shape.Defined)
            {
                return;
            }

            shape.DeclarationScope = scope ?? _rootScope;
            PrepareStruct(declaration, shape, scope ?? _rootScope);


        }

        internal void RegisterLocalStruct(RuntimeScope scope, string name, OxygenType type)
        {
            OxygenStructShape shape = null;
            if (type == null || !_structs.TryGetValue(type.Identity, out shape))
            {
                RuntimeError(ArtiSourceSpan.Empty, "The struct type is not prepared.");
            }

            _structs[LocalIdentity(scope, name)] = shape;
        }

        internal object CreateStructConstructor(OxygenType structType)
        {
            OxygenStructShape shape = RequireShape(structType, ArtiSourceSpan.Empty);
            return new RuntimeCallable(
                arguments => ConstructStruct(shape, arguments, ArtiSourceSpan.Empty),
                shape.DeclarationName,
                (position, argumentName) => FieldType(shape, argumentName));
        }

        internal void ExportStructType(RuntimeObject ns, string name, OxygenType type, object constructor)
        {
            NamespaceSet(ns, name, new OxygenTypeExport(type, constructor));
        }

        internal object CreateExportedFunction(ArtiFunctionDeclarationStatement function, RuntimeScope closure)
        {
            return CreateFunctionCallable(function, closure);
        }

        internal object CreateExportedConst(object value)
        {
            return value;
        }

        internal RuntimeObject CreateNamespace()
        {
            return new RuntimeObject(true);
        }

        internal void NamespaceSet(RuntimeObject ns, string name, object value)
        {
            if (ns == null)
            {
                throw new RuntimeFault("The module namespace is missing.");
            }

            ns.SetUnchecked(name, value);
        }


        private static bool IsNamespace(object value)
        {
            return value is IOxygenHostValue;
        }

        private static bool TryNamespaceMember(object ns, string member, out object value)
        {
            IOxygenHostValue host = ns as IOxygenHostValue;
            if (host == null || member == null)
            {
                value = null;
                return false;
            }

            return host.TryGetMember(member, out value);
        }

        internal OxygenNativeInt32 CreateNativeInt32(int value)
        {
            EnsureNativeInt32();
            return new OxygenNativeInt32(value, NativeInt32Type);
        }

        internal object ConvertChecked(string conversionName, object value, ArtiSourceSpan span)
        {
            switch (conversionName)
            {
                case "int": return ConvertToInt(value, span);
                case "float": return ConvertToFloat(value, span);
                case "string": return ToText(value);
                case "bool": return IsTruthy(value);
                default:
                    RuntimeError(span, "Unknown conversion '" + conversionName + "'.");
                    return null;
            }
        }

        internal object EvaluateInContext(ArtiExpression expression, RuntimeScope scope, OxygenType expected)
        {
            return Evaluate(expression, scope, expected);
        }

        internal OxygenType ResolveType(ArtiTypeReference reference, RuntimeScope scope, ArtiSourceSpan span)
        {
            if (reference == null)
            {
                return OxygenTypes.Any;
            }

            OxygenType cached;
            if (!IsContextualType(reference, scope) && _resolvedTypes.TryGetValue(reference, out cached))
            {
                return cached;
            }

            OxygenType resolved = ResolveTypeUncached(reference, scope, span);
            if (!IsContextualType(reference, scope)) _resolvedTypes[reference] = resolved;
            return resolved;
        }

        internal bool BindImport(RuntimeScope scope, string alias, object ns, ArtiSourceSpan span)
        {
            if (scope == null || string.IsNullOrEmpty(alias))
            {
                RuntimeError(span, "The import alias is missing.");
            }

            if (IsReservedName(alias) || OxygenTypes.Builtin(alias) != null)
            {
                RuntimeError(span, "The import alias '" + alias + "' conflicts with a reserved name.");
            }

            object existing;
            if (scope.TryGet(alias, out existing) || TryGetBuiltin(alias, out existing))
            {
                RuntimeError(span, "The import alias '" + alias + "' is already declared.");
            }

            if (!scope.TryDeclare(alias, ns, true))
            {
                RuntimeError(span, "The import alias '" + alias + "' is already declared.");
            }

            return true;
        }

        private void EnsureNativeInt32()
        {
            if (NativeInt32Type != null) return;
            string identity = StructIdentity("std", "cs", "Int32");
            NativeInt32Type = new OxygenType(OxygenTypeKind.NativeInt32, "Int32", null, false, identity);
            _nominalTypes[identity] = NativeInt32Type;
            _structs[identity] = new OxygenStructShape(NativeInt32Type, "Int32");
        }

        private OxygenType ResolveTypeUncached(ArtiTypeReference reference, RuntimeScope scope, ArtiSourceSpan span)
        {
            if (reference.ElementType != null || reference.Name == "list" || reference.Name == "array")
            {
                if (reference.Name != "list" && reference.Name != "array")
                {
                    RuntimeError(reference.Span.Length == 0 ? span : reference.Span, "Only list and array accept a type argument.");
                }

                OxygenType element = reference.ElementType == null
                    ? OxygenTypes.Any
                    : ResolveType(reference.ElementType, scope, span);
                if (element.Kind == OxygenTypeKind.Void)
                {
                    RuntimeError(reference.Span.Length == 0 ? span : reference.Span, "void is not a value type.");
                }

                OxygenTypeKind kind = reference.Name == "array" ? OxygenTypeKind.Array : OxygenTypeKind.List;
                OxygenType collection = OxygenTypes.Collection(kind, element, false);
                return reference.IsNullable ? OxygenTypes.Nullable(collection) : collection;
            }

            OxygenType builtin = OxygenTypes.Builtin(reference.Name);
            if (builtin != null)
            {
                if (builtin.Kind == OxygenTypeKind.Void && reference.IsNullable)
                {
                    RuntimeError(reference.Span.Length == 0 ? span : reference.Span, "void is not a value type.");
                }

                return reference.IsNullable ? OxygenTypes.Nullable(builtin) : builtin;
            }

            OxygenType named = LookupNamedType(reference.Name, scope, reference.Span.Length == 0 ? span : reference.Span);
            return reference.IsNullable ? OxygenTypes.Nullable(named) : named;
        }

        private OxygenType LookupNamedType(string name, RuntimeScope scope, ArtiSourceSpan span)
        {
            int dot = name == null ? -1 : name.LastIndexOf('.');
            if (dot > 0)
            {
                string alias = name.Substring(0, dot);
                string member = name.Substring(dot + 1);

                object ns = null;
                if (scope == null || !scope.TryGet(alias, out ns) || !IsNamespace(ns))
                {
                    RuntimeError(span, "The type '" + name + "' is not declared.");
                }

                object exported;
                if (!TryNamespaceMember(ns, member, out exported))
                {
                    RuntimeError(span, "The type '" + name + "' is not exported.");
                }

                OxygenTypeExport typeExport = exported as OxygenTypeExport;
                OxygenType direct = exported as OxygenType;
                OxygenType resolved = typeExport != null ? typeExport.Type : direct;
                if (resolved == null || (resolved.Kind != OxygenTypeKind.Struct && resolved.Kind != OxygenTypeKind.NativeInt32))
                {
                    RuntimeError(span, "The name '" + name + "' is not a type.");
                }

                return resolved;
            }

            for (RuntimeScope current = scope; current != null; current = current.Parent)
            {
                OxygenStructShape local;
                if (_structs.TryGetValue(LocalIdentity(current, name), out local)) return local.Type;
                object bound;
                if (!current.TryGet(name, out bound)) continue;
                OxygenType boundType = bound as OxygenType;
                OxygenTypeExport boundExport = bound as OxygenTypeExport;
                if (boundType != null && boundType.Kind == OxygenTypeKind.TypeParameter) return boundType;
                if (boundType != null && (boundType.Kind == OxygenTypeKind.Struct || boundType.Kind == OxygenTypeKind.NativeInt32)) return boundType;
                if (boundExport != null) return boundExport.Type;
            }

            RuntimeError(span, "The type '" + name + "' is not declared.");
            return OxygenTypes.Any;
        }

        private void PrepareStruct(ArtiStructDeclarationStatement declaration, OxygenStructShape shape, RuntimeScope scope)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            int slot = 0;
            RuntimeScope fieldScope = TypeScope(scope, shape.Type);
            shape.FieldScope = fieldScope;
            if (declaration.Fields != null)
            {
                foreach (ArtiStructField field in declaration.Fields)
                {
                    if (field == null)
                    {
                        continue;
                    }

                    if (field.Name == "self" || field.Name == "Self" || !names.Add(field.Name))
                    {
                        RuntimeError(field.Span, "The struct member '" + field.Name + "' is declared more than once.");
                    }

                    OxygenType fieldType = field.IsEmbedded
                        ? RequireEmbedType(field, fieldScope)
                        : ResolveValueType(field.Type, fieldScope, field.Span);
                    shape.Fields.Add(new OxygenField(field.Name, fieldType, field.DefaultValue, field.IsEmbedded, slot));
                    slot++;
                }
            }

            if (declaration.Methods != null)
                foreach (ArtiFunctionDeclarationStatement method in declaration.Methods)
                    if (method != null) PrepareMethod(shape, method, scope);

            shape.Defined = true;
        }



        private void FinishStructs()
        {
            var shapes = new List<OxygenStructShape>();
            var seen = new HashSet<OxygenStructShape>();
            foreach (OxygenStructShape shape in _structs.Values)
            {
                if (shape.Defined && shape.Type.Kind == OxygenTypeKind.Struct && seen.Add(shape)) shapes.Add(shape);
            }

            foreach (OxygenStructShape shape in shapes)
            {
                shape.Promoted.Clear();
                ValidateEmbedGraph(shape, new List<string>(), new HashSet<string>(StringComparer.Ordinal));
            }

            foreach (OxygenStructShape shape in shapes) Promote(shape);
            foreach (OxygenStructShape shape in shapes) RejectPromotionCollisions(shape);
        }

        private void RejectPromotionCollisions(OxygenStructShape shape)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (OxygenField field in shape.Fields) names.Add(field.Name);
            foreach (OxygenMethod method in shape.Methods) names.Add(method.Name);
            foreach (string name in shape.StaticMethods.Keys) names.Add(name);
            foreach (string name in shape.Promoted.Keys)
            {
                if (!names.Add(name))
                {
                    RuntimeError(ArtiSourceSpan.Empty, "The promoted member '" + name + "' conflicts with a direct member.");
                }
            }
        }

        private OxygenType RequireEmbedType(ArtiStructField field, RuntimeScope scope)
        {
            if (field.Type == null || field.Type.IsNullable || field.Type.ElementType != null)
            {
                RuntimeError(field.Span, "embed requires a non-null struct type.");
            }

            OxygenType type = ResolveType(field.Type, scope, field.Span);
            if (!IsNominalStruct(type) || type.Nullable)
            {
                RuntimeError(field.Span, "embed requires a non-null struct type.");
            }

            return type;
        }

        private void ValidateEmbedGraph(OxygenStructShape shape, List<string> chain, HashSet<string> visiting)
        {
            if (!visiting.Add(shape.Type.Identity))
            {
                chain.Add(shape.DeclarationName);
                RuntimeError(ArtiSourceSpan.Empty, "Embedded struct types form a cycle: " + string.Join(" -> ", chain));
            }

            chain.Add(shape.DeclarationName);
            foreach (OxygenField field in shape.Fields)
            {
                if (!field.Embedded)
                {
                    continue;
                }

                OxygenStructShape embedded = RequireShape(field.Type, ArtiSourceSpan.Empty);
                field.EmbeddedShape = embedded;
                if (!embedded.Defined)
                {
                    RuntimeError(ArtiSourceSpan.Empty, "The embedded struct '" + field.Name + "' is not prepared.");
                }

                ValidateEmbedGraph(embedded, chain, visiting);
            }

            chain.RemoveAt(chain.Count - 1);
            visiting.Remove(shape.Type.Identity);
        }

        private void Promote(OxygenStructShape shape)
        {
            foreach (OxygenField field in shape.Fields)
            {
                if (!field.Embedded || field.EmbeddedShape == null)
                {
                    continue;
                }

                foreach (OxygenField promoted in field.EmbeddedShape.Fields)
                {
                    AddPromotion(shape, field, promoted.Name, false);
                }

                foreach (OxygenMethod method in field.EmbeddedShape.Methods)
                {
                    AddPromotion(shape, field, method.Name, true);
                }

                foreach (KeyValuePair<string, OxygenPromotion> nested in field.EmbeddedShape.Promoted)
                {
                    AddPromotion(shape, field, nested.Key, nested.Value.Method);
                }
            }
        }

        private static void AddPromotion(OxygenStructShape shape, OxygenField field, string name, bool method)
        {
            if (shape.Promoted.ContainsKey(name))
            {
                throw new RuntimeFault("The promoted member '" + name + "' is provided by more than one embed.");
            }

            shape.Promoted[name] = new OxygenPromotion(field.Slot, name, method);
        }

        private OxygenStructShape RequireShape(OxygenType type, ArtiSourceSpan span)
        {
            OxygenStructShape shape;
            if (type != null && _structs.TryGetValue(type.Identity, out shape))
            {
                return shape;
            }

            RuntimeError(span, "The struct type is not prepared.");
            return null;
        }

        private object ConstructStruct(OxygenStructShape shape, RuntimeArguments arguments, ArtiSourceSpan span)
        {
            if (!shape.Defined)
            {
                RuntimeError(span, "The struct '" + shape.DeclarationName + "' is not prepared.");
            }

            int count = shape.FieldCount;
            object[] supplied = new object[count];
            bool[] seen = new bool[count];
            foreach (RuntimeArgument argument in arguments.Items)
            {
                if (argument.Name == null)
                {
                    throw new RuntimeFault("Struct construction requires named arguments.");
                }

                int index = FieldIndex(shape, argument.Name);
                if (index < 0)
                {
                    throw new RuntimeFault("The struct has no field named '" + argument.Name + "'.");
                }

                if (seen[index])
                {
                    throw new RuntimeFault("The field '" + argument.Name + "' was supplied more than once.");
                }

                seen[index] = true;
                supplied[index] = argument.Value;
            }

            for (int index = 0; index < count; index++)
            {
                if (!seen[index] && shape.Fields[index].DefaultValue == null)
                {
                    throw new RuntimeFault("The field '" + shape.Fields[index].Name + "' is required.");
                }
            }

            OxygenStruct instance = new OxygenStruct(shape);
            for (int index = 0; index < count; index++)
            {
                OxygenField field = shape.Fields[index];
                object value = seen[index]
                    ? supplied[index]
                    : Evaluate(field.DefaultValue, shape.FieldScope ?? (RuntimeScope)shape.DeclarationScope, field.Type);
                instance.Fields[index] = Coerce(field.Type, value, span, "field '" + field.Name + "'");
            }

            return instance;
        }


        private static int FieldIndex(OxygenStructShape shape, string name)
        {
            for (int index = 0; index < shape.Fields.Count; index++)
            {
                if (string.Equals(shape.Fields[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private object ConvertToInt(object value, ArtiSourceSpan span)
        {
            OxygenNativeInt32 native = value as OxygenNativeInt32;
            if (native != null)
            {
                return native.Value;
            }

            if (value is int)
            {
                return value;
            }

            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number) || number != Math.Truncate(number)
                    || number < int.MinValue || number > int.MaxValue)
                {
                    RuntimeError(span, "The value cannot be converted to int.");
                }

                return (int)number;
            }

            string text = value as string;
            int parsed;
            if (text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            RuntimeError(span, "The value cannot be converted to int.");
            return null;
        }

        private object ConvertToFloat(object value, ArtiSourceSpan span)
        {
            if (value is int)
            {
                return (double)(int)value;
            }

            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    RuntimeError(span, "The value cannot be converted to float.");
                }

                return number;
            }

            string text = value as string;
            double parsed;
            if (text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                && !double.IsNaN(parsed) && !double.IsInfinity(parsed))
            {
                return parsed;
            }

            RuntimeError(span, "The value cannot be converted to float.");
            return null;
        }

        private object Coerce(OxygenType expected, object value, ArtiSourceSpan span, string site)
        {
            if (expected == null || expected.IsAny)
            {
                return value;
            }

            if (value == null)
            {
                if (expected.Nullable || expected.Kind == OxygenTypeKind.Void)
                {
                    return null;
                }

                RuntimeError(span, site + " does not accept null.");
            }

            if (expected.Kind == OxygenTypeKind.Void)
            {
                RuntimeError(span, site + " does not accept a value.");
            }

            switch (expected.Kind)

            {
                case OxygenTypeKind.Int:
                    if (value is int) return value;
                    break;
                case OxygenTypeKind.Float:
                    if (value is double) return value;
                    if (value is int) return (double)(int)value;
                    break;
                case OxygenTypeKind.Bool:
                    if (value is bool) return value;
                    break;
                case OxygenTypeKind.String:
                    if (value is string) return value;
                    break;
                case OxygenTypeKind.Object:
                    if (value is IOxygenHostValue) return value;
                    break;
                case OxygenTypeKind.List:
                case OxygenTypeKind.Array:
                    OxygenCollection collection = value as OxygenCollection;
                    if (collection != null && collection.IsArray == (expected.Kind == OxygenTypeKind.Array)
                        && TypeEquals(collection.ElementType, expected.Element))

                    {
                        return collection;
                    }
                    break;
                case OxygenTypeKind.Struct:
                case OxygenTypeKind.NativeInt32:
                    OxygenStruct instance = value as OxygenStruct;
                    if (instance != null && expected.Kind == OxygenTypeKind.Struct && instance.Shape.Type.Identity == expected.Identity)
                    {
                        return instance;
                    }

                    OxygenNativeInt32 native = value as OxygenNativeInt32;
                    if (native != null && expected.Kind == OxygenTypeKind.NativeInt32 && native.Type.Identity == expected.Identity)
                    {
                        return native;
                    }
                    break;
            }

            RuntimeError(span, site + " expected " + expected + ".");
            return null;
        }

        private static bool TypeEquals(OxygenType left, OxygenType right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Kind != right.Kind || left.Nullable != right.Nullable)
            {
                return false;
            }

            if (left.Kind == OxygenTypeKind.List || left.Kind == OxygenTypeKind.Array)
            {
                return TypeEquals(left.Element, right.Element);
            }

            if (left.Kind == OxygenTypeKind.Struct || left.Kind == OxygenTypeKind.NativeInt32 || left.Kind == OxygenTypeKind.TypeParameter)
            {
                return left.Identity == right.Identity;
            }

            return true;
        }

        private OxygenType ResolveValueType(ArtiTypeReference reference, RuntimeScope scope, ArtiSourceSpan span)
        {
            OxygenType type = ResolveType(reference, scope, span);
            if (type.Kind == OxygenTypeKind.Void)
            {
                RuntimeError(reference == null ? span : reference.Span, "void is not a value type.");
            }

            return type;
        }

        private OxygenType ResolveReturnType(ArtiFunctionDeclarationStatement function, RuntimeScope scope)
        {
            if (function == null || function.ReturnType == null)
            {
                return OxygenTypes.Any;
            }

            OxygenType type = ResolveType(function.ReturnType, scope, function.Span);
            if (type.Kind == OxygenTypeKind.Void && function.ReturnType.IsNullable)
            {
                RuntimeError(function.ReturnType.Span, "void is not a value type.");
            }

            return type;
        }

        private void CacheParameterTypes(ArtiFunctionDeclarationStatement function, RuntimeScope scope)
        {
            if (function == null || _parameterTypes.ContainsKey(function))
            {
                return;
            }

            OxygenType[] types = new OxygenType[function.Parameters.Count];
            for (int index = 0; index < types.Length; index++)
            {
                ArtiParameter parameter = function.Parameters[index];
                types[index] = parameter == null ? OxygenTypes.Any : ResolveValueType(parameter.Type, scope, parameter.Span);
            }

            _parameterTypes[function] = types;
        }

        private OxygenType ParameterType(ArtiFunctionDeclarationStatement function, int position, string name)
        {
            OxygenType[] types;
            if (function == null || !_parameterTypes.TryGetValue(function, out types)) return null;
            int index = position;
            if (!string.IsNullOrEmpty(name))
            {
                index = -1;
                for (int i = 0; i < function.Parameters.Count; i++)
                {
                    if (function.Parameters[i] != null && function.Parameters[i].Name == name)
                    {
                        index = i;
                        break;
                    }
                }
            }

            return index >= 0 && index < types.Length ? types[index] : null;
        }

        private static OxygenType FieldType(OxygenStructShape shape, string name)
        {
            if (shape == null || string.IsNullOrEmpty(name)) return null;
            int index = FieldIndex(shape, name);
            return index < 0 ? null : shape.Fields[index].Type;
        }

        private void ValidateMethodParameters(ArtiFunctionDeclarationStatement method)
        {
            if (method.Parameters == null)
            {
                return;
            }

            foreach (ArtiParameter parameter in method.Parameters)
            {
                if (parameter != null && parameter.Name == "self")
                {
                    RuntimeError(parameter.Span, "A method parameter cannot be named self.");
                }
            }
        }

        private bool TryAssignChecked(RuntimeScope scope, string name, object value, ArtiSourceSpan span, ArtiTokenKind op, out string error)
        {
            Binding binding;
            if (!scope.TryGetBinding(name, out binding))
            {
                error = "The name '" + name + "' is not declared.";
                return false;
            }

            if (binding.IsConst)
            {
                error = "The name '" + name + "' cannot be assigned.";
                return false;
            }

            object next = op == ArtiTokenKind.Equal ? value : ApplyBinary(CompoundOperator(op), binding.Value, value, span);
            if (!CanStore(binding.Type, next))
            {
                error = "The name '" + name + "' expected " + binding.Type + ".";
                return false;
            }

            return scope.TryAssign(name, Coerce(binding.Type, next, span, "assignment"), out error);
        }

        private bool TryReadStruct(OxygenStruct instance, string member, out object value)
        {
            value = null;
            OxygenMethod method = FindMethod(instance.Shape, member);
            if (method != null)
            {
                value = new OxygenBoundMethod(instance, method, InvokeBound, ParameterType);
                return true;
            }

            int index = FieldIndex(instance.Shape, member);
            if (index >= 0)
            {
                value = instance.Fields[index];
                return true;
            }

            OxygenPromotion promotion;
            if (instance.Shape.Promoted.TryGetValue(member, out promotion))
            {
                OxygenStruct embedded = instance.Fields[promotion.Slot] as OxygenStruct;
                if (embedded == null)
                {
                    return false;
                }

                return TryReadStruct(embedded, promotion.Member, out value);
            }

            return false;
        }

        private bool TryWriteStruct(OxygenStruct instance, string member, object value, ArtiSourceSpan span, bool compound, ArtiTokenKind op)
        {
            int index = FieldIndex(instance.Shape, member);
            if (index >= 0)
            {
                WriteField(instance, index, value, span, compound, op);
                return true;
            }

            OxygenPromotion promotion;
            if (!instance.Shape.Promoted.TryGetValue(member, out promotion) || promotion.Method)
            {
                return false;
            }

            OxygenStruct embedded = instance.Fields[promotion.Slot] as OxygenStruct;
            if (embedded == null)
            {
                RuntimeError(span, "The embedded field is null.");
            }

            return TryWriteStruct(embedded, promotion.Member, value, span, compound, op);
        }

        private void WriteField(OxygenStruct instance, int index, object value, ArtiSourceSpan span, bool compound, ArtiTokenKind op)
        {
            OxygenField field = instance.Shape.Fields[index];
            object current = instance.Fields[index];
            object next = compound ? ApplyBinary(op, current, value, span) : value;
            if (!CanStore(field.Type, next))
            {
                RuntimeError(span, "The field '" + field.Name + "' expected " + field.Type + ".");
            }

            instance.Fields[index] = Coerce(field.Type, next, span, "field '" + field.Name + "'");
        }

        private bool CanStore(OxygenType expected, object value)
        {
            if (expected == null || expected.IsAny)
            {
                return true;
            }

            if (value == null)
            {
                return expected.Nullable;
            }

            switch (expected.Kind)

            {
                case OxygenTypeKind.Int: return value is int;
                case OxygenTypeKind.Float: return value is double || value is int;
                case OxygenTypeKind.Bool: return value is bool;
                case OxygenTypeKind.String: return value is string;
                case OxygenTypeKind.Object: return value is IOxygenHostValue;
                case OxygenTypeKind.List:
                case OxygenTypeKind.Array:
                    OxygenCollection collection = value as OxygenCollection;
                    return collection != null && collection.IsArray == (expected.Kind == OxygenTypeKind.Array)
                        && TypeEquals(collection.ElementType, expected.Element);

                case OxygenTypeKind.Struct:
                    OxygenStruct instance = value as OxygenStruct;
                    return instance != null && expected.Kind == OxygenTypeKind.Struct && instance.Shape.Type.Identity == expected.Identity;

                case OxygenTypeKind.NativeInt32:
                    OxygenNativeInt32 native = value as OxygenNativeInt32;
                    return native != null && expected.Kind == OxygenTypeKind.NativeInt32 && native.Type.Identity == expected.Identity;
                default: return false;
            }
        }

        private OxygenMethod FindMethod(OxygenStructShape shape, string name)
        {
            for (int index = 0; index < shape.Methods.Count; index++)
            {
                if (string.Equals(shape.Methods[index].Name, name, StringComparison.Ordinal))
                {
                    return shape.Methods[index];
                }
            }

            return null;
        }

        private object InvokeBound(OxygenMethod method, OxygenStruct receiver, RuntimeArguments arguments)
        {
            return InvokeUserFunction(method.Function, arguments, method.Closure as RuntimeScope, receiver);
        }

        private bool TryCollectionMember(OxygenCollection collection, string member, out object value)
        {
            value = null;
            if (member == "count" || member == "size" || member == "length")
            {
                value = collection.Count;
                return true;
            }

            if (member == "first")
            {
                value = collection.Count == 0 ? null : collection[0];
                return true;
            }

            if (member == "last")
            {
                value = collection.Count == 0 ? null : collection[collection.Count - 1];
                return true;
            }

            if (member == "add")
            {
                if (collection.IsArray)
                {
                    return false;
                }

                value = new CollectionMutator(this, collection, true);
                return true;
            }

            if (member == "remove_at")
            {
                if (collection.IsArray)
                {
                    return false;
                }

                value = new CollectionMutator(this, collection, false);
                return true;
            }

            return false;
        }

        private bool TryGetCollectionIndex(OxygenCollection collection, object index, out object value, ArtiSourceSpan span)
        {
            int numeric = RequireIndex(index, span);
            if (numeric < 0 || numeric >= collection.Count)
            {
                RuntimeError(span, "The index is outside the collection.");
            }

            value = collection[numeric];
            return true;
        }

        private void WriteCollection(OxygenCollection collection, object index, object value, ArtiSourceSpan span, bool compound, ArtiTokenKind op)
        {
            int numeric = RequireIndex(index, span);
            if (numeric < 0 || numeric >= collection.Count)
            {
                RuntimeError(span, "The index is outside the collection.");
            }

            object current = collection[numeric];
            object next = compound ? ApplyBinary(op, current, value, span) : value;
            if (!CanStore(collection.ElementType, next))
            {
                RuntimeError(span, "The collection element expected " + collection.ElementType + ".");
            }

            collection[numeric] = Coerce(collection.ElementType, next, span, "collection element");
        }

        private static int RequireIndex(object index, ArtiSourceSpan span)
        {
            if (index is int)
            {
                return (int)index;
            }

            throw new RuntimeFault("The index must be an int.");
        }

        private OxygenCollection CreateLiteral(ArtiArrayExpression array, RuntimeScope scope, OxygenType expected)
        {
            OxygenType target = expected;
            bool asArray = target != null && target.Kind == OxygenTypeKind.Array;
            OxygenType element = target != null && (target.Kind == OxygenTypeKind.List || target.Kind == OxygenTypeKind.Array)
                ? target.Element
                : OxygenTypes.Any;


            var items = new List<object>(array.Items.Count);
            foreach (ArtiExpression item in array.Items)
            {
                object value = Evaluate(item, scope, element);
                items.Add(Coerce(element, value, item == null ? array.Span : item.Span, "collection element"));
            }

            return new OxygenCollection(asArray, element, items);
        }

        private OxygenCollection ConvertCollection(bool asArray, OxygenType element, object source, ArtiSourceSpan span)
        {
            OxygenCollection input = source as OxygenCollection;
            if (input == null)
            {
                RuntimeError(span, "The collection constructor expects a list or array.");
            }

            var items = new List<object>(input.Count);
            for (int index = 0; index < input.Count; index++)
            {
                if (!CanStore(element, input[index]))
                {
                    RuntimeError(span, "The collection element expected " + element + ".");
                }
            }

            for (int index = 0; index < input.Count; index++)
            {
                items.Add(Coerce(element, input[index], span, "collection element"));
            }

            return new OxygenCollection(asArray, element, items);
        }

        private object InvokeCollectionConstructor(string name, ArtiTypeReference typeArgument, RuntimeArguments arguments, RuntimeScope scope, ArtiSourceSpan span)
        {
            bool asArray = name == "array";
            OxygenType element = OxygenTypes.Any;
            if (typeArgument != null)
            {
                element = ResolveType(typeArgument, scope, span);
                if (element.Kind == OxygenTypeKind.Void)
                {
                    RuntimeError(span, "void is not a value type.");
                }
            }

            if (arguments.Items.Count == 0)
            {
                return new OxygenCollection(asArray, element, 0);
            }

            if (arguments.Items.Count == 1 && arguments.Items[0].Name == null)
            {
                return ConvertCollection(asArray, element, arguments.Items[0].Value, span);
            }

            throw new RuntimeFault(name + " expects no arguments or one collection.");
        }

        private bool TryObjectMember(RuntimeObject target, string member, out object value)
        {
            if (target.TryGet(member, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private void WriteObject(RuntimeObject target, string member, object value, bool compound, ArtiTokenKind op, ArtiSourceSpan span)
        {
            if (target.IsSealed)
            {
                RuntimeError(span, "The member '" + member + "' is not available.");
            }

            object current;
            object next = value;
            if (compound)
            {
                if (!target.TryGet(member, out current))
                {
                    RuntimeError(span, "The member '" + member + "' is not available.");
                }

                next = ApplyBinary(op, current, value, span);
            }

            target.Set(member, next);
        }

        private static string StructIdentity(string packageId, string modulePath, string name)
        {
            return (packageId ?? string.Empty) + "/" + (modulePath ?? string.Empty) + "/" + (name ?? string.Empty);
        }

        private static string LocalIdentity(RuntimeScope scope, string name)
        {
            return "local/" + (scope == null ? "0" : scope.Id.ToString(CultureInfo.InvariantCulture)) + "/" + name;
        }

        private sealed class OxygenTypeExport
        {
            internal OxygenTypeExport(OxygenType type, object constructor)
            {
                Type = type;
                Constructor = constructor;
            }

            internal OxygenType Type { get; }

            internal object Constructor { get; }
        }

        private sealed class CollectionMutator : IOxygenTypedCallable
        {
            private readonly ArtiExecutor _executor;
            private readonly OxygenCollection _collection;
            private readonly bool _add;

            internal CollectionMutator(ArtiExecutor executor, OxygenCollection collection, bool add)
            {
                _executor = executor;
                _collection = collection;
                _add = add;
            }

            public OxygenType GetParameterType(int position, string name)
            {
                return position == 0 && name == null ? (_add ? _collection.ElementType : OxygenTypes.Int) : null;
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                if (named != null && named.Count != 0)
                {
                    throw new RuntimeFault(_add ? "add expects one positional value." : "remove_at expects one positional index.");
                }

                if (positional == null || positional.Count != 1)
                {
                    throw new RuntimeFault(_add ? "add expects one positional value." : "remove_at expects one positional index.");
                }

                if (_add)
                {
                    if (!_executor.CanStore(_collection.ElementType, positional[0]))
                    {
                        throw new RuntimeFault("The collection element expected " + _collection.ElementType + ".");
                    }

                    _collection.Add(_executor.Coerce(_collection.ElementType, positional[0], ArtiSourceSpan.Empty, "collection element"));
                    return null;
                }

                if (!(positional[0] is int))
                {
                    throw new RuntimeFault("remove_at expects an int.");
                }

                int index = (int)positional[0];
                if (index < 0 || index >= _collection.Count)
                {
                    throw new RuntimeFault("The index is outside the collection.");
                }

                _collection.RemoveAt(index);
                return null;
            }
        }
    }
}
