using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Trade;
using RimWorld;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_FaminePolicyTests
    {
        [Fact]
        public void TradeChangesOnlyPlayerFoodSalesAndOtherPurchases()
        {
            Assert.Equal(9f, RHAH_FamineTrade.Price(true, true, TradeAction.PlayerSells, 6f, 14f, 6f, 1.5f, 0.75f));
            Assert.Equal(14f, RHAH_FamineTrade.Price(true, true, TradeAction.PlayerBuys, 14f, 14f, 6f, 1.5f, 0.75f));
            Assert.Equal(10.5f, RHAH_FamineTrade.Price(true, false, TradeAction.PlayerBuys, 14f, 14f, 6f, 1.5f, 0.75f));
            Assert.Equal(6f, RHAH_FamineTrade.Price(true, false, TradeAction.PlayerSells, 6f, 14f, 6f, 1.5f, 0.75f));
            Assert.Equal(6f, RHAH_FamineTrade.Price(false, true, TradeAction.PlayerSells, 6f, 14f, 6f, 1.5f, 0.75f));
            Assert.Equal(14f, RHAH_FamineTrade.Price(true, true, TradeAction.PlayerSells, 6f, 14f, 6f, 3f, 0.75f));
            Assert.Equal(6f, RHAH_FamineTrade.Price(true, false, TradeAction.PlayerBuys, 14f, 14f, 6f, 1.5f, 0.1f));
        }

        [Fact]
        public void ArrivalOnlyAppliesToFreshMarkedNonPlayerPawns()
        {
            Assert.True(RHAH_FamineArrival.Applies(true, true, false, true));
            Assert.False(RHAH_FamineArrival.Applies(false, true, false, true));
            Assert.False(RHAH_FamineArrival.Applies(true, false, false, true));
            Assert.False(RHAH_FamineArrival.Applies(true, true, true, true));
            Assert.False(RHAH_FamineArrival.Applies(true, true, false, false));
        }

        [Fact]
        public void WeaponsOnlyEquipAdultSiegeMembersAtNormalOrBelow()
        {
            Assert.True(RHAH_FamineWeapons.CanEquip(true, true, false, "I-014", true, true));
            Assert.False(RHAH_FamineWeapons.CanEquip(true, true, false, "I-014", false, true));
            Assert.False(RHAH_FamineWeapons.CanEquip(true, true, false, "I-004", true, true));
            Assert.False(RHAH_FamineWeapons.CanEquip(true, false, false, "I-014", true, true));
            Assert.False(RHAH_FamineWeapons.CanEquip(true, true, true, "I-014", true, true));
            Assert.False(RHAH_FamineWeapons.CanEquip(false, true, false, "I-014", true, true));
            for (int roll = -1; roll <= 7; roll++) Assert.InRange(RHAH_FamineWeapons.Quality(roll), QualityCategory.Awful, QualityCategory.Normal);
        }

        [Fact]
        public void ArmedDemandOnlyAttacksForRealRefusalOrExpiry()
        {
            Assert.True(RHAH_ArmedDemand.StartsAssault(true, false, RHAH_ChoiceAction.Reject));
            Assert.True(RHAH_ArmedDemand.StartsAssault(true, false, RHAH_ChoiceAction.Timeout));
            Assert.False(RHAH_ArmedDemand.StartsAssault(false, false, RHAH_ChoiceAction.Timeout));
            Assert.False(RHAH_ArmedDemand.StartsAssault(true, true, RHAH_ChoiceAction.Reject));
            Assert.False(RHAH_ArmedDemand.StartsAssault(true, false, RHAH_ChoiceAction.Deliver));
            Assert.False(RHAH_ArmedDemand.StartsAssault(true, false, RHAH_ChoiceAction.Ignore));
        }
    }
}
