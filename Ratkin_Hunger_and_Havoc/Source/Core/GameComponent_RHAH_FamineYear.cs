using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Core
{
    // Verse 按 GameComponent 类型发现并持久化年度判定与稀有事件限额
    public sealed class GameComponent_RHAH_FamineYear : GameComponent
    {
        readonly Game game;
        int calendarMapId = -1;
        float calendarLongitude;
        int judgedYear = -1;
        bool famineYear;
        bool disasterSucceeded;
        int lastDisasterAttemptDay = -1;
        int swarmCount;
        int lastSwarmTick = -1;
        int lastPleadingTick = -1;

        static readonly string[] disasterNames = { "ColdSnap", "HeatWave", "ToxicFallout", "VolcanicWinter" };

        public GameComponent_RHAH_FamineYear(Game game)
        {
            this.game = game;
        }

        internal static bool Active
        {
            get
            {
                RHAH_Settings settings = RHAH_Mod.Settings;
                if (settings == null || !settings.famineYearEnabled || !settings.enableNewContent)
                {
                    return false;
                }

                GameComponent_RHAH_FamineYear component = CurrentComponent();
                return component != null && component.famineYear && component.calendarMapId >= 0 &&
                    component.game.tickManager != null && component.judgedYear ==
                    GenDate.Year(component.game.tickManager.TicksAbs, component.calendarLongitude) &&
                    RHAH_Runtime.AllowsNewContent;
            }
        }

        internal static bool Available(string displayId)
        {
            if (displayId != "I-052" && displayId != "I-053")
            {
                return true;
            }

            GameComponent_RHAH_FamineYear component = CurrentComponent();
            if (component == null || component.game.tickManager == null)
            {
                return false;
            }

            int tick = component.game.tickManager.TicksGame;
            if (displayId == "I-053")
            {
                return PleadingAvailable(component.lastPleadingTick, tick);
            }

            return SwarmAvailable(Active, component.swarmCount, component.lastSwarmTick, tick);
        }

        internal static void NoteQueued(string displayId)
        {
            if (displayId != "I-052" && displayId != "I-053")
            {
                return;
            }

            GameComponent_RHAH_FamineYear component = CurrentComponent();
            if (component == null || component.game.tickManager == null)
            {
                return;
            }

            int tick = component.game.tickManager.TicksGame;
            // 接受触发时占用限额 后续赶路失败仍计入这次触发
            if (displayId == "I-052")
            {
                component.swarmCount++;
                component.lastSwarmTick = tick;
            }
            else
            {
                component.lastPleadingTick = tick;
            }
        }

        static GameComponent_RHAH_FamineYear CurrentComponent()
        {
            List<GameComponent> components = Current.Game?.components;
            if (components != null)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    if (components[i] is GameComponent_RHAH_FamineYear component)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        internal static float ClampMultiplier(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 3f;
            }

            return value < 1f ? 1f : value > 4f ? 4f : value;
        }

        internal static int ScalePopulation(int count)
        {
            return !Active || count <= 1 ? count :
                (int)Math.Ceiling(count * ClampMultiplier(RHAH_Mod.Settings?.famineYearPawnMultiplier ?? 3f));
        }

        internal static bool JudgmentDue(int savedYear, int year, int day, int hour)
        {
            return savedYear != year && (savedYear < 0 ? day > 0 || hour >= 6 : day == 0 && hour >= 6);
        }

        internal static bool SwarmAvailable(bool active, int count, int lastTick, int tick)
        {
            return active && count < 2 && (lastTick < 0 || (long)tick - lastTick >= 15L * GenDate.TicksPerDay);
        }

        internal static bool PleadingAvailable(int lastTick, int tick)
        {
            return lastTick < 0 || (long)tick - lastTick >= 180L * GenDate.TicksPerDay;
        }

        internal static float ClampPercent(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return value < 0f ? 0f : value > 100f ? 100f : value;
        }

        public override void GameComponentTick()
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (game.tickManager == null || game.tickManager.TicksGame % GenDate.TicksPerHour != 0 ||
                settings == null || !settings.famineYearEnabled || !RHAH_Runtime.AllowsNewContent)
            {
                return;
            }

            Map home = CalendarHome();
            if (home == null)
            {
                return;
            }
            int absTick = game.tickManager.TicksAbs;
            int year = GenDate.Year(absTick, calendarLongitude);
            int day = GenDate.DayOfYear(absTick, calendarLongitude);
            int hour = GenDate.HourOfDay(absTick, calendarLongitude);
            if (JudgmentDue(judgedYear, year, day, hour))
            {
                bool catchup = judgedYear < 0 && day > 0;
                judgedYear = year;
                famineYear = Rand.Chance(ClampPercent(settings.famineYearChancePercent, 40f) / 100f);
                disasterSucceeded = false;
                swarmCount = 0;
                lastDisasterAttemptDay = -1;
                string key = famineYear ? "RHAH_FamineYear_Famine" : "RHAH_FamineYear_Normal";
                string text = key.Translate(year).ToString();
                if (catchup)
                {
                    text = "RHAH_FamineYear_Catchup".Translate().ToString() + "\n\n" + text;
                }
                game.letterStack.ReceiveLetter("RHAH_FamineYear_Label".Translate(year), text,
                    famineYear ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent, home.Parent);
            }

            int localDay = (year - GenDate.DefaultStartingYear) * GenDate.DaysPerYear + day;
            if (!famineYear || judgedYear != year || disasterSucceeded || hour != 6 || localDay == lastDisasterAttemptDay)
            {
                return;
            }

            lastDisasterAttemptDay = localDay;
            if (Rand.Chance(ClampPercent(settings.famineYearDisasterChancePercent, 20f) / 100f))
            {
                TryDisaster(home);
            }
        }

        Map CalendarHome()
        {
            List<Map> maps = game.Maps;
            if (maps == null || Find.WorldGrid == null)
            {
                return null;
            }

            Map fallback = null;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || map.Parent == null || map.Parent.Destroyed || !map.IsPlayerHome)
                {
                    continue;
                }

                if (map.uniqueID == calendarMapId)
                {
                    return map;
                }

                if (fallback == null || i == game.currentMapIndex)
                {
                    fallback = map;
                }
            }

            if (fallback != null)
            {
                calendarMapId = fallback.uniqueID;
                calendarLongitude = Find.WorldGrid.LongLatOf(fallback.Tile).x;
            }

            return fallback;
        }

        void TryDisaster(Map home)
        {
            int start = Rand.Range(0, disasterNames.Length);
            for (int i = 0; i < disasterNames.Length; i++)
            {
                IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(disasterNames[(start + i) % disasterNames.Length]);
                if (incident == null)
                {
                    continue;
                }

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, home);
                if (incident.Worker.CanFireNow(parms) && incident.Worker.TryExecute(parms))
                {
                    disasterSucceeded = true;
                    return;
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref calendarMapId, "calendarMapId", -1);
            Scribe_Values.Look(ref calendarLongitude, "calendarLongitude", 0f);
            Scribe_Values.Look(ref judgedYear, "judgedYear", -1);
            Scribe_Values.Look(ref famineYear, "famineYear", false);
            Scribe_Values.Look(ref disasterSucceeded, "disasterSucceeded", false);
            Scribe_Values.Look(ref lastDisasterAttemptDay, "lastDisasterAttemptDay", -1);
            Scribe_Values.Look(ref swarmCount, "swarmCount", 0);
            Scribe_Values.Look(ref lastSwarmTick, "lastSwarmTick", -1);
            Scribe_Values.Look(ref lastPleadingTick, "lastPleadingTick", -1);
        }
    }
}
