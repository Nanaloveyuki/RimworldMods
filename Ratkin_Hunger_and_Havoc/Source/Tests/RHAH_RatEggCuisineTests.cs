using HungerAndHavoc.Pawn.Compat;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_RatEggCuisineTests
    {
        [Fact]
        public void MealsSellAndOrdinaryFoodDoesNot()
        {
            Assert.False(RHAH_RatEggCuisine.NpcSells(true, true, "MealSimple"));
            Assert.False(RHAH_RatEggCuisine.NpcSells(true, true, null));
            Assert.True(RHAH_RatEggCuisine.NpcSells(true, true, "Meal_RatEggMeatStewed"));
            Assert.True(RHAH_RatEggCuisine.NpcSells(false, true, "MealSimple"));
            Assert.True(RHAH_RatEggCuisine.NpcSells(true, false, "Steel"));
        }

        [Fact]
        public void StockRejectsMissingCorpseOrUnsellableDefs()
        {
            Assert.False(RHAH_RatEggCuisine.CanStock(false, false, 1f, true));
            Assert.False(RHAH_RatEggCuisine.CanStock(true, true, 1f, true));
            Assert.False(RHAH_RatEggCuisine.CanStock(true, false, 0f, true));
            Assert.False(RHAH_RatEggCuisine.CanStock(true, false, 1f, false));
            Assert.True(RHAH_RatEggCuisine.CanStock(true, false, 1f, true));
        }

        [Fact]
        public void CountsStayInsideTheFixedRanges()
        {
            Assert.Equal(3, RHAH_RatEggCuisine.IngredientCount(0f));
            Assert.Equal(8, RHAH_RatEggCuisine.IngredientCount(0.999f));
            Assert.Equal(5, RHAH_RatEggCuisine.IngredientCount(0.4f));
            int[] picked = { -1, -1 };
            RHAH_RatEggCuisine.PickMeals(8, 0f, 0f, picked);
            Assert.Equal(0, picked[0]);
            Assert.Equal(1, picked[1]);
            RHAH_RatEggCuisine.PickMeals(1, 0.5f, 0.5f, picked);
            Assert.Equal(0, picked[0]);
            Assert.Equal(-1, picked[1]);
        }

        [Fact]
        public void OnlyYoungCaravanCompanionsCarryAndFearTheMeal()
        {
            Assert.True(RHAH_RatEggCuisine.IsCarrier(true, 3f));
            Assert.False(RHAH_RatEggCuisine.IsCarrier(true, 14f));
            Assert.False(RHAH_RatEggCuisine.IsCarrier(false, 3f));
            Assert.True(RHAH_RatEggCuisine.FearsMeal(true, 13.9f, "Meal_RatEggHeadCustard"));
            Assert.False(RHAH_RatEggCuisine.FearsMeal(true, 14f, "Meal_RatEggHeadCustard"));
            Assert.False(RHAH_RatEggCuisine.FearsMeal(false, 3f, "Meal_RatEggHeadCustard"));
            Assert.False(RHAH_RatEggCuisine.FearsMeal(true, 3f, "MealSimple"));
        }

        [Fact]
        public void VisitorLordCaravansListCompanionGoods()
        {
            Assert.True(RHAH_RatEggCuisine.ListsGoods(true, false, false));
            Assert.False(RHAH_RatEggCuisine.ListsGoods(true, true, false));
            Assert.False(RHAH_RatEggCuisine.ListsGoods(true, false, true));
            Assert.False(RHAH_RatEggCuisine.ListsGoods(false, false, false));
        }
    }
}
