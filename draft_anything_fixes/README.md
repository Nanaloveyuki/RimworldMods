# Draft Anything Fixes

This RimWorld 1.7 patch prevents Draft Anything 2.0 from deleting existing pawn trackers on undraft, which can break the colonist bar and GUI input. It also fixes two drafted-job conflicts involving Milira Race and Flight Framework.

## Behavior

Draft Anything's `RestoreOriginalState` clears `playerSettings` and `drafter` even when they belonged to the pawn before Draft Anything took control. The patch replaces those two field writes with cleanup that checks which tracker objects Draft Anything actually created during this session. Existing objects retain their identity, medical care, allowed areas, and display order. Draft Anything still undrafts the pawn, releases control, and restores its original faction.

Temporary trackers are removed on release only while they are still the same objects and vanilla no longer requires them. Settings needed by player pawns, player-hosted guests, or holding platforms remain; draft controllers needed by player humanlikes and colony mechs remain. Trackers supplied by another system or loaded without runtime ownership evidence are preserved. This means a previously controlled pawn loaded from a save can retain extra trackers after release; the patch does not guess which serialized objects were temporary. Tracking uses weak pawn keys and adds no save fields.

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
[DraftAnythingFixes] Tracker cleanup preserves existing player settings and draft controllers.
```

Run the tracker regression checks against the installed Draft Anything DLL:

```sh
/root/.dotnet/dotnet run --project draft_anything_fixes/tests/TrackerCleanupChecks/TrackerCleanupChecks.csproj -c Release
```

An optional positional argument selects a different upstream DLL. The checks first reproduce the unpatched deletion, install the production Harmony patch, and exercise repeated control cycles, original settings, temporary enemy trackers, mixed ownership, replacement objects, loaded trackers, and components now required by vanilla. They run against the actual game and upstream assemblies without starting the graphical game.

Disabling Draft Anything was confirmed in game to eliminate the affected save's UI failure. The new tracker patch still needs an in-game check with both Mods enabled: load the pre-failure save, draft and undraft the affected pawn repeatedly, and verify the colonist bar, medical settings, movement orders, and keyboard input. Drafted movement with the earlier flight compatibility fixes was already verified in the affected save.

If upstream changes its method names or cleanup IL, the patch logs an installation failure. The tracker patch expects one null assignment to each of the two fields in `RestoreOriginalState`.
