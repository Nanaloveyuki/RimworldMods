using System;

namespace Oxygendsl.Mod.Content
{
    public sealed class NativeContentException : Exception
    {
        public NativeContentException(
            string message,
            int line,
            int column,
            string provider,
            string target,
            string field)
            : base((provider ?? string.Empty) + " " + line + ":" + column + " " + (target ?? string.Empty) + "." + (field ?? string.Empty) + ": " + message)
        {
            Line = line;
            Column = column;
            Provider = provider ?? string.Empty;
            Target = target ?? string.Empty;
            Field = field ?? string.Empty;
        }

        public int Line { get; }

        public int Column { get; }

        public string Provider { get; }

        public string Target { get; }

        public string Field { get; }
    }
}
