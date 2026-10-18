// Nominal trait contracts and static/inherent method preparation.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private readonly Dictionary<ArtiFunctionDeclarationStatement, RuntimeScope> _functionScopes =
            new Dictionary<ArtiFunctionDeclarationStatement, RuntimeScope>();
        private readonly Dictionary<ArtiCallExpression, object> _genericCalls = new Dictionary<ArtiCallExpression, object>();
        private readonly Dictionary<ArtiMemberExpression, OxygenMethod> _directMethods = new Dictionary<ArtiMemberExpression, OxygenMethod>();
        private readonly Dictionary<ArtiMemberExpression, object> _directStatics = new Dictionary<ArtiMemberExpression, object>();

        private sealed class OxygenTrait
        {
            internal OxygenTrait(string identity, ArtiTraitDeclarationStatement declaration, RuntimeScope scope)
            { Identity = identity; Declaration = declaration; Scope = scope; }
            internal string Identity { get; }
            internal ArtiTraitDeclarationStatement Declaration { get; }
            internal RuntimeScope Scope { get; }
        }

        private RuntimeScope TypeScope(RuntimeScope parent, OxygenType type)
        {
            var scope = new RuntimeScope(parent);
            scope.TryDeclare("Self", new OxygenTypeExport(type, type.IsStruct ? CreateStructConstructor(type) : null), true);
            return scope;
        }

        private object FindDeclaration(string name, RuntimeScope scope, ArtiSourceSpan span)
        {
            int dot = name.LastIndexOf('.');
            object value;
            if (dot > 0)
            {
                object ns;
                if (scope.TryGet(name.Substring(0, dot), out ns) && TryNamespaceMember(ns, name.Substring(dot + 1), out value)) return value;
            }
            else if (scope.TryGet(name, out value)) return value;
            RuntimeError(span, "The declaration '" + name + "' is not available.");
            return null;
        }

        private OxygenTrait ResolveTrait(ArtiTypeReference reference, RuntimeScope scope)
        {
            if (reference == null || reference.ElementType != null || reference.IsNullable)
                RuntimeError(reference == null ? ArtiSourceSpan.Empty : reference.Span, "A constraint must name a non-null trait.");
            var trait = FindDeclaration(reference.Name, scope, reference.Span) as OxygenTrait;
            if (trait == null) RuntimeError(reference.Span, "The name '" + reference.Name + "' is not a trait.");
            return trait;
        }

        private void PredeclareTraits(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            foreach (ArtiStatement statement in statements)
            {
                var declaration = statement as ArtiTraitDeclarationStatement;
                if (declaration == null) continue;
                string package = string.IsNullOrEmpty(scope.ModulePackageId) ? "local" : scope.ModulePackageId;
                string module = string.IsNullOrEmpty(scope.ModulePath) ? LocalScopeKey(scope) : scope.ModulePath;
                var trait = new OxygenTrait(StructIdentity(package, module, declaration.Name), declaration, scope);
                if (IsReservedDeclarationName(declaration.Name) || !scope.TryDeclare(declaration.Name, trait, true, false, declaration))
                    RuntimeError(declaration.Span, "The trait name '" + declaration.Name + "' is already declared or reserved.");
            }
        }

        private void PrepareTraitsAndImpls(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            foreach (ArtiStatement statement in statements)
            {
                var declaration = statement as ArtiTraitDeclarationStatement;
                if (declaration == null) continue;
                var trait = (OxygenTrait)FindDeclaration(declaration.Name, scope, declaration.Span);
                var symbolic = new OxygenType(OxygenTypeKind.TypeParameter, "Self", null, false, trait.Identity + "/Self");
                var contractScope = TypeScope(scope, symbolic);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var signature in declaration.Methods)
                {
                    if (!names.Add(signature.Name) || signature.Name == "self" || signature.Name == "Self")
                        RuntimeError(signature.Span, "Duplicate or reserved trait member '" + signature.Name + "'.");
                    if (signature.Body != null || signature.TypeParameters.Count != 0 || signature.ReturnType == null)
                        RuntimeError(signature.Span, "Trait methods require explicit signatures without bodies or generics.");
                    foreach (var parameter in signature.Parameters)
                        if (parameter.Type == null || parameter.Name == "self") RuntimeError(parameter.Span, "Trait parameters require explicit types and cannot be named self.");
                    CacheParameterTypes(signature, contractScope);
                    _returnTypes[signature] = ResolveReturnType(signature, contractScope);
                }
            }
            foreach (ArtiStatement statement in statements)
            {
                var implementation = statement as ArtiImplDeclarationStatement;
                if (implementation == null) continue;
                var type = ResolveValueType(implementation.Target, scope, implementation.Span);
                if (!type.IsStruct || type.Nullable) RuntimeError(implementation.Span, "An impl target must be a non-null local struct.");
                var shape = RequireShape(type, implementation.Span);
                if (!ReferenceEquals(shape.DeclarationScope, scope)) RuntimeError(implementation.Span, "An impl must be declared in the struct's defining module.");
                OxygenTrait trait = implementation.Trait == null ? null : ResolveTrait(implementation.Trait, scope);
                if (trait != null && !shape.ImplementedTraits.Add(trait.Identity)) RuntimeError(implementation.Span, "The trait is already implemented for '" + type + "'.");
                if (trait != null) ValidateImplementation(trait, implementation, type, scope);
                foreach (var method in implementation.Methods) PrepareMethod(shape, method, scope);
            }
        }

        private void ValidateImplementation(OxygenTrait trait, ArtiImplDeclarationStatement implementation, OxygenType target, RuntimeScope scope)
        {
            var required = new Dictionary<string, ArtiFunctionDeclarationStatement>(StringComparer.Ordinal);
            foreach (var method in trait.Declaration.Methods) required.Add(method.Name, method);
            var actualScope = TypeScope(scope, target);
            var expectedScope = TypeScope(trait.Scope, target);
            foreach (var method in implementation.Methods)
            {
                ArtiFunctionDeclarationStatement signature;
                if (!required.TryGetValue(method.Name, out signature)) RuntimeError(method.Span, "Unknown or duplicate trait implementation member '" + method.Name + "'.");
                required.Remove(method.Name);
                bool matches = method.IsStatic == signature.IsStatic && method.Parameters.Count == signature.Parameters.Count;
                if (matches)
                {
                    for (int i = 0; i < method.Parameters.Count; i++)
                    {
                        var parameter = method.Parameters[i]; var expected = signature.Parameters[i];
                        matches &= parameter.Name == expected.Name && TypeEquals(
                            ResolveTypeUncached(parameter.Type ?? AnyReference(parameter.Span), actualScope, parameter.Span),
                            ResolveTypeUncached(expected.Type, expectedScope, expected.Span));
                    }
                    matches &= TypeEquals(ResolveTypeUncached(method.ReturnType ?? AnyReference(method.Span), actualScope, method.Span),
                        ResolveTypeUncached(signature.ReturnType, expectedScope, signature.Span));
                }
                if (!matches) RuntimeError(method.Span, "The implementation signature for '" + method.Name + "' does not match trait '" + trait.Declaration.Name + "'.");
            }
            if (required.Count != 0) foreach (var missing in required) RuntimeError(implementation.Span, "Missing trait implementation member '" + missing.Key + "'.");
        }

        private static ArtiTypeReference AnyReference(ArtiSourceSpan span) { return new ArtiTypeReference(span, "any"); }

        private void PrepareMethod(OxygenStructShape shape, ArtiFunctionDeclarationStatement method, RuntimeScope scope)
        {
            if (method.Name == "self" || method.Name == "Self" || FieldIndex(shape, method.Name) >= 0
                || FindMethod(shape, method.Name) != null || shape.StaticMethods.ContainsKey(method.Name))
                RuntimeError(method.Span, "The struct member '" + method.Name + "' is declared more than once.");
            if (method.TypeParameters.Count != 0) RuntimeError(method.Span, "Generic methods are not supported; use a generic function.");
            ValidateMethodParameters(method);
            var methodScope = TypeScope(scope, shape.Type);
            _functionScopes[method] = methodScope;
            _returnTypes[method] = ResolveReturnType(method, methodScope);
            CacheParameterTypes(method, methodScope);
            if (method.IsStatic) shape.StaticMethods.Add(method.Name, CreateFunctionCallable(method, methodScope));
            else shape.Methods.Add(new OxygenMethod(method.Name, method, methodScope));
            ValidateAnnotations(method.Body == null ? null : method.Body.Statements, methodScope);
        }

        private object CreateFunctionCallable(ArtiFunctionDeclarationStatement function, RuntimeScope scope)
        {
            if (function.TypeParameters.Count != 0) return PrepareGeneric(function, scope);
            _functionScopes[function] = scope;
            _returnTypes[function] = ResolveReturnType(function, scope);
            CacheParameterTypes(function, scope);
            return new RuntimeCallable(arguments => InvokeUserFunction(function, arguments, scope), function.Name,
                (position, name) => ParameterType(function, position, name)) { Function = function, Closure = scope };
        }

        private object ExportedTypeValue(OxygenType type)
        {
            return new OxygenTypeExport(type, type.IsStruct ? CreateStructConstructor(type) : null);
        }

        private bool TryTypeMember(OxygenTypeExport type, string member, out object value)
        {
            value = null;
            OxygenStructShape shape;
            return type != null && _structs.TryGetValue(type.Type.Identity, out shape) && shape.StaticMethods.TryGetValue(member, out value);
        }
    }
}
