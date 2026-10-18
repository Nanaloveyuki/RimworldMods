[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$UnityPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Tools/Unity'
$log = Join-Path (Split-Path $root -Parent) 'artifacts/rim_iris_shaders/build.log'
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity not found: $UnityPath" }
New-Item -ItemType Directory -Force (Split-Path $log -Parent) | Out-Null
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'MoonlightBuilder.Build', '-buildTarget', 'Win64',
    '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    Get-Content -LiteralPath $log -Tail 80
    throw "Unity build failed ($($process.ExitCode)). See $log"
}
if (-not (Select-String -LiteralPath $log -SimpleMatch '[Moonlight] Bundle reload and GPU validation passed; bundle published.')) {
    throw "Build completion marker missing. See $log"
}
Write-Output "Built and GPU-validated: $root/ShaderPacks/Moonlight/moonlight_win"
