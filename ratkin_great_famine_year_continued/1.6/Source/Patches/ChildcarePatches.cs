using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterChildcarePolicy
    {
        internal static bool AllowsColonistCare(Faction carerFaction, Pawn baby)
        {
            if (MouseDisasterMod.Settings?.allowColonistChildcareForMouseDisasterEggs != true ||
                carerFaction == null ||
                Faction.OfPlayer == null ||
                carerFaction != Faction.OfPlayer ||
                baby == null ||
                !IsMouseDisasterNeutralFaction(baby.Faction) ||
                !MouseDisasterUtility.IsMouseEggBaby(baby))
            {
                return false;
            }

            return MouseDisasterUtility.IsMouseDisasterPawn(baby) ||
                   MouseDisasterUtility.IsMouseDisasterIncidentVisitor(baby);
        }

        internal static bool IsAllowedCarer(Pawn pawn)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            return pawn.IsColonist
                ? MouseDisasterMod.Settings?.allowColonistChildcareForMouseDisasterEggs == true
                : MouseDisasterMod.Settings?.allowNonColonistChildcareForMouseDisasterEggs == true;
        }

        internal static bool AllowsCare(Pawn carer, Pawn baby)
        {
            return IsAllowedCarer(carer) && IsMouseDisasterBabyTarget(baby);
        }

        internal static bool IsMouseDisasterBabyTarget(Pawn baby)
        {
            return baby != null &&
                   IsMouseDisasterNeutralFaction(baby.Faction) &&
                   MouseDisasterUtility.IsMouseEggBaby(baby) &&
                   (MouseDisasterUtility.IsMouseDisasterPawn(baby) ||
                    MouseDisasterUtility.IsMouseDisasterIncidentVisitor(baby));
        }

        internal static IEnumerable<Pawn> AllowedBabies(Map map, Pawn carer, bool includeUnspawned)
        {
            if (map?.mapPawns == null)
            {
                yield break;
            }

            IReadOnlyList<Pawn> pawns = includeUnspawned ? map.mapPawns.AllPawns : map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn baby = pawns[i];
                if (baby != null && !baby.Dead && AllowsCare(carer, baby))
                {
                    yield return baby;
                }
            }
        }

        internal static IEnumerable<Thing> FilterAndAppendAllowedBabies(IEnumerable<Thing> original, Pawn carer)
        {
            HashSet<Pawn> seen = new HashSet<Pawn>();
            if (original != null)
            {
                foreach (Thing thing in original)
                {
                    Pawn baby = thing as Pawn;
                    if (baby != null &&
                        IsMouseDisasterBabyTarget(baby) &&
                        !AllowsCare(carer, baby))
                    {
                        continue;
                    }

                    if (baby != null)
                    {
                        seen.Add(baby);
                    }
                    yield return thing;
                }
            }

            foreach (Pawn baby in AllowedBabies(carer?.Map, carer, includeUnspawned: false))
            {
                if (seen.Add(baby))
                {
                    yield return baby;
                }
            }
        }

        private static bool IsMouseDisasterNeutralFaction(Faction faction)
        {
            return faction != null &&
                   (faction.def == MouseDisasterDefOf.MouseDisaster_HiddenFaction ||
                    faction.def == MouseDisasterDefOf.MouseDisaster_NeutralVisitors) &&
                   Faction.OfPlayer != null &&
                   !MouseDisasterUtility.IsHostileTo(faction, Faction.OfPlayer);
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.HasBreastfeedCompatibleFactions), new[] { typeof(Faction), typeof(Pawn) })]
    internal static class MouseDisasterChildcareFactionPatch
    {
        public static void Postfix(Faction faction, Pawn baby, ref bool __result)
        {
            if (!__result && MouseDisasterChildcarePolicy.AllowsColonistCare(faction, baby))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.HasBreastfeedCompatibleFactions), new[] { typeof(Pawn), typeof(Pawn) })]
    internal static class MouseDisasterChildcarePawnFactionPatch
    {
        public static void Postfix(Pawn mom, Pawn baby, ref bool __result)
        {
            if (MouseDisasterChildcarePolicy.IsMouseDisasterBabyTarget(baby))
            {
                __result = MouseDisasterChildcarePolicy.AllowsCare(mom, baby);
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_PlayWithBaby), nameof(WorkGiver_PlayWithBaby.PotentialWorkThingsGlobal))]
    internal static class MouseDisasterPlayWithBabyTargetsPatch
    {
        public static void Postfix(Pawn pawn, ref IEnumerable<Thing> __result)
        {
            __result = MouseDisasterChildcarePolicy.FilterAndAppendAllowedBabies(__result, pawn);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_PlayWithBaby), nameof(WorkGiver_PlayWithBaby.HasJobOnThing))]
    internal static class MouseDisasterPlayWithBabyJobPatch
    {
        public static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            Pawn baby = t as Pawn;
            if (__result &&
                MouseDisasterChildcarePolicy.IsMouseDisasterBabyTarget(baby) &&
                !MouseDisasterChildcarePolicy.AllowsCare(pawn, baby))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.FindAutofeedBaby))]
    internal static class MouseDisasterAutofeedBabyPatch
    {
        public static void Postfix(Pawn mom, AutofeedMode priorityLevel, ref Thing food, ref Pawn __result)
        {
            if (__result != null &&
                MouseDisasterChildcarePolicy.IsMouseDisasterBabyTarget(__result) &&
                !MouseDisasterChildcarePolicy.AllowsCare(mom, __result))
            {
                food = null;
                __result = null;
                return;
            }

            if (__result != null ||
                priorityLevel == AutofeedMode.Never ||
                !MouseDisasterChildcarePolicy.IsAllowedCarer(mom) ||
                mom.MapHeld == null)
            {
                return;
            }

            ChildcareUtility.BreastfeedFailReason? breastfeedFailReason;
            bool canBreastfeed = ChildcareUtility.CanBreastfeedNow(mom, out breastfeedFailReason);
            foreach (Pawn baby in MouseDisasterChildcarePolicy.AllowedBabies(mom.MapHeld, mom, includeUnspawned: true))
            {
                if (baby.mindState == null ||
                    baby.Suspended ||
                    !ChildcareUtility.WantsSuckle(baby, out breastfeedFailReason) ||
                    baby.mindState.AutofeedSetting(mom) != priorityLevel ||
                    !ChildcareUtility.CanFeedBaby(mom, baby, out breastfeedFailReason) ||
                    !ChildcareUtility.CanHaulBabyToMomNow(mom, mom, baby, false, out breastfeedFailReason))
                {
                    continue;
                }

                Thing candidateFood = canBreastfeed ? mom : ChildcareUtility.FindBabyFoodForBaby(mom, baby);
                if (candidateFood == null)
                {
                    continue;
                }

                food = candidateFood;
                __result = baby;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.FindUnsafeBaby))]
    internal static class MouseDisasterUnsafeBabyPatch
    {
        public static void Postfix(Pawn mom, AutofeedMode priorityLevel, ref Pawn __result)
        {
            if (__result != null &&
                MouseDisasterChildcarePolicy.IsMouseDisasterBabyTarget(__result) &&
                !MouseDisasterChildcarePolicy.AllowsCare(mom, __result))
            {
                __result = null;
                return;
            }

            if (__result != null ||
                priorityLevel == AutofeedMode.Never ||
                !MouseDisasterChildcarePolicy.IsAllowedCarer(mom) ||
                mom.MapHeld == null)
            {
                return;
            }

            ChildcareUtility.BreastfeedFailReason? breastfeedFailReason;
            foreach (Pawn baby in MouseDisasterChildcarePolicy.AllowedBabies(mom.MapHeld, mom, includeUnspawned: false))
            {
                if (baby.mindState == null ||
                    baby.mindState.AutofeedSetting(mom) != priorityLevel ||
                    CaravanFormingUtility.IsFormingCaravanOrDownedPawnToBeTakenByCaravan(baby) ||
                    !ChildcareUtility.CanSuckle(baby, out breastfeedFailReason))
                {
                    continue;
                }

                LocalTargetInfo safePlace = ChildcareUtility.SafePlaceForBaby(baby, mom, false);
                if (!safePlace.IsValid)
                {
                    continue;
                }

                Building_Bed bed = safePlace.Thing as Building_Bed;
                if ((bed != null && baby.CurrentBed() == bed) ||
                    (bed == null && baby.Spawned && baby.Position == safePlace.Cell))
                {
                    continue;
                }

                __result = baby;
                return;
            }
        }
    }
}
