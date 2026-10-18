using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(FleeUtility), nameof(FleeUtility.FleeJob), new[] { typeof(Pawn), typeof(Thing), typeof(int) })]
    internal static class MouseDisasterThiefCombatFearPatch
    {
        public static bool Prefix(Pawn pawn, Thing danger, ref Job __result)
        {
            if (MapComponent_MouseDisasterPredation.ShouldFlee(danger as Pawn, pawn)) return true;
            if ((danger is Pawn || danger is Building_Turret) && !pawn.Downed && !pawn.IsBurning() &&
                GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn, MouseDisasterPawnBehavior.IgnoreCombatFear))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class ThiefMentalStatePatch
    {
        private static readonly AccessTools.FieldRef<MentalStateHandler, Pawn> PawnField =
            AccessTools.FieldRefAccess<MentalStateHandler, Pawn>("pawn");

        public static bool Prefix(MentalStateHandler __instance, MentalStateDef stateDef, ref bool __result)
        {
            Pawn pawn = PawnField(__instance);
            if (stateDef == MentalStateDefOf.PanicFlee &&
                GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn, MouseDisasterPawnBehavior.IgnoreCombatFear) && !pawn.IsBurning())
            {
                __result = false;
                return false;
            }
            bool isThief = MouseDisasterUtility.IsThiefPawn(pawn);
            bool isBeggar = MouseDisasterUtility.IsBeggarPawn(pawn);
            if (!isThief && !isBeggar)
            {
                return true;
            }

            if (stateDef != null && stateDef.IsAggro)
            {
                if (!MouseDisasterUtility.CanMouseDisasterVisitorRetaliate(pawn))
                {
                    __result = false;
                    return false;
                }

                return true;
            }

            if (isBeggar && stateDef == MentalStateDefOf.PanicFlee && !MouseDisasterUtility.CanMouseDisasterVisitorRetaliate(pawn))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.StartFleeingBecauseOfPawnAction))]
    public static class ThiefFleePatch
    {
        private static readonly AccessTools.FieldRef<Pawn_MindState, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_MindState, Pawn>("pawn");

        public static bool Prefix(Pawn_MindState __instance)
        {
            Pawn pawn = PawnField(__instance);
            if (GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn, MouseDisasterPawnBehavior.IgnoreCombatFear) && !pawn.IsBurning()) return false;
            return !MouseDisasterUtility.IsBeggarPawn(pawn) || MouseDisasterUtility.CanMouseDisasterVisitorRetaliate(pawn);
        }
    }
}
