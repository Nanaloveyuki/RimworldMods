param([string]$Profile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'local/smoke'), [Nullable[float]]$UiScale)
. (Join-Path $PSScriptRoot '../../scripts/carbon/test-client.ps1') -Profile $Profile
$out = Join-Path $Profile ('RimCarbon14/fixture-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $out -Force | Out-Null
function Check($Actual, $Expected, [string]$Message) { if ($Actual -ne $Expected) { throw "$Message expected=$Expected actual=$Actual" } }
try {
    $originalScale = Call objects_evaluate @{type='Verse.Prefs';member='UIScale'}
    if ($null -ne $UiScale) { Call objects_write @{type='Verse.Prefs';member='UIScale';value=[float]$UiScale} | Out-Null }
    $catalog = Call objects_members @{type='RimCarbon14.Fixture.Fixture';limit=200}
    Call objects_members @{type='UnityEngine.Event';limit=1} | Out-Null
    $methods = @{}
    foreach ($m in $catalog.items | Where-Object kind -eq Method) { $methods[$m.name] = $m.methodId }
    $root = Call objects_read @{type='RimCarbon14.Fixture.Fixture';member='Root'}
    foreach ($field in @('GetterCalls','Clicks','OtherClicks','WorkerCalls')) { Call objects_write @{handle=$root.handle;member=$field;value=0} | Out-Null }
    Call objects_members @{handle=$root.handle} | Out-Null
    Check (Call objects_read @{handle=$root.handle;member='GetterCalls'}) 0 'Browsing ran getter'
    Call objects_evaluate @{handle=$root.handle;member='CountedGetter'} | Out-Null
    Check (Call objects_read @{handle=$root.handle;member='GetterCalls'}) 1 'Explicit getter did not execute'
    Call objects_invoke @{methodId=$methods.OpenWindow} | Out-Null
    $layout = Call ui_layout
    $buttons = @($layout.elements | Where-Object { $_.kind -eq 'button' -and $_.label -eq 'Duplicate' } | Sort-Object { $_.screenRect.x })
    Check $buttons.Count 2 'Duplicate controls not independently observed'
    Call ui_action @{elementId=$buttons[0].id;layoutVersion=$layout.layoutVersion} | Out-Null
    Check (Call objects_read @{handle=$root.handle;member='Clicks'}) 1 'Wrong button effect'
    Check (Call objects_read @{handle=$root.handle;member='OtherClicks'}) 0 'Other same-label button activated'
    $layout = Call ui_layout
    $disabled = $layout.elements | Where-Object { $_.kind -eq 'button' -and $_.label -eq 'Disabled action' } | Select-Object -First 1
    if (!$disabled) { throw 'Disabled fixture control missing.' }
    Call ui_action @{elementId=$disabled.id;layoutVersion=$layout.layoutVersion} 10000 'disabled_control' | Out-Null
    Call ui_action @{elementId=$buttons[0].id;layoutVersion=($layout.layoutVersion+100)} 10000 'stale_layout' | Out-Null
    foreach ($kind in @('checkbox','text','slider')) {
        $layout = Call ui_layout
        $element = $layout.elements | Where-Object { $_.kind -eq $kind -and $_.surface -like '*ProbeWindow*' } | Select-Object -First 1
        if (!$element) { throw "Missing $kind fixture control" }
        $value = switch ($kind) { checkbox { $true }; text { 'agent-edited' }; slider { 0.75 } }
        Call ui_action @{elementId=$element.id;layoutVersion=$layout.layoutVersion;value=$value} | Out-Null
        $field = switch ($kind) { checkbox { 'Checked' }; text { 'Text' }; slider { 'Slider' } }
        Check (Call objects_read @{handle=$root.handle;member=$field}) $value "$kind value mismatch"
    }
    $layout = Call ui_layout
    Check @($layout.elements | Where-Object label -eq 'Bottom action').Count 0 'Off-screen scroll control leaked into visible layout'
    $scroll = $layout.elements | Where-Object { $_.kind -eq 'scroll' -and $_.surface -like '*ProbeWindow*' } | Select-Object -First 1
    Call ui_action @{elementId=$scroll.id;layoutVersion=$layout.layoutVersion;value=@{y=770;x=0}} | Out-Null
    $layout = Call ui_layout
    $bottom = $layout.elements | Where-Object label -eq 'Bottom action' | Select-Object -First 1
    if (!$bottom) { throw 'Scrolled bottom control not visible.' }
    $shot = Call capture_screen @{elementId=$bottom.id;layoutVersion=$layout.layoutVersion}
    Invoke-WebRequest ($discovery.endpoint+'v1/artifacts/'+$shot.artifactId) -Headers $headers -OutFile (Join-Path $out 'crop.png')
    Call ui_action @{elementId=$bottom.id;layoutVersion=$layout.layoutVersion} | Out-Null
    Check (Call objects_read @{handle=$root.handle;member='Clicks'}) 2 'Bottom button not activated'
    $layout = Call ui_layout
    $other = $layout.elements | Where-Object { $_.kind -eq 'button' -and $_.label -eq 'Duplicate' } | Sort-Object { $_.screenRect.x } | Select-Object -Last 1
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -X ([int]($other.screenRect.x+$other.screenRect.width/2)) -Y ([int]($other.screenRect.y+$other.screenRect.height/2)) | Out-Null
    Start-Sleep -Milliseconds 200
    Call objects_read @{handle=$root.handle;member='InputEvents'} | Out-Null
    Check (Call objects_read @{handle=$root.handle;member='OtherClicks'}) 1 'Desktop mouse click had no game effect'
    $slider = $layout.elements | Where-Object { $_.kind -eq 'slider' -and $_.surface -like '*ProbeWindow*' } | Select-Object -First 1
    $r = $slider.screenRect
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -Kind drag -X ([int]($r.x+$r.width*0.75)) -Y ([int]($r.y+$r.height/2)) -EndX ([int]($r.x+$r.width*0.30)) -EndY ([int]($r.y+$r.height/2)) | Out-Null
    Start-Sleep -Milliseconds 200
    $actual = Call objects_read @{handle=$root.handle;member='Slider'}
    if ($actual -lt 0.2 -or $actual -gt 0.4) { throw "Desktop drag did not move slider: $actual" }
    $layout = Call ui_layout
    $scroll = $layout.elements | Where-Object { $_.kind -eq 'scroll' -and $_.surface -like '*ProbeWindow*' } | Select-Object -First 1
    $r = $scroll.screenRect
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -Kind scroll -X ([int]($r.x+$r.width/2)) -Y ([int]($r.y+$r.height/2)) -WheelDelta 120 | Out-Null
    Start-Sleep -Milliseconds 200
    $layout = Call ui_layout
    $scrolled = $layout.elements | Where-Object id -eq $scroll.id | Select-Object -First 1
    if ($scrolled.value.y -ge $scroll.value.y) { throw 'Desktop wheel did not scroll content upward.' }
    $field = $layout.elements | Where-Object { $_.kind -eq 'text' -and $_.surface -like '*ProbeWindow*' } | Select-Object -First 1
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -Kind click -X ([int]($field.screenRect.x+30)) -Y ([int]($field.screenRect.y+$field.screenRect.height/2)) | Out-Null
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -Kind key -Key Ctrl+A | Out-Null
    & (Join-Path $PSScriptRoot '../../scripts/carbon/desktop-input.ps1') -Profile $Profile -Kind text -Text 'desktop-keyboard' | Out-Null
    Start-Sleep -Milliseconds 200
    Check (Call objects_read @{handle=$root.handle;member='Text'}) 'desktop-keyboard' 'Desktop keyboard did not edit game field'
    $shot = Call capture_screen
    Invoke-WebRequest ($discovery.endpoint+'v1/artifacts/'+$shot.artifactId) -Headers $headers -OutFile (Join-Path $out 'desktop-input.png')
    Call trace_start @{methodIds=@($methods.Recurse,$methods.Throwing,$methods.Skipped,$methods.WorkerHit);detailed=$true;stack=$true;durationSeconds=60} | Out-Null
    Check (Call objects_invoke @{methodId=$methods.Recurse;arguments=@(3)}).result 3 'Recursive return mismatch'
    Call objects_invoke @{methodId=$methods.Throwing} 10000 'execution_error' | Out-Null
    Call objects_invoke @{methodId=$methods.InstallSkip} | Out-Null
    Check (Call objects_invoke @{methodId=$methods.Skipped;arguments=@(5)}).result 41 'Original was not skipped'
    Call objects_invoke @{methodId=$methods.Workers;arguments=@(20)} | Out-Null
    Call objects_invoke @{methodId=$methods.RemoveSkip} | Out-Null
    $trace = Call trace_stop
    Check ($trace.methods | Where-Object methodId -eq $methods.Recurse).calls 4 'Recursive trace count'
    Check ($trace.methods | Where-Object methodId -eq $methods.Throwing).exceptions 1 'Exception trace count'
    Check ($trace.methods | Where-Object methodId -eq $methods.Skipped).skipped 1 'Skipped trace count'
    Check ($trace.methods | Where-Object methodId -eq $methods.WorkerHit).calls 40 'Worker trace count'
    $patches = Call trace_patches @{methodId=$methods.Recurse}
    Check @($patches.prefixes | Where-Object owner -eq 'nanaloveyuki.rimcarbon14.trace').Count 0 'Trace patch survived stop'
    $results.passed = $true
} finally {
    if ($null -ne $UiScale -and $null -ne $originalScale) { Call objects_write @{type='Verse.Prefs';member='UIScale';value=$originalScale} | Out-Null }
    $results | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $out 'results.json') -Encoding utf8
    $events=Invoke-RestMethod ($discovery.endpoint+'v1/events?after=0&limit=500') -Headers $headers
    $events | ConvertTo-Json -Depth 50 | Set-Content (Join-Path $out 'events.json') -Encoding utf8
    Write-Output "Evidence: $out"
}
