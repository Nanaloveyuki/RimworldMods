using System;
using System.Collections.Generic;

namespace Oxygendsl.Core
{
    public sealed class DefReference
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string PackageId { get; set; }
    }

    public sealed class NativeTypeReference
    {
        public string Name { get; set; }
    }

    public sealed class ContentDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public IDictionary<string, object> Fields { get; set; }
        public DefReference BaseDef { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public sealed class ContentPatch
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public IDictionary<string, object> Set { get; set; }
        public IDictionary<string, object> Append { get; set; }
        public IDictionary<string, object> Remove { get; set; }
        public IReadOnlyList<string> Requires { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public interface IThoughtEvaluator
    {
        int Evaluate(PawnContext pawn, GameContext game);
    }

    public interface IGateEvaluator
    {
        bool? Evaluate(PawnContext pawn, GameContext game);
    }

    public sealed class ThoughtRuleDefinition
    {
        public string Id { get; set; }
        public DefReference Thought { get; set; }
        public IThoughtEvaluator Evaluator { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public sealed class GateRuleDefinition
    {
        public string Id { get; set; }
        public string Gate { get; set; }
        public IGateEvaluator Evaluator { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public sealed class ContentPlan
    {
        public ContentPlan(
            IReadOnlyList<ContentDefinition> definitions,
            IReadOnlyList<ContentPatch> patches,
            IReadOnlyList<ThoughtRuleDefinition> thoughts,
            IReadOnlyList<GateRuleDefinition> gates)
        {
            Definitions = definitions ?? Array.Empty<ContentDefinition>();
            Patches = patches ?? Array.Empty<ContentPatch>();
            Thoughts = thoughts ?? Array.Empty<ThoughtRuleDefinition>();
            Gates = gates ?? Array.Empty<GateRuleDefinition>();
        }

        public IReadOnlyList<ContentDefinition> Definitions { get; }
        public IReadOnlyList<ContentPatch> Patches { get; }
        public IReadOnlyList<ThoughtRuleDefinition> Thoughts { get; }
        public IReadOnlyList<GateRuleDefinition> Gates { get; }
    }

    public sealed class ContentRegistrationArgument
    {
        public ContentRegistrationArgument(string name, object value, int line, int column)
        {
            Name = name;
            Value = value;
            Line = line;
            Column = column;
        }

        public string Name { get; }
        public object Value { get; }
        public int Line { get; }
        public int Column { get; }
    }

    public sealed class ContentRegistration
    {
        public ContentRegistration(string name, int line, int column, IReadOnlyList<ContentRegistrationArgument> arguments)
        {
            Name = name ?? string.Empty;
            Line = line;
            Column = column;
            Arguments = arguments ?? Array.Empty<ContentRegistrationArgument>();
        }

        public string Name { get; }
        public int Line { get; }
        public int Column { get; }
        public IReadOnlyList<ContentRegistrationArgument> Arguments { get; }
    }

    public sealed class ContentRegistrationResult
    {
        public IReadOnlyList<ContentDefinition> Definitions { get; set; }
        public IReadOnlyList<ContentPatch> Patches { get; set; }
        public IReadOnlyList<ThoughtRuleDefinition> Thoughts { get; set; }
        public IReadOnlyList<GateRuleDefinition> Gates { get; set; }
    }

    public delegate ContentRegistrationResult ContentRegistrationReader(ContentRegistration registration);


    public sealed class ContentImport
    {
        public ContentImport(string packageId, string modulePath, string sourceHash)
        {
            PackageId = packageId ?? string.Empty;
            ModulePath = modulePath ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
        }

        public string PackageId { get; }
        public string ModulePath { get; }
        public string SourceHash { get; }
    }

    public sealed class ContentCompilation
    {
        public ContentCompilation(ContentPlan plan, IReadOnlyList<ScriptDiagnostic> diagnostics, IReadOnlyList<ContentImport> imports)
        {
            Plan = plan;
            Diagnostics = diagnostics ?? Array.Empty<ScriptDiagnostic>();
            Imports = imports ?? Array.Empty<ContentImport>();
        }

        public ContentPlan Plan { get; }
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }
        public IReadOnlyList<ContentImport> Imports { get; }
        public bool Success { get { return Plan != null && Diagnostics.Count == 0; } }
    }
}
