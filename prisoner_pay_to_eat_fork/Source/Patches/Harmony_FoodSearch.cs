using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Blocks colony prisoners from being assigned a food source beyond their spending power,
    /// at the <see cref="FoodUtility.TryFindBestFoodSourceFor"/> stage, before
    /// any food is picked up or dispensed.
    ///
    /// Why this exists in addition to <see cref="Harmony_Ingest"/>: the Ingested prefix can only
    /// refuse a meal once the pawn is already chewing it. For a nutrient-paste dispenser the
    /// hopper feedstock is consumed inside <c>Building_NutrientPasteDispenser.TryDispenseFood</c>,
    /// which runs in the "take meal from dispenser" toil — well before Ingested. So a penniless
    /// prisoner would walk to the dispenser, waste raw ingredients, chew the whole time, then get
    /// blocked at Ingested and loop forever. By nulling the food source here the pawn never gets
    /// the Ingest job at all: no walk, no dispense, no wasted feedstock, no loop.
    /// The Ingested prefix stays as a safety net for paths that bypass this search (food handed
    /// to the prisoner, already-carried food, etc.).
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.TryFindBestFoodSourceFor))]
    public static class Harmony_FoodSearch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn eater, ref Thing foodSource, ref ThingDef foodDef, ref bool __result)
        {
            if (!__result || foodSource == null) return;
            if (!ShouldBlock(eater, foodDef, foodSource)) return;

            // Veto before a dispenser consumes its feedstock.
            foodSource = null;
            foodDef = null;
            __result = false;
        }

        /// <summary>
        /// True when a chargeable meal exceeds positive family funds and remaining credit.
        /// </summary>
        private static bool ShouldBlock(Pawn eater, ThingDef foodDef, Thing foodSource)
        {
            if (eater == null || !eater.IsPrisonerOfColony) return false;

            // Resolve the def of what will actually be eaten. For a dispenser the Thing is the
            // building; the meal def comes from the out-param (MealNutrientPaste). For spawned
            // food the def is on the thing itself and foodDef matches it.
            ThingDef def = foodDef;
            if (def == null)
            {
                if (foodSource is Building_NutrientPasteDispenser disp)
                    def = disp.DispensableDef;
                else
                    def = foodSource.def;
            }
            if (!Harmony_Ingest.ShouldCharge(eater, def, out float unitCost)) return false;

            // 按实际会吃掉的份数估算（生食/肉干会一次吃多份），否则这里放行、
            // Ingested 前缀再拦下，囚犯就会走到食物前空嚼一轮又吃不到（正是本补丁要避免的循环）。
            int units = foodSource is Building_NutrientPasteDispenser ? 1 : CountUnits.Estimate(eater, foodSource);
            float cost = unitCost * units;
            float available = PrisonersPayToEat2Manager.Current?.SpendingPower(eater) ?? 0f;
            return available < cost;
        }
    }
}
