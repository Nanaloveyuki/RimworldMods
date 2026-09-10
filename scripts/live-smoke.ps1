param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'), [string]$LoadSave = 'carbon-fixture')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/live-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    if ($LoadSave) { Call game_load @{name=$LoadSave} 180000 | Out-Null }
    Call game_pause | Out-Null
    $list = Call pawn_list @{limit=100}
    $pawn = $list.items | Where-Object { $_.id -like 'Human*' -and $_.faction } | Select-Object -First 1
    if (!$pawn) { throw 'No controllable human pawn in fixture.' }
    $detail = Call pawn_get @{handle=$pawn.handle}
    if (!$detail.needs) { throw 'Pawn needs missing.' }
    Call pawn_select @{handle=$pawn.handle} | Out-Null
    Call pawn_draft @{handle=$pawn.handle; drafted=$true} | Out-Null
    $read = Call objects_read @{handle=$pawn.handle; member='thingIDNumber'}
    Call objects_write @{handle=$pawn.handle; member='thingIDNumber';value=$read} | Out-Null
    $methods = Call objects_members @{type='Verse.TickManager'; search='DoSingleTick'}
    $method = $methods.items | Where-Object { $_.name -eq 'DoSingleTick' } | Select-Object -First 1
    Call trace_start @{methodIds=@($method.methodId); detailed=$true; stack=$true; durationSeconds=60} | Out-Null
    $stepped = Call game_step @{ticks=8}
    if ($stepped.delta -ne 8) { throw 'Tick step delta mismatch.' }
    $trace = Call trace_stop
    if ($trace.methods[0].calls -ne 8) { throw 'Trace call count mismatch.' }
    Call ui_tab @{def='Work'} | Out-Null
    $layout = Call ui_layout
    if ($layout.elements.Count -lt 1) { throw 'No UI elements observed.' }
    $shot = Call capture_screen
    Invoke-WebRequest ($discovery.endpoint + 'v1/artifacts/' + $shot.artifactId) -Headers $headers -OutFile (Join-Path $out 'game.png')
    $windows = Call ui_windows
    foreach ($window in $windows | Where-Object type -eq 'RimWorld.MainTabWindow_Work') { Call ui_close @{handle=$window.handle} | Out-Null }
    $layout2 = Call ui_layout
    $check = $layout2.elements | Where-Object kind -eq checkbox | Select-Object -First 1
    if ($check) { Call ui_action @{elementId=$check.id;layoutVersion=$layout2.layoutVersion;value=(!$check.value)} | Out-Null }
    $results['passed'] = $true
} finally {
    $results | ConvertTo-Json -Depth 40 | Set-Content (Join-Path $out 'results.json') -Encoding utf8
    Write-Output "Evidence: $out"
}
