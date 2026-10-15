[CmdletBinding()]
param(
    [string]$RimWorldDir = 'D:\Appdata\Steam\steamapps\common\RimWorld',
    [string]$MouseDisasterAssembly = 'F:\repo\Ratkin-Great-Famine-Year-Continued\1.6\Assemblies\MouseDisaster.dll'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name).Name + '.dll'
    foreach ($dir in @($managed, (Join-Path $root 'Assemblies'))) {
        $path = Join-Path $dir $name
        if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $petsPath = Join-Path $root 'Assemblies/LeadYourPet.dll'
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
    if (-not (Test-Path $MouseDisasterAssembly)) {
        Write-Host 'LeadYourPetApi public signatures verified. MouseDisaster.dll was not supplied, so old integration bindings were skipped.'
        return
    }
    $mouse = [Reflection.Assembly]::LoadFrom($MouseDisasterAssembly)
    $pets = [Reflection.Assembly]::LoadFrom((Join-Path $root 'Assemblies/LeadYourPet.dll'))
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
