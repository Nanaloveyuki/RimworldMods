# Save Your Storage and Tick Every Minute

A RimWorld 1.6 performance compatibility mod for bursty storage and tick workloads.

## Current optimizations

- Maintains a reference-identity membership index for global `ListerThings` instances, replacing repeated linear `AllThings.Contains` scans. Region listers are intentionally excluded. If the index cannot prove that it mirrors the source list, the query falls back to the original method.
- Detects Outerrealm Tech without a compile-time dependency and spreads lease-expiry identity-anchor returns across game ticks. Calls to the same entry are coalesced and live lease/reservation state is checked immediately before execution.
- Pre-routes Outerrealm identity books before RimWorld enumerates the live book list, preventing anchor moves from invalidating that enumeration.
- Restores vanilla `CanReserveStack` recheck semantics for Outerrealm projections by excluding the current pawn's matching reservations. This prevents repeated reservation checks, including Smart Medicine's tending workflow, from ending otherwise valid jobs.
- Revalidates Outerrealm's custom construction-delivery jobs before they leave the work giver, so reservation contention is treated as a normal no-job result instead of failing immediately after `StartJob` and logging errors.
- Normalizes the intentionally suppressed power-inspection text on UF Recipes super workbenches before Turn It On and Off - RePowered processes it. This prevents its regular-expression postfix from receiving a null input while preserving UF Recipes' custom power-status line.

All changes are runtime Harmony patches. RimWorld `Thing`, `Map`, lister, region, and reservation operations remain on the main thread.

## Build

```powershell
dotnet build Source\SaveYourStorageAndTickEveryMinute.csproj -c Release
dotnet run --project Tests\SchedulerHarness\SchedulerHarness.csproj -c Release
.\scripts\verify.ps1
```

The release assembly is written to `1.6\Assemblies`.

When Dubs Performance Analyzer is active, `Analyzer.xml` adds a `SaveYourStorage`
entry under its modder profiling category. Profiling remains disabled until that
entry is explicitly enabled in the Analyzer UI.

## Deploy

```powershell
.\scripts\build-and-deploy.ps1
```

Pass `-GameModPath` when RimWorld is installed elsewhere. The script verifies the target package ID before replacing an existing installation and compares the copied assembly and language-file hashes.

## Verify Outerrealm Tech compatibility

```powershell
.\scripts\verify-outerrealm-contract.ps1
```

The focused verifier reads the installed `FullyAutomaticOmniCrafter.dll` and checks the
exact private method, property, and state-field contract used by the optional
Harmony adapter. Pass `-OuterrealmAssemblyPath` or `-RimWorldManagedPath` for a
non-default installation.

`verify.ps1` runs the build, scheduler harness, XML checks, optional-dependency
check, and this installed-DLL contract check together. Use
`-SkipOuterrealmContract` on machines without Outerrealm Tech.

## Compatibility boundary

The Outerrealm Tech adapter is enabled only when the expected assembly, methods, and anchor-state fields are present. A signature mismatch leaves the original Mod behavior untouched and writes a warning to the RimWorld log.

The UF Recipes / Turn It On and Off bridge is enabled only when both expected runtime types are present in their expected assemblies. It changes only a null `CompPowerTrader` inspect string on `UFRecipes.Building_SuperWorkbench` instances to an empty string, ordered after UF Recipes and before Turn It On and Off.
