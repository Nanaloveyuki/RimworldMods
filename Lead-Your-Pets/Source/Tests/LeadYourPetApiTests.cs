using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LeadYourPet.Tests
{
    public class LeadYourPetApiTests
    {
        [Fact]
        public void CopiesDistinctPetsAndEndsEveryCopiedPet()
        {
            object first = new object();
            object second = new object();
            List<object> copied = new List<object> { first };
            List<object> ended = new List<object>();

            Assert.True(LeadYourPetApiRules.CopyLinkedPets(new[] { first, second, first, null }, copied));
            Assert.Equal(new[] { first, second }, copied);
            Assert.False(LeadYourPetApiRules.CopyLinkedPets(new[] { first }, copied));
            Assert.False(LeadYourPetApiRules.CopyLinkedPets<object>(null, copied));
            Assert.False(LeadYourPetApiRules.CopyLinkedPets(copied, null));

            LeadYourPetApiRules.EndForMaster(new[] { first, null, second }, ended.Add);
            Assert.Equal(new[] { first, second }, ended);
            LeadYourPetApiRules.EndForMaster<object>(null, ended.Add);
            LeadYourPetApiRules.EndForMaster(copied, null);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, false)]
        [InlineData(-1, false)]
        public void AcceptsOnlyExistingSpecialSources(int source, bool accepted)
        {
            Assert.Equal(accepted, LeadYourPetApiRules.IsAcceptedSpecialSource(source));
            Assert.Equal(0, LeadYourPetApiRules.None);
            Assert.Equal(1, LeadYourPetApiRules.BeggarFamily);
            Assert.Equal(2, LeadYourPetApiRules.ChildExchange);
        }

    }
}
