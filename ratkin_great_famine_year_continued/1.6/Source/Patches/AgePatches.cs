using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace MouseDisaster
{
    [HarmonyPatch]
    internal static class MouseDisasterInfantMobilityPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnCapacityWorker_Moving), nameof(PawnCapacityWorker_Moving.CalculateCapacityLevel));
            yield return AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned));
        }

        internal static bool UsesFallback(Pawn pawn) => !ModsConfig.IsActive("cyanobot.toddlers") &&
            pawn?.ageTracker != null && pawn.DevelopmentalStage == DevelopmentalStage.Baby &&
            MouseDisasterUtility.IsMouseDisasterPawn(pawn);

        public static bool KeepAgeImmobility(bool alwaysDowned, Pawn pawn) => alwaysDowned && !UsesFallback(pawn);

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            FieldInfo flag = AccessTools.Field(typeof(LifeStageDef), nameof(LifeStageDef.alwaysDowned));
            bool capacity = original.DeclaringType == typeof(PawnCapacityWorker_Moving);
            FieldInfo pawn = AccessTools.Field(capacity ? typeof(HediffSet) : typeof(Pawn_HealthTracker), "pawn");
            MethodInfo check = AccessTools.Method(typeof(MouseDisasterInfantMobilityPatch), nameof(KeepAgeImmobility));
            int matches = 0;
            // Remove only age-based immobility; native limb, pain and consciousness checks remain intact.
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!instruction.LoadsField(flag)) continue;
                matches++;
                yield return new CodeInstruction(capacity ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, pawn);
                yield return new CodeInstruction(OpCodes.Call, check);
            }
            if (matches != 1) throw new InvalidOperationException("MouseDisaster infant mobility contract changed: " + original);
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class AgePatches
    {
        private static readonly AccessTools.FieldRef<Pawn_AgeTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_AgeTracker, Pawn>("pawn");

        public static void Postfix(Pawn_AgeTracker __instance)
        {
            Pawn pawn = PawnField(__instance);
            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);

            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (settings == null || !settings.enableAgeCapAdjustment)
            {
                return;
            }

            if (!MouseDisasterUtility.IsMouseDisasterPawn(pawn))
            {
                return;
            }

            int age = __instance.AgeBiologicalYears;
            if (age < settings.maxRatkinAge)
            {
                return;
            }

            float effectiveAge = pawn.def.race.lifeExpectancy * (age / (float)settings.maxRatkinAge);
            int guaranteedPasses = Mathf.Max(1, Mathf.FloorToInt(settings.ageDiseaseMultiplier));
            float extraChance = Mathf.Clamp01(settings.ageDiseaseMultiplier - guaranteedPasses);

            for (int pass = 0; pass < guaranteedPasses; pass++)
            {
                ApplyBirthdayAilments(pawn, effectiveAge);
            }

            if (Rand.Chance(extraChance))
            {
                ApplyBirthdayAilments(pawn, effectiveAge);
            }
        }

        private static void ApplyBirthdayAilments(Pawn pawn, float effectiveAge)
        {
            foreach (HediffGiver_Birthday giver in AgeInjuryUtility.RandomHediffsToGainOnBirthday(pawn, effectiveAge))
            {
                giver.TryApply(pawn);
            }
        }
    }
}
