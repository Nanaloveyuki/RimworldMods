using System;
using System.Collections.Generic;
using System.Diagnostics;
using Oxygendsl.Core;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using System.Xml;
using Oxygendsl.Language;
using RimWorld;
using Verse;
using Oxygendsl.Mod.Runtime;

namespace Oxygendsl.Mod
{
    public sealed class GameComponent_Oxygendsl : GameComponent
    {
        public const int DefaultInterval = 150;
        const string CarrierDefName = "Oxygendsl_Memory";

        string draftSource;
        string activeSource = string.Empty;
        bool enabled;
        int revision;
        int evaluationInterval = DefaultInterval;
        string lastError = string.Empty;
        string lastErrorRuleId = string.Empty;
        int lastEvaluationTick = -1;
        int evaluationCount;
        double lastEvaluationMs;
        long lastAllocatedBytes = -1L;
        int activeEffectCount;

        RuleProgram program;
        readonly List<RuleStatus> statuses = new List<RuleStatus>();
        List<Pawn> selected = new List<Pawn>();
        bool selectionCaptured;

        List<CooldownRecord> cooldowns = new List<CooldownRecord>();
        readonly List<Thought_Memory_Oxygendsl> memoryScratch = new List<Thought_Memory_Oxygendsl>();
        readonly List<Thought_Memory_Oxygendsl> owned = new List<Thought_Memory_Oxygendsl>();
        List<Pawn> ownedPawns = new List<Pawn>();
        List<string> disabledContentProviders = new List<string>();
        List<ContentLayoutRecord> contentLayouts = new List<ContentLayoutRecord>();

        readonly Game game;
        int eventChainSchemaVersion = EventChainRuntime.SchemaVersion;
        List<EventChainProviderSave> eventChainProviders = new List<EventChainProviderSave>();
        EventChainRuntime eventChainRuntime;
        bool eventChainFinalizeComplete;
        bool eventChainInitializationStarted;
        bool eventChainReady;
        bool eventChainInitializationPending;
        string eventChainError = string.Empty;
        XmlElement preservedEventChainSchema;
        XmlElement preservedEventChainProviders;
        readonly List<XmlNode> unknownEventChainFields = new List<XmlNode>();
        static readonly HashSet<string> savedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "eventChainSchemaVersion", "eventChainProviders", "draftSource", "activeSource", "enabled", "revision", "evaluationInterval",
            "lastError", "lastEvaluationTick", "evaluationCount", "disabledContentProviders", "contentLayouts", "selected", "selectionCaptured", "ownedPawns", "cooldowns"
        };

        readonly List<Pawn> candidates = new List<Pawn>();

        readonly List<PlannedEffect> plan = new List<PlannedEffect>();
        readonly List<PlannedEffect> scratch = new List<PlannedEffect>();
        readonly HashSet<int> seenPawns = new HashSet<int>();
        readonly HashSet<KeyValuePair<string, int>> heldMoods = new HashSet<KeyValuePair<string, int>>();
        readonly HashSet<string> activeIds = new HashSet<string>();
        readonly Stopwatch clock = new Stopwatch();

        public GameComponent_Oxygendsl(Game game) { this.game = game; }

        public static GameComponent_Oxygendsl Current
        {
            get
            {
                if (Verse.Current.ProgramState != ProgramState.Playing || Verse.Current.Game == null) return null;
                return Verse.Current.Game.GetComponent<GameComponent_Oxygendsl>();
            }
        }

        public string DraftSource
        {
            get { return draftSource; }
            set { draftSource = value; }
        }

        public string ActiveSource { get { return activeSource; } }
        public bool Enabled { get { return enabled; } }
        public int Revision { get { return revision; } }
        public int EvaluationInterval
        {
            get { return evaluationInterval; }
            set { evaluationInterval = value < 1 ? 1 : value; }
        }
        public string LastError { get { return lastError; } }
        public IReadOnlyList<RuleStatus> Statuses { get { return statuses; } }
        public int ActiveEffectCount { get { return activeEffectCount; } }
        public double LastEvaluationMs { get { return lastEvaluationMs; } }
        public long LastAllocatedBytes { get { return lastAllocatedBytes; } }
        public int LastEvaluationTick { get { return lastEvaluationTick; } }
        public int EvaluationCount { get { return evaluationCount; } }
        public EventChainRuntime EventChains
        {
            get { OxygenEventChainHost.RequireMainThread(); return eventChainReady ? eventChainRuntime : null; }
        }

