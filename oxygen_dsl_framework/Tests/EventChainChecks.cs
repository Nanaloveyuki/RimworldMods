using System;
using System.Collections.Generic;
using System.Threading;
using Oxygendsl.Core;
using Oxygendsl.Core.Events;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class EventChainChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            DeadlinesAndStages(check);
            ReceiptsAndRestore(check);
            AtomicMigrationAndOwnership(check);
            ModesAndCallbacks(check);
            ScriptIsolation(check);
        }

        private static void DeadlinesAndStages(Action<bool, string> check)
        {
            var definition = new Definition();
            EventChainRuntime runtime = Runtime(definition);
            int id = runtime.Start("owner", "I-002", 0, 4, 0, 20, "facts", nextTick: 10);
            definition.Decide = (instance, tick) => tick == 10 ? EventChainDecision.Action("roll", 20, 1) : EventChainDecision.Finish(EventChainEnd.Completed);
            runtime.Tick(9);
            check(runtime.CallbackCount == 0 && runtime.NextDueTick == 10, "future tick does not enumerate/call chains");
            runtime.Tick(10);
            check(runtime.Find("owner", id).Stage == 1 && definition.Applies == 1 && runtime.NextDueTick == 20, "action commits stage and next due once");
            runtime.Tick(20);
            check(runtime.Find("owner", id).End == EventChainEnd.Completed && runtime.ActiveCount == 0, "due decision at exact deadline can complete");
            int late = runtime.Start("owner", "I-002", 0, 4, 20, 25, "facts", nextTick: 25);
            int evaluated = definition.Evaluates;
            runtime.Tick(26);
            check(runtime.Find("owner", late).End == EventChainEnd.Expired && definition.Evaluates == evaluated, "overdue deadline never performs a late action");
            int dormant = runtime.Start("owner", "I-002", 0, 4, 26, 30, "facts");
            runtime.Tick(30);
            check(runtime.Find("owner", dormant).End == EventChainEnd.Expired && definition.Evaluates == evaluated, "dormant stage still has indexed deadline expiry");
            definition.Decide = (instance, tick) => EventChainDecision.Wait(-1);
            int exact = runtime.Start("owner", "I-002", 0, 4, 30, 31, "facts", nextTick: 31);
            runtime.Tick(31);
            check(runtime.Find("owner", exact).End == EventChainEnd.Expired && definition.Evaluates == evaluated + 1,
                "exact deadline evaluates once then expires an open decision");
            definition.Decide = (instance, tick) => EventChainDecision.Wait(tick);
            int looping = runtime.Start("owner", "I-002", 0, 4, 31, -1, "facts", nextTick: 32);
            runtime.Tick(32);
            check(runtime.NextDueTick == -1 && runtime.Find("owner", looping).PauseReason.Length != 0, "nonadvancing decision pauses instead of immediate loop");
            var indexed = Runtime(new Definition());
            int a = indexed.Start("owner", "I-002", 1, 0, 0, -1, "facts", nextTick: 100);
            int b = indexed.Start("owner", "I-002", 2, 0, 0, -1, "facts", nextTick: 50);
            indexed.Start("owner", "I-002", 1, 0, 0, -1, "facts", nextTick: 75);
            indexed.SetStage("owner", a, 1, -1, "facts", 25);
            indexed.End("owner", b, 0, EventChainEnd.Cancelled);
            check(indexed.NextDueTick == 25 && indexed.ActiveCountFor("owner", "I-002", 1) == 2
                && indexed.ActiveCountFor("owner", siteId: 2) == 0, "indexed reschedule/remove keeps one heap node and exact active-owner counts");
            indexed.Tick(25);
            check(indexed.NextDueTick == 75 && indexed.CallbackCount == 1, "heap removes rescheduled root without stale due callbacks");
        }

        private static void ReceiptsAndRestore(Action<bool, string> check)
        {
            var definition = new Definition { Decide = (instance, tick) => EventChainDecision.Action("roll", -1, 2) };
            EventChainRuntime runtime = Runtime(definition);
            int id = runtime.Start("owner", "I-002", 0, 0, 0, -1, "facts", nextTick: 1);
            bool pendingVisible = false;
            definition.Act = (instance, tick, action) =>
            {
                pendingVisible = instance.Receipts.Count == 1 && instance.Receipts[0].Status == "pending";
                return EventChainActionResult.Applied("chosen-random-result");
            };
            runtime.Tick(1); runtime.Tick(1);
            EventChainInstance instance = runtime.Find("owner", id);
            check(pendingVisible && definition.Applies == 1 && instance.Revision == 1 && instance.Receipts[0].Data == "chosen-random-result", "pending is visible before side effects; commit exactly once");
            var restoredInstance = Instance(7, 1);
            restoredInstance.Receipts.Add(new EventChainReceipt { Key = "0:roll", ActionId = "roll", Status = "applied", Data = "saved", Revision = 0, Tick = 1 });
            var restored = new EventChainRuntime(new List<EventChainProviderState> { State(restoredInstance, 8) });
            var restoredDefinition = new Definition { Decide = definition.Decide };
            restored.Register(new Provider(restoredDefinition)); restored.Activate(1); restored.Tick(1);
            check(restoredDefinition.Applies == 0 && restoredInstance.Stage == 2 && restoredInstance.Revision == 1, "restored applied same-revision receipt skips replay but commits transition");
            var uncertain = Instance(9, 1);
            uncertain.Receipts.Add(new EventChainReceipt { Key = "0:roll", ActionId = "roll", Status = "pending", Revision = 0, Tick = 1 });
            var blocked = new EventChainRuntime(new List<EventChainProviderState> { State(uncertain, 10) });
            blocked.Register(new Provider(restoredDefinition)); blocked.Activate(1); blocked.Tick(1);
            blocked.SetMode("owner", EventChainMode.Paused, 1); blocked.SetMode("owner", EventChainMode.Active, 5); blocked.Tick(6);
            check(blocked.NextDueTick == -1 && blocked.Describe().Contains("pending") && restoredDefinition.Applies == 0, "pending receipt blocks restore and mode resume without retry");
            definition.Act = (chain, tick, action) => { throw new InvalidOperationException("after world mutation"); };
            int failing = runtime.Start("owner", "I-002", 0, 0, 1, -1, "facts", nextTick: 2);
            runtime.Tick(2); runtime.Tick(3);
            EventChainInstance failure = runtime.Find("owner", failing);
            check(failure.Receipts[0].Status == "pending" && failure.Revision == 0 && failure.PauseReason.Contains("after world mutation"), "exception retains uncertain pending receipt and never retries");
        }

        private static void AtomicMigrationAndOwnership(Action<bool, string> check)
        {
            var definition = new Definition();
            var runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new Provider(definition));
            string error;
            EventChainInstance valid = Instance(3, 1);
            EventChainInstance invalid = Instance(4, 1); invalid.Payload = "bad";
            check(!runtime.Import("owner", new[] { valid, invalid }, 5, "legacy", out error)
                && !runtime.HasProviderState("owner") && valid.PreparedState == null && error.Length != 0, "failed prepare import is atomic and leaves originals untouched");
            EventChainInstance terminal = Instance(4, -1); terminal.End = EventChainEnd.Completed; terminal.Payload = "bad";
            check(runtime.Import("owner", new[] { valid, terminal }, 5, "legacy", out error), "terminal raw payload does not block active migration");
            runtime.Activate(0);
            check(runtime.Find("owner", 3).Id == 3 && runtime.Find("owner", 3).NextTick == 1 && runtime.GetProviderState("owner").NextId == 5, "migration preserves clocks IDs and monotonic allocation");
            var empty = new EventChainRuntime(new List<EventChainProviderState>());
            empty.Register(new Provider(new Definition()));
            check(empty.Import("owner", new EventChainInstance[0], 1, "failed: retained original", out error)
                && empty.SetMode("owner", EventChainMode.Paused, 0, "migration failed"), "empty failed partition supports mode before activation");
            check(!empty.Import("owner", new[] { valid }, 4, "retry", out error), "explicit empty authoritative partition rejects reimport");
            empty.Activate(0);
            check(empty.Start("owner", "I-002", 0, 0, 0, -1, "facts") == 0, "failed migration paused partition blocks new chain entries");
            var unknownState = State(Instance(2, 1), 3); unknownState.ProviderId = "missing-owner";
            var schemaState = State(Instance(3, 1), 4); schemaState.SchemaVersion = 9;
            var retained = new EventChainRuntime(new List<EventChainProviderState> { unknownState, schemaState });
            retained.Register(new Provider(new Definition())); retained.Activate(0); retained.Tick(100);
            check(retained.Describe().Contains("owner-unavailable") && retained.Describe().Contains("schema mismatch")
                && unknownState.Instances[0].NextTick == 1 && unknownState.Mode == EventChainMode.Active
                && schemaState.SchemaVersion == 9 && schemaState.Instances[0].PauseReason == "", "unknown owners and schemas retain bytes and stop callbacks");
            EventChainInstance versioned = Instance(1, 1); versioned.DefinitionVersion = 2;
            var versions = new EventChainRuntime(new List<EventChainProviderState> { State(versioned, 2) });
            versions.Register(new Provider(new Definition())); versions.Activate(0); versions.Tick(1);
            check(versions.Describe().Contains("Definition version mismatch") && versioned.Revision == 0, "unknown loaded definition version refuses execution");
            int prepareCount = definition.Prepares;
            Throws(() => runtime.Register(new Provider(new Definition { Version = 2 })), check, "incompatible definition replacement rejects atomically");
            runtime.Tick(1);
            check(definition.Evaluates == 1 && definition.Prepares == prepareCount, "incompatible replacement retains original provider");
            var replacement = new Definition();
            runtime.Register(new Provider(replacement));
            runtime.SetStage("owner", 3, 1, -1, "facts", 2); runtime.Tick(2);
            check(replacement.Evaluates == 1, "compatible replacement switches callbacks without resetting live state");
            Throws(() => runtime.Register(new Provider(new Definition(), apiVersion: 2)), check, "unknown provider API rejects registration");
        }

        private static void ModesAndCallbacks(Action<bool, string> check)
        {
            var definition = new Definition();
            EventChainRuntime runtime = Runtime(definition);
            int id = runtime.Start("owner", "I-002", 8, 0, 0, 20, "facts", nextTick: 10);
            runtime.SetMode("owner", EventChainMode.Paused, 5, "player pause"); runtime.Tick(8);
            check(runtime.NextDueTick == -1 && runtime.ActiveCountFor("owner", "I-002", 8) == 1 && definition.Evaluates == 0, "paused provider retains active lookup without callbacks");
            runtime.SetMode("owner", EventChainMode.Disabled, 8, "new content disabled");
            check(runtime.Start("owner", "I-002", 8, 0, 8, -1, "facts") == 0, "disabled blocks new instances without cancellation");
            runtime.SetMode("owner", EventChainMode.Active, 15);
            EventChainInstance instance = runtime.Find("owner", id);
            check(instance.NextTick == 20 && instance.DeadlineTick == 30 && instance.StartedTick == 0 && instance.Stage == 0, "resume shifts clocks by original pause elapsed only");
            definition.Decide = (chain, tick) => EventChainDecision.Action("spawn-other", -1);
            int child = 0; bool recursiveRejected = false, selfRejected = false;
            definition.Act = (chain, tick, action) =>
            {
                try { runtime.Tick(tick); } catch (InvalidOperationException) { recursiveRejected = true; }
                try { runtime.SetStage("owner", chain.Id, 2, -1, "facts", -1); } catch (InvalidOperationException) { selfRejected = true; }
                child = runtime.Start("owner", "I-002", 9, 0, tick, -1, "facts", nextTick: tick + 1);
                return EventChainActionResult.Applied();
            };
            runtime.Tick(20);
            check(recursiveRejected && selfRejected && child > id && runtime.NextDueTick == 21, "callbacks reject recursion and self mutation but allow indexed other starts");
            check(runtime.End("owner", child, 20, EventChainEnd.Cancelled) && runtime.End("owner", child, 20, EventChainEnd.Cancelled)
                && !runtime.End("owner", child, 20, EventChainEnd.Completed), "terminal transitions are idempotent without changing terminal identity");
            var overflow = Runtime(new Definition());
            overflow.Start("owner", "I-002", 0, 0, 0, int.MaxValue, "facts", nextTick: 10);
            overflow.SetMode("owner", EventChainMode.Paused, 0);
            check(!overflow.SetMode("owner", EventChainMode.Active, 1) && overflow.GetProviderState("owner").Instances[0].NextTick == 10,
                "overflowing resume leaves every provider clock unchanged");
            Exception threadError = null;
            var thread = new Thread(() => { try { runtime.Tick(21); } catch (Exception exception) { threadError = exception; } });
            thread.Start(); thread.Join();
            check(threadError is InvalidOperationException, "runtime rejects cross-thread mutations");
        }

        private static void ScriptIsolation(Action<bool, string> check)
        {
            EventChainScript script = EventChainScript.Compile("use events.ctx@event as ctx; const step = 2500; fn evaluate(chain) { if chain.stage == 0 { return ctx.action(\"roll\", next_tick: -1, stage: 2) }; return ctx.wait(chain.tick + step) }");
            var first = new DictionaryOxygenHostValue(new Dictionary<string, object> { { "stage", 0 }, { "tick", 1 } });
            var later = new DictionaryOxygenHostValue(new Dictionary<string, object> { { "stage", 1 }, { "tick", 1 } });
            check(script.Evaluate(first).ActionId == "roll" && script.Evaluate(later).NextTick == 2501
                && script.Evaluate(first).Stage == 2, "real prepared script decisions receive fresh readonly snapshots");
            Throws(() => EventChainScript.Compile("use events.ctx@event as ctx; let counter = 0; fn evaluate(chain) { counter += 1; return ctx.wait(counter) }"), check, "mutable scalar globals cannot leak decisions");
            Throws(() => EventChainScript.Compile("use events.ctx@event as ctx; const xs = [1]; fn evaluate(chain) { xs[0] = 2; return ctx.wait(xs[0]) }"), check, "mutable graph behind const is rejected");
            EventChainScript invalid = EventChainScript.Compile("use events.ctx@event as ctx; fn evaluate(chain) { return ctx.action(\"roll\", next_tick: 2, typo: 0) }");
            Throws(() => invalid.Evaluate(first), check, "decision helper rejects unknown named arguments");
            Throws(() => EventChainScript.Compile("use events.ctx@event as ctx; fn evaluate(chain) { chain.stage = 9; return ctx.wait(2) }"), check, "decision scripts cannot write snapshot facts");
            Throws(() => EventChainScript.Compile("use events.ctx@event as ctx; fn evaluate(chain) { let alias = core; alias.saved = chain.tick; return ctx.wait(2) }"), check, "builtin namespace aliases cannot retain state across decisions");
            EventChainScript infinite = EventChainScript.Compile("fn evaluate(chain) { while true { }; return null }");
            Throws(() => infinite.Evaluate(first), check, "decision invocation enforces existing interpreter budget");
            var modules = new OxygenModuleRegistry();
            modules.RegisterSource("helper", "state", "let counter = 0; pub fn increment() { counter += 1; return counter }");
            Throws(() => EventChainScript.Compile("use events.ctx@event as ctx; use state@helper as state; fn evaluate(chain) { return ctx.wait(state.increment()) }", modules: modules), check, "imported mutable module globals cannot retain decision state");
        }

        private static EventChainRuntime Runtime(Definition definition)
        {
            var runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new Provider(definition)); runtime.Activate(0); return runtime;
        }
        private static EventChainInstance Instance(int id, int due)
        { return new EventChainInstance { Id = id, DefinitionId = "I-002", Payload = "facts", NextTick = due }; }
        private static EventChainProviderState State(EventChainInstance instance, int nextId)
        { return new EventChainProviderState { ProviderId = "owner", NextId = nextId, Instances = new List<EventChainInstance> { instance } }; }
        private static void Throws(Action action, Action<bool, string> check, string scenario)
        {
            try { action(); } catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is ScriptEvaluationException)
            { check(true, scenario); return; }
            throw new InvalidOperationException("Expected EventChain failure: " + scenario);
        }
        private sealed class Provider : IEventChainProvider
        {
            public string ProviderId { get { return "owner"; } }
            public int ApiVersion { get; }
            public int StateVersion { get { return 1; } }
            public IReadOnlyList<IEventChainDefinition> Definitions { get; }
            internal Provider(Definition definition, int apiVersion = 1)
            { ApiVersion = apiVersion; Definitions = new IEventChainDefinition[] { definition }; }
        }
        private sealed class Definition : IEventChainDefinition
        {
            public string Id { get { return "I-002"; } }
            public int Version { get; set; } = 1;
            internal int Prepares, Evaluates, Applies;
            internal Func<EventChainInstance, int, EventChainDecision> Decide = (instance, tick) => EventChainDecision.Wait(-1);
            internal Func<EventChainInstance, int, string, EventChainActionResult> Act = (instance, tick, action) => EventChainActionResult.Applied();
            public void Prepare(EventChainInstance instance)
            {
                Prepares++;
                if (instance.Payload == "bad") throw new ArgumentException("malformed facts");
                instance.PreparedState = "typed-facts";
            }
            public EventChainDecision Evaluate(EventChainInstance instance, int tick) { Evaluates++; return Decide(instance, tick); }
            public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId) { Applies++; return Act(instance, tick, actionId); }
        }
    }
}
