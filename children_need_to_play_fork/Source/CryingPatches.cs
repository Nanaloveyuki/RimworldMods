using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    [HarmonyPatch(typeof(MentalFitDef), nameof(MentalFitDef.CalculateMTBDays))]
    public static class Patch_MentalFitDef_CalculateMTBDays_ChildCrying
    {
        public static bool Prefix(MentalFitDef __instance, Pawn pawn, ref float __result)
        {
            if (__instance.mentalState?.stateClass != typeof(MentalState_ChildCry)
                || ChildCryTracker.IsCryingEligibleChild(pawn))
                return true;
            __result = float.PositiveInfinity;
            return false;
        }
    }

    [HarmonyPatch]
    public static class Patch_MentalState_BabyCry_AuraEffect_ChildImmunity
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MentalState_BabyCry), "AuraEffect",
                new[] { typeof(Thing), typeof(Pawn) });
        }

        public static bool Prefix(Pawn hearer)
        {
            return hearer?.ageTracker == null || hearer.ageTracker.AgeBiologicalYears > 6;
        }
    }

    [HarmonyPatch(typeof(MentalFitGenerator), nameof(MentalFitGenerator.TickInterval))]
    public static class Patch_MentalFitGenerator_TickInterval_FirstCryCap
    {
        public static void Postfix(Pawn ___pawn, int delta)
        {
            if (ChildCryTracker.IsCryingEligibleChild(___pawn) && ___pawn.IsHashIntervalTick(150, delta))
                ChildCryTracker.TickFirstCryCap(___pawn);
        }
    }

    [HarmonyPatch]
    public static class Patch_MemoryThoughtHandler_TryGainMemory_TantrumTrigger
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
                new[] { typeof(Thought_Memory), typeof(Pawn) });
        }

        public static bool Prefix(Thought_Memory newThought, Pawn otherPawn, Pawn ___pawn, out bool __state)
        {
            __state = false;
            if (!PrisonerCryMood.AllowsMemory(___pawn, otherPawn ?? newThought.otherPawn, newThought.def))
                return false;
            if (Scribe.mode != LoadSaveMode.Inactive || !ChildCryTracker.IsCryingEligibleChild(___pawn)
                || !ThoughtUtility.CanGetThought(___pawn, newThought.def))
                return true;
            if (newThought is Thought_MemorySocial)
            {
                Pawn target = otherPawn ?? newThought.otherPawn;
                if (target == null || !newThought.def.socialTargetDevelopmentalStageFilter.Has(target.DevelopmentalStage))
                    return true;
            }
            __state = true;
            return true;
        }

        public static void Postfix(Thought_Memory newThought, Pawn ___pawn, MemoryThoughtHandler __instance, bool __state)
        {
            if (!__state || newThought.pawn != ___pawn)
                return;
            float moodEffect = newThought.MoodOffset();
            if (moodEffect > -12f)
                return;

            // A void TryGainMemory can reject or merge; only an added/renewed stored memory counts.
            var memories = __instance.Memories;
            for (int i = 0; i < memories.Count; i++)
            {
                Thought_Memory memory = memories[i];
                if (memory == newThought || (memory.age == 0 && memory.GroupsWith(newThought)))
                {
                    ChildCryTracker.TryTriggerTantrum(___pawn, memory.MoodOffset(), memory.def.defName);
                    return;
                }
            }
        }
    }
}
