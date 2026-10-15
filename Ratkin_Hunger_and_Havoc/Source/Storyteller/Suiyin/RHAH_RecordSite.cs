using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_RecordSite
    {
        internal const string PartName = "RHAH_RecordSite";
        internal const string WorldName = "RHAH_RecordSite";
        internal const string BoxName = "RHAH_RecordBox";
        internal const string RecordName = "RHAH_MigrationRecord";
        internal const int MinDistance = 5;
        internal const int MaxDistance = 22;
        internal const int SpawnRadius = 15;

        internal static bool ShouldRemove(SuiyinN009Outcome outcome, bool boxDestroyed, bool hasMap, bool anyoneOnMap, bool mapEntered)
        {
            return outcome == SuiyinN009Outcome.Empty && !boxDestroyed && !hasMap && !anyoneOnMap && !mapEntered;
        }

        internal static void Tick(int tick)
        {
            if (!RHAH_NarrativePace.Due(tick, RHAH_NarrativePace.Spread, RHAH_NarrativePace.Spread * 5) || Current.Game == null)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            if (book == null)
            {
                return;
            }

            SuiyinN009Case record = book.N009;
            if (record == null)
            {
                TryOpen(book, tick);
                return;
            }

            if (record.Outcome != SuiyinN009Outcome.Pending)
            {
                return;
            }

            if (record.SiteId == 0)
            {
                if (!TryPlace(record))
                {
                    book.N009 = null;
                }

                return;
            }

            Site site = FindSite(record.SiteId);
            if (site == null)
            {
                record.MapPresent = false;
                state.Commit(item => item.ExpireRelic(tick));
                return;
            }

            Map map = site.Map;
            bool players = map != null && map.mapPawns != null && map.mapPawns.AnyColonistSpawned;
            bool anyone = map != null && map.mapPawns != null && map.mapPawns.AllPawnsSpawned.Count > 0;
            record.MapPresent = true;
            record.PlayersInside = players;
            record.EnvoyHere = players && EnvoyHere(book);
            if (!record.MapEntered)
            {
                TryEnter(record, map);
            }
            else
            {
                RefreshBox(record, map);
            }

            state.Commit(item => item.ExpireRelic(tick));
            if (ShouldRemove(record.Outcome, record.BoxDestroyed, site.HasMap, anyone, record.MapEntered))
            {
                site.Destroy();
            }

            NoteDone(state, book, tick);
        }

        static void TryOpen(SuiyinBook book, int tick)
        {
            if (book.Distinct < book.Config.RelicKinds && !book.RelicClue)
            {
                return;
            }

            if (!book.OpenRelic(tick))
            {
                return;
            }

            SuiyinN009Case record = book.N009;
            if (record == null || TryPlace(record))
            {
                return;
            }

            book.N009 = null;
        }

        static bool TryPlace(SuiyinN009Case record)
        {
            if (Find.World == null || Find.WorldObjects == null)
            {
                return false;
            }

            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail(PartName);
            WorldObjectDef worldDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail(WorldName);
            PlanetTile tile;
            if (part == null || worldDef == null || !TileFinder.TryFindNewSiteTile(out tile, MinDistance, MaxDistance))
            {
                return false;
            }

            Site site = SiteMaker.MakeSite(part, tile, null, true, null, worldDef);
            if (site == null)
            {
                return false;
            }

            Find.WorldObjects.Add(site);
            record.SiteId = site.ID;
            record.MapPresent = true;
            return record.SiteId != 0;
        }

        static void TryEnter(SuiyinN009Case record, Map map)
        {
            if (map == null)
            {
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(BoxName);
            IntVec3 cell;
            if (def == null || !TryCell(map, def, out cell))
            {
                return;
            }

            Thing box = GenSpawn.Spawn(def, cell, map);
            if (box == null || !box.Spawned)
            {
                return;
            }

            record.BoxId = box.thingIDNumber;
            record.MapEntered = true;
            record.MapPresent = true;
        }

        static void RefreshBox(SuiyinN009Case record, Map map)
        {
            if (record.BoxDestroyed || record.BoxId == 0 || map == null)
            {
                return;
            }

            Thing box = FindBox(map, record.BoxId);
            if (box == null || !box.Spawned || box.Destroyed)
            {
                record.BoxDestroyed = true;
            }
        }

        static bool TryCell(Map map, ThingDef def, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null)
            {
                return false;
            }

            return CellFinder.TryFindRandomCellNear(
                map.Center,
                map,
                SpawnRadius,
                candidate => candidate.InBounds(map) && candidate.Standable(map) && GenSpawn.CanSpawnAt(def, candidate, map),
                out cell);
        }

        internal static bool EnvoyHere(SuiyinBook book)
        {
            if (book?.N008 == null)
            {
                return false;
            }

            for (int i = 0; i < book.N008.Count; i++)
            {
                SuiyinN008Case envoy = book.N008[i];
                if (envoy != null && envoy.Presence == SuiyinPresence.Here &&
                    (envoy.Outcome == SuiyinN008Outcome.Waiting || envoy.Outcome == SuiyinN008Outcome.Checking))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Choose(SuiyinN009Action action, Building box)
        {
            if (box == null || !box.Spawned || Current.Game == null)
            {
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            SuiyinBook book = state?.Book;
            SuiyinN009Case record = book?.N009;
            if (book == null || record == null || record.Outcome != SuiyinN009Outcome.Pending || record.BoxDestroyed)
            {
                return;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            Map map = box.Map;
            record.MapPresent = map != null;
            record.PlayersInside = map?.mapPawns != null && map.mapPawns.AnyColonistSpawned;
            record.EnvoyHere = record.PlayersInside && EnvoyHere(book);
            if ((action == SuiyinN009Action.Hand || action == SuiyinN009Action.Share) && !record.EnvoyHere)
            {
                return;
            }

            IntVec3 spot = box.Position;
            int silver = state.Commit(item => item.ChooseRelic(action, tick));
            if (record.Outcome == SuiyinN009Outcome.Pending)
            {
                return;
            }

            Drop(map, spot, silver, Copies(action));
            if (!box.Destroyed)
            {
                box.Destroy(DestroyMode.Vanish);
            }

            NoteDone(state, book, tick);
        }

        internal static void NoteDestroyed(Building box)
        {
            if (box == null || Current.Game == null)
            {
                return;
            }

            SuiyinBook book = Current.Game.GetComponent<NarrativeState>()?.Book;
            SuiyinN009Case record = book?.N009;
            if (record == null || record.Outcome != SuiyinN009Outcome.Pending || record.BoxId != box.thingIDNumber)
            {
                return;
            }

            record.BoxDestroyed = true;
            record.MapPresent = box.Map != null;
            record.PlayersInside = box.Map?.mapPawns != null && box.Map.mapPawns.AnyColonistSpawned;
        }

        static void NoteDone(NarrativeState state, SuiyinBook book, int tick)
        {
            SuiyinN009Case record = book?.N009;
            if (state == null || record == null)
            {
                return;
            }

            if (record.Outcome == SuiyinN009Outcome.Taken ||
                record.Outcome == SuiyinN009Outcome.Left ||
                record.Outcome == SuiyinN009Outcome.Handed ||
                record.Outcome == SuiyinN009Outcome.Shared)
            {
                state.NoteRelicDone(tick);
            }
        }

        static int Copies(SuiyinN009Action action)
        {
            if (action == SuiyinN009Action.Take || action == SuiyinN009Action.Hand)
            {
                return 1;
            }

            if (action == SuiyinN009Action.Share)
            {
                return 2;
            }

            return 0;
        }

        static void Drop(Map map, IntVec3 spot, int silver, int copies)
        {
            if (map == null)
            {
                return;
            }

            ThingDef silverDef = ThingDefOf.Silver;
            if (silver > 0 && silverDef != null)
            {
                Thing money = ThingMaker.MakeThing(silverDef);
                money.stackCount = silver;
                GenPlace.TryPlaceThing(money, spot, map, ThingPlaceMode.Near);
            }

            ThingDef recordDef = DefDatabase<ThingDef>.GetNamedSilentFail(RecordName);
            for (int i = 0; i < copies && recordDef != null; i++)
            {
                Thing record = ThingMaker.MakeThing(recordDef);
                GenPlace.TryPlaceThing(record, spot, map, ThingPlaceMode.Near);
            }
        }

        static Site FindSite(int id)
        {
            if (id == 0 || Find.WorldObjects == null)
            {
                return null;
            }

            List<Site> sites = Find.WorldObjects.Sites;
            if (sites == null)
            {
                return null;
            }

            for (int i = 0; i < sites.Count; i++)
            {
                Site site = sites[i];
                if (site != null && site.ID == id && site.def != null && site.def.defName == WorldName)
                {
                    return site;
                }
            }

            return null;
        }



        static Thing FindBox(Map map, int id)
        {
            if (map == null || id == 0)
            {
                return null;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(BoxName);
            List<Thing> things = def == null ? null : map.listerThings.ThingsOfDef(def);
            if (things == null)
            {
                return null;
            }

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.thingIDNumber == id)
                {
                    return thing;
                }
            }

            return null;
        }
    }



    public class Building_RHAH_RecordBox : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!Spawned || Destroyed)
            {
                yield break;
            }

            SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            SuiyinN009Case record = book?.N009;
            if (record == null || record.Outcome != SuiyinN009Outcome.Pending || record.BoxId != thingIDNumber || record.BoxDestroyed)
            {
                yield break;
            }

            yield return Button("RHAH_Record_Take", () => RHAH_RecordSite.Choose(SuiyinN009Action.Take, this));
            yield return Button("RHAH_Record_Leave", () => RHAH_RecordSite.Choose(SuiyinN009Action.Leave, this));
            if (record.EnvoyHere)
            {
                yield return Button("RHAH_Record_Hand", () => RHAH_RecordSite.Choose(SuiyinN009Action.Hand, this));
                yield return Button("RHAH_Record_Share", () => RHAH_RecordSite.Choose(SuiyinN009Action.Share, this));
            }

            yield return Button("RHAH_Record_Destroy", () => RHAH_RecordSite.Choose(SuiyinN009Action.Destroy, this));
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.Vanish && Spawned)
            {
                RHAH_RecordSite.NoteDestroyed(this);
            }

            base.Destroy(mode);
        }

        static Command_Action Button(string key, System.Action action)
        {
            return new Command_Action
            {
                defaultLabel = key.Translate(),
                action = action
            };
        }
    }
}
