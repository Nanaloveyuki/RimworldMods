$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$code = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterPawnGeneration.cs') -Raw
# Exercise the production gathering and finalization methods with an engine double.
$start = $code.IndexOf('        private static void EnsureTraderGatheringLord(')
$end = $code.IndexOf('        private static void FinalizeBatch(', $start)
$helpers = $code.Substring($start, $end - $start)
$start = $code.IndexOf('        private static bool FinalizeTraderCaravan(')
$end = $code.IndexOf('        private static void SendBatchLetter(', $start)
$finalization = $code.Substring($start, $end - $start)
$start = $code.IndexOf('        private static List<Pawn> ActivePawns(')
$active = $code.Substring($start, $code.LastIndexOf('    }') - $start)
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace TraderGatheringTests {
public struct IntVec3 {
    public bool InBounds(Map map) => true;
    public bool Standable(Map map) => true;
}
public class Faction { }
public class Map { public IntVec3 Center; public LordManager lordManager = new LordManager(); }
public class LordManager { public List<Lord> lords = new List<Lord>(); }
public class Jobs { public int stops; public void StopAll() { stops++; } }
public class Pawn {
    public bool Dead, Destroyed, Spawned=true, player, caravan;
    public string ThingID="pawn";
    public Map Map, MapHeld;
    public object ParentHolder;
    public Faction Faction=new Faction();
    public IntVec3 Position;
    public Jobs jobs=new Jobs();
    public MindState mindState=new MindState();
    public Lord lord;
    public Lord GetLord() => lord;
    public bool IsCaravanMember() => caravan;
}
public class MindState { public MentalStateHandler mentalStateHandler=new MentalStateHandler(); }
public class MentalStateHandler { public void Reset() { } }
public class LordJob_DefendPoint {
    public LordJob_DefendPoint(IntVec3 cell, float wanderRadius) { }
}
public class LordJob_MouseDisasterTrade : LordJob_TradeWithColony {
    public LordJob_MouseDisasterTrade(Faction faction,IntVec3 cell) : base(faction,cell) { }
}
public class LordJob_TradeWithColony {
    public LordJob_TradeWithColony(Faction faction, IntVec3 cell) { }
}
public class Lord {
    public Faction faction;
    public object job;
    public object LordJob => job;
    public List<Pawn> ownedPawns=new List<Pawn>();
    public void AddPawn(Pawn pawn) { ownedPawns.Add(pawn); pawn.lord=this; }
    public void RemovePawn(Pawn pawn) { ownedPawns.Remove(pawn); if (pawn.lord==this) pawn.lord=null; }
}
public static class LordMaker {
    public static Lord MakeNewLord(Faction faction, object job, Map map, IEnumerable<Pawn> pawns=null) {
        var lord=new Lord { faction=faction, job=job }; map.lordManager.lords.Add(lord);
        if (pawns!=null) foreach (var pawn in pawns) {
            if (pawn.lord!=null) throw new Exception("Pawn still belongs to gathering lord");
            lord.AddPawn(pawn);
        }
        return lord;
    }
}
public class IncidentDef { public string letterLabel, letterText, letterDef; }
public enum MouseDisasterPawnBatchKind { TraderCaravan, GreatFamine }
public class MouseDisasterPawnBatch {
    public MouseDisasterPawnBatchKind kind;
    public Map map=new Map(); public Faction faction=new Faction(); public IntVec3 entryCell;
    public List<Pawn> pawns=new List<Pawn>(); public Pawn traderPawn, escortPawn;
    public Lord gatheringLord; public int generatedSlots, remainingCount=1;
    public IncidentDef incidentDef=new IncidentDef(); public object parms;
    public bool Complete => remainingCount <= 0;
}
public static class Log { public static string warning; public static void Warning(string s) { warning=s; } public static void Error(string s) { warning=s; } public static void Message(string s) { warning=s; } }
public static class CellFinder { public static IntVec3 RandomClosewalkCellNear(IntVec3 cell, Map map, int radius) => cell; }
public static class GenSpawn { public static void Spawn(Pawn pawn, IntVec3 cell, Map map) { pawn.Spawned=true; pawn.Map=map; pawn.MapHeld=map; } }
public static class MouseDisasterUtility {
    public static int exits;
    public static bool IsPlayerAffiliatedRatkin(Pawn pawn) => pawn.player;
    public static void MarkChildExchangeMoodChildren(List<Pawn> pawns) { }
    public static void LinkIncidentParentToChildren(Pawn pawn, List<Pawn> children) { }
    public static void TryStartLeadYourPetRelatedAdultLeashes(List<Pawn> pawns) { }
    public static void TryAssignLeadYourPetTravelMouseEggs(Lord lord) { }
    public static void EnsureMouseDisasterFactionNeutralOnMap(Map map, Faction faction) { }
    public static void EnsureTradeLeader(Pawn pawn, object traderKind) { }
    public static object ResolveSlaveTraderKind() => null;
    public static void MakeTravelAndExitLord(Map map, List<Pawn> pawns, IntVec3 cell) {
        if (pawns.Any(p=>p.lord!=null)) throw new Exception("Gathering lord retained on failure");
        exits++;
    }
}
public static class MouseDisasterGeneRestorePolicy {
    public static int ResolveTraderCaravanChildrenToLinkCount(int count) => count;
}
public static class IncidentWorker_RatkinTraderCaravan { public static void FillTraderInventory(Pawn pawn) { } }
public static class RCellFinder {
    public static bool TryFindRandomSpotJustOutsideColony(IntVec3 cell, Map map, Pawn pawn, out IntVec3 result) {
        result=cell; return true;
    }
}
public static class MouseDisasterVisitorUtility {
    public static void RegisterVisitors(List<Pawn> pawns) { }
    public static bool SendVisitorChoiceLetter(IncidentDef def, object parms, Map map, List<Pawn> pawns) => true;
}
public static class IncidentWorker {
    public static void SendIncidentLetter(string label,string text,string def,object parms,List<Pawn> pawns,IncidentDef incident) { }
}
public static class Extensions { public static IEnumerable<T> InRandomOrder<T>(this IEnumerable<T> list) => list; }
public static class Harness {
    static int checks;
    static void Check(bool value,string message) { if (!value) throw new Exception(message); checks++; }
    static Pawn Add(MouseDisasterPawnBatch b) {
        var p=new Pawn { Map=b.map, MapHeld=b.map, Faction=b.faction }; b.pawns.Add(p); return p;
    }
    static Pawn AddUnspawned(MouseDisasterPawnBatch b) {
        var p=new Pawn { Spawned=false, Map=null, MapHeld=null, Faction=b.faction }; b.pawns.Add(p); return p;
    }
    public static int Run() {
        var b=new MouseDisasterPawnBatch(); b.traderPawn=Add(b);
        EnsureTraderGatheringLord(b);
        var gathering=b.gatheringLord;
        Check(gathering!=null && gathering.job is LordJob_DefendPoint,"no initial gathering duty");
        Check(b.traderPawn.GetLord()==gathering && b.traderPawn.jobs.stops==1,"old exit job not cleared");
        EnsureTraderGatheringLord(b);
        Check(gathering.ownedPawns.Count==1 && b.traderPawn.jobs.stops==1,"repeated tick resets jobs");
        var scoped=new MouseDisasterPawnBatch(); var scopedPawn=Add(scoped); EnsureTraderGatheringLord(scoped);
        var scopedLord=scoped.gatheringLord; var unrelated=new Pawn { Map=scoped.map, MapHeld=scoped.map };
        scopedLord.AddPawn(unrelated); ReleaseTraderGatheringLord(scoped);
        Check(scopedPawn.GetLord()==null && unrelated.GetLord()==scopedLord && scopedLord.ownedPawns.Count==1,"release removed an unrelated lord member");
        scopedLord.RemovePawn(unrelated);
        b.escortPawn=Add(b); Add(b); EnsureTraderGatheringLord(b);
        Check(gathering.ownedPawns.Count==3,"later members not gathered");
        b.remainingCount=0;
        Check(FinalizeTraderCaravan(b,ActivePawns(b)),"complete caravan rejected");
        Check(b.gatheringLord==null && gathering.ownedPawns.Count==0,"gathering not released");
        Check(b.pawns.All(p=>p.GetLord().job is LordJob_TradeWithColony),"normal trade lord not installed");
        var trade=b.traderPawn.GetLord(); EnsureTraderGatheringLord(b);
        Check(b.pawns.All(p=>p.GetLord()==trade),"existing trade duty overwritten");

        var oldSave=new MouseDisasterPawnBatch(); Add(oldSave);
        var savedLord=LordMaker.MakeNewLord(oldSave.faction,new LordJob_DefendPoint(oldSave.entryCell,3f),oldSave.map);
        savedLord.AddPawn(oldSave.pawns[0]); EnsureTraderGatheringLord(oldSave);
        Check(oldSave.gatheringLord==savedLord,"saved gathering lord not recovered");
        var noLordSave=new MouseDisasterPawnBatch(); Add(noLordSave); EnsureTraderGatheringLord(noLordSave);
        Check(noLordSave.pawns[0].GetLord()!=null,"old batch without saved lord not recovered");
        var captured=new MouseDisasterPawnBatch(); var player=Add(captured); player.player=true;
        EnsureTraderGatheringLord(captured);
        Check(player.GetLord()==null && captured.gatheringLord==null,"player pawn commandeered");
        Check(ActivePawns(captured).Count==0,"player pawn still included in final caravan");
        captured.kind=MouseDisasterPawnBatchKind.GreatFamine;
        Check(ActivePawns(captured).Count==1,"unrelated batch behavior changed");
        captured.kind=MouseDisasterPawnBatchKind.TraderCaravan;
        var other=Add(captured); var otherLord=new Lord(); otherLord.AddPawn(other);
        EnsureTraderGatheringLord(captured);
        Check(other.GetLord()==captured.gatheringLord && other.GetLord()!=otherLord,"batch member was not reclaimed");

        var recovered=new MouseDisasterPawnBatch();
        recovered.traderPawn=AddUnspawned(recovered);
        recovered.escortPawn=AddUnspawned(recovered);
        Add(recovered);
        Check(FinalizeTraderCaravan(recovered,ActivePawns(recovered)),"unspawned caravan members were not recovered");
        Check(recovered.traderPawn.Spawned && recovered.escortPawn.Spawned,"recovered adults did not spawn");
        Check(recovered.pawns.All(p=>p.GetLord().job is LordJob_TradeWithColony),"recovered caravan did not get one trade lord");

        var failed=new MouseDisasterPawnBatch(); failed.traderPawn=Add(failed); Add(failed);
        EnsureTraderGatheringLord(failed);
        Check(!FinalizeTraderCaravan(failed,ActivePawns(failed)),"missing escort accepted");
        Check(Log.warning.Contains("escort=null") && Log.warning.Contains("activeChildren=1"),"missing-role diagnostic absent");
        SendIncompleteTraderCaravanAway(failed,ActivePawns(failed));
        Check(MouseDisasterUtility.exits==1,"incomplete caravan cannot leave");
        failed.escortPawn=Add(failed); failed.traderPawn.Spawned=false;
        Check(!FinalizeTraderCaravan(failed,ActivePawns(failed)),"despawned trader accepted");
        Check(Log.warning.Contains("spawned=False"),"despawned state not logged");
        failed.traderPawn.Spawned=true; failed.escortPawn.Dead=true;
        Check(!FinalizeTraderCaravan(failed,ActivePawns(failed)),"dead escort accepted");
        failed.escortPawn.Dead=false; failed.pawns[1].Spawned=false;
        Check(!FinalizeTraderCaravan(failed,ActivePawns(failed)),"all held children accepted as spawned");
        Check(Log.warning.Contains("; children=") && Log.warning.Contains("heldMap="),"held child diagnostic missing");

        var removed=new MouseDisasterPawnBatch(); var first=Add(removed); EnsureTraderGatheringLord(removed);
        removed.gatheringLord.RemovePawn(first); first.Spawned=false;
        var stale=removed.gatheringLord; removed.map.lordManager.lords.Remove(stale);
        var next=Add(removed); EnsureTraderGatheringLord(removed);
        Check(next.GetLord()!=stale && removed.map.lordManager.lords.Contains(next.GetLord()),"removed lord reused");
        ReleaseTraderGatheringLord(removed); ReleaseTraderGatheringLord(removed);
        Check(removed.gatheringLord==null && next.GetLord()==null,"release not idempotent");
        return checks;
    }
'@
Add-Type -TypeDefinition ($stub + $helpers + $finalization + $active + "`n} }")
$checks = [TraderGatheringTests.Harness]::Run()
foreach ($signature in 'private static void FinalizeBatch', 'private static void TruncateBatch') {
    $offset = $code.IndexOf($signature)
    if ($code.Substring($offset, 190) -notmatch 'ReleaseTraderGatheringLord\(batch\)') {
        throw "Missing unconditional cleanup: $signature"
    }
}
if ($code -notmatch 'Scribe_References.Look\(ref gatheringLord, "gatheringLord"\)') {
    throw 'Gathering lord not saved'
}
if ($code -match 'Scribe.mode == LoadSaveMode.Saving[\s\S]{0,180}batches.Remove') {
    throw 'Save path mutates pending batches'
}
$tradeCode = Get-Content (Join-Path $root '1.6/Source/TradePatches.cs') -Raw
if (!$tradeCode.Contains('[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap), new[] { typeof(bool), typeof(Rot4) })]') -or
    !$tradeCode.Contains('child.ExitMap(false, exitDir)') -or
    !$tradeCode.Contains('LordJob_TravelAndExit')) {
    throw 'Trader departure does not carry generated children through vanilla ExitMap'
}
$leashCode = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Leashes.cs') -Raw
if (!$leashCode.Contains('GetLinksForMaster') -or !$leashCode.Contains('GetLeadYourPetLinkedPawns')) {
    throw 'Lead Your Pet linked departure fallback is missing'
}
"PASS: $checks trader gathering checks and cleanup/save wiring. Unity AI and save round trips require in-game verification."
