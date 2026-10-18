$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root '1.6/Source/MouseDisasterIncidentScheduling.cs') -Raw
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace HarmonyLib {
public static class AccessTools {
 public delegate ref F FieldRef<T,F>(T instance);
 public static FieldRef<T,int> FieldRefAccess<T,F>(string name) {
  if(typeof(T)!=typeof(StoryState)||name!="lastThreatBigTick")throw new Exception("Unexpected field contract");
  return (T state)=>ref ((StoryState)(object)state).lastThreatBigTick;
 }
}
[AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute {
 public HarmonyPatch(Type t,string n) {} public HarmonyPatch(Type t,string n,Type[] a) {}
}}
namespace Verse {
 public class MapTemperature { public float OutdoorTemp=20; }
 public class Map : IIncidentTarget { public bool IsPlayerHome=true; public MapTemperature mapTemperature=new MapTemperature(); public StoryState StoryState {get;}=new StoryState(); }
 public static class ModsConfig { public static bool AnomalyActive; }
 public class TickManager { public int TicksGame=900000; }
 public class Anomaly { public int metalHellClosedTick; }
 public static class Find { public static TickManager TickManager=new TickManager(); public static Anomaly Anomaly=new Anomaly(); }
 public static class DefDatabase<T> where T:class { public static Dictionary<string,T> defs=new Dictionary<string,T>(); public static T GetNamedSilentFail(string n)=>defs.TryGetValue(n,out var d)?d:null; }
 public static class Rand { public static List<float> rolls=new List<float>(); public static bool MTBEventOccurs(float d,float t,float i){rolls.Add(d);return true;} }
 public static class Extensions { public static T RandomElementByWeightWithFallback<T>(this IEnumerable<T> xs,Func<T,float> weight,T fallback)=>xs.FirstOrDefault(x=>weight(x)>0) ?? fallback; }
}
namespace RimWorld {
 public interface IIncidentTarget { StoryState StoryState {get;} }
 public class StoryState {
  public int lastThreatBigTick=-1,threatCount;
  public List<IncidentDef> fired=new List<IncidentDef>();
  public void Notify_IncidentFired(FiringIncident f){fired.Add(f.def);if(f.def.category==IncidentCategoryDefOf.ThreatBig){lastThreatBigTick=Find.TickManager.TicksGame;threatCount++;}}
 }
 public class IncidentCategoryDef {}
 public static class IncidentCategoryDefOf { public static IncidentCategoryDef ThreatBig=new IncidentCategoryDef(); }
 public class IncidentParms { public IIncidentTarget target; public float points=100; public object quest; public bool forced; }
 public class IncidentDef { public string defName; public bool positive,allowed=true; public IncidentCategoryDef category=new IncidentCategoryDef(); public Worker Worker=new Worker(); public bool TargetAllowed(IIncidentTarget t)=>allowed; }
 public class Worker { public bool can=true,success=true; public bool CanFireNow(IncidentParms p)=>can; }
 public static class IncidentDefOf { public static IncidentDef RaidEnemy=new IncidentDef {defName="RaidEnemy",category=IncidentCategoryDefOf.ThreatBig}; }
 public class StorytellerCompProperties {}
 public class StorytellerComp { public StorytellerCompProperties props; public IncidentParms GenerateParms(IncidentCategoryDef c,IIncidentTarget t)=>new IncidentParms {target=t}; protected float IncidentChanceFinal(IncidentDef d,IIncidentTarget t)=>1; }
 public class FiringIncident { public IncidentDef def; public IncidentParms parms; public object sourceQuestPart; public FiringIncident(IncidentDef d,StorytellerComp s,IncidentParms p){def=d;parms=p;} }
 public class Storyteller { public const int CheckInterval=1000; public List<IIncidentTarget> AllIncidentTargets=new List<IIncidentTarget>(); public bool TryFire(FiringIncident f,bool queued=false){if(!f.def.Worker.success)return false;f.parms.target.StoryState.Notify_IncidentFired(f);return true;} public IEnumerable<FiringIncident> MakeIncidentsForInterval()=>null; }
 public static class GenDate { public const int TicksPerDay=60000; public static float DaysPassedSinceSettleFloat=5; }
}
namespace MouseDisaster {
 public class Entry { public string DefName; }
 public static class MouseDisasterIncidentCatalog { public static List<Entry> AllEntries=new List<Entry>(); public static bool IsKnownIncident(string n)=>AllEntries.Any(e=>e.DefName==n); }
 public class Settings { public float positiveIncidentDays=3,negativeIncidentDays=7,mouseDisasterMinimumEnvironmentTemperature=-100,mouseDisasterMaximumEnvironmentTemperature=100; public HashSet<string> disabled=new HashSet<string>(),replace=new HashSet<string>(); public bool IsIncidentEnabled(string n)=>!disabled.Contains(n); public bool IsPositiveIncident(IncidentDef d)=>d.positive; public bool ReplacesRaid(string n)=>replace.Contains(n); public bool IsMouseDisasterEnvironmentTemperatureAllowed(float t)=>true; }
 public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
 public static class MouseDisasterTrace { public static void Log(string message){} public static string DescribeMap(Map map)=>"map"; }
 public static class MouseDisasterIncidentTargetPolicy { public static bool ShouldBlockEnvironmentTemperature(string name,IIncidentTarget target)=>false; }
 public static class MouseDisasterRuntime { public static bool AllowsNewContent=true; }
 public static class SchedulingHarness {
  static int checks; static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
  public static int Run(){
   var good=new IncidentDef{defName="Good",positive=true}; var bad=new IncidentDef{defName="Bad"};
   foreach(var d in new[]{good,bad}){MouseDisasterIncidentCatalog.AllEntries.Add(new Entry{DefName=d.defName});DefDatabase<IncidentDef>.defs[d.defName]=d;}
   var map=new Map();var st=new Storyteller();st.AllIncidentTargets.Add(map);var pool=new MouseDisasterIncidentPool();var s=MouseDisasterMod.Settings;
   Check(pool.Select(map,true).def==good,"positive pool mixed");Check(pool.Select(map,false).def==bad,"negative pool mixed");
   s.disabled.Add("Good");Check(pool.Select(map,true)==null,"disabled event selected");s.disabled.Clear();
   good.allowed=false;Check(pool.Select(map,true)==null,"target filter ignored");good.allowed=true;
   good.Worker.can=false;Check(pool.Select(map,true)==null,"eligibility ignored");good.Worker.can=true;
   Check(pool.Select(map,null)==null,"replacement enabled by default");s.replace.Add("Good");
   Check(pool.Select(map,null,new IncidentParms{points=550}).parms.points==550,"raid points lost");
   var raid=new FiringIncident(IncidentDefOf.RaidEnemy,null,new IncidentParms{target=map});bool result=false;
   Check(!MouseDisasterRaidReplacementPatch.Prefix(st,raid,false,ref result)&&result,"raid not replaced");
   Check(map.StoryState.lastThreatBigTick==Find.TickManager.TicksGame,"replacement did not consume threat deadline");
   Check(map.StoryState.threatCount==0&&map.StoryState.fired.SequenceEqual(new[]{good}),"replacement fabricated a raid or threat statistic");
   Find.TickManager.TicksGame++;
   good.Worker.success=false;Check(MouseDisasterRaidReplacementPatch.Prefix(st,raid,false,ref result),"failed replacement lost original raid");good.Worker.success=true;
   Check(map.StoryState.lastThreatBigTick==Find.TickManager.TicksGame-1&&map.StoryState.fired.Count==1,"failure changed threat bookkeeping");
   var category=good.category;good.category=IncidentCategoryDefOf.ThreatBig;
   Check(!MouseDisasterRaidReplacementPatch.Prefix(st,raid,false,ref result)&&map.StoryState.threatCount==1&&map.StoryState.lastThreatBigTick==Find.TickManager.TicksGame,"big-threat replacement bookkeeping duplicated or missing");good.category=category;
   Check(MouseDisasterRaidReplacementPatch.Prefix(st,raid,true,ref result),"queued raid replaced");
   raid.parms.forced=true;Check(MouseDisasterRaidReplacementPatch.Prefix(st,raid,false,ref result),"forced raid replaced");raid.parms.forced=false;
   raid.parms.quest=new object();Check(MouseDisasterRaidReplacementPatch.Prefix(st,raid,false,ref result),"quest raid replaced");raid.parms.quest=null;
   IEnumerable<IncidentDef> defs=new[]{good,IncidentDefOf.RaidEnemy};MouseDisasterSeparateVanillaPoolPatch.Postfix(ref defs);Check(defs.Single()==IncidentDefOf.RaidEnemy,"vanilla pool contamination");
   IEnumerable<FiringIncident> events=new[]{raid,new FiringIncident(good,null,new IncidentParms{target=map})};
   MouseDisasterIndependentPoolsPatch.Postfix(st,ref events);var generated=events.ToList();
   Check(generated.Count==3,"pool generation duplicated events");Check(Rand.rolls.SequenceEqual(new[]{3f,7f}),"pool frequencies coupled");
   s.positiveIncidentDays=0;Rand.rolls.Clear();events=Enumerable.Empty<FiringIncident>();MouseDisasterIndependentPoolsPatch.Postfix(st,ref events);
   Check(events.Single().def==bad&&Rand.rolls.SequenceEqual(new[]{7f}),"zero does not disable one pool");
   s.negativeIncidentDays=0;events=Enumerable.Empty<FiringIncident>();MouseDisasterIndependentPoolsPatch.Postfix(st,ref events);Check(!events.Any(),"zero does not disable both pools");
   bad.category=IncidentCategoryDefOf.ThreatBig;ModsConfig.AnomalyActive=true;
   Find.Anomaly.metalHellClosedTick=Find.TickManager.TicksGame-299999;
   Check(pool.Select(map,false)==null,"big threat selected during Anomaly grace period");
   Check(pool.Select(map,true).def==good,"Anomaly grace period blocked positive non-threat");
   s.replace.Clear();s.replace.Add("Bad");Check(pool.Select(map,null,raid.parms)==null,"replacement bypassed Anomaly grace period");
   s.negativeIncidentDays=7;events=Enumerable.Empty<FiringIncident>();MouseDisasterIndependentPoolsPatch.Postfix(st,ref events);Check(!events.Any(),"independent pool emitted protected threat");
   Find.TickManager.TicksGame++;Check(pool.Select(map,false).def==bad,"grace period failed to expire at 300000 ticks");
   Find.TickManager.TicksGame--;ModsConfig.AnomalyActive=false;Check(pool.Select(map,false).def==bad,"inactive DLC blocked threat");
   MouseDisasterRuntime.AllowsNewContent=false;Check(pool.Select(map,null)==null,"global switch ignored");
   return checks;
  }
 }
}
'@
# Keep the real implementation under test, substituting only engine contracts.
Add-Type -TypeDefinition ($source + $stub.Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Linq;','').Replace('using RimWorld;','').Replace('using Verse;',''))
$checks = [MouseDisaster.SchedulingHarness]::Run()
[xml]$recipe = Get-Content (Join-Path $root 'Defs/RecipeDefs/Recipes_MouseDisaster.xml') -Raw
if ($recipe.Defs.RecipeDef.ingredients.li.count -ne '1' -or $recipe.Defs.RecipeDef.products.MouseDisaster_GuanyinTu -ne '3' -or
    $recipe.Defs.RecipeDef.fixedIngredientFilter.categories.li -ne 'StoneChunks' -or $recipe.Defs.RecipeDef.recipeUsers.li -ne 'TableStonecutter') { throw 'Recipe contract mismatch' }
Write-Host "PASS: $checks scheduling assertions and stonecutting recipe contract. Engine doubles, not in-game evidence."
