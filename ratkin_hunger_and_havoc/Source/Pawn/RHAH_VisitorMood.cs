using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_VisitorMood
    {
        internal static void NotifySheltered(Verse.Pawn pawn)
        {
            Gain(pawn, "RHAH_Thought_Sheltered");
        }

        internal static void NotifyCaptured(Verse.Pawn pawn)
        {
            Gain(pawn, "RHAH_Thought_Captured");
        }

        internal static void NotifyEnslaved(Verse.Pawn pawn)
        {
            MemoryThoughtHandler memories = Memories(pawn);
            if (memories == null) return;
            ThoughtDef fed = DefDatabase<ThoughtDef>.GetNamed("RHAH_Thought_EnslavedFed");
            ThoughtDef serving = DefDatabase<ThoughtDef>.GetNamed("RHAH_Thought_EnslavedServing");
            memories.RemoveMemoriesOfDef(fed);
            memories.RemoveMemoriesOfDef(serving);
            memories.TryGainMemory(Rand.Value < 0.5f ? fed : serving);
        }

        internal static void NotifyExpelled(Verse.Pawn pawn, bool hostile)
        {
            Gain(pawn, "RHAH_Thought_Expelled", hostile ? 1 : 0);
        }

        internal static void NotifyAte(Verse.Pawn pawn, Thing food, float nutritionIngested)
        {
            if (!(nutritionIngested > 0f) || food?.def?.ingestible == null || food.def.IsDrug ||
                !(food.def.ingestible.CachedNutrition > 0f)) return;
            MemoryThoughtHandler memories = Memories(pawn);
            if (memories == null) return;
            float threshold = ThingDefOf.MealFine.ingestible.CachedNutrition;
            int stage = food.def.ingestible.CachedNutrition >= threshold ? 1 : 0;
            memories.TryGainMemoryFast(DefDatabase<ThoughtDef>.GetNamed("RHAH_Thought_AteFood"), stage);
        }

        static void Gain(Verse.Pawn pawn, string name, int stage = 0)
        {
            MemoryThoughtHandler memories = Memories(pawn);
            if (memories != null) memories.TryGainMemoryFast(DefDatabase<ThoughtDef>.GetNamed(name), stage);
        }

        static MemoryThoughtHandler Memories(Verse.Pawn pawn)
        {
            if ((RHAH_Mod.Settings != null && !RHAH_Mod.Settings.famineMoodEnabled) ||
                CompRHAH_Pawn.TryGet(pawn) == null || !RHAH_Race.IsRatkin(pawn.def)) return null;
            return pawn.needs?.mood?.thoughts?.memories;
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    internal static class RHAH_CustodyMoodPatch
    {
        static void Prefix(Verse.Pawn ___pawn, out int __state)
        {
            __state = (___pawn.IsPrisonerOfColony ? 1 : 0) | (___pawn.IsSlaveOfColony ? 2 : 0);
        }

        static void Postfix(Verse.Pawn ___pawn, int __state)
        {
            if (___pawn.IsPrisonerOfColony && (__state & 1) == 0) RHAH_VisitorMood.NotifyCaptured(___pawn);
            if (___pawn.IsSlaveOfColony && (__state & 2) == 0) RHAH_VisitorMood.NotifyEnslaved(___pawn);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class RHAH_FoodMoodPatch
    {
        static void Postfix(Thing __instance, Verse.Pawn ingester, float __result)
        {
            RHAH_VisitorMood.NotifyAte(ingester, __instance, __result);
        }
    }

    // 原版读档会清除无关联伤病的 permanent 标记，永久记忆直接禁用过期判定。
    public sealed class Thought_Memory_RHAH_EnslavedServing : Thought_Memory_RHAH_FamineFed
    {
        public override bool ShouldDiscard => false;
    }
}
