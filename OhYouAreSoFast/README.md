# Oh You Are So Fast

Pathfinding and AI optimizations for RimWorld 1.6 and 1.7. The package ID remains `Nanaloveyuki.OhYouAreSoFast`; Harmony is required. This project retains its original [MIT license](LICENSE).

Source, issues and future releases now live in [Nanaloveyuki/RimworldMods](https://github.com/Nanaloveyuki/RimworldMods/tree/main/OhYouAreSoFast). Clone the monorepo, then run the commands below from `OhYouAreSoFast/`.

## Version layout

- `1.6/` retains the previously deployed release binaries and translations. Those binaries were recovered from the existing installation during migration; they are not rebuilt by the current project.
- `1.7/Assemblies/` contains the .NET 10 release, built against the installed 1.7 game and Harmony 3.
- `Common/Languages/` supplies translations for 1.7. The 1.6 translations remain in place so updating the installation never needs to alter its 1.6 directory.
- `LoadFolders.xml` selects only `1.6` for 1.6, and `Common` plus `1.7` for 1.7. It does not load the package root or load shared resources twice.
- `Source/` builds 1.7 only. `tmp/` holds ignored intermediate and temporary smoke output.

## Build and deploy

Install the .NET 10 SDK. Game references are not NuGet substitutes: `Assembly-CSharp.dll` and `UnityStubs.dll` must come from the RimWorld 1.7 game root, and `0Harmony.dll` from Harmony's `1.7/Assemblies/` directory.

```sh
# WSL defaults use /mnt/e/Apps/Steam/steamapps/...
/root/.dotnet/dotnet build Source/OhYouAreSoFast.csproj -c Release

# Override the installation when needed.
dotnet build Source/OhYouAreSoFast.csproj -c Release \
  -p:RimWorldDir=/path/to/RimWorld \
  -p:HarmonyAssemblyPath=/path/to/Harmony/1.7/Assemblies/0Harmony.dll
```

`RIMWORLD_DIR` and `HARMONY_ASSEMBLY_PATH` are also supported. `-p:RimWorldVersion=1.6` is deliberately rejected to protect the preserved release. Release DLL/PDB files are versioned; intermediate files are ignored.

Run the deployment entry point from Windows PowerShell after closing the game:

```powershell
# Build with a Windows .NET 10 SDK, then deploy to the existing installation.
.\scripts\build-and-deploy.ps1

# Or deploy a release already built from WSL.
.\scripts\build-and-deploy.ps1 -SkipBuild

# Paths can be overridden independently.
.\scripts\build-and-deploy.ps1 -SkipBuild `
  -RimWorldDir 'E:\Apps\Steam\steamapps\common\RimWorld' `
  -GameModPath 'E:\Apps\Steam\steamapps\common\RimWorld\Mods\OhYouAreSoFast'
```

The script checks the target package ID, then copies only the 1.7 DLL/PDB, `Common/`, `About/About.xml`, `LoadFolders.xml` and `LICENSE`, verifying copied files with SHA-256. It never copies, replaces or deletes installed `1.6/` files, and preserves `About/PublishedFileId.txt`. For a fresh distributable package, include both preserved `1.6/` and built `1.7/` alongside the shared resources and metadata.

## 1.7 implementation

The algorithms and settings retain their existing behavior:

| Behavior | Actual 1.7 entry point |
| --- | --- |
| Movement-aware admissible local heuristic | `PathFinderMapData.ParameterizePathJob(ref PathFinderJob)`, the final shared stage used by both immediate and batched searches |
| Sparse door/provider/pawn buffer clearing | The actual `IJob.Execute` implementation resolved through `PathGridDoorsBlockedJob.GetInterfaceMap`, also reached through `ManagedJob` |
| Exact world shortest paths | `WorldPathing.FindPath(PlanetTile, PlanetTile, Caravan, Func<float, bool>)`, retaining vanilla fallback for custom terminators and using clamped `Caravan_PathFollower.CostToMove` edge costs |
| Caravan minimum movement tick | The five-argument `CaravanTicksPerMoveUtility.GetTicksPerMove(List<Pawn>, float, float, bool, StringBuilder)` overload, after backpack compatibility postfixes; shuttle behavior is preserved |
| Reentrant priority sorting | `ThinkNode_PrioritySorter.TryIssueJobPackage(Pawn, JobIssueParams)`, retaining vanilla random insertion, priority reevaluation and equal-priority ordering |
| Idle work scan throttling | `JobGiver_Work.TryIssueJobPackage(Pawn, JobIssueParams)`, retaining emergency work, non-idle pawns and small-map bypasses |
| Work-giver cache | Normal/emergency getters, priority-change and load invalidation, plus `WindowStack.TryRemove(Window, bool)` for work-tab warming |
| Save/load diagnosis | Existing startup, scribing, cross-reference, component and save/load stages; full tracing also records nested `DeepProfiler` calls |

`PathFinder.Dispose` releases map buffer tracking and `WorldPathing.Dispose` releases reusable world search state. Disabling sparse clearing forces a complete clear when re-enabled. Mod bootstrap explicitly patches this assembly so patch discovery does not depend on the calling assembly.

## Runtime acceptance

On 2026-10-15 the Release build completed with zero warnings and zero errors. A temporary standalone Windows host loaded the game's native Waylith CLR, runtime configuration and Harmony 3. All 44 patch classes installed against 44 actual targets. The smoke warmed and invoked `IJob.Execute` with 100/100 intercepted calls, entered `ManagedJob`, exercised sparse/vanilla/re-enable clearing and tracking release, called the final path parameterization and its option bypass, ran nested priority sorters, checked work-giver cache invalidation and invoked the actual caravan overload including shuttle exemption. Temporary smoke files were removed after verification.

Runtime checks must use an independent game CLR process with the game's Waylith settings and native libraries. Windows PowerShell's desktop CLR is not a substitute: an in-process second CLR can load the wrong JIT and fail Harmony's JIT version check.

Complete the following in the game before publishing:

1. Load Harmony and this mod on 1.7; check that there is no Harmony target or signature error. Confirm settings text appears once.
2. On a large map, order a very fast pawn along competing short/long routes, including doors, cost providers and pawn obstructions. Compare enabled and disabled options; repeat via immediate and scheduled path requests, and toggle sparse clearing off and back on. Remove the map and load another to cover tracking release.
3. Form a fast caravan with alternate roads and terrain routes. Compare the returned route's summed clamped movement ticks against the shortest route; test shuttle movement, a same-tile route and a custom-terminator caller. The terminator caller must retain vanilla behavior. Exact search may visit more world tiles than vanilla A* by design.
4. Enable work caching and throttling on a map above the configured size threshold. Change work priorities, disable a work type, close the work tab and save/reload; each must invalidate/warm the appropriate lists and let a newly eligible task run. Emergency work and pawns with an active job must retain their bypasses.
5. Enable light/full tracing in turn, save and load a colony, and check stage BEGIN/END pairs in the game log. Disable tracing after diagnosis.
6. Check that the deployed `1.6/` directory's SHA-256 manifest is unchanged, then use a separate 1.6 installation to confirm its preserved DLL and translations load without the 1.7 assembly.

These are acceptance operations, not a claim that they have already passed.
