using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraFlightJobFix
{
    public sealed class MiliraFlightJobFixMod : Mod
    {
        internal const string HarmonyId = "nanaloveyuki.miliraflightjobfix";

        public MiliraFlightJobFixMod(ModContentPack content) : base(content)
        {
            MiliraPatch.Install();
            DraftAnythingCompatibility.Install();
            DraftAnythingTrackerCleanup.Install();
        }
    }

    internal static class MiliraPatch
    {
        private const string PatchTypeName = "Milira.MilianPatch_Pawn_FlightTracker_Notify_JobStarted";

        internal static void Install()
        {
            try
            {
                var harmony = new Harmony(MiliraFlightJobFixMod.HarmonyId);
                Type patchType = AccessTools.TypeByName(PatchTypeName);
                var miliraPrefix = patchType == null
                    ? null
                    : AccessTools.Method(patchType, "Prefix");
                if (miliraPrefix == null)
                {
                    Log.Warning("[MiliraFlightJobFix] Milira's Notify_JobStarted prefix was not found; fix disabled.");
                    return;
                }

                var notifyJobStarted = AccessTools.Method(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.Notify_JobStarted));
                if (notifyJobStarted == null)
                {
                    Log.Warning("[MiliraFlightJobFix] Pawn_FlightTracker.Notify_JobStarted was not found; fix disabled.");
                    return;
                }

                bool removed = false;
                var patches = Harmony.GetPatchInfo(notifyJobStarted);
                if (patches != null)
                {
                    foreach (Patch installed in patches.Prefixes.Where(p => p.PatchMethod == miliraPrefix).ToArray())
                    {
                        harmony.Unpatch(notifyJobStarted, installed.PatchMethod);
                        removed = true;
                    }
                }

                harmony.Patch(
                    miliraPrefix,
                    prefix: new HarmonyMethod(typeof(MiliraPatch), nameof(ReturnToOriginalFlow)));

                Log.Message(removed
                    ? "[MiliraFlightJobFix] Removed Milira's flight-job override; vanilla and remaining compatibility patches handle jobs."
                    : "[MiliraFlightJobFix] Guarded Milira's flight-job override; vanilla and remaining compatibility patches handle jobs.");
            }
            catch (Exception exception)
            {
                Log.Error("[MiliraFlightJobFix] Could not disable Milira's Notify_JobStarted prefix: " + exception);
            }
        }

        private static bool ReturnToOriginalFlow(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    internal static class DraftAnythingCompatibility
    {
        private const string HarmonyId = MiliraFlightJobFixMod.HarmonyId + ".draftanything";
        private const string StartupTypeName = "DraftAnything.Startup";
        private const string ThinkNodeTypeName = "DraftAnything.ThinkNode_IsControllable";

        internal static void Install()
        {
            try
            {
                RemoveInjectedNodes();

                Type startupType = AccessTools.TypeByName(StartupTypeName);
                MethodInfo patchMethod = startupType == null
                    ? null
                    : AccessTools.Method(startupType, "PatchThinkTreeDefs");
                if (patchMethod == null)
                {
                    Log.Message("[MiliraFlightJobFix] Draft Anything not found; no ThinkTree compatibility cleanup needed.");
                    return;
                }

                new Harmony(HarmonyId).Patch(
                    patchMethod,
                    postfix: new HarmonyMethod(typeof(DraftAnythingCompatibility), nameof(RemoveInjectedNodes)));
                Log.Message("[MiliraFlightJobFix] Draft Anything compatibility cleanup installed for Milira constant trees.");
            }
            catch (Exception exception)
            {
                Log.Error("[MiliraFlightJobFix] Could not install Draft Anything compatibility cleanup: " + exception);
            }
        }

        private static void RemoveInjectedNodes()
        {
            Type injectedNodeType = AccessTools.TypeByName(ThinkNodeTypeName);
            if (injectedNodeType == null)
            {
                return;
            }

            ThinkTreeDef thinkTree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("MiliraConstant");
            if (thinkTree?.thinkRoot?.subNodes == null)
            {
                return;
            }

            int removed = thinkTree.thinkRoot.subNodes.RemoveAll(
                node => node != null && node.GetType() == injectedNodeType);
            if (removed > 0)
            {
                Log.Message("[MiliraFlightJobFix] Removed Draft Anything's drafted-order branch from " +
                    "MiliraConstant; the humanlike main tree still handles player orders.");
            }
        }
    }
}
