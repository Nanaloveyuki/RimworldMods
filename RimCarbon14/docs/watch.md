# Tick Call-Chain Data

`watch_*` is a data-only profiler. It does not create windows, overlays or charts.
It is independent of the older explicit-method `trace_*` recorder.

## Modes

- `mode: "watch"`: keep the current window in memory. No watch artifact is written.
  Query snapshots while running and the report after completion. The next start
  replaces the retained window; retrieve it first. Process exit loses it.
- `mode: "output"`: the same observation plus a UTF-8 JSONL artifact. A header,
  each completed tick, and the final report are flushed in that order. `outputPath`
  names the local file; authenticated GET of `outputRoute` downloads it. Previously
  written files survive subsequent windows, save loads and process restarts.
  Download routes belong to one process instance; after a restart use the saved
  local `outputPath` for an older instance's artifact.
  After a crash a file without a final `report` is incomplete, not a completed run.
  Tick records include incremental `nodeDefinitions` before their first use, so
  completed lines still identify their paths without that final report.
  Live downloads can end with a partial line; only consume complete JSONL lines.

Game logs and ordinary bridge operation results exist independently of these modes.
Output flushes OS buffers, not a guarantee against power loss. Output files are not
automatically deleted; the caller owns retention of completed artifacts.

## Start and Read

Discover exact root IDs using `objects_members`, checking `declaringType` as well
as `name` for inherited/overloaded methods. Then call:

```json
{
  "roots": ["<Pawn.Tick methodId>", "<Pawn.TickInterval methodId>"],
  "mode": "watch",
  "ticks": 600,
  "depth": 2
}
```

Pass the returned `watchId` to `watch_snapshot`, `watch_report` or `watch_stop`.
Snapshot/report have the same data contract; an active report is provisional.
Add `offset: 0, limit: 100` for a page of per-tick data (maximum page size 200).
Snapshots include only completed ticks, even while later ticks are in flight.
Use the existing HTTP tool endpoint or MCP game tools; there is no separate channel.

All roots start at the same next `TickManager.DoSingleTick` entry. `ticks` counts
actual invocations, not a subtraction of game tick IDs. It includes ticks with no
selected calls. `game_step` can drive a paused game explicitly; starting a watch
does not change pause state, speed, language or other game preferences.

Use `durationSeconds` instead of `ticks` for a wall-clock window. The deadline begins
after patch installation; a tick entered before it is allowed to finish. A paused
game can complete a duration window with zero ticks. Tick-count windows have a
300-second wall-clock ceiling, including time spent paused.

Only one window is retained/active. Multiple roots share it; a second active start
fails with `watch_active` rather than silently cancelling another observer. Wrong
or replaced IDs fail with `stale_watch`. `watch_expand(watchId, depth)` closes the
old window and starts a **new** one with a greater shared depth and otherwise the
same configuration. It returns `previousReport` and `current`; use the new
`current.watchId` thereafter. The tick/time budget restarts. Output mode finalizes
the old file and creates a new one. Do not merge measurements across depths.

## Interpretation

`methods` and `sites` are instrumentation metadata and can include untaken branches.
`nodes` are observed paths keyed by parent, call site and runtime target method.
Two calls to the same method at different sites or under different roots stay
separate. `parentId` and `siteId` reconstruct the tree. Root site IDs are negative;
other site IDs index `sites`. Virtual/interface dispatch resolves the actual receiver
target; the site separately retains its declared target and `call`/`callvirt` opcode.
Numeric node/site IDs are local to a window, not stable cross-window identities.

| Field | Definition |
| --- | --- |
| `inclusiveMsPerTick` | Node elapsed time including children / all completed ticks |
| `selfMsPerTick` | Elapsed time minus observed immediate children / completed ticks |
| `tickShare`, `selfTickShare` | Inclusive/self time / same-window measured tick time, as ratios |
| `parentShare` | Node inclusive time / its path parent's inclusive time |
| `callsPerTick` | Calls / completed ticks, including zero-call ticks |
| `p95MsPerTick` | Nearest-rank 95th percentile of per-tick totals, including zeros |
| `maxMsPerTick` | Maximum per-tick total, not maximum individual invocation |

Undefined denominators return null. Ancestor inclusive times overlap: summing them
double-counts work. Self time is **residual observed time**, including unexpanded code
and observer overhead, not an assertion that every internal call was instrumented.
Counts/time are accumulated only when the entire tick is committed. Limit-truncated
ticks are discarded as a unit and the report identifies the stop reason.

## Coverage and Cost

The scope is synchronous main-thread calls inside complete ticks, rooted at the
outermost selected entry. Overlapping roots are not independent duplicate traces:
a selected method nested under another root is represented by its observed call
path, not counted again as a top-level root. Calls outside ticks and root calls on
other threads are counted separately but excluded from tick-time accounting.
Callbacks through unexpanded bodies are not inferred as direct caller/callee edges.
UI/rendering, native/Burst internals and cross-thread/async causal reconstruction are
not provided by this tick profiler.

Call-site wrappers use `try/finally`, preserve original call/callvirt semantics,
return values, ref/out arguments and exceptions. Discovery expands managed game/mod
bodies and loaded virtual implementations, bounded by depth and method/site budgets.
Constructors, indirect calls, prefixed calls, generic targets, byref returns,
pointer/byref-like signatures and framework internals are not expanded or wrapped
where unsupported. `coverageGaps`, `omittedCoverageGaps`, `bodyExpanded` and
`depthLimitedCalls` expose limits. `unobservedBodyEntries` counts expanded bodies
entered inside a root without a matching observed call site (including depth cuts
and callbacks); their interiors are suppressed rather than attached to the wrong
invocation. `bodyExpanded` means call-site instrumentation
was installed, not complete coverage. Assemblies/types loaded after discovery are
not recursively added to the same measurement window.

Other Harmony patches are part of the running game being measured, not individually
attributed patch timings. Stop/session change/exit removes only Carbon's watch patches.
Instrumentation installation happens outside the recorded interval. Avoid changing
other patches or running another profiler during a comparison.

Defaults: depth 2 (0..8), roots 1..16, methods 128 (up to 512), call sites 4096
(up to 16384), path nodes 2048 (up to 8192), node/tick samples 100000 (up to 1000000).
All windows stop at 10000 ticks or 300 seconds at the latest. Node/sample exhaustion
stops the window rather than silently retaining biased aggregates.

Elapsed time uses `Stopwatch`, not CPU accounting. Hot-path collection avoids JSON,
stack-string and argument capture. Tick serialization/file writes occur after the
tick timestamp is taken, but still slow overall execution. JIT, scheduling and GC
also affect measurements. Compare equivalent warmed runs with observation off/on;
do not interpret the measured times as zero-overhead ground truth.
