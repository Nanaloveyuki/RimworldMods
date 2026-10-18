$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
Add-Type -Path (Join-Path $root '1.6/Source/MouseDisasterAbandonedDeliveryPolicy.cs')
$checks = 0
function Check([bool]$ok, [string]$message) {
    $script:checks++
    if (!$ok) { throw $message }
}
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::IsDropoffSatisfied($true)) 'near food cell must count as arrived'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::IsDropoffSatisfied($false)) 'far from food cell must not count as arrived'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::DropoffRadius($true, $true, $true) -eq 3) 'adult must not treat the whole relief area as dropoff'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::DropoffRadius($false, $false, $true) -eq 3) 'child dropoff radius changed outside relief'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::DropoffRadius($false, $true, $false) -eq 3) 'child outside relief used the expanded radius'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::DropoffRadius($false, $true, $true) -eq 8) 'child in relief dropoff radius changed'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldWaitAtDropoff()) 'mother still waits at a dropoff cell'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldAdultLeaveWhenChildrenAreOnMap($false, 1)) 'mother stayed after children were on the map'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldAdultLeaveWhenChildrenAreOnMap($true, 1)) 'mother left while still carrying a child'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldAdultLeaveWhenChildrenAreOnMap($false, 0)) 'mother left with no children on the map'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldGiveChildSelfGoto($false, $false)) 'children still walk to a dropoff cell'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldUseVanillaCarryDelivery($false, $false)) 'mother still carries children to a dropoff'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldCarryUndeliveredChild($false, $false, $true)) 'downed child is still hauled to dropoff'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceAdultLeaveAfterArrivalStall($true, [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::AdultArrivalStallTimeoutTicks)) 'stall timeout ignored'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceAdultLeaveAfterArrivalStall($false, [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::AdultArrivalStallTimeoutTicks)) 'unarrived adult timed out'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceAdultLeaveAfterApproachStall($false, [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::AdultArrivalStallTimeoutTicks)) 'unarrived approach stall ignored'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceAdultLeaveAfterApproachStall($true, [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::AdultArrivalStallTimeoutTicks)) 'arrived adult used approach stall'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceDismissMobileNonInfant($false, $true, 18)) 'disabled setting dismissed adult'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceDismissMobileNonInfant($true, $true, 2.9)) 'infant dismissed'
Check (-not [MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceDismissMobileNonInfant($true, $false, 18)) 'immobile adult dismissed'
Check ([MouseDisaster.MouseDisasterAbandonedDeliveryPolicy]::ShouldForceDismissMobileNonInfant($true, $true, 3)) 'mobile non-infant not dismissed'
$delivery = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.AbandonedDelivery.cs') -Raw
Check ($delivery -match 'ShouldAdultLeaveWhenChildrenAreOnMap') 'mother still waits for a dropoff cell'
Check ($delivery -match 'children-on-map') 'immediate leave is not logged'
Check ($delivery -match 'TryDropCarriedAbandonedChild') 'carried children are not dropped before leaving'
Check ($delivery -match 'startedTick') 'approach timer is missing'
Check ($delivery -match 'ShouldForceAdultLeaveAfterApproachStall') 'unarrived mother still has no timeout'
Check ($delivery -match 'LogAbandonedDeliveryWait') 'waiting mother still has no log'
Check ($delivery -match 'LordJob_MouseDisasterDeparture') 'exit still uses TravelAndExit to map center'
Check ($delivery -match 'forceDismissMobileNonInfantEventPawns') 'developer force-dismiss is unused'
$compat = Get-Content (Join-Path $root '1.6/Source/MapComponent_MouseDisasterCompat.cs') -Raw
Check ($compat -match 'ForceDismissMobileNonInfantEventPawns\(map, onlyWaiting: true\)') 'waiting-only force-dismiss is not ticked'
$mod = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
Check ($mod -match 'forceDismissMobileNonInfantEventPawns') 'developer checkbox missing'
$settings = Get-Content (Join-Path $root '1.6/Source/MouseDisasterSettings.cs') -Raw
Check ($settings -match 'forceDismissMobileNonInfantEventPawns = false') 'force-dismiss default is not off'
Check ($settings -match 'Scribe_Values\.Look\(ref forceDismissMobileNonInfantEventPawns') 'force-dismiss is not saved'
$state = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.State.cs') -Raw
Check ($state -match 'Scribe_Values\.Look\(ref startedTick') 'approach timer is not saved'
Write-Host "PASS: $checks abandoned-delivery dropoff, stall and force-dismiss assertions."
