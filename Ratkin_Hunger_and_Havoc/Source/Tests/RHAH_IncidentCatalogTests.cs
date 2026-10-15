using System.Linq;
using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_IncidentCatalogTests
    {
        [Fact]
        public void DisplayIdsAreStableAndUnique()
        {
            Assert.Equal(RHAH_IncidentCatalog.All.Count, RHAH_IncidentCatalog.All.Select(entry => entry.DisplayId).Distinct().Count());
            Assert.Equal("I-001", RHAH_IncidentCatalog.All[0].DisplayId);
            Assert.Equal("RHAH_LargeRefugeeWave", RHAH_IncidentCatalog.All[0].DefName);
            Assert.Equal("I-051", RHAH_IncidentCatalog.All[50].DisplayId);
            Assert.Equal("RHAH_RefugeeMassacre", RHAH_IncidentCatalog.All[50].DefName);
            Assert.All(RHAH_IncidentCatalog.All, entry => Assert.StartsWith("RHAH_", entry.DefName));
            Assert.Equal(RHAH_IncidentOrigin.Original, RHAH_IncidentCatalog.GetByDisplayId("I-014").Origin);
            Assert.Equal(RHAH_IncidentOrigin.Sequel, RHAH_IncidentCatalog.GetByDisplayId("I-015").Origin);
            Assert.Equal(RHAH_IncidentCategory.Plague, RHAH_IncidentCatalog.GetByDefName("RHAH_PlagueBeggarGroup").Category);
            Assert.Equal(RHAH_IncidentTarget.Caravan, RHAH_IncidentCatalog.GetByDisplayId("I-035").Target);
        }

    }
}
