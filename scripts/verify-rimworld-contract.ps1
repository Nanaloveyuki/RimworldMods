[CmdletBinding()]
param(
    [string]$RimWorldManagedPath =
        "D:\Appdata\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $RimWorldManagedPath -PathType Container)) {
    throw "RimWorld Managed directory not found: $RimWorldManagedPath"
}

$assemblyPath = Join-Path $RimWorldManagedPath "Assembly-CSharp.dll"
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "RimWorld assembly not found: $assemblyPath"
}

$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $fileName = ([System.Reflection.AssemblyName]$eventArgs.Name).Name + ".dll"
    $candidate = Join-Path $RimWorldManagedPath $fileName
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return [System.Reflection.Assembly]::LoadFrom($candidate)
    }
    return $null
}

function Get-ExactMethod {
    param(
        [Type]$Type,
        [string]$Name,
        [System.Reflection.BindingFlags]$Flags,
        [Type[]]$ParameterTypes,
        [string]$ReturnTypeName
    )

    $method = $Type.GetMethod($Name, $Flags, $null, $ParameterTypes, $null)
    if ($null -eq $method -or $method.ReturnType.FullName -ne $ReturnTypeName) {
        throw "$($Type.FullName).$Name does not match the supported signature."
    }
    return $method
}

function Get-CallCount {
    param(
        [System.Reflection.MethodInfo]$Method,
        [System.Reflection.MethodInfo]$CalledMethod
    )

    $bytes = $Method.GetMethodBody().GetILAsByteArray()
    $tokenBytes = [BitConverter]::GetBytes($CalledMethod.MetadataToken)
    $count = 0
    for ($i = 0; $i -le $bytes.Length - 5; $i++) {
        if ($bytes[$i] -ne 0x28 -and $bytes[$i] -ne 0x6F) {
            continue
        }

        if ($bytes[$i + 1] -eq $tokenBytes[0] -and
            $bytes[$i + 2] -eq $tokenBytes[1] -and
            $bytes[$i + 3] -eq $tokenBytes[2] -and
            $bytes[$i + 4] -eq $tokenBytes[3]) {
            $count++
        }
    }
    return $count
}

[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
    $thingType = $assembly.GetType("Verse.Thing", $true)
    $mapType = $assembly.GetType("Verse.Map", $true)
    $intVec3Type = $assembly.GetType("Verse.IntVec3", $true)
    $pawnType = $assembly.GetType("Verse.Pawn", $true)
    $jobType = $assembly.GetType("Verse.AI.Job", $true)
    $genThingType = $assembly.GetType("RimWorld.GenThing", $true)
    $storeUtilityType = $assembly.GetType("RimWorld.StoreUtility", $true)
    $workGiverMergeType = $assembly.GetType("RimWorld.WorkGiver_Merge", $true)
    $slotGroupType = $assembly.GetType("RimWorld.ISlotGroup", $true)
    $thingGridType = $assembly.GetType("Verse.ThingGrid", $true)
    $listerMergeablesType = $assembly.GetType("RimWorld.ListerMergeables", $true)

    $staticNonPublic = [System.Reflection.BindingFlags](
        [System.Reflection.BindingFlags]::Static -bor
        [System.Reflection.BindingFlags]::NonPublic)
    $instancePublic = [System.Reflection.BindingFlags](
        [System.Reflection.BindingFlags]::Instance -bor
        [System.Reflection.BindingFlags]::Public)
    $instanceNonPublic = [System.Reflection.BindingFlags](
        [System.Reflection.BindingFlags]::Instance -bor
        [System.Reflection.BindingFlags]::NonPublic)

    [void](Get-ExactMethod `
        -Type $genThingType `
        -Name "ItemCenterAt" `
        -Flags $staticNonPublic `
        -ParameterTypes ([Type[]]@($thingType)) `
        -ReturnTypeName "UnityEngine.Vector3")

    [void](Get-ExactMethod `
        -Type $storeUtilityType `
        -Name "NoStorageBlockersIn" `
        -Flags $staticNonPublic `
        -ParameterTypes ([Type[]]@($intVec3Type, $mapType, $thingType)) `
        -ReturnTypeName "System.Boolean")

    $jobOnThing = Get-ExactMethod `
        -Type $workGiverMergeType `
        -Name "JobOnThing" `
        -Flags $instancePublic `
        -ParameterTypes ([Type[]]@($pawnType, $thingType, [bool])) `
        -ReturnTypeName $jobType.FullName
    $heldThingsGetter = $slotGroupType.GetProperty("HeldThings").GetGetMethod()
    if ((Get-CallCount -Method $jobOnThing -CalledMethod $heldThingsGetter) -ne 1) {
        throw "WorkGiver_Merge.JobOnThing no longer has exactly one ISlotGroup.HeldThings call."
    }

    if ($null -eq $thingGridType.GetField("map", $instanceNonPublic)) {
        throw "ThingGrid no longer has the private map field used by Harmony injection."
    }
    if ($null -eq $listerMergeablesType.GetField("map", $instanceNonPublic)) {
        throw "ListerMergeables no longer has the private map field used by Harmony injection."
    }
    if ($null -eq ([System.Collections.Generic.List[object]]).GetField(
        "_version",
        $instanceNonPublic)) {
        throw "List<T> no longer exposes the private _version field used for cache validation."
    }

    $hash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
    Write-Host "RimWorld dense-Thing compatibility contract verified."
    Write-Host "Assembly: $assemblyPath"
    Write-Host "SHA256:  $hash"
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
