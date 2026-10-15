$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace EventBehaviorTests {
[AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name,params Type[] arguments) {} }
public enum DevelopmentalStage { Baby, Child, Adult }
public enum HungerCategory { Fed, Starving }
public enum FoodPreferability { Undefined=-1, RawBad=1 }
public enum ThingRequestGroup { FoodSource }
public enum Danger { Some }
public enum PathEndMode { ClosestTouch }
public enum LoadSaveMode { Inactive, PostLoadInit }
public enum LookMode { Reference, Deep, Value }
public interface IExposable { void ExposeData(); }
public static class Scribe { public static LoadSaveMode mode; }
public static class Scribe_Values { public static void Look<T>(ref T value,string key,T fallback=default(T)) {} }
public static class Scribe_Collections {
    public static bool reading;
    private static Dictionary<string,object> saved=new Dictionary<string,object>();
    public static void Look<T>(ref List<T> value,string key,LookMode mode) {}
    public static void Look<T>(ref HashSet<T> value,string key,LookMode mode) {
        if(reading) value=saved.TryGetValue(key,out var data) ? new HashSet<T>((HashSet<T>)data) : null;
        else saved[key]=new HashSet<T>(value ?? new HashSet<T>());
    }
}
public static class Scribe_References { public static void Look<T>(ref T value,string key) {} }
public static class Scribe_Defs { public static void Look<T>(ref T value,string key) {} }
public static class GenDate { public const int TicksPerHour=2500, TicksPerDay=60000; }
public static class Rand { public static float Range(float min,float max)=>(min+max)/2; }
public struct IntVec3 {
    public int x; public static IntVec3 Invalid => new IntVec3 { x=-1 };
    public static bool operator ==(IntVec3 a,IntVec3 b) { return a.x==b.x; }
    public static bool operator !=(IntVec3 a,IntVec3 b) { return a.x!=b.x; }
    public override bool Equals(object value) { return value is IntVec3 p && p.x==x; }
    public override int GetHashCode() { return x; }
}
public class ThingDef { public bool IsNutritionGivingIngestible=true; }
public class Thing {
    public Map Map; public Map MapHeld => Map; public ThingDef def=new ThingDef(); public Faction Faction;
    public bool Spawned=true,Destroyed,IngestibleNow=true,forbidden,fresh=true,dessicated,social=true;
    public int stackCount=10000; public IntVec3 Position; public IntVec3 PositionHeld=>Position; public object ParentHolder;
    public bool IsForbidden(Pawn p)=>forbidden; public bool IsNotFresh()=>!fresh; public bool IsDessicated()=>dessicated;
    public bool IsSociallyProper(Pawn p)=>social;
}
public class Plant:Thing {} public class Corpse:Thing {} public class Building:Thing {} public class Building_Turret:Building {}
public class PawnKindDef {} public class Job {}
public static class FleeUtility { public static Job FleeJob(Pawn pawn,Thing danger,int distance)=>null; }
public class Inventory { public List<Thing> innerContainer=new List<Thing>(); }
public class FoodNeed { public HungerCategory CurCategory; }
public class Needs { public FoodNeed food=new FoodNeed(); }
public class MentalStateDef {}
public static class Log { public static void Warning(string message) { throw new Exception(message); } }
public class MentalHandler {
    public int resets; public MentalStateDef state;
    public void Reset() { resets++; state=null; }
    public bool TryStartMentalState(MentalStateDef def,bool forced,bool forceWake,bool transitionSilently) { state=def; return true; }
}
public class Mind { public object duty; public MentalHandler mentalStateHandler=new MentalHandler(); }
public class Jobs { public int stops; public void StopAll() { stops++; } }
public class Pawn:Thing {
    public int thingIDNumber; public bool Dead,Downed,player,fedOnce,temporarySatiety,thief=true,beggar,canReserve=true,canReach=true,willEat=true;
    public DevelopmentalStage DevelopmentalStage=DevelopmentalStage.Adult;
    public PawnKindDef kindDef=new PawnKindDef(); public bool burning; public bool IsBurning()=>burning;
    public Inventory inventory=new Inventory(); public Needs needs=new Needs(); public Mind mindState=new Mind(); public Jobs jobs=new Jobs(); public Lord lord;
    public MentalStateDef MentalStateDef=>mindState.mentalStateHandler.state;
    public void SetFaction(Faction faction) {
        if(lord!=null) { lord.changedFactionLosses++; lord.RemovePawn(this); }
        Faction=faction; mindState.mentalStateHandler.Reset(); mindState.duty=null; jobs.StopAll();
    }
    public Lord GetLord()=>lord;
    public bool WillEat(Thing food,Pawn getter,bool careIfNotAcceptableForTitle,bool allowVenerated)=>willEat;
    public bool CanReserve(Thing food,int maxPawns,int count)=>canReserve;
    public bool CanReach(Thing food,PathEndMode mode,Danger danger)=>canReach;
    public bool IsCaravanMember()=>false;
    public void PostApplyDamage(DamageInfo info,float total) {}
}
public class Faction { public static Faction OfPlayer=new Faction(); public int kind; public bool temporary; }
public class FactionManager { public List<Faction> AllFactionsListForReading=new List<Faction>(); public void Notify_PawnLeftFaction(Faction f) {} }
public class IncidentDef { public string defName; }
public class TickManager { public int TicksGame; }
public static class Find { public static TickManager TickManager=new TickManager(); public static FactionManager FactionManager=new FactionManager(); }
public class Lister { public List<Thing> foods=new List<Thing>(); public List<Thing> ThingsInGroup(ThingRequestGroup group)=>foods; }
public class ReliefArea { public int TrueCount; public bool this[IntVec3 cell]=>false; }
public class Map {
    public bool IsPlayerHome;
    public Lister listerThings=new Lister(); public ReliefArea area=new ReliefArea(); public MapComponent_MouseDisasterFoodTargets cache;
    public T GetComponent<T>() where T:class { return cache as T; }
}
public class MapComponent_MouseDisasterPredation {
    public void Register(MouseDisasterEventGroup group,IEnumerable<Pawn> pawns) {}
    public static bool ShouldFlee(Pawn predator,Pawn victim)=>false;
}
public class MapComponent { protected Map map; public MapComponent(Map map) { this.map=map; } public virtual void MapComponentTick() {} }
public class Game {
    public GameComponent_MouseDisasterEventBehavior behavior;
    public GameComponent_MouseDisasterPawnGeneration generation=new GameComponent_MouseDisasterPawnGeneration();
    public T GetComponent<T>() where T:class { return (behavior as T) ?? (generation as T); }
}
public static class Current { public static Game Game; }
public class GameComponent { public virtual void ExposeData() {} public virtual void GameComponentTick() {} }
public class GameComponent_MouseDisasterPawnGeneration { public HashSet<int> pending=new HashSet<int>(); public bool HasPendingBehaviorGroup(int id)=>pending.Contains(id); }
public class Lord {
    public List<Pawn> ownedPawns=new List<Pawn>(); public object LordJob; public Faction faction;
    public int changedFactionLosses;
    public void RemovePawn(Pawn p) { ownedPawns.Remove(p); p.lord=null; p.mindState.duty=null; }
    public void AddPawns(IEnumerable<Pawn> pawns,bool updateDuties) { foreach(var p in pawns) { ownedPawns.Add(p); p.lord=this; } }
}
public class LordJob_AssaultColony {
    public LordJob_AssaultColony(Faction faction,bool canKidnap,bool canTimeoutOrFlee,bool canSteal) {}
}
public static class LordMaker {
    public static int assaults;
    public static void MakeNewLord(Faction faction,object job,Map map,List<Pawn> pawns) {
        assaults++; var lord=new Lord { faction=faction,LordJob=job,ownedPawns=pawns }; foreach(var p in pawns) p.lord=lord;
    }
}
public static class RCellFinder { public static bool TryFindBestExitSpot(Pawn pawn,out IntVec3 exit) { exit=new IntVec3(); return true; } }
public class Settings {
    public float reliefFoodScoreBonus=0.1f;
    public float fedWanderDays=0.5f;
    public MouseDisasterEventAttitude attitude;
    public Dictionary<string,MouseDisasterEventAttitude> eventAttitudes=new Dictionary<string,MouseDisasterEventAttitude>();
    public MouseDisasterEventAttitude GetEventAttitude(string name)=>eventAttitudes.TryGetValue(name,out var value) ? MouseDisasterEventPolicy.Normalize(value) : attitude;
    // NARRATIVE_SETTINGS_METHODS
}
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public static class MouseDisasterUtility {
    public static MentalStateDef begging=new MentalStateDef(),thieving=new MentalStateDef();
    public static bool IsInBeggarMentalState(Pawn pawn)=>pawn.MentalStateDef==begging;
    public static bool IsInThiefMentalState(Pawn pawn)=>pawn.MentalStateDef==thieving;
    public static Faction neutral=new Faction(),hostile=new Faction(),friendly=new Faction(); public static int exits;
    public static bool IsRatkin(Pawn pawn)=>true;
    public static bool IsPlayerAffiliatedRatkin(Pawn pawn)=>pawn==null || pawn.player || pawn.Faction==Faction.OfPlayer;
    public static bool IsThiefPawn(Pawn pawn)=>pawn!=null && pawn.thief;
    public static bool IsBeggarPawn(Pawn pawn)=>pawn!=null && pawn.beggar;
    public static Faction GetEventFaction(bool hostile,bool friendly)=>new Faction { kind=hostile?1:friendly?2:0 };
    public static void MakeFactionHostileToPlayer(Faction faction,bool explicitDriveAway) { faction.kind=1; }
    public static bool IsEventBehaviorFaction(Faction faction)=>true;
    public static bool ShouldPrioritizeReliefAreaFood(Pawn pawn)=>false;
    public static bool IsAbandonedDeliveryPawn(Pawn pawn)=>false;
    public static void MakeTravelAndExitLord(Map map,List<Pawn> pawns,IntVec3 exit,bool includeBabiesInExit) { exits++; }
    public static ReliefArea GetReliefArea(Map map)=>map.area;
    public static bool IsAreaFoodSourceThing(Thing t,bool harvest)=>t!=null && t.Spawned && !t.Destroyed && t.IngestibleNow;
}
// The visit controller has its own production-method harness in verify-visits.ps1.
public sealed partial class GameComponent_MouseDisasterEventBehavior {
    private Dictionary<int,int> fedDepartureTicks=new Dictionary<int,int>();
    private HashSet<Pawn> departingPawns=new HashSet<Pawn>();
    private void ExposeVisitTimers() {} private void TickVisits() {}
    private void ForgetVisitTimers(Pawn pawn) {}
    private static void AssignVisitLord(IEnumerable<Pawn> pawns) {}
}
public static class MouseDisasterFeeding {
    public static bool HasSatisfied(Pawn pawn)=>pawn?.fedOnce==true || GameComponent_MouseDisasterEventBehavior.Component?.HasCompletedFeeding(pawn)==true;
    public static bool IsSeekingSuppressed(Pawn pawn)=>HasSatisfied(pawn) || pawn?.temporarySatiety==true;
}
public static class FoodUtility {
    public static float GetNutrition(Pawn p,Thing t,ThingDef d)=>1;
    public static int wanted=1;
    public static int WillIngestStackCountOf(Pawn p,ThingDef d,float nutrition)=>wanted;
    public static bool TryFindBestFoodSourceFor(Pawn a,Pawn b,bool desperate,out Thing food,out ThingDef def,
        bool canRefillDispenser,bool canUseInventory,bool canUsePackAnimalInventory,bool allowForbidden,bool allowCorpse,
        bool allowSociallyImproper,bool allowHarvest,bool forceScanWholeMap,bool ignoreReservations,bool calculateWantedStackCount,bool allowVenerated) {
        food=null;def=null;return false;
    }
}
public class DamageDef { public bool ExternalViolenceFor(Pawn p)=>true; }
public struct DamageInfo { public Thing Instigator; public DamageDef Def; }
public static class Harness {
    private static int checks;
    private static void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks++; }
    private static GameComponent_MouseDisasterEventBehavior Reset() {
        Current.Game=new Game(); Current.Game.behavior=new GameComponent_MouseDisasterEventBehavior(Current.Game);
        Find.TickManager.TicksGame=0; LordMaker.assaults=0; MouseDisasterUtility.exits=0; Scribe.mode=LoadSaveMode.Inactive;
        return Current.Game.behavior;
    }
    private static Map NewMap() { var map=new Map(); map.cache=new MapComponent_MouseDisasterFoodTargets(map); return map; }
    private static Pawn NewPawn(Map map,int id)=>new Pawn { Map=map,thingIDNumber=id,Faction=MouseDisasterUtility.neutral };
    private static int Group(GameComponent_MouseDisasterEventBehavior c,MouseDisasterEventAttitude attitude,params Pawn[] pawns) {
        MouseDisasterMod.Settings.attitude=attitude; int id=c.CreateGroup(new IncidentDef { defName="Test" }); c.Register(id,pawns); return id;
    }
    private static int Key(bool forbidden=false)=>MapComponent_MouseDisasterFoodTargets.Key(false,true,true,false,forbidden,false,true,false,true,false,false,false,FoodPreferability.Undefined);
    public static string Run() {
        foreach(var attitude in new[]{MouseDisasterEventAttitude.Neutral,MouseDisasterEventAttitude.HostileLeaning,MouseDisasterEventAttitude.FriendlyLeaning,MouseDisasterEventAttitude.Friendly,MouseDisasterEventAttitude.Hostile}) {
            foreach(var state in new[]{MouseDisasterUtility.begging,MouseDisasterUtility.thieving,(MentalStateDef)null}) {
                var behavior=Reset(); var testMap=NewMap(); var visitor=NewPawn(testMap,2000);
                visitor.Faction=new Faction(); visitor.mindState.mentalStateHandler.state=state;
                var originalDuty=new object(); visitor.mindState.duty=originalDuty;
                var originalLord=new Lord { faction=visitor.Faction,LordJob=new object() };
                originalLord.AddPawns(new[]{visitor},false);
                int groupId=Group(behavior,attitude,visitor);
                bool hostile=attitude==MouseDisasterEventAttitude.Hostile;
                Check(originalLord.changedFactionLosses==0,"faction switch notified original lord");
                Check(hostile ? visitor.GetLord()!=originalLord : visitor.GetLord()==originalLord,"visitor lord retention");
                Check(hostile || ReferenceEquals(visitor.mindState.duty,originalDuty),"faction switch erased visitor duty");
                Check(visitor.MentalStateDef==(hostile || attitude==MouseDisasterEventAttitude.Friendly ? null : state),"visitor mental state retention");
                Check(hostile || originalLord.faction==visitor.Faction,"retained lord faction mismatch");
                behavior.Register(groupId,new[]{visitor});
                Check(hostile || originalLord.ownedPawns.Count==1,"repeated registration duplicated lord membership");
            }
        }
        foreach(var state in new[]{MouseDisasterUtility.begging,MouseDisasterUtility.thieving}) {
            var behavior=Reset(); var visitor=NewPawn(NewMap(),2001);
            visitor.Faction=new Faction(); visitor.mindState.mentalStateHandler.state=state;
            Group(behavior,MouseDisasterEventAttitude.Neutral,visitor);
            Check(visitor.GetLord()==null && visitor.MentalStateDef==state,"standalone visitor lost job-giver mental state");
        }
        {
            var behavior=Reset(); var testMap=NewMap(); var visitors=new[]{NewPawn(testMap,2002),NewPawn(testMap,2003)};
            var originalFaction=new Faction(); var originalLord=new Lord { faction=originalFaction,LordJob=new object() };
            originalLord.AddPawns(visitors,false);
            foreach(var visitor in visitors) { visitor.Faction=originalFaction; visitor.mindState.duty=new object(); }
            Group(behavior,MouseDisasterEventAttitude.Neutral,visitors);
            Check(originalLord.ownedPawns.Count==2 && visitors.All(p=>p.GetLord()==originalLord && p.mindState.duty!=null),"whole cohort lost lord or duty");
            Check(originalLord.changedFactionLosses==0,"cohort conversion triggered pawn-lost transition");
        }
        Check(MouseDisasterEventPolicy.Normalize((MouseDisasterEventAttitude)99)==MouseDisasterEventAttitude.Neutral,"invalid attitude");
        foreach(MouseDisasterEventAttitude a in Enum.GetValues(typeof(MouseDisasterEventAttitude))) {
            Check(MouseDisasterEventPolicy.Compose(true,false,a).HasFlag(MouseDisasterPawnBehavior.SeekFood),"thief food module");
            if(a==MouseDisasterEventAttitude.Friendly || a==MouseDisasterEventAttitude.FriendlyLeaning)
                Check(MouseDisasterEventPolicy.React(a,true)==MouseDisasterEventReaction.GroupFlee,"friendly expulsion");
        }
        var c=Reset(); var map=NewMap(); var a1=NewPawn(map,1); var a2=NewPawn(map,2); var b1=NewPawn(map,3);
        Group(c,MouseDisasterEventAttitude.HostileLeaning,a1,a2); Group(c,MouseDisasterEventAttitude.HostileLeaning,b1);
        Check(a1.Faction.kind==0 && b1.Faction!=a1.Faction && a1.Faction==a2.Faction,"independent event factions");
        MouseDisasterEventDamagePatch.Postfix(a1,new DamageInfo { Instigator=b1,Def=new DamageDef() },1);
        Check(a2.Faction.kind==0,"non-colony damage triggered group");
        MouseDisasterEventDamagePatch.Postfix(a1,new DamageInfo { Instigator=new Thing { Faction=Faction.OfPlayer },Def=new DamageDef() },0);
        Check(a2.Faction.kind==0,"zero damage triggered group");
        a1.Dead=true; c.NotifyDamage(a1);
        Check(a2.Faction.kind==1 && b1.Faction.kind==0,"lethal damage did not isolate group");
        int assaults=LordMaker.assaults; c.NotifyDamage(a2); Check(LordMaker.assaults==assaults,"repeated hit rebuilt assault");
        Check(c.TryGetGroup(a2,out var group) && group.hostile,"reaction state absent");
        Scribe.mode=LoadSaveMode.PostLoadInit; c.ExposeData(); Check(c.TryGetGroup(a2,out var loaded) && loaded==group,"post-load index");
        MouseDisasterMod.Settings.attitude=MouseDisasterEventAttitude.Friendly; Check(group.attitude==MouseDisasterEventAttitude.HostileLeaning,"settings rewrote existing group");
        var late=NewPawn(map,4); c.Register(group.id,new[]{late}); Check(late.Faction==a2.Faction && late.Faction.kind==1,"late batch member ignored group reaction");
        a2.player=true; c.NotifyDamage(a2); Check(GameComponent_MouseDisasterEventBehavior.Profile(a2)==MouseDisasterPawnBehavior.None,"recruited pawn controlled");
        var friendly=NewPawn(map,5); Group(c,MouseDisasterEventAttitude.Friendly,friendly); c.React(new[]{friendly},true,out _);
        Check(friendly.Faction.kind==2 && MouseDisasterUtility.exits==1,"friendly expulsion became hostile");
        Check(GameComponent_MouseDisasterEventBehavior.HasBehavior(friendly,MouseDisasterPawnBehavior.ReliefOnly),"leaving friendly lost food restriction");
        c.NotifyDamage(friendly); Check(MouseDisasterUtility.exits==1,"repeated hit rebuilt exit lord");
        var neutral=NewPawn(map,6); Group(c,MouseDisasterEventAttitude.Neutral,neutral); c.NotifyDamage(neutral);
        Check(neutral.Faction.kind==0 && GameComponent_MouseDisasterEventBehavior.HasBehavior(neutral,MouseDisasterPawnBehavior.IgnoreCombatFear),"neutral thief lost behavior");
        var countA=NewPawn(map,61); var countB=NewPawn(map,62); Group(c,MouseDisasterEventAttitude.FriendlyLeaning,countA,countB);
        c.React(new[]{countA,countB},true,out int affected); Check(affected==2,"expulsion counted groups instead of pawns");
        Job fleeing=new Job(); Check(!MouseDisasterThiefCombatFearPatch.Prefix(neutral,b1,ref fleeing) && fleeing==null,"thief combat flee not suppressed");
        Check(MouseDisasterThiefCombatFearPatch.Prefix(neutral,new Thing(),ref fleeing),"non-combat escape blocked");
        neutral.burning=true; Check(MouseDisasterThiefCombatFearPatch.Prefix(neutral,b1,ref fleeing),"burning thief escape blocked"); neutral.burning=false;
        neutral.Downed=true; Check(MouseDisasterThiefCombatFearPatch.Prefix(neutral,b1,ref fleeing),"downed thief escape blocked"); neutral.Downed=false;
        var colony=NewPawn(map,7); colony.player=true; Group(c,MouseDisasterEventAttitude.Hostile,colony); Check(!c.TryGetGroup(colony,out _),"colonist registered");
        var child=NewPawn(map,8); child.Spawned=false; var mother=NewPawn(map,9); mother.inventory.innerContainer.Add(child);
        Group(c,MouseDisasterEventAttitude.Neutral,mother); Check(c.TryGetGroup(child,out _),"held pawn not registered");
        c=Reset(); Check(!c.TryGetGroup(b1,out _),"cross-game cohort cache leaked");
        var pendingPawn=NewPawn(map,11); int pendingId=Group(c,MouseDisasterEventAttitude.HostileLeaning,pendingPawn);
        Current.Game.generation.pending.Add(pendingId); pendingPawn.Dead=true; c.GameComponentTick();
        var replacement=NewPawn(map,12); c.Register(pendingId,new[]{replacement});
        Check(c.TryGetGroup(replacement,out _),"empty active batch lost group");
        Current.Game.generation.pending.Clear(); replacement.Dead=true; c.GameComponentTick();
        var orphan=NewPawn(map,13); c.Register(pendingId,new[]{orphan});
        Check(!c.TryGetGroup(orphan,out _),"completed empty group retained");

        map=NewMap(); var pawn=NewPawn(map,10); var food=new Thing { Map=map }; map.listerThings.foods.Add(food);
        int key=Key(); map.cache.Store(pawn,key,food,food.def,true);
        Check(map.cache.TryRead(pawn,key,out var found,out _,out bool available) && available && found==food,"valid target not reused");
        FoodUtility.wanted=20; food.stackCount=5;
        Check(map.cache.TryRead(pawn,key,out _,out _,out available) && available,"partial stack should satisfy default search");
        map.cache.Store(pawn,key | 1024,food,food.def,true);
        Check(!map.cache.TryRead(pawn,key | 1024,out _,out _,out _),"explicit wanted count ignored");
        FoodUtility.wanted=1; food.stackCount=10000;
        pawn.canReserve=false; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"reservation change ignored"); pawn.canReserve=true;
        pawn.canReach=false; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"blocked path ignored"); pawn.canReach=true;
        food.stackCount=0; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"depleted stack reused"); food.stackCount=10000;
        food.forbidden=true; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"forbidden change ignored"); food.forbidden=false;
        food.Position=new IntVec3 { x=2 }; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"moved target reused"); food.Position=new IntVec3();
        food.fresh=false; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"stale food reused"); food.fresh=true;
        pawn.willEat=false; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"diet change ignored"); pawn.willEat=true;
        Check(!map.cache.TryRead(pawn,Key(true),out _,out _,out _),"request flags collided");
        map.cache.Store(pawn,key,null,null,false); Check(map.cache.TryRead(pawn,key,out _,out _,out available) && !available,"empty result not throttled");
        map.listerThings.foods.Add(new Thing { Map=map }); Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"new food did not invalidate empty result");
        map.cache.Store(pawn,key,null,null,false); Find.TickManager.TicksGame=1000; Check(!map.cache.TryRead(pawn,key,out _,out _,out _),"empty retry never expires");
        Check(map.cache.HasAnyFood(),"food presence scan"); foreach(var item in map.listerThings.foods) item.Destroyed=true;
        Check(!map.cache.HasAnyFood(),"destroyed shared presence target");
        var rows=new List<string>();
        foreach(int count in new[]{1,10,100}) {
            map=NewMap(); food=new Thing { Map=map }; map.listerThings.foods.Add(food);
            var pawns=Enumerable.Range(0,count).Select(i=>NewPawn(map,i+100)).ToArray(); int scans=0;
            for(int round=0;round<100;round++) foreach(var p in pawns) {
                if(map.cache.TryRead(p,key,out _,out _,out _)) continue;
                scans++; map.cache.Store(p,key,food,food.def,true);
            }
            Check(scans==1,"shared target lookup scaled with pawn count");
            rows.Add(count+" pawns x 100 requests: baseline "+(count*100)+", cached selections "+scans);
        }
        c=Reset(); map=NewMap(); pawn=NewPawn(map,999);
        Group(c,MouseDisasterEventAttitude.Friendly,pawn);
        Check(GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.SeekFood),"initial food state missing");
        pawn.temporarySatiety=true;
        Check(!GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.SeekFood),"temporary satiety ignored");
        Check(c.HasFoodSeekingProfile(pawn),"temporary satiety erased underlying food profile");
        pawn.temporarySatiety=false;
        Check(GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.SeekFood),"removed satiety still suppresses profile");
        c.CompleteFeeding(pawn);
        Check(!GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.SeekFood),"completed food state retained");
        Check(GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.ReliefOnly),"completion removed relief restriction");
        Scribe.mode=LoadSaveMode.PostLoadInit; c.ExposeData(); Scribe.mode=LoadSaveMode.Inactive;
        Check(!GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn,MouseDisasterPawnBehavior.SeekFood),"completion lost on profile rebuild");
        c.RecordRefeeding(pawn); c.ExposeData();
        var restored=new GameComponent_MouseDisasterEventBehavior(Current.Game);
        Check(!restored.HasCompletedFeeding(pawn),"feeding IDs leaked into fresh game");
        Scribe_Collections.reading=true; Scribe.mode=LoadSaveMode.PostLoadInit;
        restored.ExposeData(); Scribe_Collections.reading=false; Scribe.mode=LoadSaveMode.Inactive;
        Check(restored.HasCompletedFeeding(pawn),"saved feeding IDs not restored");
        Check(restored.HasAppliedRefeeding(pawn),"saved refeeding IDs not restored");
        MouseDisasterMod.Settings.attitude=MouseDisasterEventAttitude.Neutral;
        MouseDisasterMod.Settings.SetNarrativeAttitude("N004",MouseDisasterEventAttitude.Friendly);
        Check(MouseDisasterMod.Settings.GetEventAttitude("MouseDisaster_ShatteredMother")==MouseDisasterEventAttitude.Friendly,"N004 source setting not shared");
        MouseDisasterMod.Settings.SetNarrativeAttitude("N005",MouseDisasterEventAttitude.HostileLeaning);
        Check(MouseDisasterMod.Settings.GetEventAttitude("MouseDisaster_ChildExchange")==MouseDisasterEventAttitude.HostileLeaning,"N005 source setting not shared");
        MouseDisasterMod.Settings.SetNarrativeAttitude("N007",MouseDisasterEventAttitude.FriendlyLeaning);
        Check(MouseDisasterNarrativePolicy.GetAttitudeSources("N007").Count==9,"N007 source membership changed");
        Check(MouseDisasterMod.Settings.GetNarrativeAttitude("N007")==MouseDisasterEventAttitude.FriendlyLeaning,"N007 shared setting mismatch");
        MouseDisasterMod.Settings.eventAttitudes["MouseDisaster_PlagueWanderers"]=MouseDisasterEventAttitude.Hostile;
        Check(!MouseDisasterMod.Settings.GetNarrativeAttitude("N007").HasValue,"mixed source settings not detected");
        Check(MouseDisasterMod.Settings.GetEventAttitude("MouseDisaster_PlagueCaravanMuggers")==MouseDisasterEventAttitude.Neutral,"unrelated plague event modified");
        foreach(string id in new[]{"N001","N002","N003","N006","N009","N010","S01","E01"})
            Check(MouseDisasterNarrativePolicy.GetAttitudeSources(id).Count==0,"non-visitor narrative exposes ineffective attitude");
        foreach(MouseDisasterEventAttitude attitude in Enum.GetValues(typeof(MouseDisasterEventAttitude))) {
            c=Reset(); map=NewMap(); pawn=NewPawn(map,1001);
            var duty=new object(); pawn.mindState.duty=duty;
            MouseDisasterMod.Settings.SetNarrativeAttitude("N008",attitude);
            int id=c.CreateGroup("N008"); c.Register(id,new[]{pawn});
            Check(c.TryGetGroup(pawn,out var envoyGroup) && envoyGroup.attitude==attitude,"envoy attitude not applied");
            if(attitude!=MouseDisasterEventAttitude.Hostile) Check(ReferenceEquals(pawn.mindState.duty,duty),"nonhostile envoy lost meeting duty");
            MouseDisasterMod.Settings.SetNarrativeAttitude("N008",MouseDisasterEventAttitude.Neutral);
            Check(envoyGroup.attitude==attitude,"settings changed existing envoy snapshot");
            c.React(new[]{pawn},true,out _);
            Check(envoyGroup.hostile || envoyGroup.leaving,"envoy expulsion reaction missing");
        }
        return "PASS: "+checks+" behavior/cache assertions.\n"+string.Join("\n",rows)+"\nCounts use controlled reservations, diet and reachability; not a Unity TPS benchmark.";
    }
}
}
'@
$settingsSource = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$stub = $stub.Replace('// NARRATIVE_SETTINGS_METHODS', ((Get-CSharpMethod $settingsSource 'GetNarrativeAttitude') + (Get-CSharpMethod $settingsSource 'SetNarrativeAttitude')))
$production = foreach ($file in 'MouseDisasterEventPolicy.cs','MouseDisasterNarrativePolicy.cs','MapComponent_MouseDisasterFoodTargets.cs','GameComponent_MouseDisasterEventBehavior.cs') {
    $code = Get-Content (Join-Path $root "1.6/Source/$file") -Raw
    [regex]::Replace($code, '(?m)^using [^;]+;\r?\n', '').Replace('namespace MouseDisaster','namespace EventBehaviorTests')
}
$visitorSyntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $root '1.6/Source/GameComponent_MouseDisasterVisitorControl.cs'))).GetRoot()
$recordTypes = $visitorSyntax.DescendantNodes() | Where-Object {
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax] -and $_.Identifier.ValueText -eq 'MouseDisasterVisitorRecord') -or
    ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax] -and $_.Identifier.ValueText -eq 'MouseDisasterVisitorStatus')
} | ForEach-Object ToFullString
$recordMethods = $visitorSyntax.DescendantNodes() | Where-Object {
    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $_.Parent.Identifier.ValueText -eq 'GameComponent_MouseDisasterVisitorControl' -and
    $_.Identifier.ValueText -in @('RegisterVisitors','GetRecord','ForgetRecord','RemoveRecordAt','RebuildRecordIndex','CanTrackPawn')
} | ForEach-Object ToFullString
$registry = @'
public sealed class RegistryHarness {
    private List<MouseDisasterVisitorRecord> visitorRecords=new List<MouseDisasterVisitorRecord>();
    private readonly Dictionary<Pawn,MouseDisasterVisitorRecord> recordsByPawn=new Dictionary<Pawn,MouseDisasterVisitorRecord>();
    public static int Run() {
        var registry=new RegistryHarness(); var map=new Map(); int checks=0;
        var pawns=Enumerable.Range(0,100).Select(i=>new Pawn { Map=map,thingIDNumber=i }).ToArray();
        foreach(var pawn in pawns) registry.RegisterVisitors(new[]{pawn});
        if(registry.visitorRecords.Count!=100) throw new Exception("registration lost pawns"); checks++;
        registry.RegisterVisitors(pawns); if(registry.visitorRecords.Count!=100) throw new Exception("duplicate records"); checks++;
        foreach(var index in new[]{0,50,99}) registry.ForgetRecord(registry.GetRecord(pawns[index]));
        if(registry.visitorRecords.Count!=97) throw new Exception("removal lost records"); checks++;
        registry.recordsByPawn.Clear(); registry.RebuildRecordIndex();
        for(int i=0;i<registry.visitorRecords.Count;i++) {
            var record=registry.visitorRecords[i];
            if(record.slot!=i || registry.GetRecord(record.pawn)!=record) throw new Exception("rebuilt index mismatch"); checks++;
        }
        foreach(var record in registry.visitorRecords.ToList()) { registry.ForgetRecord(record); registry.ForgetRecord(record); }
        if(registry.visitorRecords.Count!=0 || registry.recordsByPawn.Count!=0) throw new Exception("index leaked after removals"); checks++;
        return checks;
    }
'@
$fearSyntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $root '1.6/Source/ThiefPatches.cs'))).GetRoot()
$fear = $fearSyntax.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax] -and $_.Identifier.ValueText -eq 'MouseDisasterThiefCombatFearPatch' } | ForEach-Object ToFullString
$extra = 'namespace EventBehaviorTests {' + ($recordTypes -join "`n") + ($fear -join "`n") + $registry + ($recordMethods -join "`n") + '} }'
Add-Type -TypeDefinition ($stub + ($production -join "`n") + $extra)
[EventBehaviorTests.Harness]::Run()
"PASS: $([EventBehaviorTests.RegistryHarness]::Run()) production visitor-index assertions."
