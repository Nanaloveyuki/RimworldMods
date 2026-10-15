# Implementation and acceptance

Target: RimWorld 1.6, Windows x64, agent-only. Independent of RimBridgeServer,
GABS and DPA. In-process net472 HTTP bridge; out-of-process self-contained
net10.0 stdio MCP companion and test runner. Game objects stay on the game thread.

Ordered deliverables:

1. HTTP discovery/authentication, bounded main-thread operations, MCP and screenshots.
2. Game/Pawn snapshots, session-scoped handles, explicit reflection read/write/invoke.
3. Semantic Pawn/UI actions, Windows desktop input, tick stepping.
4. Bounded opt-in method profiling/tracing, Harmony metadata, logs.
5. Isolated save-data profiles, launch/restart/reconnect and evidence-producing tests.

Do not equate builds or test doubles with real-game acceptance. Record the actual
game version, active mod set and separately verified scenarios in validation.md.
Never replay an uncertain mutation after a restart. No source-level breakpoints,
runtime C# compiler, dashboard, native/Burst tracing or promised
cross-thread causal reconstruction.

User direction on 2026-09-10 replaces the ineffective virtual IMGUI input experiment
with explicit PowerShell/Win32 desktop input. This moves the real cursor and requires
the game window to be foreground; it is separate from the Mod's semantic HTTP tools.
