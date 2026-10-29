using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Game-level component that owns every <see cref="Pawn_PlayTracker"/>
    /// in the current game. Used because we cannot add fields directly to
    /// <see cref="Pawn"/>.
    ///
    /// Serialises as two parallel lists (pawn → tracker) because
    /// <see cref="Scribe_Collections.Look"/> does not support dictionaries.
    /// </summary>
    public class GameComponent_PlayTracker : GameComponent
    {
        /// <summary>Runtime lookup: pawn → play tracker.</summary>
        Dictionary<Pawn, Pawn_PlayTracker> trackers = new Dictionary<Pawn, Pawn_PlayTracker>();

        /// <summary>Scribe buffers for saving/loading the dictionary.</summary>
        List<Pawn> scribePawnKeys = new List<Pawn>();
        List<Pawn_PlayTracker> scribeTrackerValues = new List<Pawn_PlayTracker>();

        public GameComponent_PlayTracker(Game game)
        {
        }

        /// <summary>
        /// Returns the existing tracker for <paramref name="pawn"/>,
        /// creating and initialising a new one if necessary.
        /// </summary>
        public Pawn_PlayTracker GetTracker(Pawn pawn)
        {
            if (pawn == null)
                return null;

            if (!trackers.TryGetValue(pawn, out Pawn_PlayTracker tracker))
            {
                tracker = new Pawn_PlayTracker(pawn);
                trackers[pawn] = tracker;
            }
            return tracker;
        }

        /// <summary>
        /// Non-creating lookup: returns the existing tracker for
        /// <paramref name="pawn"/> if one has already been created, or
        /// <c>null</c> if none exists yet. Unlike <see cref="GetTracker"/>,
        /// this NEVER lazily creates a tracker.
        ///
        /// Use this for read-only "peek" checks from OUTSIDE the play-need
        /// system itself — e.g. the Learning-desire mutual-exclusion check in
        /// HarmonyPatches.cs, which needs to know a pawn's currently active
        /// play desires but must NOT have the side effect of creating (and
        /// therefore saving) an empty tracker for every child in the game the
        /// first time their vanilla Learning desire happens to roll —
        /// especially wasteful when the whole child play-need system is
        /// disabled in settings, where every such tracker would stay forever
        /// empty.
        /// </summary>
        public Pawn_PlayTracker? TryGetTracker(Pawn pawn)
        {
            if (pawn == null)
                return null;
            trackers.TryGetValue(pawn, out Pawn_PlayTracker tracker);
            return tracker;
        }

        /// <summary>
        /// Removes dead or null pawns from the dictionary to prevent leaks.
        /// Called infrequently (every 2500 ticks) via
        /// <see cref="GameComponent.GameComponentTick"/>.
        /// </summary>
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 0)
                return;

            List<Pawn> toRemove = new List<Pawn>();
            foreach (var kvp in trackers)
            {
                if (kvp.Key == null || kvp.Key.Destroyed)
                    toRemove.Add(kvp.Key);
            }
            foreach (Pawn p in toRemove)
            {
                trackers.Remove(p);
            }
        }

        /// <summary>
        /// Serialises the dictionary using RimWorld's two-list pattern.
        /// </summary>
        public override void ExposeData()
        {
            // When saving, flatten the dictionary into parallel lists.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                scribePawnKeys.Clear();
                scribeTrackerValues.Clear();
                foreach (var kvp in trackers)
                {
                    if (kvp.Key != null && kvp.Value != null)
                    {
                        scribePawnKeys.Add(kvp.Key);
                        scribeTrackerValues.Add(kvp.Value);
                    }
                }
            }

            Scribe_Collections.Look(ref scribePawnKeys, "pawnKeys", LookMode.Reference);
            Scribe_Collections.Look(ref scribeTrackerValues, "trackerValues", LookMode.Deep);

            // When loading, rebuild the dictionary and re-bind pawn references.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                trackers.Clear();
                if (scribePawnKeys != null && scribeTrackerValues != null)
                {
                    int count = Mathf.Min(scribePawnKeys.Count, scribeTrackerValues.Count);
                    for (int i = 0; i < count; i++)
                    {
                        Pawn pawn = scribePawnKeys[i];
                        Pawn_PlayTracker tracker = scribeTrackerValues[i];
                        if (pawn != null && tracker != null)
                        {
                            tracker.BindPawn(pawn);
                            trackers[pawn] = tracker;
                        }
                    }
                }
                scribePawnKeys.Clear();
                scribeTrackerValues.Clear();
            }
        }
    }
}
