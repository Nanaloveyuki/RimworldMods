using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetFloatMenuOptions))]
    public static class CommsConsolePatches
    {
        private static string BroadcastHopeLabel => "MouseDisaster_UI_BroadcastHope".Translate();

        public static void Postfix(Building_CommsConsole __instance, Pawn myPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            List<FloatMenuOption> options = __result?.ToList() ?? new List<FloatMenuOption>();
            FloatMenuOption option = BuildBroadcastHopeOption(__instance, myPawn);
            if (option != null)
            {
                options.Insert(0, option);
            }

            __result = options;
        }

        private static FloatMenuOption BuildBroadcastHopeOption(Building_CommsConsole console, Pawn pawn)
        {
            if (!MouseDisasterRuntime.AllowsNewContent ||
                console == null ||
                console.Map == null ||
                !console.Map.IsPlayerHome ||
                pawn == null ||
                pawn.Dead)
            {
                return null;
            }

            if (!GameComponent_MouseDisasterBroadcastHope.HasEnabledCandidates)
                return new FloatMenuOption("MouseDisaster_BroadcastNoCandidates".Translate(), null);

            if (!pawn.CanReach(console, PathEndMode.InteractionCell, Danger.Some))
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastUnreachable".Translate(BroadcastHopeLabel).Resolve(), null);
            }

            if (console.Spawned && console.Map != null && console.Map.gameConditionManager.ElectricityDisabled(console.Map))
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastSolarFlare".Translate(BroadcastHopeLabel).Resolve(), null);
            }

            CompPowerTrader powerComp = console.GetComp<CompPowerTrader>();
            if (powerComp != null && !powerComp.PowerOn)
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastNoPower".Translate(BroadcastHopeLabel).Resolve(), null);
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastCannotTalk".Translate(BroadcastHopeLabel).Resolve(), null);
            }

            if (GameComponent_MouseDisasterBroadcastHope.TryGetBroadcastCooldownRemainingDays(console.Map, out float remainingDays))
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastCooldown".Translate(BroadcastHopeLabel, remainingDays.ToString("0.0")).Resolve(), null);
            }

            if (!pawn.CanReserve(console))
            {
                return new FloatMenuOption("MouseDisaster_UI_BroadcastOccupied".Translate(BroadcastHopeLabel).Resolve(), null);
            }

            FloatMenuOption option = new FloatMenuOption(BroadcastHopeLabel, delegate
            {
                Job job = JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_BroadcastHope, console);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
            }, MenuOptionPriority.InitiateSocial);

            return FloatMenuUtility.DecoratePrioritizedTask(option, pawn, console);
        }
    }
}
