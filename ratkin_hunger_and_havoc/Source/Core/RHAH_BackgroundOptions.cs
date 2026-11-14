using Verse;

namespace HungerAndHavoc.Core
{
    internal sealed class RHAH_BackgroundOptions : IExposable
    {
        public bool enabled = true;
        public float averageDays = 15f;
        public bool rebellionEnabled = true;
        public bool ruinsEnabled = true;
        public bool unrestEnabled = true;
        public bool mutinyEnabled = true;
        public float rebellionWeight = 1f;
        public float ruinsWeight = 1f;
        public float unrestWeight = 1f;
        public float mutinyWeight = 1f;
        public bool civilWarEnabled = true;
        public float civilWarChancePercent = 5f;
        public float civilWarDelayDays = 3f;
        public bool takeoverEnabled = true;
        public float takeoverChancePercent = 50f;
        public float takeoverDelayDays = 60f;
        public float unrestBonusPercent = 50f;
        public float unrestDays = 3f;
        public IntRange ruinsDistance = new IntRange(8, 20);
        public IntRange hardtack = new IntRange(5, 50);
        public IntRange silver = new IntRange(100, 1000);
        public IntRange components = new IntRange(1, 5);

        public bool twoTaxesEnabled = true;
        public bool sameCallsignEnabled = true;
        public bool harvestFestivalEnabled = true;
        public bool memorialEnabled = true;
        public bool aidManifestEnabled = true;
        public bool cancelledBanquetEnabled = true;
        public float twoTaxesWeight = 1f;
        public float sameCallsignWeight = 1f;
        public float harvestFestivalWeight = 1f;
        public float memorialWeight = 1f;
        public float aidManifestWeight = 1f;
        public float cancelledBanquetWeight = 1f;
        public float tradeDiscountPercent = 25f;
        public float woundedJoinChancePercent = 50f;
        public int evacuationGoodwill = 5;
        public int memorialSteel = 40;
        public int memorialWood = 80;
        public int restorationSteel = 20;
        public int restorationWood = 40;
        public int materialsGoodwill = 5;
        public int namesUndergroundGoodwill = 10;
        public int namesWarlordGoodwill = 3;
        public int restorationGoodwill = 8;
        public IntRange storyDistance = new IntRange(3, 8);

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref averageDays, "averageDays", 15f);
            Scribe_Values.Look(ref rebellionEnabled, "rebellionEnabled", true);
            Scribe_Values.Look(ref ruinsEnabled, "ruinsEnabled", true);
            Scribe_Values.Look(ref unrestEnabled, "unrestEnabled", true);
            Scribe_Values.Look(ref mutinyEnabled, "mutinyEnabled", true);
            Scribe_Values.Look(ref rebellionWeight, "rebellionWeight", 1f);
            Scribe_Values.Look(ref ruinsWeight, "ruinsWeight", 1f);
            Scribe_Values.Look(ref unrestWeight, "unrestWeight", 1f);
            Scribe_Values.Look(ref mutinyWeight, "mutinyWeight", 1f);
            Scribe_Values.Look(ref civilWarEnabled, "civilWarEnabled", true);
            Scribe_Values.Look(ref civilWarChancePercent, "civilWarChancePercent", 5f);
            Scribe_Values.Look(ref civilWarDelayDays, "civilWarDelayDays", 3f);
            Scribe_Values.Look(ref takeoverEnabled, "takeoverEnabled", true);
            Scribe_Values.Look(ref takeoverChancePercent, "takeoverChancePercent", 50f);
            Scribe_Values.Look(ref takeoverDelayDays, "takeoverDelayDays", 60f);
            Scribe_Values.Look(ref unrestBonusPercent, "unrestBonusPercent", 50f);
            Scribe_Values.Look(ref unrestDays, "unrestDays", 3f);
            Scribe_Values.Look(ref ruinsDistance, "ruinsDistance", new IntRange(8, 20));
            Scribe_Values.Look(ref hardtack, "hardtack", new IntRange(5, 50));
            Scribe_Values.Look(ref silver, "silver", new IntRange(100, 1000));
            Scribe_Values.Look(ref components, "components", new IntRange(1, 5));
            Scribe_Values.Look(ref twoTaxesEnabled, "twoTaxesEnabled", true);
            Scribe_Values.Look(ref sameCallsignEnabled, "sameCallsignEnabled", true);
            Scribe_Values.Look(ref harvestFestivalEnabled, "harvestFestivalEnabled", true);
            Scribe_Values.Look(ref memorialEnabled, "memorialEnabled", true);
            Scribe_Values.Look(ref aidManifestEnabled, "aidManifestEnabled", true);
            Scribe_Values.Look(ref cancelledBanquetEnabled, "cancelledBanquetEnabled", true);
            Scribe_Values.Look(ref twoTaxesWeight, "twoTaxesWeight", 1f);
            Scribe_Values.Look(ref sameCallsignWeight, "sameCallsignWeight", 1f);
            Scribe_Values.Look(ref harvestFestivalWeight, "harvestFestivalWeight", 1f);
            Scribe_Values.Look(ref memorialWeight, "memorialWeight", 1f);
            Scribe_Values.Look(ref aidManifestWeight, "aidManifestWeight", 1f);
            Scribe_Values.Look(ref cancelledBanquetWeight, "cancelledBanquetWeight", 1f);
            Scribe_Values.Look(ref tradeDiscountPercent, "tradeDiscountPercent", 25f);
            Scribe_Values.Look(ref woundedJoinChancePercent, "woundedJoinChancePercent", 50f);
            Scribe_Values.Look(ref evacuationGoodwill, "evacuationGoodwill", 5);
            Scribe_Values.Look(ref memorialSteel, "memorialSteel", 40);
            Scribe_Values.Look(ref memorialWood, "memorialWood", 80);
            Scribe_Values.Look(ref restorationSteel, "restorationSteel", 20);
            Scribe_Values.Look(ref restorationWood, "restorationWood", 40);
            Scribe_Values.Look(ref materialsGoodwill, "materialsGoodwill", 5);
            Scribe_Values.Look(ref namesUndergroundGoodwill, "namesUndergroundGoodwill", 10);
            Scribe_Values.Look(ref namesWarlordGoodwill, "namesWarlordGoodwill", 3);
            Scribe_Values.Look(ref restorationGoodwill, "restorationGoodwill", 8);
            Scribe_Values.Look(ref storyDistance, "storyDistance", new IntRange(3, 8));
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        internal void Normalize()
        {
            averageDays = Number(averageDays, 15f, 0f, 360f);
            rebellionWeight = Number(rebellionWeight, 1f, 0f, 10f);
            ruinsWeight = Number(ruinsWeight, 1f, 0f, 10f);
            unrestWeight = Number(unrestWeight, 1f, 0f, 10f);
            mutinyWeight = Number(mutinyWeight, 1f, 0f, 10f);
            civilWarChancePercent = Number(civilWarChancePercent, 5f, 0f, 100f);
            civilWarDelayDays = Number(civilWarDelayDays, 3f, 0f, 360f);
            takeoverChancePercent = Number(takeoverChancePercent, 50f, 0f, 100f);
            takeoverDelayDays = Number(takeoverDelayDays, 60f, 0f, 360f);
            unrestBonusPercent = Number(unrestBonusPercent, 50f, 0f, 500f);
            unrestDays = Number(unrestDays, 3f, 0f, 60f);
            ruinsDistance = Range(ruinsDistance, 1, 100);
            hardtack = Range(hardtack, 0, 1000);
            silver = Range(silver, 0, 10000);
            components = Range(components, 0, 100);
            twoTaxesWeight = Number(twoTaxesWeight, 1f, 0f, 10f);
            sameCallsignWeight = Number(sameCallsignWeight, 1f, 0f, 10f);
            harvestFestivalWeight = Number(harvestFestivalWeight, 1f, 0f, 10f);
            memorialWeight = Number(memorialWeight, 1f, 0f, 10f);
            aidManifestWeight = Number(aidManifestWeight, 1f, 0f, 10f);
            cancelledBanquetWeight = Number(cancelledBanquetWeight, 1f, 0f, 10f);
            tradeDiscountPercent = Number(tradeDiscountPercent, 25f, 0f, 100f);
            woundedJoinChancePercent = Number(woundedJoinChancePercent, 50f, 0f, 100f);
            evacuationGoodwill = UnityEngine.Mathf.Clamp(evacuationGoodwill, 0, 100);
            memorialSteel = UnityEngine.Mathf.Clamp(memorialSteel, 0, 10000);
            memorialWood = UnityEngine.Mathf.Clamp(memorialWood, 0, 10000);
            restorationSteel = UnityEngine.Mathf.Clamp(restorationSteel, 0, 10000);
            restorationWood = UnityEngine.Mathf.Clamp(restorationWood, 0, 10000);
            materialsGoodwill = UnityEngine.Mathf.Clamp(materialsGoodwill, 0, 100);
            namesUndergroundGoodwill = UnityEngine.Mathf.Clamp(namesUndergroundGoodwill, 0, 100);
            namesWarlordGoodwill = UnityEngine.Mathf.Clamp(namesWarlordGoodwill, 0, 100);
            restorationGoodwill = UnityEngine.Mathf.Clamp(restorationGoodwill, 0, 100);
            storyDistance = Range(storyDistance, 1, 100);
        }

        static float Number(float value, float fallback, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : UnityEngine.Mathf.Clamp(value, min, max);
        }

        static IntRange Range(IntRange value, int min, int max)
        {
            int lower = UnityEngine.Mathf.Clamp(value.min, min, max);
            return new IntRange(lower, UnityEngine.Mathf.Clamp(value.max, lower, max));
        }
    }
}
