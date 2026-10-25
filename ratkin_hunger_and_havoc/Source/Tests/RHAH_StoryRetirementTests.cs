using System;
using System.Xml;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_StoryRetirementTests
    {
        [Fact]
        public void RetiresExactStoriesPreservingTrustOrdinaryLettersPeopleAndSupply()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><components><li Class='HungerAndHavoc.Narrative.NarrativeState'><trust>71</trust></li></components>
<history><archive><archivables>
<li Class='HungerAndHavoc.Storyteller.Suiyin.ChoiceLetter_RHAH_KingdomArc'><ID>22</ID><def>RHAH_KingdomArcLetter</def></li>
<li Class='RimWorld.StandardLetter'><ID>23</ID><def>NeutralEvent</def><text>Existing translated story remains</text></li>
<li Class='HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Request'><ID>24</ID><def>RHAH_ChoiceRequest</def></li>
</archivables></archive></history><letterStack><letters><li>Letter_22</li><li>Letter_23</li><li>Letter_24</li></letters></letterStack>
<world><worldPawns><pawnsAlive><li><id>51</id><inventory><innerContainer><innerList/></innerContainer></inventory></li></pawnsAlive></worldPawns>
<worldObjects><worldObjects>
<li Class='HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite'><def>RHAH_KingdomSite</def><ID>201</ID><kingdomRecipients><li>Thing_51</li></kingdomRecipients><kingdomSupply><li><def>RawRice</def><id>701</id><stackCount>100</stackCount></li><li><def>RHAH_KingdomMaterial</def><id>702</id></li></kingdomSupply></li>
<li Class='RimWorld.Planet.Site'><def>RHAH_RecordSite</def><ID>202</ID><parts><li><def>RHAH_RecordSite</def></li></parts></li>
<li><def>RHAH_RefugeeCamp</def><ID>203</ID></li>
</worldObjects></worldObjects></world>
<maps><li><generatorDef>RHAH_RecordSite</generatorDef><mapInfo><parent>WorldObject_202</parent></mapInfo><things><thing Class='HungerAndHavoc.Storyteller.Suiyin.Building_RHAH_RecordBox'><def>RHAH_RecordBox</def><id>703</id></thing><thing><def>Human</def><id>9</id></thing></things></li></maps></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Equal("71", document.SelectSingleNode("/game/components/li/trust").InnerText);
            Assert.Equal(2, document.SelectNodes("/game/history/archive/archivables/li").Count);
            Assert.Equal(2, document.SelectNodes("/game/letterStack/letters/li").Count);
            Assert.NotNull(document.SelectSingleNode("/game/letterStack/letters/li[text()='Letter_24']"));
            Assert.Equal("100", document.SelectSingleNode("/game/world/worldPawns/pawnsAlive/li/inventory/innerContainer/innerList/li/stackCount").InnerText);
            Assert.Equal("701", document.SelectSingleNode("/game/world/worldPawns/pawnsAlive/li/inventory/innerContainer/innerList/li/id").InnerText);
            Assert.Null(document.SelectSingleNode("//li[ID='201']"));
            Assert.Equal("Camp", document.SelectSingleNode("//li[ID='202']/def").InnerText);
            Assert.Equal("RimWorld.Planet.Camp", document.SelectSingleNode("//li[ID='202']").Attributes["Class"].Value);
            Assert.Null(document.SelectSingleNode("//li[ID='202']/parts"));
            Assert.NotNull(document.SelectSingleNode("//li[ID='203']"));
            Assert.Equal("Encounter", document.SelectSingleNode("/game/maps/li/generatorDef").InnerText);
            Assert.Single(document.SelectNodes("/game/maps/li/things/thing"));
            Assert.Equal("9", document.SelectSingleNode("/game/maps/li/things/thing/id").InnerText);
            string once = document.OuterXml;
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Equal(once, document.OuterXml);
        }

        [Fact]
        public void MissingSupplyRecipientStopsLoadRatherThanDiscardingExistingFood()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml("<game><world><worldObjects><worldObjects><li><def>RHAH_KingdomSite</def><ID>1</ID><kingdomSupply><li><def>RawRice</def><id>701</id><stackCount>100</stackCount></li></kingdomSupply></li></worldObjects></worldObjects></world></game>");
            Assert.Throws<InvalidOperationException>(() => RHAH_StoryRetirement.Clean(document.DocumentElement));
            Assert.Equal("100", document.SelectSingleNode("//kingdomSupply/li/stackCount").InnerText);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void LegacyEnvoyWithoutHoldMarkerReleasesOnlyItsOwnRecordedGates(bool migrated, bool longFollow)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml("<game><components><li Class='HungerAndHavoc.Narrative.NarrativeState'><envoyCases><li><pawnId>55</pawnId><holdMigrated>" + migrated + "</holdMigrated></li></envoyCases></li></components>" +
                "<maps><li><things><thing><id>55</id><healthTracker><hediffSet><hediffs><li><def>RHAH_HungerMark</def><sourceIncidentDisplayId>N-008</sourceIncidentDisplayId>" +
                "<gateOverrides><keys><li>LeaveAfterFed</li><li>ExitMap</li><li>Beg</li></keys><values><li>False</li><li>False</li><li>False</li></values></gateOverrides>" +
                (longFollow ? "<extraData><keys><li>rhah:longHold</li></keys><values><li>uu</li></values></extraData>" : "") +
                "</li></hediffs></hediffSet></healthTracker></thing></things></li></maps></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            int expected = migrated || longFollow ? 3 : 1;
            Assert.Equal(expected, document.SelectNodes("//gateOverrides/keys/li").Count);
            Assert.Equal(expected, document.SelectNodes("//gateOverrides/values/li").Count);
            Assert.Equal("Beg", document.SelectSingleNode("//gateOverrides/keys/li[last()]").InnerText);
            Assert.Equal("False", document.SelectSingleNode("//gateOverrides/values/li[last()]").InnerText);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RetiredEnvoyHoldDoesNotReleaseOrdinaryLongFollow(bool longFollow)
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.SetExtra("rhah:envoyHold", "ut");
            if (longFollow) state.SetExtra("rhah:longHold", "ut");
            state.SetGate(RHAH_BehaviorGate.LeaveAfterFed, false);
            state.SetGate(RHAH_BehaviorGate.ExitMap, false);
            RHAH_StoryRetirement.RestoreVisitorGates(state);
            Assert.False(state.TryGetExtra("rhah:envoyHold", out _));
            Assert.Equal(longFollow ? false : (bool?)null, state.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed));
            Assert.Equal(!longFollow, state.GetGateOverride(RHAH_BehaviorGate.ExitMap));
            if (longFollow) Assert.True(state.TryGetExtra("rhah:longHold", out _));
        }

        [Fact]
        public void MothballedKingdomRecipientKeepsSupplyWithoutBlockingUpgrade()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><world><worldPawns><pawnsMothballed><li><def>Human</def><id>Human51</id><inventory><innerContainer><innerList/></innerContainer></inventory></li></pawnsMothballed></worldPawns><worldObjects><worldObjects><li Class='HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite'><def>RHAH_KingdomSite</def><ID>201</ID><kingdomRecipients><li>Thing_Human51</li></kingdomRecipients><kingdomSupply><li><def>RawRice</def><id>RawRice701</id><stackCount>100</stackCount></li></kingdomSupply></li></worldObjects></worldObjects></world></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Equal("RawRice701", document.SelectSingleNode("//pawnsMothballed/li/inventory/innerContainer/innerList/li/id").InnerText);
            Assert.Equal("100", document.SelectSingleNode("//pawnsMothballed/li/inventory/innerContainer/innerList/li/stackCount").InnerText);
            Assert.Null(document.SelectSingleNode("//kingdomSupply"));
        }

        [Fact]
        public void LegacyEnvoyMatchesPublishedThingIdRatherThanNumericCaseId()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><components><li Class='HungerAndHavoc.Narrative.NarrativeState'><envoyCases><li><pawnId>55</pawnId></li></envoyCases></li></components><maps><li><things><thing Class='Pawn'><def>RHAH_Ratkin</def><id>RHAH_Ratkin55</id><healthTracker><hediffSet><hediffs><li><def>RHAH_HungerMark</def><sourceIncidentDisplayId>N-008</sourceIncidentDisplayId><gateOverrides><keys><li>LeaveAfterFed</li><li>ExitMap</li><li>Beg</li></keys><values><li>False</li><li>False</li><li>False</li></values></gateOverrides></li></hediffs></hediffSet></healthTracker></thing></things></li></maps></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Single(document.SelectNodes("//gateOverrides/keys/li"));
            Assert.Equal("Beg", document.SelectSingleNode("//gateOverrides/keys/li").InnerText);
            Assert.Equal("False", document.SelectSingleNode("//gateOverrides/values/li").InnerText);
        }

        [Fact]
        public void RemovedThingReferenceDoesNotUnpairVanillaDictionary()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><maps><li><things><thing><def>RHAH_MigrationRecord</def><id>RHAH_MigrationRecord703</id></thing></things><referenceData><keys><li>Thing_RHAH_MigrationRecord703</li><li>Thing_Human9</li></keys><values><li>1</li><li>2</li></values></referenceData></li></maps></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Single(document.SelectNodes("//referenceData/keys/li"));
            Assert.Equal("Thing_Human9", document.SelectSingleNode("//referenceData/keys/li").InnerText);
            Assert.Single(document.SelectNodes("//referenceData/values/li"));
            Assert.Equal("2", document.SelectSingleNode("//referenceData/values/li").InnerText);
        }

        [Fact]
        public void GravshipKeepsRemainingThingsPairedWithTheirOriginalPositions()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<game><world><worldObjects><worldObjects><li Class='RimWorld.Planet.Gravship'><def>Gravship</def><things><keys><li><def>RHAH_KingdomMaterial</def><id>RHAH_KingdomMaterial1</id></li><li><def>Steel</def><id>Steel2</id></li></keys><values><li><position>(1, 0, 1)</position></li><li><position>(7, 0, 9)</position></li></values></things></li></worldObjects></worldObjects></world></game>");
            RHAH_StoryRetirement.Clean(document.DocumentElement);
            Assert.Single(document.SelectNodes("//things/keys/li"));
            Assert.Equal("Steel2", document.SelectSingleNode("//things/keys/li/id").InnerText);
            Assert.Single(document.SelectNodes("//things/values/li"));
            Assert.Equal("(7, 0, 9)", document.SelectSingleNode("//things/values/li/position").InnerText);
        }
    }
}
