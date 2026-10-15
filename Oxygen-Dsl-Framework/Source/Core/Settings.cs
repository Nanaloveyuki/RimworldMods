using System;
using System.Collections.Generic;

namespace Oxygendsl.Core
{
    public interface IOxygenSettingsValues
    {
        object Get(string key, string scope, object defaultValue);
        void Set(string key, string scope, object value);
        bool GameAvailable { get; }
    }

    public sealed class OxygenSettingsPlan
    {
        public OxygenSettingsPlan(IReadOnlyList<OxygenSettingsPage> pages) { Pages = pages; }
        public IReadOnlyList<OxygenSettingsPage> Pages { get; }
    }

    public sealed class OxygenSettingsPage
    {
        public string Id { get; internal set; }
        public string Scope { get; internal set; }
        public Func<string> Title { get; internal set; }
        public IReadOnlyList<OxygenSettingsItem> Items { get; internal set; }
    }

    public sealed class OxygenSettingsItem
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public string Key { get; internal set; }
        public object Default { get; internal set; }
        public double Min { get; internal set; }
        public double Max { get; internal set; }
        public Func<string> Label { get; internal set; }
        public Func<string> Description { get; internal set; }
        public Func<bool> Visible { get; internal set; }
        public Func<bool> Enabled { get; internal set; }
        public Func<IReadOnlyList<OxygenSettingsOption>> Options { get; internal set; }
        public Action Action { get; internal set; }
        public Action<object> Changed { get; internal set; }
    }

    public sealed class OxygenSettingsOption
    {
        public OxygenSettingsOption(string value, string label) { Value = value; Label = label; }
        public string Value { get; }
        public string Label { get; }
    }

    public sealed class OxygenSettingsCompilation
    {
        public OxygenSettingsCompilation(OxygenSettingsPlan plan, IReadOnlyList<ScriptDiagnostic> diagnostics)
        { Plan = plan; Diagnostics = diagnostics; }
        public OxygenSettingsPlan Plan { get; }
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }
        public bool Success { get { return Plan != null; } }
    }
}
