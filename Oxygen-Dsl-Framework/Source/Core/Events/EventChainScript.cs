using System;
using System.Collections.Generic;
using System.Threading;
using Oxygendsl.Language;

namespace Oxygendsl.Core.Events
{
    // Prepared code is reused, but only immutable scalar globals and readonly host
    // snapshots are admitted. Each function invocation gets fresh local scopes and
    // the executor's bounded step/call budget; no mutable globals survive a call.
    public sealed class EventChainScript
    {
        private readonly ArtiExecutor executor;
        private readonly object callable;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly object[] argument = new object[1];
        private readonly ReadOnlySnapshot snapshot = new ReadOnlySnapshot(null);
        private bool evaluating;

        private EventChainScript(ArtiExecutor executor, object callable)
        { this.executor = executor; this.callable = callable; }

        public static EventChainScript Compile(string source, string functionName = "evaluate",
            IOxygenModuleCatalog modules = null, IOxygenModuleProvider hostModules = null)
        {
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentException("A decision function name is required.", nameof(functionName));
            ArtiParseResult parsed = Parse(source ?? "", false);
            ArtiFunctionDeclarationStatement function = null;
            foreach (ArtiStatement statement in parsed.Program.Statements)
            {
                var candidate = statement as ArtiFunctionDeclarationStatement;
                if (candidate != null && candidate.Name == functionName) function = candidate;
            }
            if (function == null || function.Parameters.Count != 1 || function.TypeParameters.Count != 0)
                throw new ArgumentException("The EventChain decision function must exist and accept exactly one snapshot argument.", nameof(functionName));
            var executor = new ArtiExecutor();
            var result = executor.Execute(parsed.Program, new ArtiExecutionContext(random: RejectRandom.Instance,
                options: new ArtiExecutionOptions { MaxSteps = 10000, MaxCallDepth = 64 },
                modules: modules == null ? null : new SafeCatalog(modules), hostModules: new DecisionModules(hostModules)));
            if (result.HasErrors) throw Diagnostics(result.Diagnostics);
            object callable = executor.EvaluatePrepared(new ArtiNameExpression(function.Span, functionName));
            return new EventChainScript(executor, callable);
        }

        public EventChainDecision Evaluate(IOxygenHostValue context)
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("EventChain script belongs to its preparing thread.");
            if (evaluating) throw new InvalidOperationException("EventChain decision scripts are non-reentrant.");
            if (context == null) throw new ArgumentNullException(nameof(context));
            evaluating = true;
            try
            {
                snapshot.Bind(context);
                argument[0] = snapshot;
                var result = executor.InvokePrepared(callable, argument) as EventChainDecision;
                if (result == null) throw new ScriptEvaluationException("The decision function must return events.ctx@event wait, finish, or action.", 1, 1);
                return result;
            }
            finally { snapshot.Bind(null); argument[0] = null; evaluating = false; }
        }

