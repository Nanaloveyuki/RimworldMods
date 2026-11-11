# Milira Job Trace

This is a temporary read-only diagnostic for the drafted Milira job interruption. It writes one line per relevant job transition to `Player.log`, including the job condition and a short caller stack. It does not cancel, replace, or repair jobs.

Enable it with the affected save, reproduce one interruption, then copy the new `[MiliraJobTrace]` lines from `Player.log`. Remove the mod after the trace is collected.
