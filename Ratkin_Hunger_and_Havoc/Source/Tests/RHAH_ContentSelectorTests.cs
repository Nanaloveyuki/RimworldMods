using System.Collections.Generic;
using HungerAndHavoc.Data;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ContentSelectorTests
    {
        [Fact]
        public void CatalogKeepsEveryOwnedHistoryAndTrait()
        {
            Assert.Equal(121, RHAH_ContentCatalog.Histories.Count);
            Assert.Equal(50, RHAH_ContentCatalog.Traits.Count);
            Assert.NotNull(RHAH_ContentCatalog.FindHistory("A001"));
            Assert.NotNull(RHAH_ContentCatalog.FindHistory("Y047"));
            Assert.Equal("RHAH_Trait_HardyLabor", RHAH_ContentCatalog.FindTrait("T01").TraitDefName);
            Assert.True(RHAH_ContentCatalog.IsOwnedHistory("RHAH_History_Newborn"));
            Assert.False(RHAH_ContentCatalog.IsOwnedTrait("Industriousness"));
        }

        [Fact]
        public void ChildCannotDrawAnAdulthood()
        {
            RHAH_ContentQuery query = Query(6f, false, null, null);
            for (int i = 0; i < 30; i++)
            {
                RHAH_HistoryRecord selected = RHAH_ContentSelector.SelectHistory(query, () => i / 30f);
                Assert.NotNull(selected);
                Assert.Equal(RHAH_HistorySlot.Childhood, selected.Slot);
                Assert.True(selected.MaximumAge == null || selected.MaximumAge > 6f);
            }
        }
        [Fact]
        public void AdultStageBelowEighteenDrawsNoAdultHistory()
        {
            RHAH_ContentQuery query = Query(16f, true, 20f, null);
            Assert.Null(RHAH_ContentSelector.SelectHistory(query, () => 0.2f));
        }


        [Fact]
        public void DisabledHistoryStaysOutOfTheDraw()
        {
            RHAH_ContentQuery query = Query(30f, true, 20f, "I-001", id => id != "A001");
            for (int i = 0; i < 20; i++)
            {
                RHAH_HistoryRecord selected = RHAH_ContentSelector.SelectHistory(query, () => i / 20f);
                Assert.NotEqual("A001", selected?.DisplayId);
            }
        }

        [Fact]
        public void ColdAdultMissesAWarmOnlyHistory()
        {
            RHAH_HistoryRecord warm = RHAH_ContentCatalog.FindHistory("A001");
            RHAH_ContentQuery query = Query(30f, true, warm.MinimumTemperature - 5f, null);
            for (int i = 0; i < 20; i++)
            {
                Assert.NotEqual("A001", RHAH_ContentSelector.SelectHistory(query, () => i / 20f)?.DisplayId);
            }
        }

        [Fact]
        public void TraitPrefersTheMatchingHistory()
        {
            RHAH_ContentQuery query = Query(30f, true, 20f, "I-001");
            RHAH_TraitRecord selected = RHAH_ContentSelector.SelectTrait(
                query,
                "A001",
                () => 0f,
                record => true);
            Assert.NotNull(selected);
            Assert.Contains("A001", selected.HistoryDisplayIds);
        }

        [Fact]
        public void ZeroTraitWeightDrawsNothing()
        {
            RHAH_ContentQuery query = Query(30f, true, 20f, "I-001", _ => true, _ => 0f);
            Assert.Null(RHAH_ContentSelector.SelectTrait(query, "A001", () => 0f, record => true));
        }

        [Fact]
        public void PlagueCategoryUsesTheNewIncidentIds()
        {
            Assert.True(RHAH_ContentSelector.CategoryMatches(RHAH_ContentCategory.Plague, "I-036"));
            Assert.True(RHAH_ContentSelector.CategoryMatches(RHAH_ContentCategory.Plague, "I-050"));
            Assert.False(RHAH_ContentSelector.CategoryMatches(RHAH_ContentCategory.Plague, "I-051"));
            Assert.True(RHAH_ContentSelector.CategoryMatches(RHAH_ContentCategory.Theft, "I-043"));
            Assert.True(RHAH_ContentSelector.CategoryMatches(RHAH_ContentCategory.Siege, "I-045"));
        }
        [Fact]
        public void ListKeysSeparateSourceNameAndCategory()
        {
            Assert.Equal("饥与祸", RHAH_ContentSelector.ListKey(0, "A001", RHAH_ContentCategory.Plague, "饥与祸", true));
            Assert.Equal("A", RHAH_ContentSelector.ListKey(1, "A001", RHAH_ContentCategory.None, "饥与祸", true));
            Assert.Equal("plague", RHAH_ContentSelector.ListKey(2, "A001", RHAH_ContentCategory.Plague, "饥与祸", true));
            Assert.Equal("vanilla", RHAH_ContentSelector.ListKey(2, "Kind", RHAH_ContentCategory.None, "Core", false));
            Assert.True(RHAH_ContentSelector.MatchesQuery("井水", "A005", "none", "井"));
            Assert.False(RHAH_ContentSelector.MatchesQuery("井水", "A005", "none", "围攻"));
        }

        static RHAH_ContentQuery Query(
            float age,
            bool adult,
            float? temperature,
            string incident,
            System.Func<string, bool> historyEnabled = null,
            System.Func<string, float> traitWeight = null)
        {
            return new RHAH_ContentQuery(
                age,
                adult,
                true,
                temperature,
                incident,
                true,
                true,
                historyEnabled ?? (_ => true),
                _ => true,
                traitWeight ?? (id => RHAH_ContentCatalog.DefaultTraitWeight(id)));
        }
    }
}
