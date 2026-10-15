using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    public class MouseDisasterEggAirdropRecord : IExposable
    {
        public int factionId;
        public int tick;
        public int count;
        public bool plague;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionId, "factionId", 0);
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref plague, "plague", false);
        }
    }

    public class GameComponent_MouseDisasterPhase3 : GameComponent
    {
        private const int EggRetaliationWindowTicks = GenDate.TicksPerDay * 3;

        private List<MouseDisasterEggAirdropRecord> eggAirdropRecords = new List<MouseDisasterEggAirdropRecord>();

        public GameComponent_MouseDisasterPhase3(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref eggAirdropRecords, "mouseDisaster_eggAirdropRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                eggAirdropRecords ??= new List<MouseDisasterEggAirdropRecord>();
            }

            if (Scribe.mode != LoadSaveMode.Saving)
            {
                PruneExpiredAirdropRecords();
            }
        }

        public void RecordAirdroppedEggs(Faction faction, int eggCount, bool plague)
        {
            if (!MouseDisasterRuntime.AllowsNewContent || faction == null || eggCount <= 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            PruneExpiredAirdropRecords(nowTick);
            eggAirdropRecords.Add(new MouseDisasterEggAirdropRecord
            {
                factionId = faction.loadID,
                tick = nowTick,
                count = eggCount,
                plague = plague
            });

            List<MouseDisasterEggAirdropRecord> recent = eggAirdropRecords
                .Where(record => record != null && record.factionId == faction.loadID && nowTick - record.tick <= EggRetaliationWindowTicks)
                .ToList();
            int total = recent.Sum(record => record.count);
            if (total <= 15)
            {
                return;
            }

            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map != null)
            {
                MouseDisasterPhase3Utility.TriggerEggBombRetaliation(map, total * 4, recent.Any(record => record.plague), faction);
            }

            eggAirdropRecords.RemoveAll(record => record != null && record.factionId == faction.loadID);
        }

        private void PruneExpiredAirdropRecords()
        {
            PruneExpiredAirdropRecords(Find.TickManager?.TicksGame ?? 0);
        }

        private void PruneExpiredAirdropRecords(int nowTick)
        {
            eggAirdropRecords ??= new List<MouseDisasterEggAirdropRecord>();
            eggAirdropRecords.RemoveAll(record => record == null || MouseDisasterGeneRestorePolicy.ShouldPruneExpiredRecord(nowTick, record.tick, EggRetaliationWindowTicks));
        }
    }
}
