param(
    [string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'),
    [string]$GameRoot = 'D:/Appdata/Steam/steamapps/common/RimWorld',
    [string]$Save = 'carbon-fixture'
)
. (Join-Path $PSScriptRoot '../../scripts/carbon/test-client.ps1') -Profile $Profile
$root = Split-Path $PSScriptRoot -Parent
$agent = Join-Path (Split-Path $root -Parent) 'artifacts/rim_carbon14/package/Tools/win-x64/RimCarbon14.Agent.exe'
$catalog = Call objects_members @{type='RimCarbon14.Fixture.Fixture';limit=200}
$crash = $catalog.items | Where-Object { $_.kind -eq 'Method' -and $_.name -eq 'CrashOnce' } | Select-Object -First 1
if (!$crash) { throw 'Deploy the current development fixture first.' }
$scenario = @{
    name='abnormal-process-exit-recovery'
    recovery=@{maxCrashRestarts=1;resetSave=$Save}
    steps=@(
        @{id='crash-once';tool='objects_invoke';arguments=@{methodId=$crash.methodId;arguments=@([guid]::NewGuid().ToString('N'))};assert=@(@{path='result.data.result';equals=$false})},
        @{id='ready';tool='game_state';assert=@(@{path='result.data.status.ready';equals=$true})},
        @{id='capture';tool='capture_screen';assert=@(@{path='result.data.artifactId';exists=$true})}
    )
}
$file = Join-Path $Profile 'RimCarbon14/recovery-scenario.json'
$scenario | ConvertTo-Json -Depth 15 | Set-Content $file -Encoding utf8
$output = & $agent run $file --profile $Profile --game-root $GameRoot
if ($LASTEXITCODE -ne 0) { throw "Recovery scenario failed: $output" }
$run = $output | ConvertFrom-Json
if (!$run.passed -or !$run.recovered -or $run.attempts.Count -ne 2 -or !$run.attempts[0].processExitedDuringStep) { throw "Recovery did not observe and recover the abnormal exit: $output" }
$run | ConvertTo-Json -Depth 8