        internal bool EventChainFinalizeComplete { get { return eventChainFinalizeComplete; } }

        internal bool BeginEventChainInitialization()
        {
            if (eventChainInitializationStarted) return false;
            eventChainInitializationStarted = true;
            return true;
        }

        internal EventChainRuntime CreateEventChainRuntime()
        {
            if (eventChainSchemaVersion != EventChainRuntime.SchemaVersion)
            {
                eventChainError = "paused: schema-mismatch (saved " + eventChainSchemaVersion + ", supported " + EventChainRuntime.SchemaVersion + "); original EventChain data retained, migration and actions blocked.";
                Log.Warning("[Oxygendsl] " + eventChainError);
                return null;
            }
            if (eventChainRuntime == null)
                eventChainRuntime = new EventChainRuntime(EventChainSaveXml.Unwrap(eventChainProviders, item => item.State));
            return eventChainRuntime;
        }

        internal void PreserveUnavailableEventChains(IReadOnlyList<IEventChainProvider> providers)
        {
            for (int i = 0; i < eventChainProviders.Count; i++)
            {
                EventChainProviderSave saved = eventChainProviders[i];
                if (saved == null) continue;
                IEventChainProvider owner = null;
                for (int j = 0; j < providers.Count; j++)
                    if (string.Equals(providers[j].ProviderId, saved.State.ProviderId, StringComparison.Ordinal)) { owner = providers[j]; break; }
                saved.PreserveIfUnavailable(owner);
            }
        }

        internal void CompleteEventChainInitialization() { eventChainError = string.Empty; eventChainReady = true; }
        internal void FailEventChainInitialization(string reason) { eventChainError = "paused: preparation-failed; " + reason; }

        public string DescribeEventChains()
        {
            OxygenEventChainHost.RequireMainThread();
            if (!string.IsNullOrEmpty(eventChainError)) return eventChainError + (eventChainRuntime == null ? string.Empty : "\n" + eventChainRuntime.Describe());
            return eventChainRuntime == null ? "paused: waiting for finalized game and provider preparation" : eventChainRuntime.Describe();
        }

