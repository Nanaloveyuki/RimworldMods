param([switch]$Build)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$checks = 0
function Assert-Narrative($Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}

$xmlFiles = @(Get-ChildItem (Join-Path $root 'Defs'),(Join-Path $root 'Languages') -Filter *.xml -Recurse)
foreach ($file in $xmlFiles) {
    $doc = [System.Xml.XmlDocument]::new()
    $doc.Load($file.FullName)
    Assert-Narrative ($null -ne $doc.DocumentElement) "Invalid XML: $($file.FullName)"
}
$keys = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'Languages/ChineseSimplified/Keyed') -Filter *.xml) {
    [xml]$doc = Get-Content $file.FullName -Raw -Encoding UTF8
    foreach ($node in $doc.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
        Assert-Narrative (!$keys.ContainsKey($node.Name)) "Duplicate key: $($node.Name)"
        $keys[$node.Name] = $node.InnerText
    }
}
$sources = @(Get-ChildItem (Join-Path $root '1.6/Source') -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })
$allCode = ($sources | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$required = @([regex]::Matches($allCode, '"(MouseDisaster_Story_[A-Za-z0-9]+)"') | ForEach-Object { $_.Groups[1].Value })
foreach ($id in @('N005Care','N005Dead','N005Left','N005Missing','BroadcastEcho','N008Entry','N008Verified','N008Unverified','N008Traded','N008Rejected','N008Dead','N008Missing','N008Left','N008Timeout','N009Entry','N007Return','N007ReturnReceived','E01','E02','E03','E04','E05','PublicEnding','IdentityEntry','IdentityFull','IdentityPartial','BridgeChild','BridgeGrain','BridgePlague','BridgeEnvoy','S12Paid','S12Fight') + (1..14 | ForEach-Object { 'S{0:D2}' -f $_ }) + (1..6 | ForEach-Object { "N009Result$_" })) {
    $required += "MouseDisaster_Story_${id}_Label", "MouseDisaster_Story_${id}_Text"
}
foreach ($id in 'Trade','Verify','Reject','Drive','Receive','Ask','Quiet') { $required += "MouseDisaster_Story_Choice$id" }
foreach ($id in 1..5) { $required += "MouseDisaster_Story_RelicChoice$id" }
foreach ($state in 'Left','Settled','Dead','Detained','Missing') { $required += "MouseDisaster_Story_End$state" }
foreach ($state in 'Missing','Transferred','Departure','Treatment','Decision','Care','Growth') { $required += "MouseDisaster_Story_Wait$state" }
foreach ($key in $required | Sort-Object -Unique) {
    if ($key -in 'MouseDisaster_Story_Choice','MouseDisaster_Story_RelicChoice','MouseDisaster_Story_Debug','MouseDisaster_Story_Toggle','MouseDisaster_Story_End','MouseDisaster_Story_Wait') { continue }
    Assert-Narrative ($keys.ContainsKey($key)) "Missing key: $key"
}
foreach ($key in $keys.Keys | Where-Object { $_ -like 'MouseDisaster_Story_*' -or $_ -like 'MouseDisaster_N005_*' -or $_ -like 'MouseDisaster_N006_*' -or $_ -like 'MouseDisaster_N007_*' -or $_ -like 'MouseDisaster_Narrative_*' }) {
    Assert-Narrative ($keys[$key] -notmatch '不是[\s\S]{0,150}而是') "Forbidden contrast: $key"
}

[xml]$project = Get-Content (Join-Path $root '1.6/Source/MouseDisasterYear.csproj') -Raw
$compile = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object { $_.Include.Replace('\','/') })
foreach ($source in Get-ChildItem (Join-Path $root '1.6/Source') -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }) {
    $relative = [IO.Path]::GetRelativePath((Join-Path $root '1.6/Source'), $source.FullName).Replace('\','/')
    Assert-Narrative ($relative -in $compile) "Uncompiled source: $relative"
}
foreach ($item in $compile) { Assert-Narrative (Test-Path (Join-Path $root "1.6/Source/$item")) "Missing input: $item" }

# Compile the real scanner against controlled game objects; this tests transitions, not Unity AI.
$journal = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Journal.cs') -Raw
$scan = Get-CSharpMethod $journal 'ScanNarrativePawn'
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
using MouseDisaster;
namespace NarrativeTests {
public enum NarrativePawnEnd { Pending, Left, Settled, Dead, Detained, Missing }
public class Map { public int uniqueID = 1; }
public static class Find { public static List<Map> Maps = new List<Map> { new Map() }; }
public class Pawn {
    public bool Dead, Destroyed, IsPrisoner, IsSlave, Care, Healthy = true;
    public Map MapHeld = Find.Maps[0];
}
public class NarrativePawnObservation {
    public Pawn pawn;
    public int careTicks, missingSince = -1;
    public NarrativePawnEnd end;
}
public class Harness {
    int CurrentNarrativeTick;
    static bool NarrativeInCare(Pawn p, bool captive) { return p.Care && (captive || !p.IsPrisoner && !p.IsSlave); }
    static bool NarrativeCareHealthy(Pawn p) { return p.Healthy; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static int Run() {
        var h = new Harness(); var p = new Pawn { Care = true };
        var o = new NarrativePawnObservation { pawn = p };
        for (int i = 0; i < 199; i++) { h.CurrentNarrativeTick += 1500; h.ScanNarrativePawn(o,1,0); }
        Check(o.end == NarrativePawnEnd.Pending, "care completed early");
        h.CurrentNarrativeTick += 1500; h.ScanNarrativePawn(o,1,0);
        Check(o.end == NarrativePawnEnd.Settled, "five-day care did not finish");
        h.ScanNarrativePawn(o,1,0); Check(o.careTicks == 300000, "settlement applied twice");
        p = new Pawn { Care = true, Healthy = false }; o = new NarrativePawnObservation { pawn = p };
        h.ScanNarrativePawn(o,1,0); Check(o.careTicks == 0, "ill patient earns care ticks");
        p.IsPrisoner = true; h.ScanNarrativePawn(o,1,0); Check(o.end == NarrativePawnEnd.Detained, "ordinary captivity counts as aid");
        o = new NarrativePawnObservation { pawn = p }; p.Healthy = true;
        h.ScanNarrativePawn(o,1,0,true); Check(o.end == NarrativePawnEnd.Pending && o.careTicks == 1500, "N005 captive cannot receive care");
        p.Dead = true; h.ScanNarrativePawn(o,1,0,true); Check(o.end == NarrativePawnEnd.Dead, "death missed");
        o = new NarrativePawnObservation(); h.CurrentNarrativeTick = 0;
        h.ScanNarrativePawn(o,1,0); Check(o.end == NarrativePawnEnd.Pending, "missing reference lacks grace");
        h.CurrentNarrativeTick = 60000; h.ScanNarrativePawn(o,1,0);
        Check(o.end == NarrativePawnEnd.Missing, "missing reference reported alive or dead");
        o = new NarrativePawnObservation { pawn = new Pawn { MapHeld = null } };
        h.ScanNarrativePawn(o,1,0); Check(o.end == NarrativePawnEnd.Left, "departure missed");
        o = new NarrativePawnObservation { pawn = new Pawn { MapHeld = null } };
        Find.Maps.Clear(); h.ScanNarrativePawn(o,1,0); Check(o.end == NarrativePawnEnd.Missing, "lost map counted as safe departure");
        Find.Maps.Add(new Map()); o = new NarrativePawnObservation { pawn = new Pawn() };
        h.CurrentNarrativeTick = 1800000; h.ScanNarrativePawn(o,1,0);
        Check(o.end == NarrativePawnEnd.Missing, "unresolved visit never expires");
        return 12;
    }
'@
Add-Type -TypeDefinition ($stub + $scan + "`n}}" + [regex]::Replace((Get-Content (Join-Path $root '1.6/Source/MouseDisasterNarrativePolicy.cs') -Raw), '(?m)^using [^;]+;\r?$', ''))
foreach ($case in @(@(-1000,1.25),@(-100,1.25),@(0,1.0),@(100,0.75),@(1000,0.75))) {
    Assert-Narrative ([Math]::Abs([MouseDisaster.MouseDisasterNarrativePolicy]::ThreatFrequencyFactor($case[0]) - $case[1]) -lt 0.0001) 'Trust threat frequency bounds or neutral baseline changed'
}
$checks += [NarrativeTests.Harness]::Run()

# Run the real neutralization method against controlled pawns with existing duties.
$utility = (Get-ChildItem (Join-Path $root '1.6/Source/Utilities') -Filter 'MouseDisasterUtility.*.cs' | Sort-Object Name | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$neutral = Get-CSharpMethod $utility 'EnsureMouseDisasterFactionNeutralOnMap'
$neutralStub = @'
using System;
using System.Collections.Generic;
namespace NeutralDutyTests {
public class Faction { public bool neutral; }
public class Mental { public int resets; public void Reset() { resets++; } }
public class Mind { public object duty = new object(); public Mental mentalStateHandler = new Mental(); }
public class Pawn { public bool Dead, InAggroMentalState, ratkin = true; public Faction Faction; public Mind mindState = new Mind(); }
public class Pawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
public class Map { public Pawns mapPawns = new Pawns(); }
public static class Harness {
static bool IsRatkin(Pawn p) { return p.ratkin; }
static void MakeFactionNeutralToPlayer(Faction f, bool force) { f.neutral = true; }
public static int Run() {
    var f = new Faction(); var map = new Map();
    var visitor = new Pawn { Faction = f, InAggroMentalState = true };
    var other = new Pawn { Faction = new Faction(), InAggroMentalState = true };
    map.mapPawns.AllPawnsSpawned.Add(visitor); map.mapPawns.AllPawnsSpawned.Add(other);
    object duty = visitor.mindState.duty;
    EnsureMouseDisasterFactionNeutralOnMap(map, f);
    if (!ReferenceEquals(duty, visitor.mindState.duty)) throw new Exception("Neutralization erased Lord duty");
    if (!f.neutral || visitor.mindState.mentalStateHandler.resets != 1) throw new Exception("Neutralization behavior lost");
    if (other.mindState.mentalStateHandler.resets != 0) throw new Exception("Other faction affected");
    EnsureMouseDisasterFactionNeutralOnMap(null, f); EnsureMouseDisasterFactionNeutralOnMap(map, null);
    return 4;
}
'@
Add-Type -TypeDefinition ($neutralStub + $neutral + "`n}}")
$checks += [NeutralDutyTests.Harness]::Run()
Assert-Narrative ($utility -match 'allowDowned: stage == DevelopmentalStage.Baby') 'Baby generation rejects naturally downed life stage'
[xml]$clay = Get-Content (Join-Path $root 'Defs/ThingDefs/ThingDefs_MouseDisaster_GuanyinTu.xml') -Raw
Assert-Narrative ($clay.Defs.ThingDef.graphicData.graphicClass -eq 'Graphic_Single') 'Clay graphic expects a collection directory'
Assert-Narrative (Test-Path (Join-Path $root ('Textures/' + $clay.Defs.ThingDef.graphicData.texPath + '.png'))) 'Clay texture missing'

$policy = [MouseDisaster.MouseDisasterNarrativePolicy]
foreach ($id in $policy::SettingIds) {
    Assert-Narrative ($keys.ContainsKey("MouseDisaster_Story_Toggle$id")) "Missing toggle label: $id"
    Assert-Narrative ($policy::Enabled($id, [string[]]@())) "Default disabled: $id"
    Assert-Narrative (!$policy::Enabled($id, [string[]]@($id))) "Toggle ignored: $id"
    if ($id -notin 'N010','Echo') { Assert-Narrative ($keys.ContainsKey("MouseDisaster_Story_Debug$id")) "Missing debug label: $id" }
}
Assert-Narrative (!$policy::Enabled('Invalid', [string[]]@())) 'Unknown narrative enabled'
Assert-Narrative ($policy::ScaledChance(0.5, 0) -eq 0) 'Zero frequency still triggers'
Assert-Narrative ($policy::ScaledChance(0.5, 100) -eq 0.5) 'Default frequency changed'
Assert-Narrative ($policy::ScaledChance(0.5, 50) -eq 0.25) 'Frequency scale incorrect'
Assert-Narrative ($policy::ScaledChance(0, 100) -eq 0) 'Frequency bypasses low trust'
$onlyE01 = [Func[string,bool]] { param($id) $id -eq 'E01' }
Assert-Narrative ($policy::Ending(75,$true,99,3,3,100,99,3,3,100,$true,$onlyE01) -eq 'E01') 'Disabled E02 blocks E01'
$debugCode = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Debug.cs') -Raw
Assert-Narrative ($debugCode.Contains('finally { component.narrativeDebugForce = previous; }')) 'Debug override leaks into normal play'
Assert-Narrative ($debugCode.Contains('if (component == null || !Prefs.DevMode) return;')) 'Debug has no developer gate'
$checkStart = $debugCode.IndexOf('if (id == "Check")')
$checkEnd = $debugCode.IndexOf('Map map =', $checkStart)
Assert-Narrative (!$debugCode.Substring($checkStart, $checkEnd - $checkStart).Contains('ProcessNarrativeJournal')) 'Debug scan fabricates care ticks'
$settingsCode = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
foreach ($field in 'narrativeProgressGoal','narrativeRewardGoal','narrativeTheftGoal','narrativeEnvoyGoal','narrativeRelicGoal','narrativeEndingDelayDays','narrativeReturnChancePercent','narrativeReturnDelayDays','narrativeN004ReturnChancePercent','narrativeEchoChancePercent','narrativeEchoCooldownDays') {
    Assert-Narrative ($settingsCode.Contains("Scribe_Values.Look(ref $field,")) "Setting not persisted: $field"
    Assert-Narrative ($settingsCode -match "$field = Mathf.Clamp\(") "Setting not clamped: $field"
    $reset = $settingsCode.Substring($settingsCode.IndexOf('public void ResetToDefaults()'))
    $reset = $reset.Substring(0,$reset.IndexOf('public bool IsIncidentEnabled'))
    Assert-Narrative ($reset.Contains("$field =")) "Setting default not restored: $field"
}
$n005 = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N005.cs') -Raw
Assert-Narrative ($n005.Contains('!n005StartedTraderIds.Contains(traderId)')) 'N005 toggle cancels started exchange'
Assert-Narrative ($n005.Contains('Scribe_Collections.Look(ref n005StartedTraderIds')) 'Started exchange opt-in not saved'
$sceneRecord = $journal.Substring($journal.IndexOf('visit.showLetter ='), 70)
Assert-Narrative ($sceneRecord.Contains('NarrativeEnabled(scene)')) 'S notification toggle missing'
foreach ($trust in -100..100) {
    Assert-Narrative ($policy::Reward(200,$trust,$false) -eq 200) 'Non-narrator reward changed'
    $reward = $policy::Reward(200,$trust,$true)
    Assert-Narrative ($reward -ge 200 -and $reward -le 250) 'Reward outside bounds'
    if ($trust -le 0) { Assert-Narrative ($reward -eq 200) 'Nonpositive trust changes reward' }
    if ($trust -le -75) { Assert-Narrative ($policy::EchoChance($trust) -eq 0) 'Low-trust echo enabled' }
}
Assert-Narrative (!$policy::IsAidComplete($true,$false,0,0,0)) 'Empty event counted as aid'
Assert-Narrative (!$policy::IsAidComplete($true,$true,3,3,0)) 'Driven event counted as aid'
Assert-Narrative (!$policy::IsAidComplete($true,$false,3,2,0)) 'Partial survival counted as full aid'
Assert-Narrative ($policy::IsAidComplete($false,$false,3,0,3)) 'Sustained settlement not counted'
Assert-Narrative (!$policy::IsAidComplete($false,$false,3,3,0)) 'Unassisted departure counted'
Assert-Narrative ($policy::IsAidComplete($true,$false,3,2,0,1)) 'Partial identity change incorrectly failed the event'
Assert-Narrative (!$policy::IsAidComplete($true,$false,3,0,0,3)) 'Full identity change did not fail the event'
Assert-Narrative (!$policy::IsIdentityChangeFailure(3,2) -and $policy::IsIdentityChangeFailure(3,3)) 'Identity-change failure threshold is not 100 percent'
Assert-Narrative ($policy::Ending(75,$true,99,3,3,100,99,3,3,100,$true) -eq 'E02') 'E02 precedence'
Assert-Narrative ($policy::Ending(50,$true,99,3,3,100,99,3,3,100,$true) -eq 'E01') 'E01 threshold'
Assert-Narrative ($policy::Ending(-100,$false,99,3,3,100,99,3,3,100,$true) -eq 'E02') 'Public ending gated by hidden trust'
Assert-Narrative ($policy::Ending(-75,$true,0,0,0,0,99,3,3,100,$true) -eq 'E05') 'E05 boundary'
Assert-Narrative ($null -eq $policy::Ending(0,$true,0,0,0,0,99,3,3,100,$false)) 'Premature ending'
Assert-Narrative ($policy::Ending(0,$true,0,0,0,0,99,3,3,100,$true) -eq 'E03') 'Zero trust has no ending'
$scenes = @('AirdropMistake','MisguidedKinship','LaboringRefugees','Passersby','ThiefRatkinGroup','LargeRefugeeWave','Intel_Treasure_Silver','RatkinTraderCaravan','StrongSiege','GreatFamine','PlagueRevenge','CaravanMuggers','WildRatkinWandersIn','Aid_Silver')
for ($i = 0; $i -lt $scenes.Count; $i++) {
    Assert-Narrative ($policy::Scene("MouseDisaster_$($scenes[$i])") -eq ('S{0:D2}' -f ($i+1))) "Scene mismatch: $($scenes[$i])"
}

$n004 = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N004.cs') -Raw
Assert-Narrative ($n004.IndexOf('ProcessNarrativeJournal();') -lt $n004.IndexOf('TicksGame % N004CheckIntervalTicks')) 'Care scanner gated by old interval'
$n007 = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N007.cs') -Raw
$release = $n007.Substring($n007.IndexOf('case MouseDisasterN007Phase.ReleasePending:'))
$release = $release.Substring(0, $release.IndexOf('case MouseDisasterN007Phase.RecoveryDecision:'))
Assert-Narrative (!$release.Contains('StartN007Release')) 'Departure Lord reset in scanner'
$tasks = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Tasks.cs') -Raw
$hold = Get-CSharpMethod $tasks 'IsWaitingEnvoy'
$holdStub = @'
using System;
namespace EnvoyHoldTests {
public class Pawn {}
public class Harness {
    Pawn envoy = new Pawn();
    int envoyPhase;
    public static int Run() {
        var h = new Harness();
        for (int phase=0; phase<=4; phase++) {
            h.envoyPhase=phase;
            if(h.IsWaitingEnvoy(h.envoy)!=(phase>0 && phase<4)) throw new Exception("Envoy hold phase mismatch");
            if(h.IsWaitingEnvoy(new Pawn()) || h.IsWaitingEnvoy(null)) throw new Exception("Hold applied to another pawn");
        }
        return 10;
    }
'@
Add-Type -TypeDefinition ($holdStub + $hold + "`n}}")
$checks += [EnvoyHoldTests.Harness]::Run()
$startEnvoy = Get-CSharpMethod $tasks 'StartEnvoy'
Assert-Narrative ($startEnvoy.Contains('behavior.CreateGroup("N008")')) 'Envoy settings do not reach a behavior cohort'
Assert-Narrative ($startEnvoy.IndexOf('HoldN007Pawns') -lt $startEnvoy.IndexOf('behavior.CreateGroup')) 'Meeting hold overwrites hostile envoy behavior'
Assert-Narrative ($tasks.Contains('release: !reacted')) 'Story completion overwrites expulsion reaction'
$beggar = Get-Content (Join-Path $root '1.6/Source/JobGiver_MouseDisasterBeggar.cs') -Raw
Assert-Narrative ($beggar.Contains('IsWaitingEnvoy(pawn)')) 'Fed envoy can leave before the meeting ends'
Assert-Narrative (!$allCode.Contains('baby.Destroy();')) 'Baby aid still destroys its recipient'

Assert-Narrative ($utility.Contains('forceGenerateNewPawn: true')) 'Generation may rewrite existing world pawns'
Assert-Narrative ($utility.Contains('prohibitedTraits: MouseDisasterGenerationPolicy.ProhibitedTraits')) 'Generation trait gate missing'
Assert-Narrative ($utility.Contains('Scribe_Collections.Look(ref deliveredChildIds')) 'Dropoff completion is not saved'
Assert-Narrative ($utility.Contains('ShouldAdultLeaveWhenChildrenAreOnMap')) 'Mother still waits for a dropoff cell'
Assert-Narrative ($utility.Contains('if (!allChildrenArrived && !state.adultHasLeft)')) 'Orphaned delivery cannot settle'
$group = Get-Content (Join-Path $root '1.6/Source/MouseDisasterPawnGroupUtility.cs') -Raw
Assert-Narrative ($group.Contains('child.jobs?.StopAll();')) 'Loaded dropoff leaves old exit job running'
$alerts = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Alerts.cs') -Raw
Assert-Narrative ($alerts.Contains('"mouseDisaster_journalEntries", LookMode.Deep')) 'Journal entries are not saved'
Assert-Narrative ($alerts.Contains('Narrative.PendingNarrativeCount > 0')) 'Read journal hides unfinished work'
Assert-Narrative ($alerts.Contains('exitToMainMenu: false')) 'Story ending exits the running colony'
$debug = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Debug.cs') -Raw
Assert-Narrative ($debug.Contains('childGetter = () => IncidentDebugEntries')) 'Event debug groups are not lazy'
$catalog = Get-Content (Join-Path $root '1.6/Source/MouseDisasterIncidentCatalog.cs') -Raw
$eventIds = @([regex]::Matches($catalog, 'new MouseDisasterIncidentEntry\("[ON]-\d{3}", "([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
Assert-Narrative ($eventIds.Count -eq 51 -and @($eventIds | Sort-Object -Unique).Count -eq 51) 'Incident catalog membership changed'

[xml]$traitDoc = Get-Content (Join-Path $root 'Defs/TraitDefs/Traits_MouseDisaster.xml') -Raw
$xmlTraitNames = @($traitDoc.Defs.TraitDef | ForEach-Object { $_.defName })
Assert-Narrative ($xmlTraitNames.Count -eq 50) 'Owned TraitDef count changed'
Assert-Narrative (@($xmlTraitNames | Sort-Object -Unique).Count -eq 50) 'Owned TraitDef names are not unique'
Assert-Narrative (@($xmlTraitNames | Where-Object { $_ -notlike 'MouseDisaster_Trait_*' }).Count -eq 0) 'Owned TraitDef prefix changed'
Assert-Narrative (@($traitDoc.Defs.TraitDef | Where-Object { $_.commonality -ne '0' }).Count -eq 0) 'Owned traits must not roll in vanilla generation'
Assert-Narrative (@($traitDoc.Defs.TraitDef | Where-Object { $_.modExtensions.li.allowTrait -ne 'true' }).Count -eq 0) 'Owned traits missing generation opt-in'
$degreeClasses = @($traitDoc.Defs.TraitDef | ForEach-Object { $_.degreeDatas.li.GetAttribute('Class') })
Assert-Narrative (@($degreeClasses | Where-Object { $_ -ne 'MouseDisaster.MouseDisasterTraitDegreeData' }).Count -eq 0) 'Owned traits missing ColorLibrary degree data'
Assert-Narrative (@($traitDoc.Defs.TraitDef | Where-Object { [string]::IsNullOrWhiteSpace($_.degreeDatas.li.color) }).Count -eq 0) 'Owned traits missing ColorLibrary color'
[xml]$thoughtDoc = Get-Content (Join-Path $root 'Defs/ThoughtDefs/Thoughts_MouseDisaster_Traits.xml') -Raw
$thoughtNames = @($thoughtDoc.Defs.ThoughtDef | ForEach-Object { $_.defName })
Assert-Narrative ($thoughtNames.Count -gt 0) 'Owned trait thoughts are missing'
Assert-Narrative (@($thoughtNames | Where-Object { $_ -notlike 'MouseDisaster_Thought_*' }).Count -eq 0) 'Owned trait thought prefix changed'
$traitData = Get-Content (Join-Path $root '1.6/Source/MouseDisasterTraitData.cs') -Raw
$catalogTraitNames = @([regex]::Matches($traitData, '"MouseDisaster_Trait_[A-Za-z]+"') | ForEach-Object { $_.Value.Trim('"') })
Assert-Narrative ($catalogTraitNames.Count -eq 50) 'Trait catalog membership changed'
Assert-Narrative (-not (Compare-Object ($xmlTraitNames | Sort-Object) ($catalogTraitNames | Sort-Object))) 'Trait XML and catalog defNames diverged'
Assert-Narrative ($allCode.Contains('MouseDisasterPawnHistoryDefinition history = MouseDisasterPawnHistoryCatalog.TryApply(pawn, stage);')) 'History apply no longer returns the selected definition'
Assert-Narrative ($allCode.Contains('MouseDisasterTraitCatalog.TryApply(pawn, stage, history);')) 'Owned traits are not applied after pawn history'
Assert-Narrative ($allCode.Contains('class MouseDisasterTraitDegreeData : TraitDegreeData')) 'Owned trait colors are not backed by TraitDegreeData'
Assert-Narrative ($allCode.Contains('[HarmonyPatch(typeof(Trait), nameof(Trait.LabelCap), MethodType.Getter)]')) 'Owned trait label color is not patched'
$historyData = Get-Content (Join-Path $root '1.6/Source/MouseDisasterPawnHistoryData.cs') -Raw
$historyIds = [System.Collections.Generic.HashSet[string]]@([regex]::Matches($historyData, '"(?:A|Y)\d{3}"') | ForEach-Object { $_.Value.Trim('"') })
foreach ($id in @([regex]::Matches($traitData, '"(?:A|Y)\d{3}"') | ForEach-Object { $_.Value.Trim('"') } | Sort-Object -Unique)) {
    Assert-Narrative ($historyIds.Contains($id)) "Trait catalog references unknown history $id"
}


if ($Build) {
    dotnet build (Join-Path $root '1.6/Source/MouseDisasterYear.csproj') --configuration Release --nologo '-p:RimWorldManagedDir=D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed' '-p:HarmonyAssembliesDir=D:\Appdata\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies' -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
"PASS: $checks assertions; $($xmlFiles.Count) XML files. Game AI and save/load require in-game verification."
