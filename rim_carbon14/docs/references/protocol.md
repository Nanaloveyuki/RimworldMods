# HTTP and tool contract

The mod listens only on `127.0.0.1`, on an OS-selected port. Read
`<profile>/RimCarbon14/discovery.json` for endpoint, process ID, instance ID, protocol
version and token. All requests require `Authorization: Bearer <token>`.
The token is not printed to the game log. Origin-bearing browser requests are rejected.
This is a trusted local developer interface, not a sandbox against same-user processes.

## Routes

| Method and route | Result |
| --- | --- |
| `GET /v1/status` | Last main-thread heartbeat, session, frame, Tick, loading and control state |
| `GET /v1/tools` | Names, descriptions, argument schemas, scene requirements and effects |
| `POST /v1/calls` | Accepted operation snapshot, HTTP 202 |
| `GET /v1/operations/{id}` | Operation state/result/error |
| `DELETE /v1/operations/{id}` | Request cooperative cancellation |
| `GET /v1/events?after=0&limit=100` | Events, next cursor and lost-event count |
| `GET /v1/artifacts/{artifactId}` | Captured PNG bytes; use the returned artifact ID verbatim |

Call body:

```json
{
  "requestId": "client-generated-unique-id",
  "issuedAtUtc": "2026-09-10T00:00:00Z",
  "session": "session-from-status",
  "tool": "game_step",
  "arguments": { "ticks": 10 },
  "timeoutMs": 10000
}
```

Use the actual current UTC time, not the example timestamp. Session, request ID and
issue time are required. New requests must be issued within 60 seconds. Repeating
the same ID and payload returns the original operation; conflicting reuse is rejected.
Terminal operations are retained at least five minutes; total retention is capped
at 2048 operations. Never change the issue time to replay an uncertain old request.
HTTP bodies require Content-Length and are limited to 1 MiB.

## Execution

Game-object access runs on the game thread. Writes are serialized across multi-frame
operations; UI actions execute in the actual draw pass. The scheduler spends a target
3 ms per frame between operations, but cannot preempt arbitrary game methods.
`queued`, `running`, `completed`, `failed` and `cancelled` describe bridge execution,
not gameplay success. A running operation may still be waiting for a scene or another
write. `started` means the scheduler entered it, not that a gameplay effect occurred.

Results contain `data` and `stamp` (instance/session/frame/Tick/UTC). An action's
acceptance does not prove a pawn arrived or a menu closed: inspect the resulting state,
use `wait_until` or `game_run_until`, and assert the expected value.
Cancellation/deadlines stop further scheduled steps, not already-applied effects.
An invoked blocking method may hang the game thread; the companion can still report
the stale heartbeat and terminate only its owned process when explicitly requested.

Reading a new game or changing scenes invalidates object handles and pending requests
for the old session. A load operation itself is allowed to finish in the new session.
Lost HTTP acknowledgements must be resolved through operation lookup, not a second
mutation. New process instances do not remember old operations.

## Objects and tracing

Fields are read directly. Property getters run only through `objects_evaluate`;
method bodies only through explicit invocation or normal game execution. Browse
`objects_members` for declaring types, exact method IDs and parameter types.
Methods with overloaded names are never guessed. Arguments use JSON primitives,
enum names, simple value-type field objects, arrays, or `{ "handle": "..." }`.
Return/ref/out values are described separately. Boxed structs must be written back
to their parent after editing. Task objects are returned as handles, not awaited.

Handles retain objects until release or session change, capped at 4096. Collection
reads explicitly index IList/arrays; arbitrary enumerators are not run implicitly.
Pagination is bounded to 200 items and reports truncation/next offset.

Tracing selects 1..32 managed methods, for at most 300 seconds. Aggregate counters
and detailed records are separate; details are capped at 10000 records and the event
ring holds 4096 events. Detailed capture of argument fields is explicit and main-thread
only; other-thread complex values report type only. Trace records expose synchronous
parent IDs and actual operation context where present, not inferred async causality.
Missing prefix timing is reported as missing, not as a measured zero-duration call.
Stopping tracing removes only Carbon's trace patches. Native/Burst code, open generic
definitions and byref-return/pointer invocation are not supported.

For synchronized multi-root tick profiling, use `watch_start`, `watch_snapshot`,
`watch_report`, `watch_expand` and `watch_stop`. Modes `watch` and `output` select in-memory data or
additional JSONL persistence. See [watch.md](watch.md) for window boundaries, timing
denominators, call-site coverage, budgets and authenticated artifact download.

## UI and screenshots


`ui_layout` describes controls seen in a complete repaint, including deferred windows.
Use both element ID and layout version. A changed identity/geometry, disabled control,
or obscuring window rejects the action. Standard widgets can be set semantically;
custom controls can use MCP/CLI `desktop_input` or `scripts/carbon/desktop-input.ps1`（monorepo 根目录） for
Windows clicks, drags, wheel, key chords and Unicode text. This helper moves the real desktop cursor, checks the discovered game's
foreground window and converts client pixels to desktop coordinates with DPI awareness.
It runs in the companion, not HTTP. The unsuccessful `ui_input` experiment is not exposed.
Do not use desktop input concurrently with manual mouse interaction. Sending input
does not prove an effect: query game state or capture a fresh screenshot afterwards.
Concurrent desktop gestures fail instead of interleaving. Cancellation does not kill
a started helper: it finishes its bounded gesture and releases pressed buttons/keys.
Desktop input does not share the HTTP write queue; keep desktop and semantic mutations
sequential in the calling agent. The JSON test runner executes steps sequentially.

Screen rectangles and screenshot crops use top-left physical pixels. Captures include
the actual capture frame, UI scale and matching layout. Missing render frames fail
instead of returning a previous image. PNG storage is bounded to 512 MiB per instance;
archive/remove old artifacts when the limit is reached.
