$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$genes = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Genes.cs') -Raw
$lifecycle = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Lifecycle.cs') -Raw
$methods = Get-CSharpMethod $genes 'HasAnyMouseDisasterGene'
foreach ($name in 'TryEnsureToddlerCompatibilityHediffs','EnsureHediffPresentIfMissing','RemoveHediffIfPresent') {
    $methods += Get-CSharpMethod $lifecycle $name
}
$stub = @'
using System;
using System.Collections.Generic;
namespace GeneLifecycleTests {
public static class ModsConfig { public static bool BiotechActive=true; }
public class GeneDef {}
public class GeneTracker {
 public HashSet<GeneDef> active=new HashSet<GeneDef>(); public int calls;
 public bool HasActiveGene(GeneDef def) { calls++; return active.Contains(def); }
}
public class HediffDef {}
public class Hediff { public HediffDef def; public float Severity; }
public class HediffSet {
 public Dictionary<HediffDef,Hediff> entries=new Dictionary<HediffDef,Hediff>();
 public Hediff GetFirstHediffOfDef(HediffDef def)=>entries.TryGetValue(def,out var h)?h:null;
}
public class Health {
 public HediffSet hediffSet=new HediffSet(); public int adds,removes;
 public void AddHediff(Hediff h) { adds++; hediffSet.entries[h.def]=h; }
 public void RemoveHediff(Hediff h) { removes++; hediffSet.entries.Remove(h.def); }
}
public static class HediffMaker { public static Hediff MakeHediff(HediffDef def,Pawn pawn)=>new Hediff{def=def}; }
public class Age { public float AgeBiologicalYearsFloat; }
public class Pawn { public GeneTracker genes=new GeneTracker(); public Health health=new Health(); public Age ageTracker=new Age(); public bool managed=true,visitor,player; }
public static class MouseDisasterGeneRestorePolicy {
 public static bool ShouldApplyRimTalkToddlerCompatibility(bool isMouseDisasterPawn,bool isIncidentVisitor,bool isPlayerAffiliatedRatkin)=>isMouseDisasterPawn || isIncidentVisitor || isPlayerAffiliatedRatkin;
}
public static class Harness {
 private static List<GeneDef> pool=new List<GeneDef>{new GeneDef(),new GeneDef(),new GeneDef(),new GeneDef(),new GeneDef()};
 private static List<GeneDef> GetMouseDisasterGenePool()=>pool;
 private static bool syncingToddlerCompat;
 private static HediffDef toddlersLearningToWalkDef=new HediffDef(),toddlersLearningManipulationDef=new HediffDef(),toddlersLonelyDef=new HediffDef(),rimTalkToddlerLanguageLearningDef=new HediffDef(),rimTalkBabyBabblingDef=new HediffDef();
 private static void ResolveToddlerCompatibilityDefsIfNeeded() {}
 private static bool IsMouseDisasterPawn(Pawn p)=>p.managed;
 private static bool IsMouseDisasterIncidentVisitor(Pawn p)=>p.visitor;
 private static bool IsPlayerAffiliatedRatkin(Pawn p)=>p.player;
 private static int checks;
 private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
 public static int Run() {
  Check(!HasAnyMouseDisasterGene(null),"null pawn"); var pawn=new Pawn();
  Check(!HasAnyMouseDisasterGene(pawn),"no genes");
  pawn.genes.active.Add(pool[0]); pawn.genes.calls=0;
  Check(HasAnyMouseDisasterGene(pawn) && pawn.genes.calls==1,"first gene did not short circuit");
  pawn.genes.active.Clear(); pawn.genes.active.Add(pool[4]); pawn.genes.calls=0;
  Check(HasAnyMouseDisasterGene(pawn) && pawn.genes.calls==5,"last gene missing");
  ModsConfig.BiotechActive=false; Check(!HasAnyMouseDisasterGene(pawn),"DLC guard"); ModsConfig.BiotechActive=true;
  var tracker=pawn.genes; pawn.genes=null; Check(!HasAnyMouseDisasterGene(pawn),"null tracker"); pawn.genes=tracker;
  for(int i=0;i<1000;i++) HasAnyMouseDisasterGene(pawn);
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<50000;i++) HasAnyMouseDisasterGene(pawn);
  long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(allocated==0,"gene query allocated "+allocated+" bytes");
  TryEnsureToddlerCompatibilityHediffs(pawn);
  Check(pawn.health.adds==1 && pawn.health.hediffSet.GetFirstHediffOfDef(rimTalkBabyBabblingDef)!=null,"infant initialization");
  for(int i=0;i<100;i++) TryEnsureToddlerCompatibilityHediffs(pawn);
  Check(pawn.health.adds==1,"unchanged state added duplicate hediffs");
  pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(rimTalkBabyBabblingDef));
  Check(pawn.health.hediffSet.entries.Count==0 && pawn.health.adds==1,"provider removal reinserted state");
  pawn.ageTracker.AgeBiologicalYearsFloat=1; TryEnsureToddlerCompatibilityHediffs(pawn);
  Check(pawn.health.hediffSet.entries.Count==4,"toddler transition");
  int calls=pawn.health.adds; TryEnsureToddlerCompatibilityHediffs(pawn); Check(pawn.health.adds==calls,"toddler duplicates");
  pawn.ageTracker.AgeBiologicalYearsFloat=3; TryEnsureToddlerCompatibilityHediffs(pawn);
  Check(pawn.health.hediffSet.entries.Count==0 && !syncingToddlerCompat,"older child cleanup or guard leaked");
  var unrelated=new Pawn{managed=false}; TryEnsureToddlerCompatibilityHediffs(unrelated); Check(unrelated.health.adds==0,"unrelated pawn changed");
  rimTalkBabyBabblingDef=null; TryEnsureToddlerCompatibilityHediffs(new Pawn()); Check(!syncingToddlerCompat,"missing provider guard leaked");
  return checks;
 }
'@
Add-Type -TypeDefinition ($stub + $methods + '} }')
$checks = [GeneLifecycleTests.Harness]::Run()
$identity = Get-Content (Join-Path $root '1.6/Source/IdentityLifecyclePatches.cs') -Raw
if ($identity -match 'MouseDisasterHealthTrackerRemoveHediffPatch|nameof\(Pawn_HealthTracker.RemoveHediff\)') { throw 'RemoveHediff feedback hook restored' }
$birth = Get-Content (Join-Path $root '1.6/Source/BirthPatches.cs') -Raw
$age = Get-Content (Join-Path $root '1.6/Source/AgePatches.cs') -Raw
foreach ($source in @($birth,$age,$identity)) {
    if ($source -notmatch 'NotifyMouseDisasterPawnIdentityOrLifeStageChanged') { throw 'Required lifecycle normalization hook missing' }
}
Write-Host "PASS: $checks production-method assertions; 50,000 warmed gene queries allocate 0 bytes with engine doubles; lifecycle hook checks. Not an in-game reproduction."
