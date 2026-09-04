using HarmonyLib;
using SaveYourStorageAndTickEveryMinute.Compatibility;
using UnityEngine;
using Verse;

namespace SaveYourStorageAndTickEveryMinute
{
    public sealed class SaveYourStorageMod : Mod
    {
        public const string HarmonyId = "Nanaloveyuki.SaveYourStorageAndTickEveryMinute";

        public static SaveYourStorageSettings Settings { get; private set; }

        public SaveYourStorageMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SaveYourStorageSettings>();
            Settings.Normalize();

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            OuterrealmTechAdapter.TryApply(harmony);
            InspectStringCompatibility.TryApply(harmony);
            Log.Message("[SaveYourStorage] Performance patches loaded.");
        }

        public override string SettingsCategory()
        {
            return "SaveYourStorage_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "SaveYourStorage_ListerIndex".Translate(),
                ref Settings.listerMembershipIndex,
                "SaveYourStorage_ListerIndexTip".Translate());
            listing.CheckboxLabeled(
                "SaveYourStorage_DenseCellLayoutCache".Translate(),
                ref Settings.denseCellLayoutCache,
                "SaveYourStorage_DenseCellLayoutCacheTip".Translate());
            listing.CheckboxLabeled(
                "SaveYourStorage_SinglePassStorageCheck".Translate(),
                ref Settings.singlePassStorageCheck,
                "SaveYourStorage_SinglePassStorageCheckTip".Translate());
            listing.CheckboxLabeled(
                "SaveYourStorage_MergeCandidateBuckets".Translate(),
                ref Settings.mergeCandidateBuckets,
                "SaveYourStorage_MergeCandidateBucketsTip".Translate());
            listing.Label("SaveYourStorage_DenseCellMinimumItems".Translate(
                Settings.denseCellMinimumItems));
            Settings.denseCellMinimumItems = (int)listing.Slider(
                Settings.denseCellMinimumItems, 6f, 64f);
            listing.CheckboxLabeled(
                "SaveYourStorage_OuterrealmThrottle".Translate(),
                ref Settings.outerrealmLeaseThrottle,
                "SaveYourStorage_OuterrealmThrottleTip".Translate());
            listing.Label("SaveYourStorage_OuterrealmItemsPerTick".Translate(
                Settings.outerrealmMaximumRehomesPerTick));
            Settings.outerrealmMaximumRehomesPerTick = (int)listing.Slider(
                Settings.outerrealmMaximumRehomesPerTick, 1f, 64f);
            listing.Label("SaveYourStorage_OuterrealmTimeBudget".Translate(
                Settings.outerrealmMaximumMilliseconds.ToString("0.00")));
            Settings.outerrealmMaximumMilliseconds = listing.Slider(
                Settings.outerrealmMaximumMilliseconds, 0.05f, 5f);
            listing.End();
            Settings.Normalize();
        }
    }
}
