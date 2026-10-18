$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$journal = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_Journal.cs') -Raw
$main = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative.cs') -Raw
$trust = Get-Content (Join-Path $root '1.6/Source/GameComponent_MouseDisasterNarrative_N004.cs') -Raw
$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
$tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($main).GetRoot()
$gate = ($tree.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax] -and $_.Identifier.ValueText -eq 'CountsNarrativeState' }).ToFullString()
$methods = (Get-CSharpMethod $journal 'CompleteNarrativeFlag') + (Get-CSharpMethod $journal 'NotifyNarrativeBroadcast') + (Get-CSharpMethod $trust 'ChangeNarratorTrust')
$harness = @'
using System;
using System.Collections.Generic;
public static class Mathf { public static int Clamp(int n,int min,int max)=>Math.Min(max,Math.Max(min,n)); }
public class Settings { public bool countWithoutSuin=true; }
public static class MouseDisasterMod { public static Settings Settings=new Settings(); }
public class CounterHarness {
 static bool suin; static bool IsNarratorActive()=>suin;
 int narratorTrust,successfulBroadcasts,firstNarrativeTick=-1,CurrentNarrativeTick=100;
 List<string> countedNarrativeFlags=new List<string>(),narrativeFlags=new List<string>();
 static int checks; static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 public static int Run(){
  var n=new CounterHarness();MouseDisasterMod.Settings.countWithoutSuin=false;
  n.ChangeNarratorTrust(5);n.NotifyNarrativeBroadcast();n.CompleteNarrativeFlag("S01");
  Check(n.narratorTrust==0&&n.successfulBroadcasts==0,"disabled counters advanced");
  Check(n.countedNarrativeFlags.Count==0&&n.firstNarrativeTick==-1,"disabled progression advanced");
  Check(n.narrativeFlags.Contains("S01"),"disabled counters broke reward deduplication");
  suin=true;n.ChangeNarratorTrust(5);n.NotifyNarrativeBroadcast();n.CompleteNarrativeFlag("S02");
  Check(n.narratorTrust==5&&n.successfulBroadcasts==1,"Suin depends on non-Suin switch");
  Check(n.countedNarrativeFlags.Count==1&&n.firstNarrativeTick==100,"Suin progress missing");
  suin=false;MouseDisasterMod.Settings.countWithoutSuin=true;n.ChangeNarratorTrust(-2);n.NotifyNarrativeBroadcast();n.CompleteNarrativeFlag("S03");
  Check(n.narratorTrust==3&&n.successfulBroadcasts==2&&n.countedNarrativeFlags.Count==2,"enabled counters did not resume");
  n.CompleteNarrativeFlag("S03");Check(n.countedNarrativeFlags.Count==2,"progress double counted");
  MouseDisasterMod.Settings.countWithoutSuin=false;n.ChangeNarratorTrust(-100);n.NotifyNarrativeBroadcast();
  Check(n.narratorTrust==3&&n.successfulBroadcasts==2,"toggle erased existing state");
  return checks;
 }
'@
Add-Type -TypeDefinition ($harness + $gate + $methods + '}')
$checks = [CounterHarness]::Run()
# Settings serialization and reset contracts are checked against the real syntax tree.
$expose = Get-CSharpMethod $settings 'ExposeData'
$reset = Get-CSharpMethod $settings 'ResetToDefaults'
foreach ($name in @('leaveAfterFed','countWithoutSuin','endingsWithoutSuin','positiveIncidentDays','negativeIncidentDays','positiveIncidents','raidReplacementIncidents')) {
    if ($expose -notmatch ('ref ' + $name + ', "' + $name + '"') -or $reset -notmatch $name) { throw "Missing persistence/reset contract: $name" }
}
$endings = Get-CSharpMethod $journal 'ProcessNarrativeEndings'
if ($endings -notmatch '!IsNarratorActive\(\) && settings\?\.endingsWithoutSuin == false' -or $endings -notmatch 'CountsNarrativeState && CurrentNarrativeTick >= nextPopulationTick') { throw 'Ending/census toggle guard missing' }
Write-Host "PASS: $checks counter assertions; 7 persistence/reset contracts; ending and census guards."
