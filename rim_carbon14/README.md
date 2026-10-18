# RimCarbon14

An agent-facing runtime inspection, debugging and testing bridge for RimWorld 1.7,
with the existing compiled 1.6 runtime preserved, on Windows x64. No dashboard.
No dependency on RimBridgeServer, GABS or DPA.

The mod exposes loopback HTTP inside the running game. The included self-contained
`RimCarbon14.Agent.exe` exposes stdio MCP, manages isolated game processes, and runs
JSON test scenarios. The companion does not require .NET, Python or Node installed.

## Installation

1. Install Harmony and copy the release's `RimCarbon14` directory into `RimWorld/Mods`.
2. Enable RimCarbon14 for the profile being inspected, or prepare an isolated profile.
3. Configure your MCP client using [docs/mcp-config.json](docs/references/mcp-config.json), adjusting
   the executable, profile and game paths. The profile is a **save-data folder**, not a save file.

The game must have a rendered window for UI capture. Hidden/minimized players may
continue simulation without drawing usable frames. The bridge does not change the
game language; isolated minimal profiles inherit the user's preferences, including
language, and change only background execution and test-window dimensions.

RimWorld 1.7 uses .NET 10, `UnityStubs.dll`, Harmony 3 and `RimWorld.exe`.
`LoadFolders.xml` selects only `1.7/Assemblies` for 1.7 and only the unchanged
`1.6/Assemblies` for 1.6. Do not merge these DLLs into root `Assemblies`.
The companion selects the executable and log location from the installation's
`Version.txt`: `<profile>/Logs/Player.log` on 1.7, `<profile>/Player.log` on 1.6.
1.7 launch settings come from the isolated profile's preferences; obsolete Unity
screen/log command-line switches are not sent. Starting an isolated 1.7 profile
sets its rendering preferences to windowed 1280x720, preserving language and UI scale.

## Isolated Test Profile

```powershell
$agent = 'C:/Games/RimWorld/Mods/RimCarbon14/Tools/win-x64/RimCarbon14.Agent.exe'
$common = @('--profile', 'C:/RimCarbon14/test-profile', '--game-root', 'C:/Games/RimWorld')
& $agent prepare @common --minimal
& $agent start @common
& $agent call game_new_test @common --timeout-ms 180000
& $agent call game_save @common --args '{"name":"carbon-fixture"}'
& $agent run 'C:/Games/RimWorld/Mods/RimCarbon14/docs/references/smoke-scenario.json' @common
& $agent stop @common
```

`prepare --source <save-data-folder>` copies configuration without changing the source
or reordering its mods; it adds RimCarbon14 when absent. `--minimal` selects the
base game, Harmony and RimCarbon14 (RimWorld may also activate installed expansions).
Existing profiles and named saves are not overwritten. Copy a chosen `.rws` into
the test profile's `Saves` directory to test an existing save.

## Tool Groups

- `game_*`: state, load/new test/save, pause, exact tick stepping, conditional advancement.
- `pawn_*`: detailed state, selection, drafting, movement, jobs, context menus and gizmos.
- `objects_*`: type/member discovery, private fields, explicit getters, writes and exact method invocation.
- `ui_*`: real drawn controls, versioned actions and window/tab/menu controls.
- `capture_screen`: frame-end PNG capture with element/rectangle cropping and matching layout metadata.
- `trace_*`: explicit managed-method profiling, optional arguments/results/stacks, Harmony patch metadata.
- `watch_*`: synchronized multi-root tick call chains, ms/tick and share data; in-memory `watch` or persistent JSONL `output`. See [docs/watch.md](docs/references/watch.md).
- MCP `process_*`, `profile_prepare`, `bridge_*`: lifecycle, discovery, events, operation recovery.

Use `tools` or HTTP `/v1/tools` for the exact live schemas. MCP publishes game tools
when connected; `bridge_call` is always available for clients that cache the initial
tool list. Use `bridge_call.timeoutMs` for operations longer than the default 10 seconds.
Runtime snapshots come from game objects, not an external model of gameplay.