        void ExposeEventChains()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                eventChainRuntime = null;
                eventChainReady = false;
                eventChainInitializationStarted = false;
                eventChainInitializationPending = false;
                eventChainFinalizeComplete = false;
                eventChainError = string.Empty;
                XmlNode root = Scribe.loader.curXmlParent;
                preservedEventChainSchema = root == null ? null : EventChainSaveXml.Clone(root["eventChainSchemaVersion"]) as XmlElement;
                preservedEventChainProviders = root == null ? null : EventChainSaveXml.Clone(root["eventChainProviders"]) as XmlElement;
                unknownEventChainFields.Clear();
                if (root != null)
                    foreach (XmlNode node in root.ChildNodes)
                        if (node is XmlElement && !savedFields.Contains(node.Name)) unknownEventChainFields.Add(EventChainSaveXml.Clone(node));
            }
            if (Scribe.mode == LoadSaveMode.Saving && eventChainSchemaVersion != EventChainRuntime.SchemaVersion)
            {
                EventChainSaveXml.WriteNode(preservedEventChainSchema);
                EventChainSaveXml.WriteNode(preservedEventChainProviders);
            }
            else
            {
                Scribe_Values.Look(ref eventChainSchemaVersion, "eventChainSchemaVersion", EventChainRuntime.SchemaVersion, true);
                if (eventChainSchemaVersion == EventChainRuntime.SchemaVersion)
                {
                    if (Scribe.mode == LoadSaveMode.Saving && eventChainRuntime != null)
                        EventChainSaveXml.Synchronize(eventChainRuntime.States, ref eventChainProviders, item => new EventChainProviderSave(item), item => item.State);
                    EventChainSaveXml.LookList(ref eventChainProviders, "eventChainProviders", preservedEventChainProviders);
                }
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && eventChainProviders == null) eventChainProviders = new List<EventChainProviderSave>();
            if (Scribe.mode == LoadSaveMode.Saving)
                for (int i = 0; i < unknownEventChainFields.Count; i++) EventChainSaveXml.WriteNode(unknownEventChainFields[i]);
        }
        public bool ReloadModRules()
        {
            Pawn preview = Find.Selector == null || Find.Selector.SelectedPawns.Count == 0 ? null : Find.Selector.SelectedPawns[0];
            return OxygenContentHost.ReloadRules(preview);
        }

        public bool SetModRulesEnabled(string packageId, bool value)
        {
            if (!OxygenContentHost.SetProviderEnabled(packageId, value)) return false;
            disabledContentProviders.RemoveAll(id => string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase));
            if (!value) disabledContentProviders.Add(packageId.ToLowerInvariant());
            return true;
        }

        public override void FinalizeInit()
        {
            OxygenContentHost.RestoreGameState(disabledContentProviders, contentLayouts);
            eventChainFinalizeComplete = true;
            // All component FinalizeInit and loaded references finish before migration or Prepare.
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (!ReferenceEquals(Verse.Current.Game, game)) return;
                if (Verse.Current.ProgramState == ProgramState.Playing) OxygenEventChainHost.Initialize(this, game.tickManager.TicksGame);
                else eventChainInitializationPending = true;
            });
        }

        public override void GameComponentUpdate()
        {
            if (!eventChainInitializationPending || Verse.Current.ProgramState != ProgramState.Playing || !ReferenceEquals(Verse.Current.Game, game)) return;
            eventChainInitializationPending = false;
            OxygenEventChainHost.Initialize(this, game.tickManager.TicksGame);
        }

        public bool Apply(string source)
        {
            draftSource = source;
            ScriptCompilation compiled = ScriptCompiler.Compile(source ?? string.Empty, OxygenModModuleCatalog.Instance, OxygenLocalization.Instance);
            if (!compiled.Success)
            {
                lastError = FormatDiagnostics(compiled);
                lastErrorRuleId = string.Empty;
                return false;
            }
            List<Pawn> previousSelection = selected;
            bool previouslyCaptured = selectionCaptured;
            if (UsesSelection(compiled.Program) && !selectionCaptured)
            {
                selected = new List<Pawn>();
                ReadCurrentSelection();
            }
            RuleProgram previous = program;
            string previousSource = activeSource;
            bool previousEnabled = enabled;
            List<PlannedEffect> previousPlan = new List<PlannedEffect>(plan);
            program = compiled.Program;
            activeSource = source ?? string.Empty;
            enabled = true;
            int tick = Find.TickManager.TicksGame;
            long before = AllocationProbe.Snapshot();
            clock.Restart();
            bool ok = Evaluate(tick, out string error);
            if (!ok)
            {
                program = previous;
                activeSource = previousSource;
                enabled = previousEnabled;
                selected = previousSelection;
                selectionCaptured = previouslyCaptured;
                plan.Clear();
                plan.AddRange(previousPlan);
                lastError = error;
                RefreshStatuses(tick);
                FinishTiming(tick, before);
                return false;
            }
            lastError = string.Empty;
            lastErrorRuleId = string.Empty;
            ApplyPlan(tick);
            RefreshStatuses(tick);
            FinishTiming(tick, before);
            revision++;
            return true;

        }

        public bool EvaluateNow()
        {
            return Commit(false);
        }

        public bool SetEnabled(bool value)
        {
            if (value == enabled) return true;
            if (!value)
            {
                enabled = false;
                RemoveAllOwned();
                activeEffectCount = 0;
                lastError = string.Empty;
                lastErrorRuleId = string.Empty;
                RefreshStatuses(Find.TickManager.TicksGame);
                return true;
            }
            if (program == null)
            {
                lastError = "No active script.";
                lastErrorRuleId = string.Empty;
                return false;
            }

            enabled = true;
            if (!Commit(false))
            {
                enabled = false;
                return false;
            }
            return true;
        }

        public void CaptureSelection()
        {
            ReadCurrentSelection();
            lastError = selected.Count == 0 ? "No selected pawn." : string.Empty;
            lastErrorRuleId = string.Empty;
            if (enabled && program != null) Commit(false);
        }

        void ReadCurrentSelection()
        {
            selected.Clear();
            selectionCaptured = true;
            if (Find.Selector == null) return;
            List<Pawn> current = Find.Selector.SelectedPawns;
            for (int i = 0; i < current.Count; i++)
            {
                Pawn pawn = current[i];
                if (pawn != null && !pawn.Destroyed && !selected.Contains(pawn)) selected.Add(pawn);
            }
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (eventChainReady) eventChainRuntime.Tick(tick);
            if (!enabled || program == null || program.Rules.Count == 0) return;
            if (tick <= lastEvaluationTick || (tick - lastEvaluationTick) < evaluationInterval) return;
            Commit(true);
        }

        public override void ExposeData()
        {
            ExposeEventChains();
            Scribe_Values.Look(ref draftSource, "draftSource");
            Scribe_Values.Look(ref activeSource, "activeSource", string.Empty);
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref revision, "revision", 0);
            Scribe_Values.Look(ref evaluationInterval, "evaluationInterval", DefaultInterval);
            Scribe_Values.Look(ref lastError, "lastError", string.Empty);
            Scribe_Values.Look(ref lastEvaluationTick, "lastEvaluationTick", -1);
            Scribe_Values.Look(ref evaluationCount, "evaluationCount", 0);
            if (Scribe.mode == LoadSaveMode.Saving) RememberOwnedPawns();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                contentLayouts.Clear();
                foreach (OxygenContentEntry entry in OxygenContentHost.Entries)
                    if (entry.Content != null) contentLayouts.Add(new ContentLayoutRecord { PackageId = entry.PackageId, Signature = entry.LayoutSignature });
            }
            Scribe_Collections.Look(ref disabledContentProviders, "disabledContentProviders", LookMode.Value);
            Scribe_Collections.Look(ref contentLayouts, "contentLayouts", LookMode.Deep);
            Scribe_Collections.Look(ref selected, "selected", LookMode.Reference);
            Scribe_Values.Look(ref selectionCaptured, "selectionCaptured", false);
            Scribe_Collections.Look(ref ownedPawns, "ownedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (selected == null) selected = new List<Pawn>();
                if (ownedPawns == null) ownedPawns = new List<Pawn>();
                if (cooldowns == null) cooldowns = new List<CooldownRecord>();
                if (disabledContentProviders == null) disabledContentProviders = new List<string>();
                if (contentLayouts == null) contentLayouts = new List<ContentLayoutRecord>();
                cooldowns.RemoveAll(record => record == null || record.Pawn == null || record.Pawn.Destroyed || string.IsNullOrEmpty(record.RuleId));
                if (evaluationInterval < 1) evaluationInterval = DefaultInterval;
                if (activeSource != null)
                {
                    ScriptCompilation compiled = ScriptCompiler.Compile(activeSource, OxygenModModuleCatalog.Instance, OxygenLocalization.Instance);
                    if (compiled.Success) program = compiled.Program;
                    else
                    {
                        enabled = false;
                        lastError = FormatDiagnostics(compiled);
                        lastErrorRuleId = string.Empty;
                    }
                }
                RebindLoadedMemories();
                activeEffectCount = owned.Count;
                RefreshStatuses(Find.TickManager.TicksGame);

            }
        }



        bool Commit(bool periodic)
        {
            if (!enabled || program == null)
            {
                lastError = enabled ? "No active script." : "Script is disabled.";
                lastErrorRuleId = string.Empty;
                return false;
            }
            int tick = Find.TickManager.TicksGame;
            long before = AllocationProbe.Snapshot();
            clock.Restart();
            bool ok = Evaluate(tick, out string error);
            if (ok)
            {
                lastError = string.Empty;
                lastErrorRuleId = string.Empty;
                ApplyPlan(tick);
            }
            else lastError = error;
            RefreshStatuses(tick);
            FinishTiming(tick, before);
            return ok;
        }

        void FinishTiming(int tick, long before)
        {
            clock.Stop();
            long after = AllocationProbe.Snapshot();
            lastEvaluationMs = clock.Elapsed.TotalMilliseconds;
            lastAllocatedBytes = before < 0L || after < 0L ? -1L : after - before;
            lastEvaluationTick = tick;
            evaluationCount++;
        }

        static bool UsesSelection(RuleProgram source)
        {
            if (source == null) return false;
            for (int i = 0; i < source.Rules.Count; i++)
            {
                if (source.Rules[i].Target == RuleTarget.Selected) return true;
            }
            return false;
        }

        bool Evaluate(int tick, out string error)
        {
            error = null;
            scratch.Clear();
            if (program.Rules.Count == 0)
            {
                plan.Clear();
                return true;
            }

            Map map = Find.CurrentMap;

            CollectCandidates(program, candidates);

            for (int ruleIndex = 0; ruleIndex < program.Rules.Count; ruleIndex++)
            {
                RuleDefinition rule = program.Rules[ruleIndex];
                for (int pawnIndex = 0; pawnIndex < candidates.Count; pawnIndex++)
                {
                    Pawn pawn = candidates[pawnIndex];
                    if (!InTarget(rule, pawn, map)) continue;
                    if (!CanReceive(pawn, out string skip))
                    {
                        scratch.Add(new PlannedEffect { RuleId = rule.Id, PawnId = pawn.thingIDNumber, Skip = skip });
                        continue;
                    }
                    bool holds;
                    if (rule.Condition == null) holds = true;
                    else
                    {
                        GameContext game = new GameContext { Tick = tick, Hour = HourOf(pawn) };
                        try { holds = rule.Condition.Evaluate(MakeContext(pawn), game); }
                        catch (Exception ex)
                        {
                            error = FormatCondition(rule, pawn, ex, out string failedRule);
                            lastErrorRuleId = failedRule;
                            return false;

                        }
                    }
                    if (!holds)
                    {
                        scratch.Add(new PlannedEffect { RuleId = rule.Id, PawnId = pawn.thingIDNumber, Skip = "Condition false." });
                        continue;
                    }
                    scratch.Add(new PlannedEffect { RuleId = rule.Id, PawnId = pawn.thingIDNumber, Hold = true });
                }
            }
            plan.Clear();
            plan.AddRange(scratch);
            scratch.Clear();
            return true;
        }

        void ApplyPlan(int tick)
        {
            activeIds.Clear();
            for (int i = 0; i < program.Rules.Count; i++) activeIds.Add(program.Rules[i].Id);
            RemoveRetired(activeIds);
            SyncContent(tick);
            heldMoods.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                PlannedEffect item = plan[i];
                if (!item.Hold) continue;
                RuleDefinition rule = FindRule(item.RuleId);
                Pawn pawn = FindPawn(item.PawnId);
                if (rule == null || pawn == null) continue;
                if (rule.Kind == RuleKind.Mood)
                {
                    heldMoods.Add(new KeyValuePair<string, int>(item.RuleId, item.PawnId));
                    MaintainMood(rule, pawn, tick);
                }
                else MaintainMemory(rule, pawn, tick);
            }
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                Thought_Memory_Oxygendsl thought = owned[i];
                if (!thought.persistent) continue;
                if (!heldMoods.Contains(new KeyValuePair<string, int>(thought.ruleId, thought.pawn.thingIDNumber)))
                    RemoveOwned(thought);
            }
            PruneCooldowns();
            activeEffectCount = owned.Count;
        }

        void MaintainMood(RuleDefinition rule, Pawn pawn, int tick)
        {
            Thought_Memory_Oxygendsl existing = FindOwned(rule.Id, pawn, true);
            if (existing != null)
            {
                existing.ruleLabel = rule.Label;
                existing.ruleDescription = rule.Description;
                existing.ruleOffset = rule.Offset;

                return;
            }
            AddThought(rule, pawn, tick, true, -1);
        }

        void MaintainMemory(RuleDefinition rule, Pawn pawn, int tick)
        {
            if (tick < CooldownUntil(pawn, rule.Id)) return;

            List<Thought_Memory_Oxygendsl> memories = MemoriesOf(rule.Id, pawn);
            if (rule.Mode == MemoryMode.Refresh && memories.Count > 0)
            {
                Thought_Memory_Oxygendsl current = memories[0];
                for (int i = 1; i < memories.Count; i++) RemoveOwned(memories[i]);
                int duration = rule.DurationTicks < 1 ? 1 : rule.DurationTicks;
                current.triggeredTick = tick;
                current.expireTick = tick + duration;
                current.age = 0;
                current.durationTicksOverride = duration;
                current.ruleLabel = rule.Label;
                current.ruleDescription = rule.Description;
                current.ruleOffset = rule.Offset;
                SetCooldown(pawn, rule.Id, tick + rule.CooldownTicks);
                return;
            }
            if (rule.Mode == MemoryMode.Stack)
            {
                int limit = rule.StackLimit < 1 ? 1 : rule.StackLimit;
                while (memories.Count >= limit)
                {
                    Thought_Memory_Oxygendsl oldest = Oldest(memories);
                    RemoveOwned(oldest);
                    memories.Remove(oldest);
                }
            }
            int expire = tick + (rule.DurationTicks < 1 ? 1 : rule.DurationTicks);
            AddThought(rule, pawn, tick, false, expire);
            SetCooldown(pawn, rule.Id, tick + rule.CooldownTicks);
        }

        void AddThought(RuleDefinition rule, Pawn pawn, int tick, bool persistent, int expire)
        {
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(CarrierDefName);
            if (def == null || pawn.needs == null || pawn.needs.mood == null) return;
            if (!ThoughtUtility.CanGetThought(pawn, def, false)) return;
            Thought_Memory_Oxygendsl thought = (Thought_Memory_Oxygendsl)ThoughtMaker.MakeThought(def);
            thought.ruleId = rule.Id;
            thought.ruleLabel = rule.Label;
            thought.ruleDescription = rule.Description;
            thought.ruleOffset = rule.Offset;
            thought.triggeredTick = tick;
            thought.expireTick = expire;
            thought.persistent = persistent;
            thought.age = 0;
            thought.durationTicksOverride = persistent ? int.MaxValue / 4 : Math.Max(1, expire - tick);
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            if (pawn.needs.mood.thoughts.memories.Memories.Contains(thought))
            {
                owned.Add(thought);

            }
        }

        void SyncContent(int tick)
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                Thought_Memory_Oxygendsl thought = owned[i];
                RuleDefinition rule = FindRule(thought.ruleId);
                if (rule == null || rule.Kind != (thought.persistent ? RuleKind.Mood : RuleKind.Memory))
                {
                    RemoveOwned(thought);
                    continue;
                }
                thought.ruleLabel = rule.Label;
                thought.ruleDescription = rule.Description;
                thought.ruleOffset = rule.Offset;
                if (!thought.persistent)
                {
                    int duration = rule.DurationTicks < 1 ? 1 : rule.DurationTicks;
                    thought.expireTick = thought.triggeredTick + duration;
                    thought.durationTicksOverride = duration;
                    if (tick >= thought.expireTick) RemoveOwned(thought);
                }
            }
            for (int i = 0; i < owned.Count; i++)
            {
                Thought_Memory_Oxygendsl thought = owned[i];
                if (thought.persistent) continue;
                RuleDefinition rule = FindRule(thought.ruleId);
                List<Thought_Memory_Oxygendsl> memories = MemoriesOf(rule.Id, thought.pawn);
                int limit = rule.Mode == MemoryMode.Refresh ? 1 : rule.StackLimit;
                bool trimmed = false;
                while (memories.Count > limit)
                {
                    Thought_Memory_Oxygendsl oldest = Oldest(memories);
                    RemoveOwned(oldest);
                    memories.Remove(oldest);
                    trimmed = true;
                }
                if (trimmed) i = -1;
            }
        }

        void RemoveRetired(HashSet<string> ids)
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(owned[i].ruleId)) RemoveOwned(owned[i]);
            }
            for (int i = cooldowns.Count - 1; i >= 0; i--)
            {
                if (!ids.Contains(cooldowns[i].RuleId)) cooldowns.RemoveAt(i);
            }

        }

        void RemoveAllOwned()
        {
            for (int i = owned.Count - 1; i >= 0; i--) RemoveOwned(owned[i]);
        }

        void RemoveOwned(Thought_Memory_Oxygendsl thought)
        {
            if (thought == null) return;
            owned.Remove(thought);
            Pawn pawn = thought.pawn;
            if (pawn == null || pawn.needs == null || pawn.needs.mood == null) return;
            MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
            if (memories.Memories.Contains(thought)) memories.RemoveMemory(thought);
        }



        void RememberOwnedPawns()
        {
            ownedPawns.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn pawn = owned[i].pawn;
                if (pawn != null && !pawn.Destroyed && !ownedPawns.Contains(pawn)) ownedPawns.Add(pawn);
            }
            for (int i = 0; i < cooldowns.Count; i++)
            {
                Pawn pawn = cooldowns[i].Pawn;
                if (pawn != null && !pawn.Destroyed && !ownedPawns.Contains(pawn)) ownedPawns.Add(pawn);
            }
        }

        void RebindLoadedMemories()
        {
            owned.Clear();
            seenPawns.Clear();
            for (int i = 0; i < ownedPawns.Count; i++) Adopt(ownedPawns[i]);
            for (int i = 0; i < selected.Count; i++) Adopt(selected[i]);
            for (int i = 0; i < cooldowns.Count; i++) Adopt(cooldowns[i].Pawn);
        }




        void Adopt(Pawn pawn)
        {
            if (pawn == null || pawn.needs == null || pawn.needs.mood == null) return;
            if (!seenPawns.Add(pawn.thingIDNumber)) return;
            List<Thought_Memory> memories = pawn.needs.mood.thoughts.memories.Memories;
            for (int i = memories.Count - 1; i >= 0; i--)
            {
                Thought_Memory_Oxygendsl thought = memories[i] as Thought_Memory_Oxygendsl;
                if (thought == null || string.IsNullOrEmpty(thought.ruleId)) continue;
                thought.pawn = pawn;
                owned.Add(thought);
            }
        }

        void CollectCandidates(RuleProgram source, List<Pawn> result)
        {
            result.Clear();
            bool colonists = false;
            bool map = false;
            bool selectedTarget = false;
            for (int i = 0; i < source.Rules.Count; i++)
            {
                RuleTarget target = source.Rules[i].Target;
                if (target == RuleTarget.Colonists) colonists = true;
                else if (target == RuleTarget.Map) map = true;
                else selectedTarget = true;
            }
            Map current = Find.CurrentMap;
            if (current != null && (colonists || map))
            {
                IReadOnlyList<Pawn> pawns = map ? current.mapPawns.AllPawnsSpawned : current.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < pawns.Count; i++) result.Add(pawns[i]);
            }
            if (selectedTarget)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    Pawn pawn = selected[i];
                    if (pawn != null && !pawn.Destroyed && !result.Contains(pawn)) result.Add(pawn);
                }
            }
        }

        bool InTarget(RuleDefinition rule, Pawn pawn, Map map)
        {
            if (rule.Target == RuleTarget.Selected) return selected.Contains(pawn);
            if (map == null || pawn.MapHeld != map) return false;
            if (rule.Target == RuleTarget.Colonists) return pawn.IsColonist;
            return true;
        }

        static bool CanReceive(Pawn pawn, out string reason)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                reason = "Dead.";
                return false;
            }
            if (pawn.needs == null || pawn.needs.mood == null)
            {
                reason = "No mood need.";
                return false;
            }
            reason = null;
            return true;
        }

        static double HourOf(Pawn pawn)
        {
            Map map = pawn == null ? null : pawn.MapHeld;
            if (map == null) map = Find.CurrentMap;
            return map == null ? 0d : GenLocalDate.HourFloat(map);
        }

        static PawnContext MakeContext(Pawn pawn)
        {
            return PawnFacts.Create(pawn);
        }

        RuleDefinition FindRule(string id)
        {
            if (program == null) return null;
            for (int i = 0; i < program.Rules.Count; i++)
            {
                if (program.Rules[i].Id == id) return program.Rules[i];
            }
            return null;
        }

        Pawn FindPawn(int id)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].thingIDNumber == id) return candidates[i];
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].pawn != null && owned[i].pawn.thingIDNumber == id) return owned[i].pawn;
            }
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != null && selected[i].thingIDNumber == id) return selected[i];
            }
            return null;
        }

        Thought_Memory_Oxygendsl FindOwned(string ruleId, Pawn pawn, bool persistent)
        {
            for (int i = 0; i < owned.Count; i++)
            {
                Thought_Memory_Oxygendsl thought = owned[i];
                if (thought.persistent == persistent && thought.ruleId == ruleId && thought.pawn == pawn)
                    return thought;
            }
            return null;
        }

        List<Thought_Memory_Oxygendsl> MemoriesOf(string ruleId, Pawn pawn)
        {
            List<Thought_Memory_Oxygendsl> result = memoryScratch;
            result.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                Thought_Memory_Oxygendsl thought = owned[i];
                if (!thought.persistent && thought.ruleId == ruleId && thought.pawn == pawn)
                    result.Add(thought);
            }
            return result;
        }

        static Thought_Memory_Oxygendsl Oldest(List<Thought_Memory_Oxygendsl> memories)
        {
            Thought_Memory_Oxygendsl oldest = memories[0];
            for (int i = 1; i < memories.Count; i++)
            {
                if (memories[i].triggeredTick < oldest.triggeredTick) oldest = memories[i];
            }
            return oldest;
        }

        int CooldownUntil(Pawn pawn, string ruleId)
        {
            int until = int.MinValue;
            for (int i = 0; i < cooldowns.Count; i++)
            {
                CooldownRecord record = cooldowns[i];
                if (record.Pawn == pawn && record.RuleId == ruleId && record.UntilTick > until)
                    until = record.UntilTick;
            }
            return until;
        }

        void SetCooldown(Pawn pawn, string ruleId, int until)
        {
            for (int i = cooldowns.Count - 1; i >= 0; i--)
            {
                if (cooldowns[i].Pawn == pawn && cooldowns[i].RuleId == ruleId) cooldowns.RemoveAt(i);
            }
            cooldowns.Add(new CooldownRecord { Pawn = pawn, RuleId = ruleId, UntilTick = until });
        }


        void PruneCooldowns()
        {
            for (int i = cooldowns.Count - 1; i >= 0; i--)
            {
                CooldownRecord record = cooldowns[i];
                if (record == null || record.Pawn == null || record.Pawn.Destroyed) cooldowns.RemoveAt(i);
            }
        }

        void RefreshStatuses(int tick)
        {
            statuses.Clear();
            if (program == null) return;
            for (int i = 0; i < program.Rules.Count; i++)
            {
                RuleDefinition rule = program.Rules[i];
                RuleStatus status = new RuleStatus
                {
                    Id = rule.Id,
                    Label = rule.Label,
                    Kind = rule.Kind,
                    LastTick = tick
                };
                for (int planIndex = 0; planIndex < plan.Count; planIndex++)
                {
                    PlannedEffect item = plan[planIndex];
                    if (item.RuleId != rule.Id) continue;
                    if (item.Hold) status.Matched++;
                    else
                    {
                        status.Skipped++;
                        status.Details.Add(item.PawnId + ": " + item.Skip);
                    }
                }
                for (int ownedIndex = 0; ownedIndex < owned.Count; ownedIndex++)
                {
                    if (owned[ownedIndex].ruleId == rule.Id) status.Active++;
                }
                if (lastErrorRuleId == rule.Id && !string.IsNullOrEmpty(lastError))
                {
                    status.LastError = lastError;
                    status.Details.Add(lastError);
                }
                statuses.Add(status);
            }
        }

        static string FormatDiagnostics(ScriptCompilation compiled)
        {
            if (compiled == null || compiled.Diagnostics == null || compiled.Diagnostics.Count == 0)
                return "Script failed.";
            return compiled.Diagnostics[0].ToString();
        }

        static string FormatCondition(RuleDefinition rule, Pawn pawn, Exception ex, out string ruleId)
        {
            ScriptEvaluationException script = ex as ScriptEvaluationException;
            int line = script == null ? rule.Line : script.Line;
            int column = script == null ? rule.Column : script.Column;
            string name = pawn == null ? "?" : pawn.LabelShort;
            ruleId = rule.Id;
            return line + ":" + column + " " + rule.Id + " " + name + " " + ex.Message;
        }

        struct PlannedEffect
        {
            public string RuleId;
            public int PawnId;
            public bool Hold;
            public string Skip;
        }



        sealed class CooldownRecord : IExposable
        {
            public Pawn Pawn;
            public string RuleId;
            public int UntilTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref Pawn, "pawn");
                Scribe_Values.Look(ref RuleId, "ruleId");
                Scribe_Values.Look(ref UntilTick, "untilTick", 0);
            }
        }
    }
}
