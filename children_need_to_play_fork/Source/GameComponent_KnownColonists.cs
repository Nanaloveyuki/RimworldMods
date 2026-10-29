using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Save-wide (NOT per-map) registry of every pawn that has ever been a free
    /// colonist. Exists solely to support <see cref="MapComponent_ToddlerEventMoods"/>'s
    /// "a new person came to play!" toddler thought.
    ///
    /// ── Why this exists instead of a simple per-map headcount comparison ──
    /// The original implementation compared <c>map.mapPawns.FreeColonistsCount</c>
    /// tick-over-tick and fired whenever it went up. That produces false
    /// positives for EVERY reason a colonist's presence on one particular map
    /// can increase that has nothing to do with someone genuinely joining the
    /// colony for the first time: a caravan returning, a pawn waking from
    /// cryosleep, someone coming back from an off-map quest/shuttle, or simply
    /// walking from one of the player's own maps to another. Because a colonist
    /// can only ever occupy one map at a time, tracking "have I EVER seen this
    /// pawn ID as a free colonist, on ANY map, in this save" is the only way to
    /// reliably answer "is this genuinely their first time" — a per-map set
    /// can't do it (arriving on a second map for the first time would look
    /// identical to a genuine new join), which is why this lives on a
    /// <see cref="GameComponent"/> (one per save) rather than being folded into
    /// the per-map <c>MapComponent_ToddlerEventMoods</c>.
    /// </summary>
    public class GameComponent_KnownColonists : GameComponent
    {
        private HashSet<int> knownColonistIds = new HashSet<int>();

        /// <summary>Required constructor signature for RimWorld's automatic GameComponent discovery (Game.FillComponents).</summary>
        public GameComponent_KnownColonists(Game game)
        {
        }

        // (The old single-pawn RegisterAndCheckIfNew(Pawn) was removed — its only
        // caller now uses RegisterColonistsOn(Map). HashSet<T>.Add returning false
        // for an existing element is still the whole "is this new" signal.)

        /// <summary>
        /// Registers every free colonist (colonists AND slaves) currently SPAWNED on
        /// <paramref name="map"/> and returns <c>true</c> if at least one of them
        /// had never been seen before.
        ///
        /// PERF: deliberately NOT <c>map.mapPawns.FreeColonists</c> — that goes through
        /// <c>MapPawns.AllPawns</c>, which does a recursive walk of every thing-holder
        /// on the map (ThingOwnerUtility.GetAllThingsRecursively), far too expensive
        /// to run every 150 ticks on every map. <c>SpawnedPawnsInFaction</c> is the
        /// engine's maintained per-faction list; the filter below is FreeColonists'
        /// own rule. (Vanilla's FreeColonistsSpawned is NOT a substitute: it drops
        /// slaves, which FreeColonists keeps.) Unspawned colonists (carried, in a
        /// casket) are registered the moment they spawn — i.e. when a toddler could
        /// actually meet them; the load-time seed in FinalizeInit covers them too. Called by MapComponent_ToddlerEventMoods on
        /// EVERY check (even when the map has no toddlers, so a join is judged at
        /// the moment it happens, never hours later), and by the WandererJoin
        /// incident patch to pre-register the wanderer so the generic "new
        /// colonist" mood doesn't fire on top of the specific "wanderer joins"
        /// mood for the same person.
        ///
        /// Every colonist is registered even after a new one is found (no early
        /// break), so two simultaneous joins are both consumed in one call.
        /// Index loop over the engine's shared cached list: allocation-free, and
        /// nothing inside the loop can refill that list.
        /// </summary>
        public bool RegisterColonistsOn(Map map)
        {
            if (map == null) return false;
            bool anyNew = false;
            bool anomaly = ModsConfig.AnomalyActive;
            List<Pawn> spawned = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn p = spawned[i];
                // Same rule as MapPawns.FreeHumanlikesOfFaction (FreeColonists).
                if (!p.RaceProps.Humanlike) continue;
                if (p.HostFaction != null && !p.IsSlave) continue;
                if (anomaly && p.IsSubhuman) continue;
                if (knownColonistIds.Add(p.thingIDNumber))
                    anyNew = true;
            }
            return anyNew;
        }

        /// <summary>
        /// Pre-seeds the registry with every colonist that already exists the
        /// moment the game finishes loading (new game OR loaded save) so none of
        /// them retroactively fire "a new colonist!" the first time
        /// MapComponent_ToddlerEventMoods runs. This matters both for a brand
        /// new game (the starting colonists shouldn't count as "new") and for
        /// this mod being added to an already-in-progress save (every existing
        /// colonist would otherwise look brand new the instant this component
        /// first exists). Calling Add for an already-known pawn is a harmless
        /// no-op, so re-seeding on every load (including saves that already had
        /// this component) is safe and idempotent.
        /// </summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                RegisterColonistsOn(maps[i]);

            // BUGFIX: the old seed only covered pawns ON MAPS. A colonist who was
            // away in a caravan, a travelling transporter, or lent to a quest (all
            // world pawns) when the save was loaded / the mod was added was missing
            // from the registry and got announced as "a new person came to play!"
            // the moment they came home. PawnsFinder.AllMapsWorldAndTemporary_Alive
            // covers maps + world pawns (caravans included) + temporary pawns. Runs
            // once per load, so iterating the world pawns here is fine.
            Faction? ofPlayer = Faction.OfPlayer;
            List<Pawn> everyone = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            for (int i = 0; i < everyone.Count; i++)
            {
                Pawn p = everyone[i];
                if (p != null && p.Faction == ofPlayer && p.RaceProps.Humanlike)
                    knownColonistIds.Add(p.thingIDNumber);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref knownColonistIds, "knownColonistIds", LookMode.Value);
            if (knownColonistIds == null) knownColonistIds = new HashSet<int>();
        }
    }
}