For desktop input, use the MCP tool `desktop_input` or run `Tools/win-x64/desktop-input.ps1` (source:
`../scripts/carbon/desktop-input.ps1` from this module, or `scripts/carbon/desktop-input.ps1` from the monorepo root) with Windows PowerShell 5.1 or PowerShell 7:

```powershell
./scripts/carbon/desktop-input.ps1 -Profile C:/RimCarbon14/test-profile -Kind click -X 500 -Y 300
./scripts/carbon/desktop-input.ps1 -Profile C:/RimCarbon14/test-profile -Kind drag -X 500 -Y 300 -EndX 650 -EndY 300
./scripts/carbon/desktop-input.ps1 -Profile C:/RimCarbon14/test-profile -Kind scroll -X 500 -Y 300 -WheelDelta -120
./scripts/carbon/desktop-input.ps1 -Profile C:/RimCarbon14/test-profile -Kind key -Key Ctrl+A
./scripts/carbon/desktop-input.ps1 -Profile C:/RimCarbon14/test-profile -Kind text -Text 'agent input'
```

Coordinates are physical pixels inside the game client, matching `ui_layout` and
screenshots. This moves the real cursor and requires game focus; do not operate the
mouse simultaneously. Input runs in the companion, not the in-game HTTP server. Verify effects using
game-state reads or a new screenshot, not the helper's `sent` acknowledgement.

## Build and Verification

Requires .NET 10 SDK, an installed RimWorld 1.7, Harmony 3 for 1.7, and PowerShell 7.

```powershell
$env:RIMWORLD_DIR = 'C:/Games/RimWorld'
$harmony = 'C:/Games/Steam/steamapps/workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll'
dotnet build RimCarbon14.slnx -c Release "-p:RimWorldDir=$env:RIMWORLD_DIR" "-p:HarmonyPath=$harmony"
dotnet test tests/RimCarbon14.Tests/RimCarbon14.Tests.csproj -c Release
./scripts/build.ps1 -RimWorldDir $env:RIMWORLD_DIR -GameVersion 1.7 -HarmonyPath $harmony
```

Runtime compilation outputs remain in `1.7/Assemblies`. Shared staging uses root
`artifacts/rim_carbon14/release`; companion packaging uses `artifacts/rim_carbon14/package` and places its zip under the same module artifact directory. The package includes both isolated runtimes.
The default Harmony path follows the usual Steam workshop layout; override
`-HarmonyPath` for another installation. `-GameVersion 1.6` packages the preserved
legacy runtime without rebuilding it and requires an already-built 1.7 runtime.
Current mod/core/fixture source targets net10.0 only; the 1.6 binaries are not rebuilt.

Deployment uses the shared entry from the monorepo root:
`bash scripts/deploy.sh rim_carbon14` or Windows `python scripts/python/deploy.py rim_carbon14`.
`--build-only` stages without writing to the game; `--target` selects the destination.
The specialized `scripts/build.ps1` only builds/packages; `-SkipAgent` is a development-only shortcut, not a complete release build. Game
and Harmony assemblies are referenced locally and never redistributed. The 1.7-only
development fixture is not included in the release; its installer has explicit
1.7 load routing and does not overwrite an existing root legacy fixture DLL.

The historical rendered-game evidence in [validation status](docs/references/validation.md)
belongs to 1.6, not 1.7. A successful 1.7 build or real-assembly Harmony smoke does
not establish Windows desktop input, GPU readback, pawn behavior, save round trips,
MCP recovery, or complete tick-watch acceptance. Run the documented live/fixture
checks in a separate 1.7 profile before claiming those behaviors were verified.

See [protocol](docs/references/protocol.md), [test scenarios](docs/references/testing.md), and
[validation status](docs/references/validation.md) for contracts, evidence and remaining limits.
