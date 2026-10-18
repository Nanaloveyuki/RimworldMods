// Oxygen module loading and builtin std extensions. Shares one executor budget.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Oxygendsl.Core;
namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private readonly Dictionary<string, ModuleRecord> _modules =
            new Dictionary<string, ModuleRecord>(StringComparer.Ordinal);
        private readonly Stack<string> _moduleChain = new Stack<string>();

        internal object ResolveModule(ArtiUseStatement use, RuntimeScope importingScope)
        {
            if (use == null)
            {
                RuntimeError(ArtiSourceSpan.Empty, "A module import is missing.");
            }

            string packageId = use.PackageId ?? string.Empty;
            string modulePath = use.ModulePath ?? string.Empty;
            if (packageId.Length == 0 || modulePath.Length == 0)
            {
                RuntimeError(use.Span, "Module '" + modulePath + "' from '" + packageId + "' is missing.");
            }

            string key = ModuleKey(packageId, modulePath);
            ModuleRecord cached;
            if (_modules.TryGetValue(key, out cached))
            {
                if (cached.Loading)
                {
                    RuntimeError(use.Span, CycleMessage(packageId, modulePath));
                }

                if (!cached.Available && !use.Optional)
                {
                    RuntimeError(use.Span, "Module '" + modulePath + "' from '" + packageId + "' is not available.");
                }

                return cached.Namespace;
            }

            if (IsStd(packageId))
            {
                return LoadStd(modulePath, use.Span, key, use.Optional);
            }

            IOxygenModuleProvider hostModules = _context == null ? null : _context.HostModules;
            if (hostModules != null)
            {
                IOxygenHostValue hostModule;
                try
                {
                    if (hostModules.TryGetModule(packageId, modulePath, out hostModule) && hostModule != null)
                    {
                        return CacheHost(key, hostModule);
                    }
                }
                catch (Exception exception)
                {
                    if (exception is ExecutionStopException) throw;
                    RuntimeError(use.Span, "Module '" + modulePath + "' from '" + packageId + "' could not be read: " + exception.Message);
                }
            }

            IOxygenModuleCatalog catalog = _context == null ? null : _context.Modules;
            string source = null;
            try
            {
                bool found = catalog != null && catalog.TryGetSource(packageId, modulePath, out source) && source != null;
                if (!found)
                {
                    if (use.Optional)
                    {
                        return OptionalModule(modulePath, packageId, key);
                    }

                    RuntimeError(use.Span, "Module '" + modulePath + "' from '" + packageId + "' is not available.");
                }
            }
            catch (Exception exception)
            {
                if (exception is ExecutionStopException) throw;
                RuntimeError(use.Span, "Module '" + modulePath + "' from '" + packageId + "' could not be read: " + exception.Message);
            }

            return LoadScript(packageId, modulePath, source, use.Span, key);
        }

        private object OptionalModule(string modulePath, string packageId, string key)
        {
            var unavailable = new DictionaryOxygenHostValue(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "available", false },
                { "module", modulePath },
                { "provider", packageId }
            });
            _modules[key] = new ModuleRecord(unavailable, false);
            return unavailable;
        }

        private object LoadStd(string modulePath, ArtiSourceSpan span, string key, bool optional)
        {
            IOxygenStandardModuleProvider standard = _context == null ? null : _context.StandardModules;
            if (standard != null && modulePath != "math" && modulePath != "logic" && modulePath != "cs")
            {
                IOxygenHostValue provided;
                try
                {
                    if (standard.TryGetModule(modulePath, out provided) && provided != null)
                    {
                        return CacheHost(key, provided);
                    }
                }
                catch (Exception exception)
                {
                    if (exception is ExecutionStopException) throw;
                    RuntimeError(span, "Module '" + modulePath + "' from 'std' could not be read: " + exception.Message);
                }
            }

            RuntimeObject ns = CreateNamespace();
            switch (modulePath)
            {
                case "math":
                    NamespaceSet(ns, "clamp", new MathClampCallable());
                    break;
                case "logic":
                    NamespaceSet(ns, "all", new LogicAllCallable(this));
                    NamespaceSet(ns, "any", new LogicAnyCallable(this));
                    break;
                case "cs":
                    CreateNativeInt32(0);
                    ExportStructType(ns, "Int32", NativeInt32Type, new RejectedCallable("cs.Int32 cannot be constructed; use cs.int32."));
                    NamespaceSet(ns, "int32", new CsInt32Callable(this));
                    break;
                default:
                    if (optional)
                    {
                        return OptionalModule(modulePath, "std", key);
                    }

                    RuntimeError(span, "Module '" + modulePath + "' from 'std' is not available.");
                    break;
            }

            return CacheHost(key, ns);
        }

        private object CacheHost(string key, IOxygenHostValue ns)
        {
            bool isAvailable = ns != null;
            if (isAvailable) ns = new AvailableHostValue(ns);

            _modules[key] = new ModuleRecord(ns, isAvailable);
            return ns;
        }

        private sealed class AvailableHostValue : IOxygenHostValue
        {
            private readonly IOxygenHostValue _inner;

            internal AvailableHostValue(IOxygenHostValue inner)
            {
                _inner = inner;
            }

            public bool TryGetMember(string name, out object value)
            {
                if (string.Equals(name, "available", StringComparison.Ordinal))
                {
                    value = true;
                    return true;
                }

                if (_inner == null)
                {
                    value = null;
                    return false;
                }

                return _inner.TryGetMember(name, out value);
            }
        }

        private object LoadScript(string packageId, string modulePath, string source, ArtiSourceSpan span, string key)
        {
            var record = new ModuleRecord(null);
            record.Loading = true;
            _modules[key] = record;
            _moduleChain.Push(packageId + "/" + modulePath);
            bool failed = true;
            try
            {
                ArtiParseResult parsed = new ArtiParser().Parse(source ?? string.Empty);
                if (parsed == null || HasErrors(parsed.Diagnostics))
                {
                    if (parsed != null) AddModuleDiagnostics(parsed.Diagnostics, modulePath, packageId);
                    RuntimeError(span, "Module '" + modulePath + "' from '" + packageId + "' failed to parse.");
                }

                ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program, true);
                if (analysis == null || HasErrors(analysis.Diagnostics))
                {
                    if (analysis != null) AddModuleDiagnostics(analysis.Diagnostics, modulePath, packageId);
                    RuntimeError(span, "Module '" + modulePath + "' from '" + packageId + "' failed analysis.");
                }

                RejectLibraryShape(parsed.Program, packageId, modulePath);
                RejectLibraryDeclarations(parsed.Program, packageId, modulePath);
                RuntimeScope scope = CreateLibraryScope();
                SetModuleIdentity(scope, packageId, modulePath);
                RuntimeObject ns = CreateNamespace();
                record.Namespace = ns;
                PrepareDeclarations(parsed.Program.Statements, scope);
                ExportScript(parsed.Program, scope, ns, packageId, modulePath);
                ExecuteStatements(parsed.Program.Statements, scope);
                PublishExports(parsed.Program, scope, ns, packageId, modulePath);
                record.Loading = false;
                failed = false;
                RecordImport(packageId, modulePath, source);
                return CacheHost(key, ns);
            }
            catch (ExecutionStopException)
            {
                throw;
            }
            catch (RuntimeFault fault)
            {
                RuntimeError(span, "Module '" + modulePath + "' from '" + packageId + "' failed: " + fault.Message);
                return null;
            }
            finally
            {
                if (_moduleChain.Count > 0) _moduleChain.Pop();
                if (failed) _modules.Remove(key);
            }
        }

        private void RecordImport(string packageId, string modulePath, string source)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
                var text = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    text.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                _imports.Add(new ContentImport(packageId, modulePath, text.ToString()));
            }
        }

        private void AddModuleDiagnostics(IEnumerable<ArtiDiagnostic> diagnostics, string modulePath, string packageId)
        {
            if (diagnostics == null) return;
            string prefix = (modulePath ?? string.Empty) + "@" + (packageId ?? string.Empty) + ": ";
            foreach (ArtiDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null) continue;
                AddDiagnostic(new ArtiDiagnostic(
                    diagnostic.Severity,
                    diagnostic.Code,
                    prefix + diagnostic.MessageZhCn,
                    prefix + diagnostic.MessageEn,
                    diagnostic.Span));
            }
        }

        private static object ConstructorOf(object value)
        {
            OxygenTypeExport exported = value as OxygenTypeExport;
            return exported == null ? null : exported.Constructor;
        }

        private void ExportScript(ArtiProgram program, RuntimeScope scope, RuntimeObject ns, string packageId, string modulePath)
        {
            if (program == null)
            {
                return;
            }

            foreach (ArtiStatement statement in program.Statements)
            {
                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null && function.IsPublic)
                {
                    NamespaceSet(ns, function.Name, CreateExportedFunction(function, scope));
                    continue;
                }
                if (statement is ArtiTraitDeclarationStatement trait && trait.IsPublic)
                {
                    NamespaceSet(ns, trait.Name, FindDeclaration(trait.Name, scope, trait.Span));
                    continue;
                }

                ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
                if (declaration != null && declaration.IsPublic)
                {
                    object value;
                    if (!scope.TryGet(declaration.Name, out value) || ConstructorOf(value) == null)
                    {
                        RuntimeError(declaration.Span, "Struct '" + declaration.Name + "' in module '" + modulePath
                            + "' from '" + packageId + "' was not prepared.");
                    }

                    NamespaceSet(ns, declaration.Name, value);
                }
            }
        }

        private void PublishExports(ArtiProgram program, RuntimeScope scope, RuntimeObject ns, string packageId, string modulePath)
        {
            if (program == null)
            {
                return;
            }

            foreach (ArtiStatement statement in program.Statements)
            {
                ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
                if (variable == null || !variable.IsConst || !variable.IsPublic || variable.Name == "_")
                {
                    continue;
                }

                object value;
                if (!scope.TryGet(variable.Name, out value))
                {
                    RuntimeError(variable.Span, "Exported const '" + variable.Name + "' in module '" + modulePath
                        + "' from '" + packageId + "' was not initialized.");
                }

                NamespaceSet(ns, variable.Name, CreateExportedConst(value));
            }
        }

        private void RejectLibraryShape(ArtiProgram program, string packageId, string modulePath)
        {
            if (program == null || program.Statements == null)
            {
                return;
            }

            bool sawBody = false;
            foreach (ArtiStatement statement in program.Statements)
            {
                if (statement == null)
                {
                    continue;
                }

                if (statement is ArtiUseStatement)
                {
                    if (sawBody)
                    {
                        RuntimeError(statement.Span, "Module '" + modulePath + "' from '" + packageId
                            + "' has an import after another declaration.");
                    }

                    continue;
                }

                sawBody = true;
                if (statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement) continue;
                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    continue;
                }

                ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
                if (declaration != null)
                {
                    continue;
                }

                ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
                if (variable != null)
                {
                    if (variable.IsPublic && !variable.IsConst)
                    {
                        RuntimeError(variable.Span, "Module '" + modulePath + "' from '" + packageId
                            + "' cannot export mutable '" + variable.Name + "'.");
                    }

                    continue;
                }

                RuntimeError(statement.Span, "Module '" + modulePath + "' from '" + packageId
                    + "' can only contain imports, functions, structs, traits, impls, and bindings.");
            }
        }

        private static void RejectLibraryDeclarations(ArtiProgram program, string packageId, string modulePath)
        {
            if (program == null || program.Statements == null) return;
            foreach (ArtiStatement statement in program.Statements)
            {
                RejectDeclarationCalls(statement, packageId, modulePath);
            }
        }

        private static void RejectDeclarationCalls(ArtiStatement statement, string packageId, string modulePath)
        {
            if (statement == null) return;
            ArtiExpressionStatement expression = statement as ArtiExpressionStatement;
            if (expression != null)
            {
                RejectDeclarationCalls(expression.Expression, packageId, modulePath);
                return;
            }
            ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
            if (function != null && function.Body != null)
            {
                foreach (ArtiStatement nested in function.Body.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
                return;
            }
            ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
            if (variable != null)
            {
                RejectDeclarationCalls(variable.Value, packageId, modulePath);
                return;
            }
            ArtiStructDeclarationStatement structure = statement as ArtiStructDeclarationStatement;
            if (structure != null)
            {
                if (structure.Fields != null)
                {
                    foreach (ArtiStructField field in structure.Fields)
                    {
                        RejectDeclarationCalls(field == null ? null : field.DefaultValue, packageId, modulePath);
                    }
                }
                if (structure.Methods != null)
                {
                    foreach (ArtiFunctionDeclarationStatement method in structure.Methods)
                    {
                        RejectDeclarationCalls(method, packageId, modulePath);
                    }
                }
                return;
            }
            if (statement is ArtiImplDeclarationStatement implementation)
            {
                foreach (var method in implementation.Methods) RejectDeclarationCalls(method, packageId, modulePath);
                return;
            }
            ArtiIfStatement conditional = statement as ArtiIfStatement;
            if (conditional != null)
            {
                if (conditional.Branches != null)
                {
                    foreach (ArtiIfBranch branch in conditional.Branches)
                    {
                        if (branch == null) continue;
                        RejectDeclarationCalls(branch.Condition, packageId, modulePath);
                        if (branch.Body != null)
                        {
                            foreach (ArtiStatement nested in branch.Body.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
                        }
                    }
                }
                if (conditional.ElseBody != null)
                {
                    foreach (ArtiStatement nested in conditional.ElseBody.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
                }
                return;
            }
            ArtiWhileStatement loop = statement as ArtiWhileStatement;
            if (loop != null)
            {
                RejectDeclarationCalls(loop.Condition, packageId, modulePath);
                if (loop.Body != null)
                {
                    foreach (ArtiStatement nested in loop.Body.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
                }
                return;
            }
            ArtiForStatement iteration = statement as ArtiForStatement;
            if (iteration != null)
            {
                RejectDeclarationCalls(iteration.Source, packageId, modulePath);
                if (iteration.Body != null)
                {
                    foreach (ArtiStatement nested in iteration.Body.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
                }
                return;
            }
            ArtiReturnStatement returned = statement as ArtiReturnStatement;
            if (returned != null) RejectDeclarationCalls(returned.Value, packageId, modulePath);
            ArtiAssignmentStatement assignment = statement as ArtiAssignmentStatement;
            if (assignment != null)
            {
                RejectDeclarationCalls(assignment.Target, packageId, modulePath);
                RejectDeclarationCalls(assignment.Value, packageId, modulePath);
            }
            ArtiBlockStatement block = statement as ArtiBlockStatement;
            if (block != null && block.Statements != null)
            {
                foreach (ArtiStatement nested in block.Statements) RejectDeclarationCalls(nested, packageId, modulePath);
            }
        }

        private static void RejectDeclarationCalls(ArtiExpression expression, string packageId, string modulePath)
        {
            if (expression == null) return;
            ArtiCallExpression call = expression as ArtiCallExpression;
            if (call != null)
            {
                ArtiNameExpression name = call.Target as ArtiNameExpression;
                if (name != null && OxygenDeclarations.IsName(name.Name))
                {
                    throw new RuntimeFault("Module '" + modulePath + "' from '" + packageId
                        + "' cannot call declaration '" + name.Name + "'.");
                }
                RejectDeclarationCalls(call.Target, packageId, modulePath);
                if (call.Arguments != null)
                {
                    foreach (ArtiArgument argument in call.Arguments)
                    {
                        if (argument != null) RejectDeclarationCalls(argument.Value, packageId, modulePath);
                    }
                }
                return;
            }
            ArtiMemberExpression member = expression as ArtiMemberExpression;
            if (member != null)
            {
                RejectDeclarationCalls(member.Target, packageId, modulePath);
                return;
            }
            ArtiIndexExpression index = expression as ArtiIndexExpression;
            if (index != null)
            {
                RejectDeclarationCalls(index.Target, packageId, modulePath);
                RejectDeclarationCalls(index.Index, packageId, modulePath);
                return;
            }
            ArtiArrayExpression array = expression as ArtiArrayExpression;
            if (array != null && array.Items != null)
            {
                foreach (ArtiExpression item in array.Items) RejectDeclarationCalls(item, packageId, modulePath);
                return;
            }
            ArtiObjectExpression obj = expression as ArtiObjectExpression;
            if (obj != null && obj.Members != null)
            {
                foreach (ArtiObjectMember item in obj.Members)
                {
                    if (item != null) RejectDeclarationCalls(item.Value, packageId, modulePath);
                }
                return;
            }
            ArtiUnaryExpression unary = expression as ArtiUnaryExpression;
            if (unary != null)
            {
                RejectDeclarationCalls(unary.Operand, packageId, modulePath);
                return;
            }
            ArtiBinaryExpression binary = expression as ArtiBinaryExpression;
            if (binary != null)
            {
                RejectDeclarationCalls(binary.Left, packageId, modulePath);
                RejectDeclarationCalls(binary.Right, packageId, modulePath);
            }
            ArtiInterpolatedStringExpression interpolated = expression as ArtiInterpolatedStringExpression;
            if (interpolated != null && interpolated.Parts != null)
            {
                foreach (ArtiExpression part in interpolated.Parts) RejectDeclarationCalls(part, packageId, modulePath);
            }
        }

        private string CycleMessage(string packageId, string modulePath)
        {
            var chain = new List<string>(_moduleChain);
            chain.Reverse();
            chain.Add(packageId + "/" + modulePath);
            return "Module '" + modulePath + "' from '" + packageId + "' is part of an import cycle: "
                + string.Join(" -> ", chain);
        }

        private static bool IsStd(string packageId)
        {
            return string.Equals(packageId, "std", StringComparison.OrdinalIgnoreCase);
        }

        private static string ModuleKey(string packageId, string modulePath)
        {
            return packageId.ToLowerInvariant() + "\n" + modulePath;
        }

        private sealed class ModuleRecord
        {
            internal ModuleRecord(IOxygenHostValue ns)
                : this(ns, true)
            {
            }

            internal ModuleRecord(IOxygenHostValue ns, bool available)
            {
                Namespace = ns;
                Available = available;
            }

            internal IOxygenHostValue Namespace { get; set; }
            internal bool Available { get; }
            internal bool Loading { get; set; }
        }

        private sealed class MathClampCallable : IArtiCallable
        {
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                double value = RequiredFloat(positional, named, 0, "value");
                double min = RequiredFloat(positional, named, 1, "min");
                double max = RequiredFloat(positional, named, 2, "max");
                RejectUnknown(named, "value", "min", "max");
                RejectCount(positional, named, 3);
                if (min > max) throw new RuntimeFault("math.clamp requires min <= max.");
                if (value < min) return min;
                if (value > max) return max;
                return value;
            }
        }

        private sealed class LogicAllCallable : LogicFoldCallable
        {
            internal LogicAllCallable(ArtiExecutor executor)
                : base(executor, true)
            {
            }
        }

        private sealed class LogicAnyCallable : LogicFoldCallable
        {
            internal LogicAnyCallable(ArtiExecutor executor)
                : base(executor, false)
            {
            }
        }

        private abstract class LogicFoldCallable : IOxygenTypedCallable
        {
            private readonly ArtiExecutor _executor;
            private readonly bool _all;

            protected LogicFoldCallable(ArtiExecutor executor, bool all)
            {
                _executor = executor;
                _all = all;
            }

            public OxygenType GetParameterType(int position, string name)
            {
                if (position == 0 || string.Equals(name, "values", StringComparison.Ordinal))
                    return _executor.ListOf(_executor.BoolType, false);
                return null;
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                object values = Required(positional, named, 0, "values");
                RejectUnknown(named, "values");
                RejectCount(positional, named, 1);
                OxygenCollection collection = values as OxygenCollection;
                OxygenType element = collection == null ? null : collection.ElementType;
                if (collection == null || collection.IsArray || element == null
                    || element.Kind != OxygenTypeKind.Bool || element.Nullable)
                    throw new RuntimeFault((_all ? "logic.all" : "logic.any") + " expects a list<bool>.");
                if (_all)
                {
                    foreach (object item in collection)
                        if (!(item is bool) || !(bool)item) return false;
                    return true;
                }

                foreach (object item in collection)
                    if (item is bool && (bool)item) return true;
                return false;
            }
        }

        private sealed class RejectedCallable : IArtiCallable
        {
            private readonly string _message;

            internal RejectedCallable(string message)
            {
                _message = message;
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                throw new RuntimeFault(_message);
            }
        }


        private sealed class CsInt32Callable : IArtiCallable
        {
            private readonly ArtiExecutor _executor;

            internal CsInt32Callable(ArtiExecutor executor)
            {
                _executor = executor;
            }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                object value = Required(positional, named, 0, "value");
                RejectUnknown(named, "value");
                RejectCount(positional, named, 1);
                if (!(value is int)) throw new RuntimeFault("cs.int32 expects an int.");
                return _executor.CreateNativeInt32((int)value);
            }
        }

        private static object Required(IList<object> positional, IDictionary<string, object> named, int position, string name)
        {
            object namedValue = null;
            bool hasNamed = named != null && named.TryGetValue(name, out namedValue);
            bool hasPositional = positional != null && position < positional.Count;
            if (hasNamed && hasPositional) throw new RuntimeFault("The argument '" + name + "' was supplied more than once.");
            if (hasNamed) return namedValue;
            if (hasPositional) return positional[position];
            throw new RuntimeFault("Required argument '" + name + "' is missing.");
        }

        private static double RequiredFloat(IList<object> positional, IDictionary<string, object> named, int position, string name)
        {
            object value = Required(positional, named, position, name);
            if (value is int) return (int)value;
            if (value is double) return (double)value;
            if (value is float) return (float)value;
            throw new RuntimeFault("Argument '" + name + "' must be a number.");
        }

        private static void RejectUnknown(IDictionary<string, object> named, params string[] allowed)
        {
            if (named == null) return;
            foreach (string key in named.Keys)
            {
                bool known = false;
                for (int i = 0; i < allowed.Length; i++)
                {
                    if (string.Equals(key, allowed[i], StringComparison.Ordinal)) { known = true; break; }
                }
                if (!known) throw new RuntimeFault("Unknown argument '" + key + "'.");
            }
        }

        private static void RejectCount(IList<object> positional, IDictionary<string, object> named, int expected)
        {
            int count = positional == null ? 0 : positional.Count;
            if (named != null && named.Count > count) count = named.Count;
            if (count != expected)
                throw new RuntimeFault("Expected " + expected.ToString(CultureInfo.InvariantCulture) + " arguments.");
        }
    }
}
