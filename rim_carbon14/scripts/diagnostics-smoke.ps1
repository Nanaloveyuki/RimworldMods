param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'))
. (Join-Path $PSScriptRoot '../../scripts/carbon/test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/diagnostics-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$samples = @()
try {
    Call game_pause @{paused=$true} | Out-Null
    foreach ($enabled in @($false,$true,$false,$true)) {
        Call ui_capture @{enabled=$enabled} | Out-Null
        $sample = Call diagnostics_sample @{frames=180} 30000
        if ($sample.elapsedFrames -lt 180 -or $sample.fps -le 0 -or $sample.elapsedTicks -ne 0) { throw 'Paused frame measurement is inconsistent.' }
        $samples += @{uiObservation=$enabled;paused=$true;measurement=$sample}
    }
    Call game_pause @{paused=$false} | Out-Null
    $sample = Call diagnostics_sample @{frames=180} 30000
    if ($sample.elapsedTicks -le 0 -or $sample.tps -le 0) { throw 'Running simulation did not report Tick progress.' }
    $samples += @{uiObservation=$true;paused=$false;measurement=$sample}
    $results.passed = $true
} finally {
    Call game_pause @{paused=$true} | Out-Null
    Call ui_capture @{enabled=$true} | Out-Null
    @{samples=$samples;scope='UI observation off is not a no-mod baseline';operations=$results} | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'results.json') -Encoding utf8
    Write-Output "Evidence: $out"
}
$samples | ForEach-Object { [pscustomobject]@{uiObservation=$_.uiObservation;paused=$_.paused;fps=$_.measurement.fps;tps=$_.measurement.tps;allocatedBytes=$_.measurement.mainThreadAllocatedBytes} }
