$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$camp = Get-Content (Join-Path $root '1.6/Source/Incidents/RefugeeMassacre.cs') -Raw
$predation = Get-Content (Join-Path $root '1.6/Source/MapComponent_MouseDisasterPredation.cs') -Raw
$methods = @('Register','ForbiddenCorpse','ForbiddenTarget','WildPredator','Hungry','TryFoodJob','MapComponentTick','AddPredator','IsPredatorFoodJob','BeginMealJob','ObserveMeal','TryMakeExitJob','TryStartExitJob','IsTracked','IsHomeCell','RefreshCandidateCache','CanTargetRatkin','FindVictim','FindCorpse','HasEventFoodOnMap') | ForEach-Object { Get-CSharpMethod $predation $_ }
$methods += Get-CSharpMethod $camp 'AllowedWeapon'
$methods += Get-CSharpMethod $camp 'Sponsors'
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace RefugeeTests {
public enum TechLevel { Undefined, Animal, Neolithic, Medieval, Industrial, Spacer, Ultra }
public enum FactionRelationKind { Neutral, Ally }
public class Stuff { public List<string> categories = new List<string> { "Woody" }; }
public class ThingDef { public bool IsWeapon=true, IsMeleeWeapon=true, MadeFromStuff; public TechLevel techLevel=TechLevel.Neolithic; public string defName="Club"; public List<string> stuffCategories=new List<string>{"Woody"}; public Stuff stuffProps=new Stuff(); }
public static class ThingDefOf { public static ThingDef WoodLog=new ThingDef(); }
public class Thing { public bool Spawned=true,Destroyed; public Map Map; public IntVec3 Position; public Map MapHeld=>Map; public IntVec3 PositionHeld=>Position; }
public class Race { public bool Animal=true,predator=true,canBePredatorPrey=true,IsFlesh=true; public float FoodLevelPercentageWantEat=0.3f; public float maxPreyBodySize=999; public ThingDef corpseDef=new ThingDef(); public bool CanEverEat(ThingDef def)=>true; public bool CanEverEat(Thing thing)=>true; }
public class Melee { public object TryGetMeleeVerb(object target)=>this; }
public class Age { public float AgeBiologicalYearsFloat=20; }
public class Food { public float CurLevelPercentage=0.2f; }
public class Needs { public Food food=new Food(); }
public class Pawn:Thing { public Corpse Corpse; public bool ratkin=true,Dead,Downed,InMentalState,player,reachable=true; public float BodySize=0.5f; public Faction Faction; public Race RaceProps=new Race(); public Melee meleeVerbs=new Melee(); public Age ageTracker=new Age(); public Needs needs=new Needs(); public Job CurJob; public string CurJobDef=>CurJob?.def; public Jobs jobs=new Jobs(); public bool CanReach(Thing thing,PathEndMode mode,Danger danger)=>!(thing is Pawn p) || p.reachable; }
public enum JobCondition { InterruptForced }
public class Jobs { public Job LastJob; public void StartJob(Job job,JobCondition condition) { LastJob=job; } }
public class MapComponent { public virtual void MapComponentTick() {} }
public class LookTargets { public LookTargets(List<Pawn> pawns) {} }
public static class LetterDefOf { public static string ThreatSmall="small"; }
public class LetterStack { public int count; public void ReceiveLetter(string label,string text,string def,LookTargets targets) { count++; } }
public static class Extensions { public static T RandomElement<T>(this List<T> list)=>list[0]; public static string Translate(this string key)=>key; }
public class Corpse:Thing { public Pawn InnerPawn; public bool IngestibleNow=true; }
public enum Danger { Deadly } public enum PathEndMode { Touch } public enum ThingRequestGroup { Corpse }
public class Job { public string def; public Thing target; public bool killIncappedTarget,exitMapOnArrival; public int count; }
public static class JobDefOf { public const string PredatorHunt="hunt",Ingest="eat",Goto="go"; }
public static class JobMaker { public static Job MakeJob(string def,Thing target)=>new Job{def=def,target=target}; public static Job MakeJob(string def,IntVec3 target)=>new Job{def=def}; }
public struct IntVec3 { public int value; public static implicit operator IntVec3(int value)=>new IntVec3{value=value}; }
public static class RCellFinder { public static bool exitAvailable=true; public static bool TryFindRandomExitSpot(Pawn pawn,out IntVec3 exit) {exit=new IntVec3(); return exitAvailable;} }
public static class Distances { public static int DistanceToSquared(this IntVec3 a,IntVec3 b)=>(a.value-b.value)*(a.value-b.value); }
public class MapPawns { public List<Pawn> AllPawnsSpawned=new List<Pawn>(); }
public class Lister { public List<Thing> corpses=new List<Thing>(); public List<Thing> ThingsInGroup(ThingRequestGroup group)=>corpses; }
public class Areas { public HashSet<int> Home=new HashSet<int>(); }
public class HomeArea { public HashSet<IntVec3> cells=new HashSet<IntVec3>(); public bool this[IntVec3 cell]=>cells.Contains(cell); }
public class AreaManager { public HomeArea Home=new HomeArea(); }
public class Map { public int uniqueID; public AreaManager areaManager=new AreaManager(); public MapPawns mapPawns=new MapPawns(); public Lister listerThings=new Lister(); }
public class MouseDisasterEventGroup { public int id; public List<int> predationRolledMaps=new List<int>(); }
public static class MouseDisasterRuntime { public static bool AllowsNewContent=true; }
public static class MouseDisasterSettings { public const int MinWildPredatorSearchIntervalTicks=60, MaxWildPredatorSearchIntervalTicks=1200, DefaultWildPredatorSearchIntervalTicks=250; }
public class Settings { public float refugeePredationChancePercent=10; public bool outsidePredatorsFollowDifficulty; public bool wildPredatorsAvoidRatkinWhenFed=true, wildPredatorsLeaveAfterFed, wildPredatorsHuntHomeAreaRatkin; public int wildPredatorSearchIntervalTicks=250; }
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public static class Rand { public static int calls; public static float value; public static bool Chance(float chance) { calls++; return value < chance; } }
public class TickManager { public int TicksGame=1000; }
public class FactionDef { public bool humanlikeFaction=true; }
public class Faction { public bool IsPlayer,Hidden,defeated,temporary,hostile; public FactionDef def=new FactionDef(); public FactionRelationKind relation; public static Faction OfPlayer=new Faction(); public bool HostileTo(Faction other)=>hostile; public FactionRelationKind RelationKindWith(Faction other)=>relation; }
public class Settlement { public Faction Faction; }
public class FactionManager { public List<Faction> AllFactionsListForReading=new List<Faction>(); }
public class WorldObjects { public List<Settlement> Settlements=new List<Settlement>(); }
public static class Find { public static TickManager TickManager=new TickManager(); public static FactionManager FactionManager=new FactionManager(); public static WorldObjects WorldObjects=new WorldObjects(); public static LetterStack LetterStack=new LetterStack(); }
public static class MouseDisasterUtility { public static bool IsRatkin(Pawn pawn)=>pawn!=null && pawn.ratkin; public static bool IsPlayerAffiliatedRatkin(Pawn pawn)=>pawn.player; }
public class MouseDisasterPredator { public Pawn pawn; public bool outside,firstHunt=true,mealJobActive,fedAfterMeal; public float mealJobStartFoodLevel=-1; }
public class Harness : MapComponent {
 private const float MealDetectionEpsilon=0.001f; private int nextTick, arrivals, candidateCacheTick=-1;
 private static int SearchIntervalTicks { get { int value=MouseDisasterMod.Settings?.wildPredatorSearchIntervalTicks ?? MouseDisasterSettings.DefaultWildPredatorSearchIntervalTicks; return Math.Max(MouseDisasterSettings.MinWildPredatorSearchIntervalTicks,Math.Min(MouseDisasterSettings.MaxWildPredatorSearchIntervalTicks,value)); } }
private void SpawnOutside() { arrivals++; }
private Map map=new Map(); private List<int> selectedGroups=new List<int>(); private List<Pawn> prey=new List<Pawn>(); private int pendingTick=-1;
private Dictionary<Pawn,MouseDisasterPredator> byPawn=new Dictionary<Pawn,MouseDisasterPredator>();
 private Dictionary<Pawn,int> nextSearch=new Dictionary<Pawn,int>(); private HashSet<Pawn> vanillaFallback=new HashSet<Pawn>(); private List<Pawn> cachedWildPrey=new List<Pawn>(); private List<Corpse> cachedRatkinCorpses=new List<Corpse>();
private List<MouseDisasterPredator> predators=new List<MouseDisasterPredator>();
private static int checks;
private static void Check(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
public static int Run() {
 var group=new MouseDisasterEventGroup{id=12}; var first=new Harness(); var second=new Harness(); second.map.uniqueID=2;
 var a=new Pawn(); var b=new Pawn(); Rand.value=0.05f;
 first.Register(group,new[]{a}); first.Register(group,new[]{a,b});
 Check(Rand.calls==1,"same event/map rolled twice"); Check(first.prey.Count==2,"batch merge lost or duplicated pawns"); Check(first.pendingTick==1120,"pending delay changed");
 second.Register(group,new[]{a}); Check(Rand.calls==2,"second map did not roll independently");
 Rand.value=0.5f; var failed=new MouseDisasterEventGroup{id=13}; first.Register(failed,new[]{new Pawn()}); first.Register(failed,new[]{new Pawn()});
 Check(Rand.calls==3 && first.prey.Count==2,"failed roll retried");
 MouseDisasterMod.Settings.refugeePredationChancePercent=0; Rand.value=0; first.Register(new MouseDisasterEventGroup{id=14},new[]{new Pawn()}); Check(first.prey.Count==2,"zero chance succeeded");
 MouseDisasterMod.Settings.refugeePredationChancePercent=100; Rand.value=0.999f; first.Register(new MouseDisasterEventGroup{id=15},new[]{new Pawn()}); Check(first.prey.Count==3,"100 percent failed");
 MouseDisasterRuntime.AllowsNewContent=false; int calls=Rand.calls; first.Register(new MouseDisasterEventGroup{id=16},new[]{a}); Check(Rand.calls==calls,"disabled generation rolled");
 Check(AllowedWeapon(new ThingDef()),"primitive melee rejected");
 Check(AllowedWeapon(new ThingDef{techLevel=TechLevel.Medieval}),"medieval melee rejected");
 Check(!AllowedWeapon(new ThingDef{techLevel=TechLevel.Industrial}),"industrial melee allowed");
 Check(!AllowedWeapon(new ThingDef{techLevel=TechLevel.Spacer}),"spacer melee allowed");
 Check(!AllowedWeapon(new ThingDef{IsMeleeWeapon=false,defName="Gun_Autopistol"}),"gun allowed");
 Check(!AllowedWeapon(new ThingDef{IsMeleeWeapon=false,defName="Bow_Great"}),"greatbow allowed");
 Check(AllowedWeapon(new ThingDef{IsMeleeWeapon=false,defName="Bow_Short"}),"short bow rejected");
 Check(AllowedWeapon(new ThingDef{MadeFromStuff=true}),"wood weapon rejected");
 Check(!AllowedWeapon(new ThingDef{MadeFromStuff=true,stuffCategories=new List<string>{"Metallic"}}),"incompatible material allowed");
 var corpse=new Corpse{InnerPawn=a,Map=first.map,Position=5}; a.Corpse=corpse;
 Check(!first.ForbiddenCorpse(corpse),"outside corpse forbidden"); first.map.areaManager.Home.cells.Add(5);
 Check(first.ForbiddenCorpse(corpse),"home corpse was not blocked by default"); MouseDisasterMod.Settings.wildPredatorsHuntHomeAreaRatkin=true;
 Check(!first.ForbiddenCorpse(corpse),"home corpse setting did not allow hunting"); a.player=true; Check(first.ForbiddenCorpse(a),"player-affiliated Ratkin was exposed"); a.player=false; MouseDisasterMod.Settings.wildPredatorsHuntHomeAreaRatkin=false;
 Check(first.ForbiddenCorpse(a),"hunt transition bypassed home area");
 corpse.Map=second.map; Check(!first.ForbiddenCorpse(corpse),"other map home area leaked"); corpse.Map=first.map;
 a.ratkin=false; Check(!first.ForbiddenCorpse(corpse),"non-ratkin affected"); a.ratkin=true; corpse.Spawned=false; Check(first.ForbiddenCorpse(corpse),"carried corpse bypassed home area");
 corpse.Map=null; Check(!first.ForbiddenCorpse(corpse),"off-map corpse affected");
 Check(Sponsors().Count==0,"no faction offered task");
 var neutral=new Faction(); var ally=new Faction{relation=FactionRelationKind.Ally}; var enemy=new Faction{hostile=true};
 Find.FactionManager.AllFactionsListForReading.AddRange(new[]{neutral,ally,enemy});
 foreach(var f in Find.FactionManager.AllFactionsListForReading) Find.WorldObjects.Settlements.Add(new Settlement{Faction=f});
 Check(Sponsors().SequenceEqual(new[]{ally}),"ally priority broken"); ally.defeated=true;
 Check(Sponsors().SequenceEqual(new[]{neutral}),"neutral fallback broken"); neutral.Hidden=true;
 Check(Sponsors().Count==0,"hidden/defeated/hostile sponsor allowed");
 var hunting=new Harness(); var hunter=new Pawn{Map=hunting.map,ratkin=false}; var record=new MouseDisasterPredator{pawn=hunter,outside=true}; hunting.byPawn[hunter]=record; hunting.predators.Add(record);
 var adult=new Pawn{Map=hunting.map,Position=1}; var child=new Pawn{Map=hunting.map,Position=10,ageTracker=new Age{AgeBiologicalYearsFloat=4}};
 hunting.map.mapPawns.AllPawnsSpawned.AddRange(new[]{adult,child});
 Job job; Check(hunting.TryFoodJob(hunter,out job) && job.target==child && job.killIncappedTarget,"child priority or downed finish broken");
 Check(hunting.TryFoodJob(hunter,out job) && job==null,"path search cooldown ignored");
 Find.TickManager.TicksGame+=250; child.reachable=false;
 Check(hunting.TryFoodJob(hunter,out job) && job.target==adult,"unreachable child blocks reachable adult");
 Find.TickManager.TicksGame+=250; adult.player=true;
 Check(hunting.TryFoodJob(hunter,out job) && job.exitMapOnArrival,"hungry outsider without eligible prey stayed");
 Find.TickManager.TicksGame+=250; var foodCorpse=new Corpse{Map=hunting.map,InnerPawn=child,Position=7}; hunting.map.listerThings.corpses.Add(foodCorpse);
 Check(hunting.TryFoodJob(hunter,out job) && job.def==JobDefOf.Ingest && job.target==foodCorpse,"outsider did not eat ratkin corpse");
 Find.TickManager.TicksGame+=250; hunting.map.areaManager.Home.cells.Add(7);
 Check(hunting.TryFoodJob(hunter,out job) && job.exitMapOnArrival,"home corpse prevented exit");
 Find.TickManager.TicksGame+=250; hunter.needs.food.CurLevelPercentage=0.8f;
 Check(hunting.TryFoodJob(hunter,out job) && job==null,"fed outsider left early");
 var nativeCorpseHarness=new Harness(); var nativeHunter=new Pawn{Map=nativeCorpseHarness.map,ratkin=false}; var nativeRecord=new MouseDisasterPredator{pawn=nativeHunter,outside=false}; nativeCorpseHarness.byPawn[nativeHunter]=nativeRecord; nativeCorpseHarness.predators.Add(nativeRecord);
 var nativeVictim=new Pawn{Map=nativeCorpseHarness.map,Dead=true,Position=3}; var nativeCorpse=new Corpse{Map=nativeCorpseHarness.map,InnerPawn=nativeVictim,Position=3}; nativeVictim.Corpse=nativeCorpse; nativeCorpseHarness.prey.Add(nativeVictim); nativeCorpseHarness.map.areaManager.Home.cells.Add(3); MouseDisasterMod.Settings.wildPredatorsHuntHomeAreaRatkin=true;
 Check(nativeCorpseHarness.TryFoodJob(nativeHunter,out job) && job.def==JobDefOf.Ingest && job.target==nativeCorpse,"event predator did not eat an allowed home corpse"); MouseDisasterMod.Settings.wildPredatorsHuntHomeAreaRatkin=false;
 var blockedNative=new Harness(); var blockedHunter=new Pawn{Map=blockedNative.map,ratkin=false}; var blockedRecord=new MouseDisasterPredator{pawn=blockedHunter,outside=false}; blockedNative.byPawn[blockedHunter]=blockedRecord; blockedNative.predators.Add(blockedRecord); var blockedTarget=new Pawn{Map=blockedNative.map,Position=4}; blockedNative.map.areaManager.Home.cells.Add(4); blockedNative.prey.Add(blockedTarget);
 Check(blockedNative.TryFoodJob(blockedHunter,out job) && job==null && blockedNative.byPawn.ContainsKey(blockedHunter),"event target released native predator to vanilla food search");
 var fed=new Harness(); var fedHunter=new Pawn{Map=fed.map,ratkin=false}; var fedRecord=new MouseDisasterPredator{pawn=fedHunter,outside=true}; fed.byPawn[fedHunter]=fedRecord; fed.predators.Add(fedRecord);
 var fedTarget=new Pawn{Map=fed.map,Position=1}; fed.map.mapPawns.AllPawnsSpawned.Add(fedTarget); fedHunter.needs.food.CurLevelPercentage=0.8f;
 Check(fed.TryFoodJob(fedHunter,out job) && job==null,"fed predator ignored default avoidance setting"); MouseDisasterMod.Settings.wildPredatorsAvoidRatkinWhenFed=false;
 Check(fed.TryFoodJob(fedHunter,out job) && job.target==fedTarget,"fed predator did not hunt when avoidance was disabled"); MouseDisasterMod.Settings.wildPredatorsAvoidRatkinWhenFed=true;
 var leaving=new Harness(); var leavingHunter=new Pawn{Map=leaving.map,ratkin=false}; var leavingRecord=new MouseDisasterPredator{pawn=leavingHunter,outside=true}; leaving.byPawn[leavingHunter]=leavingRecord; leaving.predators.Add(leavingRecord);
 var leavingTarget=new Pawn{Map=leaving.map,Position=1}; leaving.map.mapPawns.AllPawnsSpawned.Add(leavingTarget); leavingHunter.needs.food.CurLevelPercentage=0.2f;
 Check(leaving.TryFoodJob(leavingHunter,out job) && job.target==leavingTarget,"meal tracking setup failed"); leavingHunter.CurJob=job; Find.TickManager.TicksGame+=250; leaving.MapComponentTick();
 leavingHunter.needs.food.CurLevelPercentage=0.8f; MouseDisasterMod.Settings.wildPredatorsLeaveAfterFed=true; Find.TickManager.TicksGame+=250; leaving.MapComponentTick();
 Check(leavingHunter.jobs.LastJob?.exitMapOnArrival==true,"fed predator did not leave when configured"); MouseDisasterMod.Settings.wildPredatorsLeaveAfterFed=false;
 Find.TickManager.TicksGame+=250; hunter.needs.food.CurLevelPercentage=0.2f; MouseDisasterMod.Settings.outsidePredatorsFollowDifficulty=true;
 Check(!hunting.TryFoodJob(hunter,out job),"difficulty mode did not return vanilla control");
 Check(!hunting.TryFoodJob(hunter,out job),"cooldown blocked vanilla fallback");
 Find.TickManager.TicksGame+=250; child.reachable=true;
 Check(hunting.TryFoodJob(hunter,out job) && job.target==child,"returning ratkin failed to regain priority");
 Find.TickManager.TicksGame+=250; hunter.Faction=Faction.OfPlayer;
 Check(!hunting.TryFoodJob(hunter,out job),"tamed predator still controlled");
 hunter.Faction=null; record.outside=false; hunting.prey.Clear(); Find.TickManager.TicksGame+=250;
 Check(!hunting.TryFoodJob(hunter,out job) && !hunting.byPawn.ContainsKey(hunter),"native predator did not release after event prey left");
 MouseDisasterRuntime.AllowsNewContent=true;
 foreach(int state in new[]{0,1,2,3,4}) {
  var pending=new Harness();var visitor=new Pawn{Map=pending.map};pending.prey.Add(visitor);pending.pendingTick=Find.TickManager.TicksGame;
  if(state==1)visitor.Map=new Map();
  if(state==2)visitor.Dead=true;
  if(state==3)visitor.player=true;
  if(state==4)visitor.Spawned=false;
  pending.MapComponentTick();
  Check(pending.arrivals==(state==0?1:0),"delayed predator arrival ignored visitor state: "+state);
  Check(pending.pendingTick==-1,"pending arrival was not consumed");
 }
 var scheduled=new Harness(); scheduled.prey.Add(new Pawn{Map=scheduled.map}); scheduled.pendingTick=Find.TickManager.TicksGame+120; MouseDisasterMod.Settings.wildPredatorSearchIntervalTicks=1200; scheduled.MapComponentTick();
 Check(scheduled.nextTick==Find.TickManager.TicksGame+120,"search interval delayed pending event arrival"); MouseDisasterMod.Settings.wildPredatorSearchIntervalTicks=250;
 var stopped=new Harness();stopped.prey.Add(new Pawn{Map=stopped.map});stopped.pendingTick=Find.TickManager.TicksGame;
 MouseDisasterRuntime.AllowsNewContent=false;stopped.MapComponentTick();
 Check(stopped.arrivals==0,"disabled content spawned a delayed predator");
 return checks;
}
'@
Add-Type -TypeDefinition ($stub + ($methods -join "`n") + '} }')
$checks = [RefugeeTests.Harness]::Run()
[xml]$defs = Get-Content (Join-Path $root 'Defs/IncidentDefs/RefugeeMassacre.xml') -Raw
$steps = @($defs.Defs.MapGeneratorDef.genSteps.li)
if ($steps -contains 'Settlement' -or $steps -contains 'AncientTurret' -or $defs.Defs.MapGeneratorDef.ParentName) { throw 'Camp generator inherits fortified content' }
if ($defs.Defs.WorldObjectDef.worldObjectClass -ne 'MouseDisaster.MouseDisasterRefugeeSite') { throw 'Wrong camp world object' }
foreach ($path in @('Languages/English/Keyed/MouseDisasterPredation.xml','Languages/ChineseSimplified/Keyed/MouseDisasterPredation.xml')) { [xml]$language = Get-Content (Join-Path $root $path) -Raw }
$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$expose = Get-CSharpMethod $settings 'ExposeData'
$reset = Get-CSharpMethod $settings 'ResetToDefaults'
foreach ($name in @('refugeePredationChancePercent','refugeePredationFightBack','outsidePredatorsFollowDifficulty','wildPredatorsAvoidRatkinWhenFed','wildPredatorsLeaveAfterFed','wildPredatorsHuntHomeAreaRatkin','wildPredatorSearchIntervalTicks')) {
    if ($expose -notmatch ('ref ' + $name + ', "' + $name + '"') -or $reset -notmatch $name) { throw "Missing settings persistence/reset: $name" }
}
$clamp = Get-CSharpMethod $settings 'ClampValues'
if ($clamp -notmatch 'wildPredatorSearchIntervalTicks = Mathf\.Clamp') { throw 'Missing predation search interval clamp' }
foreach ($name in @('prey','selectedGroups','predators','pendingTick')) {
    if ($predation -notmatch ('Scribe_\w+\.Look\(ref ' + $name + ',')) { throw "Missing map persistence: $name" }
}
Write-Host "PASS: $checks production-method assertions; camp generator and localization XML. Engine doubles, not in-game evidence."
