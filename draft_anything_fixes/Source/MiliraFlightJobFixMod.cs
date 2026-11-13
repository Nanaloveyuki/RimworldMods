using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MiliraFlightJobFix
{
    public sealed class MiliraFlightJobFixMod : Mod
    {
        internal const string HarmonyId = "nanaloveyuki.miliraflightjobfix";

        public MiliraFlightJobFixMod(ModContentPack content) : base(content)
        {
            DraftAnythingCompatibility.Install();
            DraftAnythingTrackerCleanup.Install();
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
