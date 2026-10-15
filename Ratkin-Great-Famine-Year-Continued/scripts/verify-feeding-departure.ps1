$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$beggar = Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/JobGiver_MouseDisasterBeggar.cs') -Raw) 'TryGiveJob'
$thief = Get-CSharpMethod (Get-Content (Join-Path $root '1.6/Source/JobGiver_MouseDisasterThief.cs') -Raw) 'TryGiveJob'
$harness = @'
using System;
public class Job { public string name; public Job(string n){name=n;} }
public abstract class ThinkNode_JobGiver { protected abstract Job TryGiveJob(Pawn pawn); public Job Run(Pawn pawn)=>TryGiveJob(pawn); }
public enum DevelopmentalStage { Adult, Baby }
public class Food { public float CurLevelPercentage=0.9f; }
public class Needs { public Food food=new Food(); }
public class MapPawns { public int FreeColonistsSpawnedCount=1; }
public class Map { public MapPawns mapPawns=new MapPawns(); }
public class Faction { public static Faction OfPlayer=new Faction(); public bool HostileTo(Faction other)=>false; }
public class Pawn {
 public bool InMentalState,Downed,InAggroMentalState,stay,success,completed,temporary,relief;
 public Map Map=new Map(); public Faction Faction; public Needs needs=new Needs(); public DevelopmentalStage DevelopmentalStage;
}
public static class Current { public static Game Game=new Game(); }
public class Game { public T GetComponent<T>() where T:new()=>new T(); }
public class GameComponent_MouseDisasterNarrative { public bool IsWaitingEnvoy(Pawn p)=>false; }
public enum MouseDisasterPawnBehavior { ReliefOnly }
public class GameComponent_MouseDisasterEventBehavior {
 public static GameComponent_MouseDisasterEventBehavior Component;
 public bool TryVisitJob(Pawn p,out Job job) { job=null; return false; }
 public static bool HasBehavior(Pawn p,MouseDisasterPawnBehavior b)=>p.relief;
}
public class Settings { public bool leaveAfterFed=true; }
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public static class MouseDisasterFeeding {
 public static bool HasSatisfied(Pawn p)=>p.completed;
 public static bool ShouldLeaveAfterFed(Pawn p)=>MouseDisasterMod.Settings.leaveAfterFed&&p.completed;
 public static bool HasTemporarySatiety(Pawn p)=>p.temporary;
}
public static class MouseDisasterDefOf { public static string MouseDisaster_BegForFood="beg"; }
public static class JobMaker { public static Job MakeJob(string name,Pawn target)=>new Job(name); }
public static class MouseDisasterUtility {
 public static bool IsAbandonedDeliveryPawn(Pawn p)=>false;
 public static bool IsPendingAbandonedChild(Pawn p)=>false;
 public static bool IsPlayerAffiliatedRatkin(Pawn p)=>false;
 public static bool IsBeggarPawn(Pawn p)=>true;
 public static bool IsThiefPawn(Pawn p)=>true;
 public static bool IsInBeggarMentalState(Pawn p)=>true;
 public static bool IsInThiefMentalState(Pawn p)=>true;
public static bool MustStayForAirDropError(Pawn p)=>p.stay;
public static bool CanBegAgain(Pawn p)=>true;
public static bool IsSiegeBeggar(Pawn p)=>false;
public static bool HasSiegeBeggarLeaveCondition(Pawn p)=>false;
public static bool HasBeggarSucceeded(Pawn p)=>p.success;
public static bool IsThiefChildPawn(Pawn p)=>false;
public static int GetBegAttempts(Pawn p)=>0;
public static void ClearBeggarTargetHistory(Pawn p){}
public static Job ExitMapJob(Pawn p)=>new Job("exit");
public static Job TryCreateReliefFoodJob(Pawn p,bool allowInventorySearch)=>p.relief?new Job("relief"):null;
public static Job TryCreateImproperFoodJob(Pawn p,bool siege)=>null;
public static bool AllowsEventPawnOutsideReliefFood(Pawn p)=>false;
 public static bool IsHostileTo(Faction faction,Faction other)=>faction!=null && other!=null && faction.HostileTo(other);
}
public partial class Beggar : ThinkNode_JobGiver {
 private static Pawn FindClosestReachableColonist(Pawn p,bool preferUnbegged)=>null;
 private static Job TryCreateRecoveryJob(Pawn p)=>null;
}
public partial class Thief : ThinkNode_JobGiver {
 private const float LeaveFoodLevel=0.82f;
 private Job TryCreateExtraFoodJob(Pawn p)=>null;
 private Job TryCreateFoodJob(Pawn p)=>null;
 private Job TryCreateRecoveryOrAssaultJob(Pawn p)=>null;
}
public static class DepartureHarness {
 static int checks; static void Check(bool b,string message){if(!b)throw new Exception(message);checks++;}
 public static int Run(){
  foreach(var ai in new ThinkNode_JobGiver[]{new Beggar(),new Thief()}){
   foreach(bool completed in new[]{false,true}){
    var p=new Pawn{completed=completed};MouseDisasterMod.Settings.leaveAfterFed=false;
    Check(ai.Run(p)==null,"disabled full visitor left: "+ai.GetType().Name);
    MouseDisasterMod.Settings.leaveAfterFed=true;Check(ai.Run(p)?.name=="exit","enabled full visitor stayed");
    p.stay=true;Check(ai.Run(p)==null,"feeding bypassed story wait");
   p.stay=false;p.temporary=true;MouseDisasterMod.Settings.leaveAfterFed=false;Check(ai.Run(p)==null,"temporary satiety ignored");
   }
   var fedRelief=new Pawn{completed=true,relief=true};MouseDisasterMod.Settings.leaveAfterFed=false;
   Check(ai.Run(fedRelief)==null,"disabled departure resumed relief feeding: "+ai.GetType().Name);
   var hungry=new Pawn();hungry.needs.food.CurLevelPercentage=0.2f;
   Check(ai.Run(hungry)?.name=="exit","disabled feeding departure blocked unrelated no-food exit");
  }
  var beggar=new Beggar();var partial=new Pawn();partial.needs.food.CurLevelPercentage=0.76f;
  Check(beggar.Run(partial)==null,"legacy 0.75 threshold ignored toggle");
  partial.success=true;Check(beggar.Run(partial)?.name=="exit","begging success departure changed");
  var waiting=new Pawn{stay=true,relief=true};Check(beggar.Run(waiting)?.name=="relief","story wait blocked existing relief job");
  var relief=new Pawn{relief=true};Check(new Thief().Run(relief)?.name=="relief","relief-only behavior changed");
  return checks;
 }
}
'@
# Execute the complete production decision methods; replace engine and downstream job factories only.
Add-Type -TypeDefinition ($harness + "`npublic partial class Beggar {" + $beggar + "}`npublic partial class Thief {" + $thief + '}')
"PASS: $([DepartureHarness]::Run()) departure assertions against production TryGiveJob methods. Engine doubles, not in-game evidence."
