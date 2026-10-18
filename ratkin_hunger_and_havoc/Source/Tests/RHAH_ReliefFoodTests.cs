using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ReliefFoodTests
    {
        [Fact]
        public void EmptyDenylistAllowsFoodAndDisableAllKeepsLaterFoods()
        {
            RHAH_Settings settings = new RHAH_Settings();
            Assert.True(settings.IsReliefFoodEnabled("MealSimple"));
            settings.SetAllReliefFood(false, new System.Collections.Generic.List<string> { "MealSimple" });
            Assert.False(settings.IsReliefFoodEnabled("MealSimple"));
            Assert.True(settings.IsReliefFoodEnabled("MealFine"));
            settings.SetAllReliefFood(true, null);
            Assert.True(settings.IsReliefFoodEnabled("MealSimple"));
        }

        [Fact]
        public void SatietyAndRefeedThresholdsStayAtThePlannedValues()
        {
            Assert.Equal(0.82f, RHAH_ReliefFood.SatisfiedLevel);
            Assert.Equal(0.4f, RHAH_ReliefFood.RefeedMalnutrition);
            Assert.True(0.81f < RHAH_ReliefFood.SatisfiedLevel);
            Assert.True(0.39f < RHAH_ReliefFood.RefeedMalnutrition);
            Assert.False(0.39f >= RHAH_ReliefFood.RefeedMalnutrition);
        }

        [Fact]
        public void MissingFoodIsRejectedBeforeAJobCanBeBuilt()
        {
            Assert.Equal(RHAH_FoodReject.Missing, RHAH_ReliefFood.Reject(null, null, true));
            Assert.Null(RHAH_ReliefFood.MakeJob(null, null));
            Assert.Null(JobGiver_RHAH_Feed.TryCreate(null));
            Assert.Null(JobGiver_RHAH_WaitFood.TryCreate(null));
        }

        [Fact]
        public void FoodsGroupBySourceModAndKeepOrder()
        {
            ThingDef meal = Food("MealSimple", "Core");
            ThingDef fine = Food("MealFine", "Core");
            ThingDef paste = Food("MealNutrientPaste", "Ratkin: Hunger and Havoc");
            ThingDef loose = Food("LooseBerry", null);
            List<List<ThingDef>> groups = RHAH_ReliefFood.GroupBySourceMod(new List<ThingDef>
            {
                meal,
                paste,
                fine,
                loose
            });
            Assert.Equal(3, groups.Count);
            Assert.Equal(new[] { meal, fine }, groups[0]);
            Assert.Equal(new[] { paste }, groups[1]);
            Assert.Equal(new[] { loose }, groups[2]);
            Assert.Equal("Core", RHAH_ReliefFood.SourceModName(meal));
            Assert.Null(RHAH_ReliefFood.SourceModName(loose));
            Assert.Empty(RHAH_ReliefFood.GroupBySourceMod(null));
        }

        [Fact]
        public void FoodQueryMatchesLabelDefNameAndMod()
        {
            ThingDef meal = Food("MealSimple", "Core");
            meal.label = "simple meal";
            Assert.True(RHAH_ReliefFood.MatchesQuery(meal, null));
            Assert.True(RHAH_ReliefFood.MatchesQuery(meal, "simple core"));
            Assert.True(RHAH_ReliefFood.MatchesQuery(meal, "mealsimple"));
            Assert.False(RHAH_ReliefFood.MatchesQuery(meal, "simple paste"));
            Assert.False(RHAH_ReliefFood.MatchesQuery(null, "meal"));
        }

        [Fact]
        public void GiveFoodDenylistStaysSeparateFromReliefFood()
        {
            RHAH_Settings settings = new RHAH_Settings();
            settings.SetGiveFoodEnabled("MealSimple", false);
            Assert.False(settings.IsGiveFoodEnabled("MealSimple"));
            Assert.True(settings.IsGiveFoodEnabled("MealFine"));
            Assert.True(settings.IsReliefFoodEnabled("MealSimple"));
            settings.SetAllGiveFood(false, new List<string> { "MealFine" });
            Assert.False(settings.IsGiveFoodEnabled("MealFine"));
            Assert.True(settings.IsGiveFoodEnabled("MealSurvivalPack"));
            settings.SetAllGiveFood(true, null);
            Assert.True(settings.IsGiveFoodEnabled("MealFine"));
            Assert.Equal(0, settings.giveFoodListMode);
            settings.SetBegFoodEnabled("MealSimple", false);
            Assert.False(settings.IsBegFoodEnabled("MealSimple"));
            Assert.True(settings.IsGiveFoodEnabled("MealSimple"));
            settings.SetAllBegFood(false, new List<string> { "MealFine" });
            Assert.False(settings.IsBegFoodEnabled("MealFine"));
            Assert.True(settings.IsBegFoodEnabled("MealSurvivalPack"));
            settings.SetAllBegFood(true, null);
            Assert.True(settings.IsBegFoodEnabled("MealFine"));
            Assert.False(settings.begAutoGiveEnabled);
            Assert.Equal(35, settings.begSuccessChancePercent);
            Assert.Equal(3, settings.begSocialBonusPercent);
        }

        [Fact]
        public void GiveFoodGroupsByModNameAndVanillaCategory()
        {
            ThingDef meal = Categorized("MealSimple", "Core", "simple meal", "FoodMeals", "Foods");
            ThingDef fine = Categorized("MealFine", "Core", "fine meal", "FoodMeals", "Foods");
            ThingDef meat = Categorized("Meat_Cow", "Core", "cow meat", "MeatRaw", "FoodRaw");
            ThingDef loose = Food("LooseBerry", null);
            loose.label = "berry";

            List<ThingDef> foods = new List<ThingDef> { meal, meat, fine, loose };
            List<List<ThingDef>> byMod = RHAH_ReliefFood.GroupFoods(0, foods);
            Assert.Equal(new[] { meal, meat, fine }, byMod[0]);
            Assert.Equal(new[] { loose }, byMod[1]);
            Assert.Equal("Core", RHAH_ReliefFood.GroupKey(0, meal));

            List<List<ThingDef>> byName = RHAH_ReliefFood.GroupFoods(1, foods);
            Assert.Equal(4, byName.Count);
            Assert.Equal(new[] { loose }, byName[0]);
            Assert.Equal(new[] { meat }, byName[1]);
            Assert.Equal(new[] { fine }, byName[2]);
            Assert.Equal(new[] { meal }, byName[3]);
            Assert.Equal("B", RHAH_ReliefFood.GroupKey(1, loose));
            Assert.Equal("C", RHAH_ReliefFood.GroupKey(1, meat));
            Assert.Equal("F", RHAH_ReliefFood.GroupKey(1, fine));
            Assert.Equal("S", RHAH_ReliefFood.GroupKey(1, meal));

            List<List<ThingDef>> byCategory = RHAH_ReliefFood.GroupFoods(2, foods);
            Assert.Equal("FoodMeals", RHAH_ReliefFood.GroupKey(2, fine));
            Assert.Equal("FoodRaw", RHAH_ReliefFood.GroupKey(2, meat));
            Assert.Equal("other", RHAH_ReliefFood.GroupKey(2, loose));
            Assert.Equal(3, byCategory.Count);
            Assert.Equal(new[] { fine, meal }, byCategory[0]);
            Assert.Null(RHAH_ReliefFood.GroupTitle(2, "other"));
            Assert.Empty(RHAH_ReliefFood.GroupFoods(2, null));
        }
        static ThingDef Food(string defName, string modName)
        {
            ThingDef food = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            food.defName = defName;
            if (modName != null)
            {
                ModContentPack pack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
                typeof(ModContentPack).GetField("nameInt", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pack, modName);
                food.modContentPack = pack;
            }

            return food;
        }

        static ThingDef Categorized(string defName, string modName, string label, string categoryName, string parentName)
        {
            ThingDef food = Food(defName, modName);
            food.label = label;
            ThingCategoryDef parent = (ThingCategoryDef)FormatterServices.GetUninitializedObject(typeof(ThingCategoryDef));
            parent.defName = parentName;
            ThingCategoryDef category = (ThingCategoryDef)FormatterServices.GetUninitializedObject(typeof(ThingCategoryDef));
            category.defName = categoryName;
            category.parent = parentName == "Foods" ? parent : null;
            if (parentName != "Foods")
            {
                ThingCategoryDef foods = (ThingCategoryDef)FormatterServices.GetUninitializedObject(typeof(ThingCategoryDef));
                foods.defName = "Foods";
                parent.parent = foods;
                category.parent = parent;
            }

            food.thingCategories = new List<ThingCategoryDef> { category };
            return food;
        }
    }
}
