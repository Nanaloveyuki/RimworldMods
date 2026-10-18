using HarmonyLib;
using UnityEngine;
using Verse;

namespace OhYouAreSoFast
{
    public sealed class OhYouAreSoFastMod : Mod
    {
        public const string HarmonyId = "Nanaloveyuki.OhYouAreSoFast";

        public static OhYouAreSoFastSettings Settings { get; private set; }

        public OhYouAreSoFastMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<OhYouAreSoFastSettings>();
            Settings.Normalize();
            new Harmony(HarmonyId).PatchAll(typeof(OhYouAreSoFastMod).Assembly);
            Log.Message("[OhYouAreSoFast] Performance patches applied.");
            SaveLoadTrace.LogEnabledState();
        }

        public override string SettingsCategory()
        {
            return "OhYouAreSoFast_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "OhYouAreSoFast_SaveLoadTrace".Translate(),
                ref Settings.saveLoadTrace,
                "OhYouAreSoFast_SaveLoadTraceTip".Translate());
            listing.CheckboxLabeled(
                "OhYouAreSoFast_SaveLoadFullTrace".Translate(),
                ref Settings.saveLoadFullTrace,
                "OhYouAreSoFast_SaveLoadFullTraceTip".Translate());
            listing.CheckboxLabeled(
                "OhYouAreSoFast_AdaptivePathHeuristic".Translate(),
                ref Settings.adaptivePathHeuristic,
                "OhYouAreSoFast_AdaptivePathHeuristicTip".Translate());
            listing.CheckboxLabeled(
                "OhYouAreSoFast_WorldPathCorrectness".Translate(),
                ref Settings.worldPathCorrectness,
                "OhYouAreSoFast_WorldPathCorrectnessTip".Translate());
            listing.CheckboxLabeled(
                "OhYouAreSoFast_SparseDynamicPathData".Translate(),
                ref Settings.sparseDynamicPathData,
                "OhYouAreSoFast_SparseDynamicPathDataTip".Translate());
            listing.CheckboxLabeled(
                "OhYouAreSoFast_WorkScanThrottle".Translate(),
                ref Settings.workScanThrottle,
                "OhYouAreSoFast_WorkScanThrottleTip".Translate());
            listing.Label("OhYouAreSoFast_WorkScanRetryInterval".Translate(Settings.workScanIntervalSeconds));
            Settings.workScanIntervalSeconds = (int)listing.Slider(Settings.workScanIntervalSeconds, 1f, 60f);
            listing.CheckboxLabeled(
                "OhYouAreSoFast_WorkPriorityCache".Translate(),
                ref Settings.workPriorityCache,
                "OhYouAreSoFast_WorkPriorityCacheTip".Translate());
            listing.Label("OhYouAreSoFast_WorkPriorityRefreshInterval".Translate(Settings.workPriorityCacheIntervalSeconds));
            Settings.workPriorityCacheIntervalSeconds = (int)listing.Slider(Settings.workPriorityCacheIntervalSeconds, 1f, 60f);
            listing.Label("OhYouAreSoFast_MinimumMapSize".Translate(Settings.idleThinkMinMapCells));
            Settings.idleThinkMinMapCells = (int)listing.Slider(Settings.idleThinkMinMapCells, 0f, 200000f);
            listing.End();
            Settings.Normalize();
        }
    }
}
