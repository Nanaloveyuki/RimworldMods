$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace TaskStateTests {
public enum DevelopmentalStage { Baby, Child, Adult }
public enum JobTag { Misc }
public enum HungerCategory { Starving }
public enum PathEndMode { Touch }
public enum Danger { Deadly }
public enum JobCondition { InterruptForced }
public enum LocomotionUrgency { Jog }
public enum MouseDisasterN004Decision { Accepted, Rejected, Ignored }
public struct IntVec3 { public static IntVec3 Invalid; }
public class Map { public IntVec3 Center; public MapPawns mapPawns = new MapPawns(); }
public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
public class Faction { public static Faction OfPlayer = new Faction(); }
public class JobDef {}
public class Job { public JobDef def; public bool exitMapOnArrival; public LocomotionUrgency locomotionUrgency; }
public static class JobDefOf { public static JobDef Goto=new JobDef(), PredatorHunt=new JobDef(), Wait=new JobDef(), Wait_Wander=new JobDef(), LayDown=new JobDef(), GotoWander=new JobDef(); }
public static class JobMaker { public static Job MakeJob(JobDef def, object target) { return new Job { def=def }; } }
public class Jobs { public Job current; public bool reject; public int starts; public void StartJob(Job j, JobCondition c) { starts++; current=reject?null:j; } }
public class Needs { public Food food = new Food(); }
public class Food { public HungerCategory CurCategory; }
public class Race { public bool predator=true; }
public class Verbs { public object TryGetMeleeVerb(object target) { return new object(); } }
public class Pawn {
 public bool Dead, Destroyed, Downed, Spawned=true, IsAnimal, prisoner, slave, reachable=true;
 public Map Map, MapHeld; public Faction Faction; public Jobs jobs=new Jobs(); public IntVec3 Position;
 public DevelopmentalStage DevelopmentalStage; public Needs needs=new Needs(); public Race RaceProps=new Race(); public Verbs meleeVerbs=new Verbs();
 public Job CurJob => jobs.current; public object CurJobDef => CurJob?.def;
 public bool CanReach(Pawn p, PathEndMode mode, Danger danger) { return reachable; }
 public void SetFaction(Faction f) { Faction=f; }
}
public class DiaOption { public Action action; public bool resolveTree; public DiaOption(string label) {} }
public class Targets { public bool IsValid() { return false; } }
public static class MessageTypeDefOf { public static object PositiveEvent, NeutralEvent; }
public static class Messages { public static void Message(string s, List<Pawn> p, object t, bool historical) {} }
public class LetterStack { public void RemoveLetter(object letter) {} }
public static class Find { public static LetterStack LetterStack=new LetterStack(); }
public static class Extensions {
 public static string Translate(this string s) { return s; }
 public static bool TryRandomElement<T>(this List<T> items, out T item) { item=items.FirstOrDefault(); return items.Count>0; }
}
public static class RCellFinder { public static bool TryFindBestExitSpot(Pawn p, out IntVec3 cell) { cell=new IntVec3(); return true; } }
public static partial class MouseDisasterUtility {
 public static List<Pawn> leaving = new List<Pawn>();
 public static bool IsPlayerAffiliatedRatkin(Pawn p) { return p.Faction==Faction.OfPlayer || p.prisoner || p.slave; }
 public static bool IsPendingAbandonedChild(Pawn p) { return false; }
 public static bool ShouldBlockIdleDeparture(Pawn p) { return false; }
 public static bool ShouldUsePassiveAutoOrderMode(Pawn p) { return false; }
 public static Job CreateGotoJob(IntVec3 c) { return JobMaker.MakeJob(JobDefOf.Goto,c); }
 public static bool TryFindFarEdgeCell(Map m, IntVec3 p, out IntVec3 c) { c=new IntVec3(); return true; }
 public static void CancelAbandonedDelivery(Pawn p, IEnumerable<Pawn> c) {}
 public static void RegisterAbandonedDelivery(Pawn p, IEnumerable<Pawn> c, IntVec3 pos) {}
 public static void MakeTravelAndExitLord(Map m, IEnumerable<Pawn> p, IntVec3 c) { leaving.AddRange(p); }
 public static bool Auto(Pawn p) { return CanAutoOrderJob(p,false); }
}
public class LetterBase { public virtual IEnumerable<DiaOption> Choices => null; }
public partial class Letter : LetterBase {
 public bool ArchivedOnly; public Pawn mother; public List<Pawn> children=new List<Pawn>(); public Map map; public IntVec3 foodCell;
 public DiaOption Option_Close=new DiaOption(""), Option_JumpToLocationAndPostpone=new DiaOption(""), Option_Postpone=new DiaOption(""); public Targets lookTargets=new Targets();
 private void ResolveDecision(MouseDisasterN004Decision d) {}
}
public class MouseDisasterN004Record { public MouseDisasterN004Decision decision; public bool predatorHuntStarted; public List<Pawn> children=new List<Pawn>(); public int predatorHuntDeadlineTick; }
public partial class Narrative {
 public Map map; public int CurrentNarrativeTick;
 private Map ResolveN004Map(MouseDisasterN004Record r) { return map; }
 private static bool IsPresentOnMap(Pawn p) { return p!=null && !p.Dead && (p.Spawned || p.MapHeld!=null); }
 public void Run(MouseDisasterN004Record r) { ProcessN004PredatorHunt(r); }
}
public static class Harness {
 static int checks;
 static void Check(bool ok,string s) { if(!ok) throw new Exception(s); checks++; }
 static Pawn P(Map m) { return new Pawn { Map=m, MapHeld=m }; }
 public static int Run() {
  var m=new Map(); var mother=P(m); var child=P(m); child.Spawned=false; child.Map=null;
  var l=new Letter { map=m,mother=mother,children=new List<Pawn>{child} };
  l.Choices.First().action(); Check(child.Faction==Faction.OfPlayer && mother.Faction==Faction.OfPlayer,"carried child omitted from acceptance");
  foreach(int state in new[]{0,1,2,3,4}) {
   mother=P(m); child=P(m); l=new Letter{map=m,mother=mother,children=new List<Pawn>{child}};
   var reject=l.Choices.Skip(1).First();
   if(state==0) child.Faction=Faction.OfPlayer; if(state==1) child.prisoner=true; if(state==2) child.slave=true;
   if(state==3) child.MapHeld=new Map(); if(state==4) child.Dead=true;
   var original=child.Faction ?? new Faction(); child.Faction=original;
   reject.action(); Check(child.Faction==original,"stale reject changed protected child");
  }
  child=P(m); l=new Letter{map=m,mother=P(m),children=new List<Pawn>{child}}; var accept=l.Choices.First(); child.prisoner=true;
  accept.action(); Check(child.Faction!=Faction.OfPlayer,"stale accept recruited prisoner");
  var p=P(m); Check(MouseDisasterUtility.Auto(p) && MouseDisasterUtility.ExitMapJob(p)!=null,"mobile pawn rejected");
  p.Downed=true; Check(!MouseDisasterUtility.Auto(p) && MouseDisasterUtility.ExitMapJob(p)==null,"downed pawn received move job");
  p.Downed=false;p.Spawned=false;Check(!MouseDisasterUtility.Auto(p) && MouseDisasterUtility.ExitMapJob(p)==null,"held pawn received move job");
  foreach(int state in new[]{0,1,2,3,4,5}) {
   var prey=P(m); var predator=P(m); predator.IsAnimal=true; m.mapPawns.AllPawnsSpawned=new List<Pawn>{predator};
   var r=new MouseDisasterN004Record{decision=MouseDisasterN004Decision.Rejected,children=new List<Pawn>{prey},predatorHuntDeadlineTick=100};
   if(state==0) predator.Downed=true; if(state==1) predator.reachable=false; if(state==2) predator.jobs.reject=true;
   if(state==3) { prey.Spawned=false;prey.Map=null; } if(state==4) prey.prisoner=true;
   new Narrative{map=m}.Run(r); Check(r.predatorHuntStarted==(state==5),"invalid hunt marked started");
   if(state!=2 && state!=5) Check(predator.jobs.starts==0,"invalid hunter or prey received job");
  }
  var protectedChild=P(m);protectedChild.Faction=Faction.OfPlayer;var captive=P(m);captive.prisoner=true;var slave=P(m);slave.slave=true;var visitor=P(m);var other=P(new Map());
  m.mapPawns.AllPawnsSpawned.Clear();MouseDisasterUtility.leaving.Clear();
  new Narrative{map=m,CurrentNarrativeTick=100}.Run(new MouseDisasterN004Record{decision=MouseDisasterN004Decision.Rejected,children=new List<Pawn>{protectedChild,captive,slave,visitor,other},predatorHuntDeadlineTick=100});
  Check(MouseDisasterUtility.leaving.SequenceEqual(new[]{visitor}),"delayed exit reclaimed protected or off-map child");
  return checks;
 }
}
'@
$letter = Get-Content (Join-Path $root '1.6/Source/ChoiceLetter_MouseDisasterAbandonedChildren.cs') -Raw
$syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($letter).GetRoot()
$choices = ($syntax.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax] -and $_.Identifier.ValueText -eq 'Choices' }).ToFullString()
$code = $stub + "`npublic partial class Letter {`n" + $choices + (Get-CSharpMethod $letter 'GetValidPawns') + (Get-CSharpMethod $letter 'IsEligiblePawn') + "`n}`n"
$code += 'public static partial class MouseDisasterUtility {'
$code += Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Identity.cs') -Raw) 'CanAutoOrderJob'
$code += Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Travel.cs') -Raw) 'ExitMapJob'
$code += "}`npublic partial class Narrative {"
$code += Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N004.cs') -Raw) 'ProcessN004PredatorHunt'
$code += '}}'
Add-Type -TypeDefinition $code
$checks = [TaskStateTests.Harness]::Run()
$recovery = Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Lifecycle.cs') -Raw) 'TryRecoverIncidentVisitorFromPlayerGuest'
if ($recovery.IndexOf('pawn.Downed') -lt 0 -or $recovery.IndexOf('pawn.Downed') -gt $recovery.IndexOf('SetGuestStatus')) { throw 'Recovery must retain guest state while downed' }
foreach ($entry in @(@('IncidentWorker_AbandonedRatkinChildren.cs', 'child'), @('Incidents/PlagueVisitorIncidents.cs', 'baby'))) {
    $source = Get-Content (Join-Path $root "1.6/Source/$($entry[0])") -Raw
    if (!$source.Contains("IsLeadYourPetEnabled && !$($entry[1]).Downed")) { throw "Missing direct baby movement guard: $($entry[0])" }
}
Write-Host "Task state: $checks behavior assertions and 3 movement guard checks passed."
