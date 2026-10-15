using System;
using System.Collections.Generic;

namespace Oxygendsl.Core
{
    public enum RuleKind { Mood, Memory }
    public enum RuleTarget { Colonists, Map, Selected }
    public enum MemoryMode { Refresh, Stack }

    public sealed class PawnContext
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double? Mood { get; set; }
        public double? Food { get; set; }
        public double? Rest { get; set; }
        public bool Drafted { get; set; }
        public bool Downed { get; set; }
        public bool Colonist { get; set; }
        public IOxygenHostValue Facts { get; set; }
    }

    public sealed class GameContext
    {
        public int Tick { get; set; }
        public double Hour { get; set; }
    }

    public interface IRuleCondition
    {
        bool Evaluate(PawnContext pawn, GameContext game);
    }

    public sealed class RuleDefinition
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public float Offset { get; set; }
        public RuleKind Kind { get; set; }
        public RuleTarget Target { get; set; }
        public int DurationTicks { get; set; }
        public int CooldownTicks { get; set; }
        public MemoryMode Mode { get; set; }
        public int StackLimit { get; set; }
        public IRuleCondition Condition { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public sealed class RuleProgram
    {
        public RuleProgram(IReadOnlyList<RuleDefinition> rules) { Rules = rules; }
        public IReadOnlyList<RuleDefinition> Rules { get; }
    }

    public sealed class ScriptDiagnostic
    {
        public ScriptDiagnostic(string message, int line = 1, int column = 1)
        {
            Message = message; Line = line; Column = column;
        }
        public string Message { get; }
        public int Line { get; }
        public int Column { get; }
        public override string ToString() { return Line + ":" + Column + " " + Message; }
    }

    public sealed class ScriptCompilation
    {
        public ScriptCompilation(RuleProgram program, IReadOnlyList<ScriptDiagnostic> diagnostics)
        {
            Program = program; Diagnostics = diagnostics;
        }
        public RuleProgram Program { get; }
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }
        public bool Success { get { return Program != null && Diagnostics.Count == 0; } }
    }

    public sealed class ScriptEvaluationException : Exception
    {
        public ScriptEvaluationException(string message, int line, int column) : base(message)
        { Line = line; Column = column; }
        public int Line { get; }
        public int Column { get; }
    }
}
