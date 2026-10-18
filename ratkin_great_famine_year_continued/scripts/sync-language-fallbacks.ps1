param([switch]$Check)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$definitions = @{}
$documents = @{}
$changed = [Collections.Generic.HashSet[string]]::new()
$count = 0
foreach ($file in Get-ChildItem (Join-Path $root 'Defs') -Recurse -Filter *.xml) {
    $doc = [Xml.XmlDocument]::new()
    $doc.PreserveWhitespace = $true
    $doc.Load($file.FullName)
    $documents[$file.FullName] = $doc
    foreach ($def in $doc.DocumentElement.ChildNodes | Where-Object NodeType -eq Element) {
        $id = $def.SelectSingleNode('defName')
        if ($id) { $definitions["$($def.Name)/$($id.InnerText)"] = @{ Node = $def; Path = $file.FullName } }
    }
}
# These exported fields are authored in Languages. Defs retain generated defaults
# because DefInjected has no per-field fallback to English for other languages.
foreach ($file in Get-ChildItem (Join-Path $root 'Languages/ChineseSimplified/DefInjected') -Recurse -Filter MouseDisasterText.xml) {
    [xml]$doc = Get-Content $file.FullName -Raw
    foreach ($entry in $doc.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
        $parts = $entry.Name.Split('.')
        $definition = $definitions["$($file.Directory.Name)/$($parts[0])"]
        if (!$definition) { throw "Missing Def for $($entry.Name)" }
        $node = $definition.Node
        foreach ($part in $parts[1..($parts.Length - 1)]) {
            if ($part -match '^\d+$') {
                $items = @($node.ChildNodes | Where-Object NodeType -eq Element)
                $node = $items[[int]$part]
            } else { $node = $node.SelectSingleNode($part) }
            if (!$node) { throw "Missing Def field: $($entry.Name)" }
        }
        if ($node.InnerText -cne $entry.InnerText) {
            if ($Check) { throw "Stale Def fallback: $($entry.Name). Run scripts/sync-language-fallbacks.ps1." }
            $node.InnerText = $entry.InnerText
            [void]$changed.Add($definition.Path)
        }
        $count++
    }
}
foreach ($path in $changed) { $documents[$path].Save($path) }
# English translations are maintained independently; never replace them with Chinese.
"PASS: $count Def fallbacks; $($changed.Count) Def files updated."
