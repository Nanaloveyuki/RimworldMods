using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public sealed partial class GameComponent_MouseDisasterEventBehavior
    {
        private Dictionary<int, int> fedDepartureTicks = new Dictionary<int, int>();
        private Dictionary<int, int> noFoodSinceTicks = new Dictionary<int, int>();
        private HashSet<Pawn> departingPawns = new HashSet<Pawn>();
        private readonly Dictionary<int, int> nextFoodSearchTicks = new Dictionary<int, int>();

        private void ExposeVisitTimers()
        {
            Scribe_Collections.Look(ref fedDepartureTicks, "fedDepartureTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref noFoodSinceTicks, "noFoodSinceTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref departingPawns, "departingPawns", LookMode.Reference);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            fedDepartureTicks ??= new Dictionary<int, int>();
            noFoodSinceTicks ??= new Dictionary<int, int>();
            departingPawns ??= new HashSet<Pawn>();
            fedPawnIds ??= new HashSet<int>();
            foreach (int id in fedPawnIds)
                if (!fedDepartureTicks.ContainsKey(id))
                    fedDepartureTicks[id] = (Find.TickManager?.TicksGame ?? 0) +
                        (int)((MouseDisasterMod.Settings?.fedWanderDays ?? MouseDisasterSettings.DefaultFedWanderDays) * GenDate.TicksPerDay);
        }

        internal bool FedDepartureDue(Pawn pawn) => pawn != null &&
            fedDepartureTicks.TryGetValue(pawn.thingIDNumber, out int until) && Find.TickManager.TicksGame >= until;

        internal bool IsDeparting(Pawn pawn) => pawn != null && departingPawns.Contains(pawn);

        internal bool ReferencesFaction(Faction faction) => groups.Any(g => g?.faction == faction);

        internal static bool HasProtectedVisit(Pawn pawn) =>
            MouseDisasterUtility.HasActivePendingPawn(pawn) || MouseDisasterUtility.MustStayForAirDropError(pawn) ||
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.IsWaitingEnvoy(pawn) == true ||
            pawn.GetLord()?.LordJob is LordJob_MouseDisasterBegForItems ||
            pawn.GetLord()?.LordJob is LordJob_MouseDisasterFamilyExit ||
            pawn.GetLord()?.LordJob is LordJob_TravelAndExit;

        internal bool ManagesFoodVisit(Pawn pawn) => pawn?.Spawned == true && pawn.needs?.food != null &&
            TryGetGroup(pawn, out var group) && !group.hostile && !group.leaving &&
            !MouseDisasterUtility.IsHostileTo(pawn.Faction, Faction.OfPlayer) && HasFoodSeekingProfile(pawn) && !HasProtectedVisit(pawn);

        internal void RequestDeparture(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn)) return;
            departingPawns.Add(pawn);
        }

        private static void EnsureDepartureLord(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Downed || pawn.GetLord()?.LordJob is LordJob_MouseDisasterDeparture) return;
            pawn.GetLord()?.RemovePawn(pawn);
            pawn.mindState?.mentalStateHandler?.Reset();
            pawn.jobs?.StopAll();
            LordMaker.MakeNewLord(pawn.Faction, new LordJob_MouseDisasterDeparture(), pawn.Map, new[] { pawn });
        }

        private void TickVisits()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            // Walk existing membership only. Food/path searches happen in the cached job giver, not here.
            foreach (MouseDisasterEventGroup group in groups)
            {
                if (group == null) continue;
                if (group.faction == null && group.pawns.Count > 0) Apply(group, group.pawns);
                if (!group.hostile && MouseDisasterUtility.IsHostileTo(group.faction, Faction.OfPlayer))
                {
                    group.hostile = true;
                    group.leaving = false;
                    Apply(group, group.pawns);
                }
                if (Current.Game?.GetComponent<GameComponent_MouseDisasterPawnGeneration>()?.HasPendingBehaviorGroup(group.id) == true) continue;
                if (!group.hostile && !group.leaving) AssignVisitLord(group.pawns);
                foreach (Pawn pawn in group.pawns)
                {
                    if (!ManagesFoodVisit(pawn)) continue;
                    if (MouseDisasterFeeding.ShouldLeaveAfterFed(pawn)) RequestDeparture(pawn);
                }
            }
            departingPawns.RemoveWhere(p => p == null || p.Dead || p.Destroyed ||
                MouseDisasterUtility.IsPlayerAffiliatedRatkin(p) || (!p.Spawned && p.MapHeld == null));
            foreach (Pawn pawn in departingPawns) EnsureDepartureLord(pawn);
        }

        private void ForgetVisitTimers(Pawn pawn)
        {
            int id = pawn.thingIDNumber;
            fedDepartureTicks.Remove(id);
            noFoodSinceTicks.Remove(id);
            nextFoodSearchTicks.Remove(id);
            fedPawnIds.Remove(id);
            refeedingPawnIds.Remove(id);
        }

        internal bool TryVisitJob(Pawn pawn, out Job job)
        {
            job = null;
            if (!ManagesFoodVisit(pawn) || pawn.Downed || pawn.InAggroMentalState) return false;
            if (IsDeparting(pawn) || MouseDisasterFeeding.ShouldLeaveAfterFed(pawn))
            {
                RequestDeparture(pawn);
                job = MouseDisasterUtility.ExitMapJob(pawn, force: true);
                return true;
            }
            MouseDisasterFeeding.Evaluate(pawn, pawn.needs.food.CurLevelPercentage);
            if (MouseDisasterFeeding.IsSeekingSuppressed(pawn)) return true;
            int id = pawn.thingIDNumber;
            int now = Find.TickManager.TicksGame;
            if (nextFoodSearchTicks.TryGetValue(id, out int next) && now < next) return true;
            nextFoodSearchTicks[id] = now + 250;
            job = MouseDisasterUtility.TryCreateImproperFoodJob(pawn, allowInventorySearch: false);
            if (job != null)
            {
                noFoodSinceTicks.Remove(id);
                return true;
            }
            if (!noFoodSinceTicks.TryGetValue(id, out int since)) noFoodSinceTicks[id] = since = now;
            int waitTicks = MouseDisasterMod.Settings?.waitWhenNoFood != false
                ? (int)((MouseDisasterMod.Settings?.noFoodWaitDays ?? 0.5f) * GenDate.TicksPerDay) : 0;
            if (now - since >= waitTicks)
            {
                RequestDeparture(pawn);
                job = MouseDisasterUtility.ExitMapJob(pawn, force: true);
            }
            else job = JobGiver_MouseDisasterBeggar.TryCreateVisitorBeggingJob(pawn);
            return true;
        }

        private static void AssignVisitLord(IEnumerable<Pawn> pawns)
        {
            foreach (Pawn protectedPawn in pawns.Where(p => p != null && !p.Dead && HasProtectedVisit(p)))
            {
                Lord visitLord = protectedPawn.GetLord();
                if (visitLord?.LordJob is LordJob_MouseDisasterVisit)
                {
                    visitLord.RemovePawn(protectedPawn);
                    if (protectedPawn.mindState?.duty?.def == MouseDisasterDefOf.MouseDisaster_ReliefVisit)
                        protectedPawn.mindState.duty = null;
                }
            }

            foreach (Lord lord in pawns.Where(p => p != null && !p.Dead && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p))
                .Select(p => p.GetLord()).Where(l => l?.LordJob is LordJob_TradeWithColony &&
                l.LordJob is not LordJob_MouseDisasterTrade).Distinct().ToList())
            {
                var members = lord.ownedPawns.ToList();
                foreach (Pawn pawn in members) lord.RemovePawn(pawn);
                LordMaker.MakeNewLord(lord.faction, new LordJob_MouseDisasterTrade(lord.faction, lord.Map.Center), lord.Map, members);
            }
            foreach (var mapPawns in pawns.Where(p => p != null && p.Spawned && !p.Dead && !HasProtectedVisit(p) &&
                p.CurJob?.exitMapOnArrival != true &&
                Component.ManagesFoodVisit(p) && !Component.IsDeparting(p) &&
                p.GetLord()?.LordJob is not LordJob_MouseDisasterVisit &&
                p.GetLord()?.LordJob is not LordJob_TradeWithColony).GroupBy(p => p.Map))
            {
                var list = mapPawns.ToList();
                foreach (Pawn pawn in list)
                {
                    pawn.GetLord()?.RemovePawn(pawn);
                    pawn.mindState?.mentalStateHandler?.Reset();
                    pawn.jobs?.StopAll();
                }
                Lord existing = mapPawns.Key.lordManager.lords.FirstOrDefault(l =>
                    l.faction == list[0].Faction && l.LordJob is LordJob_MouseDisasterVisit);
                if (existing != null) existing.AddPawns(list);
                else LordMaker.MakeNewLord(list[0].Faction, new LordJob_MouseDisasterVisit(), mapPawns.Key, list);
            }
        }
    }

    public class LordJob_MouseDisasterDeparture : LordJob
    {
        public override bool AddFleeToil => false;
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_ExitMap(LocomotionUrgency.Walk, false, false));
            return graph;
        }
    }

    public class LordJob_MouseDisasterVisit : LordJob
    {
        public override bool AddFleeToil => false;
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_MouseDisasterVisit());
            return graph;
        }
    }

    // Preserve the trader type contract for stock/trade integrations, but do not use its exit graph.
    public class LordJob_MouseDisasterTrade : LordJob_TradeWithColony
    {
        public LordJob_MouseDisasterTrade() { }
        public LordJob_MouseDisasterTrade(Faction faction, IntVec3 spot) : base(faction, spot) { }
        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_MouseDisasterVisit());
            return graph;
        }
    }

    public class LordToil_MouseDisasterVisit : LordToil
    {
        public override void UpdateAllDuties()
        {
            Area_MouseDisasterRelief area = MouseDisasterUtility.GetReliefArea(lord.Map);
            IntVec3 spot = area?.TrueCount > 0 ? area.ActiveCells.First() : lord.Map.Center;
            foreach (Pawn pawn in lord.ownedPawns)
                pawn.mindState.duty = new PawnDuty(MouseDisasterDefOf.MouseDisaster_ReliefVisit, spot, 12f);
        }
        public override void LordToilTick()
        {
            if (Find.TickManager.TicksGame % GenDate.TicksPerHour == 0) UpdateAllDuties();
        }
    }

    public class JobGiver_MouseDisasterVisit : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var state = GameComponent_MouseDisasterEventBehavior.Component;
            return state != null && state.TryVisitJob(pawn, out Job job) ? job : null;
        }
    }

    [HarmonyPatch(typeof(JobGiver_ExitMap), "TryGiveJob")]
    internal static class MouseDisasterUnscheduledExitPatch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (MouseDisasterUtility.IsPendingAbandonedChild(pawn) ||
                (MouseDisasterUtility.IsAbandonedDeliveryPawn(pawn) &&
                 !MouseDisasterUtility.IsAbandonedDeliveryLeaving(pawn)))
            {
                __result = null;
                return false;
            }

            var state = GameComponent_MouseDisasterEventBehavior.Component;
            if (state?.ManagesFoodVisit(pawn) != true || state.IsDeparting(pawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(FactionManager), "FactionCanBeRemoved")]
    internal static class MouseDisasterEventFactionRetentionPatch
    {
        public static void Postfix(Faction faction, ref bool __result)
        {
            if (__result && MouseDisasterUtility.IsEventBehaviorFaction(faction) &&
                (GameComponent_MouseDisasterEventBehavior.Component?.ReferencesFaction(faction) == true ||
                 Current.Game?.GetComponent<GameComponent_MouseDisasterVisitorControl>()?.ReferencesFaction(faction) == true))
                __result = false;
        }
    }
}
