[CmdletBinding()]
param(
    [string]$RimWorldDir = $(if ($env:RIMWORLD_DIR) { $env:RIMWORLD_DIR } elseif ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'E:\Apps\Steam\steamapps\common\RimWorld' } else { '/mnt/e/Apps/Steam/steamapps/common/RimWorld' }),
    [ValidateSet('1.6', '1.7')][string]$RimWorldVersion = $(if ($env:RIMWORLD_VERSION) { $env:RIMWORLD_VERSION } else { '1.7' }),
    [string]$MouseDisasterAssembly = $env:MOUSE_DISASTER_ASSEMBLY,
    [string]$HarmonyAssembly = $env:HARMONY_ASSEMBLY
)
$ErrorActionPreference = 'Stop'
if ($RimWorldVersion -eq '1.7' -and [Environment]::Version.Major -lt 10) { throw 'RimWorld 1.7 verification requires pwsh running on .NET 10; Windows PowerShell 5.1 cannot load net10.0 game DLLs.' }
$root = Split-Path $PSScriptRoot -Parent
$runtime = if ($RimWorldVersion -eq '1.7') { '1.7/' } else { '' }
$managed = if ($RimWorldVersion -eq '1.7') { $RimWorldDir } else { Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed' }
$assemblies = Join-Path $root "${runtime}Assemblies"
if (!(Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw "RimWorld $RimWorldVersion game assembly not found: $managed" }
if ($HarmonyAssembly) {
    if (!(Test-Path -LiteralPath $HarmonyAssembly)) { throw "Harmony assembly not found: $HarmonyAssembly" }
    [Reflection.Assembly]::LoadFrom($HarmonyAssembly) | Out-Null
}
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name).Name + '.dll'
    foreach ($dir in @($managed, $assemblies)) {
        $path = Join-Path $dir $name
        if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $petsPath = Join-Path $assemblies 'LeadYourPet.dll'
    $pets = [Reflection.Assembly]::LoadFrom($petsPath)
    $api = $pets.GetType('LeadYourPet.LeadYourPetApi', $true)
    $publicStatic = [Reflection.BindingFlags]'Public,Static'
    $pawnName = 'Verse.Pawn'
    $lordName = 'Verse.AI.Group.Lord'
    $cellName = 'Verse.IntVec3'
    function Format-ApiType([type]$type) {
        if ($type.IsGenericType -and $type.GetGenericTypeDefinition().FullName -eq 'System.Collections.Generic.List`1') {
            return 'System.Collections.Generic.List`1[[' + $type.GetGenericArguments()[0].FullName + ']]'
        }
        return $type.FullName
    }
    function Test-ApiMethod([string]$name, [string]$returnName, [string[]]$parameterNames) {
        $matches = @($api.GetMethods($publicStatic) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $parameterNames.Count })
        if ($matches.Count -ne 1) { throw "Unresolved public API: $name" }
        $method = $matches[0]
        $actualReturn = Format-ApiType $method.ReturnType
        if ($actualReturn -ne $returnName) { throw "Public API return mismatch: $name -> $actualReturn" }
        $parameters = $method.GetParameters()
        for ($i = 0; $i -lt $parameterNames.Count; $i++) {
            $actual = Format-ApiType $parameters[$i].ParameterType
            if ($actual -ne $parameterNames[$i]) { throw "Public API parameter mismatch: $name[$i] -> $actual" }
        }
        Write-Host "LeadYourPetApi.$name verified"
    }
    Test-ApiMethod 'get_Available' 'System.Boolean' @()
    Test-ApiMethod 'TryStartMotherLeash' 'System.Boolean' @($pawnName, $pawnName)
    Test-ApiMethod 'TryStartChildLeash' 'System.Boolean' @($pawnName, $pawnName)
    Test-ApiMethod 'TryAssignTravelChildren' 'System.Boolean' @($lordName)
    Test-ApiMethod 'TryAnchor' 'System.Boolean' @($pawnName, $cellName)
    Test-ApiMethod 'EndForPet' 'System.Void' @($pawnName)
    Test-ApiMethod 'EndForMaster' 'System.Void' @($pawnName)
    Test-ApiMethod 'ClearOwnership' 'System.Void' @($pawnName)
    Test-ApiMethod 'TryCopyLinkedPets' 'System.Boolean' @($pawnName, 'System.Collections.Generic.List`1[[Verse.Pawn]]')
    Test-ApiMethod 'SetSpecialSource' 'System.Void' @($pawnName, 'System.Int32')
    Test-ApiMethod 'IsLeashed' 'System.Boolean' @($pawnName)
    if (!$MouseDisasterAssembly) {
        Write-Host "RimWorld $RimWorldVersion LeadYourPetApi public signatures verified. Supply a matching -MouseDisasterAssembly (and external -HarmonyAssembly if needed) to verify integration bindings."
        return
    }
    if (!(Test-Path -LiteralPath $MouseDisasterAssembly)) { throw "MouseDisaster assembly not found: $MouseDisasterAssembly" }
    $mouse = [Reflection.Assembly]::LoadFrom($MouseDisasterAssembly)
    $pets = [Reflection.Assembly]::LoadFrom($petsPath)
    $utility = $pets.GetType('LeadYourPet.LeadYourPetUtility', $true)
    [Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($utility.TypeHandle)
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    foreach ($name in @('MouseDisasterGenerateFactionRatkinPawnMethod','MouseDisasterSetBiologicalAgeYearsMethod','MouseDisasterPrepareTradablePrisonerMethod','MouseDisasterClearTradeLeaderStateMethod','MouseDisasterResetBeggarStateMethod','MouseDisasterMakeFactionHostileToPlayerMethod','MouseDisasterTryRefreshHiddenFactionRelationsMethod','MouseDisasterNotifyIdentityChangedMethod','MouseDisasterTryMakeHostileVisitorsMethod')) {
        $method = $utility.GetField($name, $flags).GetValue($null)
        if ($null -eq $method) { throw "Unresolved integration: $name" }
        Write-Host "$name -> $($method.DeclaringType.FullName).$method"
    }
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $pawn = $game.GetType('Verse.Pawn', $true)
    $component = $pets.GetType('LeadYourPet.LeadYourPetGameComponent', $true)
    $signatures = @{
        TryStartRatkinMotherLeash = [Type[]]@($pawn,$pawn,[bool])
        StartLeash = [Type[]]@($pawn,$pawn,[bool],[bool])
        TryAssignTravelMouseEggs = [Type[]]@($game.GetType('Verse.AI.Group.Lord', $true))
        AnchorLeashedPetsToCell = [Type[]]@($pawn,$game.GetType('Verse.IntVec3', $true))
        EndLeashForPet = [Type[]]@($pawn,[bool])
    }
    foreach ($name in $signatures.Keys) {
        if ($null -eq $component.GetMethod($name, $signatures[$name])) { throw "Unresolved reverse integration: $name" }
    }
    Write-Host 'LeadYourPetUtility initialized against real MouseDisaster DLL; 9 forward bindings and 5 reverse signatures verified. No game simulation performed.'
} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
