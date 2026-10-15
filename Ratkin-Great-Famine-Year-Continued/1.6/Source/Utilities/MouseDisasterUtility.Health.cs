using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static bool TrySpawnRatEggTailDrop(Pawn sourcePawn)
        {
            return TrySpawnOptionalDrop(sourcePawn, ref ratEggTailThingDef, ref ratEggTailResolved, "RatEgg_Tail", "MouseDisaster_Drop_Tail");
        }

        public static void ClampFoodToMax(Pawn pawn)
        {
            if (pawn?.needs?.food == null)
            {
                return;
            }

            pawn.needs.food.CurLevel = Mathf.Min(pawn.needs.food.CurLevel, pawn.needs.food.MaxLevel);
        }

        public static void AddOrRefreshHediff(Pawn pawn, HediffDef hediffDef, float minimumSeverity = 0.2f)
        {
            if (pawn == null || hediffDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(hediffDef);
            if (hediff.Severity < minimumSeverity)
            {
                hediff.Severity = minimumSeverity;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            disappears?.ResetElapsedTicks();
        }

        public static void RemoveHediffByDef(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static bool TrySpawnOptionalDrop(Pawn sourcePawn, ref ThingDef cachedDef, ref bool resolved, string defName, string textKey)
        {
            if (sourcePawn?.MapHeld == null)
            {
                return false;
            }

            if (!resolved)
            {
                cachedDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                resolved = true;
            }

            if (cachedDef == null)
            {
                return false;
            }

            Thing drop = ThingMaker.MakeThing(cachedDef);
            drop.stackCount = 1;
            GenPlace.TryPlaceThing(drop, sourcePawn.PositionHeld, sourcePawn.MapHeld, ThingPlaceMode.Near);
            if (sourcePawn.Spawned && !textKey.NullOrEmpty())
            {
                string text = textKey.Translate(drop.LabelCapNoCount).Resolve();
                TryThrowText(sourcePawn.MapHeld, sourcePawn.DrawPos, text, 3f);
                Messages.Message(text, sourcePawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
            return true;
        }

        private static void TryApplyRandomRatEggMissingParts(Pawn pawn)
        {
            if (!IsMouseEggBaby(pawn) || pawn.health?.hediffSet == null || !Rand.Chance(0.2f))
            {
                return;
            }

            int missingCount = 1;
            if (Rand.Chance(0.08f))
            {
                missingCount++;
            }

            if (missingCount > 1 && Rand.Chance(0.03f))
            {
                missingCount++;
            }

            HashSet<string> usedPartKinds = new HashSet<string>();
            int attempts = 0;
            while (usedPartKinds.Count < missingCount && attempts < 16)
            {
                attempts++;
                string partKind = RandomMissingPartPool.RandomElement();
                if (partKind == null || usedPartKinds.Contains(partKind))
                {
                    continue;
                }

                if (TryRemoveSinglePartByKind(pawn, partKind))
                {
                    usedPartKinds.Add(partKind);
                }
            }
        }

        private static bool TryRemoveSinglePartByKind(Pawn pawn, string partKind)
        {
            if (pawn?.health?.hediffSet == null || partKind.NullOrEmpty())
            {
                return false;
            }

            BodyPartRecord part = null;
            switch (partKind)
            {
                case EarPartDefName:
                    part = FindRemovablePart(pawn, EarPartDefName, null);
                    break;
                case RatTailPartDefName:
                    part = FindRemovablePart(pawn, RatTailPartDefName, null) ?? FindRemovablePart(pawn, "Tail", null);
                    break;
                case FingerPartDefName:
                    part = FindRemovablePart(pawn, FingerPartDefName, null);
                    break;
                case ToePartDefName:
                    part = FindRemovablePart(pawn, ToePartDefName, null);
                    break;
            }

            if (part == null)
            {
                return false;
            }

            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            return true;
        }

        private static BodyPartRecord FindRemovablePart(Pawn pawn, string defName, string labelContains)
        {
            if (pawn?.health?.hediffSet == null || defName.NullOrEmpty())
            {
                return null;
            }

            IEnumerable<BodyPartRecord> candidates = pawn.health.hediffSet.GetNotMissingParts()
                .Where(part => part.def?.defName == defName &&
                               !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part) &&
                               (labelContains == null || part.Label.ToLowerInvariant().Contains(labelContains.ToLowerInvariant())));
            if (!candidates.Any())
            {
                return null;
            }

            return candidates.RandomElement();
        }

        public static int RecordWallGnaw(Pawn pawn)
        {
            if (pawn == null)
            {
                return 0;
            }

            int count = WallGnawCounts.TryGetValue(pawn.thingIDNumber, out int current) ? current + 1 : 1;
            WallGnawCounts[pawn.thingIDNumber] = count;
            return count;
        }

        public static void TrySpawnSleepPoopFilth(Map map)
        {
            if (map == null || MouseDisasterDefOf.Filth_MouseDisasterPoop == null)
            {
                return;
            }

            List<Pawn> pawns = GetCachedMouseDisasterPawns(map);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || pawn.Awake() || pawn.CurJobDef != JobDefOf.LayDown)
                {
                    continue;
                }

                if (Rand.Chance(SleepPoopSpawnChance))
                {
                    FilthMaker.TryMakeFilth(pawn.PositionHeld, map, MouseDisasterDefOf.Filth_MouseDisasterPoop, 1, FilthSourceFlags.Pawn);
                }
            }
        }

        public static Thing FindGnawableTree(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                9999f,
                thing => thing is Plant plant && plant.def.plant != null && plant.def.plant.IsTree && pawn.CanReserve(thing));
        }

        public static Thing FindGnawableWall(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                9999f,
                thing => thing.def.building != null && !thing.def.building.isNaturalRock && thing.def.Fillage == FillCategory.Full && thing.def.passability == Traversability.Impassable && !thing.def.IsDoor && !thing.def.IsFrame && pawn.CanReserve(thing));
        }

        public static void AddNutrition(Pawn pawn, float percentage)
        {
            if (pawn?.needs?.food == null)
            {
                return;
            }

            pawn.needs.food.CurLevelPercentage = Mathf.Clamp01(pawn.needs.food.CurLevelPercentage + percentage);
        }

        public static bool TryApplyPrisonerScavengePoison(Pawn pawn)
        {
            if (pawn?.health == null)
            {
                return false;
            }

            float amount = MouseDisasterMod.Settings?.GetPrisonerScavengeToxicBuildupPerEat() ?? MouseDisasterSettings.ScavengeToxicBuildupNormal;
            if (amount <= 0f)
            {
                return false;
            }

            Hediff toxic = pawn.health.GetOrAddHediff(HediffDefOf.ToxicBuildup);
            if (toxic == null)
            {
                return false;
            }

            toxic.Severity += amount;
            return true;
        }

        public static void ApplyPrisonerScavengeMood(Pawn pawn, ThoughtDef thoughtDef)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            ThoughtDef resolved = thoughtDef ?? MouseDisasterDefOf.MouseDisaster_ScavengedDirtyFilth;
            if (resolved == null)
            {
                return;
            }

            pawn.needs.mood.thoughts.memories.TryGainMemory(resolved);
        }

        public static void RemoveBodyPartByName(Pawn pawn, string defName, string labelContains = null)
        {
            if (pawn == null)
            {
                return;
            }

            BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(record => record.def.defName == defName && (labelContains == null || record.Label.ToLowerInvariant().Contains(labelContains.ToLowerInvariant())));

            if (part == null)
            {
                part = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(record => record.def.defName == defName);
            }

            if (part != null)
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            }
        }

        public static List<BodyPartRecord> GetNaturalEars(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return new List<BodyPartRecord>();
            }

            return pawn.health.hediffSet.GetNotMissingParts()
                .Where(part => part.def?.defName == "Ear" && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part))
                .ToList();
        }

        public static BodyPartRecord GetRandomNaturalEar(Pawn pawn)
        {
            return GetNaturalEars(pawn).RandomElementWithFallback();
        }

        public static BodyPartRecord GetPreferredNaturalEar(Pawn pawn)
        {
            List<BodyPartRecord> ears = GetNaturalEars(pawn);
            if (ears.Count == 0)
            {
                return null;
            }

            BodyPartRecord leftEar = ears.FirstOrDefault(record =>
                !record.Label.NullOrEmpty() &&
                (record.Label.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 record.Label.IndexOf("\u5de6", StringComparison.OrdinalIgnoreCase) >= 0));
            return leftEar ?? ears[0];
        }

        public static BodyPartRecord GetNaturalRatTail(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            return pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(part =>
                part.def?.defName == RatTailPartDefName && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part));
        }
    }
}
