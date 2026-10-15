$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace EventRecoveryTests {
public class Map { public int uniqueID; public bool IsPlayerHome = true; }
public class Game {
    public GameComponent_MouseDisasterBroadcastHope broadcast;
    public T GetComponent<T>() where T : class { return broadcast as T; }
}
public static class Current { public static Game Game; }
public class TickManager { public int TicksGame; }
public static class Find { public static TickManager TickManager = new TickManager(); public static List<Map> Maps = new List<Map>(); }
public static class GenDate { public const int TicksPerHour = 2500; public const int TicksPerDay = 60000; }
public static class Rand { public static int RangeInclusive(int min, int max) { return min; } }
public struct IntRange { public int min, max; public IntRange(int min, int max) { this.min=min; this.max=max; } public int RandomInRange => min; }
public interface IExposable { void ExposeData(); }
public class GameComponent { public virtual void ExposeData() {} public virtual void GameComponentTick() {} }
public enum LoadSaveMode { PostLoadInit }
public enum LookMode { Deep, Value }
public static class Scribe { public static LoadSaveMode mode; }
public static class Scribe_Values { public static void Look(ref int value, string key, int fallback) {} }
public static class Scribe_Collections {
    public static void Look<T>(ref List<T> value, string key, LookMode mode) {}
    public static void Look<K,V>(ref Dictionary<K,V> value, string key, LookMode k, LookMode v) {}
}
public class IncidentDef { public string defName; public IncidentWorker Worker = new IncidentWorker(); public object category; }
public class IncidentWorker {
    public bool canFire = true, succeeds = true, throws; public int executed;
    public bool CanFireNow(IncidentParms parms) { return canFire; }
    public bool TryExecute(IncidentParms parms) { executed++; if(throws) throw new Exception("test failure"); return succeeds; }
}
public class IncidentParms { }
public static class StorytellerUtility { public static IncidentParms DefaultParmsNow(object category, Map map) { return new IncidentParms(); } }
public static class DefDatabase<T> where T : IncidentDef {
    public static List<T> defs = new List<T>();
    public static T GetNamedSilentFail(string name) { return defs.FirstOrDefault(d => d.defName == name); }
}
public class Entry { public string DefName; public bool BroadcastEligible=true; }
public static class MouseDisasterIncidentCatalog {
    public static List<Entry> AllEntries = new List<Entry>();
    public static bool IsIncidentEnabled(string name, IEnumerable<string> disabled) { return disabled == null || !disabled.Contains(name); }
}
public class Settings { public int broadcastHopeCooldownDays=3; public List<string> disabledIncidentDefNames=new List<string>(); }
public static class MouseDisasterMod { public static Settings Settings = new Settings(); }
public static class MouseDisasterRuntime { public static bool AllowsNewContent = true; }
public class GameComponent_MouseDisasterNarrative { public void NotifyNarrativeBroadcast() {} }
public static class Messages { public static List<string> texts = new List<string>(); public static void Message(string text, object type, bool historical) { texts.Add(text); } }
public static class MessageTypeDefOf { public static object NeutralEvent, RejectInput; }
public static class Log { public static int errors; public static void Warning(string text) {} public static void Error(string text) { errors++; } }
public static class Extensions {
    public static string Translate(this string text) { return text; }
    public static IEnumerable<T> InRandomOrder<T>(this IEnumerable<T> values) { return values; }
}
public enum DevelopmentalStage { Baby, Child, Adult }
public class Capacities { public bool capable=true; public bool CapableOf(object def) { return capable; } }
public class Health { public Capacities capacities = new Capacities(); }
public static class PawnCapacityDefOf { public static object Talking; }
public class Pawn {
    public int thingIDNumber; public bool Spawned=true, Dead, Downed, awake=true; public DevelopmentalStage DevelopmentalStage=DevelopmentalStage.Adult;
    public Health health = new Health(); public bool Awake() { return awake; }
}
public static partial class MouseDisasterUtility { }
public static class Harness {
    static int checks;
    static void Check(bool value, string name) { if(!value) throw new Exception(name); checks++; }
    static IncidentDef Reset() {
        Current.Game = new Game(); Current.Game.broadcast = new GameComponent_MouseDisasterBroadcastHope(Current.Game);
        Find.TickManager.TicksGame=0; Find.Maps=new List<Map> { new Map { uniqueID=1 } };
        MouseDisasterRuntime.AllowsNewContent=true; MouseDisasterMod.Settings=new Settings(); Messages.texts.Clear(); Log.errors=0;
        var def=new IncidentDef { defName="Test" }; DefDatabase<IncidentDef>.defs=new List<IncidentDef> { def };
        MouseDisasterIncidentCatalog.AllEntries=new List<Entry> { new Entry { DefName="Test" } }; return def;
    }
    static bool Queue() { return GameComponent_MouseDisasterBroadcastHope.TryQueueBroadcast(Find.Maps[0],out _,out _); }
    public static int Run() {
        var d=Reset(); Check(Queue(),"queue rejected"); Check(!Queue(),"duplicate broadcast accepted");
        Current.Game.broadcast.GameComponentTick(); Check(d.Worker.executed==1,"scheduled event not executed");
        Check(GameComponent_MouseDisasterBroadcastHope.TryGetBroadcastCooldownRemainingDays(Find.Maps[0],out _),"successful cooldown missing");
        d=Reset(); MouseDisasterMod.Settings.broadcastHopeCooldownDays=0; Check(Queue() && Queue(),"zero-cooldown setting ignored");
        d=Reset(); MouseDisasterMod.Settings.disabledIncidentDefNames.Add("Test"); Check(!Queue(),"all-disabled queue accepted");
        d=Reset(); MouseDisasterRuntime.AllowsNewContent=false; Check(!Queue(),"disabled save queue accepted");
        d=Reset(); d.Worker.canFire=false; Check(Queue(),"temporarily blocked event rejected at queue time"); Current.Game.broadcast.GameComponentTick();
        Check(d.Worker.executed==0 && Messages.texts.Contains("MouseDisaster_BroadcastWaiting"),"retry lacked feedback");
        for(int tick=60;tick<=72*GenDate.TicksPerHour;tick+=60) { Find.TickManager.TicksGame=tick; Current.Game.broadcast.GameComponentTick(); }
        Check(Messages.texts.Contains("MouseDisaster_BroadcastExpired"),"permanent failure not expired");
        Check(!GameComponent_MouseDisasterBroadcastHope.TryGetBroadcastCooldownRemainingDays(Find.Maps[0],out _),"failed cooldown not released");
        d=Reset(); d.Worker.throws=true; Queue(); Current.Game.broadcast.GameComponentTick();
        Check(Log.errors==1 && Messages.texts.Contains("MouseDisaster_BroadcastError"),"exception swallowed");
        Find.TickManager.TicksGame=2520; Current.Game.broadcast.GameComponentTick(); Check(d.Worker.executed==1,"partial failure automatically retried");
        d=Reset(); Queue(); MouseDisasterRuntime.AllowsNewContent=false; Current.Game.broadcast.GameComponentTick(); Check(d.Worker.executed==0,"paused queue executed");
        MouseDisasterRuntime.AllowsNewContent=true; Current.Game.broadcast.GameComponentTick(); Check(d.Worker.executed==1,"queue not resumed");
        d=Reset(); Queue(); Find.Maps.Clear(); Current.Game.broadcast.GameComponentTick(); Check(d.Worker.executed==0,"removed map executed");
        var p=new Pawn { thingIDNumber=1 }; var target=new Pawn { thingIDNumber=2 }; Find.TickManager.TicksGame=0;
        Check(MouseDisasterUtility.CanReceiveBegging(target),"adult rejected"); target.DevelopmentalStage=DevelopmentalStage.Baby;
        Check(!MouseDisasterUtility.CanReceiveBegging(target),"baby accepted"); target.DevelopmentalStage=DevelopmentalStage.Child; target.awake=false;
        Check(!MouseDisasterUtility.CanReceiveBegging(target),"sleeping child accepted"); target.awake=true; target.Downed=true;
        Check(!MouseDisasterUtility.CanReceiveBegging(target),"downed target accepted"); target.Downed=false;
        MouseDisasterUtility.StartBeggingCooldown(p,target);
        Check(!MouseDisasterUtility.CanBegAgain(p) && !MouseDisasterUtility.CanBegAgain(target),"cooldown missing on one participant");
        Find.TickManager.TicksGame=1250; Check(MouseDisasterUtility.CanBegAgain(p) && MouseDisasterUtility.CanBegAgain(target),"cooldown never expires");
        return checks;
    }
}
}
'@
$production = foreach ($file in 'GameComponent_MouseDisasterBroadcastHope.cs','MouseDisasterBroadcastHopePolicy.cs') {
    $code = Get-Content (Join-Path $root "1.6/Source/$file") -Raw
    [regex]::Replace($code, '(?m)^using [^;]+;\r?\n', '').Replace('namespace MouseDisaster','namespace EventRecoveryTests')
}
$begging = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Begging.cs') -Raw
$syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($begging).GetRoot()
$giveFood = $syntax.DescendantNodes() | Where-Object {
    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $_.Identifier.ValueText -eq 'TryConsumeBeggedFood'
}
$guard = $giveFood.Body.Statements[0].ToString()
if ($guard -notmatch 'MouseDisasterMod.Settings\?\.allowColonistAutoGiveFood != true' -or $guard -notmatch 'return false;') {
    throw 'Automatic food giving must be opt-in before any inventory access.'
}
$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
if ($settings -notmatch 'public bool allowColonistAutoGiveFood = false;' -or
    (Get-CSharpMethod $settings 'ResetToDefaults') -notmatch 'allowColonistAutoGiveFood = false;' -or
    (Get-CSharpMethod $settings 'ExposeData') -notmatch 'Scribe_Values.Look\(ref allowColonistAutoGiveFood, "allowColonistAutoGiveFood", false\);') {
    throw 'Automatic food giving must default off for new, reset, and existing settings.'
}
Write-Host 'PASS: automatic food giving entry guard and settings default/reset/load contracts.'
$members = $syntax.DescendantNodes() | Where-Object {
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax] -and $_.Declaration.Variables.Identifier.ValueText -in @('BeggingCooldownTicks','NextBegTickByPawnId')) -or
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $_.Identifier.ValueText -in @('CanBegAgain','CanReceiveBegging','StartBeggingCooldown'))
} | ForEach-Object ToFullString
$beggingSource = 'namespace EventRecoveryTests { public static partial class MouseDisasterUtility { ' + ($members -join "`n") + ' } }'
Add-Type -TypeDefinition ($stub + ($production -join "`n") + $beggingSource)
"PASS: $([EventRecoveryTests.Harness]::Run()) production broadcast/begging assertions. Unity interaction and real save/load remain in-game checks."
