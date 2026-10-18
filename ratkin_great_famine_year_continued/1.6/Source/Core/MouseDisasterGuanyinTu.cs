using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class CompProperties_GuanyinTu : CompProperties
    {
        public CompProperties_GuanyinTu()
        {
            compClass = typeof(CompGuanyinTu);
        }
    }

    public class CompGuanyinTu : ThingComp
    {
        public override void PostIngested(Pawn ingester)
        {
            MouseDisasterGuanyinTuUtility.NotifyIngested(ingester);
        }
    }

    public static class MouseDisasterGuanyinTuUtility
    {
        public const int MaxIngestionsPerWindow = 3;
        public static readonly int IngestionWindowTicks = GenDate.TicksPerDay * 15;

        public static bool CanIngest(Pawn pawn)
        {
            Hediff_GuanyinTuSatiety satiety = GetSatiety(pawn);
            return satiety == null || satiety.CanConsumeNow;
        }

        public static Hediff_GuanyinTuSatiety GetSatiety(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety == null)
            {
                return null;
            }

            return pawn.health.hediffSet.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety) as Hediff_GuanyinTuSatiety;
        }

        public static void NotifyIngested(Pawn pawn)
        {
            if (pawn?.health == null || MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety == null)
            {
                return;
            }

            Hediff_GuanyinTuSatiety satiety = GetSatiety(pawn);
            if (satiety == null)
            {
                satiety = HediffMaker.MakeHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety, pawn) as Hediff_GuanyinTuSatiety;
                if (satiety == null)
                {
                    return;
                }

                pawn.health.AddHediff(satiety);
                satiety.RegisterIngestion(fresh: true);
                return;
            }

            satiety.RegisterIngestion(fresh: false);
        }

        public static void ApplyN004StartingState(Pawn mother, System.Collections.Generic.IEnumerable<Pawn> children)
        {
            SeedSatiety(mother, 0.8f);
            GiveStartingSupplies(mother);

            if (children == null)
            {
                return;
            }

            foreach (Pawn child in children)
            {
                SeedSatiety(child, 0.5f);
                ApplyTemporaryAnesthetic(child);
            }
        }

        private static void SeedSatiety(Pawn pawn, float severity)
        {
            if (pawn?.health == null || MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety == null)
            {
                return;
            }

            Hediff_GuanyinTuSatiety satiety = GetSatiety(pawn);
            if (satiety == null)
            {
                satiety = HediffMaker.MakeHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety, pawn) as Hediff_GuanyinTuSatiety;
                if (satiety == null)
                {
                    return;
                }

                pawn.health.AddHediff(satiety);
            }

            int uses = Mathf.Clamp(Mathf.CeilToInt(severity / 0.33f), 1, MaxIngestionsPerWindow);
            satiety.SeedStoryState(severity, uses);
        }

        private static void ApplyTemporaryAnesthetic(Pawn pawn)
        {
            if (pawn?.health == null || HediffDefOf.Anesthetic == null)
            {
                return;
            }

            Hediff anesthetic = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);
            if (anesthetic == null)
            {
                pawn.health.forceDowned = true;
                anesthetic = pawn.health.AddHediff(HediffDefOf.Anesthetic);
                pawn.health.forceDowned = false;
            }

            HediffComp_Disappears disappears = anesthetic?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                int duration = GenDate.TicksPerDay * 2;
                disappears.disappearsAfterTicks = Mathf.Max(disappears.disappearsAfterTicks, duration);
                disappears.ticksToDisappear = Mathf.Max(disappears.ticksToDisappear, duration);
            }
        }

        private static void GiveStartingSupplies(Pawn mother)
        {
            if (mother?.MapHeld == null || MouseDisasterDefOf.MouseDisaster_GuanyinTu == null)
            {
                return;
            }

            TryAddToInventoryOrPlace(mother, MouseDisasterDefOf.MouseDisaster_GuanyinTu, Rand.RangeInclusive(1, 2));
            TryAddToInventoryOrPlace(mother, ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(1, 3));
            if (Rand.Chance(0.5f))
            {
                TryAddToInventoryOrPlace(mother, ThingDefOf.ComponentSpacer, 1);
            }

            TryAddToInventoryOrPlace(mother, ThingDefOf.Silver, Rand.RangeInclusive(5, 20));
        }

        private static void TryAddToInventoryOrPlace(Pawn pawn, ThingDef def, int count)
        {
            if (pawn == null || def == null || count <= 0)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = Mathf.Min(count, def.stackLimit);
            if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
            {
                if (pawn.Spawned && pawn.Map != null)
                {
                    GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
                else
                {
                    thing.Destroy();
                }
            }
        }
    }

    public class Hediff_GuanyinTuSatiety : HediffWithComps
    {
        private const float SeverityPerIngestion = 0.33f;
        private const float SeverityDecayPerDay = 0.1f;
        private const float MinimumStoredSeverity = 0.001f;
        private const float NormalTreatment = 0.15f;
        private const float GlitterworldTreatment = 0.5f;
        private const float MechSerumTreatment = 1f;

        private int windowStartTick = -1;
        private int ingestionCount;
        private int nextTreatmentTick;
        private int lastTendedTick = -1;
        private float lastTendQuality;
        private float lastTendMaxQuality;

        public bool CanConsumeNow
        {
            get
            {
                ResetWindowIfExpired();
                return ingestionCount < MouseDisasterGuanyinTuUtility.MaxIngestionsPerWindow;
            }
        }

        public int IngestionCount => ingestionCount;

        public int RemainingWindowDays
        {
            get
            {
                if (windowStartTick < 0)
                {
                    return 0;
                }

                int remainingTicks = Mathf.Max(0, windowStartTick + MouseDisasterGuanyinTuUtility.IngestionWindowTicks - CurrentTick);
                return Mathf.CeilToInt(remainingTicks / (float)GenDate.TicksPerDay);
            }
        }

        private int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public void RegisterIngestion(bool fresh)
        {
            ResetWindowIfExpired();
            if (!fresh && ingestionCount >= MouseDisasterGuanyinTuUtility.MaxIngestionsPerWindow)
            {
                return;
            }

            if (windowStartTick < 0)
            {
                windowStartTick = CurrentTick;
            }

            if (fresh)
            {
                ingestionCount = 1;
                Severity = Mathf.Max(Severity, SeverityPerIngestion);
                return;
            }

            ingestionCount++;
            Severity = Mathf.Clamp(Severity + SeverityPerIngestion, MinimumStoredSeverity, 1f);
        }

        public void SeedStoryState(float severity, int uses)
        {
            windowStartTick = CurrentTick;
            ingestionCount = Mathf.Clamp(uses, 1, MouseDisasterGuanyinTuUtility.MaxIngestionsPerWindow);
            Severity = Mathf.Clamp(severity, MinimumStoredSeverity, 1f);
        }

        public override bool TendableNow(bool ignoreTimer = false)
        {
            if (!base.TendableNow(ignoreTimer))
            {
                return false;
            }

            return ignoreTimer || CurrentTick >= nextTreatmentTick;
        }

        public override void Tended(float quality, float maxQuality, int batchPosition = 0)
        {
            lastTendedTick = CurrentTick;
            lastTendQuality = quality;
            lastTendMaxQuality = maxQuality;
        }

        public bool TryApplyTreatment(ThingDef medicineDef)
        {
            bool tendedThisTick = lastTendedTick == CurrentTick;
            lastTendedTick = -1;
            if (!tendedThisTick || medicineDef == null || CurrentTick < nextTreatmentTick)
            {
                return false;
            }

            float treatment = ResolveTreatmentAmount(medicineDef);
            if (lastTendMaxQuality > 0f)
            {
                treatment *= Mathf.Clamp01(lastTendQuality / lastTendMaxQuality);
            }

            nextTreatmentTick = CurrentTick + GenDate.TicksPerDay;
            Severity = Mathf.Max(MinimumStoredSeverity, Severity - treatment);
            return true;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (delta <= 0)
            {
                return;
            }

            Severity = Mathf.Max(MinimumStoredSeverity, Severity - SeverityDecayPerDay * delta / GenDate.TicksPerDay);
            ResetWindowIfExpired();
        }

        public override bool ShouldRemove
        {
            get
            {
                ResetWindowIfExpired();
                return Severity <= MinimumStoredSeverity && ingestionCount <= 0 && windowStartTick < 0;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                string status = "MouseDisaster_GuanyinTuSatiety_Status".Translate(
                    ingestionCount,
                    MouseDisasterGuanyinTuUtility.MaxIngestionsPerWindow,
                    RemainingWindowDays);
                return base.TipStringExtra + status;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref windowStartTick, "windowStartTick", -1);
            Scribe_Values.Look(ref ingestionCount, "ingestionCount", 0);
            Scribe_Values.Look(ref nextTreatmentTick, "nextTreatmentTick", 0);
            Scribe_Values.Look(ref lastTendedTick, "lastTendedTick", -1);
            Scribe_Values.Look(ref lastTendQuality, "lastTendQuality", 0f);
            Scribe_Values.Look(ref lastTendMaxQuality, "lastTendMaxQuality", 0f);
        }

        private void ResetWindowIfExpired()
        {
            if (windowStartTick >= 0 && CurrentTick >= windowStartTick + MouseDisasterGuanyinTuUtility.IngestionWindowTicks)
            {
                windowStartTick = -1;
                ingestionCount = 0;
            }
        }

        private static float ResolveTreatmentAmount(ThingDef medicineDef)
        {
            string defName = medicineDef.defName;
            if (defName == "MechSerumHealer" || defName == "MechSerum")
            {
                return MechSerumTreatment;
            }

            if (defName == "MedicineUltratech")
            {
                return GlitterworldTreatment;
            }

            return NormalTreatment;
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillEat), new[] { typeof(Pawn), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool) })]
    public static class MouseDisasterGuanyinTuWillEatThingPatch
    {
        public static void Postfix(Pawn p, Thing food, ref bool __result)
        {
            if (__result && food?.def == MouseDisasterDefOf.MouseDisaster_GuanyinTu && !MouseDisasterGuanyinTuUtility.CanIngest(p))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillEat), new[] { typeof(Pawn), typeof(ThingDef), typeof(Pawn), typeof(bool), typeof(bool) })]
    public static class MouseDisasterGuanyinTuWillEatDefPatch
    {
        public static void Postfix(Pawn p, ThingDef food, ref bool __result)
        {
            if (__result && food == MouseDisasterDefOf.MouseDisaster_GuanyinTu && !MouseDisasterGuanyinTuUtility.CanIngest(p))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.FoodIsSuitable), new[] { typeof(Pawn), typeof(ThingDef) })]
    public static class MouseDisasterGuanyinTuFoodSuitablePatch
    {
        public static void Postfix(Pawn p, ThingDef food, ref bool __result)
        {
            if (__result && food == MouseDisasterDefOf.MouseDisaster_GuanyinTu && !MouseDisasterGuanyinTuUtility.CanIngest(p))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculateCapacityLevel))]
    public static class MouseDisasterGuanyinTuMetabolismPatch
    {
        public static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            if (capacity?.defName != "Metabolism")
            {
                return;
            }

            Hediff_GuanyinTuSatiety satiety = MouseDisasterGuanyinTuUtility.GetSatiety(diffSet?.pawn);
            if (satiety != null)
            {
                __result = Mathf.Max(0f, __result - satiety.Severity / 2f);
            }
        }
    }

    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class MouseDisasterGuanyinTuTendPatch
    {
        public static void Postfix(Pawn patient, Medicine medicine)
        {
            MouseDisasterGuanyinTuUtility.GetSatiety(patient)?.TryApplyTreatment(medicine?.def);
        }
    }
}
