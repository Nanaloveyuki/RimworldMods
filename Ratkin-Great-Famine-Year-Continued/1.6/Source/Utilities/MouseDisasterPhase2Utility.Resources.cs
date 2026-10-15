using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public enum MouseDisasterRequestKind
    {
        SimpleMeal,
        FineMeal,
        Medicine,
        Silver,
        PrisonerOrSlaveBaby,
        HerbalMedicine
    }

    public enum MouseDisasterIntelSiteKind
    {
        Treasure,
        StructureCluster,
        SmallSettlement
    }
    public static partial class MouseDisasterPhase2Utility
    {

        public static float GetCurrentColonyWealth(Map map)
        {
            return map?.wealthWatcher?.WealthTotal ?? 0f;
        }

        public static int CalculateAidAmount(Map map, MouseDisasterRequestKind kind)
        {
            float wealth = GetCurrentColonyWealth(map);
            switch (kind)
            {
                case MouseDisasterRequestKind.SimpleMeal:
                    return Mathf.Clamp(6 + Mathf.RoundToInt(wealth / 12000f) + Rand.RangeInclusive(0, 4), 6, 28);
                case MouseDisasterRequestKind.FineMeal:
                    return Mathf.Clamp(4 + Mathf.RoundToInt(wealth / 18000f) + Rand.RangeInclusive(0, 3), 4, 18);
                case MouseDisasterRequestKind.Medicine:
                    return Mathf.Clamp(2 + Mathf.RoundToInt(wealth / 25000f) + Rand.RangeInclusive(0, 2), 2, 10);
                case MouseDisasterRequestKind.Silver:
                    return Mathf.Clamp(80 + Mathf.RoundToInt(wealth / 40f) + Rand.RangeInclusive(0, 120), 80, 1200);
                case MouseDisasterRequestKind.HerbalMedicine:
                    return Mathf.Clamp(3 + Mathf.RoundToInt(wealth / 22000f) + Rand.RangeInclusive(0, 3), 3, 15);
                case MouseDisasterRequestKind.PrisonerOrSlaveBaby:
                    return 1;
                default:
                    return 1;
            }
        }

        public static ThingDef ResolveRequestedThingDef(MouseDisasterRequestKind kind)
        {
            switch (kind)
            {
                case MouseDisasterRequestKind.SimpleMeal:
                    return ThingDefOf.MealSimple;
                case MouseDisasterRequestKind.FineMeal:
                    return ThingDefOf.MealFine;
                case MouseDisasterRequestKind.Medicine:
                    return ThingDefOf.MedicineIndustrial;
                case MouseDisasterRequestKind.HerbalMedicine:
                    return ThingDefOf.MedicineHerbal;
                case MouseDisasterRequestKind.Silver:
                    return ThingDefOf.Silver;
                default:
                    return null;
            }
        }

        public static bool TryConsumeRequest(Map map, MouseDisasterRequestKind kind, int amount, out string failureReason)
        {
            failureReason = string.Empty;
            if (map == null)
            {
                failureReason = "MouseDisaster_UI_InvalidMap".Translate().Resolve();
                return false;
            }

            switch (kind)
            {
                case MouseDisasterRequestKind.SimpleMeal:
                    return TryConsumeThingByDef(map, ThingDefOf.MealSimple, amount, out failureReason);
                case MouseDisasterRequestKind.FineMeal:
                    return TryConsumeThingByDef(map, ThingDefOf.MealFine, amount, out failureReason);
                case MouseDisasterRequestKind.Medicine:
                    return TryConsumeMedicine(map, amount, out failureReason);
                case MouseDisasterRequestKind.HerbalMedicine:
                    return TryConsumeThingByDef(map, ThingDefOf.MedicineHerbal, amount, out failureReason);
                case MouseDisasterRequestKind.Silver:
                    return TryConsumeThingByDef(map, ThingDefOf.Silver, amount, out failureReason);
                case MouseDisasterRequestKind.PrisonerOrSlaveBaby:
                    return TryConsumePrisonerOrSlaveBaby(map, out failureReason);
                default:
                    failureReason = "MouseDisaster_UI_UnsupportedRequest".Translate().Resolve();
                    return false;
            }
        }

        private static bool TryConsumeThingByDef(Map map, ThingDef def, int amount, out string failureReason)
        {
            failureReason = string.Empty;
            if (def == null)
            {
                failureReason = "MouseDisaster_UI_RequestedThingMissing".Translate().Resolve();
                return false;
            }

            List<Thing> things = map.listerThings.AllThings.Where(t => t?.def == def && t.stackCount > 0 && t.Spawned).ToList();
            int total = things.Sum(t => t.stackCount);
            if (total < amount)
            {
                failureReason = "MouseDisaster_UI_InsufficientStock".Translate().Resolve();
                return false;
            }

            int remaining = amount;
            for (int i = 0; i < things.Count && remaining > 0; i++)
            {
                Thing thing = things[i];
                int consume = Math.Min(thing.stackCount, remaining);
                if (consume >= thing.stackCount)
                {
                    remaining -= thing.stackCount;
                    thing.Destroy();
                }
                else
                {
                    thing.SplitOff(consume).Destroy();
                    remaining -= consume;
                }
            }

            return true;
        }

        private static bool TryConsumeMedicine(Map map, int amount, out string failureReason)
        {
            failureReason = string.Empty;
            List<Thing> medicines = map.listerThings.AllThings
                .Where(t => t?.def != null && t.def.IsMedicine && t.stackCount > 0 && t.Spawned)
                .OrderByDescending(t => t.def.BaseMarketValue)
                .ToList();
            int total = medicines.Sum(t => t.stackCount);
            if (total < amount)
            {
                failureReason = "MouseDisaster_UI_InsufficientMedicine".Translate().Resolve();
                return false;
            }

            int remaining = amount;
            for (int i = 0; i < medicines.Count && remaining > 0; i++)
            {
                Thing med = medicines[i];
                int consume = Math.Min(med.stackCount, remaining);
                if (consume >= med.stackCount)
                {
                    remaining -= med.stackCount;
                    med.Destroy();
                }
                else
                {
                    med.SplitOff(consume).Destroy();
                    remaining -= consume;
                }
            }

            return true;
        }

        private static bool TryConsumePrisonerOrSlaveBaby(Map map, out string failureReason)
        {
            failureReason = string.Empty;
            Pawn baby = MouseDisasterUtility.FindExchangeOfferBaby(map, MouseDisasterUtility.ChildExchangeModePrisoner) ??
                        MouseDisasterUtility.FindExchangeOfferBaby(map, MouseDisasterUtility.ChildExchangeModeSlave);
            if (baby == null)
            {
                failureReason = "MouseDisaster_UI_NoCaptiveBaby".Translate().Resolve();
                return false;
            }

            if (!MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            { failureReason = "MouseDisaster_Story_Stale".Translate(); return false; }
            var narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            narrative?.TrackNarrativeVisit("S14", map, new[] { baby });
            baby.guest?.SetGuestStatus(null, GuestStatus.Guest);
            baby.SetFaction(faction);
            baby.GetLord()?.RemovePawn(baby);
            baby.jobs?.StopAll();
            if (baby.Spawned) baby.DeSpawn();
            if (!Find.WorldPawns.Contains(baby)) Find.WorldPawns.PassToWorld(baby);
            narrative?.NotifyNarrativeDelivery(new[] { baby });
            return true;
        }
    }
}
