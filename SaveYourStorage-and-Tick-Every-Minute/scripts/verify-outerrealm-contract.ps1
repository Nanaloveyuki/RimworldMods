[CmdletBinding()]
param(
    [string]$OuterrealmAssemblyPath =
        "D:\Appdata\Steam\steamapps\workshop\content\294100\3712146789\Assemblies\FullyAutomaticOmniCrafter.dll",

    [string]$RimWorldManagedPath =
        "D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $OuterrealmAssemblyPath -PathType Leaf)) {
    throw "Outerrealm Tech assembly not found: $OuterrealmAssemblyPath"
}
if (-not (Test-Path -LiteralPath $RimWorldManagedPath -PathType Container)) {
    throw "RimWorld Managed directory not found: $RimWorldManagedPath"
}

$expectedAssemblyName = "FullyAutomaticOmniCrafter"
$routingTypeName =
    "FullyAutomaticOmniCrafter.OuterrealmStorage.OuterrealmIdentityRouting"
$viewOwnerTypeName =
    "FullyAutomaticOmniCrafter.OuterrealmStorage.OuterrealmVaultViewThingOwner"
$bindingFlags = [System.Reflection.BindingFlags](
    [System.Reflection.BindingFlags]::Static -bor
    [System.Reflection.BindingFlags]::Public -bor
    [System.Reflection.BindingFlags]::NonPublic -bor
    [System.Reflection.BindingFlags]::DeclaredOnly)
$searchDirectories = @(
    $RimWorldManagedPath,
    (Split-Path -Parent $OuterrealmAssemblyPath))

$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $fileName = ([System.Reflection.AssemblyName]$eventArgs.Name).Name + ".dll"
    foreach ($directory in $searchDirectories) {
        $candidate = Join-Path $directory $fileName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.Reflection.Assembly]::LoadFrom($candidate)
        }
    }
    return $null
}

