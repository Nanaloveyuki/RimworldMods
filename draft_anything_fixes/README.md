# Draft Anything Fixes

This 1.7 compatibility patch keeps Milira Race and Flight Framework installed while fixing two drafted-job conflicts that can interrupt Milira pawns' orders.

## Behavior

The patch removes Milira's exact prefix from `Pawn_FlightTracker.Notify_JobStarted` when it is already installed. It also guards Milira's prefix method so it returns `true` if another mod installs it later. The normal Harmony chain then continues to the remaining flight patches and vanilla method. Flight Framework remains loaded and its controller continues to work for pawns that have one.

When Draft Anything is installed, the patch removes its dynamically inserted controllable-pawn order branch from the `MiliraConstant` think tree. Drafted player orders continue to use the humanlike main think tree; Draft Anything's behavior in other think trees is unchanged.

No save data is rewritten. The patch does not remove `CompFlightControl`, `Milira_InFlight`, or Flight Framework components. It changes drafted Milira behavior from “every current job is forced into the custom flying branch” to the remaining flight patches and vanilla job-def rules. Jobs that request flight can still fly; ordinary movement and work jobs are no longer forcibly marked as flying by the Milira prefix.

## Build

```sh
/root/.dotnet/dotnet build draft_anything_fixes/Source/MiliraFlightJobFix.csproj -c Release
```

The project uses the local RimWorld 1.7 `Assembly-CSharp.dll`, Unity stubs, and Harmony 3 references through the shared build properties.

## Verification

The assembly should log the flight-prefix message and, when Draft Anything is installed, a compatibility cleanup message:

```text
[MiliraFlightJobFix] Removed Milira's flight-job override; vanilla and remaining compatibility patches handle jobs.
[MiliraFlightJobFix] Guarded Milira's flight-job override; vanilla and remaining compatibility patches handle jobs.
[MiliraFlightJobFix] Removed Draft Anything's drafted-order branch from MiliraConstant; the humanlike main tree still handles player orders.
```

If Milira changes the patch class or method name, the mod logs a warning and leaves the game running without installing a partial patch. Drafted movement was verified in the affected save with the compatibility patch enabled.
