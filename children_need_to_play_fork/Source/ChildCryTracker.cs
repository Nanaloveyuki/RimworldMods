using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    public static class ChildCryTracker
    {
        public static bool IsCryingEligibleChild(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed
                || pawn.DevelopmentalStage != DevelopmentalStage.Child || pawn.needs?.mood == null)
                return false;

            int age = pawn.ageTracker.AgeBiologicalYears;
            if (age >= 3 && age <= 6)
                return TPE_Mod.Settings?.enableChildCrying == true;
            return age >= 7 && age <= 13 && TPE_Mod.Settings?.enableOlderChildCrying == true;
        }

        public static bool IsOlderChild(Pawn pawn)
        {
            return pawn.DevelopmentalStage == DevelopmentalStage.Child
                && pawn.ageTracker.AgeBiologicalYears >= 7;
        }

        public static void TickFirstCryCap(Pawn pawn)
        {
            if (IsCryingEligibleChild(pawn))
                Current.Game.GetComponent<GameComponent_ChildCryTracker>().TickChild(pawn);
        }

        public static void TryTriggerTantrum(Pawn pawn, float moodEffect, string? thoughtDefName = null)
        {
            if (moodEffect <= -12f && IsCryingEligibleChild(pawn))
                Current.Game.GetComponent<GameComponent_ChildCryTracker>().TryTriggerTantrum(
                    pawn, moodEffect, thoughtDefName == null ? null : DefDatabase<ThoughtDef>.GetNamed(thoughtDefName));
        }

        public static void OnCryStarted(Pawn pawn)
        {
            Current.Game.GetComponent<GameComponent_ChildCryTracker>().OnCryStarted(pawn);
        }
    }

    // Runtime episode state belongs to a game, never a static dictionary shared across saves.
    public sealed class GameComponent_ChildCryTracker : GameComponent
    {
        private sealed class EpisodeData
        {
            public int firstEligibleTick = -1;
            public bool hasCried;
            public int lastTantrumTick = -2500;
            public int nextThoughtScanTick;
            public readonly List<ThoughtDef> reactedThoughts = new List<ThoughtDef>();
        }

        private const int FirstCryTimeoutTicks = 2500;
        private const int TantrumCooldownTicks = 2500;
        private const int ThoughtScanIntervalTicks = 600;
        private const int CleanupIntervalTicks = 15000;
        private readonly Dictionary<Pawn, EpisodeData> episodes = new Dictionary<Pawn, EpisodeData>();
        private readonly List<Pawn> expiredPawns = new List<Pawn>();
        private readonly List<Thought> thoughtBuffer = new List<Thought>();
        private readonly HashSet<ThoughtDef> severeThoughtBuffer = new HashSet<ThoughtDef>();

        public GameComponent_ChildCryTracker(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CleanupIntervalTicks != 0)
                return;

            foreach (KeyValuePair<Pawn, EpisodeData> entry in episodes)
            {
                if (!ChildCryTracker.IsCryingEligibleChild(entry.Key))
                    expiredPawns.Add(entry.Key);
            }
            for (int i = 0; i < expiredPawns.Count; i++)
                episodes.Remove(expiredPawns[i]);
            expiredPawns.Clear();
        }

        public void TickChild(Pawn pawn)
        {
            EpisodeData data = GetEpisode(pawn);
            int now = Find.TickManager.TicksGame;
            if (now >= data.nextThoughtScanTick)
            {
                data.nextThoughtScanTick = now + ThoughtScanIntervalTicks;
                CheckSevereSituationalThoughts(pawn, data, now);
            }

            if (pawn.needs.mood.CurLevelPercentage >= 0.30f)
            {
                data.firstEligibleTick = -1;
                data.hasCried = false;
                return;
            }
            if (data.hasCried)
                return;
            if (pawn.MentalState is MentalState_ChildCry)
            {
                data.hasCried = true;
                return;
            }
            if (data.firstEligibleTick < 0)
                data.firstEligibleTick = now;
            if (now - data.firstEligibleTick >= FirstCryTimeoutTicks)
            {
                data.hasCried = pawn.mindState.mentalStateHandler.TryStartMentalState(
                    TPE_MentalStateDefOf.TPE_ChildCrying,
                    reason: "TPE_CryReason_LowMood".Translate(), causedByMood: true);
            }
        }

        public void TryTriggerTantrum(Pawn pawn, float moodEffect, ThoughtDef? thoughtDef)
        {
            EpisodeData data = GetEpisode(pawn);
            int now = Find.TickManager.TicksGame;
            if (pawn.MentalState is MentalState_ChildCry
                || now - data.lastTantrumTick < TantrumCooldownTicks)
                return;

            float chance = moodEffect >= -14f ? 0.20f
                : moodEffect >= -19f ? 0.35f
                : moodEffect >= -24f ? 0.50f : 0.70f;
            if (!Rand.Chance(chance))
                return;
            if (!pawn.mindState.mentalStateHandler.TryStartMentalState(
                TPE_MentalStateDefOf.TPE_ChildTantrum,
                reason: "TPE_CryReason_SevereThought".Translate(), causedByMood: true))
                return;

            data.lastTantrumTick = now;
            data.hasCried = true;
            if (thoughtDef != null && !data.reactedThoughts.Contains(thoughtDef))
                data.reactedThoughts.Add(thoughtDef);
        }

        public void OnCryStarted(Pawn pawn)
        {
            GetEpisode(pawn).hasCried = true;
        }

        private EpisodeData GetEpisode(Pawn pawn)
        {
            if (!episodes.TryGetValue(pawn, out EpisodeData data))
            {
                data = new EpisodeData();
                episodes.Add(pawn, data);
            }
            return data;
        }

        private void CheckSevereSituationalThoughts(Pawn pawn, EpisodeData data, int now)
        {
            if (pawn.MentalState is MentalState_ChildCry
                || now - data.lastTantrumTick < TantrumCooldownTicks)
                return;

            pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughtBuffer);
            severeThoughtBuffer.Clear();
            Thought? trigger = null;
            for (int i = 0; i < thoughtBuffer.Count; i++)
            {
                Thought thought = thoughtBuffer[i];
                if (thought.def == null || thought.MoodOffset() > -12f)
                    continue;
                severeThoughtBuffer.Add(thought.def);
                if (trigger == null && !(thought is Thought_Memory)
                    && !data.reactedThoughts.Contains(thought.def))
                    trigger = thought;
            }
            for (int i = data.reactedThoughts.Count - 1; i >= 0; i--)
            {
                if (!severeThoughtBuffer.Contains(data.reactedThoughts[i]))
                    data.reactedThoughts.RemoveAt(i);
            }

            // Clear shared scratch state before a mental-state start can cause nested memory receipt.
            ThoughtDef? triggerDef = trigger?.def;
            float moodEffect = trigger?.MoodOffset() ?? 0f;
            thoughtBuffer.Clear();
            severeThoughtBuffer.Clear();
            if (triggerDef != null)
                TryTriggerTantrum(pawn, moodEffect, triggerDef);
        }
    }
}
