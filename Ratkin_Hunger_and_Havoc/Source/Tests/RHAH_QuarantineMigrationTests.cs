using System.Xml;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_QuarantineMigrationTests
    {
        [Fact]
        public void LoadDiscardsRetiredLettersAndNoticesWithoutChangingOtherEvents()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><history><archive><archivables>
<li Class='HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Quarantine'><ID>22</ID><def>RHAH_QuarantineLetter</def></li>
<li Class='Foreign.Letter'><ID>23</ID><def>ForeignLetter</def><text>keep</text></li>
</archivables></archive></history><letterStack><letters><li>Letter_22</li><li>Letter_23</li></letters>
<letterQueue><li Class='HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Quarantine'><ID>24</ID></li></letterQueue></letterStack>
<components><li Class='HungerAndHavoc.Narrative.NarrativeState'><trust>41</trust><quarantineCases><li><mapId>0</mapId></li></quarantineCases>
<envoyCases><li><pawnId>55</pawnId></li></envoyCases><pendingNotices>
<li><letter>N007Recovered</letter></li><li><letter>58</letter></li><li><letter>Aside</letter><arg>3</arg></li>
<li><letter>N008Trade</letter><arg>55</arg></li><li><letter>Aside</letter><arg>4</arg></li>
</pendingNotices></li><li Class='Foreign.Component'><quarantineCases>keep</quarantineCases></li></components></game>");
            XmlNode game = document.DocumentElement;
            string foreignLetter = game.SelectSingleNode("history/archive/archivables/li[@Class='Foreign.Letter']").OuterXml;
            string foreignComponent = game.SelectSingleNode("components/li[@Class='Foreign.Component']").OuterXml;

            RHAH_QuarantineMigration.Clean(game);

            Assert.Equal(foreignLetter, game.SelectSingleNode("history/archive/archivables/li").OuterXml);
            Assert.Equal("Letter_23", game.SelectSingleNode("letterStack/letters/li").InnerText);
            Assert.Empty(game.SelectNodes("letterStack/letterQueue/li"));
            XmlNode narrative = game.SelectSingleNode("components/li[@Class='HungerAndHavoc.Narrative.NarrativeState']");
            Assert.Null(narrative["quarantineCases"]);
            Assert.Equal("41", narrative["trust"].InnerText);
            Assert.Equal("55", narrative.SelectSingleNode("envoyCases/li/pawnId").InnerText);
            Assert.Equal(2, narrative["pendingNotices"].ChildNodes.Count);
            Assert.Equal("N008Trade", narrative["pendingNotices"].FirstChild["letter"].InnerText);
            Assert.Equal("4", narrative["pendingNotices"].LastChild["arg"].InnerText);
            Assert.Equal(foreignComponent, game.SelectSingleNode("components/li[@Class='Foreign.Component']").OuterXml);
            string once = game.OuterXml;
            RHAH_QuarantineMigration.Clean(game);
            Assert.Equal(once, game.OuterXml);
        }

        [Fact]
        public void LoadResetsRetiredSlotWithoutChangingEnvoyState()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><components><li Class='HungerAndHavoc.Narrative.NarrativeState'>
<suiyinEnabled><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li></suiyinEnabled>
<suiyinStarted><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li><li>True</li></suiyinStarted>
<suiyinDeadlineTick><li>100</li><li>200</li><li>300</li><li>400</li><li>500</li><li>600</li><li>700</li><li>800</li></suiyinDeadlineTick>
</li></components></game>");

            RHAH_QuarantineMigration.Clean(document.DocumentElement);

            XmlNode narrative = document.DocumentElement.SelectSingleNode("components/li");
            Assert.Equal("False", narrative["suiyinEnabled"].ChildNodes[6].InnerText);
            Assert.Equal("False", narrative["suiyinStarted"].ChildNodes[6].InnerText);
            Assert.Equal("-1", narrative["suiyinDeadlineTick"].ChildNodes[6].InnerText);
            Assert.Equal("True", narrative["suiyinEnabled"].ChildNodes[7].InnerText);
            Assert.Equal("True", narrative["suiyinStarted"].ChildNodes[7].InnerText);
            Assert.Equal("800", narrative["suiyinDeadlineTick"].ChildNodes[7].InnerText);
        }

        [Theory]
        [InlineData("ut", null, false, null, true)]
        [InlineData("uf", null, true, null, true)]
        [InlineData("ut", "rhah:envoyHold", false, false, false)]
        [InlineData("ut", "rhah:longHold", false, false, false)]
        public void LoadReleasesOnlyRetiredHoldAndPreservesOtherOwners(
            string previous, string otherHold, bool exit, bool? expectedFed, bool expectedExit)
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.SetExtra("rhah:quarantineHold", previous);
            if (otherHold != null) state.SetExtra(otherHold, previous);
            state.SetGate(RHAH_BehaviorGate.LeaveAfterFed, false);
            state.SetGate(RHAH_BehaviorGate.ExitMap, exit);

            RHAH_QuarantineMigration.RestoreVisitorGates(state);

            Assert.False(state.TryGetExtra("rhah:quarantineHold", out _));
            Assert.Equal(expectedFed, state.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed));
            Assert.Equal(expectedExit, state.GetGateOverride(RHAH_BehaviorGate.ExitMap));
            if (otherHold != null) Assert.True(state.TryGetExtra(otherHold, out _));
            RHAH_QuarantineMigration.RestoreVisitorGates(state);
            Assert.Equal(expectedFed, state.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed));
            Assert.Equal(expectedExit, state.GetGateOverride(RHAH_BehaviorGate.ExitMap));
        }
    }
}
