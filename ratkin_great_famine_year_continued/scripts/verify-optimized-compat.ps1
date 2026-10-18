param(
    [string]$RimWorldManagedDir = 'D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed',
    [string]$OptimizedModRoot
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[Reflection.Assembly]::LoadFrom((Join-Path $RimWorldManagedDir 'UnityEngine.CoreModule.dll')) > $null
[Reflection.Assembly]::LoadFrom((Join-Path $RimWorldManagedDir 'Assembly-CSharp.dll')) > $null
$alias = [Reflection.Assembly]::LoadFrom((Join-Path $root '1.6/Assemblies/AL_MouseDisaster.dll'))
if ($alias.GetName().Name -ne 'AL_MouseDisaster' -or $alias.GetName().Version.ToString() -ne '1.0.0.0') { throw 'Optimized assembly identity changed' }
if ($alias.GetTypes().Count -ne 0) { throw 'Forwarding assembly defines duplicate gameplay types' }
$types = @($alias.GetForwardedTypes())
if ($types.Count -ne 27) { throw "Unexpected forwarder count: $($types.Count)" }
foreach ($type in $types) {
    $qualified = "$($type.FullName), AL_MouseDisaster, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    if ([Type]::GetType($qualified, $true) -ne $type -or $type.Assembly.GetName().Name -ne 'MouseDisaster') { throw "Type identity split: $qualified" }
}
$nested = [Type]::GetType('MouseDisaster.GameComponent_MouseDisasterBroadcastHope+BroadcastHopeSchedule, AL_MouseDisaster', $true)
if ($nested.Assembly.GetName().Name -ne 'MouseDisaster') { throw 'Nested saved type did not forward' }
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$driver = Get-Content (Join-Path $root '1.6/Source/JobDriver_MotherFeedBaby.cs') -Raw
$method = Get-CSharpMethod $driver 'MakeNewToils'
$stub = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace OptimizedJobTests {
public enum JobCondition { Incompletable }
public class Toil { public Action initAction,tickAction; }
public static class Toils_General { public static Toil DoAtomic(Action action)=>new Toil{initAction=action}; }
public abstract class DriverBase { protected abstract IEnumerable<Toil> MakeNewToils(); }
public class Driver:DriverBase {
 private int cancellations;
 private void EndJobWith(JobCondition condition) { if(condition!=JobCondition.Incompletable)throw new Exception(); cancellations++; }
 public static void Run() {
  for(int savedIndex=0;savedIndex<4;savedIndex++) {
   var driver=new Driver(); var toils=driver.MakeNewToils().ToList();
   if(toils.Count!=4)throw new Exception("Legacy toil index lost");
   toils[savedIndex].tickAction();
   if(driver.cancellations!=1)throw new Exception("Resumed feeding job not retired");
  }
  var fresh=new Driver(); fresh.MakeNewToils().First().initAction();
  if(fresh.cancellations!=1)throw new Exception("Queued legacy job not retired");
 }
'@
Add-Type -TypeDefinition ($stub + $method + '} }')
[OptimizedJobTests.Driver]::Run()
$definitions = @{}
foreach ($file in Get-ChildItem (Join-Path $root 'Defs') -Recurse -Filter *.xml) {
    [xml]$doc = Get-Content $file.FullName -Raw
    foreach ($def in $doc.SelectNodes('/Defs/*[defName]')) { $definitions["$($def.LocalName):$($def.defName)"]=$true }
}
foreach ($name in 'AidVisitor','IntelMessenger','LaboringRefugee','StrongSieger','Passerby','AirdropBaby','MisguidedKin','CaravanMugger') {
    if (!$definitions.ContainsKey("HediffDef:MouseDisaster_$name")) { throw "Missing optimized marker: $name" }
}
if (!$definitions.ContainsKey('JobDef:MouseDisaster_MotherFeedBaby')) { throw 'Missing legacy feeding task' }
if ($OptimizedModRoot) {
    foreach ($file in Get-ChildItem (Join-Path $OptimizedModRoot '1.6/Defs') -Recurse -Filter *.xml) {
        [xml]$doc = Get-Content $file.FullName -Raw
        foreach ($def in $doc.SelectNodes('/Defs/*[defName]')) {
            $key = "$($def.LocalName):$($def.defName)"
            if (!$definitions.ContainsKey($key)) { throw "Missing optimized Def: $key" }
        }
    }
}
[xml]$folders = Get-Content (Join-Path $root 'LoadFolders.xml') -Raw
[xml]$about = Get-Content (Join-Path $root 'About/About.xml') -Raw
foreach ($id in 'lezhizhong.mouse.disaster.famine','local.mousedisaster.greatfamine') {
    foreach ($folder in $folders.SelectNodes('/loadFolders/v1.6/li[@IfModNotActive]')) {
        if ($folder.IfModNotActive.Split(',') -notcontains $id) { throw "Missing load exclusion: $id" }
    }
    if ($about.ModMetaData.incompatibleWith.li -notcontains $id) { throw "Missing incompatibleWith: $id" }
}
Write-Host 'PASS: 27 real DLL type forwarders, nested saved type, 4 resumed/1 queued job states, legacy Defs, duplicate-load exclusions. No game was started; real save round trips still require in-game verification.'
