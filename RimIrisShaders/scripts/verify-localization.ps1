$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$options = Get-Content (Join-Path $root 'ShaderPacks/Moonlight/options.json') -Raw | ConvertFrom-Json
$required = @('Moonlight_Name', 'Moonlight_Description')
$required += @($options.options | ForEach-Object { 'Moonlight_' + $_.id })
foreach ($language in @('English', 'ChineseSimplified')) {
    [xml]$xml = [xml]([System.IO.File]::ReadAllText((Join-Path $root "Languages/$language/Keyed/Moonlight.xml")))
    $nodes = @($xml.LanguageData.ChildNodes | Where-Object NodeType -eq 'Element')
    $keys = @($nodes | ForEach-Object Name)
    if (@($keys | Select-Object -Unique).Count -ne $keys.Count) { throw "Duplicate keys: $language" }
    if (Compare-Object ($required | Sort-Object) ($keys | Sort-Object)) { throw "Translation keys differ: $language" }
    foreach ($node in $nodes) {
        if ([string]::IsNullOrWhiteSpace($node.InnerText)) { throw "Empty translation: $language/$($node.Name)" }
        if ($node.InnerText -match 'bundle|shader|uniform|DLL|_RimIris') { throw "Technical UI text: $language/$($node.Name)" }
    }
    Write-Output "$language`: $($keys.Count) translation keys passed."
}
