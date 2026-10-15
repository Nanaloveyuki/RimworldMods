using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class LocalizationChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            var provider = new CatalogLocalization { Greeting = "Hello {name}" };
            Fail(check, "lang(\"Greeting\")", null, "missing provider fails when invoked");
            Fail(check, "lang(1)", provider, "nonstring key");
            Fail(check, "lang(\" \" )", provider, "blank key");
            Fail(check, "lang(\"Greeting\", \"Ada\")", provider, "extra positional argument");
            Fail(check, "lang()", provider, "missing key");
            Fail(check, "lang(name: \"Ada\")", provider, "named key not accepted");
            Fail(check, "let sample = {name: \"Ada\"}; lang(\"Greeting\", name: sample)", provider, "object substitution");
            Fail(check, "lang(\"Greeting\", name: [1])", provider, "collection substitution");
            ArtiExecutionResult untouched = new ArtiExecutor().Execute("fn later(): string { return lang(\"Greeting\") }; 1");
            check(!untouched.HasErrors && Equals(untouched.Value, 1), "unexecuted translation requires no provider");

            const string settings = "optional use irismenus@std\n"
                + "fn page_text(): string { return lang(\"Greeting\", name: \"Ada\") }\n"
                + "if irismenus.available { irismenus.registry(pages: [{id: \"p\", title: page_text, items: [{id: \"text\", kind: \"label\", label: page_text}]}]) }";
            OxygenSettingsCompilation page = OxygenSettingsCompiler.Compile(settings, true, localization: provider);
            if (!page.Success) throw new InvalidOperationException(string.Join("; ", page.Diagnostics));
            string first = page.Plan.Pages[0].Title();
            provider.Greeting = "你好 {name}";
            check(first == "Hello Ada" && page.Plan.Pages[0].Title() == "你好 Ada", "prepared title follows language changes");
            var other = new CatalogLocalization { Greeting = "Hola {name}" };
            OxygenSettingsCompilation otherPage = OxygenSettingsCompiler.Compile(settings, true, localization: other);
            check(otherPage.Success && otherPage.Plan.Pages[0].Title() == "Hola Ada" && page.Plan.Pages[0].Title() == "你好 Ada",
                "prepared settings keep independent localization hosts");
            var modules = new OxygenModuleRegistry();
            modules.RegisterSource("example.labels", "words", "pub fn greeting(): string { return lang(\"Greeting\", name: \"Ada\") }");
            OxygenSettingsCompilation imported = OxygenSettingsCompiler.Compile("optional use irismenus@std\nuse words@example.labels\n"
                + "if irismenus.available { irismenus.registry(pages: [{id: \"p\", title: words.greeting, items: []}]) }",
                true, modules: modules, localization: provider);
            if (!imported.Success) throw new InvalidOperationException(string.Join("; ", imported.Diagnostics));
            provider.Greeting = "Welcome {name}";
            check(imported.Plan.Pages[0].Title() == "Welcome Ada", "prepared imported callback sees later language");
            OxygenSettingsCompilation offline = OxygenSettingsCompiler.Compile(settings, true);
            if (!offline.Success) throw new InvalidOperationException(string.Join("; ", offline.Diagnostics));
            try
            {
                offline.Plan.Pages[0].Title();
                throw new InvalidOperationException("Expected missing provider failure.");
            }
            catch (ScriptEvaluationException exception)
            {
                check(exception.Line >= 1 && exception.Message.IndexOf("localization provider", StringComparison.Ordinal) >= 0,
                    "deferred settings translation reports missing provider");
            }
            ScriptCompilation mood = ScriptCompiler.Compile("mood(\"x\", label: lang(\"Greeting\"), description: \"x\", offset: 1, target: \"colonists\")");
            check(!mood.Success && mood.Program == null && mood.Diagnostics[0].Line >= 1,
                "missing provider rejects translated mood registration");
        }

        static void Fail(Action<bool, string> check, string source, IOxygenLocalization provider, string scenario)
        {
            ArtiExecutionResult result = new ArtiExecutor().Execute(source, new ArtiExecutionContext(localization: provider));
            bool positioned = false;
            foreach (ArtiDiagnostic diagnostic in result.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error && diagnostic.Span.Line >= 1 && diagnostic.Span.Column >= 1
                    && diagnostic.MessageEn.IndexOf("lang", StringComparison.Ordinal) >= 0) positioned = true;
            check(result.HasErrors && positioned, scenario + " rejects with positioned lang error");
        }

        sealed class CatalogLocalization : IOxygenLocalization
        {
            public string Greeting { get; set; }
            public string Translate(string key, IDictionary<string, object> arguments)
            {
                if (key != "Greeting") return key;
                string result = Greeting;
                foreach (KeyValuePair<string, object> argument in arguments)
                    result = result.Replace("{" + argument.Key + "}", Convert.ToString(argument.Value, System.Globalization.CultureInfo.InvariantCulture));
                return result;
            }
        }
    }
}
