$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $root '1.6/Source'
$checks = 0
function Assert-Refactor($Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}
$keys = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'Languages/ChineseSimplified/Keyed') -Filter *.xml) {
    [xml]$doc = Get-Content $file.FullName -Raw
    foreach ($entry in $doc.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
        Assert-Refactor (!$keys.ContainsKey($entry.Name)) "Duplicate key: $($entry.Name)"
        $keys[$entry.Name] = $entry.InnerText
    }
}
$used = [Collections.Generic.HashSet[string]]::new()
foreach ($file in Get-ChildItem $sourceRoot -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName))
    foreach ($call in $tree.GetRoot().DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax] }) {
        $access = $call.Expression
        if ($access -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax] -or $access.Name.Identifier.ValueText -ne 'Translate') { continue }
        if ($access.Expression -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax]) { continue }
        $key = $access.Expression.Token.ValueText
        if (!$key.StartsWith('MouseDisaster_UI_')) { continue }
        Assert-Refactor ($keys.ContainsKey($key)) "Missing text: $key"
        [void]$used.Add($key)
        $indexes = @([regex]::Matches($keys[$key], '(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})') | ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique)
        $argumentCount = $call.ArgumentList.Arguments.Count
        Assert-Refactor ($indexes.Count -eq $argumentCount) "Argument count mismatch: $key"
        for ($i = 0; $i -lt $argumentCount; $i++) {
            Assert-Refactor ($indexes[$i] -eq $i) "Non-contiguous placeholders: $key"
        }
        $values = [object[]]@(0..([Math]::Max(0, $argumentCount - 1)) | ForEach-Object { "argument$_" })
        [void][string]::Format($keys[$key], $values)
    }
}
foreach ($key in $keys.Keys | Where-Object { $_.StartsWith('MouseDisaster_UI_') }) {
    Assert-Refactor ($used.Contains($key)) "Unused runtime text: $key"
}
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace RimWorld {
    public interface IIncidentTarget { }
}
namespace RimWorld.Planet {
    public class Caravan : RimWorld.IIncidentTarget { public bool IsPlayerControlled; }
}
namespace Verse {
    public class Pawn { public int thingIDNumber; }
    public class Map : RimWorld.IIncidentTarget { public bool IsPlayerHome; public MapTemperature mapTemperature = new MapTemperature(); }
    public class MapTemperature { public float OutdoorTemp = 20f; }
    public static class Translator { public static string Translate(this string key) { return key; } }
    public static class Extensions { public static bool NullOrEmpty(this string value) { return string.IsNullOrEmpty(value); } }
}
namespace MouseDisaster {
public class MouseDisasterSettings { public bool IsMouseDisasterEnvironmentTemperatureAllowed(float value) { return true; } }
public static class MouseDisasterMod { public static MouseDisasterSettings Settings = new MouseDisasterSettings(); }
public static class RefactorHarness {
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    public static int Run() {
        var original = new[] { "LargeRefugeeWave", "AbandonedRatkinChildren", "ShatteredMother", "BeggarFamily", "BeggarGroup", "ThiefRatkinGroup", "ThiefRatkinChildGroup", "WildRatkinWandersIn", "WildRatkinChildWandersIn", "WildRatkinGroupWandersIn", "FamineRefugees", "RatkinTraderCaravan", "ChildExchange", "BeggarSiege" }.Select(s => "MouseDisaster_" + s).ToArray();
        var entries = MouseDisasterIncidentCatalog.AllEntries;
        Check(entries.Count == 51, "catalog count");
        Check(entries.Where(e => e.IsOriginal).Select(e => e.DefName).SequenceEqual(original), "original membership/order changed");
        Check(entries.Where(e => e.BroadcastEligible).Select(e => e.DefName).SequenceEqual(original.Take(13)), "broadcast membership/order changed");
        Check(entries.Reverse().Where(e => e.IsOriginal).Count() == 14, "classification depends on order");
        Check(entries.Count(e => e.TargetKind == MouseDisasterIncidentTargetKind.Caravan) == 2, "caravan target count");
        Check(entries.Count(e => e.Category == MouseDisasterIncidentCategory.Plague) == 15, "plague count");
        Check(MouseDisasterIncidentCatalog.CountEnabledIncidents(null) == 51, "default availability");
        Check(MouseDisasterIncidentCatalog.CountEnabledIncidents(new[] { original[0] }) == 50, "event toggle");
        foreach (string good in MouseDisasterTraderTradePolicy.OptionalGoodDefNames) {
            Check(MouseDisasterTraderTradePolicy.IsRatEggTradeGood(good.ToUpperInvariant(), null), "optional good");
            Check(MouseDisasterTraderTradePolicy.CanTraderStockRatEggTradeGood("MouseDisaster_RatkinTrader", good, null), "stock permission");
        }
        Check(MouseDisasterTraderTradePolicy.OptionalGoodDefNames.Count == 6, "optional goods changed");
        Check(!MouseDisasterTraderTradePolicy.IsRatEggTradeGood(null, null), "null good");
        Check(!MouseDisasterTraderTradePolicy.MatchesCuisineKeywords("Steel", "steel"), "unrelated good");
        Check(MouseDisasterTraderTradePolicy.MatchesCuisineKeywords("foreign_RAT_EGG_food", null), "keyword matching");
        Check(!MouseDisasterTraderTradePolicy.CanTraderStockRatEggTradeGood("OtherTrader", "RatEgg_Meat", null), "unrelated trader");
        var first = new Verse.Pawn { thingIDNumber = 1 }; var last = new Verse.Pawn { thingIDNumber = 1 };
        var buffer = new Dictionary<int, Verse.Pawn> { { 99, first } };
        var result = MouseDisasterPawnLookup.Populate(new[] { first, null, last }, buffer);
        Check(ReferenceEquals(buffer, result), "buffer ownership changed");
        Check(result.Count == 1 && ReferenceEquals(result[1], last), "null/duplicate lookup semantics");
        MouseDisasterPawnLookup.Populate(null, buffer); Check(buffer.Count == 0, "stale lookup entries");
        return checks;
    }
}
}
'@
$sources = foreach ($name in 'MouseDisasterIncidentCatalog.cs','MouseDisasterIncidentTargetPolicy.cs','MouseDisasterTraderTradePolicy.cs','Utilities/MouseDisasterPawnLookup.cs') {
    [regex]::Replace((Get-Content (Join-Path $sourceRoot $name) -Raw), '(?m)^using [^;]+;\r?\n', '')
}
# Compile actual implementation with only the game boundary stubbed.
Add-Type -TypeDefinition ($stub + ($sources -join "`n"))
$checks += [MouseDisaster.RefactorHarness]::Run()
$xmlIds = foreach ($file in Get-ChildItem (Join-Path $root 'Defs/IncidentDefs') -Filter *.xml) {
    [xml]$doc = Get-Content $file.FullName -Raw
    foreach ($def in $doc.Defs.IncidentDef) { $def.defName }
}
$catalogIds = @([MouseDisaster.MouseDisasterIncidentCatalog]::AllEntries | ForEach-Object DefName)
Assert-Refactor (@(Compare-Object $xmlIds $catalogIds).Count -eq 0) 'XML/catalog membership differs'
foreach ($entry in [MouseDisaster.MouseDisasterIncidentCatalog]::AllEntries) {
    $labelKey = $entry.DisplayLabel.Substring($entry.DisplayId.Length + 1)
    Assert-Refactor ($keys.ContainsKey($labelKey)) "Missing incident menu text: $($entry.DefName)"
    Assert-Refactor ($entry.DisplayId -match '^[ON]-\d{3}$') "Invalid display ID: $($entry.DefName)"
    Assert-Refactor ($entry.IsOriginal -eq $entry.DisplayId.StartsWith('O-')) "Origin classification mismatch: $($entry.DefName)"
}
$displayIds = @([MouseDisaster.MouseDisasterIncidentCatalog]::AllEntries | ForEach-Object DisplayId)
Assert-Refactor (@($displayIds | Sort-Object -Unique).Count -eq $displayIds.Count) 'Display IDs are not unique'
Assert-Refactor (@(Compare-Object @($displayIds | Where-Object { $_ -like 'O-*' }) @(1..14 | ForEach-Object { 'O-{0:D3}' -f $_ })).Count -eq 0) 'Original display ID range changed'
Assert-Refactor (@(Compare-Object @($displayIds | Where-Object { $_ -like 'N-*' }) @(11..47 | ForEach-Object { 'N-{0:D3}' -f $_ })).Count -eq 0) 'New incident IDs overlap narrative IDs'
$plagueStub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace PlagueTests {
public class Pawn {
    public bool Dead, Spawned = true, ratkin = true;
    public Health health = new Health();
}
public class Hediff { public float Severity; }
public class HediffSet {
    public Hediff infection;
    public bool HasHediff(object def) => infection != null;
    public Hediff GetFirstHediffOfDef(object def) => infection;
}
public class Capacities { public float level = 1; public float GetLevel(object def) => level; }
public class Health {
    public HediffSet hediffSet = new HediffSet(); public Capacities capacities = new Capacities();
    public void AddHediff(Hediff h) { hediffSet.infection = h; }
}
public class Map { public MapPawns mapPawns = new MapPawns(); }
public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(), FreeColonistsSpawned = new List<Pawn>(); }
public static class MouseDisasterDefOf { public static object MouseDisaster_Plague = new object(); }
public static class MouseDisasterUtility { public static bool IsRatkin(Pawn p) => p.ratkin; }
public static class HediffMaker { public static Hediff MakeHediff(object def, Pawn p) => new Hediff(); }
public static class PawnCapacityDefOf { public static object BloodPumping = new object(); }
public static class MessageTypeDefOf { public static object NegativeHealthEvent = new object(); }
public static class Mathf { public static float Max(float a,float b) => Math.Max(a,b); public static float Min(float a,float b) => Math.Min(a,b); }
public static class Rand {
    public static float lastChance = -1; public static bool succeeds = true;
    public static float Range(float a,float b) => (a+b)/2;
    public static bool Chance(float chance) { lastChance = chance; return succeeds; }
}
public static class Messages { public static int count; public static void Message(string text, List<Pawn> targets, object type, bool historical) { count++; } }
public static class Translator { public static string Translate(this string s) => s; public static string Resolve(this string s) => s; }
public static class Harness {
    static int checks;
    static void Check(bool value,string name) { if (!value) throw new Exception(name); checks++; }
    public static int Run() {
        var p = new Pawn();
        MouseDisasterPhase2Utility.InfectWithPlague(p,0.4f);
        Check(p.health.hediffSet.infection.Severity == 0.4f,"old infection entry no longer forwards");
        var infection = p.health.hediffSet.infection;
        MouseDisasterPlagueUtility.InfectWithPlague(p,0.2f);
        Check(ReferenceEquals(infection,p.health.hediffSet.infection) && infection.Severity == 0.4f,"infection downgraded or duplicated");
        MouseDisasterPlagueUtility.InfectWithPlague(p,0.8f); Check(infection.Severity == 0.8f,"severity increase lost");
        MouseDisasterPlagueUtility.InfectWithPlague(p); Check(infection.Severity == 0.8f,"unspecified severity rewrites infection");
        Check(MouseDisasterPhase2Utility.IsPlagueCarrierMouseDisasterPawn(p),"carrier facade");
        p.Dead=true; Check(!MouseDisasterPlagueUtility.IsPlagueCarrierMouseDisasterPawn(p),"dead carrier");
        p.Dead=false; p.Spawned=false; Check(!MouseDisasterPlagueUtility.IsPlagueCarrierMouseDisasterPawn(p),"unspawned carrier");
        p.Spawned=true; p.ratkin=false; Check(!MouseDisasterPlagueUtility.IsPlagueCarrierMouseDisasterPawn(p),"non-ratkin carrier");
        p.ratkin=true;
        var first=new Pawn(); var second=new Pawn();
        MouseDisasterPhase2Utility.InfectMany(new[] { null, first, second },true);
        Check(first.health.hediffSet.infection != null && second.health.hediffSet.infection == null,"leader-only selection");
        Check(first.health.hediffSet.infection.Severity == 0.05f,"initial severity bounds");
        MouseDisasterPlagueUtility.InfectMany(new[] { first, null, second }); Check(second.health.hediffSet.infection != null,"group infection");
        MouseDisasterPlagueUtility.InfectMany(null); MouseDisasterPlagueUtility.InfectWithPlague(null);
        MouseDisasterPlagueUtility.InfectWithPlague(new Pawn { health=null });
        var map=new Map(); var vulnerable=new Pawn(); var resistant=new Pawn(); resistant.health.capacities.level=1.2f;
        map.mapPawns.FreeColonistsSpawned.AddRange(new[] { vulnerable,resistant,null });
        MouseDisasterPlagueUtility.DoPlagueSpreadCheck(map); Check(vulnerable.health.hediffSet.infection == null,"spread without carriers");
        map.mapPawns.AllPawnsSpawned.Add(p); Rand.succeeds=false;
        MouseDisasterPlagueUtility.DoPlagueSpreadCheck(map); Check(vulnerable.health.hediffSet.infection == null && Rand.lastChance == 0.005f,"spread chance ignored");
        Rand.succeeds=true; MouseDisasterPhase2Utility.DoPlagueSpreadCheck(map);
        Check(vulnerable.health.hediffSet.infection != null && resistant.health.hediffSet.infection == null,"immunity threshold or facade changed");
        Check(Messages.count == 1,"spread notification count");
        map.mapPawns.AllPawnsSpawned.AddRange(Enumerable.Repeat(p,100)); map.mapPawns.FreeColonistsSpawned.Add(new Pawn());
        MouseDisasterPlagueUtility.DoPlagueSpreadCheck(map); Check(Rand.lastChance == 0.30f,"spread chance cap");
        MouseDisasterDefOf.MouseDisaster_Plague=null; var untouched=new Pawn();
        MouseDisasterPlagueUtility.InfectWithPlague(untouched); Check(untouched.health.hediffSet.infection == null,"missing def not tolerated");
        MouseDisasterPlagueUtility.DoPlagueSpreadCheck(null);
        return checks;
    }
}
}
'@
$plagueSources = foreach ($name in 'MouseDisasterPlagueUtility.cs','MouseDisasterPhase2Utility.Plague.cs') {
    $code = Get-Content (Join-Path $sourceRoot "Utilities/$name") -Raw
    [regex]::Replace($code, '(?m)^using [^;]+;\r?\n', '').Replace('namespace MouseDisaster','namespace PlagueTests')
}
Add-Type -TypeDefinition ($plagueStub + ($plagueSources -join "`n"))
$checks += [PlagueTests.Harness]::Run()
& (Join-Path $PSScriptRoot 'sync-language-fallbacks.ps1') -Check
"PASS: $checks refactor assertions; game behavior and save/load still need in-game verification."
