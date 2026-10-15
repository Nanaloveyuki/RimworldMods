// Constrained function specialization. Runtime values never select trait implementations.
using System;
using System.Collections.Generic;
using System.Text;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private readonly Dictionary<ArtiFunctionDeclarationStatement, OxygenGenericFunction> _genericFunctions =
            new Dictionary<ArtiFunctionDeclarationStatement, OxygenGenericFunction>();
        private readonly Dictionary<string, OxygenTrait> _typeConstraints = new Dictionary<string, OxygenTrait>(StringComparer.Ordinal);

        private sealed class OxygenGenericFunction
        {
            internal OxygenGenericFunction(ArtiFunctionDeclarationStatement function, RuntimeScope scope, RuntimeScope symbolic, OxygenTrait[] constraints)
            { Function = function; Scope = scope; Symbolic = symbolic; Constraints = constraints; }
            internal ArtiFunctionDeclarationStatement Function { get; }
            internal RuntimeScope Scope { get; }
            internal RuntimeScope Symbolic { get; }
            internal OxygenTrait[] Constraints { get; }
            internal Dictionary<string, object> Specializations { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private OxygenGenericFunction PrepareGeneric(ArtiFunctionDeclarationStatement function, RuntimeScope scope)
        {
            OxygenGenericFunction existing;
            if (_genericFunctions.TryGetValue(function, out existing)) return existing;
            var symbolic = new RuntimeScope(scope);
            var constraints = new OxygenTrait[function.TypeParameters.Count];
            for (int i = 0; i < constraints.Length; i++)
            {
                var parameter = function.TypeParameters[i];
                constraints[i] = ResolveTrait(parameter.Constraint, scope);
                var type = new OxygenType(OxygenTypeKind.TypeParameter, parameter.Name, null, false,
                    "generic/" + symbolic.Id + "/" + parameter.Name);
                _typeConstraints[type.Identity] = constraints[i];
                if (!symbolic.TryDeclare(parameter.Name, type, true)) RuntimeError(parameter.Span, "Duplicate generic type parameter.");
            }
            var generic = new OxygenGenericFunction(function, scope, symbolic, constraints);
            _genericFunctions.Add(function, generic);
            CacheParameterTypes(function, symbolic);
            _returnTypes[function] = ResolveReturnType(function, symbolic);
            return generic;
        }

        private bool IsContextualType(ArtiTypeReference reference, RuntimeScope scope)
        {
            if (reference == null) return false;
            if (reference.Name == "Self" || IsContextualType(reference.ElementType, scope)) return true;
            Binding binding;
            return scope != null && scope.TryGetBinding(reference.Name, out binding)
                && (binding.Value is OxygenType || binding.Value is OxygenTypeExport) && _genericFunctions.Count != 0;
        }

        private void ValidateGenericBodies(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            foreach (ArtiStatement statement in statements)
            {
                var function = statement as ArtiFunctionDeclarationStatement;
                if (function == null || function.TypeParameters.Count == 0) continue;
                var generic = _genericFunctions[function];
                AnalyzeTypedFunction(function, generic.Symbolic, false);
            }
        }

        private object SpecializeCall(OxygenGenericFunction generic, ArtiCallExpression call, RuntimeScope scope)
        {
            object cached;
            if (_genericCalls.TryGetValue(call, out cached)) return cached;
            int count = generic.Function.TypeParameters.Count;
            var types = new OxygenType[count];
            if (call.TypeArguments.Count != 0)
            {
                if (call.TypeArguments.Count != count) RuntimeError(call.Span, "Generic type argument count does not match '" + generic.Function.Name + "'.");
                for (int i = 0; i < count; i++) types[i] = ResolveValueType(call.TypeArguments[i], scope, call.Span);
            }
            else
            {
                int position = 0;
                foreach (var argument in call.Arguments)
                {
                    int index = ArgumentIndex(generic.Function, argument.Name, position);
                    if (argument.Name == null) position++;
                    if (index >= 0 && index < generic.Function.Parameters.Count)
                        InferTypeArguments(generic, generic.Function.Parameters[index].Type, StaticExpressionType(argument.Value, scope), types, call.Span);
                }
            }
            var key = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var type = types[i];
                if (type == null || !type.IsStruct || type.Nullable)
                    RuntimeError(call.Span, "Cannot statically determine '" + generic.Function.TypeParameters[i].Name + "'; supply a concrete struct type explicitly.");
                if (!RequireShape(type, call.Span).ImplementedTraits.Contains(generic.Constraints[i].Identity))
                    RuntimeError(call.Span, "Type '" + type + "' does not implement trait '" + generic.Constraints[i].Declaration.Name + "'.");
                key.Append(type.Identity.Length).Append(':').Append(type.Identity);
            }
            string identity = key.ToString();
            if (!generic.Specializations.TryGetValue(identity, out cached))
            {
                var concreteScope = new RuntimeScope(generic.Scope);
                for (int i = 0; i < count; i++) concreteScope.TryDeclare(generic.Function.TypeParameters[i].Name, ExportedTypeValue(types[i]), true);
                var concrete = CloneFunction(generic.Function);
                cached = CreateFunctionCallable(concrete, concreteScope);
                generic.Specializations.Add(identity, cached);
                AnalyzeTypedFunction(concrete, concreteScope, true);
                ValidateAnnotations(concrete.Body.Statements, concreteScope);
            }
            _genericCalls[call] = cached;
            return cached;
        }

        private static int ArgumentIndex(ArtiFunctionDeclarationStatement function, string name, int position)
        {
            if (name == null) return position;
            for (int i = 0; i < function.Parameters.Count; i++) if (function.Parameters[i].Name == name) return i;
            return -1;
        }

        private void InferTypeArguments(OxygenGenericFunction generic, ArtiTypeReference pattern, OxygenType actual, OxygenType[] types, ArtiSourceSpan span)
        {
            if (pattern == null || actual == null || actual.IsAny) return;
            if (pattern.ElementType != null)
            {
                if ((pattern.Name == "list" && actual.Kind == OxygenTypeKind.List) || (pattern.Name == "array" && actual.Kind == OxygenTypeKind.Array))
                    InferTypeArguments(generic, pattern.ElementType, actual.Element, types, span);
                return;
            }
            for (int i = 0; i < generic.Function.TypeParameters.Count; i++)
            {
                if (pattern.Name != generic.Function.TypeParameters[i].Name) continue;
                var inferred = actual.Nullable && pattern.IsNullable ? new OxygenType(actual.Kind, actual.Name, actual.Element, false, actual.Identity) : actual;
                if (types[i] != null && !TypeEquals(types[i], inferred)) RuntimeError(span, "Conflicting inferred types for '" + pattern.Name + "'.");
                types[i] = inferred;
            }
        }

        private object StaticDeclarationValue(ArtiExpression expression, RuntimeScope scope)
        {
            if (expression is ArtiNameExpression name)
            {
                object value;
                if (scope.TryGet(name.Name, out value)) return value;
                for (RuntimeScope current = scope; current != null; current = current.Parent)
                    foreach (var entry in _genericFunctions)
                        if (entry.Key.Name == name.Name && ReferenceEquals(entry.Value.Scope, current)) return entry.Value;
                return null;
            }
            if (expression is ArtiMemberExpression member)
            {
                object target = StaticDeclarationValue(member.Target, scope); object value;
                if (target is OxygenTypeExport type) return TryTypeMember(type, member.Member, out value) ? value : null;
                return target is IOxygenHostValue && TryNamespaceMember(target, member.Member, out value) ? value : null;
            }
            return null;
        }

        private OxygenType StaticExpressionType(ArtiExpression expression, RuntimeScope scope, bool capabilities = false)
        {
            if (expression == null) return OxygenTypes.Any;
            if (expression is ArtiNameExpression name)
            {
                Binding binding;
                return scope.TryGetBinding(name.Name, out binding) ? capabilities ? binding.AnalysisType ?? binding.Type : binding.Type : OxygenTypes.Any;
            }
            if (expression is ArtiLiteralExpression literal)
            {
                if (literal.Value is int) return OxygenTypes.Int;
                if (literal.Value is double) return OxygenTypes.Float;
                if (literal.Value is bool) return OxygenTypes.Bool;
                if (literal.Value is string) return OxygenTypes.String;
                return OxygenTypes.Any;
            }
            if (expression is ArtiIndexExpression index)
            {
                var collection = StaticExpressionType(index.Target, scope, capabilities);
                return collection.Element ?? OxygenTypes.Any;
            }
            if (expression is ArtiMemberExpression member)
            {
                var receiver = StaticExpressionType(member.Target, scope, capabilities);
                if (receiver.IsStruct)
                {
                    var shape = RequireShape(receiver, member.Span);
                    return FieldType(shape, member.Member) ?? OxygenTypes.Any;
                }
                return OxygenTypes.Any;
            }
            if (expression is ArtiCallExpression call)
            {
                object target = StaticDeclarationValue(call.Target, scope);
                if (target is OxygenTypeExport type) return type.Type;
                if (target is RuntimeCallable callable && callable.Function != null) return ResolveReturnType(callable.Function, callable.Closure);
                if (call.Target is ArtiMemberExpression staticCall)
                {
                    object owner = StaticDeclarationValue(staticCall.Target, scope);
                    var staticType = owner as OxygenType;
                    if (staticType != null && staticType.Kind == OxygenTypeKind.TypeParameter)
                    {
                        var method = ContractMethod(staticType, staticCall.Member, true, call.Span, true);
                        return ContractReturnType(method, staticType, scope, call.Span);
                    }
                }
                if (target is OxygenGenericFunction generic)
                {
                    // Symbolic calls retain the generic return contract; concrete calls specialize before evaluation.
                    if (call.TypeArguments.Count != 0 && call.TypeArguments.Count == generic.Function.TypeParameters.Count)
                    {
                        var resultScope = new RuntimeScope(generic.Scope);
                        for (int i = 0; i < call.TypeArguments.Count; i++) resultScope.TryDeclare(generic.Function.TypeParameters[i].Name,
                            ResolveType(call.TypeArguments[i], scope, call.Span), true);
                        return ResolveReturnType(generic.Function, resultScope);
                    }
                }
                if (call.Target is ArtiMemberExpression methodCall)
                {
                    var receiver = StaticExpressionType(methodCall.Target, scope, capabilities);
                    ArtiFunctionDeclarationStatement signature = ContractMethod(receiver, methodCall.Member, false, methodCall.Span, false);
                    if (signature != null)
                    {
                        return ContractReturnType(signature, receiver, scope, call.Span);
                    }
                }
            }
            return OxygenTypes.Any;
        }

        private OxygenType ContractReturnType(ArtiFunctionDeclarationStatement signature, OxygenType receiver, RuntimeScope scope, ArtiSourceSpan span)
        {
            OxygenTrait trait;
            RuntimeScope definition = receiver.IsStruct ? (RuntimeScope)RequireShape(receiver, span).DeclarationScope
                : _typeConstraints.TryGetValue(receiver.Identity, out trait) ? trait.Scope : scope;
            return ResolveTypeUncached(signature.ReturnType ?? AnyReference(span), TypeScope(definition, receiver), span);
        }

        private ArtiFunctionDeclarationStatement ContractMethod(OxygenType receiver, string member, bool isStatic, ArtiSourceSpan span, bool required)
        {
            if (receiver.Kind == OxygenTypeKind.TypeParameter)
            {
                OxygenTrait trait;
                if (_typeConstraints.TryGetValue(receiver.Identity, out trait))
                    foreach (var method in trait.Declaration.Methods) if (method.Name == member && method.IsStatic == isStatic) return method;
                if (required) RuntimeError(span, "Member '" + member + "' is not declared by the generic trait constraint.");
            }
            else if (receiver.IsStruct)
            {
                var shape = RequireShape(receiver, span);
                if (!isStatic)
                {
                    var method = FindMethod(shape, member);
                    if (method != null) return method.Function;
                }
                else
                {
                    object callable;
                    if (shape.StaticMethods.TryGetValue(member, out callable) && callable is RuntimeCallable runtime) return runtime.Function;
                }
            }
            return null;
        }

        private void AnalyzeTypedFunction(ArtiFunctionDeclarationStatement function, RuntimeScope types, bool bind)
        {
            var locals = new RuntimeScope(types);
            foreach (var parameter in function.Parameters)
            {
                locals.TryDeclare(parameter.Name, null, false);
                Binding binding;
                if (locals.TryGetBinding(parameter.Name, out binding)) binding.Type = ResolveValueType(parameter.Type, types, parameter.Span);
            }
            AnalyzeTypedStatements(function.Body == null ? null : function.Body.Statements, locals, bind);
        }

        private void AnalyzeTypedStatements(IList<ArtiStatement> statements, RuntimeScope scope, bool bind)
        {
            if (statements == null) return;
            foreach (var statement in statements)
            {
                if (statement is ArtiVariableDeclarationStatement variable)
                {
                    AnalyzeTypedExpression(variable.Value, scope, bind);
                    var source = StaticExpressionType(variable.Value, scope, true);
                    scope.TryDeclare(variable.Name, null, variable.IsConst, true);
                    Binding binding;
                    if (scope.TryGetBinding(variable.Name, out binding))
                    {
                        binding.Type = ResolveValueType(variable.Type, scope, variable.Span);
                        if (binding.Type.IsAny && (ContainsTypeParameter(source) || bind && source.IsStruct)) binding.AnalysisType = source;
                    }
                }
                else if (statement is ArtiAssignmentStatement assignment)
                {
                    AnalyzeTypedExpression(assignment.Target, scope, bind);
                    AnalyzeTypedExpression(assignment.Value, scope, bind);
                    if (assignment.Target is ArtiNameExpression assigned && scope.TryGetBinding(assigned.Name, out Binding destination)
                        && destination.Type.IsAny)
                    {
                        var source = StaticExpressionType(assignment.Value, scope, true);
                        destination.AnalysisType = ContainsTypeParameter(source) || bind && source.IsStruct ? source : null;
                    }
                }
                else if (statement is ArtiExpressionStatement expression) AnalyzeTypedExpression(expression.Expression, scope, bind);
                else if (statement is ArtiReturnStatement returned) AnalyzeTypedExpression(returned.Value, scope, bind);
                else if (statement is ArtiIfStatement conditional)
                {
                    foreach (var branch in conditional.Branches) { AnalyzeTypedExpression(branch.Condition, scope, bind); AnalyzeTypedStatements(branch.Body.Statements, scope, bind); }
                    if (conditional.ElseBody != null) AnalyzeTypedStatements(conditional.ElseBody.Statements, scope, bind);
                }
                else if (statement is ArtiForStatement loop)
                {
                    AnalyzeTypedExpression(loop.Source, scope, bind);
                    scope.TryDeclare(loop.VariableName, null, false, true);
                    Binding binding; if (scope.TryGetBinding(loop.VariableName, out binding)) binding.Type = StaticExpressionType(loop.Source, scope).Element ?? OxygenTypes.Any;
                    AnalyzeTypedStatements(loop.Body.Statements, scope, bind);
                }
                else if (statement is ArtiWhileStatement loopWhile) { AnalyzeTypedExpression(loopWhile.Condition, scope, bind); AnalyzeTypedStatements(loopWhile.Body.Statements, scope, bind); }
                else if (statement is ArtiBlockStatement block) AnalyzeTypedStatements(block.Statements, scope, bind);
                else if (statement is ArtiFunctionDeclarationStatement nested) AnalyzeTypedFunction(nested, scope, bind);
            }
        }

        private static bool ContainsTypeParameter(OxygenType type)
        { return type != null && (type.Kind == OxygenTypeKind.TypeParameter || ContainsTypeParameter(type.Element)); }

        private void AnalyzeTypedExpression(ArtiExpression expression, RuntimeScope scope, bool bind)
        {
            if (expression == null) return;
            if (expression is ArtiMemberExpression member)
            {
                AnalyzeTypedExpression(member.Target, scope, bind);
                object target = StaticDeclarationValue(member.Target, scope);
                var type = target as OxygenType; var export = target as OxygenTypeExport;
                bool isStatic = type != null || export != null;
                var receiver = type ?? (export == null ? StaticExpressionType(member.Target, scope, true) : export.Type);
                var signature = ContractMethod(receiver, member.Member, isStatic, member.Span, receiver.Kind == OxygenTypeKind.TypeParameter);
                if (bind && signature != null && receiver.IsStruct)
                {
                    var shape = RequireShape(receiver, member.Span);
                    if (isStatic) _directStatics[member] = shape.StaticMethods[member.Member];
                    else _directMethods[member] = FindMethod(shape, member.Member);
                }
            }
            else if (expression is ArtiCallExpression call)
            {
                AnalyzeTypedExpression(call.Target, scope, bind);
                foreach (var argument in call.Arguments) AnalyzeTypedExpression(argument.Value, scope, bind);
                foreach (var type in call.TypeArguments) ResolveValueType(type, scope, call.Span);
                object target = StaticDeclarationValue(call.Target, scope);
                if (target is OxygenGenericFunction generic)
                {
                    if (bind) SpecializeCall(generic, call, scope);
                    else ValidateSymbolicGenericCall(generic, call, scope);
                }
            }
            else if (expression is ArtiIndexExpression index) { AnalyzeTypedExpression(index.Target, scope, bind); AnalyzeTypedExpression(index.Index, scope, bind); }
            else if (expression is ArtiArrayExpression array) foreach (var item in array.Items) AnalyzeTypedExpression(item, scope, bind);
            else if (expression is ArtiObjectExpression obj) foreach (var item in obj.Members) AnalyzeTypedExpression(item.Value, scope, bind);
            else if (expression is ArtiInterpolatedStringExpression text) foreach (var part in text.Parts) AnalyzeTypedExpression(part, scope, bind);
            else if (expression is ArtiUnaryExpression unary) AnalyzeTypedExpression(unary.Operand, scope, bind);
            else if (expression is ArtiBinaryExpression binary) { AnalyzeTypedExpression(binary.Left, scope, bind); AnalyzeTypedExpression(binary.Right, scope, bind); }
        }

        private void ValidateSymbolicGenericCall(OxygenGenericFunction generic, ArtiCallExpression call, RuntimeScope scope)
        {
            var types = new OxygenType[generic.Function.TypeParameters.Count];
            if (call.TypeArguments.Count != 0)
            {
                if (call.TypeArguments.Count != types.Length) RuntimeError(call.Span, "Generic type argument count does not match.");
                for (int i = 0; i < types.Length; i++) types[i] = ResolveValueType(call.TypeArguments[i], scope, call.Span);
            }
            else
            {
                int position = 0;
                foreach (var argument in call.Arguments)
                {
                    int index = ArgumentIndex(generic.Function, argument.Name, position);
                    if (argument.Name == null) position++;
                    if (index >= 0 && index < generic.Function.Parameters.Count)
                        InferTypeArguments(generic, generic.Function.Parameters[index].Type, StaticExpressionType(argument.Value, scope), types, call.Span);
                }
            }
            for (int i = 0; i < types.Length; i++)
            {
                OxygenTrait contract;
                var type = types[i];
                bool valid = type != null && !type.Nullable && (type.Kind == OxygenTypeKind.TypeParameter
                    ? _typeConstraints.TryGetValue(type.Identity, out contract) && contract.Identity == generic.Constraints[i].Identity
                    : type.IsStruct && RequireShape(type, call.Span).ImplementedTraits.Contains(generic.Constraints[i].Identity));
                if (!valid) RuntimeError(call.Span, "The generic call cannot satisfy trait '" + generic.Constraints[i].Declaration.Name + "' statically.");
            }
        }
    }
}
