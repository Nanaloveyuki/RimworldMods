using System;
using System.Collections.Generic;
using System.Text;
using Oxygendsl.Core;
using RimWorld;
using Verse;

namespace Oxygendsl.Mod.Runtime
{
    public static class NativeRuleRuntime
    {
        const string WorkerName = "Oxygendsl.Mod.Runtime.ThoughtWorker_OxygenRule";
        static readonly List<ProviderSnapshot> providers = new List<ProviderSnapshot>();
        static Dictionary<string, ThoughtBinding> thoughts = new Dictionary<string, ThoughtBinding>(StringComparer.Ordinal);
        static Dictionary<string, GateBinding> gates = new Dictionary<string, GateBinding>(StringComparer.Ordinal);
        static readonly HashSet<string> logged = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<RuleError> errors = new List<RuleError>();
        static int revision;
        public static int Revision { get { return revision; } }
        public static string Inspection { get { return DescribeInstalled(); } }

        public static void Validate(ContentPlan plan)
        {
            CheckPlan(plan);
        }

        public static bool IsGateName(string name)
        {
            return name != null && Enum.IsDefined(typeof(RhahGate), name);
        }

        public static void ValidateForPawn(ContentPlan plan, Pawn pawn)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            Candidate candidate = CheckPlan(plan);
            PawnContext context = PawnFacts.Create(pawn);
            GameContext game = GameOf(pawn);
            foreach (KeyValuePair<string, CandidateThought> thought in candidate.Thoughts)
            {
                int stage = thought.Value.Rule.Evaluator.Evaluate(context, game);
                if (stage < -1 || stage >= thought.Value.Stages)
                    throw new InvalidOperationException(Locate(thought.Value.Rule, "Thought stage " + stage + " is outside -1.." + (thought.Value.Stages - 1) + "."));
            }
            foreach (KeyValuePair<string, GateRuleDefinition> gate in candidate.Gates)
                gate.Value.Evaluator.Evaluate(context, game);
        }

        public static void SetProvider(string packageId, ContentPlan plan, bool enabled)
        {
            if (string.IsNullOrEmpty(packageId)) throw new ArgumentException("Provider package id is required.", nameof(packageId));
            string id = packageId.ToLowerInvariant();
            Candidate candidate = CheckPlan(plan);
            int index = IndexOf(id);
            ProviderSnapshot next = new ProviderSnapshot(id, candidate, enabled, revision + 1);
            Install(index, next);
        }

        public static void RemoveProvider(string packageId)
        {
            int index = IndexOf(packageId);
            if (index < 0) return;
            ProviderSnapshot removed = providers[index];
            providers.RemoveAt(index);
            try { Publish(); }
            catch
            {
                providers.Insert(index, removed);
                throw;
            }
        }

        public static void SetEnabled(string packageId, bool enabled)
        {
            int index = IndexOf(packageId);
            if (index < 0) throw new InvalidOperationException("No native rule snapshot is installed for '" + packageId + "'.");
            ProviderSnapshot current = providers[index];
            if (current.Enabled == enabled) return;
            providers[index] = current.WithEnabled(enabled);
            try { Publish(); }
            catch
            {
                providers[index] = current;
                throw;
            }
        }

        public static ThoughtState EvaluateThought(ThoughtDef thought, Pawn pawn)
        {
            if (thought == null || pawn == null || string.IsNullOrEmpty(thought.defName)) return ThoughtState.Inactive;
            ThoughtBinding binding;
            if (!thoughts.TryGetValue(thought.defName, out binding) || !binding.Provider.Enabled) return ThoughtState.Inactive;
            try
            {
                if (!ThoughtUtility.CanGetThought(pawn, thought, true)) return ThoughtState.Inactive;
                return ToState(binding.Rule.Evaluator.Evaluate(PawnFacts.Create(pawn), GameOf(pawn)), thought);
            }
            catch (Exception exception)
            {
                Remember(binding.Provider, binding.Rule.Id, binding.Rule.Line, binding.Rule.Column, exception, true);
                return ThoughtState.Inactive;
            }
        }

