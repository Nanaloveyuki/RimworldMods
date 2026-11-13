# Draft Anything Fixes

This RimWorld 1.7 patch fixes Draft Anything 2.0's tracker deletion on undraft and its injected order branch in Milira's constant think tree. Milira Race's native drafted flight remains enabled.

## Behavior

Draft Anything's `RestoreOriginalState` clears `playerSettings` and `drafter` even when they belonged to the pawn before Draft Anything took control. The patch replaces those two field writes with cleanup that checks which tracker objects Draft Anything actually created during this session. Existing objects retain their identity, medical care, allowed areas, and display order. Draft Anything still undrafts the pawn, releases control, and restores its original faction.

Temporary trackers are removed on release only while they are still the same objects and vanilla no longer requires them. Settings needed by player pawns, player-hosted guests, or holding platforms remain; draft controllers needed by player humanlikes and colony mechs remain. Trackers supplied by another system or loaded without runtime ownership evidence are preserved. This means a previously controlled pawn loaded from a save can retain extra trackers after release; the patch does not guess which serialized objects were temporary. Tracking uses weak pawn keys and adds no save fields.

When Draft Anything is installed, the patch removes its dynamically inserted controllable-pawn order branch from the `MiliraConstant` think tree. Drafted player orders continue to use the humanlike main think tree; Draft Anything's behavior in other think trees is unchanged.

Milira's `Pawn_FlightTracker.Notify_JobStarted` prefix remains installed and runs its original logic: eligible Milira fly while drafted or targeting an enemy, subject to `CompFlightControl.CanFly`. Its flight modes, hunger threshold, wing requirements, food cost, and `Milira_InFlight` effects remain managed by Milira. This patch makes no changes to Flight Framework.

Version 0.1.2 removes the earlier flight-prefix suppression. No save data is rewritten. Restart the game after upgrading so the previous version's Harmony patches are unloaded.

## Build

```sh
/root/.dotnet/dotnet build draft_anything_fixes/Source/MiliraFlightJobFix.csproj -c Release
```

The project uses the local RimWorld 1.7 `Assembly-CSharp.dll`, Unity stubs, and Harmony 3 references through the shared build properties.

## Verification

With Draft Anything installed, the assembly should log the compatibility installation and tracker protection messages. The branch-removal message appears once the injected node is present:

```text
[MiliraFlightJobFix] Draft Anything compatibility cleanup installed for Milira constant trees.
[MiliraFlightJobFix] Removed Draft Anything's drafted-order branch from MiliraConstant; the humanlike main tree still handles player orders.
[DraftAnythingFixes] Tracker cleanup preserves existing player settings and draft controllers.
```

Run the tracker regression checks against the installed Draft Anything DLL:

```sh
/root/.dotnet/dotnet run --project draft_anything_fixes/tests/TrackerCleanupChecks/TrackerCleanupChecks.csproj -c Release
```

An optional positional argument selects a different upstream DLL. The checks first reproduce the unpatched deletion, install the production Harmony patch, and exercise repeated control cycles, original settings, temporary enemy trackers, mixed ownership, replacement objects, loaded trackers, and components now required by vanilla. They run against the actual game and upstream assemblies without starting the graphical game.

Disabling Draft Anything was confirmed in game to eliminate the affected save's UI failure. Version 0.1.2 still needs an in-game check with both Mods enabled: load the pre-failure save, draft and undraft the affected pawn repeatedly, and verify flight, uninterrupted movement orders, the colonist bar, medical settings, and keyboard input. Exercise Milira's always-fly, movement-only, and never-fly modes. Drafted movement was previously verified with flight suppression enabled; that result does not cover restored native flight.

If upstream changes its method names or cleanup IL, the patch logs an installation failure. The tracker patch expects one null assignment to each of the two fields in `RestoreOriginalState`.
