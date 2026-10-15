using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class LordJob_MouseDisasterBegForItems : LordJob
    {
        public LordJob_MouseDisasterBegForItems()
        {
        }

        public LordJob_MouseDisasterBegForItems(Faction faction, IntVec3 waitSpot, Pawn target, ThingDef thingDef, int amount)
        {
            this.faction = faction;
            this.waitSpot = waitSpot;
            this.target = target;
            this.thingDef = thingDef;
            this.amount = amount;
        }

        public override bool LostImportantReferenceDuringLoading
        {
            get
            {
                return faction == null || target == null || thingDef == null || amount <= 0;
            }
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_MouseDisasterTravelAndWaitForItems travel = new LordToil_MouseDisasterTravelAndWaitForItems(
                waitSpot,
                target,
                thingDef,
                amount);
            LordToil_MouseDisasterBegForItems wait = new LordToil_MouseDisasterBegForItems(
                waitSpot,
                target,
                thingDef,
                amount);
            graph.AddToil(travel);
            graph.StartingToil = travel;
            graph.AddToil(wait);

            LordToil_ExitMap exit = new LordToil_ExitMap(LocomotionUrgency.None, false, false);
            graph.AddToil(exit);

            Transition arrived = new Transition(travel, wait, false, true);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrived, false);

            Transition fulfilled = new Transition(wait, exit, false, true);
            fulfilled.AddSource(travel);
            fulfilled.AddTrigger(new Trigger_Custom(_ => wait.HasValidTarget && wait.HasAllRequestedItems));
            fulfilled.AddPostAction(new TransitionAction_Custom(() =>
            {
                if (wait.HasValidTarget)
                {
                    Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeDelivery(new[] { wait.target });
                }
            }));
            fulfilled.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(fulfilled, false);

            Transition invalidTarget = new Transition(wait, exit, false, true);
            invalidTarget.AddSource(travel);
            invalidTarget.AddTrigger(new Trigger_Custom(_ => !wait.HasValidTarget));
            invalidTarget.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(invalidTarget, false);

            LordToil_ExitMapAndDefendSelf defendSelf = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(defendSelf);
            Transition hostile = new Transition(wait, defendSelf, false, true);
            hostile.AddSource(travel);
            hostile.AddTrigger(new Trigger_BecamePlayerEnemy());
            hostile.AddTrigger(new Trigger_PawnKilled());
            hostile.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hostile, false);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref waitSpot, "waitSpot", IntVec3.Invalid);
            Scribe_References.Look(ref target, "target");
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref amount, "amount", 0);
        }

        public static Lord MakeOrReplaceLord(
            Faction faction,
            IntVec3 waitSpot,
            Pawn target,
            ThingDef thingDef,
            int amount,
            Map map,
            IEnumerable<Pawn> pawns)
        {
            List<Pawn> pawnList = pawns?
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (map == null ||
                target == null ||
                target.Dead ||
                target.Destroyed ||
                !target.Spawned ||
                target.Map != map ||
                target.inventory?.innerContainer == null ||
                thingDef == null ||
                amount <= 0 ||
                pawnList.Count == 0)
            {
                return null;
            }

            Lord existingLord = target.GetLord();
            if (existingLord != null && pawnList.All(pawn => pawn.GetLord() == existingLord))
            {
                existingLord.SetJob(new LordJob_MouseDisasterBegForItems(faction, waitSpot, target, thingDef, amount));
                return existingLord;
            }

            HashSet<Lord> oldLords = new HashSet<Lord>();
            for (int i = 0; i < pawnList.Count; i++)
            {
                Lord oldLord = pawnList[i].GetLord();
                if (oldLord != null)
                {
                    oldLords.Add(oldLord);
                    oldLord.RemovePawn(pawnList[i]);
                }
            }

            foreach (Lord oldLord in oldLords)
            {
                if (oldLord.ownedPawns.Count == 0 && oldLord.lordManager != null && oldLord.lordManager.lords.Contains(oldLord))
                {
                    oldLord.lordManager.RemoveLord(oldLord);
                }
            }

            return LordMaker.MakeNewLord(
                faction,
                new LordJob_MouseDisasterBegForItems(faction, waitSpot, target, thingDef, amount),
                map,
                pawnList);
        }

        private Faction faction;
        private IntVec3 waitSpot;
        private Pawn target;
        private ThingDef thingDef;
        private int amount;
    }

    public class LordToil_MouseDisasterTravelAndWaitForItems : LordToil_Travel, IWaitForItemsLordToil
    {
        public LordToil_MouseDisasterTravelAndWaitForItems(IntVec3 waitSpot, Pawn target, ThingDef thingDef, int amount)
            : base(waitSpot)
        {
            this.target = target;
            requestedThingDef = thingDef;
            requestedThingCount = amount;
        }

        public bool HasValidTarget => target != null && !target.Dead && !target.Destroyed && target.Spawned && target.Map == Map && target.inventory?.innerContainer != null;

        public int CountRemaining
        {
            get
            {
                if (!HasValidTarget || requestedThingDef == null || requestedThingCount <= 0)
                {
                    return 0;
                }

                return Math.Max(0, GiveItemsToPawnUtility.GetCountRemaining(target, requestedThingDef, requestedThingCount));
            }
        }

        public bool HasAllRequestedItems => CountRemaining <= 0;

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            if (pawn == target && pawn.Spawned && pawn.Map != null)
            {
                pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
            }
        }

        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Pawn requester, Pawn current)
        {
            if (requester == target && current != null && HasValidTarget && requestedThingDef != null)
            {
                foreach (FloatMenuOption option in GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn(
                    requester,
                    current,
                    requestedThingDef,
                    requestedThingCount))
                {
                    yield return option;
                }
            }
        }

        public Pawn target;
        public ThingDef requestedThingDef;
        public int requestedThingCount;
    }

    public class LordToil_MouseDisasterBegForItems : LordToil_DefendPoint, IWaitForItemsLordToil
    {
        public LordToil_MouseDisasterBegForItems(IntVec3 waitSpot, Pawn target, ThingDef thingDef, int amount)
            : base(waitSpot, 12f, 10f)
        {
            this.target = target;
            requestedThingDef = thingDef;
            requestedThingCount = amount;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState != null)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.WanderClose_NoNeeds, FlagLoc, 10f);
                }
            }
        }

        public bool HasValidTarget => target != null && !target.Dead && !target.Destroyed && target.Spawned && target.Map == Map && target.inventory?.innerContainer != null;

        public int CountRemaining
        {
            get
            {
                if (!HasValidTarget || requestedThingDef == null || requestedThingCount <= 0)
                {
                    return 0;
                }

                return Math.Max(0, GiveItemsToPawnUtility.GetCountRemaining(target, requestedThingDef, requestedThingCount));
            }
        }

        public bool HasAllRequestedItems => CountRemaining <= 0;

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            if (pawn == target && pawn.Spawned && pawn.Map != null)
            {
                pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
            }
        }

        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Pawn requester, Pawn current)
        {
            if (requester == target && current != null && HasValidTarget && requestedThingDef != null)
            {
                foreach (FloatMenuOption option in GiveItemsToPawnUtility.GetFloatMenuOptionsForPawn(
                    requester,
                    current,
                    requestedThingDef,
                    requestedThingCount))
                {
                    yield return option;
                }
            }
        }

        public Pawn target;
        public ThingDef requestedThingDef;
        public int requestedThingCount;
    }

    [HarmonyPatch(typeof(Lord), nameof(Lord.LordTick))]
    internal static class MouseDisasterVanillaBegForItemsCleanupPatch
    {
        private static readonly AccessTools.FieldRef<LordJob_BegForItems, Pawn> TargetField =
            AccessTools.FieldRefAccess<LordJob_BegForItems, Pawn>("target");
        private static readonly AccessTools.FieldRef<LordJob_BegForItems, Faction> FactionField =
            AccessTools.FieldRefAccess<LordJob_BegForItems, Faction>("faction");

        internal static bool IsValidLegacyLord(Lord lord, LordJob_BegForItems begForItems)
        {
            Map map = lord?.lordManager?.map;
            Pawn target = begForItems == null ? null : TargetField(begForItems);
            Faction faction = begForItems == null ? null : FactionField(begForItems);
            return lord != null &&
                lord.ownedPawns != null &&
                lord.ownedPawns.Count > 0 &&
                map != null &&
                faction != null &&
                target != null &&
                !target.Dead &&
                !target.Destroyed &&
                target.Spawned &&
                target.Map == map &&
                target.inventory?.innerContainer != null;
        }

        internal static void RemoveLegacyLord(Lord lord)
        {
            Map map = lord?.lordManager?.map;
            if (map?.lordManager != null && map.lordManager.lords.Contains(lord))
            {
                map.lordManager.RemoveLord(lord);
            }
        }

        public static bool Prefix(Lord __instance)
        {
            LordJob_BegForItems begForItems = __instance?.LordJob as LordJob_BegForItems;
            if (begForItems == null)
            {
                return true;
            }

            if (IsValidLegacyLord(__instance, begForItems))
            {
                return true;
            }

            RemoveLegacyLord(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Lord), "ExposeData_StateGraph")]
    internal static class MouseDisasterVanillaBegForItemsLoadCleanupPatch
    {
        public static bool Prefix(Lord __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return true;
            }

            LordJob_BegForItems begForItems = __instance?.LordJob as LordJob_BegForItems;
            if (begForItems == null || MouseDisasterVanillaBegForItemsCleanupPatch.IsValidLegacyLord(__instance, begForItems))
            {
                return true;
            }

            MouseDisasterVanillaBegForItemsCleanupPatch.RemoveLegacyLord(__instance);
            return false;
        }
    }
}
