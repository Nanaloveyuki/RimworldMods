using System;
using System.Collections.Generic;

namespace Oxygendsl.Mod.Content
{
    public enum NativeContentIssueKind
    {
        Error,
        Unknown
    }

    public enum NativeContentChangeKind
    {
        Define,
        Set,
        Append,
        Remove
    }

    public enum NativeDefResolution
    {
        Found,
        Missing,
        Unknown
    }

    public sealed class NativeContentIssue
    {
        public NativeContentIssue(
            string message,
            int line,
            int column,
            string provider,
            string target,
            string field,
            NativeContentIssueKind kind)
        {
            Message = message ?? string.Empty;
            Line = line;
            Column = column;
            Provider = provider ?? string.Empty;
            Target = target ?? string.Empty;
            Field = field ?? string.Empty;
            Kind = kind;
        }

        public string Message { get; }

        public int Line { get; }

        public int Column { get; }

        public string Provider { get; }

        public string Target { get; }

        public string Field { get; }

        public NativeContentIssueKind Kind { get; }
    }

    public sealed class NativeContentChange
    {
        public NativeContentChange(
            int order,
            NativeContentChangeKind kind,
            string type,
            string id,
            string field,
            string before,
            string after,
            string provider)
        {
            Order = order;
            Kind = kind;
            Type = type ?? string.Empty;
            Id = id ?? string.Empty;
            Field = field ?? string.Empty;
            Before = before ?? string.Empty;
            After = after ?? string.Empty;
            Provider = provider ?? string.Empty;
        }

        public int Order { get; }

        public NativeContentChangeKind Kind { get; }

        public string Type { get; }

        public string Id { get; }

        public string Field { get; }

        public string Before { get; }

        public string After { get; }

        public string Provider { get; }
    }

    public sealed class NativeRegisteredContent
    {
        public NativeRegisteredContent(
            string type,
            string id,
            IReadOnlyList<int> degrees,
            IReadOnlyList<string> stageLabels,
            string workerClass,
            string thoughtClass,
            bool isAbstract,
            string parentName)
        {
            Type = type ?? string.Empty;
            Id = id ?? string.Empty;
            Degrees = degrees ?? Array.Empty<int>();
            StageLabels = stageLabels ?? Array.Empty<string>();
            WorkerClass = workerClass ?? string.Empty;
            ThoughtClass = thoughtClass ?? string.Empty;
            Abstract = isAbstract;
            ParentName = parentName ?? string.Empty;
        }

        public string Type { get; }

        public string Id { get; }

        public IReadOnlyList<int> Degrees { get; }

        public IReadOnlyList<string> StageLabels { get; }

        public string WorkerClass { get; }

        public string ThoughtClass { get; }

        public bool Abstract { get; }

        public string ParentName { get; }
    }

    public sealed class NativeContentCheckResult
    {
        public NativeContentCheckResult(
            bool success,
            string candidateXml,
            IReadOnlyList<NativeContentIssue> issues,
            IReadOnlyList<NativeContentChange> changes,
            IReadOnlyList<NativeRegisteredContent> layout,
            bool restartRequired)
        {
            Success = success;
            CandidateXml = candidateXml ?? string.Empty;
            Issues = issues ?? Array.Empty<NativeContentIssue>();
            Changes = changes ?? Array.Empty<NativeContentChange>();
            Layout = layout ?? Array.Empty<NativeRegisteredContent>();
            RestartRequired = restartRequired;
        }

        public bool Success { get; }

        public string CandidateXml { get; }

        public IReadOnlyList<NativeContentIssue> Issues { get; }

        public IReadOnlyList<NativeContentChange> Changes { get; }

        public IReadOnlyList<NativeRegisteredContent> Layout { get; }

        public bool RestartRequired { get; }
    }
}
