function Get-CSharpMethod([string]$SourceText, [string]$Name) {
    $syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($SourceText).GetRoot()
    $methods = @($syntax.DescendantNodes() | Where-Object {
        $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $_.Identifier.ValueText -eq $Name
    })
    if ($methods.Count -ne 1) { throw "Expected one method named $Name, found $($methods.Count)" }
    $methods[0].ToFullString()
}
