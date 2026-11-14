using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_LongHandoverTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.long.handover.tests");
        readonly Dictionary<int, Verse.Pawn> index;
        readonly Dictionary<int, Verse.Pawn> previousIndex;
        readonly int previousBuiltTick;
        readonly Game previousBuiltGame;
        readonly Map map;
        readonly RHAH_LongFollowRecord record;
        readonly Verse.Pawn receiver;
        readonly List<Verse.Pawn> children = new List<Verse.Pawn>();
        EventChainRuntime runtime;
        bool exitAvailable = true;
        int unreachableId;
        int throwsOnExitId;
        int exitCalls;
        int receiverLeaveCalls;
        static RHAH_LongHandoverTests active;

        public RHAH_LongHandoverTests()
        {
            active = this;
            Current.Game.letterStack = new LetterStack();
            map = Find.Maps[0];
            map.info = new MapInfo { Size = new IntVec3(20, 1, 20), parent = new MapParent() };
            map.mapPawns = new MapPawns(map);
            index = (Dictionary<int, Verse.Pawn>)AccessTools.Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
            previousIndex = new Dictionary<int, Verse.Pawn>(index);
            previousBuiltTick = (int)AccessTools.Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
            previousBuiltGame = (Game)AccessTools.Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
            AccessTools.Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, Current.Game);
            AccessTools.Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, 0);
            index.Clear();
            receiver = Pawn(1901, false);
            children.Add(Pawn(1902, true));
            children.Add(Pawn(1903, true));
            record = new RHAH_LongFollowRecord
            {
                ChainId = 7, DisplayId = "I-032", BatchId = 31, MapId = map.uniqueID,
                SubjectId = children[0].thingIDNumber, Phase = 2, Opened = true, LetterOpen = true
            };
            record.VisitorIds.Add(receiver.thingIDNumber);
            foreach (Verse.Pawn child in children)
            {
                record.ChildIds.Add(child.thingIDNumber);
                record.SelectedIds.Add(child.thingIDNumber);
            }
            Current.Game.GetComponent<GameComponent_RHAH_Game>().LongFollows.Add(record);
            Patch(AccessTools.PropertyGetter(typeof(OxygenEventChainHost), nameof(OxygenEventChainHost.Runtime)), nameof(ReadRuntime));
            Patch(AccessTools.Method(typeof(RCellFinder), nameof(RCellFinder.TryFindBestExitSpot)), nameof(FindExit));
            Patch(AccessTools.Method(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach), new[]
            {
                typeof(Verse.Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger),
                typeof(bool), typeof(bool), typeof(TraverseMode)
            }), nameof(Reach));
            Patch(AccessTools.Method(typeof(Verse.Pawn), nameof(Verse.Pawn.SetFaction)), nameof(SetFaction));
            Patch(AccessTools.Method(typeof(Verse.Pawn), nameof(Verse.Pawn.ExitMap)), nameof(ExitMap));
            Patch(AccessTools.Method(typeof(Verse.Pawn), nameof(Verse.Pawn.Notify_PassedToWorld)), nameof(Ignore));
            Patch(AccessTools.Method(typeof(RHAH_VisitorGroup), nameof(RHAH_VisitorGroup.NotifyReleased)), nameof(Ignore));
            Patch(AccessTools.Method(typeof(RHAH_LongFollowRuntime), nameof(RHAH_LongFollowRuntime.Keep)), nameof(Ignore));
            Patch(AccessTools.Method(typeof(RHAH_LongFollowRuntime), nameof(RHAH_LongFollowRuntime.Leave)), nameof(Leave));
            runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, new[]
            {
                new EventChainInstance
                {
                    Id = record.ChainId, DefinitionId = record.DisplayId, SiteId = record.MapId,
                    Stage = 3, Payload = RHAH_EventFollowRules.Payload(record.DisplayId, record.BatchId, record.MapId)
                }
            }, 8, "completed", out string error), error);
            runtime.Activate(0);
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            index.Clear();
            foreach (KeyValuePair<int, Verse.Pawn> item in previousIndex) index.Add(item.Key, item.Value);
            AccessTools.Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, previousBuiltTick);
            AccessTools.Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, previousBuiltGame);
            active = null;
            fixture.Dispose();
        }

        [Theory]
        [InlineData(GuestStatus.Prisoner)]
        [InlineData(GuestStatus.Slave)]
        public void CaptiveReceiverCannotAcquireChildrenOrKeepTheDecisionOpen(GuestStatus status)
        {
            receiver.guest = new Pawn_GuestTracker(receiver);
            receiver.guest.guestStatusInt = status;
            AccessTools.Field(typeof(Pawn_GuestTracker), "hostFactionInt").SetValue(receiver.guest, Faction.OfPlayer);

            Assert.False(RHAH_LongFollowRuntime.SendChildren(record, map, record.SelectedIds, receiver));
            Assert.True(RHAH_LongAdoptionFollow.Ready(record, map, 0));
            Assert.True(RHAH_LongAdoptionFollow.Tick(record, map, 0));
            Assert.True(record.Completed);
            AssertUnchangedChildren();
        }

        [Fact]
        public void StoryAdulthoodSettingDoesNotDisqualifyAnAdultReceiver()
        {
            RHAH_Mod.Settings.followAdultAge = 80f;

            Assert.True(RHAH_LongFollowRuntime.EligibleReceiver(receiver, map));
            Assert.True(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0),
                runtime.Find(RHAH_EventChainProvider.ProviderKey, record.ChainId).PauseReason);
            Assert.Equal(2, exitCalls);
        }

        [Fact]
        public void DownedReceiverLeavesTheDecisionAvailableWithoutTransferringChildren()
        {
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(receiver.health, PawnHealthState.Down);

            Assert.False(RHAH_LongFollowRuntime.SendChildren(record, map, record.SelectedIds, receiver));
            Assert.True(record.LetterOpen);
            AssertUnchangedChildren();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingExitOrUnreachableLaterChildDoesNotPartiallyCommit(bool unreachableChild)
        {
            if (unreachableChild) unreachableId = children[1].thingIDNumber;
            else exitAvailable = false;

            Assert.False(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0));
            Assert.Equal(2, record.Phase);
            Assert.True(record.LetterOpen);
            AssertUnchangedChildren();
        }

        [Fact]
        public void SuccessfulHandoverRecordsActualWorldPawnsBeforeAdvancingTheStory()
        {
            Assert.True(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0),
                runtime.Find(RHAH_EventChainProvider.ProviderKey, record.ChainId).PauseReason);

            Assert.Equal(2, exitCalls);
            Assert.Equal(1, receiverLeaveCalls);
            Assert.Equal(record.SelectedIds, record.DepartedIds);
            Assert.Equal(5, record.Phase);
            Assert.False(record.LetterOpen);
            foreach (Verse.Pawn child in children)
            {
                Assert.False(child.Spawned);
                Assert.True(Find.WorldPawns.Contains(child));
                Assert.Same(receiver.Faction, child.Faction);
            }
        }

        [Fact]
        public void ExceptionAfterOneDeparturePreservesConfirmedResultsAndPreventsReplay()
        {
            throwsOnExitId = children[1].thingIDNumber;
            using (Log.LockMessages())
                Assert.False(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0));

            Assert.Equal(new[] { children[0].thingIDNumber }, record.DepartedIds);
            Assert.False(children[0].Spawned);
            Assert.True(children[1].Spawned);
            Assert.Equal(2, record.Phase);
            Assert.Contains("uncertain", runtime.Find(RHAH_EventChainProvider.ProviderKey, record.ChainId).PauseReason);
            Assert.False(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0));
            Assert.Equal(2, exitCalls);
        }

        [Theory]
        [InlineData("paused")]
        [InlineData("disabled")]
        [InlineData("instance_paused")]
        [InlineData("pending_receipt")]
        [InlineData("failed_receipt")]
        [InlineData("missing_instance")]
        [InlineData("missing_runtime")]
        public void UnavailableOdfStateCannotCommitTheOldLetter(string state)
        {
            Assert.True(RHAH_LongFollowRuntime.CanChoose(record, runtime));
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, record.ChainId);
            if (state == "paused") runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Paused, 0);
            if (state == "disabled") runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Disabled, 0);
            if (state == "instance_paused") instance.PauseReason = "Uncertain mutation";
            if (state == "pending_receipt" || state == "failed_receipt")
                instance.Receipts.Add(new EventChainReceipt { Status = state == "pending_receipt" ? "pending" : "failed" });
            if (state == "missing_instance") record.ChainId = 8;
            if (state == "missing_runtime") runtime = null;

            Assert.False(RHAH_LongFollowRuntime.CanChoose(record, runtime));
            Assert.False(RHAH_LongFollowRuntime.Choose(record.ChainId, record.Phase, "send", 0));
            Assert.Equal(2, record.Phase);
            Assert.True(record.LetterOpen);
            AssertUnchangedChildren();
        }

        void AssertUnchangedChildren()
        {
            Assert.Equal(0, exitCalls);
            Assert.Empty(record.DepartedIds);
            foreach (Verse.Pawn child in children)
            {
                Assert.True(child.Spawned);
                Assert.Same(Faction.OfPlayer, child.Faction);
            }
        }

        Verse.Pawn Pawn(int id, bool player)
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests).GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { id });
            ((HashSet<Verse.Pawn>)AccessTools.Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns)).Remove(pawn);
            pawn.def.race.lifeStageAges = new List<LifeStageAge>
            {
                new LifeStageAge { def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult } }
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            AccessTools.Field(typeof(Pawn_AgeTracker), "ageBiologicalTicksInt").SetValue(pawn.ageTracker, 24L * GenDate.TicksPerYear);
            AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            if (player)
            {
                pawn.SetFactionDirect(Faction.OfPlayer);
                CompRHAH_Pawn.TryGet(pawn).State.lifecycle = RHAH_Lifecycle.Released;
            }
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            index.Add(id, pawn);
            return pawn;
        }

        void Patch(MethodInfo target, string prefix)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(RHAH_LongHandoverTests), prefix));
        }

        static bool ReadRuntime(ref EventChainRuntime __result) { __result = active.runtime; return false; }
        static bool FindExit(ref IntVec3 spot, ref bool __result)
        {
            spot = new IntVec3(0, 0, 10);
            __result = active.exitAvailable;
            return false;
        }
        static bool Reach(LocalTargetInfo __1, ref bool __result)
        {
            __result = __1.Thing == null || __1.Thing.thingIDNumber != active.unreachableId;
            return false;
        }
        static bool SetFaction(Verse.Pawn __instance, Faction newFaction) { __instance.SetFactionDirect(newFaction); return false; }
        static bool Ignore() => false;
        static bool Leave(Verse.Pawn pawn)
        {
            Assert.Same(active.receiver, pawn);
            Assert.Equal(active.children.Count, active.record.DepartedIds.Count);
            active.receiverLeaveCalls++;
            return false;
        }
        static bool ExitMap(Verse.Pawn __instance)
        {
            active.exitCalls++;
            Assert.DoesNotContain(__instance.thingIDNumber, active.record.DepartedIds);
            if (__instance.thingIDNumber == active.throwsOnExitId) throw new InvalidOperationException("Injected uncertain exit");
            AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(__instance, (sbyte)-1);
            Find.WorldPawns.PassToWorld(__instance, PawnDiscardDecideMode.KeepForever);
            return false;
        }
    }
}
