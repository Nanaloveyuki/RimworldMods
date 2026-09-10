# Test scenarios and evidence

`RimCarbon14.Agent run <scenario.json> --profile <folder> --game-root <game>`
executes ordered tools, checks assertions and writes a timestamped evidence directory
under `<profile>/RimCarbon14/runs`. Outputs include results.json, junit.xml, game log,
available events and PNGs. A failed step stops the scenario; failure screenshot errors
are retained instead of silently ignored. Exit code is zero only for passing runs.

The packaged [smoke scenario](smoke-scenario.json) loads `carbon-fixture`, checks Pawn
state and precise ticks, captures an image, restarts the process, and loads the same
fixture again. Prepare the profile/save first using README commands.

Each step has a unique `id`, `tool`, optional `arguments`, `timeoutMs`, and `assert`.
IDs must be valid filenames; `_evidence` and the generated recovery-step IDs are
reserved. Run metadata lives under `_evidence`, without replacing named step results.
Assertions select a JSON path and support `exists`, `equals`, `min` and `max`.
An argument object containing only `$ref` resolves a typed value from prior results,
for example `{ "handle": { "$ref": "pawns.result.data.items[0].handle" } }`.
Tests do not evaluate arbitrary script expressions or silently resume failed mutations.

Optional `"recovery": { "maxCrashRestarts": 1, "resetSave": "carbon-fixture" }`
explicitly opts into whole-scenario replay after an owned process exits during a failed
step. The limit is 0..3 (default 0). Every attempt starts/attaches to the owned profile
and loads the named save before running any scenario steps. No individual uncertain
operation is retried. Assertion failures, live-but-unresponsive games and manual stop
steps do not trigger recovery. Use scenarios whose external effects tolerate replay.
Failed attempts retain their own logs, results and failing JUnit files in `attempt-N`;
the root `run.json` and JUnit report the final result and attempt count.

## Development checks

```powershell
dotnet test tests/RimCarbon14.Tests/RimCarbon14.Tests.csproj
./scripts/live-smoke.ps1 -Profile C:/RimCarbon14/test-profile -LoadSave carbon-fixture
```

For explicit real-game MCP/auth checks, set `CARBON_LIVE_PROFILE` and `RIMWORLD_DIR`
before running the tests. Without them, those tests are reported as skipped.
They launch the **published self-contained companion**, perform MCP initialization and
tool discovery, retrieve actual image content and check unauthenticated/browser requests.
Set `CARBON_DESKTOP_TESTS=1` as well, with an open ProbeWindow fixture, to exercise
the published MCP desktop click, Ctrl+A and text tool against its actual text field.

For deterministic UI/trace fixture acceptance, stop the isolated game, run
`scripts/install-fixture.ps1 -RimWorldDir <game> -Profile <profile>`, then restart and
run `scripts/fixture-smoke.ps1 -Profile <profile>`. This extra mod is test-only, never
included in the release. It verifies actual fields behind duplicate-label buttons,
checkbox/text/slider controls, clipped scrolling, exceptions, recursion, worker threads
and Harmony skipping/cleanup. Use a clean process for an independent fixture run.
This script also clicks, drags and scrolls using the real Windows mouse. It brings
the test game to the foreground; do not use the mouse while it runs.

`diagnostics_sample` reports FPS/TPS, main-thread allocated bytes when the Mono API
is available, GC counts and frame intervals. These include bridge overhead. Turning
off `ui_capture` disables observation, not the whole mod; label that baseline accurately.
Do not claim compatibility or speedup from builds alone, or generalize the fixture's
coverage to every third-party mod.

Additional real-game checks:

```powershell
./scripts/fixture-smoke.ps1 -Profile C:/RimCarbon14/test-profile -UiScale 1.25
./scripts/pawn-smoke.ps1 -Profile C:/RimCarbon14/test-profile
./scripts/diagnostics-smoke.ps1 -Profile C:/RimCarbon14/test-profile
./scripts/watch-smoke.ps1 -Profile C:/RimCarbon14/test-profile
./scripts/watch-overhead.ps1 -Profile C:/RimCarbon14/test-profile
./scripts/recovery-smoke.ps1 -Profile C:/RimCarbon14/test-profile -GameRoot C:/Games/RimWorld
```

The recovery fixture deliberately terminates only its own test game process once,
then checks a new process and full scenario replay. Use an isolated profile.

The watch fixture verifies shared windows, distinct dynamic paths, inherited virtual
dispatch, recursion, ref arguments, exceptions, worker exclusion, zero-call ticks,
expansion into a new window, limits, JSONL persistence/download and real Pawn tick
chains. It does not move the mouse or change language. The overhead script reloads
`carbon-fixture` and compares off/watch/output in reversed order with warmup; results
are fixture elapsed-time evidence, not a universal overhead bound.
