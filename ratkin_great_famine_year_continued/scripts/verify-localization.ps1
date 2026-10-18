$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Language([string]$language) {
    $entries = @{}
    $base = Join-Path $root "Languages/$language"
    foreach ($file in Get-ChildItem $base -Recurse -Filter *.xml) {
        [xml]$doc = Get-Content $file.FullName -Raw
        $relative = [IO.Path]::GetRelativePath($base, $file.DirectoryName)
        foreach ($node in $doc.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
            $key = "$relative/$($node.Name)"
            if ($entries.ContainsKey($key)) { throw "Duplicate $language key: $key" }
            if ([string]::IsNullOrWhiteSpace($node.InnerText)) { throw "Empty $language text: $key" }
            $entries[$key] = $node.InnerText
        }
    }
    return $entries
}
$zh = Read-Language ChineseSimplified
$en = Read-Language English
$diff = @(Compare-Object @($zh.Keys) @($en.Keys))
if ($diff.Count) { throw "Translation key mismatch: $($diff | Out-String)" }
foreach ($key in $zh.Keys) {
    if ($en[$key] -match '[\u3400-\u9fff]') { throw "Chinese text in English: $key" }
    $a = @([regex]::Matches($zh[$key], '\{[^{}]+\}|\[[A-Za-z][A-Za-z0-9_]*\]') | ForEach-Object Value | Sort-Object -Unique)
    $b = @([regex]::Matches($en[$key], '\{[^{}]+\}|\[[A-Za-z][A-Za-z0-9_]*\]') | ForEach-Object Value | Sort-Object -Unique)
    if (@(Compare-Object $a $b).Count) { throw "Placeholder mismatch: $key" }
}
"PASS: $($en.Count) English entries, matching Chinese keys and placeholders; no Chinese fallback text."
