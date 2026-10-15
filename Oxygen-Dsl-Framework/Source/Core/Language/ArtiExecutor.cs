// Derived from AdvancedRimTalk Source/Arti (MIT License).
// Copyright (c) 2026 Nanaloveyuki
// Adapted for Oxygendsl.Language; original prompt/RimTalk/Verse host interfaces are not retained.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Oxygendsl.Core;
namespace Oxygendsl.Language
{
    public interface IArtiRandomSource
    {
        int NextInt(int minInclusive, int maxInclusive);

        double NextDouble(double minInclusive, double maxInclusive);
    }

    internal interface IArtiCallable
    {
        object Invoke(IList<object> positionalArguments, IDictionary<string, object> namedArguments);
    }

    internal interface IArtiMethodCallable : IArtiCallable
    {
        bool TryInvokeWithReceiver(object receiver, IList<object> positionalArguments,
            IDictionary<string, object> namedArguments, out object value);
    }

    public sealed class SystemArtiRandomSource : IArtiRandomSource
    {
        private static readonly Random Random = new Random();
        private static readonly object SyncRoot = new object();

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            }

            long range = (long)maxInclusive - minInclusive + 1L;
            lock (SyncRoot)
            {
                long offset = (long)(Random.NextDouble() * range);
                if (offset >= range)
                {
                    offset = range - 1L;
                }

                return (int)((long)minInclusive + offset);
            }
        }

        public double NextDouble(double minInclusive, double maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            }

            lock (SyncRoot)
            {
                return minInclusive + ((maxInclusive - minInclusive) * Random.NextDouble());
            }
        }
    }

    public sealed class ArtiExecutionOptions
    {
        public int MaxSteps { get; set; } = 10000;
        public int MaxCallDepth { get; set; } = 64;
    }

    public sealed class ArtiExecutionContext
    {
        public ArtiExecutionContext(IArtiRandomSource random = null, ArtiExecutionOptions options = null,
            IOxygenModuleCatalog modules = null, IOxygenStandardModuleProvider standardModules = null,
            IOxygenModuleProvider hostModules = null, IOxygenLocalization localization = null)
        {
            Random = random ?? new SystemArtiRandomSource();
            Options = options ?? new ArtiExecutionOptions();
            Modules = modules;
            StandardModules = standardModules;
            HostModules = hostModules;
            Localization = localization;
        }

        public IArtiRandomSource Random { get; }
        public ArtiExecutionOptions Options { get; }
        public IOxygenModuleCatalog Modules { get; }
        public IOxygenStandardModuleProvider StandardModules { get; }
        public IOxygenModuleProvider HostModules { get; }
        public IOxygenLocalization Localization { get; }
    }

    public sealed class ArtiExecutionResult
    {
        public ArtiExecutionResult(string output, object value, IList<ArtiDiagnostic> diagnostics)
        {
            Output = output ?? string.Empty;
            Value = value;
            Diagnostics = diagnostics ?? new List<ArtiDiagnostic>();
        }

        public string Output { get; }
        public object Value { get; }
        public object ReturnValue { get { return Value; } }
        public IList<ArtiDiagnostic> Diagnostics { get; }
        public bool HasErrors
        {
            get
            {
                foreach (ArtiDiagnostic diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    public partial class ArtiExecutor : ArtiDiagnosticReporter
    {
        private static readonly HashSet<string> ReservedNames = CreateReservedNames();

        private static HashSet<string> CreateReservedNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal)
            {
                "core", "range", "random", "exists", "len", "lang", "pawn", "game",
                "any", "int", "float", "bool", "string", "list", "array", "object", "void"
            };
            foreach (OxygenDeclaration declaration in OxygenDeclarations.All)
            {
                names.Add(declaration.Name);
            }

            return names;
        }

        private ArtiExecutionContext _context;
        private StringBuilder _output;
        private RuntimeObject _core;
        private RuntimeScope _rootScope;
        private int _steps;
        private int _callDepth;
        private object _lastValue;
        private PawnContext _pawn;
        private GameContext _game;
        private RuntimeScope _diagnosticScope;
        private OxygenType _returnType = OxygenTypes.Any;
        private readonly Dictionary<string, RuntimeCallable> _builtinConstructors =
            new Dictionary<string, RuntimeCallable>(StringComparer.Ordinal);
        private RuntimeCallable _lang;
        private readonly Dictionary<string, object> _localizationArguments = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, OxygenStructShape> _structs =
            new Dictionary<string, OxygenStructShape>(StringComparer.Ordinal);
        private readonly Dictionary<ArtiFunctionDeclarationStatement, OxygenType> _returnTypes =
            new Dictionary<ArtiFunctionDeclarationStatement, OxygenType>();
        private readonly Dictionary<string, OxygenType> _nominalTypes =
            new Dictionary<string, OxygenType>(StringComparer.Ordinal);

        public ArtiExecutionResult Execute(string source, ArtiExecutionContext context = null)
        {
            ClearDiagnostics();
            ArtiParseResult parsed = new ArtiParser().Parse(source ?? string.Empty);
            AddDiagnostics(parsed.Diagnostics);
            if (!HasErrors(ReportedDiagnostics))
            {
                ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program);
                AddDiagnostics(analysis.Diagnostics);
            }

            if (HasErrors(ReportedDiagnostics))
            {
                return new ArtiExecutionResult(string.Empty, null, SnapshotDiagnostics());
            }

            return ExecuteProgram(parsed.Program, context);
        }

        public ArtiExecutionResult Execute(ArtiParseResult parsed, ArtiExecutionContext context = null)
        {
            ClearDiagnostics();
            if (parsed == null)
            {
                ReportError(3000);
                return new ArtiExecutionResult(string.Empty, null, SnapshotDiagnostics());
            }

            AddDiagnostics(parsed.Diagnostics);
            if (!HasErrors(ReportedDiagnostics))
            {
                ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program);
                AddDiagnostics(analysis.Diagnostics);
            }

            if (HasErrors(ReportedDiagnostics))
            {
                return new ArtiExecutionResult(string.Empty, null, SnapshotDiagnostics());
            }

            return ExecuteProgram(parsed.Program, context);
        }

        public ArtiExecutionResult Execute(ArtiProgram program, ArtiExecutionContext context = null)
        {
            ClearDiagnostics();
            return ExecuteProgram(program, context);
        }

        public object EvaluatePrepared(ArtiExpression expression)
        {
            if (_rootScope == null)
            {
                throw new InvalidOperationException("The program has not been prepared.");
            }

            _steps = 0;
            _callDepth = 0;
            ClearDiagnostics();
            try
            {
                return Evaluate(expression, _rootScope);
            }
            catch (ExecutionStopException)
            {
                throw CreateEvaluationException(expression == null ? ArtiSourceSpan.Empty : expression.Span);
            }
            catch (RuntimeFault fault)
            {
                ArtiSourceSpan span = expression == null ? ArtiSourceSpan.Empty : expression.Span;
                throw new ScriptEvaluationException(fault.Message, span.Line, span.Column);
            }
        }

        private readonly RuntimeArguments _emptyCallbackArguments = new RuntimeArguments();

        public object InvokePrepared(object callable, params object[] arguments)
        {
            if (callable == null)
            {
                throw new ScriptEvaluationException("The callback is missing.", 1, 1);
            }

            _steps = 0;
            _callDepth = 0;
            _lastValue = null;
            ClearDiagnostics();
            _output = _output ?? new StringBuilder();
            _context = _context ?? new ArtiExecutionContext();
            RuntimeArguments runtimeArguments = arguments == null || arguments.Length == 0
                ? _emptyCallbackArguments : new RuntimeArguments();
            if (arguments != null)
            {
                for (int index = 0; index < arguments.Length; index++)
                {
                    runtimeArguments.Add(new RuntimeArgument(null, arguments[index]));
                }
            }

            try
            {
                IOxygenHostCallable hostCallable = callable as IOxygenHostCallable;
                if (hostCallable != null)
                {
                    return InvokeHostCallable(hostCallable, runtimeArguments);
                }

                return InvokeCallable(callable, runtimeArguments);
            }
            catch (ExecutionStopException)
            {
                throw CreateEvaluationException(ArtiSourceSpan.Empty);
            }
            catch (RuntimeFault fault)
            {
                throw new ScriptEvaluationException(fault.Message, 1, 1);
            }
            catch (ScriptEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ScriptEvaluationException(exception.Message, 1, 1);
            }
        }

        public object InvokeIsolated(ArtiFunctionDeclarationStatement function, PawnContext pawn, GameContext game, params object[] extraArguments)
        {
            if (function == null)
            {
                throw new ScriptEvaluationException("The condition function is missing.", 1, 1);
            }

            _steps = 0;
            _callDepth = 0;
            _lastValue = null;
            ClearDiagnostics();
            _output = _output ?? new StringBuilder();
            BindContexts(pawn, game);
            RuntimeArguments arguments = new RuntimeArguments();
            arguments.Add(new RuntimeArgument(null, _pawn));
            if (extraArguments != null)
                for (int index = 0; index < extraArguments.Length; index++)
                    arguments.Add(new RuntimeArgument(null, extraArguments[index]));
            try
            {
                return InvokeUserFunction(function, arguments, _rootScope);
            }
            catch (ExecutionStopException)
            {
                throw CreateEvaluationException(function.Span);
            }
            catch (RuntimeFault fault)
            {
                throw new ScriptEvaluationException(fault.Message, function.Span.Line, function.Span.Column);
            }
            catch (ScriptEvaluationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ScriptEvaluationException(exception.Message, function.Span.Line, function.Span.Column);
            }
        }

        private void BindContexts(PawnContext pawn, GameContext game)
        {
            _pawn = pawn;
            _game = game;
        }

        private ScriptEvaluationException CreateEvaluationException(ArtiSourceSpan fallback)
        {
            foreach (ArtiDiagnostic diagnostic in ReportedDiagnostics)
            {
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                {
                    return new ScriptEvaluationException(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column);
                }
            }

            return new ScriptEvaluationException("The condition exceeded its execution budget.", fallback.Line, fallback.Column);
        }

        private ArtiExecutionResult ExecuteProgram(ArtiProgram program, ArtiExecutionContext context)
        {
            _context = context ?? new ArtiExecutionContext();
            _output = new StringBuilder();
            _core = CreateCoreModule();
            _rootScope = new RuntimeScope(null);
            _steps = 0;
            _callDepth = 0;
            _lastValue = null;
            ResetRuntimeCaches();

            if (program == null)
            {
                ReportError(3000);
                return CreateResult();
            }

            try
            {
                PrepareDeclarations(program.Statements, _rootScope);
                ExecuteStatements(program.Statements, _rootScope);
            }
            catch (ExecutionStopException)
            {
            }
            catch (ReturnSignal)
            {
                RecordRuntimeError(program.Span, "return escaped the root program.");
            }
            catch (BreakSignal)
            {
                RecordRuntimeError(program.Span, "break escaped the root program.");
            }
            catch (ContinueSignal)
            {
                RecordRuntimeError(program.Span, "continue escaped the root program.");
            }
            catch (RuntimeFault fault)
            {
                RecordRuntimeError(program.Span, fault.Message);
            }
            catch (Exception exception)
            {
                RecordRuntimeError(program.Span, "Unhandled runtime error: " + exception.Message);
            }

            return CreateResult();
        }

        private void ResetRuntimeCaches()
        {
            _resolvedTypes.Clear();
            _parameterTypes.Clear();
            _returnTypes.Clear();
            _structs.Clear();
            _nominalTypes.Clear();
            _modules.Clear();
            _moduleChain.Clear();
            NativeInt32Type = null;
            _returnType = OxygenTypes.Any;
            _functionScopes.Clear();
            _genericFunctions.Clear();
            _genericCalls.Clear();
            _directMethods.Clear();
            _directStatics.Clear();
            _typeConstraints.Clear();
        }


        private ArtiExecutionResult CreateResult()
        {
            return new ArtiExecutionResult(_output == null ? string.Empty : _output.ToString(), _lastValue, SnapshotDiagnostics());
        }

        internal void PrepareDeclarations(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            if (statements == null)
            {
                return;
            }

            foreach (ArtiStatement statement in statements)
            {
                ArtiUseStatement use = statement as ArtiUseStatement;
                if (use != null)
                {
                    object ns = ResolveModule(use, scope);
                    string alias = string.IsNullOrEmpty(use.Alias) ? LastPathSegment(use.ModulePath) : use.Alias;
                    BindImport(scope, alias, ns, use.Span);
                    continue;
                }
                ArtiStructDeclarationStatement structDeclaration = statement as ArtiStructDeclarationStatement;
                if (structDeclaration != null)
                {
                    if (IsReservedDeclarationName(structDeclaration.Name)) RuntimeError(structDeclaration.Span, "The name '" + structDeclaration.Name + "' is reserved.");
                    string packageId = string.IsNullOrEmpty(scope.ModulePackageId) ? "local" : scope.ModulePackageId;
                    string modulePath = string.IsNullOrEmpty(scope.ModulePath) ? LocalScopeKey(scope) : scope.ModulePath;
                    OxygenType type = CreateNominalStructType(packageId, modulePath, structDeclaration.Name);
                    RegisterLocalStruct(scope, structDeclaration.Name, type);
                    if (!scope.TryDeclare(structDeclaration.Name, new OxygenTypeExport(type, null), true, ShouldAllowRedeclare(scope), structDeclaration))
                        RuntimeError(structDeclaration.Span, "The name '" + structDeclaration.Name + "' is already declared.");
                    continue;
                }

                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    if (IsReservedDeclarationName(function.Name)) RuntimeError(function.Span, "The name '" + function.Name + "' is reserved.");
                    scope.TryDeclare(
                        function.Name,
                        new RuntimeCallable(
                            delegate(RuntimeArguments arguments)
                            {
                                return InvokeUserFunction(function, arguments, scope);
                            },
                            function.Name,
                            (position, argumentName) => ParameterType(function, position, argumentName)),
                        true,
                        ShouldAllowRedeclare(scope), function);
                }
            }
            PredeclareTraits(statements, scope);

            foreach (ArtiStatement statement in statements)
            {
                ArtiStructDeclarationStatement structDeclaration = statement as ArtiStructDeclarationStatement;
                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    object callable = CreateFunctionCallable(function, scope);
                    scope.TryDeclare(function.Name, callable, true, true, function);
                    continue;
                }
                if (structDeclaration == null) continue;
                object exported;
                if (!scope.TryGet(structDeclaration.Name, out exported)) continue;
                OxygenTypeExport typeExport = exported as OxygenTypeExport;
                DefineStruct(typeExport.Type, structDeclaration, scope);
                scope.TryDeclare(structDeclaration.Name, new OxygenTypeExport(typeExport.Type, CreateStructConstructor(typeExport.Type)), true, true, structDeclaration);
            }
            PrepareTraitsAndImpls(statements, scope);

            foreach (ArtiStatement statement in statements)
            {
                if (statement is ArtiStructDeclarationStatement)
                {
                    FinishStructs();
                    break;
                }
            }
            ValidateGenericBodies(statements, scope);
            if (scope.Parent == null) ValidateAnnotations(statements, scope);

        }

        internal void ExecuteStatements(IList<ArtiStatement> statements, RuntimeScope scope)
        {
            if (statements == null)
            {
                return;
            }

            foreach (ArtiStatement statement in statements)
            {
                Touch(statement == null ? ArtiSourceSpan.Empty : statement.Span);
                ExecuteStatement(statement, scope);
            }
        }

        private void ExecuteStatement(ArtiStatement statement, RuntimeScope scope)
        {
            if (statement == null)
            {
                return;
            }

            ArtiUseStatement use = statement as ArtiUseStatement;
            if (use != null)
            {
                return;
            }

            ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
            if (variable != null)
            {
                if (IsReservedDeclarationName(variable.Name))
                    RuntimeError(variable.Span, "The name '" + variable.Name + "' is reserved.");
                if (variable.IsConst && !ArtiConstantExpression.IsStatic(variable.Value, scope.IsConstant))
                    RuntimeError(variable.Span, "const requires a static expression; use a function for dynamic values.");
                OxygenType declared = ResolveValueType(variable.Type, scope, variable.Span);
                object value = Evaluate(variable.Value, scope, declared);
                value = Coerce(declared, value, variable.Span, "initialization");
                if (variable.Name != "_" && !scope.TryDeclare(
                    variable.Name,
                    value,
                    variable.IsConst,
                    ShouldAllowRedeclare(scope), variable))
                {
                    RuntimeError(variable.Span, "The name '" + variable.Name + "' is already declared.");
                }

                Binding stored;
                if (variable.Name != "_" && scope.TryGetBinding(variable.Name, out stored)) stored.Type = declared;
                _lastValue = value;
                if (variable.IsConst && variable.Name != "_") scope.MarkConstant(variable.Name);
                return;
            }

            if (statement is ArtiUnpackDeclarationStatement unpack)
            {
                AssignUnpacked(unpack.Targets, Evaluate(unpack.Value, scope), scope, unpack);
                return;
            }

            ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
            if (function != null || statement is ArtiStructDeclarationStatement
                || statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement)
            {
                return;
            }

            ArtiAssignmentStatement assignment = statement as ArtiAssignmentStatement;
            if (assignment != null)
            {
                if (assignment.Target is ArtiArrayExpression targets)
                {
                    if (assignment.Operator != ArtiTokenKind.Equal)
                        RuntimeError(assignment.Span, "Unpacking only supports =.");
                    AssignUnpacked(targets, Evaluate(assignment.Value, scope), scope, null);
                    return;
                }

                ArtiNameExpression name = assignment.Target as ArtiNameExpression;
                if (name != null)
                {
                    if (name.Name == "_")
                    {
                        if (assignment.Operator != ArtiTokenKind.Equal)
                            RuntimeError(name.Span, "A discard only supports =.");
                        _lastValue = Evaluate(assignment.Value, scope);
                        return;
                    }

                    Binding binding;
                    if (!scope.TryGetBinding(name.Name, out binding))
                        RuntimeError(name.Span, "The name '" + name.Name + "' is not declared.");
                    object value = Evaluate(assignment.Value, scope, assignment.Operator == ArtiTokenKind.Equal ? binding.Type : null);
                    string assignmentError;
                    if (!TryAssignChecked(scope, name.Name, value, assignment.Span, assignment.Operator, out assignmentError))
                        RuntimeError(name.Span, assignmentError);
                    _lastValue = value;
                    return;
                }

                AssignMemberOrIndex(assignment, scope);
                return;
            }



            ArtiIfStatement conditional = statement as ArtiIfStatement;
            if (conditional != null)
            {
                foreach (ArtiIfBranch branch in conditional.Branches)
                {
                    if (IsTruthy(Evaluate(branch.Condition, scope)))
                    {
                        ExecuteBlock(branch.Body, scope);
                        return;
                    }
                }

                if (conditional.ElseBody != null)
                {
                    ExecuteBlock(conditional.ElseBody, scope);
                }

                return;
            }

            ArtiForStatement loop = statement as ArtiForStatement;
            if (loop != null)
            {
                object source = Evaluate(loop.Source, scope);
                RuntimeScope loopScope = new RuntimeScope(scope, true);

                foreach (object item in Enumerate(source, loop.Source == null ? loop.Span : loop.Source.Span))
                {
                    Touch(loop.Span);
                    if (loop.VariableName != "_" && !loopScope.TryDeclare(loop.VariableName, item, false, false, loop))
                    {
                        RuntimeError(loop.Span, "The loop variable '" + loop.VariableName + "' is already declared.");
                    }

                    try
                    {
                        ExecuteBlock(loop.Body, loopScope);
                    }
                    catch (ContinueSignal)
                    {
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                }

                return;
            }

            ArtiWhileStatement whileLoop = statement as ArtiWhileStatement;
            if (whileLoop != null)
            {
                while (IsTruthy(Evaluate(whileLoop.Condition, scope)))
                {
                    Touch(whileLoop.Span);
                    try
                    {
                        ExecuteBlock(whileLoop.Body, scope);
                    }
                    catch (ContinueSignal)
                    {
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                }

                return;
            }

            ArtiReturnStatement returnStatement = statement as ArtiReturnStatement;
            if (returnStatement != null)
            {
                throw new ReturnSignal(returnStatement.Value == null ? null : Evaluate(returnStatement.Value, scope, _returnType), returnStatement.Value != null);
            }

            if (statement is ArtiBreakStatement)
            {
                throw new BreakSignal();
            }

            if (statement is ArtiContinueStatement)
            {
                throw new ContinueSignal();
            }

            ArtiBlockStatement block = statement as ArtiBlockStatement;
            if (block != null)
            {
                ExecuteBlock(block, scope);
                return;
            }

            ArtiExpressionStatement expression = statement as ArtiExpressionStatement;
            if (expression != null)
            {
                if (IsRuleRegistration(expression.Expression))
                {
                    return;
                }

                _lastValue = Evaluate(expression.Expression, scope);
            }
        }

        private static bool IsRuleRegistration(ArtiExpression expression)
        {
            ArtiCallExpression call = expression as ArtiCallExpression;
            ArtiNameExpression name = call == null ? null : call.Target as ArtiNameExpression;
            return name != null && OxygenDeclarations.IsRegistration(name.Name);
        }

        private void AssignUnpacked(ArtiArrayExpression targets, object value, RuntimeScope scope,
            ArtiUnpackDeclarationStatement declaration)
        {
            var values = value as IList<object>;
            if (values == null || values.Count != targets.Items.Count)
                RuntimeError(targets.Span, "Unpacking expects " + targets.Items.Count + " values, got "
                    + (values == null ? "a non-array value" : values.Count.ToString(CultureInfo.InvariantCulture)) + ".");

            // Validate and snapshot every slot before changing any binding.
            var captured = new object[targets.Items.Count];
            var declared = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < targets.Items.Count; i++)
            {
                var name = targets.Items[i] as ArtiNameExpression;
                if (name == null) RuntimeError(targets.Items[i].Span, "Unpacking targets must be variable names or _.");
                captured[i] = values[i];
                if (name.Name == "_") continue;
                if (declaration != null)
                {
                    if (!declared.Add(name.Name) || !scope.CanDeclare(name.Name, false, ShouldAllowRedeclare(scope), declaration))
                        RuntimeError(name.Span, "The name '" + name.Name + "' is already declared.");
                    if (IsReservedDeclarationName(name.Name)) RuntimeError(name.Span, "The name '" + name.Name + "' is reserved.");
                }
                else
                {
                    if (!scope.CanAssign(name.Name, out string error)) RuntimeError(name.Span, error);
                    Binding binding;
                    if (scope.TryGetBinding(name.Name, out binding))
                        captured[i] = Coerce(binding.Type, captured[i], name.Span, "assignment to '" + name.Name + "'");
                }
            }
            for (int i = 0; i < targets.Items.Count; i++)
            {
                var name = (ArtiNameExpression)targets.Items[i];
                if (name.Name == "_") continue;
                if (declaration != null)
                    scope.TryDeclare(name.Name, captured[i], false, ShouldAllowRedeclare(scope), declaration);
                else scope.TryAssign(name.Name, captured[i], out _);
            }
            _lastValue = value;
        }

        private void ExecuteBlock(ArtiBlockStatement block, RuntimeScope parent)
        {
            if (block == null)
            {
                return;
            }

            RuntimeScope scope = new RuntimeScope(parent, true);
            PrepareDeclarations(block.Statements, scope);
            ExecuteStatements(block.Statements, scope);
        }

        private bool ShouldAllowRedeclare(RuntimeScope scope)
        {
            return false;
        }

        internal object Evaluate(ArtiExpression expression, RuntimeScope scope)
        {
            return Evaluate(expression, scope, null);
        }

        private object Evaluate(ArtiExpression expression, RuntimeScope scope, OxygenType expected)
        {
            if (expression == null || expression is ArtiErrorExpression)
            {
                return null;
            }

            Touch(expression.Span);

            if (expression is ArtiInterpolatedStringExpression interpolated)
            {
                var text = new StringBuilder();
                foreach (ArtiExpression part in interpolated.Parts) text.Append(ToText(Evaluate(part, scope)));
                return text.ToString();
            }

            ArtiLiteralExpression literal = expression as ArtiLiteralExpression;
            if (literal != null)
            {
                return literal.Value;
            }

            ArtiNameExpression name = expression as ArtiNameExpression;
            if (name != null)
            {
                object value;
                if (scope.TryGet(name.Name, out value))
                {
                    return UnwrapExport(value, false);
                }

                if (TryGetBuiltin(name.Name, out value))
                {
                    return value;
                }

                if (string.Equals(name.Name, "pawn", StringComparison.Ordinal) && _pawn != null)
                {
                    return _pawn;
                }

                if (string.Equals(name.Name, "game", StringComparison.Ordinal) && _game != null)
                {
                    return _game;
                }


                RuntimeError(name.Span, "The name '" + name.Name + "' is not declared.");
            }

            ArtiMemberExpression member = expression as ArtiMemberExpression;
            if (member != null)
            {
                object target = Evaluate(member.Target, scope);
                object value;
                object staticTarget;
                if (_directStatics.TryGetValue(member, out staticTarget)) return staticTarget;
                OxygenMethod direct;
                if (_directMethods.TryGetValue(member, out direct))
                    return new OxygenBoundMethod((OxygenStruct)target, direct, InvokeBound, ParameterType);
                if (TryGetMember(target, member.Member, out value))
                {
                    return value;
                }

                RuntimeError(member.Span, "The member '" + member.Member + "' is not available.");
            }

            ArtiIndexExpression index = expression as ArtiIndexExpression;
            if (index != null)
            {
                object target = Evaluate(index.Target, scope);
                object indexValue = Evaluate(index.Index, scope);
                OxygenCollection collection = target as OxygenCollection;
                if (collection != null)
                {
                    object indexed;
                    TryGetCollectionIndex(collection, indexValue, out indexed, index.Span);
                    return indexed;
                }

                object value;
                if (TryGetIndex(target, indexValue, out value))
                {
                    return value;
                }

                RuntimeError(index.Span, "The value cannot be indexed with '" + FormatValue(indexValue) + "'.");
            }

            ArtiCallExpression call = expression as ArtiCallExpression;
            if (call != null)
            {
                return EvaluateCall(call, scope);
            }


            ArtiArrayExpression array = expression as ArtiArrayExpression;
            if (array != null)
            {
                return CreateLiteral(array, scope, expected);
            }

            ArtiObjectExpression obj = expression as ArtiObjectExpression;
            if (obj != null)
            {
                RuntimeObject value = new RuntimeObject();
                foreach (ArtiObjectMember item in obj.Members)
                {
                    if (value.Contains(item.Name)) RuntimeError(obj.Span, "The object key '" + item.Name + "' is duplicated.");
                    value.Set(item.Name, Evaluate(item.Value, scope));
                }

                return value;
            }

            ArtiUnaryExpression unary = expression as ArtiUnaryExpression;
            if (unary != null)
            {
                object operand = Evaluate(unary.Operand, scope);
                switch (unary.Operator)
                {
                    case ArtiTokenKind.Bang:
                        return !IsTruthy(operand);
                    case ArtiTokenKind.Plus:
                        return NumericValue(operand, unary.Span);
                    case ArtiTokenKind.Minus:
                        return NegateNumber(operand, unary.Span);
                    default:
                        RuntimeError(unary.Span, "Unsupported unary operator.");
                        break;
                }
            }

            ArtiBinaryExpression binary = expression as ArtiBinaryExpression;
            if (binary != null)
            {
                object left = Evaluate(binary.Left, scope);
                if (binary.Operator == ArtiTokenKind.AndAnd && !IsTruthy(left))
                {
                    return false;
                }

                if (binary.Operator == ArtiTokenKind.OrOr && IsTruthy(left))
                {
                    return true;
                }

                if (binary.Operator == ArtiTokenKind.QuestionQuestion && left != null)
                {
                    return left;
                }

                object right = Evaluate(binary.Right, scope);
                if (binary.Operator == ArtiTokenKind.AndAnd)
                {
                    return IsTruthy(right);
                }

                if (binary.Operator == ArtiTokenKind.OrOr)
                {
                    return IsTruthy(right);
                }

                return ApplyBinary(binary.Operator, left, right, binary.Span);
            }

            return null;
        }

        private object InvokeCallable(object target, RuntimeArguments arguments)
        {
            if (target is OxygenTypeExport exported)
            {
                if (exported.Constructor == null) throw new RuntimeFault("The type is not constructible.");
                return InvokeCallable(exported.Constructor, arguments);
            }
            if (target is OxygenGenericFunction) throw new RuntimeFault("A generic callback must be specialized by a script call with static types.");
            RuntimeCallable runtimeCallable = target as RuntimeCallable;
            if (runtimeCallable != null)
            {
                return runtimeCallable.InvokeInternal(arguments);
            }
            OxygenBoundMethod boundMethod = target as OxygenBoundMethod;
            if (boundMethod != null) return boundMethod.InvokeInternal(arguments);

            IArtiCallable callable = target as IArtiCallable;
            if (callable != null)
            {
                Dictionary<string, object> named = new Dictionary<string, object>(StringComparer.Ordinal);
                List<object> positional = new List<object>();
                foreach (RuntimeArgument argument in arguments.Items)
                {
                    if (argument.Name == null)
                    {
                        positional.Add(argument.Value);
                    }
                    else
                    {
                        if (named.ContainsKey(argument.Name))
                        {
                            throw new RuntimeFault("The named argument '" + argument.Name + "' was supplied more than once.");
                        }

                        named.Add(argument.Name, argument.Value);
                    }
                }

                return callable.Invoke(positional, named);
            }

            throw new RuntimeFault("The value is not callable.");
        }

        private object InvokeCallable(object target, RuntimeArguments arguments, object receiver)
        {
            IArtiMethodCallable methodCallable = target as IArtiMethodCallable;
            if (methodCallable != null)
            {
                Dictionary<string, object> named = new Dictionary<string, object>(StringComparer.Ordinal);
                List<object> positional = new List<object>();
                foreach (RuntimeArgument argument in arguments.Items)
                {
                    if (argument.Name == null)
                    {
                        positional.Add(argument.Value);
                    }
                    else
                    {
                        if (named.ContainsKey(argument.Name))
                        {
                            throw new RuntimeFault("The named argument '" + argument.Name + "' was supplied more than once.");
                        }

                        named.Add(argument.Name, argument.Value);
                    }
                }

                object value;
                if (methodCallable.TryInvokeWithReceiver(receiver, positional, named, out value))
                {
                    return value;
                }
            }

            return InvokeCallable(target, arguments);
        }

        internal object InvokeUserFunction(
            ArtiFunctionDeclarationStatement function,
            RuntimeArguments arguments,
            RuntimeScope closure)
        {
            return InvokeUserFunction(function, arguments, closure, null);
        }

        private object InvokeUserFunction(
            ArtiFunctionDeclarationStatement function,
            RuntimeArguments arguments,
            RuntimeScope closure,
            OxygenStruct self)
        {
            if (function == null)
            {
                throw new RuntimeFault("The function declaration is missing.");
            }

            _callDepth++;
            if (_callDepth > Math.Max(1, _context.Options.MaxCallDepth))
            {
                _callDepth--;
                throw new RuntimeFault("The maximum Arti call depth was exceeded.");
            }

            RuntimeScope functionScope = new RuntimeScope(closure);
            if (self != null) functionScope.TryDeclare("self", self, true);
            bool[] assigned = new bool[function.Parameters.Count];
            int nextPositional = 0;
            OxygenType previousReturnType = _returnType;
            RuntimeScope previousDiagnosticScope = _diagnosticScope;
            if (!_returnTypes.TryGetValue(function, out _returnType)) _returnType = OxygenTypes.Any;
            _diagnosticScope = closure;
            try
            {
                foreach (RuntimeArgument argument in arguments.Items)
                {
                    int parameterIndex;
                    if (argument.Name != null)
                    {
                        if (argument.Name == "_") throw new RuntimeFault("Discard parameters must be supplied positionally.");
                        parameterIndex = -1;
                        for (int index = 0; index < function.Parameters.Count; index++)
                        {
                            if (string.Equals(function.Parameters[index].Name, argument.Name, StringComparison.Ordinal))
                            {
                                parameterIndex = index;
                                break;
                            }
                        }

                        if (parameterIndex < 0)
                        {
                            throw new RuntimeFault("The function has no parameter named '" + argument.Name + "'.");
                        }
                    }
                    else
                    {
                        while (nextPositional < assigned.Length && assigned[nextPositional])
                        {
                            nextPositional++;
                        }

                        parameterIndex = nextPositional++;
                    }

                    if (parameterIndex < 0 || parameterIndex >= assigned.Length)
                    {
                        throw new RuntimeFault("Too many arguments were supplied to '" + function.Name + "'.");
                    }

                    if (assigned[parameterIndex])
                    {
                        throw new RuntimeFault("The parameter '" + function.Parameters[parameterIndex].Name + "' was supplied more than once.");
                    }

                    assigned[parameterIndex] = true;
                    ArtiParameter parameter = function.Parameters[parameterIndex];
                    OxygenType[] parameterTypes;
                    if (!_parameterTypes.TryGetValue(function, out parameterTypes))
                    {
                        CacheParameterTypes(function, closure);
                        parameterTypes = _parameterTypes[function];
                    }

                    object coerced = Coerce(parameterTypes[parameterIndex], argument.Value, parameter.Span, "parameter '" + parameter.Name + "'");
                    if (parameter.Name != "_")
                    {
                        functionScope.TryDeclare(parameter.Name, coerced, false);
                        Binding stored;
                        if (functionScope.TryGetBinding(parameter.Name, out stored)) stored.Type = parameterTypes[parameterIndex];
                    }
                }

                for (int index = 0; index < assigned.Length; index++)
                {
                    if (!assigned[index])
                    {
                        throw new RuntimeFault("The parameter '" + function.Parameters[index].Name + "' was not supplied.");
                    }
                }

                PrepareDeclarations(function.Body == null ? null : function.Body.Statements, functionScope);
                try
                {
                    ExecuteStatements(function.Body == null ? null : function.Body.Statements, functionScope);
                }
                catch (ReturnSignal signal)
                {
                    OxygenType returnType;
                    if (!_returnTypes.TryGetValue(function, out returnType)) returnType = OxygenTypes.Any;
                    return CoerceReturn(returnType, signal.Value, function.Span, signal.HasValue);
                }

                OxygenType implicitReturn;
                if (!_returnTypes.TryGetValue(function, out implicitReturn)) implicitReturn = OxygenTypes.Any;
                return CoerceReturn(implicitReturn, null, function.Span, false);
            }
            catch (RuntimeFault fault)
            {
                RuntimeError(function.Span, fault.Message);
                return null;
            }
            finally
            {
                _returnType = previousReturnType;
                _diagnosticScope = previousDiagnosticScope;
                _callDepth--;
            }
        }

        private bool TryEvaluateExisting(ArtiExpression expression, RuntimeScope scope, out object value)
        {
            if (expression is ArtiNameExpression name)
                return scope.TryGet(name.Name, out value) || TryGetBuiltin(name.Name, out value);
            if (expression is ArtiMemberExpression member)
            {
                value = null;
                return TryEvaluateExisting(member.Target, scope, out object target)
                    && TryGetMember(target, member.Member, out value);
            }
            if (expression is ArtiIndexExpression index)
            {
                value = null;
                if (!TryEvaluateExisting(index.Target, scope, out object indexedTarget)) return false;
                object indexValue = Evaluate(index.Index, scope);
                OxygenCollection collection = indexedTarget as OxygenCollection;
                if (collection != null)
                {
                    if (!(indexValue is int)) RuntimeError(index.Index.Span, "The index must be an int.");
                    int numeric = (int)indexValue;
                    if (numeric < 0 || numeric >= collection.Count) return false;
                    value = collection[numeric];
                    return true;
                }
                return TryGetIndex(indexedTarget, indexValue, out value);
            }
            value = Evaluate(expression, scope);
            return true;
        }

        internal bool TryGetBuiltin(string name, out object value)
        {
            if (name == "int" || name == "float" || name == "bool" || name == "string" || name == "list" || name == "array")
            {
                RuntimeCallable constructor;
                if (!_builtinConstructors.TryGetValue(name, out constructor))
                {
                    constructor = new RuntimeCallable(
                        arguments => InvokeBuiltinConstructor(name, null, arguments, _rootScope, ArtiSourceSpan.Empty), name);
                    _builtinConstructors.Add(name, constructor);
                }
                value = constructor;
                return true;
            }
            if (name == "exists")
            {
                value = ExistsCallable.Instance;
                return true;
            }
            if (name == "lang")
            {
                if (_lang == null) _lang = new RuntimeCallable(InvokeLang);
                value = _lang;
                return true;
            }
            if (string.Equals(name, "core", StringComparison.Ordinal))
            {
                value = _core;
                return true;
            }

            if (string.Equals(name, "range", StringComparison.Ordinal))
            {
                value = new RuntimeCallable(CreateRange);
                return true;
            }

            if (string.Equals(name, "random", StringComparison.Ordinal))
            {
                value = new RuntimeCallable(delegate(RuntimeArguments arguments)
                {
                    int min = ToInt(arguments.Get(0, "min", true), ArtiSourceSpan.Empty);
                    int max = ToInt(arguments.Get(1, "max", true), ArtiSourceSpan.Empty);
                    return _context.Random.NextInt(min, max);
                });
                return true;
            }

            OxygenDeclaration declaration;
            if (OxygenDeclarations.TryGet(name, out declaration) && declaration.Kind == OxygenDeclarationKind.Value)
            {
                value = ContentValueCallables.Get(name);
                return value != null;
            }


            IArtiCallable stringCallable;
            if (ArtiStringFunctions.TryGetCallable(name, out stringCallable))
            {
                value = stringCallable;
                return true;
            }

            value = null;
            return false;
        }
        private object InvokeLang(RuntimeArguments arguments)
        {
            int positional = 0;
            object key = null;
            bool sawKey = false;
            Dictionary<string, object> substitutions = _localizationArguments;
            substitutions.Clear();
            foreach (RuntimeArgument argument in arguments.Items)
            {
                if (argument.Name == null)
                {
                    positional++;
                    if (positional != 1)
                    {
                        throw new RuntimeFault("lang expects exactly one positional string key.");
                    }

                    if (!(argument.Value is string text) || string.IsNullOrWhiteSpace(text))
                    {
                        throw new RuntimeFault("lang expects exactly one positional string key.");
                    }

                    key = argument.Value;
                    sawKey = true;
                    continue;
                }

                if (argument.Name.Length == 0)
                {
                    throw new RuntimeFault("lang substitution names cannot be empty.");
                }

                object value = argument.Value;
                if (value != null && !(value is string) && !(value is bool) && !IsNumber(value)
                    || value is double number && (double.IsNaN(number) || double.IsInfinity(number))
                    || value is float single && (float.IsNaN(single) || float.IsInfinity(single)))
                {
                    throw new RuntimeFault("lang substitution '" + argument.Name + "' must be a string, finite number, bool, or null.");
                }

                if (substitutions.ContainsKey(argument.Name))
                {
                    throw new RuntimeFault("The named argument '" + argument.Name + "' was supplied more than once.");
                }

                substitutions.Add(argument.Name, value);
            }

            if (!sawKey)
            {
                throw new RuntimeFault("lang expects exactly one positional string key.");
            }

            IOxygenLocalization localization = _context == null ? null : _context.Localization;
            if (localization == null)
            {
                throw new RuntimeFault("lang requires a localization provider. This execution has none.");
            }

            return localization.Translate((string)key, substitutions);
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }


        private RuntimeObject CreateCoreModule()
        {
            RuntimeObject core = new RuntimeObject();
            RuntimeObject stringModule = new RuntimeObject();
            foreach (string name in ArtiStringFunctions.Names)
            {
                IArtiCallable stringCallable;
                if (ArtiStringFunctions.TryGetCallable(name, out stringCallable))
                {
                    stringModule.Set(name, stringCallable);
                }
            }
            core.Set("string", stringModule);

            RuntimeObject random = new RuntimeObject();
            random.Set("int", new RuntimeCallable(delegate(RuntimeArguments arguments)
            {
                int min = ToInt(arguments.Get(0, "min", true), ArtiSourceSpan.Empty);
                int max = ToInt(arguments.Get(1, "max", true), ArtiSourceSpan.Empty);
                return _context.Random.NextInt(min, max);
            }));
            random.Set("float", new RuntimeCallable(delegate(RuntimeArguments arguments)
            {
                double min = ToDouble(arguments.Get(0, "min", true), ArtiSourceSpan.Empty);
                double max = ToDouble(arguments.Get(1, "max", true), ArtiSourceSpan.Empty);
                return _context.Random.NextDouble(min, max);
            }));
            random.Set("pick", new RuntimeCallable(RandomPick));
            core.Set("random", random);

            RuntimeObject value = new RuntimeObject();
            value.Set("default", new RuntimeCallable(delegate(RuntimeArguments arguments)
            {
                object first = arguments.Get(0, "value", true);
                object fallback = arguments.Get(1, "fallback", true);
                return IsEmpty(first) ? fallback : first;
            }));
            value.Set("coalesce", new RuntimeCallable(Coalesce));
            core.Set("value", value);

            RuntimeObject text = new RuntimeObject();
            text.Set("join_nonempty", new RuntimeCallable(JoinNonEmpty));
            text.Set("trim_lines", new RuntimeCallable(delegate(RuntimeArguments arguments)
            {
                return TrimLines(ToText(arguments.Get(0, "text", true)));
            }));
            text.Set("indent", new RuntimeCallable(delegate(RuntimeArguments arguments)
            {
                string input = ToText(arguments.Get(0, "text", true));
                int level = ToInt(arguments.Get(1, "level", true), ArtiSourceSpan.Empty);
                return Indent(input, level);
            }));
            core.Set("text", text);
            return core;
        }

        private object RandomPick(RuntimeArguments arguments)
        {
            List<object> values = new List<object>();
            if (arguments.Positional.Count == 1 && IsEnumerableValue(arguments.Positional[0]))
            {
                values.AddRange(Enumerate(arguments.Positional[0], ArtiSourceSpan.Empty));
            }
            else
            {
                values.AddRange(arguments.Positional);
            }

            if (values.Count == 0)
            {
                return null;
            }

            int index = _context.Random.NextInt(0, values.Count - 1);
            return values[index];
        }

        private object Coalesce(RuntimeArguments arguments)
        {
            IEnumerable<object> values;
            if (arguments.Positional.Count == 1 && IsEnumerableValue(arguments.Positional[0]))
            {
                values = Enumerate(arguments.Positional[0], ArtiSourceSpan.Empty);
            }
            else
            {
                values = arguments.Positional;
            }

            foreach (object value in values)
            {
                if (!IsEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        private object JoinNonEmpty(RuntimeArguments arguments)
        {
            string separator = ToText(arguments.Get(0, "separator", true));
            List<object> source = new List<object>();
            if (arguments.Positional.Count >= 2)
            {
                if (arguments.Positional.Count == 2 && IsEnumerableValue(arguments.Positional[1]))
                {
                    source.AddRange(Enumerate(arguments.Positional[1], ArtiSourceSpan.Empty));
                }
                else
                {
                    for (int index = 1; index < arguments.Positional.Count; index++)
                    {
                        source.Add(arguments.Positional[index]);
                    }
                }
            }

            List<string> values = new List<string>();
            foreach (object value in source)
            {
                string text = ToText(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }
            }

            return string.Join(separator, values);
        }

        private object CreateRange(RuntimeArguments arguments)
        {
            int count = arguments.Positional.Count;
            if (count < 1 || count > 3)
            {
                throw new RuntimeFault("range expects one to three positional arguments.");
            }

            int start;
            int stop;
            int step;
            if (count == 1)
            {
                start = 0;
                stop = ToInt(arguments.Positional[0], ArtiSourceSpan.Empty);
                step = 1;
            }
            else
            {
                start = ToInt(arguments.Positional[0], ArtiSourceSpan.Empty);
                stop = ToInt(arguments.Positional[1], ArtiSourceSpan.Empty);
                step = count == 3 ? ToInt(arguments.Positional[2], ArtiSourceSpan.Empty) : 1;
            }

            if (step == 0)
            {
                throw new RuntimeFault("range step cannot be zero.");
            }

            OxygenCollection result = new OxygenCollection(false, OxygenTypes.Int, 0);
            if (step > 0)
            {
                for (int value = start; value < stop; value += step)
                {
                    result.Add(value);
                    if (result.Count > Math.Max(1, _context.Options.MaxSteps))
                    {
                        throw new RuntimeFault("range produced too many values.");
                    }
                }
            }
            else
            {
                for (int value = start; value > stop; value += step)
                {
                    result.Add(value);
                    if (result.Count > Math.Max(1, _context.Options.MaxSteps))
                    {
                        throw new RuntimeFault("range produced too many values.");
                    }
                }
            }

            return result;
        }


        private bool TryGetMember(object target, string member, out object value)
        {
            if (target is OxygenTypeExport typeExport) return TryTypeMember(typeExport, member, out value);
            if (target is PawnContext pawn)
            {
                switch (member)
                {
                    case "id": value = pawn.Id; return true;
                    case "name": value = pawn.Name; return true;
                    case "mood": value = pawn.Mood; return true;
                    case "food": value = pawn.Food; return true;
                    case "rest": value = pawn.Rest; return true;
                    case "drafted": value = pawn.Drafted; return true;
                    case "downed": value = pawn.Downed; return true;
                    case "colonist": value = pawn.Colonist; return true;
                    default:
                        if (pawn.Facts != null && pawn.Facts.TryGetMember(member, out value)) return true;
                        value = null;
                        return false;
                }
            }
            if (target is GameContext game)
            {
                switch (member)
                {
                    case "tick": value = game.Tick; return true;
                    case "hour": value = game.Hour; return true;
                    default: value = null; return false;
                }
            }
            if (ReferenceEquals(target, MissingValue))
            {
                value = member == "exists" ? ExistsCallable.Instance : null;
                return value != null;
            }

            RuntimeObject runtimeObject = target as RuntimeObject;
            OxygenStruct oxygenStruct = target as OxygenStruct;
            if (runtimeObject != null && runtimeObject.TryGet(member, out value))
            {
                value = UnwrapExport(value, false);
                return true;
            }

            IOxygenHostValue hostValue = target as IOxygenHostValue;
            if (hostValue != null && !(target is RuntimeObject) && hostValue.TryGetMember(member, out value))
            {
                value = UnwrapExport(value, false);
                return true;
            }

            if (oxygenStruct != null && TryReadStruct(oxygenStruct, member, out value))
            {
                return true;
            }

            OxygenCollection oxygenCollection = target as OxygenCollection;
            if (oxygenCollection != null && TryCollectionMember(oxygenCollection, member, out value))
            {
                return true;
            }

            IDictionary<string, object> dictionary = target as IDictionary<string, object>;
            if (dictionary != null && dictionary.TryGetValue(member, out value))
            {
                return true;
            }

            IDictionary nonGenericDictionary = target as IDictionary;
            if (nonGenericDictionary != null && nonGenericDictionary.Contains(member))
            {
                value = nonGenericDictionary[member];
                return true;
            }

            string text = target as string;
            if (text != null)
            {
                if (string.Equals(member, "length", StringComparison.Ordinal))
                {
                    value = text.Length;
                    return true;
                }

                IArtiCallable stringCallable;
                if (ArtiStringFunctions.TryGetCallable(member, out stringCallable))
                {
                    value = stringCallable;
                    return true;
                }
            }

            IList list = target as IList;
            if (list != null)
            {
                if (string.Equals(member, "count", StringComparison.Ordinal)
                    || string.Equals(member, "size", StringComparison.Ordinal)
                    || string.Equals(member, "length", StringComparison.Ordinal))
                {
                    value = list.Count;
                    return true;
                }

                if (string.Equals(member, "first", StringComparison.Ordinal))
                {
                    value = list.Count == 0 ? null : list[0];
                    return true;
                }

                if (string.Equals(member, "last", StringComparison.Ordinal))
                {
                    value = list.Count == 0 ? null : list[list.Count - 1];
                    return true;
                }
            }



            if (member == "exists")
            {
                value = ExistsCallable.Instance;
                return true;
            }
            value = null;
            return false;
        }

        private bool TryGetIndex(object target, object index, out object value)
        {
            RuntimeObject runtimeObject = target as RuntimeObject;
            string key = index as string;
            if (key != null && IsReadOnlyHost(target))
                return TryGetMember(target, key, out value);
            if (runtimeObject != null && key != null)
            {
                if (!runtimeObject.TryGet(key, out value))
                {
                    RuntimeError(ArtiSourceSpan.Empty, "The member '" + key + "' is not available.");
                }

                return true;
            }

            IDictionary<string, object> dictionary = target as IDictionary<string, object>;
            if (dictionary != null && key != null && dictionary.TryGetValue(key, out value))
            {
                return true;
            }

            IDictionary nonGenericDictionary = target as IDictionary;
            if (nonGenericDictionary != null && nonGenericDictionary.Contains(index))
            {
                value = nonGenericDictionary[index];
                return true;
            }

            int numericIndex;
            if (TryGetInt(index, out numericIndex))
            {
                IList list = target as IList;
                if (list != null && numericIndex >= 0 && numericIndex < list.Count)
                {
                    value = list[numericIndex];
                    return true;
                }

                Array array = target as Array;
                if (array != null && array.Rank == 1 && numericIndex >= 0 && numericIndex < array.Length)
                {
                    value = array.GetValue(numericIndex);
                    return true;
                }

                string text = target as string;
                if (text != null && numericIndex >= 0 && numericIndex < text.Length)
                {
                    value = text[numericIndex].ToString();
                    return true;
                }
            }



            value = null;
            return false;
        }

        private object ApplyBinary(ArtiTokenKind op, object left, object right, ArtiSourceSpan span)
        {
            switch (op)
            {
                case ArtiTokenKind.Pipe:
                    RuntimeArguments pipelineArguments = new RuntimeArguments();
                    pipelineArguments.Add(new RuntimeArgument(null, left));
                    return InvokeCallable(right, pipelineArguments);
                case ArtiTokenKind.QuestionQuestion:
                    return left ?? right;
                case ArtiTokenKind.EqualEqual:
                    return AreEqual(left, right);
                case ArtiTokenKind.BangEqual:
                    return !AreEqual(left, right);
                case ArtiTokenKind.Less:
                    return Compare(left, right, span) < 0;
                case ArtiTokenKind.LessEqual:
                    return Compare(left, right, span) <= 0;
                case ArtiTokenKind.Greater:
                    return Compare(left, right, span) > 0;
                case ArtiTokenKind.GreaterEqual:
                    return Compare(left, right, span) >= 0;
                case ArtiTokenKind.Plus:
                    if (left is string || right is string)
                    {
                        return ToText(left) + ToText(right);
                    }

                    return NumericOperation(left, right, op, span);
                case ArtiTokenKind.Minus:
                case ArtiTokenKind.Star:
                case ArtiTokenKind.Slash:
                case ArtiTokenKind.Percent:
                    return NumericOperation(left, right, op, span);
                default:
                    RuntimeError(span, "Unsupported binary operator.");
                    return null;
            }
        }

        private object NumericOperation(object left, object right, ArtiTokenKind op, ArtiSourceSpan span)
        {
            if (left is int && right is int && op != ArtiTokenKind.Slash)
            {
                long leftNumber = (int)left;
                long rightNumber = (int)right;
                if (op == ArtiTokenKind.Percent && rightNumber == 0L)
                {
                    RuntimeError(span, "Division by zero.");
                }

                long exact;
                switch (op)
                {
                    case ArtiTokenKind.Plus: exact = leftNumber + rightNumber; break;
                    case ArtiTokenKind.Minus: exact = leftNumber - rightNumber; break;
                    case ArtiTokenKind.Star: exact = leftNumber * rightNumber; break;
                    case ArtiTokenKind.Percent: exact = leftNumber % rightNumber; break;
                    default: RuntimeError(span, "Unsupported numeric operator."); return null;
                }

                if (exact < int.MinValue || exact > int.MaxValue)
                {
                    RuntimeError(span, "The integer result is outside the int range.");
                }

                return (int)exact;
            }

            if (!IsNumeric(left) || !IsNumeric(right))
            {
                RuntimeError(span, "Expected a number.");
            }

            double leftFloat = ToDouble(left, span);
            double rightFloat = ToDouble(right, span);
            if ((op == ArtiTokenKind.Slash || op == ArtiTokenKind.Percent) && rightFloat == 0d)
            {
                RuntimeError(span, "Division by zero.");
            }

            switch (op)
            {
                case ArtiTokenKind.Plus: return leftFloat + rightFloat;
                case ArtiTokenKind.Minus: return leftFloat - rightFloat;
                case ArtiTokenKind.Star: return leftFloat * rightFloat;
                case ArtiTokenKind.Slash: return leftFloat / rightFloat;
                case ArtiTokenKind.Percent: return leftFloat % rightFloat;
                default: RuntimeError(span, "Unsupported numeric operator."); return null;
            }
        }

        private static ArtiTokenKind CompoundOperator(ArtiTokenKind assignmentOperator)
        {
            switch (assignmentOperator)
            {
                case ArtiTokenKind.PlusEqual: return ArtiTokenKind.Plus;
                case ArtiTokenKind.MinusEqual: return ArtiTokenKind.Minus;
                case ArtiTokenKind.StarEqual: return ArtiTokenKind.Star;
                case ArtiTokenKind.SlashEqual: return ArtiTokenKind.Slash;
                case ArtiTokenKind.PercentEqual: return ArtiTokenKind.Percent;
                default: return ArtiTokenKind.EqualEqual;
            }
        }

        private static bool AreEqual(object left, object right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (left is OxygenStruct || left is OxygenCollection || left is IOxygenHostValue
                || right is OxygenStruct || right is OxygenCollection || right is IOxygenHostValue)
            {
                return ReferenceEquals(left, right);
            }

            if (TryUnwrapNumber(left, out double leftNumber) && TryUnwrapNumber(right, out double rightNumber))
            {
                return leftNumber == rightNumber;
            }

            return Equals(left, right);
        }

        private static int Compare(object left, object right, ArtiSourceSpan span)
        {
            if (TryUnwrapNumber(left, out double leftValue) && TryUnwrapNumber(right, out double rightValue))
            {
                return leftValue.CompareTo(rightValue);
            }


            if (left is string && right is string)
            {
                return string.CompareOrdinal((string)left, (string)right);
            }

            throw new RuntimeFault("Values are not comparable.");
        }

        private static bool IsTruthy(object value)
        {
            if (value is IArtiCallable || value is IOxygenHostCallable)
                throw new RuntimeFault("A function cannot be used as a condition; call it with (...) first.");
            if (value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            double numeric;
            if (TryUnwrapNumber(value, out numeric))
            {
                return numeric != 0d;
            }


            string text = value as string;
            if (text != null)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            OxygenCollection oxygenCollection = value as OxygenCollection;
            if (oxygenCollection != null)
            {
                return oxygenCollection.Count > 0;
            }

            ICollection collection = value as ICollection;
            if (collection != null)
            {
                return collection.Count > 0;
            }

            return true;
        }

        private static bool IsEmpty(object value)
        {
            if (value == null)
            {
                return true;
            }

            string text = value as string;
            if (text != null)
            {
                return string.IsNullOrWhiteSpace(text);
            }

            ICollection collection = value as ICollection;
            return collection != null && collection.Count == 0;
        }

        private static bool TryUnwrapNumber(object value, out double number)
        {
            if (value is double?)
            {
                double? nullable = (double?)value;
                if (nullable.HasValue)
                {
                    number = nullable.Value;
                    return true;
                }
            }

            if (IsNumeric(value))
            {
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }

            number = 0d;
            return false;
        }

        private static bool IsNumeric(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static bool IsIntegral(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong;
        }

        private static bool TryGetInt(object value, out int result)
        {
            if (IsNumeric(value))
            {
                try
                {
                    result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            result = 0;
            return false;
        }

        private static int ToInt(object value, ArtiSourceSpan span)
        {
            int result;
            if (TryGetInt(value, out result))
            {
                return result;
            }

            string text = value as string;
            if (text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            throw new RuntimeFault("Expected an integer.");
        }

        private static double ToDouble(object value, ArtiSourceSpan span)
        {
            if (IsNumeric(value))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            string text = value as string;
            double result;
            if (text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            throw new RuntimeFault("Expected a number.");
        }

        private static object NumericValue(object value, ArtiSourceSpan span)
        {
            return IsIntegral(value) ? (object)ToInt(value, span) : ToDouble(value, span);
        }

        private static object NegateNumber(object value, ArtiSourceSpan span)
        {
            if (value is int)
            {
                int number = (int)value;
                if (number == int.MinValue)
                {
                    throw new RuntimeFault("The integer result is outside the int range.");
                }

                return -number;
            }

            if (IsIntegral(value))
            {
                return -ToInt(value, span);
            }

            return -ToDouble(value, span);
        }

        private static bool ToBool(object value, bool defaultValue)
        {
            if (value == null)
            {
                return defaultValue;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(ToText(value), out parsed) ? parsed : defaultValue;
        }

        private static string ToText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string text = value as string;
            if (text != null)
            {
                return text;
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (IsNumeric(value))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }



            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string FormatValue(object value)
        {
            return value == null ? "null" : ToText(value);
        }

        private static string TrimLines(string value)
        {
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] sourceLines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            List<string> lines = new List<string>(sourceLines.Length);
            foreach (string line in sourceLines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 && lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                {
                    continue;
                }

                lines.Add(trimmed);
            }

            while (lines.Count > 0 && lines[0].Length == 0)
            {
                lines.RemoveAt(0);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return string.Join("\n", lines);
        }

        private static string Indent(string value, int level)
        {
            if (level <= 0)
            {
                return value ?? string.Empty;
            }

            string prefix = new string(' ', level * 2);
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            for (int index = 0; index < lines.Length; index++)
            {
                lines[index] = prefix + lines[index];
            }

            return string.Join("\n", lines);
        }


        private static bool IsEnumerableValue(object value)
        {
            return value != null && !(value is string) && value is IEnumerable;
        }

        private static IEnumerable<object> Enumerate(object value, ArtiSourceSpan span)
        {
            if (value == null)
            {
                yield break;
            }

            string text = value as string;
            if (text != null)
            {
                foreach (char character in text)
                {
                    yield return character.ToString();
                }

                yield break;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                throw new RuntimeFault("Expected an iterable value.");
            }

            foreach (object item in enumerable)
            {
                yield return item;
            }
        }

        private void Touch(ArtiSourceSpan span)
        {
            _steps++;
            if (_steps > Math.Max(1, _context.Options.MaxSteps))
            {
                RuntimeError(span, "The maximum Arti execution step count was exceeded.");
            }
        }

        internal void RuntimeError(ArtiSourceSpan span, string message)
        {
            RecordRuntimeError(span, message);
            throw new ExecutionStopException();
        }

        internal void RecordRuntimeError(ArtiSourceSpan span, string message)
        {
            RuntimeScope moduleScope = _diagnosticScope;
            while (moduleScope != null && string.IsNullOrEmpty(moduleScope.ModulePackageId)) moduleScope = moduleScope.Parent;
            if (moduleScope != null)
                message = moduleScope.ModulePath + "@" + moduleScope.ModulePackageId + ": " + message;
            else if (_moduleChain.Count > 0)
            {
                string identity = _moduleChain.Peek();
                int separator = identity.IndexOf('/');
                message = identity.Substring(separator + 1) + "@" + identity.Substring(0, separator) + ": " + message;
            }
            ReportError(4000, span, message);
        }

        internal void AddDiagnostics(IEnumerable<ArtiDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            foreach (ArtiDiagnostic diagnostic in diagnostics)
            {
                AddDiagnostic(diagnostic);
            }
        }

        internal static bool HasErrors(IEnumerable<ArtiDiagnostic> diagnostics)
        {
            foreach (ArtiDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        internal sealed class ExecutionStopException : Exception
        {
        }

        private static readonly object MissingValue = new object();

        private sealed class ExistsCallable : IArtiMethodCallable
        {
            public static readonly ExistsCallable Instance = new ExistsCallable();

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                if (positional.Count == 1 && named.Count == 0)
                    return !ReferenceEquals(positional[0], MissingValue);
                if (positional.Count == 0 && named.Count == 1 && named.TryGetValue("value", out object value))
                    return !ReferenceEquals(value, MissingValue);
                throw new RuntimeFault("exists expects exactly one argument: value.");
            }

            public bool TryInvokeWithReceiver(object receiver, IList<object> positional,
                IDictionary<string, object> named, out object value)
            {
                if (positional.Count != 0 || named.Count != 0)
                    throw new RuntimeFault("The inverted exists call does not accept extra arguments.");
                value = !ReferenceEquals(receiver, MissingValue);
                return true;
            }
        }


        private sealed class ReturnSignal : Exception
        {
            public ReturnSignal(object value, bool hasValue = true)
            {
                Value = value;
                HasValue = hasValue;
            }

            public object Value { get; }

            public bool HasValue { get; }
        }

        private sealed class BreakSignal : Exception
        {
        }

        private sealed class ContinueSignal : Exception
        {
        }

    }
}
