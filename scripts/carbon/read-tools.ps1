param(
    [Parameter(Mandatory = $true)][string]$DiscoveryFile,
    [ValidateRange(1000, 60000)][int]$TimeoutMs = 10000
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$token = $null
try {
    $discovery = Get-Content -LiteralPath $DiscoveryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if (($discovery.protocolVersion -isnot [int] -and $discovery.protocolVersion -isnot [long]) -or $discovery.protocolVersion -ne 1) {
        throw 'Expected Carbon bridge protocol 1'
    }
    $raw = [string]$discovery.endpoint
    if ($raw -notmatch '^http://127\.0\.0\.1:([0-9]+)(/)?$') {
        throw 'Discovery endpoint must be explicit loopback HTTP without extra URL parts'
    }
    $port = [int]$Matches[1]
    if ($port -lt 1 -or $port -gt 65535) { throw 'Discovery port is outside its valid range' }
    $token = $discovery.token
    if ($token -isnot [string] -or [string]::IsNullOrEmpty($token) -or $token -match '\s') {
        throw 'Discovery authentication token is missing or malformed'
    }
    $request = [System.Net.HttpWebRequest]::Create("http://127.0.0.1:$port/v1/tools")
    $request.Proxy = $null
    $request.AllowAutoRedirect = $false
    $request.Timeout = $TimeoutMs
    $request.ReadWriteTimeout = $TimeoutMs
    $request.Accept = 'application/json'
    $request.Headers['Authorization'] = 'Bearer ' + $token
    $response = $request.GetResponse()
    try {
        if ([int]$response.StatusCode -ne 200) { throw 'Carbon tool discovery did not return HTTP 200; redirects are not followed' }
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream(), [System.Text.Encoding]::UTF8)
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $null = ConvertFrom-Json -InputObject $content
        [Console]::Out.WriteLine($content)
    } finally { $response.Dispose() }
    exit 0
} catch {
    $message = $_.Exception.Message
    if ($token -is [string] -and $token.Length -gt 0) { $message = $message.Replace($token, '[REDACTED]') }
    $failure = @{ error = 'windows_tools_discovery_failed'; message = $message } | ConvertTo-Json -Compress
    [Console]::Error.WriteLine($failure)
    exit 1
}
