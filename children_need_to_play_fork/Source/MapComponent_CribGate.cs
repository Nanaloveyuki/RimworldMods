using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Tracks crib gate open/closed state per building across the map.
    /// 
    /// The crib gate allows crawling toddlers to escape when open (giving them
    /// the same escape permissions as walking toddlers). When closed, crawlers
    /// remain confined to the crib.
    /// 
    /// This component uses a Dictionary to track Building_Bed → gate state, enabling
    /// save/load persistence via ExposeData. Each crib maintains its own independent
    /// gate state.
    /// 
    /// Persistence:
    /// - The Map class automatically serializes all MapComponents in Map.components
    ///   using Scribe_Collections.Look(ref components, "components", LookMode.Deep)
    /// - This component's ExposeData() is called during both Save and Load operations
    /// - After loading, FinalizeInit() validates all Building_Bed references to ensure
    ///   they still point to valid cribs on the map (handles edge cases where cribs
    ///   may have been destroyed between saves)
    /// </summary>
    public class MapComponent_CribGate : MapComponent
    {
        /// <summary>
        /// Dictionary mapping each crib (Building_Bed with defName "Crib") to its gate state.
        /// Key = Building_Bed (the crib) - must be a valid crib building on the map
        /// Value = bool (true = gate open, false = gate closed)
        /// 
        /// Default is false (gate closed) for any crib not explicitly tracked.
        /// 
        /// SERIALIZATION NOTE: This dictionary is serialized with:
        /// - Key: LookMode.Reference (Building_Bed is a Thing with thingIDNumber)
        /// - Value: LookMode.Value (bool is a primitive type)
        /// After loading, this is validated in FinalizeInit() to remove any invalid references.
        /// </summary>
        private Dictionary<Building_Bed, bool> cribGateStates = new Dictionary<Building_Bed, bool>();

        /// <summary>
        /// Transient working lists used by Scribe during save/load cycles.
        /// Only populated during Scribe operations; cleared after loading completes.
        /// Required because we use mixed LookModes (Reference for keys, Value for values).
        /// </summary>
        private List<Building_Bed> keysWorkingList;
        private List<bool> valuesWorkingList;

        /// <summary>
        /// Constructor required by MapComponent system.
        /// </summary>
        public MapComponent_CribGate(Map map) : base(map) { }

        /// <summary>
        /// Checks if a crib's gate is currently open.
        /// 
        /// Returns false (gate closed) by default for cribs not in the tracking dictionary.
        /// This is safe because:
        /// 1. New cribs start with gate closed (expected behavior)
        /// 2. Destroyed/removed cribs are cleaned up in CleanupDestroyedCribs()
        /// 3. Loading a save initializes gates from ExposeData
        /// 
        /// NOTE: This method has defensive checks (null, destroyed, non-crib) that appear
        /// redundant with caller validation in the Harmony patches. These are intentionally
        /// kept for safety — if any caller forgets to validate, this method still returns
        /// a safe default (false = gate closed = toddler cannot escape). This does NOT
        /// create a debug-logging dependency — the checks themselves are valid regardless
        /// of whether logging is enabled.
        /// 
        /// [DEBUG] When debugLogging is ON in settings, logs state queries.
        /// </summary>
        /// <param name="crib">The crib building to check. Can be null (returns false).</param>
        /// <returns>True if gate is open, false if closed or crib is null/not a crib.</returns>
        public bool IsCribGateOpen(Building_Bed crib)
        {
            bool debug = TPE_Mod.Settings?.debugLogging ?? false;

            if (crib == null)
            {
                if (debug) Log.Message("[TPE Crib Gate] IsCribGateOpen called with null crib");
                return false;
            }
            if (crib.Destroyed)
            {
                if (debug) Log.Message($"[TPE Crib Gate] IsCribGateOpen called with destroyed crib at {crib.Position}");
                return false;
            }
            if (crib.def.defName != "Crib")
            {
                if (debug) Log.Message($"[TPE Crib Gate] IsCribGateOpen called with non-crib building ({crib.def.defName}) at {crib.Position}");
                return false;
            }

            // Return tracked state, or false (closed) if not tracked yet
            bool isOpen = cribGateStates.TryGetValue(crib, out bool state) && state;
            if (debug) Log.Message($"[TPE Crib Gate] Crib at {crib.Position} gate state: {(isOpen ? "OPEN" : "CLOSED")} (tracked: {cribGateStates.ContainsKey(crib)})");
            return isOpen;
        }

        /// <summary>
        /// Toggles a crib's gate between open and closed.
        /// 
        /// Automatically adds the crib to tracking if not already present.
        /// Returns the new state.
        /// 
        /// [DEBUG] When debugLogging is ON in settings, logs toggle events.
        /// </summary>
        /// <param name="crib">The crib building to toggle. Does nothing if null or not a crib.</param>
        /// <returns>The new gate state (true = open, false = closed), or false if crib is invalid.</returns>
        public bool ToggleCribGate(Building_Bed crib)
        {
            bool debug = TPE_Mod.Settings?.debugLogging ?? false;

            if (crib == null)
            {
                if (debug) Log.Message("[TPE Crib Gate] ToggleCribGate called with null crib");
                return false;
            }
            if (crib.Destroyed)
            {
                if (debug) Log.Message($"[TPE Crib Gate] ToggleCribGate called with destroyed crib at {crib.Position}");
                return false;
            }
            if (crib.def.defName != "Crib")
            {
                if (debug) Log.Message($"[TPE Crib Gate] ToggleCribGate called with non-crib building ({crib.def.defName}) at {crib.Position}");
                return false;
            }

            // Get current state (default false if not in dict)
            bool currentState = cribGateStates.TryGetValue(crib, out bool existing) ? existing : false;

            // Toggle and store
            bool newState = !currentState;
            cribGateStates[crib] = newState;

            if (debug)
            {
                Log.Message($"[TPE Crib Gate] Toggled crib at {crib.Position}: {currentState} -> {newState}");
            }

            return newState;
        }

        /// <summary>
        /// Cleanup tick: Remove entries for destroyed or despawned cribs.
        /// 
        /// Called periodically (not every tick) to prevent memory leaks from old cribs.
        /// </summary>
        public override void MapComponentTick()
        {
            // Clean up destroyed cribs every 500 ticks (~8 seconds at 60 t/s)
            if (Find.TickManager.TicksGame % 500 != 0) return;

            CleanupDestroyedCribs();
        }

        /// <summary>
        /// Removes entries for cribs that no longer exist on the map.
        /// Handles both null keys (from failed deserialization) and destroyed/displaced cribs.
        /// </summary>
        private void CleanupDestroyedCribs()
        {
            List<Building_Bed> cribsToRemove = new List<Building_Bed>();

            foreach (var kvp in cribGateStates)
            {
                Building_Bed crib = kvp.Key;
                
                // Check for null first to avoid accessing properties on null references
                bool isInvalid = false;
                if (crib == null)
                {
                    // Null key — can occur if reference deserialization failed
                    isInvalid = true;
                }
                else if (crib.Destroyed || crib.Map != map)
                {
                    // Destroyed or no longer on this map
                    isInvalid = true;
                }

                if (isInvalid)
                {
                    cribsToRemove.Add(crib);
                }
            }

            // Remove all invalid entries from the dictionary
            foreach (Building_Bed crib in cribsToRemove)
            {
                cribGateStates.Remove(crib);
            }
        }

        /// <summary>
        /// Called after the map and all components are deserialized from a save file.
        /// Validates and cleans up the deserialized crib gate states, removing any
        /// Building_Bed references that are no longer valid (e.g., destroyed cribs,
        /// null references from failed deserialization).
        /// </summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Defensive: ensure dictionary is never null after deserialization.
            // This handles the case where a save had no cribGateStates or deserialization failed.
            if (cribGateStates == null)
            {
                cribGateStates = new Dictionary<Building_Bed, bool>();
                return; // Nothing to validate if dict is empty
            }

            // Validate and clean up deserialized references.
            // After Scribe_Collections.Look() loads from a save file, the dictionary may contain
            // invalid keys if:
            // - A crib was destroyed between saves
            // - A reference failed to deserialize (null key)
            // - A crib was removed from the map
            List<Building_Bed> invalidCribs = new List<Building_Bed>();
            foreach (var kvp in cribGateStates)
            {
                Building_Bed crib = kvp.Key;
                
                // Check for null first to avoid accessing properties on null references
                bool isInvalid = false;
                if (crib == null)
                {
                    // Null key — can occur if reference deserialization failed
                    isInvalid = true;
                }
                else if (crib.Destroyed || crib.Map != map || crib.def.defName != "Crib")
                {
                    // Destroyed, not on this map, or not a valid crib building
                    isInvalid = true;
                }

                if (isInvalid)
                {
                    invalidCribs.Add(crib);
                }
            }

            // Remove all invalid entries from the dictionary
            foreach (Building_Bed invalidCrib in invalidCribs)
            {
                cribGateStates.Remove(invalidCrib);
            }

            // Log cleanup summary if debug enabled and entries were removed
            bool debug = TPE_Mod.Settings?.debugLogging ?? false;
            if (debug && invalidCribs.Count > 0)
            {
                Log.Message($"[TPE Crib Gate] FinalizeInit: Removed {invalidCribs.Count} invalid crib entries from deserialized state");
            }
        }

        /// <summary>
        /// Saves crib gate states to savegame data.
        /// 
        /// Uses Verse.Scribe to serialize the dictionary. Each crib is saved as
        /// a reference + bool pair, allowing save/load to persist gate states correctly.
        /// 
        /// CRITICAL: When LookModes differ (Reference for keys, Value for values), Scribe requires
        /// explicit working lists to handle cross-reference resolution. The 5-parameter overload
        /// manages the lists automatically during save/load cycles.
        /// 
        /// SERIALIZATION PROCESS:
        /// - Saving: Dictionary is extracted into working lists, then serialized.
        /// - Loading (vars): Empty dict created, working lists allocated for deserialization.
        /// - Loading (cross-refs): Working lists populated with deserialized data, dict rebuilt.
        /// - Post-load: Working lists cleared (handled by Scribe).
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Use the 5-parameter overload with explicit working lists when LookModes differ.
            // Scribe_Collections.Look will:
            // - On save: extract dict into keysWorkingList and valuesWorkingList, serialize them
            // - On load: create empty dict, then populate working lists from XML
            // - On cross-ref resolution: rebuild dict from the working lists
            // - After load: automatically clear the working lists
            Scribe_Collections.Look(
                ref cribGateStates,
                "cribGateStates",
                LookMode.Reference,      // keys: Building_Bed references
                LookMode.Value,          // values: bool primitives
                ref keysWorkingList,
                ref valuesWorkingList
            );

            // Defensive: ensure dict is never null after ExposeData completes.
            // This handles edge cases where Scribe fails to initialize it.
            if (cribGateStates == null)
            {
                cribGateStates = new Dictionary<Building_Bed, bool>();
            }
        }
    }
}
