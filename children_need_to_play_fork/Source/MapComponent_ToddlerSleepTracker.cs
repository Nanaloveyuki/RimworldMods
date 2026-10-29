using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Gives the TPE_CribConfinement hediff to toddlers when they're placed in cribs.
    /// Lightweight: only checks every 250 ticks and only acts on toddlers without the hediff.
    /// 
    /// Also auto-removes TPE_CribLocked only when a toddler ages up — the lock persists
    /// even when the toddler is temporarily out of the crib (e.g., for feeding).
    /// </summary>
    public class MapComponent_ToddlerSleepTracker : MapComponent
    {
        public MapComponent_ToddlerSleepTracker(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;

            // Copy the list first — AllPawnsSpawned is a cached mutable list
            // that vanilla modifies when pawns spawn/despawn.
            List<Pawn> allSpawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn pawn in allSpawned)
            {
                // FIX N4b / CribLock v2: Auto-remove crib lock ONLY when the
                // pawn is no longer a toddler (aged up). The lock intentionally
                // persists when the toddler is temporarily out of the crib
                // (e.g., during feeding) so they get returned immediately after.
                if (pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_CribLocked) == true)
                {
                    if (!ToddlerUtility.IsToddler(pawn))
                    {
                        var lockHediff = pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_CribLocked);
                        if (lockHediff != null)
                            pawn.health.RemoveHediff(lockHediff);
                    }
                }

                if (!ToddlerUtility.IsToddler(pawn)) continue;
                if (pawn.Downed || pawn.Dead) continue;
                if (!CribUtility.InCrib(pawn)) continue;

                // FIX R5: Null-safety
                if (pawn.health?.hediffSet == null) continue;

                // All toddlers in cribs (crawling or walking) get confinement hediff
                if (pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_CribConfinement) == null)
                {
                    pawn.health.AddHediff(TPE_HediffDefOf.TPE_CribConfinement);
                }
            }
        }


    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    public static class Map_FinalizeInit_Patch
    {
        public static void Postfix(Map __instance)
        {
            if (!__instance.components.Any(c => c is MapComponent_ToddlerSleepTracker))
            {
                __instance.components.Add(new MapComponent_ToddlerSleepTracker(__instance));
            }
            if (!__instance.components.Any(c => c is MapComponent_ToddlerCribRescue))
            {
                __instance.components.Add(new MapComponent_ToddlerCribRescue(__instance));
            }
            if (!__instance.components.Any(c => c is MapComponent_ToddlerCribReturn))
            {
                __instance.components.Add(new MapComponent_ToddlerCribReturn(__instance));
            }
            if (!__instance.components.Any(c => c is MapComponent_ToddlerRandomMoods))
            {
                __instance.components.Add(new MapComponent_ToddlerRandomMoods(__instance));
            }
            if (!__instance.components.Any(c => c is MapComponent_ToddlerEventMoods))
            {
                __instance.components.Add(new MapComponent_ToddlerEventMoods(__instance));
            }
            if (!__instance.components.Any(c => c is MapComponent_CribGate))
            {
                __instance.components.Add(new MapComponent_CribGate(__instance));
            }
        }
    }
}
