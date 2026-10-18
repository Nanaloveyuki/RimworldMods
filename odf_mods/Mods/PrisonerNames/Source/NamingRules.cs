using System;
using System.IO;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Odf.PrisonerNames
{
    public sealed class NamingRules
    {
        readonly ArtiExecutor executor;
        readonly ArtiFunctionDeclarationStatement function;
        readonly ArtiFunctionDeclarationStatement eventFunction;
        public static IOxygenModuleCatalog ModuleCatalog { get; set; }
        public static Func<bool, string> InstalledDefault { get; set; }
        public static Func<string> InstalledEvents { get; set; }

        NamingRules(ArtiExecutor executor, ArtiFunctionDeclarationStatement function, ArtiFunctionDeclarationStatement eventFunction)
        {
            this.executor = executor;
            this.function = function;
            this.eventFunction = eventFunction;
        }

        public static NamingRules Compile(string source, IOxygenModuleCatalog modules = null, IOxygenLocalization localization = null)
        {
            ArtiParseResult parsed = new ArtiParser().Parse(source ?? string.Empty);
            foreach (ArtiDiagnostic diagnostic in parsed.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error) throw Error(diagnostic);
            ArtiAnalysisResult analyzed = new ArtiAnalyzer().Analyze(parsed.Program);
            foreach (ArtiDiagnostic diagnostic in analyzed.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error) throw Error(diagnostic);
            ArtiFunctionDeclarationStatement entry = null;
            ArtiFunctionDeclarationStatement events = null;
            foreach (ArtiStatement statement in parsed.Program.Statements)
            {
                if (statement is ArtiFunctionDeclarationStatement fn)
                {
                    if (fn.Name == "codename") entry = fn;
                    else if (fn.Name == "on_event") events = fn;
                    else throw new ScriptEvaluationException("Only codename and on_event functions are allowed.", fn.Span.Line, fn.Span.Column);
                }
                else if (!(statement is ArtiVariableDeclarationStatement)
                    && !(statement is ArtiStructDeclarationStatement) && !(statement is ArtiUseStatement))
                    throw new ScriptEvaluationException("Only imports, functions, structs, constants and variables are allowed.", statement.Span.Line, statement.Span.Column);
            }
            if (entry == null || entry.Parameters.Count != 1)
                throw new ScriptEvaluationException("Define fn codename(pawn) with exactly one parameter.", entry == null ? 1 : entry.Span.Line, entry == null ? 1 : entry.Span.Column);
            if (events != null && events.Parameters.Count != 3)
                throw new ScriptEvaluationException("Define fn on_event(pawn, event, state) with exactly three parameters.", events.Span.Line, events.Span.Column);
            ArtiExecutor executor = new ArtiExecutor();
            ArtiExecutionResult prepared = executor.Execute(parsed.Program,
                new ArtiExecutionContext(options: new ArtiExecutionOptions { MaxSteps = 3000, MaxCallDepth = 24 }, modules: modules ?? ModuleCatalog,
                    localization: localization));
            foreach (ArtiDiagnostic diagnostic in prepared.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error) throw Error(diagnostic);
            return new NamingRules(executor, entry, events);
        }

        public string Evaluate(PawnContext pawn, GameContext game)
        {
            object result = executor.InvokeIsolated(function, pawn, game);
            if (result == null) return null;
            if (!(result is string text))
                throw new ScriptEvaluationException("codename must return a string or null.", function.Span.Line, function.Span.Column);
            return ValidateAlias(text);
        }

        public bool HasEvents { get { return eventFunction != null; } }

        public void EvaluateEvent(PawnContext pawn, GameContext game, object message, object state)
        {
            if (eventFunction == null) return;
            executor.InvokeIsolated(eventFunction, pawn, game, message, state);
        }

        public static string ValidateAlias(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (text.Length > 120) throw new ArgumentException("A codename may contain at most 120 characters.");
            for (int i = 0; i < text.Length; i++)
                if (char.IsControl(text[i]) || text[i] == '<' || text[i] == '>')
                    throw new ArgumentException("A codename cannot contain line breaks, control characters or rich-text tags.");
            return text.Trim();
        }

        public static string DefaultSource(bool chinese)
        {
            string loaded = InstalledDefault == null ? null : InstalledDefault(chinese);
            if (loaded != null) return loaded;
            using (Stream stream = typeof(NamingRules).Assembly.GetManifestResourceStream(chinese ? "DefaultNaming.zh.oxy" : "DefaultNaming.en.oxy"))
            using (StreamReader reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
        public static string DefaultEvents()
        {
            string loaded = InstalledEvents == null ? null : InstalledEvents();
            return string.IsNullOrEmpty(loaded) ? "fn on_event(pawn, event, state) {}\n" : loaded;
        }

        static ScriptEvaluationException Error(ArtiDiagnostic diagnostic)
        {
            return new ScriptEvaluationException(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column);
        }
    }
}
