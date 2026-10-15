using HungerAndHavoc.Storyteller.Suiyin;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_NarrativeWatchTests
    {
        [Fact]
        public void UnvisitedSiteWaitsForItsDeadlineBeforeRemoval()
        {
            SuiyinBook book = new SuiyinBook();
            Assert.True(book.OpenRelic(0));
            book.N009.SiteId = 32;
            book.N009.MapPresent = true;
            book.N009.MapEntered = false;
            book.N009.PlayersInside = false;
            int before = book.N009.Deadline - 1;

            Assert.False(book.ExpireRelic(before));
            Assert.Equal(SuiyinN009Outcome.Pending, book.N009.Outcome);
            Assert.False(RHAH_RecordSite.ShouldRemove(book.N009.Outcome, book.N009.BoxDestroyed, false, false, book.N009.MapEntered));

            Assert.True(book.ExpireRelic(book.N009.Deadline));
            Assert.Equal(SuiyinN009Outcome.Empty, book.N009.Outcome);
            Assert.True(RHAH_RecordSite.ShouldRemove(book.N009.Outcome, book.N009.BoxDestroyed, false, false, book.N009.MapEntered));
            Assert.False(RHAH_RecordSite.ShouldRemove(book.N009.Outcome, book.N009.BoxDestroyed, true, false, book.N009.MapEntered));
            Assert.False(RHAH_RecordSite.ShouldRemove(book.N009.Outcome, book.N009.BoxDestroyed, false, true, book.N009.MapEntered));
        }

        [Fact]
        public void EnteredOrDestroyedSiteIsNotAnAbandonedRemoval()
        {
            Assert.False(RHAH_RecordSite.ShouldRemove(SuiyinN009Outcome.Empty, false, false, false, true));
            Assert.False(RHAH_RecordSite.ShouldRemove(SuiyinN009Outcome.Destroyed, true, false, false, true));
            Assert.False(RHAH_RecordSite.ShouldRemove(SuiyinN009Outcome.Empty, true, false, false, false));
        }

    }
}
