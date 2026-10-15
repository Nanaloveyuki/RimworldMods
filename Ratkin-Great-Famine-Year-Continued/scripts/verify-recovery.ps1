param([string[]]$SavePaths = @())
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSaveCleanup.cs') -Raw
$harness = @'
namespace MouseDisaster {
public static class RecoveryHarness {
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new System.Exception(name); checks++; }
    private static MouseDisasterCleanupPlan Plan() {
        var p = new MouseDisasterCleanupPlan { PackageId = "test.mod" };
        p.LegacyPackageIds.Add("legacy.test.mod");
        p.OwnedDefs.UnionWith(new[] { "ModTrait", "ModGene", "ModKind", "ModStory", "ModItem", "ModIncident", "ModHediff", "ModDuty" });
        p.OwnedClasses.UnionWith(new[] { "Mod.Component", "Mod.Lord", "Mod.Driver", "Mod.Letter" });
        p.Replacements.Add("ModKind", "RaceKind"); p.Replacements.Add("ModStory", "CoreStory");
        p.ThingDefs.Add("ModItem"); return p;
    }
    public static int Run() {
        var doc = System.Xml.Linq.XDocument.Parse(@"<savegame><meta><modIds><li>core</li><li>test.mod</li><li>legacy.test.mod</li><li>race</li></modIds><modNames><li>Core</li><li>Mod</li><li>Legacy Mod</li><li>Race</li></modNames></meta><game>
<components><li Class='Mod.Component'><nested>ModDuty</nested></li><li Class='Other.Component'/></components>
<world><pawns><pawn><kindDef>ModKind</kindDef><story><childhood>ModStory</childhood><traits><allTraits><li><def>ModTrait</def></li><li><def>Kind</def></li></allTraits></traits></story>
<genes><xenogenes><li><def>ModGene</def><loadID>7</loadID></li></xenogenes></genes><sourceGene>Gene_7</sourceGene>
<jobs><curJob><def>Wait</def></curJob><curDriver Class='Mod.Driver'/></jobs><mindState><duty><def>ModDuty</def></duty></mindState>
<inventory><innerList><li><def>ModItem</def><id>ModItem9</id></li><li><def>Rice</def><id>Rice10</id></li></innerList></inventory></pawn></pawns></world>
<lordManager><lords><li><lordJob Class='Mod.Lord'/><ownedPawns><li>Thing_Ratkin1</li></ownedPawns></li></lords></lordManager>
<history><archive><archivables><li Class='Mod.Letter'><ID>8</ID><def>NeutralEvent</def></li></archivables></archive></history><letters><li>Letter_8</li></letters><worldObjectRef>WorldObject_8</worldObjectRef>
<filters><allowedDefs><li>ModItem</li><li>Rice</li></allowedDefs></filters><stats><keys><li>ModIncident</li><li>RaidEnemy</li></keys><values><li>4</li><li>9</li></values></stats>
<target>Thing_ModItem9</target><otherTarget>Thing_Ratkin1</otherTarget><label>ModTrait is descriptive text, not a Def reference</label>
</game></savegame>");
        var cleanup = new MouseDisasterSaveCleanup(Plan()); cleanup.Clean(doc);
        var game = doc.Root.Element("game");
        Check(game.Element("components").Elements().Count() == 1, "foreign component removed");
        Check(game.Descendants("kindDef").Single().Value == "RaceKind", "world pawn kind not replaced");
        Check(game.Descendants("childhood").Single().Value == "CoreStory", "backstory not replaced");
        Check(game.Descendants("allTraits").Single().Elements().Count() == 1, "traits not scoped");
        Check(game.Descendants("sourceGene").Single().Value == "null", "dangling gene reference");
        Check(!game.Descendants("curJob").Any() && !game.Descendants("curDriver").Any(), "driver/job mismatch");
        Check(!game.Descendants("duty").Any(), "custom duty retained");
        Check(game.Descendants("innerList").Single().Elements().Count() == 1, "foreign item removed");
        Check(!game.Descendants("lords").Single().Elements().Any(), "custom lord retained");
        Check(game.Descendants("letters").Single().Elements().Count() == 0, "letter reference retained");
        Check(game.Element("worldObjectRef").Value == "WorldObject_8", "letter ID confused with world object ID");
        Check(game.Element("stats").Element("keys").Elements().Count() == 1 && game.Element("stats").Element("values").Elements().Single().Value == "9", "parallel dictionary misaligned");
        Check(game.Element("target").Value == "null" && game.Element("otherTarget").Value == "Thing_Ratkin1", "thing references not scoped");
        Check(game.Document.Root.Element("meta").Element("modIds").Elements().Count() == 2, "legacy mod metadata retained");
        Check(doc.Root.Element("meta").Element("modNames").Elements().Last().Value == "Race", "mod metadata misaligned");
        string once = doc.ToString(); new MouseDisasterSaveCleanup(Plan()).Clean(doc); Check(doc.ToString() == once, "cleanup not idempotent");
        bool rejected = false;
        try { new MouseDisasterSaveCleanup(Plan()).Clean(System.Xml.Linq.XDocument.Parse("<savegame><game><unknown>ModTrait</unknown></game></savegame>")); }
        catch (System.InvalidOperationException) { rejected = true; }
        Check(rejected, "unknown scalar silently discarded");
        var extendedPlan = Plan();
        extendedPlan.OwnedDefs.UnionWith(new[] { "MouseDisaster_RefugeeCamp", "MouseDisaster_RefugeeMassacre", "MouseDisaster_MakeGuanyinTu" });
        extendedPlan.OwnedClasses.Add("MouseDisaster.MouseDisasterRefugeeSite");
        extendedPlan.Replacements.Add("MouseDisaster_RefugeeCamp", "PossibleUnknownThreatMarker");
        var extended = System.Xml.Linq.XDocument.Parse(@"<savegame><game>
<world><worldObjects><worldObjects><li Class='MouseDisaster.MouseDisasterRefugeeSite'><def>MouseDisaster_RefugeeCamp</def><ID>30</ID><tile>40</tile><refugeeResidents><li>Thing_Ratkin1</li></refugeeResidents><refugeeCleared>False</refugeeCleared><parts><li><def>MouseDisaster_RefugeeCamp</def></li></parts></li></worldObjects></worldObjects></world>
<maps><li><mapInfo><parent>WorldObject_30</parent></mapInfo><things><thing Class='Pawn'><id>Ratkin1</id><kindDef>ModKind</kindDef></thing></things></li></maps>
<questManager><quests><li><id>20</id><root>MouseDisaster_RefugeeMassacre</root><parts><li Class='QuestPart_SpawnWorldObject'><worldObject>WorldObject_30</worldObject></li></parts></li><li><id>21</id><root>MouseDisaster_RefugeeMassacre</root><parts><li Class='QuestPart_SpawnWorldObject'><worldObject Class='MouseDisaster.MouseDisasterRefugeeSite'><def>MouseDisaster_RefugeeCamp</def><ID>31</ID></worldObject></li></parts></li><li><id>22</id><root>OtherQuest</root></li></quests></questManager>
<questRef>Quest_20</questRef><partRef>QuestPart_20_0</partRef><pendingSiteRef>WorldObject_31</pendingSiteRef><foreignQuestRef>Quest_22</foreignQuestRef>
<billStack><bills><li Class='Bill_Production'><recipe>MouseDisaster_MakeGuanyinTu</recipe><loadID>9</loadID></li><li Class='Bill_Production'><recipe>MakeStoneBlocks</recipe><loadID>10</loadID></li></bills></billStack>
<pawnJobs><curJob><def>DoBill</def><loadID>41</loadID><bill>Bill_MouseDisaster_MakeGuanyinTu_9</bill></curJob><curDriver Class='JobDriver_DoBill'/><jobQueue><jobs><li><job><def>DoBill</def><bill>Bill_MouseDisaster_MakeGuanyinTu_9</bill></job></li></jobs></jobQueue></pawnJobs>
<reservationManager><reservations><li><job>Job_41</job></li></reservations></reservationManager>
</game></savegame>");
        new MouseDisasterSaveCleanup(extendedPlan).Clean(extended);
        var site = extended.Descendants().Single(n => (string)n.Element("ID") == "30");
        Check((string)site.Attribute("Class") == "RimWorld.Planet.Site" && (string)site.Element("def") == "Site", "site class/WorldObjectDef not converted");
        Check(site.Element("parts").Descendants("def").Single().Value == "PossibleUnknownThreatMarker", "same-named site part confused with world object");
        Check(extended.Descendants("parent").Single().Value == "WorldObject_30" && extended.Descendants("kindDef").Single().Value == "RaceKind", "visited map or resident lost");
        Check(!extended.Descendants("refugeeResidents").Any(), "custom site state retained");
        Check(extended.Descendants("quests").Single().Elements().Single().Element("id").Value == "22", "quest cleanup was not scoped");
        Check(extended.Descendants("questRef").Single().Value == "null" && extended.Descendants("partRef").Single().Value == "null" && extended.Descendants("pendingSiteRef").Single().Value == "null", "removed quest/part/pending site reference retained");
        Check(extended.Descendants("foreignQuestRef").Single().Value == "Quest_22", "foreign quest reference removed");
        Check(extended.Descendants("bills").Single().Elements().Single().Element("recipe").Value == "MakeStoneBlocks", "bill cleanup was not scoped");
        Check(!extended.Descendants("curJob").Any() && !extended.Descendants("curDriver").Any() && !extended.Descendants("jobs").Single().Elements().Any(), "removed bill still has running or queued jobs");
        Check(!extended.Descendants("reservations").Single().Elements().Any(), "removed bill job still reserved");
        string extendedOnce = extended.ToString(); new MouseDisasterSaveCleanup(extendedPlan).Clean(extended);
        Check(extended.ToString() == extendedOnce, "site/quest/bill cleanup not idempotent");
        return checks;
    }
    public static string InspectSave(string path, string root) {
        var p = new MouseDisasterCleanupPlan { PackageId = "nanaloveyuki.mouse.disaster.famine.continued" };
        p.LegacyPackageIds.UnionWith(new[] { "lezhizhong.mouse.disaster.famine", "local.mousedisaster.greatfamine" });
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Defs"), "*.xml", System.IO.SearchOption.AllDirectories))
        foreach (var d in System.Xml.Linq.XDocument.Load(file).Root.Elements()) {
            string id = (string)d.Element("defName"); if (id == null) continue; p.OwnedDefs.Add(id);
            switch (d.Name.LocalName) {
                case "PawnKindDef": p.Replacements[id] = "TEST_RACE_KIND"; break;
                case "BackstoryDef": p.Replacements[id] = "TEST_CORE_BACKSTORY"; break;
                case "FactionDef": p.Replacements[id] = "Ancients"; break;
                case "StorytellerDef": p.Replacements[id] = "Randy"; break;
                case "XenotypeDef": p.Replacements[id] = "Baseliner"; break;
                case "TraderKindDef": p.Replacements[id] = "Caravan_Outlander_BulkGoods"; break;
                case "SitePartDef": p.Replacements[id] = "PossibleUnknownThreatMarker"; break;
                case "ThingDef": p.ThingDefs.Add(id); break;
            }
        }
        var doc = System.Xml.Linq.XDocument.Load(path);
        foreach (var c in doc.Descendants().Attributes("Class").Select(a => a.Value.Split(',')[0]).Where(c => c.StartsWith("MouseDisaster.", System.StringComparison.Ordinal))) p.OwnedClasses.Add(c);
        var pawnIds = doc.Descendants("kindDef").Select(n => n.Parent).Where(n => n.Element("id") != null &&
            !n.Ancestors().Any(a => p.OwnedClasses.Contains(((string)a.Attribute("Class") ?? "").Split(',')[0])))
            .Select(n => n.Element("id").Value).OrderBy(id => id).ToArray();
        var cleanup = new MouseDisasterSaveCleanup(p); cleanup.Clean(doc);
        var remainingPawnIds = doc.Descendants("kindDef").Select(n => n.Parent).Where(n => n.Element("id") != null)
            .Select(n => n.Element("id").Value).OrderBy(id => id).ToArray();
        if (!pawnIds.SequenceEqual(remainingPawnIds)) throw new System.Exception("Cleanup changed non-pending pawn identities.");
        string once = doc.ToString(); new MouseDisasterSaveCleanup(p).Clean(doc);
        if (once != doc.ToString()) throw new System.Exception("Real-save cleanup is not idempotent.");
        return "PASS: read-only save schema cleanup; replaced " + cleanup.ReplacedDefs + ", removed " + cleanup.RemovedEntries + ". Replacement Def availability requires the game runtime.";
    }
}
}
'@
Add-Type -TypeDefinition ($source + $harness)
"PASS: $([MouseDisaster.RecoveryHarness]::Run()) serialized cleanup assertions."
foreach ($path in $SavePaths) { [MouseDisaster.RecoveryHarness]::InspectSave((Resolve-Path -LiteralPath $path).Path, $root) }
$translationKeys = @()
foreach ($language in 'ChineseSimplified','English') {
    $xml = [xml](Get-Content (Join-Path $root "Languages/$language/Keyed/MouseDisasterRecovery.xml") -Raw)
    $keys = @($xml.LanguageData.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object Name | Sort-Object)
    if ($translationKeys.Count -gt 0 -and (Compare-Object $translationKeys $keys)) { throw "Recovery translation key mismatch: $language" }
    $translationKeys = $keys
}
'PASS: recovery localization XML. In-game uninstall/load/save/reload remains required.'
