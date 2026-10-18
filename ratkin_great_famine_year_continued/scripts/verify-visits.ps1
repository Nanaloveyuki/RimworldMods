$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$visits = Get-Content "$root/1.6/Source/GameComponent_MouseDisasterEventBehavior.Visits.cs" -Raw
$behavior = Get-Content "$root/1.6/Source/GameComponent_MouseDisasterEventBehavior.cs" -Raw
$generation = Get-Content "$root/1.6/Source/Utilities/MouseDisasterUtility.Generation.cs" -Raw
$visitor = Get-Content "$root/1.6/Source/GameComponent_MouseDisasterVisitorControl.cs" -Raw
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace VisitTests {
public enum LoadSaveMode { Inactive, Saving, PostLoadInit }
public enum LookMode { Value, Reference }
public enum MouseDisasterVisitorStatus { Visitor, TemporaryRecruit, HiredWorker }
public static class Scribe { public static LoadSaveMode mode; }
public static class Scribe_Collections {
 static Dictionary<string,object> data=new Dictionary<string,object>();
 public static void Look<K,V>(ref Dictionary<K,V> value,string key,LookMode a,LookMode b) {
  if(Scribe.mode==LoadSaveMode.Saving) data[key]=new Dictionary<K,V>(value);
  else if(Scribe.mode==LoadSaveMode.PostLoadInit) value=data.TryGetValue(key,out var saved)?new Dictionary<K,V>((Dictionary<K,V>)saved):null;
 }
 public static void Look<T>(ref HashSet<T> value,string key,LookMode a) {
  if(Scribe.mode==LoadSaveMode.Saving) data[key]=new HashSet<T>(value);
  else if(Scribe.mode==LoadSaveMode.PostLoadInit) value=data.TryGetValue(key,out var saved)?new HashSet<T>((HashSet<T>)saved):null;
 }
}
public static class Mathf { public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(n,max)); public static int Min(int a,int b)=>Math.Min(a,b); }
public static class GenDate { public const int TicksPerDay=60000; }
public static class Rand { public static float sample=0.5f; public static int draws; public static float Range(float min,float max) { draws++; return min+(max-min)*sample; } }
public class Settings { public int maxEventPawns=30; public float fedWanderDays=0.5f,noFoodWaitDays=0.5f; public bool leaveAfterFed=true,waitWhenNoFood=true; }
public static class MouseDisasterSettings { public const float DefaultFedWanderDays = 0f; }
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public class TickManager { public int TicksGame; }
public static class Find { public static TickManager TickManager=new TickManager(); }
public class PawnKindDef { }
public class Faction { public static Faction OfPlayer=new Faction(); }
public class Job { public bool exitMapOnArrival; }
public static class JobGiver_MouseDisasterBeggar { public static Job TryCreateVisitorBeggingJob(Pawn pawn)=>null; }
public class MentalHandler { public int resets; public void Reset() { resets++; } }
public class Mind { public object duty; public MentalHandler mentalStateHandler=new MentalHandler(); }
public class Jobs { public int stops; public void StopAll() { stops++; } }
public class Food { public float CurLevelPercentage=0.1f; }
public class Needs { public Food food=new Food(); }
public class Pawn {
 public int thingIDNumber; public bool Spawned=true,Dead,Downed,InAggroMentalState,protectedVisit,player;
 public object Map=new object(); public Needs needs=new Needs(); public Mind mindState=new Mind(); public Jobs jobs=new Jobs();
 public Faction Faction=new Faction(); public PawnKindDef kindDef=new PawnKindDef(); public Lord lord;
 public Lord GetLord()=>lord;
 public void ChangeKind(PawnKindDef kind) { kindDef=kind; }
 public void SetFaction(Faction faction) { Faction=faction; player=faction==Faction.OfPlayer; }
}
public class Lord { public object LordJob; public void RemovePawn(Pawn p) { p.lord=null; } }
public class LordJob_MouseDisasterDeparture { }
public static class LordMaker { public static int created; public static void MakeNewLord(Faction f,object job,object map,Pawn[] pawns) { created++; foreach(var p in pawns) p.lord=new Lord { LordJob=job }; } }
public static partial class MouseDisasterUtility {
 public static bool foodFound; public static int searches;
 public static bool IsPlayerAffiliatedRatkin(Pawn p)=>p.player;
 public static Job TryCreateImproperFoodJob(Pawn pawn,bool allowInventorySearch) { searches++; return foodFound?new Job():null; }
 public static Job ExitMapJob(Pawn pawn,bool force)=>new Job { exitMapOnArrival=true };
 public static void UnmarkTradableChattel(Pawn p) { }
 public static void ConsumeForcedPrisonerOnPurchase(Pawn p) { }
 public static void ClearTradeLeaderState(Pawn p) { }
 public static void MakeFactionNeutralToPlayer(Faction f) { }
 public static void NotifyMouseDisasterPawnIdentityOrLifeStageChanged(Pawn p) { }
}
public static class MouseDisasterFeeding {
 public static bool HasSatisfied(Pawn p)=>GameComponent_MouseDisasterEventBehavior.Component.HasCompletedFeeding(p);
 public static bool ShouldLeaveAfterFed(Pawn p)=>MouseDisasterMod.Settings.leaveAfterFed && HasSatisfied(p) && GameComponent_MouseDisasterEventBehavior.Component.FedDepartureDue(p);
 public static bool IsSeekingSuppressed(Pawn p)=>MouseDisasterMod.Settings.leaveAfterFed && HasSatisfied(p);
 public static void Evaluate(Pawn p,float level) { if(level>=0.82f) GameComponent_MouseDisasterEventBehavior.Component.CompleteFeeding(p); }
}
public class MouseDisasterVisitorRecord { public Pawn pawn; public Faction originalFaction; public PawnKindDef originalKindDef; public MouseDisasterVisitorStatus status; public int temporaryUntilTick; public bool employmentTimerPaused; }
public partial class VisitorControl {
 public static void Restore(MouseDisasterVisitorRecord r)=>RestoreOriginalIdentityAndForceLeave(r);
 private static void ReleaseGuestState(Pawn p) { }
 private static void SyncEmploymentMarkers(Pawn p,MouseDisasterVisitorStatus s) { }
}
public sealed partial class GameComponent_MouseDisasterEventBehavior {
 public static GameComponent_MouseDisasterEventBehavior Component;
 private Dictionary<int,int> fedDepartureTicks=new Dictionary<int,int>(),noFoodSinceTicks=new Dictionary<int,int>();
 private readonly Dictionary<int,int> nextFoodSearchTicks=new Dictionary<int,int>();
 private HashSet<int> fedPawnIds=new HashSet<int>(),refeedingPawnIds=new HashSet<int>();
 private HashSet<Pawn> departingPawns=new HashSet<Pawn>();
 public bool HasCompletedFeeding(Pawn pawn)=>fedPawnIds.Contains(pawn.thingIDNumber);
 public bool IsDeparting(Pawn pawn)=>departingPawns.Contains(pawn);
 public bool ManagesFoodVisit(Pawn pawn)=>pawn.Spawned && !pawn.player && !pawn.protectedVisit;
 public int Deadline(Pawn pawn)=>fedDepartureTicks[pawn.thingIDNumber];
 public void Save()=>ExposeVisitTimers();
 public static void Ensure(Pawn pawn)=>EnsureDepartureLord(pawn);
}
public static class Tests {
 static int checks;
 static void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
 static GameComponent_MouseDisasterEventBehavior Reset() {
  Find.TickManager.TicksGame=1000; MouseDisasterMod.Settings=new Settings(); Rand.draws=0;
  MouseDisasterUtility.searches=0; MouseDisasterUtility.foodFound=false; LordMaker.created=0;
  return GameComponent_MouseDisasterEventBehavior.Component=new GameComponent_MouseDisasterEventBehavior();
 }
 public static string Run() {
  Reset();
  for(int limit=1;limit<=100;limit++) for(int minimum=1;minimum<=20;minimum++) for(int requested=minimum;requested<=120;requested++) {
   MouseDisasterMod.Settings.maxEventPawns=limit;
   int actual=MouseDisasterUtility.LimitEventPawnCount(requested,minimum);
   Check(actual==(limit<minimum?requested:Math.Min(limit,requested)),"baseline cap/minimum exception");
  }
  var state=Reset(); var a=new Pawn { thingIDNumber=1 }; var b=new Pawn { thingIDNumber=2 };
  Rand.sample=0; state.CompleteFeeding(a); Rand.sample=1; state.CompleteFeeding(b);
  Check(state.Deadline(a)==16000 && state.Deadline(b)==46000,"independent random duration bounds");
  int draws=Rand.draws; state.CompleteFeeding(a);
  Check(state.Deadline(a)==16000 && Rand.draws==draws,"repeated feeding rerolls deadline");
  Find.TickManager.TicksGame=16000; Check(state.FedDepartureDue(a)&&!state.FedDepartureDue(b),"independent expiry");
  state.TryVisitJob(b,out var job); Check(job==null && MouseDisasterUtility.searches==0,"sated wandering rescanned food");
  state.TryVisitJob(a,out job); Check(state.IsDeparting(a)&&job.exitMapOnArrival,"fed deadline not leaving");
  Check(LordMaker.created==0,"job selection mutated lord/jobs");
  GameComponent_MouseDisasterEventBehavior.Ensure(a); GameComponent_MouseDisasterEventBehavior.Ensure(a);
  Check(LordMaker.created==1,"persistent departure lord rebuilt every check");
  a.lord=null; GameComponent_MouseDisasterEventBehavior.Ensure(a); Check(LordMaker.created==2,"interrupted departure not retried");
  var downed=new Pawn { thingIDNumber=3,Downed=true }; state.RequestDeparture(downed);
  GameComponent_MouseDisasterEventBehavior.Ensure(downed); Check(downed.Spawned&&downed.lord==null,"downed pawn vanished");
  downed.Downed=false; GameComponent_MouseDisasterEventBehavior.Ensure(downed); Check(downed.lord!=null,"recovered pawn did not resume exit");
  Scribe.mode=LoadSaveMode.Saving; state.Save(); var restored=new GameComponent_MouseDisasterEventBehavior();
  Scribe.mode=LoadSaveMode.PostLoadInit; restored.Save(); Scribe.mode=LoadSaveMode.Inactive;
  Check(restored.Deadline(a)==16000&&restored.Deadline(b)==46000&&restored.IsDeparting(a),"save/load lost deadlines or departure");
  state=Reset(); a=new Pawn { thingIDNumber=1 }; state.TryVisitJob(a,out job);
  for(int tick=1001;tick<1250;tick++) { Find.TickManager.TicksGame=tick; state.TryVisitJob(a,out job); }
  Check(MouseDisasterUtility.searches==1,"no-food scan not throttled");
  Find.TickManager.TicksGame=1250; MouseDisasterUtility.foodFound=true; state.TryVisitJob(a,out job);
  Check(job!=null&&!state.IsDeparting(a),"food appearing during wait was ignored");
  MouseDisasterUtility.foodFound=false; Find.TickManager.TicksGame=1500; state.TryVisitJob(a,out job);
  Find.TickManager.TicksGame=31000; state.TryVisitJob(a,out job); Check(!state.IsDeparting(a),"food discovery did not reset wait");
  Find.TickManager.TicksGame=31500; state.TryVisitJob(a,out job); Check(state.IsDeparting(a),"no-food wait never expired");
  state=Reset(); MouseDisasterMod.Settings.waitWhenNoFood=false; a=new Pawn { thingIDNumber=1 };
  state.TryVisitJob(a,out job); Check(state.IsDeparting(a)&&job.exitMapOnArrival,"immediate no-food mode");
  b=new Pawn { thingIDNumber=2,protectedVisit=true }; Check(!state.TryVisitJob(b,out job)&&!state.IsDeparting(b),"story obligation overwritten");
  b.protectedVisit=false; b.player=true; Check(!state.TryVisitJob(b,out job),"employment/player pawn controlled");
  state=Reset(); MouseDisasterMod.Settings.fedWanderDays=0; a=new Pawn { thingIDNumber=1 };
  state.CompleteFeeding(a); Check(state.FedDepartureDue(a),"zero stay not immediate");
  MouseDisasterMod.Settings.leaveAfterFed=false; MouseDisasterUtility.foodFound=true;
  state.TryVisitJob(a,out job); Check(job!=null&&!state.IsDeparting(a),"disabled fed departure still forced exit");
  var record=new MouseDisasterVisitorRecord { pawn=new Pawn { thingIDNumber=9,player=true,Faction=Faction.OfPlayer },originalFaction=new Faction(),originalKindDef=new PawnKindDef(),status=MouseDisasterVisitorStatus.TemporaryRecruit,temporaryUntilTick=1 };
  VisitorControl.Restore(record);
  Check(record.pawn.Faction==record.originalFaction&&record.pawn.kindDef==record.originalKindDef,"expired visitor identity not restored");
  Check(record.status==MouseDisasterVisitorStatus.Visitor&&!record.employmentTimerPaused&&state.IsDeparting(record.pawn),"expired employment has no persistent departure");
  return "PASS: "+checks+" production cap/visit/expiry assertions. Engine doubles, not real-game acceptance.";
 }
}
}
'@
$controller = @('FedDepartureDue','RequestDeparture','EnsureDepartureLord','TryVisitJob','ExposeVisitTimers') | ForEach-Object { Get-CSharpMethod $visits $_ }
$controller += Get-CSharpMethod $behavior 'CompleteFeeding'
$code = $stub + "`nnamespace VisitTests { public sealed partial class GameComponent_MouseDisasterEventBehavior {`n" + ($controller -join "`n") + "`n} public static partial class MouseDisasterUtility {`n" + (Get-CSharpMethod $generation 'LimitEventPawnCount') + "`n} public partial class VisitorControl {`n" + (Get-CSharpMethod $visitor 'RestoreOriginalIdentityAndForceLeave') + "`n} }"
Add-Type -TypeDefinition $code
[VisitTests.Tests]::Run()

$factions = Get-Content "$root/1.6/Source/Utilities/MouseDisasterUtility.Factions.cs" -Raw
$factory = Get-CSharpMethod $factions 'GetEventFaction'
if ($factory.Contains('FirstFactionOfDef') -or !$factory.Contains('faction.temporary = true')) { throw 'Event factions are not disposable isolated instances.' }
if (!$visits.Contains('FactionCanBeRemoved') -or !$visits.Contains('ReferencesFaction(faction)')) { throw 'Temporary faction retention is missing.' }
$xenotypes = Get-Content "$root/1.6/Source/MouseDisasterAdaptiveXenotypeUtility.cs" -Raw
if (!(Get-CSharpMethod $xenotypes 'IsCandidate').Contains('!IsVirtualDefaultRatkinXenotype')) { throw 'Virtual Ratkin candidate is still selectable.' }
Write-Output 'PASS: disposable faction retention and virtual xenotype exclusion wiring.'
