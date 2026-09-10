# Validation status

Local functional acceptance for 0.1.0 was performed on 2026-09-10 using Windows x64,
RimWorld 1.6.4871, Unity 2022.3.35f1 and .NET SDK 10.0.401. This is a development
bridge, not a claim of compatibility with every third-party mod or old save.

The isolated profile loaded Harmony, Core, Royalty, Ideology, Biotech, Anomaly,
Odyssey, RimCarbon14 and the development fixture. The fixture is not distributed.
Original and isolated preferences remain Simplified Chinese. No Unity editor was needed.

## Requirement evidence

Evidence paths below are relative to the ignored `local/smoke/RimCarbon14` directory.

| Requirement | Observed evidence |
| --- | --- |
| Independent Mod and loopback HTTP | Release build against installed game DLLs; authenticated HTTP, rejected anonymous/browser requests; no reference to RimBridgeServer or DPA |
| Self-contained MCP companion | Published executable initialized through stdio, listed live tools and returned actual PNG image blocks; no separate runtime host used |
| Main-thread operations and reflection | Queue/deduplication/cancellation/schema unit tests; fixture field writes, explicit getter count, exact invocation and Unity forwarded-type lookup |
| Pawn actions and conditions | `pawn-smoke-20260910T104513`: draft gizmo, closed-menu rejection, menu move, direct move, actual arrival, read-only wait and old-handle rejection after load |
| UI and screenshots | `fixture-smoke-20260910T103918` at 100%, `fixture-smoke-20260910T110013` at 125%: duplicate controls, disabled/stale targets, checkbox/text/slider, clipped scroll, pixel crops, real mouse drag/wheel and keyboard editing |
| Profiling and traces | Same fixture: recursion 4 calls, exception 1, skipped original 1, worker calls 40; trace patches removed after stop |
| Real game stepping | `live-smoke-20260910T104749`: exactly 8 Tick calls and 8 corresponding trace records; real Work tab and screenshot |
| Process lifecycle and runner | `runs/20260910T105734454-1abb96b0`: load/inspect/step/capture/restart/reload, with named step results separate from `_evidence` metadata |
| Abnormal exit recovery | `runs/20260910T105944147-b9e4fdda`: test-only forced abnormal exit, preserved failing attempt, new process, reset save and successful second attempt |
| Runtime diagnostics | `diagnostics-20260910T104756`: about 60 FPS; paused TPS 0, running TPS about 60; UI observation on/off recorded separately |

The automated suite passed 22 tests with `CARBON_LIVE_PROFILE`, `RIMWORLD_DIR` and
`CARBON_DESKTOP_TESTS=1`, including published MCP click/Ctrl+A/text effects read back
from the fixture. Without the live environment, the two live tests are skipped.

## Tick Watch Acceptance

The data-only watch/output extension was verified on the same isolated profile.
Evidence is under `local/smoke/RimCarbon14/watch-smoke-20260910T115430`:

- Shared 12-tick windows: roots ran 12 and 6 times, with 0.5 calls/tick for the
  latter. Four paths to the same leaf stayed separate; the untaken branch was absent.
- Recursive calls, exceptions, ref arguments and inherited virtual dispatch preserved
  fixture results. Six worker calls and one outside-tick root call were excluded from
  main-thread tick accounting. Unobserved recursive callbacks did not fabricate edges.
- `watch` created no artifact. `output` produced a header, 12 self-describing tick
  records and a final report; authenticated download matched the file hash. Incremental
  path definitions let completed records be interpreted without the final report.
- Expansion returned a new window/ID with reset counters and the previous report.
  Paused duration windows ended with zero ticks. Node limits discarded partial ticks.
- Real `Pawn.Tick`/`TickInterval` ran for 60 ticks. `DoSingleTick` itself also worked
  as a root with internal call-site data. Stop preserved the call-site/gap catalogue
  and removed only Carbon watch patches. Save load finalized output without mixing games.

The suite now contains 29 passing tests with live MCP/auth enabled, including discovery
of all five watch tools. Desktop gestures were not rerun for this data-only extension;
the earlier desktop evidence above remains separate. The watch scope and remaining
instrumentation limits are documented in [watch.md](watch.md).

`watch-overhead-20260910T115449` compared six warmed 180-tick fixture runs in
off/watch/output/output/watch/off order, reloading the same test save between runs.
Elapsed ms/tick ranges were off 0.58..0.78, watch 0.62..0.74, output 0.78..1.01.
The baseline variation is too large to infer a precise watch overhead or a speedup.
These are diagnostic fixture timings including post-tick output work, not a general
performance guarantee. Normal watch/output operation never reloads a save.

## Limits

- Desktop input intentionally moves the real cursor and requires an interactive,
  foreground game window. It is not an invisible/background input channel. Use the
  semantic tools when possible and serialize desktop and HTTP mutations in the agent.
- Pixel checks covered 1280x720 and UI scales 1.0/1.25, not every monitor/DPI combination.
- The tested Unity Mono allocation counter always returns zero, even for a known
  allocation. The tool therefore reports `allocationCounterSupported=false` and
  `mainThreadAllocatedBytes=null`, not a false claim of zero allocation. Memory delta
  is not allocation volume. These samples are not a no-mod overhead benchmark.
- Reflection can execute arbitrary game/mod code. A blocking invocation cannot be
  preempted or rolled back. Traces cover selected managed synchronous methods, not
  native/Burst code, source breakpoints or cross-thread/async causal reconstruction.
- Recovery is explicit, bounded whole-scenario replay from a named save after an
  observed owned-process exit. It is not per-operation retry or automatic hang killing.
  Logs already written to disk survive; in-memory events after the last retrieval may not.
- No dashboard, WebSocket transport, runtime C# compiler or arbitrary old-save
  compatibility guarantee is included. The interface is agent-facing HTTP/MCP.
