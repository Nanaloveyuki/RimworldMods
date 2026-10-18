using System;

namespace Oxygendsl.Mod.Editor
{
    public sealed class OxygenEditorDocument
    {
        public string Id { get; set; }
        public string PackageId { get; set; } = string.Empty;
        public string Title { get; set; }
        public string Path { get; set; }
        public string Kind { get; set; } = "text";
        public bool Writable { get; set; } = true;
        public bool IsModule { get; set; }
        public Func<string> Read { get; set; }
        public Action<string> Write { get; set; }
        public Func<OxygenEditorActionResult> Apply { get; set; }
        public Func<string, string, object> Preview { get; set; }
    }

    public sealed class OxygenEditorActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
