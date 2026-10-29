using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Toddlers;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    // Keep the base algorithm and other mods' patches for its usual (<95%) path.
    // Only supply the full-bar path that the old literal-rewrite transpiler unlocked.
    [HarmonyPatch]
    internal static class Patch_ToddlerPlaySelection
    {
        private static readonly List<ToddlerPlayDef> Candidates = new List<ToddlerPlayDef>();
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlay"), "TryGiveJob");

        private static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!PlayScheduleUtility.CanPlayNow(pawn) || pawn.needs?.play == null)
            {
                __result = null;
                return false;
            }
            if (pawn.needs.play.CurLevelPercentage < 0.95f)
                return true;

            Candidates.Clear();
            Candidates.AddRange(DefDatabase<ToddlerPlayDef>.AllDefsListForReading);
            for (int i = Candidates.Count - 1; i > 0; i--)
            {
                int j = Rand.Range(0, i + 1);
                ToddlerPlayDef swap = Candidates[i];
                Candidates[i] = Candidates[j];
                Candidates[j] = swap;
            }
            for (int i = 0; i < Candidates.Count; i++)
            {
                ToddlerPlayGiver worker = Candidates[i].Worker;
                if (!worker.CanDo(pawn)) continue;
                __result = worker.TryGiveJob(pawn);
                if (__result != null) break;
            }
            Candidates.Clear();
            return false;
        }
    }

    [HarmonyPatch]
    internal static class Patch_ToddlerCribPlaySelection
    {
        private static Func<Pawn, Job> televisionFromCrib;
        private static Func<Pawn, Job> skydreamingFromCrib;

        private static Func<Pawn, Job> BindCribJob(ToddlerPlayGiver worker)
        {
            MethodInfo method = AccessTools.Method(worker.GetType(), "TryGiveJobFromCrib", new[] { typeof(Pawn) });
            return method != null ? (Func<Pawn, Job>)Delegate.CreateDelegate(typeof(Func<Pawn, Job>), worker, method)
                : worker.TryGiveJob;
        }

        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlayInCrib"), "TryGiveJob");

        private static bool Prefix(Pawn pawn, ref Job __result, out bool __state)
        {
            // Null from the original can mean disqualified, NOT "try a fallback".
            __state = TPE_PlayUtility.CanStartInCribPlay(pawn) && pawn.needs?.play != null;
            if (!__state)
            {
                __result = null;
                return false;
            }
            if (pawn.needs.play.CurLevelPercentage < 0.95f)
                return true;

            ToddlerPlayGiver television = Toddlers_DefOf.ToddlerWatchTelevision.Worker;
            if (television.CanDoFromCrib(pawn))
                __result = (televisionFromCrib ??= BindCribJob(television))(pawn);
            if (__result == null)
            {
                ToddlerPlayGiver skydreaming = Toddlers_DefOf.ToddlerSkydreaming.Worker;
                if (skydreaming.CanDoFromCrib(pawn))
                    __result = (skydreamingFromCrib ??= BindCribJob(skydreaming))(pawn);
            }
            return false;
        }

        private static void Postfix(Pawn pawn, ref Job __result, bool __state)
        {
            if (!__state || __result != null) return;
            // Randomize the starting option so one always-available activity cannot
            // starve the other fallbacks. Workers retain feature/cooldown checks.
            int first = Rand.Range(0, 4);
            for (int i = 0; i < 4 && __result == null; i++)
            {
                ToddlerPlayDef def = ((first + i) % 4) switch
                {
                    0 => TPE_ToddlerPlayDefOf.TPE_ToddlerSelfSoothing,
                    1 => TPE_ToddlerPlayDefOf.TPE_ToddlerCribJumping,
                    2 => TPE_ToddlerPlayDefOf.TPE_ToddlerCeilingWatching,
                    _ => TPE_ToddlerPlayDefOf.TPE_ToddlerSpeechPracticeInCrib
                };
                __result = TryFallback(def, pawn);
            }
        }

        private static Job TryFallback(ToddlerPlayDef def, Pawn pawn)
        {
            if (def == null || pawn.CurJobDef == def.jobDef || !def.Worker.CanDoFromCrib(pawn))
                return null;
            return def.Worker is ToddlerPlayGiver_CeilingWatching ceiling
                ? ceiling.TryGiveJobFromCrib(pawn) : def.Worker.TryGiveJob(pawn);
        }
    }

    [HarmonyPatch]
    internal static class Patch_ToddlerPlayEligibility
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ToddlerPlayGiver), nameof(ToddlerPlayGiver.CanDo));
            yield return AccessTools.Method(typeof(ToddlerPlayGiver), nameof(ToddlerPlayGiver.CanDoFromCrib));
        }

        private static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                __result = false;
                return false;
            }
            if (pawn.needs?.play == null || pawn.needs.play.CurLevel < 0.9f)
                return true;

            // Preserve the base predicates, changing only its fullness cap.
            // The job selectors already evaluated schedule/rest once before workers.
            __result = ToddlerUtility.IsToddler(pawn) && !pawn.Downed
                && !PawnUtility.WillSoonHaveBasicNeed(pawn, 0f);
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.DriverTick))]
    internal static class Patch_PlayJobPriorityChanges
    {
        private static bool Prefix(JobDriver __instance)
        {
            if (!PlayScheduleUtility.IsPlayJob(__instance.job)) return true;
            return PlayScheduleUtility.ContinuePlaying(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    internal static class Patch_Pawn_Destroy_PlayTracker
    {
        private static void Postfix(Pawn __instance)
        {
            Current.Game?.GetComponent<GameComponent_PlayTracker>()?.RemoveTracker(__instance);
        }
    }
}
