$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Compile the production decision code with controlled world objects, without starting Unity.
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace PawnFlowTests {
public class DefModExtension { }
public class ModContentPack { public bool IsOfficialMod; }
public class Def {
    public string defName; public ModContentPack modContentPack; public DefModExtension extension;
    public T GetModExtension<T>() where T : DefModExtension { return extension as T; }
}
public class TraitDef : Def { }
public class ThingDef : Def { public bool IsApparel; }
public static class DefDatabase<T> { public static List<T> AllDefsListForReading = new List<T>(); }
public class Faction { public static Faction OfPlayer = new Faction(); }
public class Map { }
public struct IntVec3 { public int x; public int DistanceToSquared(IntVec3 other) { return (x-other.x)*(x-other.x); } }
public enum DevelopmentalStage { Baby, Child, Adult }
public enum PathEndMode { ClosestTouch, Touch }
public enum Danger { Deadly }
public enum ThingPlaceMode { Near }
public class Corpse { public bool Spawned; public IntVec3 Position; }
public class Container {
    public bool TryDrop(Pawn p, IntVec3 pos, Map map, ThingPlaceMode mode, out Pawn result) {
        p.ParentHolder = null; p.Spawned = true; p.Map = map; result = p; return true;
    }
}
public class Pawn_InventoryTracker { public Pawn pawn; public Container innerContainer = new Container(); }
public class Pawn {
    public bool Dead, Downed, Spawned = true, IsPrisonerOfColony, IsSlaveOfColony, reachable = true;
    public DevelopmentalStage DevelopmentalStage = DevelopmentalStage.Adult;
    public Map Map; public Map MapHeld { get { return Map; } } public IntVec3 Position;
    public Faction Faction = new Faction(); public object ParentHolder; public Corpse Corpse;
    public Pawn_InventoryTracker inventory; public Mind mindState = new Mind(); public Lord lord;
    public Pawn() { inventory = new Pawn_InventoryTracker { pawn = this }; }
    public Lord GetLord() { return lord; }
    public bool CanReserveAndReach(Pawn p, PathEndMode mode, Danger danger) { return p.reachable; }
    public bool CanReach(Pawn p, PathEndMode mode, Danger danger) { return p.reachable; }
}
public class Mind { public PawnDuty duty; }
public class PawnDuty { public PawnDuty(object def) { } }
public class Lord { public LordJob LordJob; public List<Pawn> ownedPawns = new List<Pawn>(); }
public class LordJob { public virtual void ExposeData() { } public virtual StateGraph CreateGraph() { return null; } }
public class LordToil { public Lord lord; public virtual void UpdateAllDuties() { } }
public class StateGraph { public void AddToil(LordToil toil) { } }
public class Job { public string def; public int count; public bool checkEncumbrance; public object target; }
public static class JobDefOf { public const string Wait="Wait", TakeInventory="TakeInventory", TakeFromOtherInventory="TakeFromOtherInventory"; }
public static class JobMaker { public static Job MakeJob(string def, params object[] targets) { return new Job { def=def, target=targets.Length > 0 ? targets[0] : null }; } }
public class ThinkNode_JobGiver { protected virtual Job TryGiveJob(Pawn p) { return null; } }
public enum LoadSaveMode { PostLoadInit }
public enum LookMode { Reference }
public static class Scribe { public static LoadSaveMode mode; }
public static class Scribe_References { public static void Look<T>(ref T value, string key) { } }
public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string key, LookMode mode) { } }
public static class MouseDisasterDefOf { public static object MouseDisaster_FamilyExit = new object(); }
public static class MouseDisasterUtility {
    public static bool IsPlayerAffiliatedRatkin(Pawn p) { return p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony || p.IsSlaveOfColony; }
    public static Job ExitMapJob(Pawn p, bool force = false) { return new Job { def="Exit" }; }
}
public class Harness : JobGiver_MouseDisasterFamilyExit {
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    public static int Run() {
        var official = new TraitDef { modContentPack = new ModContentPack { IsOfficialMod = true } };
        var foreign = new TraitDef { modContentPack = new ModContentPack() };
        var opted = new TraitDef { extension = new MouseDisasterGenerationExtension { allowTrait = true } };
        var owned = new TraitDef { defName = "MouseDisaster_Trait_HardyLabor" };
        Check(MouseDisasterGenerationPolicy.AllowsTrait(official), "official trait denied");
        Check(!MouseDisasterGenerationPolicy.AllowsTrait(foreign), "foreign trait leaked");
        Check(MouseDisasterGenerationPolicy.AllowsTrait(opted), "trait opt-in denied");
        Check(!MouseDisasterGenerationPolicy.AllowsTrait(null), "null trait allowed");
        Check(MouseDisasterGenerationPolicy.IsOwnedTrait(owned), "owned trait not recognized");
        Check(!MouseDisasterGenerationPolicy.IsOwnedTrait(foreign), "foreign trait marked owned");
        Check(MouseDisasterGenerationPolicy.AllowsTrait(owned), "owned trait denied");
        DefDatabase<TraitDef>.AllDefsListForReading.AddRange(new[] { official, foreign, opted, owned });
        Check(MouseDisasterGenerationPolicy.ProhibitedTraits.SequenceEqual(new[] { foreign }), "generation blacklist mismatch");
        Check(MouseDisasterGenerationPolicy.AllowsApparel(new ThingDef { IsApparel=true, defName="Apparel_TribalA" }), "tribalwear denied");
        Check(!MouseDisasterGenerationPolicy.AllowsApparel(new ThingDef { IsApparel=true, defName="Apparel_PowerArmor" }), "armor leaked");
        Check(!MouseDisasterGenerationPolicy.AllowsApparel(new ThingDef { defName="Apparel_TribalA" }), "non-apparel accepted");
        Check(MouseDisasterGenerationPolicy.AllowsApparel(new ThingDef { IsApparel=true, extension=new MouseDisasterGenerationExtension { allowRefugeeApparel=true } }), "apparel opt-in denied");
        var h = new Harness(); var map = new Map(); var adult = new Pawn { Map=map };
        var baby = new Pawn { Map=map, DevelopmentalStage=DevelopmentalStage.Baby };
        var family = new LordJob_MouseDisasterFamilyExit(adult, new[] { baby });
        var lord = new Lord { LordJob=family }; lord.ownedPawns.AddRange(new[] { adult,baby });
        adult.lord = baby.lord = lord;
        Check(h.TryGiveJob(new Pawn()) == null, "unrelated pawn received job");
        Check(h.TryGiveJob(adult).def == "TakeInventory", "trader leaves before collecting child");
        Check(h.TryGiveJob(adult).target == baby, "wrong child selected");
        Check(h.TryGiveJob(baby).def == "Wait", "child independently exits");
        baby.reachable=false;
        Check(h.TryGiveJob(adult).def == "Wait", "unreachable child abandoned");
        baby.reachable=true; baby.Spawned=false; baby.ParentHolder=adult.inventory;
        Check(h.TryGiveJob(adult).def == "Exit", "loaded family cannot leave");
        baby.Spawned=true; baby.ParentHolder=null; baby.Faction=Faction.OfPlayer;
        Check(h.TryGiveJob(adult).def == "Exit", "adopted child reclaimed");
        baby.Faction=new Faction(); baby.IsPrisonerOfColony=true;
        Check(h.TryGiveJob(adult).def == "Exit", "colony prisoner reclaimed");
        baby.IsPrisonerOfColony=false; baby.Dead=true;
        Check(h.TryGiveJob(adult).def == "Exit", "dead child prevents departure");
        baby.Dead=false; adult.Downed=true;
        Check(h.TryGiveJob(baby).def == "Wait", "baby chosen as replacement carrier");
        var escort = new Pawn { Map=map, lord=lord }; lord.ownedPawns.Add(escort);
        Check(h.TryGiveJob(escort).def == "TakeInventory" && family.carrier == escort, "escort cannot replace downed carrier");
        baby.Spawned=false; baby.ParentHolder=adult.inventory;
        Check(h.TryGiveJob(escort).def == "TakeFromOtherInventory", "child left with downed carrier");
        adult.Dead=true; adult.Corpse = new Corpse { Spawned=true };
        Check(h.TryGiveJob(escort).def == "TakeInventory" && baby.Spawned, "child trapped in dead carrier inventory");
        return checks;
    }
}
}
'@
$source = foreach ($name in 'MouseDisasterGenerationExtension.cs','LordJob_MouseDisasterFamilyExit.cs') {
    $code = Get-Content (Join-Path $root "1.6/Source/$name") -Raw
    $code = [regex]::Replace($code, '(?m)^using [^;]+;\r?\n', '')
    $code.Replace('namespace MouseDisaster', 'namespace PawnFlowTests')
}
Add-Type -TypeDefinition ($stub + ($source -join "`n"))
$checks = [PawnFlowTests.Harness]::Run()
"PASS: $checks pawn component checks. Unity pathing and UI remain in-game checks."
