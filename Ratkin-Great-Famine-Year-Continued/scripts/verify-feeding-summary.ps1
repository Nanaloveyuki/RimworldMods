$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$feeding = Get-Content (Join-Path $root '1.6/Source/MouseDisasterFeeding.cs') -Raw
$journal = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Journal.cs') -Raw
$syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($journal).GetRoot()
$records = ($syntax.DescendantNodes() | Where-Object {
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax] -and $_.Identifier.ValueText -in @('NarrativePawnObservation','NarrativeVisit','NarrativeVisitSummary')) -or
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax] -and $_.Identifier.ValueText -eq 'NarrativePawnEnd')
} | ForEach-Object { $_.ToFullString() }) -join "`n"
$archive = Get-CSharpMethod $journal 'ArchiveResolvedVisits'
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using HarmonyLib;
namespace HarmonyLib {
public enum MethodType { Setter }
[AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute {
    public HarmonyPatch(Type type, string name, MethodType method) {}
    public HarmonyPatch(Type type, string name) {}
}
}
namespace Verse {
public interface IExposable { void ExposeData(); }
public enum LoadSaveMode { Inactive, PostLoadInit }
public enum LookMode { Reference, Deep }
public static class Scribe { public static LoadSaveMode mode; }
public static class Scribe_Values {
    public static Dictionary<string, object> values = new Dictionary<string, object>();
    public static bool reading;
    public static void Look<T>(ref T value, string key, T fallback = default(T)) {
        if (reading) value = values.TryGetValue(key, out var saved) ? (T)saved : fallback;
        else values.Add(key, value);
    }
}
public static class Scribe_Collections { public static void Look<T>(ref List<T> value,string key,LookMode mode) {} }
public static class Scribe_References { public static void Look<T>(ref T value,string key) {} }
public class HediffDef {}
public class Hediff { public float Severity; }
public class HediffSet {
    public Dictionary<HediffDef,Hediff> entries = new Dictionary<HediffDef,Hediff>();
    public bool HasHediff(HediffDef def) => entries.ContainsKey(def);
    public Hediff GetFirstHediffOfDef(HediffDef def) => entries.TryGetValue(def,out var value) ? value : null;
}
public class Health {
    public HediffSet hediffSet = new HediffSet();
    public void AddHediff(HediffDef def) { hediffSet.entries.Add(def,new Hediff()); }
    public void RemoveHediff(Hediff value) { hediffSet.entries.Remove(hediffSet.entries.First(p=>p.Value==value).Key); }
}
public class Pawn {
    static int nextId;
    public int thingIDNumber=++nextId;
    public bool Spawned = true, Dead, player, thief = true, beggar, seek;
    public Health health = new Health();
    public object MentalStateDef;
    public Mind mindState = new Mind();
}
public class Mind { public Handler mentalStateHandler = new Handler(); }
public class Handler { public int resets; public void Reset() { resets++; } }
}
namespace RimWorld {
public class Need { public float CurLevel { get; set; } public float MaxLevel=1f; public float CurLevelPercentage => CurLevel/MaxLevel; }
public class Need_Food : Need { public void NeedInterval() {} }
public static class HediffDefOf { public static HediffDef Malnutrition = new HediffDef(); }
}
namespace MouseDisaster {
public static class MouseDisasterDefOf {
    public static HediffDef MouseDisaster_FedOnce = new HediffDef(), MouseDisaster_RefeedingSyndrome = new HediffDef();
    public static HediffDef MouseDisaster_GuanyinTuSatiety = new HediffDef();
    public static object MouseDisaster_BeggingState = new object(), MouseDisaster_ThievingState = new object();
}
public static class MouseDisasterRuntime { public static bool AllowsNewContent = true; }
public enum MouseDisasterPawnBehavior { SeekFood }
public class MouseDisasterSettings { public bool leaveAfterFed = true; }
public static class MouseDisasterMod { public static MouseDisasterSettings Settings = new MouseDisasterSettings(); }
public class GameComponent_MouseDisasterEventBehavior {
    public bool FedDepartureDue(Pawn pawn)=>true;
    public static GameComponent_MouseDisasterEventBehavior Component=new GameComponent_MouseDisasterEventBehavior();
    HashSet<int> fed=new HashSet<int>(), refed=new HashSet<int>();
    public bool HasCompletedFeeding(Pawn p)=>fed.Contains(p.thingIDNumber);
    public void CompleteFeeding(Pawn p)=>fed.Add(p.thingIDNumber);
    public bool HasAppliedRefeeding(Pawn p)=>refed.Contains(p.thingIDNumber);
    public void RecordRefeeding(Pawn p)=>refed.Add(p.thingIDNumber);
    public bool HasFoodSeekingProfile(Pawn p)=>p.seek;
    public static bool HasBehavior(Pawn p,MouseDisasterPawnBehavior b) => p.seek;
}
public static class MouseDisasterUtility {
    public static bool IsPlayerAffiliatedRatkin(Pawn p) => p.player;
    public static bool IsThiefPawn(Pawn p) => p.thief;
    public static bool IsBeggarPawn(Pawn p) => p.beggar;
    public static bool IsAbandonedDeliveryPawn(Pawn p) => false;
}
public static class FeedingHarness {
    static int checks;
    static void Check(bool value,string label) { if (!value) throw new Exception(label); checks++; }
    public static int Run() {
        var foodNeed=new Need_Food { CurLevel=0.8f };
        MouseDisasterFoodLevelPatch.Prefix(foodNeed,0.7f,out bool snapshot);
        Check(!snapshot,"hunger decay enters setter checks");
        MouseDisasterFoodLevelPatch.Prefix(foodNeed,0.9f,out snapshot);
        Check(snapshot,"food increase ignored");
        MouseDisasterFoodLevelPatch.Prefix(new Need { CurLevel=0.2f },0.9f,out snapshot);
        Check(!snapshot,"non-food need enters feeding checks");
        Check(!MouseDisasterFeeding.IsFull(0.81f),"partial feeding completes state");
        Check(MouseDisasterFeeding.IsFull(0.82f),"full feeding not recognized");
        Check(MouseDisasterFeeding.ShouldRefeed(0.9f,0.8f),"full need does not trigger syndrome");
        Check(!MouseDisasterFeeding.ShouldRefeed(0.9f,0.39f),"minor malnutrition triggers syndrome");
        Check(!MouseDisasterFeeding.IsFull(float.NaN) && !MouseDisasterFeeding.IsFull(float.PositiveInfinity),"invalid modded level accepted");
        var p = new Pawn { MentalStateDef=MouseDisasterDefOf.MouseDisaster_ThievingState }; p.health.AddHediff(HediffDefOf.Malnutrition);
        p.health.hediffSet.entries[HediffDefOf.Malnutrition].Severity=0.4f;
        MouseDisasterFeeding.Evaluate(p,0.9f);
        Check(MouseDisasterFeeding.HasSatisfied(p),"fed marker missing");
        Check(MouseDisasterFeeding.ShouldLeaveAfterFed(p),"default departure disabled");
        MouseDisasterMod.Settings.leaveAfterFed=false;
        Check(!MouseDisasterFeeding.ShouldLeaveAfterFed(p),"departure ignores toggle");
        Check(MouseDisasterFeeding.IsSeekingSuppressed(p),"disabled departure resumed feeding after completion");
        Check(MouseDisasterFeeding.HasSatisfied(p),"toggle erased feeding history");
        var staying = new Pawn { MentalStateDef=MouseDisasterDefOf.MouseDisaster_ThievingState };
        MouseDisasterFeeding.Evaluate(staying,0.9f);
        Check(staying.mindState.mentalStateHandler.resets==0,"disabled departure reset visitor state");
        MouseDisasterMod.Settings.leaveAfterFed=true;
        Check(MouseDisasterFeeding.ShouldLeaveAfterFed(staying),"reenabling departure ignored saved completion");
        Check(p.mindState.mentalStateHandler.resets==1,"food-seeking mental state not cleared");
        Check(p.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome),"syndrome missing");
        Check(p.health.hediffSet.HasHediff(HediffDefOf.Malnutrition),"malnutrition removed");
        var syndrome=p.health.hediffSet.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome);
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(ReferenceEquals(syndrome,p.health.hediffSet.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome)),"syndrome refreshed");
        Check(MouseDisasterFeeding.HasSatisfied(p),"later hunger reset completion");
        p.health.hediffSet.entries.Clear();
        Check(MouseDisasterFeeding.HasSatisfied(p),"health editor removed saved completion");
        p.health.AddHediff(HediffDefOf.Malnutrition);
        p.health.hediffSet.entries[HediffDefOf.Malnutrition].Severity=0.8f;
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(!p.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome),"removed syndrome forced back");
        foreach (var excluded in new[]{new Pawn { player=true },new Pawn { Spawned=false },new Pawn { Dead=true },new Pawn { thief=false }}) {
            MouseDisasterFeeding.Evaluate(excluded,1f);
            Check(!MouseDisasterFeeding.HasSatisfied(excluded),"unrelated pawn marked");
        }
        MouseDisasterFeeding.Evaluate(null,1f);
        var otherState=new Pawn { MentalStateDef=new object() };
        MouseDisasterFeeding.Evaluate(otherState,1f);
        Check(otherState.mindState.mentalStateHandler.resets==0,"unrelated mental state cleared");
        p=new Pawn(); p.health.AddHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety);
        Check(MouseDisasterFeeding.IsSeekingSuppressed(p),"temporary satiety ignored");
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(!MouseDisasterFeeding.HasSatisfied(p),"temporary satiety became permanent");
        p.health.hediffSet.entries.Clear();
        MouseDisasterFeeding.Evaluate(p,0.2f);
        Check(!MouseDisasterFeeding.IsSeekingSuppressed(p),"removed satiety still blocks hungry visitor");
        MouseDisasterFeeding.Evaluate(p,0.82f);
        Check(MouseDisasterFeeding.HasSatisfied(p),"full visitor not completed after satiety removal");
        p=new Pawn { thief=false,seek=true }; p.health.AddHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety);
        p.health.AddHediff(HediffDefOf.Malnutrition); p.health.hediffSet.entries[HediffDefOf.Malnutrition].Severity=0.8f;
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(!MouseDisasterFeeding.HasSatisfied(p) && p.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome),"buff/fullness confused with refeeding eligibility");
        p=new Pawn { thief=false }; p.health.AddHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety);
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(!MouseDisasterFeeding.HasSatisfied(p),"unrelated pawn with clay buff marked");
        p=new Pawn(); p.health.AddHediff(MouseDisasterDefOf.MouseDisaster_FedOnce);
        Check(MouseDisasterFeeding.HasSatisfied(p) && !p.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_FedOnce),"legacy marker not migrated");
        p.health.hediffSet.entries.Clear();
        Check(MouseDisasterFeeding.HasSatisfied(p),"migration depends on health state");
        p=new Pawn(); foodNeed=new Need_Food { CurLevel=0.5f,MaxLevel=0.5f };
        MouseDisasterFoodIntervalPatch.Prefix(foodNeed,p);
        Check(MouseDisasterFeeding.HasSatisfied(p),"changed MaxNutrition or direct field edit missed by interval");
        p=new Pawn(); foodNeed=new Need_Food { CurLevel=1f,MaxLevel=0f };
        MouseDisasterFoodIntervalPatch.Prefix(foodNeed,p);
        Check(!MouseDisasterFeeding.HasSatisfied(p),"zero nutrition maximum treated as full");
        p=new Pawn(); p.health.AddHediff(HediffDefOf.Malnutrition);
        p.health.hediffSet.entries[HediffDefOf.Malnutrition].Severity=0.8f;
        MouseDisasterRuntime.AllowsNewContent=false;
        MouseDisasterFeeding.Evaluate(p,1f);
        Check(MouseDisasterFeeding.HasSatisfied(p) && !p.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome),"new-content toggle broke existing feeding or added syndrome");
        return checks;
    }
}
public partial class GameComponent_MouseDisasterNarrative {
    List<NarrativeVisit> narrativeVisits = new List<NarrativeVisit>();
    List<NarrativeVisitSummary> narrativeVisitSummaries = new List<NarrativeVisitSummary>();
    public static int VerifySummary() {
        var h=new GameComponent_MouseDisasterNarrative();
        var visit=new NarrativeVisit { id=7,scene="S03",created=100,resolved=true,delivered=true,counted=true,deliveredTick=200,drivenTick=-1 };
        foreach(var end in new[]{NarrativePawnEnd.Left,NarrativePawnEnd.Settled,NarrativePawnEnd.Dead,NarrativePawnEnd.Detained,NarrativePawnEnd.Missing})
            visit.people.Add(new NarrativePawnObservation { pawn=new Pawn(),end=end });
        var active=new NarrativeVisit { id=8 };
        h.narrativeVisits.Add(visit); h.narrativeVisits.Add(active);
        h.ArchiveResolvedVisits(); h.ArchiveResolvedVisits();
        if(h.narrativeVisits.Count!=1 || h.narrativeVisits[0]!=active || h.narrativeVisitSummaries.Count!=1)
            throw new Exception("migration lost active visit or duplicated summary");
        var summary=h.narrativeVisitSummaries[0];
        if(summary.completed!=-1 || summary.created!=100 || summary.deliveredTick!=200 || !summary.counted || !summary.delivered)
            throw new Exception("legacy time or decisions changed");
        if(summary.left!=1 || summary.settled!=1 || summary.dead!=1 || summary.detained!=1 || summary.missing!=1)
            throw new Exception("outcome counts changed");
        foreach(var field in typeof(NarrativeVisitSummary).GetFields())
            if(field.FieldType!=typeof(int) && field.FieldType!=typeof(bool) && field.FieldType!=typeof(string))
                throw new Exception("summary retains detailed references");
        summary=NarrativeVisitSummary.FromVisit(visit,900);
        Scribe_Values.values.Clear(); Scribe_Values.reading=false; summary.ExposeData();
        var copy=new NarrativeVisitSummary(); Scribe_Values.reading=true; copy.ExposeData();
        foreach(var field in typeof(NarrativeVisitSummary).GetFields())
            if(!Equals(field.GetValue(summary),field.GetValue(copy))) throw new Exception("summary serialization lost "+field.Name);
        return 5;
    }
'@
$feedingBody = [regex]::Replace($feeding, '(?m)^using [^;]+;\r?$', '')
Add-Type -TypeDefinition ($stub + $archive + "`n}" + $records + "`n}" + $feedingBody)
"PASS: $([MouseDisaster.FeedingHarness]::Run()) feeding assertions; $([MouseDisaster.GameComponent_MouseDisasterNarrative]::VerifySummary()) summary migration/serialization checks."
[xml]$defs = Get-Content (Join-Path $root 'Defs/HediffDefs/Hediffs_MouseDisaster.xml') -Raw
$syndrome = $defs.Defs.HediffDef | Where-Object defName -eq MouseDisaster_RefeedingSyndrome
if ($syndrome.comps.li.disappearsAfterTicks -ne '120000~300000') { throw 'Refeeding duration changed' }
$expected = @{ Metabolism=-0.30; BloodFiltration=-0.20; Consciousness=-0.25 }
foreach ($cap in $syndrome.stages.li.capMods.li) {
    if (!$expected.ContainsKey($cap.capacity) -or [double]$cap.offset -ne $expected[$cap.capacity]) { throw 'Refeeding capacity mismatch' }
    $expected.Remove($cap.capacity)
}
if ($expected.Count) { throw 'Missing refeeding capacity' }
$scavenging = Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Scavenging.cs') -Raw) 'TryCreatePrisonerScavengeJob'
if ($scavenging.Contains('HasAccessibleFood')) { throw 'Unused scavenge food search returned' }
'PASS: refeeding XML and removed redundant scavenge search. Real feeding, save/load and AI remain in-game checks.'
