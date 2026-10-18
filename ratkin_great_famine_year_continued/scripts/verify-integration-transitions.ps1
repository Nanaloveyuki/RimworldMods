$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
function Read-Source([string]$file) { Get-Content (Join-Path $root "1.6/Source/$file") -Raw }
$delivery = Read-Source 'Utilities/MouseDisasterUtility.AbandonedDelivery.cs'
$leashes = Read-Source 'Utilities/MouseDisasterUtility.Leashes.cs'
$age = Read-Source 'AgePatches.cs'
$methods = @(
    (Get-CSharpMethod (Read-Source 'ReliefAreaFoodPatches.cs') 'BoostScore')
    (Get-CSharpMethod $delivery 'NotifyAbandonedChildDropped')
    (Get-CSharpMethod $leashes 'TryReleaseLeadYourPetTradePawn')
    (Get-CSharpMethod $leashes 'ClearLeadYourPetOwnershipState')
    (Get-CSharpMethod $age 'UsesFallback')
    (Get-CSharpMethod $age 'KeepAgeImmobility')
) -join "`n"
$stub = @'
using System;
using System.Collections.Generic;
using System.Reflection;
namespace IntegrationTests {
public enum DevelopmentalStage { Baby, Child, Adult }
public class Map { public int uniqueID=1; }
public struct IntVec3 {
    public int x;
    public bool InHorDistOf(IntVec3 other,float radius)=>Math.Abs(x-other.x)<=radius;
}
public class Pawn {
    public bool Spawned=true,Dead,player,eventPawn=true;
    public object ageTracker=new object();
    public DevelopmentalStage DevelopmentalStage;
    public int thingIDNumber;
    public Map Map=new Map();
    public IntVec3 Position;
}
public class AbandonedDeliveryState {
    public int mapId=1;
    public IntVec3 foodCell;
    public List<int> childPawnIds=new List<int>();
    public HashSet<int> deliveredChildIds=new HashSet<int>();
}
public static class ModsConfig {
    public static bool toddlers;
    public static bool IsActive(string id) {
        if(id!="cyanobot.toddlers") throw new Exception("Wrong Toddlers package ID");
        return toddlers;
    }
}
public static class Current { public static object Game=new object(); }
public static class MouseDisasterUtility { public static bool IsMouseDisasterPawn(Pawn p)=>p.eventPawn; }
public class Provider {
    public readonly List<string> calls=new List<string>();
    public void End(Pawn p,bool message) { if(message) throw new Exception("Unexpected message"); calls.Add("end:"+p.thingIDNumber); }
    public void Pet(Pawn p) { calls.Add("pet:"+p.thingIDNumber); }
    public void Travel(Pawn p) { calls.Add("travel:"+p.thingIDNumber); }
}
public static class Tests {
    static readonly Provider provider=new Provider();
    static bool IsLeadYourPetEnabled=true;
    static Type leadYourPetComponentType=typeof(Provider);
    static MethodInfo gameGetComponentMethod=typeof(Provider).GetMethod("End");
    static MethodInfo leadYourPetEndLeashForPetMethod=typeof(Provider).GetMethod("End");
    static MethodInfo leadYourPetClearMouseEggPetStateMethod=typeof(Provider).GetMethod("Pet");
    static MethodInfo leadYourPetClearTravelStockMethod=typeof(Provider).GetMethod("Travel");
    static void EnsureLeadYourPetReflection() {}
    static object TryGetLeadYourPetComponent()=>provider;
    static void InvokeLeadYourPet(MethodInfo method,object component,object[] args)=>method.Invoke(component,args);
    static List<Pawn> linked=new List<Pawn>();
    static List<Pawn> GetLeadYourPetLinkedPawns(Pawn p)=>new List<Pawn>(linked);
    static bool IsPlayerAffiliatedRatkin(Pawn p)=>p.player;
    static Dictionary<int,AbandonedDeliveryState> ActiveAbandonedDeliveryByAdultId=new Dictionary<int,AbandonedDeliveryState>();
    static int count;
    static void Check(bool ok,string message) { count++; if(!ok) throw new Exception(message); }
    public static string Run() {
        Check(Math.Abs(BoostScore(300,0.1f)-330)<0.001,"positive score");
        Check(Math.Abs(BoostScore(-300,0.1f)+270)<0.001,"negative score must improve");
        Check(BoostScore(300,0)==300,"disabled bonus");
        Check(BoostScore(-9999999,1)==-9999999,"invalid food sentinel");
        Check(float.IsNaN(BoostScore(float.NaN,1)),"NaN preservation");
        Check(float.IsInfinity(BoostScore(float.PositiveInfinity,1)),"infinity preservation");
        var child=new Pawn { thingIDNumber=7 };
        Check(!KeepAgeImmobility(true,child),"healthy event infant fallback");
        ModsConfig.toddlers=true;
        Check(KeepAgeImmobility(true,child),"Toddlers must own movement");
        ModsConfig.toddlers=false; child.eventPawn=false;
        Check(KeepAgeImmobility(true,child),"unrelated infant changed");
        child.eventPawn=true; child.DevelopmentalStage=DevelopmentalStage.Child;
        Check(KeepAgeImmobility(true,child),"non-infant changed");
        Check(!KeepAgeImmobility(false,child),"introduced age immobility");
        var state=new AbandonedDeliveryState(); state.childPawnIds.Add(7);
        ActiveAbandonedDeliveryByAdultId.Add(1,state);
        NotifyAbandonedChildDropped(child);
        Check(state.deliveredChildIds.Contains(7) && provider.calls.Count==3,"first drop must release all provider state");
        child.Position=new IntVec3 { x=10 }; NotifyAbandonedChildDropped(child);
        Check(state.deliveredChildIds.Contains(7) && provider.calls.Count==3,"crawling outside must retain delivery");
        child.Position=new IntVec3(); NotifyAbandonedChildDropped(child);
        Check(provider.calls.Count==3,"duplicate drop repeated cleanup");
        var other=new Pawn { thingIDNumber=8 }; NotifyAbandonedChildDropped(other);
        Check(!state.deliveredChildIds.Contains(8),"unrelated baby marked delivered");
        state.childPawnIds.Add(8); other.player=true; NotifyAbandonedChildDropped(other);
        Check(!state.deliveredChildIds.Contains(8),"player baby delivery changed");
        other.player=false; other.Map.uniqueID=2; NotifyAbandonedChildDropped(other);
        Check(!state.deliveredChildIds.Contains(8),"cross-map delivery changed");
        linked.Add(child); provider.calls.Clear(); TryReleaseLeadYourPetTradePawn(other);
        Check(provider.calls.Count==6,"master acquisition must clear linked pets too");
        IsLeadYourPetEnabled=false; provider.calls.Clear(); TryReleaseLeadYourPetTradePawn(other);
        Check(provider.calls.Count==0,"absent provider path");
        IsLeadYourPetEnabled=true; leadYourPetClearTravelStockMethod=null;
        TryReleaseLeadYourPetTradePawn(other);
        Check(provider.calls.Count==4,"older provider optional API");
        return "PASS: "+count+" integration transition assertions.";
    }
'@
Add-Type -TypeDefinition ($stub + $methods + "`n}}")
[IntegrationTests.Tests]::Run()
$generation = Read-Source 'Utilities/MouseDisasterUtility.Generation.cs'
if ($generation -notmatch '!hasFixedAge && !preserveRoleAge && stage != DevelopmentalStage.Baby') { throw 'Special-age exemption missing' }
if ($generation -notmatch 'requestedXenotype, fixedGender.HasValue') { throw 'Fixed-role age exemption disconnected' }
$exchange = Get-CSharpMethod (Read-Source 'Utilities/MouseDisasterUtility.ChildExchange.cs') 'TryResolveChildExchange'
if ($exchange -notmatch 'TryReleaseLeadYourPetTradePawn') { throw 'Exchange cleanup disconnected' }
$control = Read-Source 'GameComponent_MouseDisasterVisitorControl.cs'
foreach ($name in @('BringPawnUnderPlayerProtection','TrySetVisitorCaptiveStatus','TrySendPawnToPrison')) {
    $method = Get-CSharpMethod $control $name
    if ($method -notmatch 'TryReleaseLeadYourPetTradePawn' -or $method -notmatch 'RemoveChildExchangeTrackingForPurchasedPawn') { throw "Acquisition cleanup disconnected: $name" }
}
if (($delivery | Select-String 'typeof\(Thing\).MakeByRefType\(\)' -AllMatches).Matches.Count -ne 2) { throw 'Both drop overloads must be patched' }
'PASS: age, exchange, capture and carry-drop wiring checks (source contracts, not live-game tests).'
