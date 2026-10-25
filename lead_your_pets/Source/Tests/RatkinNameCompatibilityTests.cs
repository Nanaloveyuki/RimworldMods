#if RIMWORLD_1_7
using System;
using Xunit;

namespace LeadYourPet.Tests
{
    public class RatkinNameCompatibilityTests
    {
        [Theory]
        [InlineData("MouseEggState", typeof(YoungRatkinState))]
        [InlineData("Thought_Memory_MouseEggOwnerList", typeof(Thought_Memory_YoungRatkinOwnerList))]
        [InlineData("JobDriver_MouseEggInteraction", typeof(JobDriver_YoungRatkinInteraction))]
        [InlineData("JobDriver_CarryTravelMouseEgg", typeof(JobDriver_CarryTravelYoungRatkin))]
        public void SavedPolymorphicTypesResolveToRenamedImplementations(string savedName, Type expected)
        {
            Assert.Same(expected, LegacyRatkinNames.ResolveType(savedName));
            Assert.Same(expected, LegacyRatkinNames.ResolveType("LeadYourPet." + savedName));
            Assert.Null(LegacyRatkinNames.ResolveType(expected.FullName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("AnotherMod.MouseEggState")]
        [InlineData("MouseDisaster.MouseEggState")]
        [InlineData("LeadYourPet.MouseEggStateExtra")]
        [InlineData("LeadYourPet.MouseEggState, AnotherMod")]
        [InlineData("LeadYourPet_MouseEggOwnerMood")]
        public void NonLegacyNamesRemainForVanillaResolution(string name)
        {
            Assert.Null(LegacyRatkinNames.ResolveType(name));
        }

        [Theory]
        [InlineData(typeof(LeashLinkKind), "MouseEggPet", 1)]
        [InlineData(typeof(YoungRatkinSpecialSource), "MouseDisasterBeggarFamily", 1)]
        [InlineData(typeof(YoungRatkinSpecialSource), "MouseDisasterChildExchange", 2)]
        public void SavedEnumNamesPreserveIdentity(Type type, string name, int expected)
        {
            object resolved = LegacyRatkinNames.ResolveEnum(type, name);
            Assert.NotNull(resolved);
            Assert.Equal(type, resolved.GetType());
            Assert.Equal(expected, Convert.ToInt32(resolved));
        }

        [Fact]
        public void EnumAliasesDoNotCaptureOtherOwnersOrCurrentMembers()
        {
            Assert.Null(LegacyRatkinNames.ResolveEnum(typeof(CollidingLegacyMembers), "MouseEggPet"));
            Assert.Null(LegacyRatkinNames.ResolveEnum(typeof(YoungRatkinSpecialSource), "MouseEggPet"));
            Assert.Null(LegacyRatkinNames.ResolveEnum(typeof(LeashLinkKind), "MouseDisasterChildExchange"));
            Assert.Null(LegacyRatkinNames.ResolveEnum(typeof(LeashLinkKind), "YoungRatkinPet"));
            Assert.Null(LegacyRatkinNames.ResolveEnum(typeof(LeashLinkKind), "mouseEggPet"));
        }

        private enum CollidingLegacyMembers
        {
            MouseEggPet = 41
        }
    }
}
#endif
