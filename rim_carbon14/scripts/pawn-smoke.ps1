param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'), [string]$Save = 'carbon-fixture')
. (Join-Path $PSScriptRoot '../../scripts/carbon/test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/pawn-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    Call game_load @{name=$Save} 180000 | Out-Null
    $pawn = (Call pawn_list @{limit=100}).items | Where-Object { $_.id -like 'Human*' -and $_.faction -and !$_.downed } | Select-Object -First 1
    if (!$pawn) { throw 'No controllable human in fixture.' }
    Call pawn_draft @{handle=$pawn.handle;drafted=$true} | Out-Null
    Call pawn_select @{handle=$pawn.handle} | Out-Null
    $gizmos = Call pawn_gizmos @{handle=$pawn.handle}
    $draft = $gizmos | Where-Object hotKey -eq Command_ColonistDraft | Select-Object -First 1
    if (!$draft) { throw 'Draft gizmo missing.' }
    Call ui_gizmo @{handle=$draft.handle} | Out-Null
    if ((Call pawn_get @{handle=$pawn.handle}).drafted) { throw 'Draft gizmo did not undraft the pawn.' }
    Call pawn_draft @{handle=$pawn.handle;drafted=$true} | Out-Null
    $x = $pawn.position.x + 2; $z = $pawn.position.z
    $menu = Call pawn_menu @{handle=$pawn.handle;x=$x;z=$z}
    $option = $menu | Where-Object { !$_.disabled } | Select-Object -First 1
    $windows = Call ui_windows
    foreach ($window in $windows | Where-Object type -eq Verse.FloatMenu) { Call ui_close @{handle=$window.handle} | Out-Null }
    Call ui_menu_choose @{handle=$option.handle} 10000 'stale_control' | Out-Null
    $menu = Call pawn_menu @{handle=$pawn.handle;x=$x;z=$z}
    $option = $menu | Where-Object { !$_.disabled } | Select-Object -First 1
    Call ui_menu_choose @{handle=$option.handle} | Out-Null
    $arrival = Call game_run_until @{tool='pawn_get';arguments=@{handle=$pawn.handle};path='position.x';equals=$x;maxTicks=2000} 60000
    if (!$arrival.matched -or $arrival.snapshot.position.z -ne $z) { throw 'Pawn did not follow the selected context-menu order.' }
    $x += 2
    Call pawn_move @{handle=$pawn.handle;x=$x;z=$z} | Out-Null
    $arrival = Call game_run_until @{tool='pawn_get';arguments=@{handle=$pawn.handle};path='position.x';equals=$x;maxTicks=2000} 60000
    if (!$arrival.matched -or $arrival.snapshot.position.z -ne $z) { throw 'Pawn did not reach the commanded cell.' }
    $wait = Call wait_until @{tool='pawn_get';arguments=@{handle=$pawn.handle};path='drafted';equals=$true}
    if (!$wait.matched) { throw 'Read-only condition wait failed.' }
    $shot = Call capture_screen
    Invoke-WebRequest ($discovery.endpoint+'v1/artifacts/'+$shot.artifactId) -Headers $headers -OutFile (Join-Path $out 'arrival.png')
    Call game_load @{name=$Save} 180000 | Out-Null
    Call pawn_get @{handle=$pawn.handle} 10000 'stale_handle' | Out-Null
    $results.passed = $true
} finally {
    $results | ConvertTo-Json -Depth 40 | Set-Content (Join-Path $out 'results.json') -Encoding utf8
    Write-Output "Evidence: $out"
}
