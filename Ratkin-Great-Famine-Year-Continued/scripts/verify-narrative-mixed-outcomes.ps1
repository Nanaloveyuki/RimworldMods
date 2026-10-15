$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$n004 = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N004.cs') -Raw
$effects = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N004Effects.cs') -Raw
$n007 = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N007.cs') -Raw
$policy = [regex]::Replace((Get-Content (Join-Path $root '1.6/Source/MouseDisasterNarrativePolicy.cs') -Raw), '(?m)^using [^;]+;\r?$', '')
$methods = @(
    (Get-CSharpMethod $n004 'TryResolveN004')
    (Get-CSharpMethod $n004 'TryExpireN004Observation')
    (Get-CSharpMethod $n004 'IsChildAge')
    (Get-CSharpMethod $n004 'IsPlayerCare')
    (Get-CSharpMethod $effects 'IsN004Present')
    (Get-CSharpMethod $effects 'IsN004InPlayerDomain')
    (Get-CSharpMethod $effects 'IsN004Together')
    (Get-CSharpMethod $n007 'ScanN007Record')
) -join "`n"
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
using MouseDisaster;
namespace MixedNarrativeTests {
public enum MouseDisasterN004Outcome { Pending,ChildSurvived,FamilySurvived,ChildEnslaved,FamilyDied,FamilySeparated,ChildDied,FamilyChanged,Unknown }
public class Map {}
public class Caravan {}
public class Faction { public static Faction OfPlayer=new Faction(); }
public class Age { public float AgeBiologicalYearsFloat=4; }
public class Pawn {
 public bool Dead,Destroyed,Spawned=true,IsSlaveOfColony,IsPrisonerOfColony,managed=true,plague;
 public Map Map,MapHeld;
 public Faction Faction;
 public Age ageTracker=new Age();
 public Caravan caravan;
 public Caravan GetCaravan()=>caravan;
}
public class MouseDisasterN004Record {
 public Pawn mother;
 public List<Pawn> children=new List<Pawn>();
 public int createdTick;
 public MouseDisasterN004Outcome outcome;
}
public class MouseDisasterN007PawnRecord {
 public Pawn pawn;
 public bool died,handled,leftMap,recovered;
 public int unresolvedSinceTick=-1;
}
public class MouseDisasterN007Record { public List<MouseDisasterN007PawnRecord> pawnRecords=new List<MouseDisasterN007PawnRecord>(); }
public static class MouseDisasterVisitorUtility { public static bool IsManagedVisitor(Pawn p)=>p.managed; }
public static class MouseDisasterUtility { public static bool IsPlayerAffiliatedRatkin(Pawn p)=>p.Faction==Faction.OfPlayer||p.IsSlaveOfColony||p.IsPrisonerOfColony; }
public class N007ScanSummary { public bool HasActiveInfected,HasActiveRecovered,HasInfectedLeft,HasRecovered,HasLeft,HasDied,HasHandled,AllRecoveredOrDied,AllTerminal,AllLeftOrDead,AllDied; }
public class Harness {
 int CurrentNarrativeTick, completions;
 const int N007ReferenceGraceTicks=60000;
 static readonly Map home=new Map();
 static Pawn Pawn(bool present=true,bool slave=false,bool prisoner=false,bool player=false,float age=4)=>new Pawn {
  Spawned=present,Map=present?home:null,MapHeld=present?home:null,
  IsSlaveOfColony=slave,IsPrisonerOfColony=prisoner,Faction=player?Faction.OfPlayer:null,
  ageTracker=new Age{AgeBiologicalYearsFloat=age}
 };
 void ResolveN004(MouseDisasterN004Record r,MouseDisasterN004Outcome outcome) {
  if(r.outcome!=MouseDisasterN004Outcome.Pending)return;
  r.outcome=outcome; completions++;
 }
 static Pawn ResolveN007Pawn(MouseDisasterN007PawnRecord r,Map m)=>r.pawn;
 static bool HasN007Plague(Pawn p)=>p.plague;
 static int checks;
 static void Check(bool ok,string message) { checks++; if(!ok)throw new Exception(message); }
 static MouseDisasterN004Record Family(Pawn mother,params Pawn[] children)=>new MouseDisasterN004Record{mother=mother,children=children.ToList()};
 public static string Run() {
  var h=new Harness();
  foreach(var record in new[]{Family(Pawn(false),Pawn(slave:true)),Family(Pawn(slave:true),Pawn(false)),
    Family(Pawn(),Pawn(player:true),Pawn(false)),Family(Pawn(false),Pawn(player:true,age:1))}) {
   h.TryResolveN004(record);
   Check(record.outcome==MouseDisasterN004Outcome.FamilyChanged,"mixed family still pending");
  }
  var deadMother=Pawn();deadMother.Dead=true;
  var deadChild=Pawn();deadChild.Dead=true;
  foreach(var pair in new[]{
   Tuple.Create(Family(deadMother,deadChild),MouseDisasterN004Outcome.FamilyDied),
   Tuple.Create(Family(Pawn(),deadChild),MouseDisasterN004Outcome.ChildDied),
   Tuple.Create(Family(Pawn(false),Pawn(false)),MouseDisasterN004Outcome.FamilySeparated),
   Tuple.Create(Family(deadMother,Pawn(false)),MouseDisasterN004Outcome.FamilyChanged),
   Tuple.Create(Family(deadMother,Pawn(slave:true,age:1)),MouseDisasterN004Outcome.ChildEnslaved),
   Tuple.Create(Family(deadMother,Pawn(player:true)),MouseDisasterN004Outcome.ChildSurvived),
   Tuple.Create(Family(Pawn(player:true),Pawn(player:true)),MouseDisasterN004Outcome.FamilySurvived)}) {
   h.TryResolveN004(pair.Item1);Check(pair.Item1.outcome==pair.Item2,"existing family outcome changed");
  }
  h.CurrentNarrativeTick=1800000;
  var growing=Family(Pawn(player:true),Pawn(player:true),Pawn(player:true,age:1));
  h.TryResolveN004(growing);Check(growing.outcome==MouseDisasterN004Outcome.Pending,"young sibling skipped or expired");
  growing.children[1].ageTracker.AgeBiologicalYearsFloat=3;
  h.TryResolveN004(growing);Check(growing.outcome==MouseDisasterN004Outcome.FamilySurvived,"grown family not resolved");
  int count=h.completions;h.TryResolveN004(growing);Check(h.completions==count,"duplicate completion");
  var missing=Family(null,Pawn(player:true));h.TryResolveN004(missing);
  Check(missing.outcome==MouseDisasterN004Outcome.Unknown,"missing mother assumed dead");
  var noChildren=Family(Pawn());h.TryResolveN004(noChildren);
  Check(noChildren.outcome==MouseDisasterN004Outcome.Unknown,"empty loaded family stuck");
  var splitMaps=Family(Pawn(player:true),Pawn(player:true));splitMaps.children[0].MapHeld=new Map();
  h.TryResolveN004(splitMaps);Check(splitMaps.outcome==MouseDisasterN004Outcome.Unknown,"unresolved adult family lacks timeout");
  foreach(bool infected in new[]{false,true})foreach(int identity in new[]{0,1,2}) {
   var p=Pawn(slave:identity==0,prisoner:identity==1,player:identity==2);p.plague=infected;
   var pr=new MouseDisasterN007PawnRecord{pawn=p};
   var record=new MouseDisasterN007Record{pawnRecords=new List<MouseDisasterN007PawnRecord>{pr,new MouseDisasterN007PawnRecord{leftMap=true,recovered=true}}};
   var summary=h.ScanN007Record(record,home);
   Check(pr.handled&&summary.AllLeftOrDead&&summary.AllTerminal,"actual ownership ignored by plague scan");
   Check(!summary.HasActiveInfected&&!summary.HasActiveRecovered,"colony pawn still selectable as visitor");
   Check(pr.recovered==!infected,"custody cured plague");
  }
  var carried=Pawn();carried.Spawned=false;carried.Map=null;
  var carryRecord=new MouseDisasterN007Record{pawnRecords=new List<MouseDisasterN007PawnRecord>{new MouseDisasterN007PawnRecord{pawn=carried}}};
  var carrySummary=h.ScanN007Record(carryRecord,home);
  Check(!carrySummary.AllLeftOrDead&&!carryRecord.pawnRecords[0].leftMap,"carried patient counted as gone");
  Check(MouseDisasterNarrativePolicy.IsAidComplete(true,false,2,1,1,1,0),"identity history double-counted settled pawn");
  Check(MouseDisasterNarrativePolicy.IsAidComplete(true,false,2,1,0,1,1),"partial employment identity exception lost");
  Check(!MouseDisasterNarrativePolicy.IsAidComplete(true,false,2,1,0,1,0),"dead or missing identity counted as aid");
  Check(!MouseDisasterNarrativePolicy.IsAidComplete(true,false,2,0,0,2,2),"all identities changed counted as aid");
  Check(!MouseDisasterNarrativePolicy.IsAidComplete(true,true,2,1,1,0,0),"driven group counted as aid");
  return "PASS: "+checks+" mixed narrative assertions using production resolvers.";
 }
'@
Add-Type -TypeDefinition ($stub + $methods + "`n}}" + $policy)
[MixedNarrativeTests.Harness]::Run()
$journal=Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Journal.cs') -Raw
if($journal -notmatch 'p.identityChanged && p.end == NarrativePawnEnd.Detained') { throw 'Identity exception must exclude deaths and missing pawns' }
$active=Get-CSharpMethod $n007 'GetActiveN007Pawns'
if($active -notmatch 'IsPlayerAffiliatedRatkin') { throw 'Acquired plague patients remain eligible for release' }
