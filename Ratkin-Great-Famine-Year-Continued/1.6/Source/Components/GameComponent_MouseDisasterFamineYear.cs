using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class GameComponent_MouseDisasterFamineYear : GameComponent
    {
        private const int FamineYearCheckIntervalTicks = 60;

        private static readonly List<string> MajorDisasterIncidentDefNames = new List<string>
        {
            "ColdSnap",
            "HeatWave",
            "ToxicFallout",
            "VolcanicWinter"
        };

        private int famineStateYear = -1;
        private bool famineYearActive;
        private bool famineYearBonusConsumed;
        private int lastDailyCheckYear = -1;
        private int lastDailyCheckDayOfYear = -1;

        public GameComponent_MouseDisasterFamineYear(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref famineStateYear, "mouseDisaster_famineStateYear", -1);
            Scribe_Values.Look(ref famineYearActive, "mouseDisaster_famineYearActive", false);
            Scribe_Values.Look(ref famineYearBonusConsumed, "mouseDisaster_famineYearBonusConsumed", false);
            Scribe_Values.Look(ref lastDailyCheckYear, "mouseDisaster_lastDailyCheckYear", -1);
            Scribe_Values.Look(ref lastDailyCheckDayOfYear, "mouseDisaster_lastDailyCheckDayOfYear", -1);
        }

        public override void GameComponentTick()
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || settings == null || !settings.enableFamineYearSystem || Find.TickManager == null)
            {
                return;
            }

            if (Find.TickManager.TicksGame % FamineYearCheckIntervalTicks != 0)
            {
                return;
            }

            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            if (GenLocalDate.HourInteger(map) != 6)
            {
                return;
            }

            int year = GenLocalDate.Year(map);
            int dayOfYear = GenLocalDate.DayOfYear(map);
            if (year == lastDailyCheckYear && dayOfYear == lastDailyCheckDayOfYear)
            {
                return;
            }

            lastDailyCheckYear = year;
            lastDailyCheckDayOfYear = dayOfYear;

            TryJudgeFamineYear(settings, map, year, dayOfYear);

            if (!famineYearActive || famineYearBonusConsumed)
            {
                return;
            }

            float bonusChance = Mathf.Clamp01(settings.famineYearDisasterBonusPercent / 100f);
            if (!Rand.Chance(bonusChance))
            {
                return;
            }

            if (TryTriggerMajorDisaster(map, out IncidentDef triggeredIncident))
            {
                famineYearBonusConsumed = true;
                if (triggeredIncident != null)
                {
                    Messages.Message("MouseDisaster_FamineYear_MajorDisasterTriggered".Translate(triggeredIncident.LabelCap), new TargetInfo(map.Center, map), MessageTypeDefOf.ThreatBig, historical: true);
                }
            }
        }

        private void TryJudgeFamineYear(MouseDisasterSettings settings, Map map, int currentYear, int dayOfYear)
        {
            if (famineStateYear == currentYear)
            {
                return;
            }

            bool firstDayOfYear = dayOfYear == 0;
            if (!firstDayOfYear && famineStateYear >= 0)
            {
                return;
            }

            bool wasCatchup = !firstDayOfYear;
            famineStateYear = currentYear;
            famineYearBonusConsumed = false;
            famineYearActive = Rand.Chance(Mathf.Clamp01(settings.famineYearChancePercent / 100f));

            string letterLabel = "MouseDisaster_FamineYear_JudgementLabel".Translate(currentYear);
            string letterText = famineYearActive
                ? "MouseDisaster_FamineYear_JudgementActive".Translate(settings.famineYearDisasterBonusPercent.ToString("0"))
                : "MouseDisaster_FamineYear_JudgementInactive".Translate();

            if (wasCatchup)
            {
                letterText += "\n" + "MouseDisaster_FamineYear_JudgementCatchup".Translate();
            }

            Find.LetterStack.ReceiveLetter(
                letterLabel,
                letterText,
                LetterDefOf.NeutralEvent,
                new TargetInfo(map.Center, map));
        }

        private static bool TryTriggerMajorDisaster(Map map, out IncidentDef triggeredIncident)
        {
            triggeredIncident = null;
            if (map == null)
            {
                return false;
            }

            List<IncidentDef> candidates = MajorDisasterIncidentDefNames
                .Select(DefDatabase<IncidentDef>.GetNamedSilentFail)
                .Where(def => def?.Worker != null)
                .InRandomOrder()
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                IncidentDef incidentDef = candidates[i];
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                if (!incidentDef.Worker.CanFireNow(parms))
                {
                    continue;
                }

                if (incidentDef.Worker.TryExecute(parms))
                {
                    triggeredIncident = incidentDef;
                    return true;
                }
            }

            return false;
        }
    }
}
