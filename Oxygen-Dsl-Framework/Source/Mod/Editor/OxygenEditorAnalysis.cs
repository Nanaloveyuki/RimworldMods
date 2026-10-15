using System;
using System.Collections.Generic;
using Oxygendsl.Language;

namespace Oxygendsl.Mod.Editor
{
    internal static class OxygenEditorAnalysis
    {
        public static List<Dictionary<string, object>> Analyze(string source, bool isModule, string language)
        {
            source = source ?? string.Empty;
            ArtiDiagnosticLanguage diagnosticLanguage = string.Equals(language, "en", StringComparison.Ordinal)
                ? ArtiDiagnosticLanguage.English
                : ArtiDiagnosticLanguage.ChineseSimplified;
            var diagnostics = new List<Dictionary<string, object>>();
            ArtiParseResult parsed = new ArtiParser().Parse(source);
            Append(diagnostics, parsed.Diagnostics, diagnosticLanguage, source.Length);
            if (!parsed.HasErrors && parsed.Program != null)
            {
                ArtiAnalysisResult analyzed = new ArtiAnalyzer().Analyze(parsed.Program, isModule || OxygenFileCompiler.IsLibrary(parsed.Program));
                Append(diagnostics, analyzed.Diagnostics, diagnosticLanguage, source.Length);
            }
            return diagnostics;
        }

        static void Append(List<Dictionary<string, object>> destination, IList<ArtiDiagnostic> source, ArtiDiagnosticLanguage language, int length)
        {
            if (source == null) return;
            foreach (ArtiDiagnostic diagnostic in source)
            {
                if (diagnostic == null) continue;
                int start = Clamp(diagnostic.Span.StartOffset, 0, length);
                int end = Clamp(diagnostic.Span.EndOffset, start, length);
                destination.Add(new Dictionary<string, object>
                {
                    { "code", diagnostic.Code ?? string.Empty },
                    { "message", diagnostic.GetMessage(language) ?? string.Empty },
                    { "severity", Severity(diagnostic.Severity) },
                    { "start", start },
                    { "length", end - start }
                });
            }
        }

        static string Severity(ArtiDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case ArtiDiagnosticSeverity.Warning: return "warning";
                case ArtiDiagnosticSeverity.Info: return "info";
                default: return "error";
            }
        }

        static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
