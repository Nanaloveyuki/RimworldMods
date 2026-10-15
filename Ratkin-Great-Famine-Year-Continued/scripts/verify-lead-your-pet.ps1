$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $root '1.6/Source/MouseDisasterBabyLeashPolicy.cs')
foreach ($id in @('nanaloveyuki.leadyourpet.continued','lezhizhong.leadyourpet','codex.leadyourpet','NANALOVEYUKI.LEADYOURPET.CONTINUED')) {
    if (![MouseDisaster.MouseDisasterBabyLeashPolicy]::IsKnownLeadYourPetPackageId($id)) { throw "Unrecognized mod: $id" }
}
foreach ($id in @($null,'','unrelated.mod')) {
    if ([MouseDisaster.MouseDisasterBabyLeashPolicy]::IsKnownLeadYourPetPackageId($id)) { throw "Unexpected mod match: $id" }
}
if (![MouseDisaster.MouseDisasterBabyLeashPolicy]::ShouldTryRelatedAdultBabyLeash($true,$true,$true)) { throw 'Related adult leash disabled.' }
if ([MouseDisaster.MouseDisasterBabyLeashPolicy]::ShouldTryRelatedAdultBabyLeash($false,$true,$true)) { throw 'Missing mod must disable leash.' }
Write-Host 'Lead Your Pets compatibility: 9 checks passed.'
