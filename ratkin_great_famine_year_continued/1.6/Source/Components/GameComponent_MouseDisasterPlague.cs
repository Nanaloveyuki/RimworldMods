using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class GameComponent_MouseDisasterPlague : GameComponent
    {
        private const int PlagueCheckIntervalTicks = 60;
        private Dictionary<int, int> lastPlagueSpreadDayByMapId = new Dictionary<int, int>();

        public GameComponent_MouseDisasterPlague(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref lastPlagueSpreadDayByMapId, "mouseDisaster_plagueLastSpreadDayByMapId", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastPlagueSpreadDayByMapId == null)
            {
                lastPlagueSpreadDayByMapId = new Dictionary<int, int>();
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager == null || Find.Maps == null || Find.TickManager.TicksGame % PlagueCheckIntervalTicks != 0)
            {
                return;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map == null || !map.IsPlayerHome || GenLocalDate.HourInteger(map) != 6)
                {
                    continue;
                }

                int absoluteDay = GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);
                if (absoluteDay % 3 != 0)
                {
                    continue;
                }

                if (lastPlagueSpreadDayByMapId.TryGetValue(map.uniqueID, out int lastDay) && lastDay == absoluteDay)
                {
                    continue;
                }

                lastPlagueSpreadDayByMapId[map.uniqueID] = absoluteDay;
                MouseDisasterPlagueUtility.DoPlagueSpreadCheck(map);
            }
        }
    }
}
