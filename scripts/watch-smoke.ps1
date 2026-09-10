param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'))
. (Join-Path $PSScriptRoot 'test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/watch-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
function Check($Actual, $Expected, [string]$Message) { if ($Actual -ne $Expected) { throw "$Message expected=$Expected actual=$Actual" } }
try {
    Call game_pause @{paused=$true} | Out-Null
    $catalog = Call objects_members @{type='RimCarbon14.Fixture.Fixture';limit=200}
    $methods = @{}
    foreach ($m in $catalog.items | Where-Object kind -eq Method) { $methods[$m.name] = $m.methodId }
    $reports = @()
    foreach ($mode in @('watch','output')) {
        Call objects_invoke @{methodId=$methods.WatchReset} | Out-Null
        $w = Call watch_start @{roots=@($methods.WatchA,$methods.WatchB,$methods.WorkerHit);mode=$mode;ticks=12;depth=3} 30000
        $watchId = $w.watchId
        Call watch_start @{roots=@($methods.WatchA);mode='watch';ticks=1} 10000 'watch_active' | Out-Null
        $initial = Call watch_snapshot @{watchId=$watchId;offset=0;limit=2}
        Check $initial.completedTicks 0 'Paused window advanced'
        Call objects_invoke @{methodId=$methods.WatchA} | Out-Null
        Call objects_invoke @{methodId=$methods.Workers;arguments=@(3)} | Out-Null
        Call objects_invoke @{methodId=$methods.WatchReset} | Out-Null
        Call game_step @{ticks=12} 30000 | Out-Null
        $r = Call watch_report @{watchId=$watchId;offset=0;limit=200}
        Check $r.state 'completed' 'Watch did not complete'
        Check $r.reason 'ticks_completed' 'Wrong stop reason'
        Check $r.completedTicks 12 'Incorrect shared tick window'
        Check $r.sites.Count $initial.sites.Count 'Unpatching changed the call-site catalogue'
        Check $r.coverageGaps.Count $initial.coverageGaps.Count 'Unpatching changed coverage metadata'
        Check $r.outsideTickRootCalls 1 'Outside-tick invocation was not separated'
        Check $r.otherThreadRootCalls 6 'Worker calls entered main-thread accounting'
        Check (Call objects_read @{type='RimCarbon14.Fixture.Fixture';member='WatchValue'}) 72 'Instrumentation changed game results'
        $a = @($r.nodes | Where-Object { $_.methodId -eq $methods.WatchA -and $null -eq $_.parentId })
        $b = @($r.nodes | Where-Object { $_.methodId -eq $methods.WatchB -and $null -eq $_.parentId })
        Check $a.Count 1 'Root A absent'; Check $a[0].calls 12 'Root A count'
        Check $b.Count 1 'Root B absent'; Check $b[0].calls 6 'Root B count'; Check $b[0].callsPerTick 0.5 'Zero-call ticks missing from divisor'
        $leaves = @($r.nodes | Where-Object methodId -eq $methods.WatchLeaf)
        Check $leaves.Count 4 'Distinct call sites/virtual path were merged'
        Check @($r.nodes | Where-Object methodId -eq $methods.WatchNever).Count 0 'Unexecuted static branch counted as dynamic call'
        $throw = @($r.nodes | Where-Object methodId -eq $methods.Throwing)
        Check $throw[0].exceptions 12 'Throwing call lost'
        $derived = $r.methods | Where-Object signature -like '*WatchDerived.*Hit*'
        if (!$derived -or !($r.nodes | Where-Object methodId -eq $derived.methodId)) { throw 'Virtual dispatch did not report the actual override.' }
        Check $r.ticks.items.Count 12 'Tick series missing'
        foreach ($node in $r.nodes) {
            if ($node.selfMs -lt 0 -or $node.selfMs -gt $node.inclusiveMs + 0.001 -or $node.tickShare -gt 1.001) { throw 'Invalid time accounting.' }
            $total = ($r.ticks.items.nodes | Where-Object nodeId -eq $node.id | Measure-Object inclusiveMs -Sum).Sum
            if ([Math]::Abs($total - $node.inclusiveMs) -gt 0.001) { throw 'Tick data disagrees with aggregate.' }
        }
        if ($mode -eq 'watch') {
            Check $r.outputPath $null 'Watch persisted output'
            $status = Invoke-RestMethod ($discovery.endpoint + 'v1/status') -Headers $headers
            Check (Test-Path (Join-Path $Profile "RimCarbon14/artifacts/$($status.instanceId)/$watchId.jsonl")) $false 'Unexpected watch file'
        } else {
            $lines = @(Get-Content -LiteralPath $r.outputPath | ForEach-Object { $_ | ConvertFrom-Json })
            Check $lines.Count 14 'JSONL must contain header, 12 ticks, final report'
            Check $lines[0].kind 'header' 'Missing output header'; Check $lines[-1].kind 'report' 'Missing final report'
            Check $lines[-1].data.completedTicks 12 'Persisted window disagrees'
            $definitions = @{}
            foreach ($line in $lines | Where-Object kind -eq 'tick') {
                foreach ($definition in $line.data.nodeDefinitions) { $definitions[[int]$definition.id] = $definition }
                foreach ($node in $line.data.nodes) {
                    if (!$definitions.ContainsKey([int]$node.nodeId)) { throw 'Persisted tick requires the final report to identify its paths.' }
                }
            }
            Check $definitions.Count $r.nodes.Count 'Incremental path definitions are incomplete'
            $download = Join-Path $out 'download.jsonl'
            Invoke-WebRequest ($discovery.endpoint.TrimEnd('/') + $r.outputRoute) -Headers $headers -OutFile $download
            Check (Get-FileHash $download).Hash (Get-FileHash $r.outputPath).Hash 'Downloaded JSONL mismatch'
        }
        $reports += $r
    }
    Call watch_snapshot @{watchId=$reports[0].watchId} 10000 'stale_watch' | Out-Null
    Call objects_write @{type='RimCarbon14.Fixture.Fixture';member='WatchCallbackEnabled';value=$true} | Out-Null
    $w = Call watch_start @{roots=@($methods.WatchCallbackRoot);mode='watch';ticks=1;depth=3}
    Call game_step @{ticks=1} | Out-Null
    $callback = Call watch_report @{watchId=$w.watchId}
    Check @($callback.nodes | Where-Object methodId -eq $methods.WatchLeaf).Count 0 'Unobserved recursive callback fabricated a direct edge'
    Check $callback.unobservedBodyEntries 1 'Missing callback coverage marker'
    Call objects_write @{type='RimCarbon14.Fixture.Fixture';member='WatchCallbackEnabled';value=$false} | Out-Null
    $w = Call watch_start @{roots=@($methods.WatchA);mode='watch';ticks=12;depth=0}
    Call game_step @{ticks=2} | Out-Null
    $expanded = Call watch_expand @{watchId=$w.watchId;depth=2} 30000
    Check $expanded.previousReport.completedTicks 2 'Expansion lost prior window'
    Check $expanded.current.completedTicks 0 'Expansion mixed measurement windows'
    if ($expanded.current.watchId -eq $w.watchId) { throw 'Expansion reused the measurement identity.' }
    $w = $expanded.current
    Call game_step @{ticks=12} | Out-Null
    $r = Call watch_report @{watchId=$w.watchId}
    Check $r.completedTicks 12 'Expanded window did not restart its tick budget'
    $w = Call watch_start @{roots=@($methods.WatchA);mode='watch';durationSeconds=1;depth=0}
    Start-Sleep -Milliseconds 1200
    $zero = Call watch_report @{watchId=$w.watchId}
    Check $zero.completedTicks 0 'Paused duration created fictitious ticks'; Check $zero.reason 'duration_elapsed' 'Paused duration never ended'
    $w = Call watch_start @{roots=@($methods.WatchA);mode='output';ticks=12;depth=1;maxNodes=1}
    Call game_step @{ticks=1} | Out-Null
    $limited = Call watch_report @{watchId=$w.watchId}
    Check $limited.reason 'node_limit' 'Node budget did not stop watch'; Check $limited.completedTicks 0 'Partial tick entered aggregates'
    Call objects_write @{type='RimCarbon14.Fixture.Fixture';member='WatchEnabled';value=$false} | Out-Null
    $pawn = Call objects_members @{type='Verse.Pawn';search='Tick';limit=50}
    $pawnRoots = @($pawn.items | Where-Object { $_.declaringType -like 'Verse.Pawn,*' -and $_.name -in @('Tick','TickInterval') } | ForEach-Object methodId)
    $w = Call watch_start @{roots=$pawnRoots;mode='output';ticks=60;depth=2} 120000
    Call game_step @{ticks=60} 30000 | Out-Null
    $pawnReport = Call watch_report @{watchId=$w.watchId}
    Check $pawnReport.completedTicks 60 'Real pawn window failed'; Check $pawnReport.reason 'ticks_completed' 'Pawn watch stopped early'
    if ($pawnReport.nodes.Count -lt 3) { throw 'Real pawn call chains were not observed.' }
    foreach ($root in $pawnRoots) {
        $patches = Call trace_patches @{methodId=$root}
        if (@($patches.prefixes + $patches.postfixes + $patches.transpilers + $patches.finalizers | Where-Object owner -eq 'nanaloveyuki.rimcarbon14.watch').Count) { throw 'Watch patches remain after completion.' }
    }
    $reports += $pawnReport
    $tickCatalog = Call objects_members @{type='Verse.TickManager';search='DoSingleTick';limit=10}
    $tickRoot = ($tickCatalog.items | Where-Object name -eq 'DoSingleTick').methodId
    $w = Call watch_start @{roots=@($tickRoot);mode='watch';ticks=2;depth=1} 30000
    Call game_step @{ticks=2} | Out-Null
    $tickReport = Call watch_report @{watchId=$w.watchId}
    Check $tickReport.completedTicks 2 'Tick boundary root failed'
    Check @($tickReport.nodes | Where-Object methodId -eq $tickRoot)[0].calls 2 'Tick root duplicated'
    if ($tickReport.nodes.Count -lt 2) { throw 'Tick root internal calls absent.' }
    $w = Call watch_start @{roots=$pawnRoots;mode='output';ticks=600;depth=0}
    Call game_step @{ticks=1} | Out-Null
    Call game_load @{name='carbon-fixture'} 180000 | Out-Null
    $changed = Call watch_report @{watchId=$w.watchId}
    Check $changed.reason 'session_changed' 'Save load did not close the window'
    Check $changed.completedTicks 1 'Ticks from different games were mixed'
    $last = Get-Content -LiteralPath $changed.outputPath -Tail 1 | ConvertFrom-Json
    Check $last.kind 'report' 'Save load did not finalize persistent output'
    Call game_pause @{paused=$true} | Out-Null
    $reports | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'reports.json') -Encoding utf8
    Write-Output "Watch acceptance: $out"
} finally {
    if ($w.watchId) { Call watch_stop @{watchId=$w.watchId} | Out-Null }
    Call objects_write @{type='RimCarbon14.Fixture.Fixture';member='WatchEnabled';value=$false} | Out-Null
    $results | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $out 'operations.json') -Encoding utf8
}
