param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'), [int]$Ticks = 180)
. (Join-Path $PSScriptRoot 'test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/watch-overhead-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$samples = @()
try {
    $catalog = Call objects_members @{type='RimCarbon14.Fixture.Fixture';limit=200}
    $methods = @{}
    foreach ($m in $catalog.items | Where-Object kind -eq Method) { $methods[$m.name] = $m.methodId }
    foreach ($mode in @('off','watch','output','output','watch','off')) {
        Call game_load @{name='carbon-fixture'} 180000 | Out-Null
        Call game_pause @{paused=$true} | Out-Null
        Call objects_invoke @{methodId=$methods.WatchReset} | Out-Null
        Call objects_invoke @{methodId=$methods.WatchMeasureTicks;arguments=@(30)} 30000 | Out-Null
        Call objects_invoke @{methodId=$methods.WatchReset} | Out-Null
        $w = $null
        if ($mode -ne 'off') {
            # Warm the installed wrappers, then include the same path in the measured window.
            $w = Call watch_start @{roots=@($methods.WatchA,$methods.WatchB);mode=$mode;ticks=($Ticks+30);depth=3} 30000
            Call objects_invoke @{methodId=$methods.WatchMeasureTicks;arguments=@(30)} 30000 | Out-Null
        } else {
            Call objects_invoke @{methodId=$methods.WatchMeasureTicks;arguments=@(30)} 30000 | Out-Null
        }
        $measure = Call objects_invoke @{methodId=$methods.WatchMeasureTicks;arguments=@($Ticks)} 30000
        $report = if ($w) { Call watch_report @{watchId=$w.watchId} } else { $null }
        $samples += [pscustomobject]@{mode=$mode;ticks=$Ticks;elapsedMs=$measure.result;msPerTick=($measure.result/$Ticks);report=$report}
    }
    $samples | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'samples.json') -Encoding utf8
    $samples | Select-Object mode,ticks,elapsedMs,msPerTick | Format-Table -AutoSize
    Write-Output "Observer cost evidence (fixture, elapsed time, includes post-tick output): $out"
} finally {
    if ($w) { Call watch_stop @{watchId=$w.watchId} | Out-Null }
    Call objects_write @{type='RimCarbon14.Fixture.Fixture';member='WatchEnabled';value=$false} | Out-Null
    $results | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'operations.json') -Encoding utf8
}
