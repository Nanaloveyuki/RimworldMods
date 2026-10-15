$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root '1.6/Source/CompatibilityStabilityPatches.cs') -Raw
$start = $source.IndexOf('    internal static class MouseDisasterMapJobDiagnostics')
$end = $source.IndexOf('    [HarmonyPatch(typeof(Pawn_JobTracker)', $start)
$production = $source.Substring($start, $end - $start)
$stub = @'
#pragma warning disable 0649
using System;
using System.Runtime.CompilerServices;
namespace MouseDisaster {
class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string m) {} }
class PawnKindDef { public string defName = "MouseDisaster_Test"; }
class Pawn {
    public PawnKindDef kindDef = new PawnKindDef();
    public bool identity, Spawned, Dead, Destroyed;
    public string ThingID = "test", Position = "edge";
    public object Map, MapHeld, ParentHolder, Faction;
    public Pawn_JobTracker jobs = new Pawn_JobTracker();
    public object CurJob { get { return jobs.curJob; } }
}
class Pawn_JobTracker {
    public object curJob;
    public JobDriver curDriver;
    public int ended;
    public bool restarted, pooled;
    public void EndCurrentJob(JobCondition c, bool startNewJob, bool canReturnToPool) {
        ended++; restarted = startNewJob; pooled = canReturnToPool; curJob = null;
    }
}
enum JobCondition { InterruptForced }
class JobDriver { public Pawn pawn; public object job; }
class JobDriver_Wait : JobDriver {}
class JobDriver_Vomit : JobDriver {}
class JobUtility { public static void TryStartErrorRecoverJob() {} }
static class MouseDisasterUtility { public static bool IsMouseDisasterPawn(Pawn p) { return p.identity; } }
class TickManager { public int TicksGame; }
static class Find { public static TickManager TickManager = new TickManager(); }
static class Log { public static int count; public static string last; public static void Warning(string s) { count++; last = s; } }
'@
$tests = @'
public static class MapJobTests {
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    public static int Run() {
        var p = new Pawn();
        var d = new JobDriver_Wait { pawn = p, job = new object() };
        p.jobs.curJob = d.job;
        Check(!MouseDisasterMapJobToilGuardPatch.Prefix(d), "mapless Wait blocked");
        Check(p.jobs.ended == 1 && !p.jobs.restarted && !p.jobs.pooled, "cleanup without restart or pooling");
        Check(Log.last.Contains("Caller stack:") && Log.last.Contains("holder=") && Log.last.Contains("job="), "trace context");
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(d), "stale driver ignored");
        p.jobs.curJob = d.job;
        p.MapHeld = new object();
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(d), "held Wait preserved");
        var v = new JobDriver_Vomit { pawn = p, job = d.job };
        Check(!MouseDisasterMapJobToilGuardPatch.Prefix(v), "held mapless Vomit blocked");
        Check(Log.count == 1, "repeated traces throttled");
        p.jobs.curJob = d.job; p.Map = p.MapHeld;
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(v), "spawned Vomit preserved");
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(d), "spawned Wait preserved");
        p.Map = p.MapHeld = null; p.kindDef.defName = "OtherMod";
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(d), "unrelated pawn preserved");
        p.identity = true;
        Check(!MouseDisasterMapJobToilGuardPatch.Prefix(d), "normalized incident pawn protected");
        var other = new JobDriver { pawn = p, job = new object() };
        p.jobs.curJob = other.job;
        Check(MouseDisasterMapJobToilGuardPatch.Prefix(other), "other jobs preserved");
        Find.TickManager.TicksGame = 600;
        MouseDisasterMapJobErrorTracePatch.Prefix(p, "initAction", new NullReferenceException("fixture"), v);
        Check(Log.count == 2 && Log.last.Contains("fixture") && Log.last.Contains("suppressed=2"), "error trace and suppressed count");
        Find.TickManager.TicksGame = 0;
        MouseDisasterMapJobErrorTracePatch.Prefix(p, "reset", null, v);
        Check(Log.count == 3, "tick reset traced");
        MouseDisasterMapJobErrorTracePatch.Prefix(null, "unrelated", null, null);
        Check(Log.count == 3, "null pawn ignored");
        return checks;
    }
}
}
'@
Add-Type -TypeDefinition ($stub + $production + $tests) -WarningAction SilentlyContinue
$checks = [MouseDisaster.MapJobTests]::Run()
Write-Host "PASS: $checks map-job guard checks (production methods with engine doubles)."