[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $rimWorldAssembly = Join-Path $RimWorldManagedPath "Assembly-CSharp.dll"
    if (-not (Test-Path -LiteralPath $rimWorldAssembly -PathType Leaf)) {
        throw "RimWorld assembly not found: $rimWorldAssembly"
    }

    [void][System.Reflection.Assembly]::LoadFrom($rimWorldAssembly)
    $assembly = [System.Reflection.Assembly]::LoadFrom($OuterrealmAssemblyPath)
    if ($assembly.GetName().Name -ne $expectedAssemblyName) {
        throw "Unexpected assembly name: $($assembly.GetName().Name)"
    }

    $routingType = $assembly.GetType($routingTypeName, $true)
    $viewOwnerType = $assembly.GetType($viewOwnerTypeName, $true)
    $declaredMethods = @($routingType.GetMethods($bindingFlags))
    $tickMethods = @($declaredMethods | Where-Object {
        $_.Name -eq "Tick" -and $_.ReturnType -eq [void] -and
        $_.GetParameters().Count -eq 0
    })
    $moveMethods = @($declaredMethods | Where-Object {
        $_.Name -eq "MoveToHome" -and $_.ReturnType -eq [void] -and
        $_.GetParameters().Count -eq 2
    })
    $reservedMethods = @($declaredMethods | Where-Object {
        $_.Name -eq "IsReserved" -and $_.ReturnType -eq [bool] -and
        $_.GetParameters().Count -eq 2
    })
    $searchMethods = @($declaredMethods | Where-Object {
        $_.Name -eq "PrepareForSearch" -and $_.ReturnType -eq [void] -and
        $_.GetParameters().Count -eq 4
    })
    if ($tickMethods.Count -ne 1 -or -not $tickMethods[0].IsStatic) {
        throw "Expected one static void Tick() method."
    }
    if ($moveMethods.Count -ne 1 -or -not $moveMethods[0].IsStatic) {
        throw "Expected one static void MoveToHome(entry, state) method."
    }
    if ($reservedMethods.Count -ne 1 -or -not $reservedMethods[0].IsStatic) {
        throw "Expected one static bool IsReserved(entry, state) method."
    }
    if ($searchMethods.Count -ne 1 -or -not $searchMethods[0].IsStatic) {
        throw "Expected one static void PrepareForSearch(def, map, root, pawn) method."
    }
    if ($null -eq $viewOwnerType) {
        throw "Outerrealm projection owner type was not found."
    }

    $reservationManagerType = [Type]::GetType("Verse.AI.ReservationManager, Assembly-CSharp", $true)
    $pawnType = [Type]::GetType("Verse.Pawn, Assembly-CSharp", $true)
    $localTargetInfoType = [Type]::GetType("Verse.LocalTargetInfo, Assembly-CSharp", $true)
    $reservationLayerDefType = [Type]::GetType("Verse.ReservationLayerDef, Assembly-CSharp", $true)
    $canReserveStack = $reservationManagerType.GetMethod(
        "CanReserveStack",
        [System.Reflection.BindingFlags](
            [System.Reflection.BindingFlags]::Instance -bor
            [System.Reflection.BindingFlags]::Public),
        $null,
        [Type[]]@(
            $pawnType,
            $localTargetInfoType,
            [int],
            $reservationLayerDefType,
            [bool]),
        $null)
    if ($null -eq $canReserveStack -or $canReserveStack.ReturnType -ne [int]) {
        throw "ReservationManager.CanReserveStack does not match the supported signature."
    }

    $workGiverType = [Type]::GetType(
        "RimWorld.WorkGiver_ConstructDeliverResources, Assembly-CSharp",
        $true)
    $constructibleType = [Type]::GetType("RimWorld.IConstructible, Assembly-CSharp", $true)
    $jobType = [Type]::GetType("Verse.AI.Job, Assembly-CSharp", $true)
    $resourceDeliverJob = $workGiverType.GetMethod(
        "ResourceDeliverJobFor",
        [System.Reflection.BindingFlags](
            [System.Reflection.BindingFlags]::Instance -bor
            [System.Reflection.BindingFlags]::NonPublic),
        $null,
        [Type[]]@($pawnType, $constructibleType, [bool], [bool]),
        $null)
    if ($null -eq $resourceDeliverJob -or $resourceDeliverJob.ReturnType -ne $jobType) {
        throw "WorkGiver_ConstructDeliverResources.ResourceDeliverJobFor does not match the supported signature."
    }

    $moveParameters = $moveMethods[0].GetParameters()
    $reservedParameters = $reservedMethods[0].GetParameters()
    if ($reservedParameters[0].ParameterType -ne $moveParameters[0].ParameterType -or
        $reservedParameters[1].ParameterType -ne $moveParameters[1].ParameterType) {
        throw "MoveToHome and IsReserved parameter types differ."
    }

    $searchParameterNames = @(
        $searchMethods[0].GetParameters() | ForEach-Object { $_.ParameterType.FullName })
    $expectedSearchParameterNames = @(
        "Verse.ThingDef",
        "Verse.Map",
        "Verse.IntVec3",
        "Verse.Pawn")
    if (($searchParameterNames -join "|") -ne ($expectedSearchParameterNames -join "|")) {
        throw "PrepareForSearch parameter types differ from the supported contract."
    }

    $statesProperty = $routingType.GetProperty("StatesForReading", $bindingFlags)
    if ($null -eq $statesProperty -or
        $null -eq $statesProperty.GetGetMethod($true) -or
        -not $statesProperty.GetGetMethod($true).IsStatic -or
        -not [System.Collections.IDictionary].IsAssignableFrom($statesProperty.PropertyType)) {
        throw "StatesForReading is not the expected static dictionary property."
    }

    $stateType = $moveParameters[1].ParameterType
    $instanceFields = [System.Reflection.BindingFlags](
        [System.Reflection.BindingFlags]::Instance -bor
        [System.Reflection.BindingFlags]::Public -bor
        [System.Reflection.BindingFlags]::NonPublic -bor
        [System.Reflection.BindingFlags]::DeclaredOnly)
    $softClaimantField = $stateType.GetField("SoftClaimant", $instanceFields)
    $softUntilTickField = $stateType.GetField("SoftUntilTick", $instanceFields)
    if ($null -eq $softClaimantField) {
        throw "Outerrealm anchor state has no SoftClaimant field."
    }
    if ($null -eq $softUntilTickField -or $softUntilTickField.FieldType -ne [int]) {
        throw "Outerrealm anchor state has no Int32 SoftUntilTick field."
    }

    $hash = (Get-FileHash -LiteralPath $OuterrealmAssemblyPath -Algorithm SHA256).Hash
    Write-Host "Outerrealm Tech compatibility contract verified."
    Write-Host "Assembly: $OuterrealmAssemblyPath"
    Write-Host "SHA256:  $hash"
    Write-Host "Entry:   $($moveParameters[0].ParameterType.FullName)"
    Write-Host "State:   $($stateType.FullName)"
    Write-Host "View:    $($viewOwnerType.FullName)"
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
