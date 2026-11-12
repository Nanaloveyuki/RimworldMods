# UIRoot GUI Trace

This temporary RimWorld 1.7 diagnostic Mod records Harmony callbacks on `UIRoot_Play.UIRootOnGUI` only when an exception is observed. It captures colonist bar cache lengths when recaching or drawing fails and checks failed recaches for null entries, missing pawn trackers, and invalid corpse references. For Milira pawns, it also records changes to `playerSettings` around faction changes, drafted-state changes, dynamic component updates, spawn/despawn, save/load, and pawn ticks, including active Harmony patches on the method where the state was observed. Remove it after collecting the relevant `Player.log` entries.

The log identifies the throwing patch owner and method when an individual callback fails. If the exception comes from the combined UI root method, it includes the recent callback sequence and colonist bar phase snapshots for that GUI frame.
