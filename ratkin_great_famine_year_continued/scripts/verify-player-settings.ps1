$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$birth = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Birth.cs') -Raw
$tail = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.TailBiting.cs') -Raw
$methods = (Get-CSharpMethod $birth 'TryInheritBirthStatus') + (Get-CSharpMethod $birth 'IsColonyBirthMother')
foreach ($name in 'CanBeTailBiteTarget','IsDefenselessTailTarget','CanReceiveTailBiteNow','CanAttemptTailBite','RecordTailBiteAttempt','RecordTailBiteVictim') { $methods += Get-CSharpMethod $tail $name }
$stub = @'
using System;
using System.Collections.Generic;
public enum GuestStatus { Guest, Prisoner, Slave }
public class Faction { public static Faction OfPlayer=new Faction(); }
public class Guest { public Faction host; public GuestStatus status; public int calls; public void SetGuestStatus(Faction f,GuestStatus s){host=f;status=s;calls++;} }
public class Capacity { public bool Moving=true,Manipulation=true,CanBeAwake=true; public bool CapableOf(string c)=>c=="Moving"?Moving:Manipulation; }
public static class PawnCapacityDefOf { public static string Moving="Moving",Manipulation="Manipulation"; }
public class Health { public Capacity capacities=new Capacity(); }
public class Pawn {
 public bool ratkin=true,genes=true,Dead,Spawned=true,IsPrisonerOfColony,IsSlaveOfColony,young=true,awake,Downed,tail=true;
 public int thingIDNumber; public Guest guest=new Guest(); public Faction Faction; public Health health=new Health();
 public void SetFaction(Faction f){Faction=f;guest.status=GuestStatus.Guest;guest.host=null;}
 public bool Awake()=>awake;
}
public class Settings { public bool enableExperimentalIdentityInheritance; }
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public class TickManager { public int TicksGame=10000; }
public static class Find { public static TickManager TickManager=new TickManager(); }
public class Harness {
 static bool IsRatkin(Pawn p)=>p?.ratkin==true;
 static bool HasAnyMouseDisasterGene(Pawn p)=>p.genes;
 static bool IsMouseEggBaby(Pawn p)=>p.young;
 static object GetNaturalRatTail(Pawn p)=>p.tail?(object)p:null;
 const int TailBiteVictimCooldownTicks=2500,TailBiteAttemptCooldownTicks=600;
 static Dictionary<int,int> TailBiteLastVictimTickByPawnId=new Dictionary<int,int>(),TailBiteLastAttemptTickByPawnId=new Dictionary<int,int>();
 static int checks; static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 public static int Run(){
  var foreign=new Faction();var mother=new Pawn{Faction=foreign,IsPrisonerOfColony=true};var baby=new Pawn{Faction=Faction.OfPlayer};
  TryInheritBirthStatus(baby,mother);Check(baby.guest.calls==0,"disabled setting changed birth");
  MouseDisasterMod.Settings.enableExperimentalIdentityInheritance=true;
  TryInheritBirthStatus(baby,mother);Check(baby.Faction==foreign&&baby.guest.host==Faction.OfPlayer&&baby.guest.status==GuestStatus.Prisoner,"prisoner birth");
  mother.IsPrisonerOfColony=false;mother.IsSlaveOfColony=true;mother.Faction=Faction.OfPlayer;
  TryInheritBirthStatus(baby,mother);Check(baby.Faction==Faction.OfPlayer&&baby.guest.status==GuestStatus.Slave&&baby.guest.host==Faction.OfPlayer,"slave birth");
  mother.IsSlaveOfColony=false;TryInheritBirthStatus(baby,mother);
  Check(baby.Faction==Faction.OfPlayer&&baby.guest.status==GuestStatus.Guest&&baby.guest.host==null,"colonist birth");
  foreach(int invalid in new[]{0,1,2,3,4,5}) {
   mother=new Pawn{Faction=Faction.OfPlayer};baby=new Pawn();
   if(invalid==0)mother.genes=false;if(invalid==1)mother.ratkin=false;if(invalid==2)baby.ratkin=false;
   if(invalid==3)baby.Dead=true;if(invalid==4)mother.Faction=foreign;if(invalid==5)mother=null;
   TryInheritBirthStatus(baby,mother);Check(baby.guest.calls==0,"unrelated birth changed: "+invalid);
  }
  TryInheritBirthStatus(null,mother);
  var actor=new Pawn{thingIDNumber=1};var target=new Pawn{thingIDNumber=2,IsPrisonerOfColony=true};
  Check(CanBeTailBiteTarget(target,actor),"sleeping captive not eligible");
  target.awake=true;Check(!CanBeTailBiteTarget(target,actor),"awake capable captive targeted");
  target.Downed=true;Check(CanBeTailBiteTarget(target,actor),"helpless captive not eligible");
  foreach(int invalid in new[]{0,1,2,3,4}) {
   target=new Pawn{thingIDNumber=2,IsPrisonerOfColony=true};
   if(invalid==0)target.Spawned=false;if(invalid==1)target.Dead=true;if(invalid==2)target.IsPrisonerOfColony=false;
   if(invalid==3)target.young=false;if(invalid==4)target.tail=false;
   Check(!CanBeTailBiteTarget(target,actor),"invalid tail target: "+invalid);
  }
  target=new Pawn{thingIDNumber=2,IsPrisonerOfColony=true};
  Check(!CanBeTailBiteTarget(target,target),"self bite allowed");
  RecordTailBiteVictim(target);Check(!CanBeTailBiteTarget(target,actor),"victim cooldown ignored");
  Find.TickManager.TicksGame+=TailBiteVictimCooldownTicks;Check(CanBeTailBiteTarget(target,actor),"victim cooldown failed to expire");
  Check(CanAttemptTailBite(actor),"initial attempt blocked");RecordTailBiteAttempt(actor);Check(!CanAttemptTailBite(actor),"actor cooldown ignored");
  Find.TickManager.TicksGame+=TailBiteAttemptCooldownTicks;Check(CanAttemptTailBite(actor),"actor cooldown failed to expire");
  return checks;
 }
'@
Add-Type -TypeDefinition ($stub + $methods + '}')
"PASS: $([Harness]::Run()) birth-status and tail-biting assertions using production methods."
$entry = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
$window = Get-CSharpMethod $entry 'DoSettingsWindowContents'
if ($window -notmatch 'maxOneColumn = true' -or $window -notmatch 'settingsContentHeight = listing.CurHeight') { throw 'Legacy settings must measure a single scrollable column' }
$biology = Get-CSharpMethod $entry 'DrawBiologySettings'
if ($biology.IndexOf('ref Settings.enableExperimentalIdentityInheritance') -gt $biology.IndexOf('if (Settings.enableChaosRoomPregnancy)')) { throw 'Birth status setting incorrectly depends on spontaneous pregnancy' }
$patch = Get-Content (Join-Path $root '1.6/Source/BirthPatches.cs') -Raw
if ($patch -notmatch 'TryInheritBirthStatus\(pawn, parentPawn\)') { throw 'Birth status is not connected to births' }
'PASS: settings visibility and birth hook. Real birth, AI and save/load remain in-game checks.'
