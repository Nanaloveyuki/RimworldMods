using System;
using System.Collections.Generic;
using System.Globalization;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public static class ScriptCompiler
    {
        public static ScriptCompilation Compile(string source, IOxygenModuleCatalog modules = null, IOxygenLocalization localization = null)
        {
            ArtiParseResult parsed = new ArtiParser().Parse(source ?? string.Empty);
            ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program);
            List<ScriptDiagnostic> diagnostics = new List<ScriptDiagnostic>();
            AddDiagnostics(diagnostics, parsed.Diagnostics);
            AddDiagnostics(diagnostics, analysis.Diagnostics);
            if (diagnostics.Count > 0)
            {
                return new ScriptCompilation(null, diagnostics);
            }

            try
            {
                CompiledScript compiled = CompiledScript.Prepare(parsed.Program, modules, localization);
                List<RuleDefinition> rules = Register(parsed.Program, compiled, diagnostics);
                return diagnostics.Count == 0
                    ? new ScriptCompilation(new RuleProgram(rules), diagnostics)
                    : new ScriptCompilation(null, diagnostics);
            }
            catch (ScriptRegistrationException exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, exception.Line, exception.Column));
                return new ScriptCompilation(null, diagnostics);
            }
        }

        private static List<RuleDefinition> Register(ArtiProgram program, CompiledScript compiled, List<ScriptDiagnostic> diagnostics)
        {
            List<RuleDefinition> rules = new List<RuleDefinition>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (program == null)
            {
                return rules;
            }

            foreach (ArtiStatement statement in program.Statements)
            {
                if (statement is ArtiFunctionDeclarationStatement || statement is ArtiVariableDeclarationStatement
                    || statement is ArtiStructDeclarationStatement || statement is ArtiUseStatement
                    || statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement)
                {
                    continue;
                }

                ArtiExpressionStatement expression = statement as ArtiExpressionStatement;
                ArtiCallExpression call = expression == null ? null : expression.Expression as ArtiCallExpression;
                ArtiNameExpression target = call == null ? null : call.Target as ArtiNameExpression;
                if (target == null || (target.Name != "mood" && target.Name != "memory"))
                {
                    throw new ScriptRegistrationException("Only imports, structs, traits, impls, functions, constants, variables, mood, and memory are allowed.", statement.Span.Line, statement.Span.Column);
                }

                RuleDefinition rule = ReadRule(call, target.Name == "memory", compiled);
                if (!ids.Add(rule.Id))
                {
                    throw new ScriptRegistrationException("Rule id '" + rule.Id + "' is declared more than once.", rule.Line, rule.Column);
                }

                rules.Add(rule);
            }

            if (diagnostics.Count > 0)
            {
                return new List<RuleDefinition>();
            }

            return rules;
        }

        private static RuleDefinition ReadRule(ArtiCallExpression call, bool memory, CompiledScript compiled)
        {
            string id = null;
            string label = null;
            string description = null;
            float? offset = null;
            RuleTarget? target = null;
            int? duration = null;
            int? cooldown = null;
            MemoryMode? mode = null;
            int? stackLimit = null;
            ArtiExpression when = null;
            bool sawPositional = false;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ArtiArgument argument in call.Arguments)
            {
                if (argument == null || argument.Value == null)
                {
                    throw new ScriptRegistrationException("A rule argument is missing.", call.Span.Line, call.Span.Column);
                }

                if (argument.Name == null)
                {
                    if (sawPositional || id != null)
                    {
                        throw new ScriptRegistrationException("Only the rule id may be positional.", argument.Value.Span.Line, argument.Value.Span.Column);
                    }

                    sawPositional = true;
                    id = ReadText(compiled, argument.Value, "id");
                    continue;
                }

                if (!seen.Add(argument.Name))
                {
                    throw new ScriptRegistrationException("Rule argument '" + argument.Name + "' was supplied more than once.", argument.Value.Span.Line, argument.Value.Span.Column);
                }

                switch (argument.Name)
                {
                    case "id":
                        id = ReadText(compiled, argument.Value, "id");
                        break;
                    case "label":
                        label = ReadText(compiled, argument.Value, "label");
                        break;
                    case "description":
                        description = ReadText(compiled, argument.Value, "description");
                        break;
                    case "offset":
                        offset = ReadFiniteFloat(compiled, argument.Value, "offset");
                        break;
                    case "target":
                        target = ReadTarget(compiled, argument.Value);
                        break;
                    case "when":
                        when = argument.Value;
                        break;
                    case "duration":
                        if (!memory) RejectUnknown(argument);
                        duration = ReadPositiveInt(compiled, argument.Value, "duration");
                        break;
                    case "cooldown":
                        if (!memory) RejectUnknown(argument);
                        cooldown = ReadPositiveInt(compiled, argument.Value, "cooldown");
                        break;
                    case "mode":
                        if (!memory) RejectUnknown(argument);
                        mode = ReadMode(compiled, argument.Value);
                        break;
                    case "stack_limit":
                        if (!memory) RejectUnknown(argument);
                        stackLimit = ReadPositiveInt(compiled, argument.Value, "stack_limit");
                        break;
                    default:
                        RejectUnknown(argument);
                        break;
                }
            }

            Require(id, "id", call.Span);
            Require(label, "label", call.Span);
            Require(description, "description", call.Span);
            if (!offset.HasValue) Require(null, "offset", call.Span);
            if (!target.HasValue) Require(null, "target", call.Span);
            if (memory)
            {
                if (!duration.HasValue) Require(null, "duration", call.Span);
                if (!cooldown.HasValue) Require(null, "cooldown", call.Span);
                if (!mode.HasValue) Require(null, "mode", call.Span);
                if (mode == MemoryMode.Stack && !stackLimit.HasValue)
                {
                    throw new ScriptRegistrationException("stack mode requires a positive stack_limit.", call.Span.Line, call.Span.Column);
                }

                if (mode == MemoryMode.Refresh && stackLimit.HasValue)
                {
                    throw new ScriptRegistrationException("refresh mode does not accept stack_limit.", call.Span.Line, call.Span.Column);
                }
            }

            return new RuleDefinition
            {
                Id = id,
                Label = label,
                Description = description,
                Offset = offset.Value,
                Kind = memory ? RuleKind.Memory : RuleKind.Mood,
                Target = target.Value,
                DurationTicks = duration ?? 0,
                CooldownTicks = cooldown ?? 0,
                Mode = mode ?? MemoryMode.Refresh,
                StackLimit = stackLimit ?? 0,
                Condition = ReadCondition(when, compiled),
                Line = call.Span.Line,
                Column = call.Span.Column
            };
        }

        private static IRuleCondition ReadCondition(ArtiExpression when, CompiledScript compiled)
        {
            if (when == null)
            {
                return null;
            }

            ArtiNameExpression name = when as ArtiNameExpression;
            if (name == null)
            {
                throw new ScriptRegistrationException("when must name a function that accepts one pawn.", when.Span.Line, when.Span.Column);
            }

            ArtiFunctionDeclarationStatement function;
            if (!compiled.TryGetFunction(name.Name, out function) || function.Parameters.Count != 1)
            {
                throw new ScriptRegistrationException("when must name a function that accepts one pawn.", name.Span.Line, name.Span.Column);
            }

            return new FunctionCondition(compiled, function);
        }

        private static string ReadText(CompiledScript compiled, ArtiExpression expression, string name)
        {
            string value = compiled.Evaluate(expression) as string;
            if (value == null)
            {
                throw new ScriptRegistrationException(name + " must be a string.", expression.Span.Line, expression.Span.Column);
            }

            if (name == "id" && value.Length == 0)
            {
                throw new ScriptRegistrationException("id cannot be empty.", expression.Span.Line, expression.Span.Column);
            }

            return value;
        }

        private static float ReadFiniteFloat(CompiledScript compiled, ArtiExpression expression, string name)
        {
            double value = ReadFiniteNumber(compiled, expression, name);
            float result = (float)value;
            if (float.IsInfinity(result) || float.IsNaN(result))
            {
                throw new ScriptRegistrationException(name + " must be a finite number.", expression.Span.Line, expression.Span.Column);
            }

            return result;
        }

        private static int ReadPositiveInt(CompiledScript compiled, ArtiExpression expression, string name)
        {
            double value = ReadFiniteNumber(compiled, expression, name);
            if (value < 1d || value > int.MaxValue || value != Math.Truncate(value))
            {
                throw new ScriptRegistrationException(name + " must be a positive integer.", expression.Span.Line, expression.Span.Column);
            }

            return (int)value;
        }

        private static double ReadFiniteNumber(CompiledScript compiled, ArtiExpression expression, string name)
        {
            object value = compiled.Evaluate(expression);
            if (!IsNumber(value))
            {
                throw new ScriptRegistrationException(name + " must be a finite number.", expression.Span.Line, expression.Span.Column);
            }

            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                throw new ScriptRegistrationException(name + " must be a finite number.", expression.Span.Line, expression.Span.Column);
            }

            return number;
        }

        private static RuleTarget ReadTarget(CompiledScript compiled, ArtiExpression expression)
        {
            string value = ReadText(compiled, expression, "target");
            if (value == "colonists") return RuleTarget.Colonists;
            if (value == "map") return RuleTarget.Map;
            if (value == "selected") return RuleTarget.Selected;
            throw new ScriptRegistrationException("target must be colonists, map, or selected.", expression.Span.Line, expression.Span.Column);
        }

        private static MemoryMode ReadMode(CompiledScript compiled, ArtiExpression expression)
        {
            string value = ReadText(compiled, expression, "mode");
            if (value == "refresh") return MemoryMode.Refresh;
            if (value == "stack") return MemoryMode.Stack;
            throw new ScriptRegistrationException("mode must be refresh or stack.", expression.Span.Line, expression.Span.Column);
        }

        private static void Require(string value, string name, ArtiSourceSpan span)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ScriptRegistrationException("Rule argument '" + name + "' is required.", span.Line, span.Column);
            }
        }

        private static void RejectUnknown(ArtiArgument argument)
        {
            throw new ScriptRegistrationException("Unknown rule argument '" + argument.Name + "'.", argument.Value.Span.Line, argument.Value.Span.Column);
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal;
        }

        private static void AddDiagnostics(List<ScriptDiagnostic> destination, IEnumerable<ArtiDiagnostic> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (ArtiDiagnostic diagnostic in source)
            {
                if (diagnostic != null && diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                {
                    destination.Add(new ScriptDiagnostic(diagnostic.ToString(), diagnostic.Span.Line, diagnostic.Span.Column));
                }
            }
        }

        private sealed class CompiledScript
        {
            private readonly ArtiProgram _program;
            private readonly Dictionary<string, ArtiFunctionDeclarationStatement> _functions =
                new Dictionary<string, ArtiFunctionDeclarationStatement>(StringComparer.Ordinal);
            private ArtiExecutor _executor;

            private CompiledScript(ArtiProgram program, ArtiExecutor executor)
            {
                _program = program;
                _executor = executor;
                if (program == null)
                {
                    return;
                }

                foreach (ArtiStatement statement in program.Statements)
                {
                    ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                    if (function != null)
                    {
                        _functions[function.Name] = function;
                    }
                }
            }

            public static CompiledScript Prepare(ArtiProgram program, IOxygenModuleCatalog modules, IOxygenLocalization localization)
            {
                ArtiExecutor executor = new ArtiExecutor();
                ArtiExecutionResult prepared = executor.Execute(program, new ArtiExecutionContext(modules: modules, localization: localization));
                if (prepared.HasErrors)
                {
                    ArtiDiagnostic diagnostic = FirstError(prepared.Diagnostics);
                    throw new ScriptRegistrationException(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column);
                }

                return new CompiledScript(program, executor);
            }

            public bool TryGetFunction(string name, out ArtiFunctionDeclarationStatement function)
            {
                return _functions.TryGetValue(name, out function);
            }

            public object Evaluate(ArtiExpression expression)
            {
                try
                {
                    return _executor.EvaluatePrepared(expression);
                }
                catch (ScriptEvaluationException exception)
                {
                    throw new ScriptRegistrationException(exception.Message, exception.Line, exception.Column);
                }
            }

            public object Invoke(ArtiFunctionDeclarationStatement function, PawnContext pawn, GameContext game)
            {
                return _executor.InvokeIsolated(function, pawn, game);
            }

            private static ArtiDiagnostic FirstError(IEnumerable<ArtiDiagnostic> diagnostics)
            {
                foreach (ArtiDiagnostic diagnostic in diagnostics)
                {
                    if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                    {
                        return diagnostic;
                    }
                }

                return new ArtiDiagnostic(ArtiDiagnosticSeverity.Error, "ART4000", "Condition preparation failed.", ArtiSourceSpan.Empty);
            }
        }

        private sealed class FunctionCondition : IRuleCondition
        {
            private readonly CompiledScript _script;
            private readonly ArtiFunctionDeclarationStatement _function;

            public FunctionCondition(CompiledScript script, ArtiFunctionDeclarationStatement function)
            {
                _script = script;
                _function = function;
            }

            public bool Evaluate(PawnContext pawn, GameContext game)
            {
                object value = _script.Invoke(_function, pawn, game);
                if (value is bool)
                {
                    return (bool)value;
                }

                throw new ScriptEvaluationException("when must return a boolean.", _function.Span.Line, _function.Span.Column);
            }
        }


        private sealed class ScriptRegistrationException : Exception
        {
            public ScriptRegistrationException(string message, int line, int column)
                : base(message)
            {
                Line = line;
                Column = column;
            }

            public int Line { get; }
            public int Column { get; }
        }
    }
}
