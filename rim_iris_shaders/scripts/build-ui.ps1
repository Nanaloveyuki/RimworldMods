[CmdletBinding()]
param(
    [ValidateSet('1.6', '1.7')][string]$RimWorldVersion = '1.7',
    [string]$GameDir,
    [string]$RimIrisAssemblyPath,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
if (-not $GameDir) {
    $GameDir = if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        'E:\Apps\Steam\steamapps\common\RimWorld'
    } else {
        '/mnt/e/Apps/Steam/steamapps/common/RimWorld'
    }
}
$GameDir = [IO.Path]::GetFullPath($GameDir)
if (-not $RimIrisAssemblyPath) {
    $RimIrisAssemblyPath = Join-Path $GameDir 'Mods/3796735623/Assemblies/RimIris.dll'
}
$RimIrisAssemblyPath = [IO.Path]::GetFullPath($RimIrisAssemblyPath)
$managedDir = $GameDir
if ($RimWorldVersion -eq '1.6' -and -not (Test-Path -LiteralPath (Join-Path $managedDir 'Assembly-CSharp.dll') -PathType Leaf)) {
    $managedDir = Join-Path $GameDir 'RimWorldWin64_Data/Managed'
}
$assemblies = if ($RimWorldVersion -eq '1.6') {
    @('Assembly-CSharp', 'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.TextRenderingModule')
} else {
    @('Assembly-CSharp', 'UnityStubs', 'RimWorld', 'Waylith', 'Waylith.UnityCompat',
        'Brutal.Core.Common', 'Brutal.Core.Collections', 'Brutal.Core.Logging', 'Brutal.Core.Maths',
        'Brutal.Core.Memory', 'Brutal.Core.Numerics', 'Brutal.Core.Strings', 'Brutal.Glfw',
        'Brutal.Gltf', 'Brutal.ImGui', 'Brutal.Interop', 'Brutal.Slang', 'Brutal.Texture',
        'Brutal.Vulkan', 'Brutal.Vulkan.Abstractions', 'Brutal.Vulkan.Vma')
}
foreach ($assembly in $assemblies) {
    $file = Join-Path $managedDir "$assembly.dll"
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing RimWorld $RimWorldVersion assembly: $file. Pass -GameDir for that installed game version (or its 1.6 Managed directory), not decompiled source."
    }
}
if (-not (Test-Path -LiteralPath $RimIrisAssemblyPath -PathType Leaf)) {
    throw "Missing RimIris assembly: $RimIrisAssemblyPath"
}
& dotnet build (Join-Path $PSScriptRoot '../Source/Moonlight.UI.csproj') -c $Configuration "-p:RimWorldVersion=$RimWorldVersion" "-p:RimWorldManagedDir=$managedDir" "-p:RimIrisAssemblyPath=$RimIrisAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw "RimWorld $RimWorldVersion UI build failed: $LASTEXITCODE" }
$output = if ($RimWorldVersion -eq '1.6') { 'Assemblies/RimIrisShaders.UI.dll' } else { '1.7/Assemblies/RimIrisShaders.UI.dll' }
Write-Output "Built RimWorld ${RimWorldVersion}: $output"
