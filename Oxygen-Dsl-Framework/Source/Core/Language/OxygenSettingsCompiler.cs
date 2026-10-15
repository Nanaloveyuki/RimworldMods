using System;
using System.Collections.Generic;
using System.Globalization;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public static class OxygenSettingsCompiler
    {
        public static OxygenSettingsCompilation Compile(string source, bool irisMenusAvailable,
            IOxygenModuleCatalog modules = null, IOxygenStandardModuleProvider standardModules = null,
            IOxygenModuleProvider hostModules = null, IOxygenLocalization localization = null)
        {
            var executor = new ArtiExecutor();
            var declarations = new List<object>();
            var provider = new SettingsModules(irisMenusAvailable, declarations, standardModules);
            ArtiExecutionResult result = executor.Execute(source,
                new ArtiExecutionContext(modules: modules, standardModules: provider, hostModules: hostModules, localization: localization));
            var diagnostics = new List<ScriptDiagnostic>();
            foreach (ArtiDiagnostic diagnostic in result.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                    diagnostics.Add(new ScriptDiagnostic(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column));
            if (result.HasErrors) return new OxygenSettingsCompilation(null, diagnostics);
            return RegisterPrepared(declarations, executor, diagnostics);
        }

        internal static OxygenSettingsCompilation RegisterPrepared(List<object> declarations, ArtiExecutor executor,
            List<ScriptDiagnostic> diagnostics)
        {
            try
            {
                var pages = new List<OxygenSettingsPage>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (object declaration in declarations)
                    foreach (object value in Sequence(declaration, "pages"))
                    {
                        RuntimeObject page = Object(value, "page", "id", "title", "scope", "items");
                        string id = RequiredText(page, "id");
                        if (!ids.Add(id)) throw Error("Duplicate page id '" + id + "'.");
                        string scope = Text(Member(page, "scope", "global"), "scope");
                        if (scope != "global" && scope != "game") throw Error("scope must be global or game.");
                        var items = new List<OxygenSettingsItem>();
                        var itemIds = new HashSet<string>(StringComparer.Ordinal);
                        foreach (object item in Sequence(Required(page, "items"), "items"))
                        {
                            OxygenSettingsItem parsed = ReadItem(Object(item, "item", "id", "kind", "key", "default", "min", "max",
                                "label", "description", "visible", "enabled", "options", "action", "changed"), executor);
                            if (!itemIds.Add(parsed.Id)) throw Error("Duplicate item id '" + parsed.Id + "' in page '" + id + "'.");
                            items.Add(parsed);
                        }
                        pages.Add(new OxygenSettingsPage { Id = id, Scope = scope,
                            Title = TextCallback(Required(page, "title"), executor, "title"), Items = items.AsReadOnly() });
                    }
                return new OxygenSettingsCompilation(new OxygenSettingsPlan(pages.AsReadOnly()), diagnostics);
            }
            catch (Exception exception)
            {
                var positioned = exception as ScriptEvaluationException;
                diagnostics.Add(new ScriptDiagnostic(exception.Message, positioned == null ? 1 : positioned.Line,
                    positioned == null ? 1 : positioned.Column));
                return new OxygenSettingsCompilation(null, diagnostics);
            }
        }

        static OxygenSettingsItem ReadItem(RuntimeObject item, ArtiExecutor executor)
        {
            var result = new OxygenSettingsItem
            {
                Id = RequiredText(item, "id"), Kind = RequiredText(item, "kind"),
                Label = TextCallback(Required(item, "label"), executor, "label"),
                Description = TextCallback(Member(item, "description", ""), executor, "description"),
                Visible = BoolCallback(Member(item, "visible", true), executor, "visible"),
                Enabled = BoolCallback(Member(item, "enabled", true), executor, "enabled")
            };
            switch (result.Kind)
            {
                case "label": case "section": break;
                case "button":
                    object action = Required(item, "action");
                    RequireCallable(action, "action");
                    result.Action = () => executor.InvokePrepared(action);
                    break;
                case "checkbox": case "number": case "slider": case "text": case "select":
                    result.Key = RequiredText(item, "key");
                    result.Default = Required(item, "default");
                    if (result.Kind == "checkbox" && !(result.Default is bool)) throw Error("checkbox default must be bool.");
                    if ((result.Kind == "text" || result.Kind == "select") && !(result.Default is string)) throw Error(result.Kind + " default must be string.");
                    if (result.Kind == "number" || result.Kind == "slider")
                    {
                        double number = Number(result.Default, "default");
                        result.Min = Number(Member(item, "min", result.Kind == "slider" ? 0d : int.MinValue), "min");
                        result.Max = Number(Member(item, "max", result.Kind == "slider" ? 1d : int.MaxValue), "max");
                        if (result.Min > result.Max || number < result.Min || number > result.Max)
                            throw Error("Numeric default must be within min <= max.");
                        if (result.Min < -float.MaxValue || result.Max > float.MaxValue) throw Error("Numeric bounds exceed the UI range.");
                        if (result.Kind == "number" && IsInteger(result.Default) &&
                            (result.Min < int.MinValue || result.Max > int.MaxValue || result.Min != Math.Truncate(result.Min) || result.Max != Math.Truncate(result.Max)))
                            throw Error("Integer number fields require Int32 bounds.");
                    }
                    if (result.Kind == "select")
                    {
                        object options = Required(item, "options");
                        if (Callable(options)) result.Options = () => ReadOptions(executor.InvokePrepared(options));
                        else
                        {
                            IReadOnlyList<OxygenSettingsOption> fixedOptions = ReadOptions(options);
                            result.Options = () => fixedOptions;
                        }
                    }
                    object changed = Member(item, "changed", null);
                    if (changed != null)
                    {
                        RequireCallable(changed, "changed");
                        result.Changed = value => executor.InvokePrepared(changed, value);
                    }
                    break;
                default: throw Error("Unknown settings kind '" + result.Kind + "'.");
            }
            return result;
        }

        static IReadOnlyList<OxygenSettingsOption> ReadOptions(object value)
        {
            var result = new List<OxygenSettingsOption>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (object entry in Sequence(value, "options"))
            {
                RuntimeObject option = Object(entry, "option", "value", "label");
                string id = Text(Required(option, "value"), "option value");
                if (!ids.Add(id)) throw Error("Duplicate option value '" + id + "'.");
                result.Add(new OxygenSettingsOption(id, Text(Required(option, "label"), "option label")));
            }
            return result.AsReadOnly();
        }

        static Func<string> TextCallback(object value, ArtiExecutor executor, string name)
        {
            if (!Callable(value)) { string text = Text(value, name); return () => text; }
            return () => Text(executor.InvokePrepared(value), name);
        }

        static Func<bool> BoolCallback(object value, ArtiExecutor executor, string name)
        {
            if (!Callable(value)) { bool flag = Bool(value, name); return () => flag; }
            return () => Bool(executor.InvokePrepared(value), name);
        }

        static bool Callable(object value) { return value is IArtiCallable || value is IOxygenHostCallable; }
        static void RequireCallable(object value, string name) { if (!Callable(value)) throw Error(name + " must be a function."); }
        static bool Bool(object value, string name) { if (!(value is bool)) throw Error(name + " must return bool."); return (bool)value; }
        static string Text(object value, string name) { if (!(value is string)) throw Error(name + " must return string."); return (string)value; }
        static string RequiredText(RuntimeObject value, string name)
        {
            string text = Text(Required(value, name), name);
            if (string.IsNullOrWhiteSpace(text)) throw Error(name + " must not be blank.");
            return text;
        }
        static object Required(RuntimeObject value, string name)
        {
            object member;
            if (!value.TryGet(name, out member) || member == null) throw Error("Missing settings member '" + name + "'.");
            return member;
        }
        static object Member(RuntimeObject value, string name, object fallback) { object member; return value.TryGet(name, out member) ? member : fallback; }
        static RuntimeObject Object(object value, string name, params string[] keys)
        {
            RuntimeObject result = value as RuntimeObject;
            if (result == null) throw Error(name + " must be an object.");
            foreach (string key in result.Keys)
                if (Array.IndexOf(keys, key) < 0) throw Error("Unknown " + name + " member '" + key + "'.");
            return result;
        }
        static IList<object> Sequence(object value, string name)
        {
            IList<object> list = value as IList<object>;
            if (list == null) throw Error(name + " must be a list or array.");
            return list;
        }
        static bool IsInteger(object value) { return value is long || value is int; }
        static double Number(object value, string name)
        {
            if (!(value is double || value is float || value is long || value is int)) throw Error(name + " must be a finite number.");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number)) throw Error(name + " must be a finite number.");
            return number;
        }
        static InvalidOperationException Error(string message) { return new InvalidOperationException(message); }

        internal sealed class SettingsModules : IOxygenStandardModuleProvider, IOxygenHostCallable
        {
            readonly bool available;
            readonly List<object> declarations;
            readonly IOxygenStandardModuleProvider extensions;
            public SettingsModules(bool available, List<object> declarations, IOxygenStandardModuleProvider extensions)
            { this.available = available; this.declarations = declarations; this.extensions = extensions; }
            public bool TryGetModule(string path, out IOxygenHostValue module)
            {
                if (path != "irismenus")
                {
                    module = null;
                    return extensions != null && extensions.TryGetModule(path, out module);
                }
                module = available ? new DictionaryOxygenHostValue(new Dictionary<string, object> { { "registry", this } }) : null;
                return available;
            }
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                if (positional.Count != 0 || named.Count != 1 || !named.ContainsKey("pages"))
                    throw Error("irismenus.registry expects only pages: [...].");
                declarations.Add(named["pages"]);
                return null;
            }
        }
    }
}
