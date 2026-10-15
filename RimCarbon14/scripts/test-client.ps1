param([string]$Profile)
$ErrorActionPreference = 'Stop'
$discovery = Get-Content (Join-Path $Profile 'RimCarbon14/discovery.json') -Raw | ConvertFrom-Json
$headers = @{ Authorization = 'Bearer ' + $discovery.token }
$results = [ordered]@{}
function Call([string]$Tool, [hashtable]$Arguments = @{}, [int]$Timeout = 10000, [string]$ExpectedError = '') {
    $status = Invoke-RestMethod ($discovery.endpoint + 'v1/status') -Headers $headers
    $body = @{ requestId = [guid]::NewGuid().ToString('N'); issuedAtUtc = [DateTime]::UtcNow; tool = $Tool; arguments = $Arguments; timeoutMs = $Timeout; session = $status.session } | ConvertTo-Json -Depth 20 -Compress
    $op = Invoke-RestMethod ($discovery.endpoint + 'v1/calls') -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    $deadline = [DateTime]::UtcNow.AddMilliseconds($Timeout + 10000)
    while ($op.state -in @('queued','running')) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Uncertain operation $($op.id)" }
        Start-Sleep -Milliseconds 50
        $op = Invoke-RestMethod ($discovery.endpoint + 'v1/operations/' + $op.id) -Headers $headers
    }
    $results["$Tool-$($results.Count)"] = $op
    if ($ExpectedError) {
        if ($op.state -ne 'failed' -or $op.error.code -ne $ExpectedError) { throw "Expected error $ExpectedError, got $($op | ConvertTo-Json -Depth 15 -Compress)" }
        return $op.error
    }
    if ($op.state -ne 'completed') { throw ($op | ConvertTo-Json -Depth 15 -Compress) }
    Write-Host "$Tool completed at tick $($op.result.stamp.tick)"
    return $op.result.data
}
