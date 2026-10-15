using System;
using System.Linq;
using System.Xml.Linq;
using HungerAndHavoc.Core;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_SaveCleanupTests
    {
        [Fact]
        public void ExportRemovesOnlyRatkinProviderPartition()
        {
            XDocument document = XDocument.Parse("<savegame><meta><modIds><li>nanaloveyuki.oxygendsl</li><li>nanaloveyuki.ratkin.hungerandhavoc</li></modIds><modNames><li>Oxygen</li><li>Ratkin</li></modNames></meta><game><components><li Class='Oxygendsl.Mod.GameComponent_Oxygendsl'><eventChainSchemaVersion>1</eventChainSchemaVersion><eventChainProviders><li><providerId>nanaloveyuki.ratkin.hungerandhavoc</providerId><instances><li><id>7</id></li></instances></li><li><providerId>foreign.provider</providerId><instances><li><payload>RHAH_Unknown</payload><reference>RHAH_HungerMark</reference><data Class='HungerAndHavoc.Future.UnknownRecord'>foreign bytes</data></li></instances></li></eventChainProviders><enabled>true</enabled></li><li Class='ForeignComponent'><eventChainProviders><li><providerId>nanaloveyuki.ratkin.hungerandhavoc</providerId><payload>unrelated field</payload></li></eventChainProviders></li></components></game></savegame>");
            XDocument original = new XDocument(document);
            XElement framework = document.Root.Element("game").Element("components").Elements("li").First();
            string foreign = framework.Element("eventChainProviders").Elements("li").Last().ToString();
            string unrelated = document.Root.Element("game").Element("components").Elements("li").Last().ToString();

            new RHAH_SaveCleanup(Plan()).Clean(document);

            Assert.Equal("Oxygendsl.Mod.GameComponent_Oxygendsl", (string)framework.Attribute("Class"));
            Assert.Equal("1", framework.Element("eventChainSchemaVersion").Value);
            Assert.Equal("true", framework.Element("enabled").Value);
            Assert.Equal(foreign, framework.Element("eventChainProviders").Elements("li").Single().ToString());
            Assert.Equal(unrelated, document.Root.Element("game").Element("components").Elements("li").Last().ToString());
            Assert.Contains("nanaloveyuki.oxygendsl", document.Root.Element("meta").Element("modIds").Value);
            Assert.DoesNotContain("nanaloveyuki.ratkin.hungerandhavoc", document.Root.Element("meta").Element("modIds").Value);
            Assert.Equal(2, original.Root.Element("game").Element("components").Elements("li").First().Element("eventChainProviders").Elements("li").Count());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExportRemovesOnlyOwnedSpokenThoughtStagesAndTheirTicks(bool worldPawn)
        {
            XElement cache = XElement.Parse(@"<SpokenThoughtTicks><keys>
<li>RHAH_Thought_GrainGreed_0</li><li>RHAH_Thought_GrainGreed_1</li>
<li>Foreign_Thought_0</li><li>RHAH_Thought_BeggingRejected_0</li><li>Foreign_Thought_1</li>
</keys><values><li>10</li><li>20</li><li>30</li><li>40</li><li>50</li></values></SpokenThoughtTicks>");
            XElement hediff = new XElement("li", new XAttribute("Class", "Foreign.SpeechHediff"),
                new XElement("def", "ForeignSpeech"), new XElement("loadID", "71"), cache);
            XElement pawn = new XElement(worldPawn ? "li" : "thing", new XElement("id", "11"),
                new XElement("healthTracker", new XElement("hediffSet", new XElement("hediffs", hediff))));
            XElement owner = worldPawn
                ? new XElement("world", new XElement("worldPawns", new XElement("pawnsAlive", pawn)))
                : new XElement("maps", new XElement("li", new XElement("things", pawn)));
            XDocument document = new XDocument(new XElement("savegame", new XElement("game", owner)));
            RHAH_SaveCleanupPlan plan = Plan();
            plan.ThoughtStageKeys.UnionWith(new[]
            {
                "RHAH_Thought_GrainGreed_0", "RHAH_Thought_GrainGreed_1", "RHAH_Thought_BeggingRejected_0"
            });

            new RHAH_SaveCleanup(plan).Clean(document);

            Assert.Equal(new[] { "Foreign_Thought_0", "Foreign_Thought_1" },
                cache.Element("keys").Elements("li").Select(item => item.Value));
            Assert.Equal(new[] { "30", "50" }, cache.Element("values").Elements("li").Select(item => item.Value));
            Assert.Equal("Foreign.SpeechHediff", (string)hediff.Attribute("Class"));
            Assert.Equal("ForeignSpeech", (string)hediff.Element("def"));
            Assert.Equal("71", (string)hediff.Element("loadID"));
            Assert.Equal("11", (string)pawn.Element("id"));
            string once = document.ToString();
            new RHAH_SaveCleanup(plan).Clean(document);
            Assert.Equal(once, document.ToString());
        }

        [Theory]
        [InlineData("RHAH_Thought_GrainGreed_99", true)]
        [InlineData("RHAH_Thought_Unknown_0", true)]
        [InlineData("RHAH_Thought_GrainGreed_0", false)]
        public void UnknownOrMisplacedSpokenThoughtStageStillAborts(string key, bool inHediff)
        {
            XElement cache = new XElement("SpokenThoughtTicks", new XElement("keys", new XElement("li", key)),
                new XElement("values", new XElement("li", "123")));
            XElement content = inHediff ? new XElement("hediffs", new XElement("li", cache)) : cache;
            XDocument document = new XDocument(new XElement("savegame", new XElement("game", content)));
            RHAH_SaveCleanupPlan plan = Plan();
            plan.ThoughtStageKeys.Add("RHAH_Thought_GrainGreed_0");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => new RHAH_SaveCleanup(plan).Clean(document));

            Assert.Contains(key, error.Message);
            Assert.Equal(key, cache.Element("keys").Element("li").Value);
            Assert.Equal("123", cache.Element("values").Element("li").Value);
        }

        [Fact]
        public void ExportRemovesShortStayLocksButKeepsForeignLocks()
        {
            XDocument document = XDocument.Parse("<savegame><game><pawn><apparel><lockedApparel><li>Thing_1</li><li>Thing_2</li></lockedApparel></apparel><healthTracker><hediffSet><hediffs><li><def>RHAH_HungerMark</def><shortStayLockedApparel><li>Thing_2</li></shortStayLockedApparel></li></hediffs></hediffSet></healthTracker></pawn></game></savegame>");

            new RHAH_SaveCleanup(Plan()).Clean(document);

            Assert.Equal("Thing_1", document.Descendants("lockedApparel").Single().Elements("li").Single().Value);
            Assert.Empty(document.Descendants("shortStayLockedApparel"));
        }
        [Fact]
        public void RemovesOwnedNodesAndKeepsForeignIdentity()
        {
            RHAH_SaveCleanup cleanup = new RHAH_SaveCleanup(Plan());
            XDocument document = Sample();

            cleanup.Clean(document);

            XElement game = document.Root.Element("game");
            Assert.Single(game.Element("components").Elements());
            Assert.Equal("ForeignComponent", game.Element("components").Element("li").Attribute("Class").Value);
            Assert.Equal("RatkinColonist", Pawn(document, "11").Element("kindDef").Value);
            Assert.Equal("11", Pawn(document, "11").Element("id").Value);
            Assert.Equal("22", Pawn(document, "22").Element("id").Value);
            Assert.Equal("Colonist", Pawn(document, "22").Element("kindDef").Value);
            Assert.Equal("Ancients", Only(document, "factionDef").Value);
            Assert.Equal("99", FactionLoadId(document));
            Assert.Equal("Baseliner", Only(document, "xenotype").Value);
            Assert.Equal("ForeignGene", Only(Pawn(document, "11").Element("genes").Element("endogenes"), "def").Value);
            Assert.Equal("Ratkin_Kid", Pawn(document, "11").Element("story").Element("childhood").Value);
            Assert.Equal("Ratkin_Farmer", Pawn(document, "11").Element("story").Element("adulthood").Value);
            Assert.Equal("Randy", Only(document, "storytellerDef").Value);
            Assert.False(HasValue(document, "def", "RHAH_HungerMark"));
            Assert.False(HasValue(document, "def", "RHAH_Trait_HardyLabor"));
            Assert.False(HasValue(document, "def", "RHAH_Gene_ThinRations"));
            Assert.False(HasClass(document, "HungerAndHavoc.Pawn.Area_RHAH_Relief"));
            Assert.False(HasClass(document, "HungerAndHavoc.Pawn.LordJob_RHAH_Visitor"));
            Assert.False(HasNamed(document, "curJob"));
            Assert.False(HasQueuedJob(document));
            Assert.False(HasValue(document, "target", "Thing_18"));
            Assert.True(HasValue(document, "target", "Thing_19"));
            Assert.Equal(1, CountChildren(document, "keys"));
            Assert.Equal(1, CountChildren(document, "values"));
            Assert.Equal("Thing_19", FirstChild(FirstNamed(document, "keys")).Value);
            Assert.True(cleanup.RemovedEntries > 0);
            Assert.True(cleanup.ReplacedDefs > 0);
        }

        [Fact]
        public void ConvertsAMappedCampAndDeletesAnEmptyOne()
        {
            RHAH_SaveCleanup cleanup = new RHAH_SaveCleanup(Plan());
            XDocument document = Sample();

            cleanup.Clean(document);

            XElement mapped = WorldObject(document, "30");
            Assert.Equal("RimWorld.Planet.Site", (string)mapped.Attribute("Class"));
            Assert.Equal("Site", (string)mapped.Element("def"));
            Assert.Equal("30", (string)mapped.Element("ID"));
            Assert.Equal("WorldObject_30", (string)MapParent(document, "30"));
            Assert.Equal("Encounter", (string)MapGenerator(document, "30"));
            Assert.Null(mapped.Element("parts"));
            Assert.Equal("Thing_11", (string)mapped.Element("residents").Element("li"));
            Assert.Null(FindBareId(document, "31"));
            XElement record = WorldObject(document, "32");
            Assert.Equal("Site", (string)record.Element("def"));
            Assert.Equal("32", (string)record.Element("ID"));
            Assert.Equal("WorldObject_32", (string)MapParent(document, "32"));
            Assert.Equal("Encounter", (string)MapGenerator(document, "32"));
            Assert.Null(record.Element("parts"));
            Assert.Null(FindBareId(document, "33"));
            Assert.Equal("Base_Player", (string)MapGenerator(document, "90"));
            Assert.Equal("WorldObject_90", (string)MapParent(document, "90"));
            Assert.Equal("ForeignThing", (string)document.Descendants("things").Elements()
                .First(item => (string)item.Element("id") == "77").Element("def"));
        }

        [Fact]
        public void InterceptionExportKeepsColonistsAndRetainedCaravanData()
        {
            XDocument document = XDocument.Parse(@"<savegame><game>
<world><worldObjects><worldObjects>
<li Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_Interception""><def>RHAH_Interception</def><ID>91</ID>
<displayId></displayId><spawnBatchId>0</spawnBatchId><settled>False</settled></li>
<li Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_Approach""><def>RHAH_Approach</def><ID>92</ID></li>
<li Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_Interception""><def>RHAH_Interception</def><ID>93</ID></li>
<li Class=""RimWorld.Planet.CaravansBattlefield""><def>AttackedNonPlayerCaravan</def><ID>94</ID></li>
</worldObjects></worldObjects></world>
<maps><li><mapInfo><parent>WorldObject_91</parent></mapInfo><generatorDef>Encounter</generatorDef>
<retainedCaravanData><name>Homeward</name><outfits><li>Outfit_2</li></outfits></retainedCaravanData>
<components><li Class=""HungerAndHavoc.Core.MapComponent_RHAH_Map"" /></components>
<things>
<li Class=""Verse.Pawn""><def>Human</def><id>100</id><kindDef>Colonist</kindDef><faction>Faction_1</faction></li>
<li Class=""Verse.Pawn""><def>Ratkin</def><id>101</id><kindDef>RHAH_PawnKind_Ratkin</kindDef>
<healthTracker><hediffSet><hediffs><li><def>RHAH_HungerMark</def></li></hediffs></hediffSet></healthTracker></li>
</things>
<lordManager><lords><li><loadID>3</loadID><lordJob Class=""HungerAndHavoc.Pawn.LordJob_RHAH_Intercept""><exitCell>(-1, -1, -1)</exitCell></lordJob></li></lords></lordManager>
</li></maps></game></savegame>");
            XElement map = document.Descendants("maps").Single().Element("li");
            string colonist = Pawn(document, "100").ToString();
            string retained = map.Element("retainedCaravanData").ToString();

            new RHAH_SaveCleanup(Plan()).Clean(document);

            XElement parent = WorldObject(document, "91");
            Assert.Equal("RimWorld.Planet.Site", (string)parent.Attribute("Class"));
            Assert.Equal("Site", (string)parent.Element("def"));
            Assert.Equal("91", (string)parent.Element("ID"));
            Assert.Null(parent.Element("displayId"));
            Assert.Null(parent.Element("spawnBatchId"));
            Assert.Null(parent.Element("settled"));
            Assert.Equal("WorldObject_91", (string)map.Element("mapInfo").Element("parent"));
            Assert.Equal("Encounter", (string)map.Element("generatorDef"));
            Assert.Equal(colonist, Pawn(document, "100").ToString());
            Assert.Equal(retained, map.Element("retainedCaravanData").ToString());
            Assert.Equal("RatkinColonist", (string)Pawn(document, "101").Element("kindDef"));
            Assert.Null(FindBareId(document, "92"));
            Assert.Null(FindBareId(document, "93"));
            Assert.Equal("RimWorld.Planet.CaravansBattlefield", (string)WorldObject(document, "94").Attribute("Class"));
            Assert.Equal("AttackedNonPlayerCaravan", (string)WorldObject(document, "94").Element("def"));
            Assert.Empty(map.Element("components").Elements());
            Assert.Empty(map.Element("lordManager").Element("lords").Elements());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void KingdomSiteExportKeepsEverySupplyStackInWorldPawnInventories(bool multipleRecipients)
        {
            XDocument original = KingdomSupplySave();
            XElement recipients = WorldObject(original, "201").Element("kingdomRecipients");
            if (!multipleRecipients)
            {
                recipients.Elements("li").Last().Remove();
            }
            string originalXml = original.ToString();
            XDocument document = new XDocument(original);
            XElement worldPawns = document.Root.Element("game").Element("world").Element("worldPawns").Element("pawnsAlive");
            XElement unrelated = Pawn(document, "53");
            string unrelatedXml = unrelated.ToString();
            XElement[] supplies = WorldObject(document, "201").Element("kingdomSupply").Elements("li")
                .Where(item => (string)item.Element("def") == "RawRice").ToArray();
            string[] supplyXml = supplies.Select(item => item.ToString()).ToArray();

            new RHAH_SaveCleanup(Plan()).Clean(document);

            Assert.Equal(originalXml, original.ToString());
            Assert.Equal(new[] { "51", "52", "53" }, worldPawns.Elements("li").Select(item => (string)item.Element("id")));
            Assert.Equal(unrelatedXml, unrelated.ToString());
            Assert.Null(FindBareId(document, "201"));
            Assert.DoesNotContain(document.Descendants("def"), item => item.Value == "RHAH_KingdomMaterial");
            XElement[] inventories = worldPawns.Elements("li").Select(item => item.Element("inventory").Element("innerContainer")).ToArray();
            Assert.All(inventories, item => Assert.Empty(item.Elements("li")));
            XElement[] grain = inventories.SelectMany(item => item.Element("innerList").Elements("li"))
                .Where(item => (string)item.Element("def") == "RawRice").ToArray();
            Assert.Equal(3, grain.Length);
            Assert.Equal(200, grain.Sum(item => (int)item.Element("stackCount")));
            for (int i = 0; i < supplies.Length; i++)
            {
                XElement stack = document.Descendants("id").Single(item => item.Value == (string)supplies[i].Element("id")).Parent;
                Assert.Same(supplies[i], stack);
                Assert.Equal(supplyXml[i], stack.ToString());
                string recipientId = multipleRecipients && i % 2 == 1 ? "52" : "51";
                Assert.Same(Pawn(document, recipientId).Element("inventory").Element("innerContainer").Element("innerList"), stack.Parent);
            }
            Assert.Equal("10", (string)Pawn(document, "51").Descendants("stackCount").First());
            string once = document.ToString();
            new RHAH_SaveCleanup(Plan()).Clean(document);
            Assert.Equal(once, document.ToString());
        }

        [Theory]
        [InlineData("worldPawn")]
        [InlineData("inventory")]
        [InlineData("innerContainer")]
        [InlineData("innerList")]
        [InlineData("nullInnerList")]
        [InlineData("nullInventory")]
        [InlineData("nullInnerContainer")]
        public void KingdomSiteExportSkipsUnsafeRecipientsOrAbortsWithoutLosingSupply(string unavailable)
        {
            XDocument document = KingdomSupplySave();
            XElement first = Pawn(document, "51");
            if (unavailable == "worldPawn")
            {
                first.Remove();
            }
            else
            {
                string element = unavailable == "nullInnerList" ? "innerList" :
                    unavailable == "nullInventory" ? "inventory" : unavailable == "nullInnerContainer" ? "innerContainer" : unavailable;
                XElement target = first.Descendants(element).Single();
                if (unavailable.StartsWith("null", StringComparison.Ordinal))
                {
                    target.SetAttributeValue("IsNull", "True");
                    target.RemoveNodes();
                }
                else
                {
                    target.Remove();
                }
            }
            XDocument withoutSafeRecipient = new XDocument(document);
            WorldObject(withoutSafeRecipient, "201").Element("kingdomRecipients").Elements("li").Last().Remove();
            string supplyBefore = WorldObject(withoutSafeRecipient, "201").Element("kingdomSupply").ToString();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => new RHAH_SaveCleanup(Plan()).Clean(withoutSafeRecipient));

            Assert.Contains("kingdomSupply", error.Message);
            Assert.Contains("201", error.Message);
            Assert.Equal(supplyBefore, WorldObject(withoutSafeRecipient, "201").Element("kingdomSupply").ToString());

            new RHAH_SaveCleanup(Plan()).Clean(document);

            XElement[] grain = Pawn(document, "52").Element("inventory").Element("innerContainer").Element("innerList").Elements("li").ToArray();
            Assert.Equal(new[] { "701", "702", "703" }, grain.Select(item => (string)item.Element("id")));
            Assert.Equal(200, grain.Sum(item => (int)item.Element("stackCount")));
            Assert.Null(FindBareId(document, "201"));
        }

        [Fact]
        public void SecondPassDoesNotChangeTheDocument()
        {
            RHAH_SaveCleanup first = new RHAH_SaveCleanup(Plan());
            XDocument document = Sample();
            first.Clean(document);
            string once = document.ToString();

            new RHAH_SaveCleanup(Plan()).Clean(document);

            Assert.Equal(once, document.ToString());
        }

        [Fact]
        public void UnknownOwnedScalarAborts()
        {
            XDocument document = XDocument.Parse(
                "<savegame><game><unknown>RHAH_Unknown</unknown></game></savegame>");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => new RHAH_SaveCleanup(Plan()).Clean(document));

            Assert.Contains("RHAH_Unknown", error.Message);
        }

        [Fact]
        public void UnknownHungerAndHavocTypeAborts()
        {
            XDocument document = XDocument.Parse(
                "<savegame><game><li Class=\"HungerAndHavoc.Future.UnknownRecord\"><id>1</id></li></game></savegame>");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => new RHAH_SaveCleanup(Plan()).Clean(document));

            Assert.Contains("HungerAndHavoc.Future.UnknownRecord", error.Message);
            Assert.NotNull(document.Descendants().First(item =>
                (string)item.Attribute("Class") == "HungerAndHavoc.Future.UnknownRecord"));
        }

        [Fact]
        public void MissingReplacementAbortsBeforeWritingAName()
        {
            RHAH_SaveCleanupPlan plan = Plan();
            plan.Replacements.Remove("RHAH_PawnKind_Ratkin");
            XDocument document = Sample();

            Assert.Throws<InvalidOperationException>(() => new RHAH_SaveCleanup(plan).Clean(document));
            Assert.Equal("RHAH_PawnKind_Ratkin", Pawn(document, "11").Element("kindDef").Value);
        }

        static XDocument KingdomSupplySave()
        {
            return XDocument.Parse(@"<savegame><game><world>
<worldPawns><pawnsAlive>
<li Class=""Verse.Pawn""><def>Ratkin</def><id>51</id><kindDef>RatkinColonist</kindDef>
<inventory><innerContainer><innerList><li><def>Silver</def><id>700</id><stackCount>10</stackCount></li></innerList></innerContainer></inventory></li>
<li Class=""Verse.Pawn""><def>Ratkin</def><id>52</id><kindDef>RatkinColonist</kindDef>
<inventory><innerContainer><innerList /></innerContainer></inventory></li>
<li Class=""Verse.Pawn""><def>Human</def><id>53</id><kindDef>Tribal</kindDef>
<inventory><innerContainer><innerList /></innerContainer></inventory></li>
</pawnsAlive></worldPawns>
<worldObjects><worldObjects><li Class=""HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite""><def>RHAH_KingdomSite</def><ID>201</ID>
<kingdomRecipients><li>Thing_999</li><li>Thing_51</li><li>Thing_51</li><li>Thing_52</li></kingdomRecipients>
<kingdomSupply>
<li><def>RHAH_KingdomMaterial</def><id>704</id><stackCount>1</stackCount></li>
<li><def>RawRice</def><id>701</id><stackCount>100</stackCount></li>
<li><def>RawRice</def><id>702</id><stackCount>60</stackCount></li>
<li><def>RawRice</def><id>703</id><stackCount>40</stackCount></li>
</kingdomSupply></li></worldObjects></worldObjects>
</world></game></savegame>");
        }

        static RHAH_SaveCleanupPlan Plan()
        {
            RHAH_SaveCleanupPlan plan = new RHAH_SaveCleanupPlan
            {
                PackageId = "nanaloveyuki.ratkin.hungerandhavoc"
            };
            string[] defs =
            {
                "RHAH_PawnKind_Ratkin", "RHAH_Faction_Neutral", "RHAH_History_A001", "RHAH_History_Y001",
                "RHAH_Xenotype_Ratkin", "RHAH_Suiyin", "RHAH_HungerMark", "RHAH_Trait_HardyLabor",
                "RHAH_Gene_ThinRations", "RHAH_GuanyinTu", "RHAH_Beg", "RHAH_RefugeeMassacre",
                "RHAH_RefugeeCamp", "RHAH_RecordSite", "RHAH_Approach", "RHAH_Interception", "RHAH_ChoiceRequest",
                "RHAH_KingdomSite", "RHAH_KingdomMaterial"
            };
            for (int i = 0; i < defs.Length; i++)
            {
                plan.OwnedDefs.Add(defs[i]);
            }

            plan.ThingDefs.Add("RHAH_GuanyinTu");
            plan.ThingDefs.Add("RHAH_KingdomMaterial");
            plan.OwnedClasses.Add("HungerAndHavoc.Storyteller.Suiyin.WorldObject_RHAH_KingdomSite");
            plan.OwnedClasses.Add("HungerAndHavoc.Core.GameComponent_RHAH_Game");
            plan.OwnedClasses.Add("HungerAndHavoc.Narrative.NarrativeState");
            plan.OwnedClasses.Add("HungerAndHavoc.Core.MapComponent_RHAH_Map");
            plan.OwnedClasses.Add("HungerAndHavoc.Pawn.LordJob_RHAH_Visitor");
            plan.OwnedClasses.Add("HungerAndHavoc.Pawn.LordJob_RHAH_Intercept");
            plan.OwnedClasses.Add("HungerAndHavoc.Pawn.JobDriver_RHAH_Beg");
            plan.OwnedClasses.Add("HungerAndHavoc.Pawn.Area_RHAH_Relief");
            plan.OwnedClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp");
            plan.OwnedClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_Approach");
            plan.OwnedClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_Interception");
            plan.OwnedClasses.Add("HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Request");
            plan.Replacements.Add("RHAH_PawnKind_Ratkin", "RatkinColonist");
            plan.Replacements.Add("RHAH_Faction_Neutral", "Ancients");
            plan.Replacements.Add("RHAH_History_Y001", "Ratkin_Kid");
            plan.Replacements.Add("RHAH_History_A001", "Ratkin_Farmer");
            plan.Replacements.Add("RHAH_Xenotype_Ratkin", "Baseliner");
            plan.Replacements.Add("RHAH_Suiyin", "Randy");
            plan.GeneDefs.Add("RHAH_Gene_ThinRations");
            plan.CampObjectDefs.Add("RHAH_RefugeeCamp");
            plan.CampObjectDefs.Add("RHAH_RecordSite");
            plan.CampObjectDefs.Add("RHAH_Interception");
            plan.CampObjectClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp");
            plan.CampObjectClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_Interception");
            plan.MapGeneratorReplacements.Add("RHAH_RefugeeCamp", "Encounter");
            plan.MapGeneratorReplacements.Add("RHAH_RecordSite", "Encounter");
            return plan;
        }
        static XElement Pawn(XDocument document, string id)
        {
            foreach (XElement kind in document.Descendants("kindDef"))
            {
                if ((string)kind.Parent.Element("id") == id)
                {
                    return kind.Parent;
                }
            }

            throw new InvalidOperationException("missing pawn " + id);
        }

        static XElement WorldObject(XDocument document, string id)
        {
            foreach (XElement node in document.Descendants())
            {
                if (node.Element("ID") != null && (string)node.Element("ID") == id && node.Element("def") != null)
                {
                    return node;
                }
            }

            throw new InvalidOperationException("missing world object " + id);
        }

        static XElement MapParent(XDocument document, string worldObjectId)
        {
            foreach (XElement parent in document.Descendants("parent"))
            {
                if ((string)parent == "WorldObject_" + worldObjectId && parent.Parent?.Name == "mapInfo")
                {
                    return parent;
                }
            }

            throw new InvalidOperationException("missing map parent " + worldObjectId);
        }

        static XElement MapGenerator(XDocument document, string worldObjectId)
        {
            XElement parent = MapParent(document, worldObjectId);
            XElement generator = parent.Parent?.Parent?.Element("generatorDef");
            if (generator == null)
            {
                throw new InvalidOperationException("missing generator " + worldObjectId);
            }

            return generator;
        }

        static XElement Only(XContainer container, string name)
        {
            XElement found = null;
            foreach (XElement node in container.Descendants(name))
            {
                if (found != null)
                {
                    throw new InvalidOperationException("more than one " + name);
                }

                found = node;
            }

            if (found == null)
            {
                throw new InvalidOperationException("missing " + name);
            }

            return found;
        }

        static string FactionLoadId(XDocument document)
        {
            foreach (XElement node in document.Descendants("loadID"))
            {
                if (node.Parent.Name == "faction" || node.Parent.Name == "li" && node.Parent.Parent?.Name == "factions")
                {
                    return node.Value;
                }
            }

            throw new InvalidOperationException("missing faction");
        }

        static bool HasValue(XDocument document, string name, string value)
        {
            foreach (XElement node in document.Descendants(name))
            {
                if (node.Value == value)
                {
                    return true;
                }
            }

            return false;
        }

        static bool HasClass(XDocument document, string className)
        {
            foreach (XElement node in document.Descendants())
            {
                if ((string)node.Attribute("Class") == className)
                {
                    return true;
                }
            }

            return false;
        }
        static bool HasNamed(XDocument document, string name)
        {
            foreach (XElement node in document.Descendants(name))
            {
                return true;
            }

            return false;
        }

        static bool HasQueuedJob(XDocument document)
        {
            foreach (XElement node in document.Descendants("jobs"))
            {
                foreach (XElement child in node.Elements())
                {
                    if (child.Name == "li")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool HasChild(XDocument document, string name)
        {
            foreach (XElement node in document.Descendants(name))
            {
                if (node.Elements().GetEnumerator().MoveNext())
                {
                    return true;
                }
            }

            return false;
        }

        static int CountChildren(XDocument document, string name)
        {
            foreach (XElement node in document.Descendants(name))
            {
                int count = 0;
                foreach (XElement child in node.Elements())
                {
                    count++;
                }

                return count;
            }

            return 0;
        }

        static XElement FirstChild(XElement parent)
        {
            foreach (XElement child in parent.Elements())
            {
                return child;
            }

            return null;
        }
        static XElement FirstNamed(XDocument document, string name)
        {
            foreach (XElement node in document.Descendants(name))
            {
                return node;
            }

            return null;
        }

        static XElement FindBareId(XDocument document, string id)
        {
            foreach (XElement node in document.Descendants())
            {
                if ((string)node.Element("ID") == id)
                {
                    return node;
                }
            }

            return null;
        }
        static string Dump(XDocument document, string name)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (XElement node in document.Descendants(name))
            {
                text.Append(node.ToString());
            }

            return text.ToString();
        }

        static XDocument Sample()
        {
            return XDocument.Parse(@"<savegame>
<meta>
  <modIds><li>nanaloveyuki.ratkin.hungerandhavoc</li><li>Foreign</li></modIds>
  <modNames><li>Hunger</li><li>Foreign</li></modNames>
  <modSteamIds><li></li><li></li></modSteamIds>
</meta>
<game>
  <components>
    <li Class=""HungerAndHavoc.Core.GameComponent_RHAH_Game""><newContentDisabled>True</newContentDisabled></li>
    <li Class=""HungerAndHavoc.Narrative.NarrativeState"" />
    <li Class=""ForeignComponent"" />
  </components>
  <storytellerDef>RHAH_Suiyin</storytellerDef>
  <worldObjects>
    <worldObject Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp"">
      <def>RHAH_RefugeeCamp</def><ID>30</ID>
      <parts><li><def>RHAH_RefugeeCamp</def></li></parts>
      <residents><li>Thing_11</li></residents>
    </worldObject>
    <worldObject Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp"">
      <def>RHAH_RefugeeCamp</def><ID>31</ID>
    </worldObject>
    <worldObject Class=""RimWorld.Planet.Site"">
      <def>RHAH_RecordSite</def><ID>32</ID>
      <parts><li><def>RHAH_RecordSite</def></li></parts>
    </worldObject>
    <worldObject Class=""RimWorld.Planet.Site"">
      <def>RHAH_RecordSite</def><ID>33</ID>
    </worldObject>
    <worldObject Class=""RimWorld.Planet.Settlement"">
      <def>Settlement</def><ID>90</ID>
    </worldObject>
    <worldObject Class=""HungerAndHavoc.Incidents.WorldObject_RHAH_Approach"">
      <def>RHAH_Approach</def><ID>40</ID>
    </worldObject>
  </worldObjects>
  <maps>
    <li>
      <uniqueID>8</uniqueID>
      <generatorDef>RHAH_RefugeeCamp</generatorDef>
      <mapInfo><size>(250, 1, 250)</size><parent>WorldObject_30</parent></mapInfo>
      <components><li Class=""HungerAndHavoc.Core.MapComponent_RHAH_Map"" /></components>
      <areas><li Class=""HungerAndHavoc.Pawn.Area_RHAH_Relief""><ID>7</ID></li><li Class=""Verse.Area_Home""><ID>1</ID></li></areas>
      <lords><li><loadID>4</loadID><lordJob Class=""HungerAndHavoc.Pawn.LordJob_RHAH_Visitor"" /></li></lords>
    </li>
    <li>
      <uniqueID>9</uniqueID>
      <generatorDef>RHAH_RecordSite</generatorDef>
      <mapInfo><size>(200, 1, 200)</size><parent>WorldObject_32</parent></mapInfo>
    </li>
    <li>
      <uniqueID>10</uniqueID>
      <generatorDef>Base_Player</generatorDef>
      <mapInfo><size>(250, 1, 250)</size><parent>WorldObject_90</parent></mapInfo>
      <things><thing><def>ForeignThing</def><id>77</id></thing></things>
    </li>
  </maps>
  <worldPawns>
    <li>
      <kindDef>RHAH_PawnKind_Ratkin</kindDef><id>11</id>
      <faction>Faction_99</faction>
      <story><childhood>RHAH_History_Y001</childhood><adulthood>RHAH_History_A001</adulthood></story>
      <xenotype>RHAH_Xenotype_Ratkin</xenotype>
      <genes>
        <endogenes><li><def>ForeignGene</def><loadID>3</loadID></li><li><def>RHAH_Gene_ThinRations</def><loadID>8</loadID></li></endogenes>
      </genes>
      <hediffSet><hediffs><li><def>RHAH_HungerMark</def><loadID>5</loadID></li><li><def>Cut</def><loadID>6</loadID></li></hediffs></hediffSet>
      <storyTraits><allTraits><li><def>RHAH_Trait_HardyLabor</def></li><li><def>Kind</def></li></allTraits></storyTraits>
      <jobs><curJob><def>RHAH_Beg</def><loadID>9</loadID></curJob><curDriver Class=""HungerAndHavoc.Pawn.JobDriver_RHAH_Beg"" /><jobs><li><def>RHAH_Beg</def></li></jobs></jobs>
    </li>
    <li><kindDef>Colonist</kindDef><id>22</id><faction>Faction_Player</faction></li>
  </worldPawns>
  <factions><li><def>RHAH_Faction_Neutral</def><loadID>99</loadID><factionDef>RHAH_Faction_Neutral</factionDef></li></factions>
  <letters><li Class=""HungerAndHavoc.Incidents.ChoiceLetter_RHAH_Request""><def>RHAH_ChoiceRequest</def><ID>12</ID></li></letters>
  <quests><li><id>15</id><root>RHAH_RefugeeMassacre</root><parts><li /></parts></li><li><id>16</id><root>Hospitality</root></li></quests>
  <things><thing><def>RHAH_GuanyinTu</def><id>18</id></thing><thing><def>MealSimple</def><id>19</id></thing></things>
  <reservations><li><target>Thing_18</target></li><li><target>Thing_19</target></li></reservations>
  <dictionary><keys><li>Thing_18</li><li>Thing_19</li><li>Area_7_RHAH_Relief</li></keys><values><li>a</li><li>b</li><li>c</li></values></dictionary>
</game>
</savegame>");
        }
    }
}