        public static bool? EvaluateGate(PawnContext pawn, string gate, GameContext game)
        {
            if (pawn == null || string.IsNullOrEmpty(gate)) return null;
            GateBinding binding;
            if (!gates.TryGetValue(gate, out binding) || !binding.Provider.Enabled) return null;
            try
            {
                return binding.Rule.Evaluator.Evaluate(pawn, game);
            }
            catch (Exception exception)
            {
                Remember(binding.Provider, binding.Rule.Id, binding.Rule.Line, binding.Rule.Column, exception, true);
                return null;
            }
        }

        public static string PreviewThoughts(string packageId, Pawn pawn)
        {
            int index = IndexOf(packageId);
            if (index < 0 || pawn == null) return string.Empty;
            return Describe(providers[index], pawn);
        }

        public static void InvalidateSituationalThoughts()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.Maps == null) return;
            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                if (map == null || map.mapPawns == null || map.mapPawns.AllPawnsSpawned == null) continue;
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn == null || pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null) continue;
                    SituationalThoughtHandler situational = pawn.needs.mood.thoughts.situational;
                    if (situational != null) situational.Notify_SituationalThoughtsDirty();
                }
            }
        }

        static void Install(int index, ProviderSnapshot next)
        {
            ProviderSnapshot previous = index >= 0 ? providers[index] : null;
            if (index >= 0) providers[index] = next;
            else providers.Add(next);
            try { Publish(); }
            catch
            {
                if (previous == null) providers.RemoveAt(providers.Count - 1);
                else providers[index] = previous;
                throw;
            }
        }

        static void Publish()
        {
            Dictionary<string, ThoughtBinding> nextThoughts = new Dictionary<string, ThoughtBinding>(StringComparer.Ordinal);
            Dictionary<string, GateBinding> nextGates = new Dictionary<string, GateBinding>(StringComparer.Ordinal);
            for (int i = 0; i < providers.Count; i++)
            {
                ProviderSnapshot provider = providers[i];
                if (!provider.Enabled) continue;
                foreach (KeyValuePair<string, CandidateThought> thought in provider.Candidate.Thoughts)
                {
                    if (nextThoughts.ContainsKey(thought.Key))
                        throw new InvalidOperationException("ThoughtDef '" + thought.Key + "' is already bound by an earlier provider.");
                    nextThoughts.Add(thought.Key, new ThoughtBinding(provider, thought.Value.Rule));
                }
                foreach (KeyValuePair<string, GateRuleDefinition> gate in provider.Candidate.Gates)
                {
                    if (nextGates.ContainsKey(gate.Key))
                        throw new InvalidOperationException("Gate '" + gate.Key + "' is already bound by an earlier provider.");
                    nextGates.Add(gate.Key, new GateBinding(provider, gate.Value));
                }
            }
            thoughts = nextThoughts;
            gates = nextGates;
            revision++;
            logged.Clear();
            InvalidateSituationalThoughts();
        }

        static Candidate CheckPlan(ContentPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            Candidate candidate = new Candidate();
            for (int i = 0; i < plan.Thoughts.Count; i++)
            {
                ThoughtRuleDefinition rule = plan.Thoughts[i];
                if (rule == null || rule.Thought == null || string.IsNullOrEmpty(rule.Thought.Id))
                    throw new InvalidOperationException(Locate(rule, "thought_rule requires a ThoughtDef reference."));
                if (!string.Equals(rule.Thought.Type, "ThoughtDef", StringComparison.Ordinal))
                    throw new InvalidOperationException(Locate(rule, "thought_rule reference must be ThoughtDef, not '" + rule.Thought.Type + "'."));
                if (rule.Evaluator == null)
                    throw new InvalidOperationException(Locate(rule, "thought_rule '" + rule.Id + "' has no evaluator."));
                ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(rule.Thought.Id);
                if (def == null)
                    throw new InvalidOperationException(Locate(rule, "ThoughtDef '" + rule.Thought.Id + "' is not loaded."));
                if (!string.IsNullOrEmpty(rule.Thought.PackageId) && !Owns(def, rule.Thought.PackageId))
                    throw new InvalidOperationException(Locate(rule, "ThoughtDef '" + rule.Thought.Id + "' is not owned by '" + rule.Thought.PackageId + "'."));
                if (def.workerClass == null || def.workerClass.FullName != WorkerName)
                    throw new InvalidOperationException(Locate(rule, "ThoughtDef '" + rule.Thought.Id + "' workerClass must be " + WorkerName + "."));
                int stages = def.stages == null ? 0 : def.stages.Count;
                if (stages < 1)
                    throw new InvalidOperationException(Locate(rule, "ThoughtDef '" + rule.Thought.Id + "' has no stages."));
                if (candidate.Thoughts.ContainsKey(rule.Thought.Id))
                    throw new InvalidOperationException(Locate(rule, "ThoughtDef '" + rule.Thought.Id + "' is bound more than once."));
                candidate.Thoughts.Add(rule.Thought.Id, new CandidateThought(rule, stages));
            }
            for (int i = 0; i < plan.Gates.Count; i++)
            {
                GateRuleDefinition rule = plan.Gates[i];
                if (rule == null || string.IsNullOrEmpty(rule.Gate))
                    throw new InvalidOperationException(Locate(rule, "gate_rule requires a gate name."));
                if (!IsGateName(rule.Gate))
                    throw new InvalidOperationException(Locate(rule, "Unknown RHAH gate '" + rule.Gate + "'."));
                if (rule.Evaluator == null)
                    throw new InvalidOperationException(Locate(rule, "gate_rule '" + rule.Id + "' has no evaluator."));
                if (candidate.Gates.ContainsKey(rule.Gate))
                    throw new InvalidOperationException(Locate(rule, "Gate '" + rule.Gate + "' is bound more than once."));
                candidate.Gates.Add(rule.Gate, rule);
            }
            return candidate;
        }

        static ThoughtState ToState(int stage, ThoughtDef thought)
        {
            if (stage < 0) return ThoughtState.Inactive;
            int count = thought.stages == null ? 0 : thought.stages.Count;
            if (stage >= count) return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage(stage);
        }

        static GameContext GameOf(Pawn pawn)
        {
            Map map = pawn == null ? null : pawn.MapHeld;
            if (map == null && Current.ProgramState == ProgramState.Playing) map = Find.CurrentMap;
            int tick = Current.ProgramState == ProgramState.Playing && Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            double hour = map == null ? 0d : GenLocalDate.HourFloat(map);
            return new GameContext { Tick = tick, Hour = hour };
        }


        static void Remember(ProviderSnapshot provider, string ruleId, int line, int column, Exception exception, bool log)
        {
            string key = provider.PackageId + "/" + provider.Revision + "/" + ruleId;
            if (!logged.Add(key)) return;
            string message = exception == null ? "Rule evaluation failed." : exception.Message;
            errors.Add(new RuleError(provider.PackageId, ruleId, line, column, message, provider.Revision));
            if (errors.Count > 32) errors.RemoveAt(0);
            if (log) Log.Error("[Oxygendsl] " + provider.PackageId + "/" + ruleId + " " + line + ":" + column + " " + message);
        }

        static string DescribeInstalled()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Native rules revision ").Append(revision).AppendLine();
            for (int i = 0; i < providers.Count; i++)
            {
                ProviderSnapshot provider = providers[i];
                text.Append(provider.PackageId).Append(provider.Enabled ? " [enabled]" : " [disabled]")
                    .Append(" thoughts ").Append(provider.Candidate.Thoughts.Count)
                    .Append(" gates ").Append(provider.Candidate.Gates.Count).AppendLine();
            }
            for (int i = 0; i < errors.Count; i++)
            {
                RuleError error = errors[i];
                text.Append(error.PackageId).Append('/').Append(error.RuleId).Append(' ')
                    .Append(error.Line).Append(':').Append(error.Column).Append(' ').AppendLine(error.Message);
            }
            return text.ToString();
        }

        static string Describe(ProviderSnapshot provider, Pawn pawn)
        {
            StringBuilder text = new StringBuilder();
            text.Append(provider.PackageId).Append(provider.Enabled ? string.Empty : " disabled").AppendLine();
            PawnContext context = PawnFacts.Create(pawn);
            GameContext game = GameOf(pawn);
            foreach (KeyValuePair<string, CandidateThought> thought in provider.Candidate.Thoughts)
            {
                text.Append("  ").Append(thought.Key).Append(": ");
                if (!provider.Enabled)
                {
                    text.AppendLine("inactive");
                    continue;
                }
                ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought.Key);
                if (def == null)
                {
                    text.AppendLine("unavailable: ThoughtDef is not loaded");
                    continue;
                }
                try
                {
                    if (!ThoughtUtility.CanGetThought(pawn, def, true))
                    {
                        text.AppendLine("inactive: native thought gate");
                        continue;
                    }
                    int stage = thought.Value.Rule.Evaluator.Evaluate(context, game);
                    text.AppendLine(ToState(stage, def).Active ? "stage " + stage : "inactive");
                }
                catch (Exception exception)
                {
                    text.Append("error: ").AppendLine(exception.Message);
                }
            }
            foreach (KeyValuePair<string, GateRuleDefinition> gate in provider.Candidate.Gates)
            {
                text.Append("  gate ").Append(gate.Key).Append(": ");
                if (!provider.Enabled)
                {
                    text.AppendLine("abstain");
                    continue;
                }
                try
                {
                    bool? allowed = gate.Value.Evaluator.Evaluate(context, game);
                    text.AppendLine(allowed.HasValue ? (allowed.Value ? "allow" : "deny") : "abstain");
                }
                catch (Exception exception)
                {
                    text.Append("error: ").AppendLine(exception.Message);
                }
            }
            return text.ToString();
        }

        static int IndexOf(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return -1;
            for (int i = 0; i < providers.Count; i++)
                if (string.Equals(providers[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        static bool Owns(ThoughtDef def, string packageId)
        {
            if (def.modContentPack == null || def.modContentPack.PackageId == null) return false;
            return string.Equals(def.modContentPack.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.modContentPack.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase);
        }

        static string Locate(ThoughtRuleDefinition rule, string message)
        {
            return (rule == null ? 0 : rule.Line) + ":" + (rule == null ? 0 : rule.Column) + " " + message;
        }

        static string Locate(GateRuleDefinition rule, string message)
        {
            return (rule == null ? 0 : rule.Line) + ":" + (rule == null ? 0 : rule.Column) + " " + message;
        }

        sealed class Candidate
        {
            public readonly Dictionary<string, CandidateThought> Thoughts = new Dictionary<string, CandidateThought>(StringComparer.Ordinal);
            public readonly Dictionary<string, GateRuleDefinition> Gates = new Dictionary<string, GateRuleDefinition>(StringComparer.Ordinal);
        }

        sealed class CandidateThought
        {
            public readonly ThoughtRuleDefinition Rule;
            public readonly int Stages;
            public CandidateThought(ThoughtRuleDefinition rule, int stages)
            {
                Rule = rule;
                Stages = stages;
            }
        }

        sealed class ProviderSnapshot
        {
            public readonly string PackageId;
            public readonly Candidate Candidate;
            public readonly bool Enabled;
            public readonly int Revision;
            public ProviderSnapshot(string packageId, Candidate candidate, bool enabled, int snapshotRevision)
            {
                PackageId = packageId;
                Candidate = candidate;
                Enabled = enabled;
                Revision = snapshotRevision;
            }
            public ProviderSnapshot WithEnabled(bool enabled)
            {
                return new ProviderSnapshot(PackageId, Candidate, enabled, Revision);
            }
        }

        sealed class ThoughtBinding
        {
            public readonly ProviderSnapshot Provider;
            public readonly ThoughtRuleDefinition Rule;
            public ThoughtBinding(ProviderSnapshot provider, ThoughtRuleDefinition rule)
            {
                Provider = provider;
                Rule = rule;
            }
        }

        sealed class GateBinding
        {
            public readonly ProviderSnapshot Provider;
            public readonly GateRuleDefinition Rule;
            public GateBinding(ProviderSnapshot provider, GateRuleDefinition rule)
            {
                Provider = provider;
                Rule = rule;
            }
        }

        sealed class RuleError
        {
            public readonly string PackageId;
            public readonly string RuleId;
            public readonly int Line;
            public readonly int Column;
            public readonly string Message;
            public readonly int Revision;
            public RuleError(string packageId, string ruleId, int line, int column, string message, int errorRevision)
            {
                PackageId = packageId;
                RuleId = ruleId;
                Line = line;
                Column = column;
                Message = message;
                Revision = errorRevision;
            }
        }

        enum RhahGate
        {
            Beg, Steal, Fight, LeaveAfterFed, EatOutsideRelief, FeedFromRelief, Gnaw, TailBite,
            Leash, Carry, JoinColony, Hire, Transfer, Imprison, DropOffChild, ExitMap
        }
    }
}