        private static ArtiParseResult Parse(string source, bool module)
        {
            ArtiParseResult parsed = new ArtiParser().Parse(source);
            if (parsed.HasErrors) throw Diagnostics(parsed.Diagnostics);
            ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program, module);
            if (analysis.HasErrors) throw Diagnostics(analysis.Diagnostics);
            var constants = new HashSet<string>(StringComparer.Ordinal);
            foreach (ArtiStatement statement in parsed.Program.Statements)
            {
                if (statement is ArtiUseStatement) continue;
                var function = statement as ArtiFunctionDeclarationStatement;
                if (function != null) { ValidateBody(function.Body); continue; }
                var variable = statement as ArtiVariableDeclarationStatement;
                if (variable == null || !variable.IsConst || !ScalarConstant(variable.Value, constants))
                    throw new ScriptEvaluationException("EventChain script globals must be immutable scalar constants, imports, or functions; mutable globals and retained object graphs are unsafe.", statement.Span.Line, statement.Span.Column);
                constants.Add(variable.Name);
            }
            return parsed;
        }

        private static void ValidateBody(ArtiBlockStatement body)
        {
            if (body == null) return;
            foreach (ArtiStatement statement in body.Statements)
            {
                // Even built-in core/random namespaces are interpreter objects. A
                // member write through a local alias could otherwise persist there.
                var assignment = statement as ArtiAssignmentStatement;
                if (assignment != null && !(assignment.Target is ArtiNameExpression))
                    throw new ScriptEvaluationException("Decision functions may assign local scalars, not object members or collection indexes.", statement.Span.Line, statement.Span.Column);
                var unpack = statement as ArtiUnpackDeclarationStatement;
                if (unpack != null)
                    foreach (ArtiExpression target in unpack.Targets.Items)
                        if (!(target is ArtiNameExpression))
                            throw new ScriptEvaluationException("Decision unpacking targets must be local names.", statement.Span.Line, statement.Span.Column);
                var branch = statement as ArtiIfStatement;
                if (branch != null)
                {
                    foreach (ArtiIfBranch arm in branch.Branches) ValidateBody(arm.Body);
                    ValidateBody(branch.ElseBody);
                }
                var loop = statement as ArtiForStatement;
                if (loop != null) ValidateBody(loop.Body);
                var repeated = statement as ArtiWhileStatement;
                if (repeated != null) ValidateBody(repeated.Body);
                var function = statement as ArtiFunctionDeclarationStatement;
                if (function != null) ValidateBody(function.Body);
                var block = statement as ArtiBlockStatement;
                if (block != null) ValidateBody(block);
                if (statement is ArtiUseStatement || statement is ArtiStructDeclarationStatement
                    || statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement)
                    throw new ScriptEvaluationException("Decision bodies cannot retain type or module initialization state.", statement.Span.Line, statement.Span.Column);
            }
        }

        private static bool ScalarConstant(ArtiExpression expression, HashSet<string> constants)
        {
            var literal = expression as ArtiLiteralExpression;
            if (literal != null) return literal.Value == null || literal.Value is string || literal.Value is bool
                || literal.Value is int || literal.Value is double || literal.Value is float || literal.Value is long;
            var name = expression as ArtiNameExpression;
            if (name != null) return constants.Contains(name.Name);
            var unary = expression as ArtiUnaryExpression;
            if (unary != null) return ScalarConstant(unary.Operand, constants);
            var binary = expression as ArtiBinaryExpression;
            if (binary != null) return ScalarConstant(binary.Left, constants) && ScalarConstant(binary.Right, constants);
            var interpolated = expression as ArtiInterpolatedStringExpression;
            if (interpolated != null)
            {
                foreach (ArtiExpression part in interpolated.Parts) if (!ScalarConstant(part, constants)) return false;
                return true;
            }
            return false;
        }

        private static ScriptEvaluationException Diagnostics(IList<ArtiDiagnostic> diagnostics)
        {
            foreach (ArtiDiagnostic diagnostic in diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                    return new ScriptEvaluationException(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column);
            return new ScriptEvaluationException("Invalid EventChain script.", 1, 1);
        }

        private sealed class SafeCatalog : IOxygenModuleCatalog
        {
            private readonly IOxygenModuleCatalog inner;
            internal SafeCatalog(IOxygenModuleCatalog inner) { this.inner = inner; }
            public bool TryGetSource(string packageId, string modulePath, out string source)
            {
                if (!inner.TryGetSource(packageId, modulePath, out source)) return false;
                Parse(source ?? "", true);
                return true;
            }
        }

        private sealed class DecisionModules : IOxygenModuleProvider
        {
            private readonly IOxygenModuleProvider inner;
            internal DecisionModules(IOxygenModuleProvider inner) { this.inner = inner; }
            public bool TryGetModule(string packageId, string modulePath, out IOxygenHostValue module)
            {
                if (string.Equals(packageId, "event", StringComparison.OrdinalIgnoreCase) && modulePath == "events.ctx")
                { module = DecisionModule.Instance; return true; }
                IOxygenHostValue value;
                if (inner != null && inner.TryGetModule(packageId, modulePath, out value) && value != null)
                { module = new ReadOnlySnapshot(value); return true; }
                module = null;
                return false;
            }
        }

        private sealed class ReadOnlySnapshot : IOxygenHostValue
        {
            private IOxygenHostValue value;
            internal ReadOnlySnapshot(IOxygenHostValue value) { this.value = value; }
            internal void Bind(IOxygenHostValue value) { this.value = value; }
            public bool TryGetMember(string name, out object member)
            {
                if (!value.TryGetMember(name, out member)) return false;
                if (member == null || member is string || member is bool || member is int || member is long
                    || member is float || member is double) return true;
                var nested = member as IOxygenHostValue;
                if (nested != null && !(member is IOxygenHostCallable)) { member = new ReadOnlySnapshot(nested); return true; }
                throw new ScriptEvaluationException("EventChain snapshots expose only readonly facts, not callables, mutable collections, or game objects.", 1, 1);
            }
        }

        private sealed class RejectRandom : IArtiRandomSource
        {
            internal static readonly RejectRandom Instance = new RejectRandom();
            public int NextInt(int minInclusive, int maxInclusive) { throw new InvalidOperationException("Random outcomes belong to receipted host actions, not decision scripts."); }
            public double NextDouble(double minInclusive, double maxInclusive) { throw new InvalidOperationException("Random outcomes belong to receipted host actions, not decision scripts."); }
        }

        private sealed class DecisionModule : IOxygenHostValue
        {
            internal static readonly DecisionModule Instance = new DecisionModule();
            private static readonly Helper Wait = new Helper("wait"), Finish = new Helper("finish"), Action = new Helper("action");
            public bool TryGetMember(string name, out object value)
            {
                switch (name)
                {
                    case "wait": value = Wait; return true;
                    case "finish": value = Finish; return true;
                    case "action": value = Action; return true;
                    default: value = null; return false;
                }
            }
        }

        private sealed class Helper : IOxygenHostCallable
        {
            private readonly string kind;
            internal Helper(string kind) { this.kind = kind; }
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                if (positional.Count != 1) throw new ArgumentException("ctx." + kind + " requires exactly one positional argument.");
                if (kind == "wait")
                {
                    NoNamed(named);
                    return EventChainDecision.Wait(Integer(positional[0], "next_tick", -1));
                }
                if (kind == "finish")
                {
                    NoNamed(named);
                    switch (positional[0] as string)
                    {
                        case "completed": return EventChainDecision.Finish(EventChainEnd.Completed);
                        case "failed": return EventChainDecision.Finish(EventChainEnd.Failed);
                        case "expired": return EventChainDecision.Finish(EventChainEnd.Expired);
                        case "cancelled": return EventChainDecision.Finish(EventChainEnd.Cancelled);
                        default: throw new ArgumentException("Unknown EventChain terminal status.");
                    }
                }
                string action = positional[0] as string;
                if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("An action identity is required.");
                foreach (string name in named.Keys)
                    if (name != "next_tick" && name != "stage") throw new ArgumentException("Unknown ctx.action argument: " + name);
                object next, stage;
                if (!named.TryGetValue("next_tick", out next)) throw new ArgumentException("ctx.action requires next_tick.");
                return EventChainDecision.Action(action, Integer(next, "next_tick", -1),
                    named.TryGetValue("stage", out stage) ? Integer(stage, "stage", -1) : -1);
            }
            private static void NoNamed(IDictionary<string, object> named)
            { if (named.Count != 0) throw new ArgumentException("Unknown EventChain helper argument."); }
            private static int Integer(object value, string name, int minimum)
            {
                int number;
                if (value is int) number = (int)value;
                else if (value is long && (long)value <= int.MaxValue && (long)value >= minimum) number = (int)(long)value;
                else throw new ArgumentException(name + " must be an integer in the simulation clock range.");
                if (number < minimum) throw new ArgumentException(name + " is outside the supported range.");
                return number;
            }
        }
    }
}
