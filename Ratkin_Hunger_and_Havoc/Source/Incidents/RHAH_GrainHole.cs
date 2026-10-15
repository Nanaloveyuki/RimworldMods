using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_GrainHole
    {
        internal const int CheckInterval = GenDate.TicksPerHour * 2;
        internal const int DayTicks = 60000;
        internal const int LossRange = 12;
        internal const int MaxLosses = 3;
        internal const int WoodCost = 20;
        internal const int CleanPortions = 5;
        internal const string HoleDefName = "RHAH_GrainHole";
        internal const string LetterDefName = "RHAH_GrainHoleLetter";

        internal static void Tick(Map map)
        {
            if (map == null || Find.TickManager == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (!RHAH_NarrativePace.Due(tick, CheckInterval, map.uniqueID))
            {
                return;
            }

            SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            if (book?.N006 == null)
            {
                return;
            }

            MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
            for (int i = 0; i < book.N006.Count; i++)
            {
                SuiyinN006Case record = book.N006[i];
                if (record == null || record.MapId != map.uniqueID)
                {
                    continue;
                }

                if (record.Outcome == SuiyinN006Outcome.Pending && !record.Hole)
                {
                    TryPlace(map, component, book, record, tick);
                }
                else if (record.Hole && (record.Outcome == SuiyinN006Outcome.Pending || record.Outcome == SuiyinN006Outcome.BaitSet))
                {
                    TickOpen(map, component, book, record, tick);
                }
            }
        }

        internal static bool TakeFood(Map map, IntVec3 origin, int max, int range)
        {
            if (map == null || max <= 0)
            {
                return false;
            }

            int taken = 0;
            List<Thing> foods = Foods(map);
            for (int n = 0; n < max && taken < max; n++)
            {
                bool any = false;
                for (int i = 0; i < foods.Count; i++)
                {
                    Thing food = foods[i];
                    if (!InRange(food, origin, range) || food.stackCount <= 0)
                    {
                        continue;
                    }

                    food.SplitOff(1).Destroy();
                    taken++;
                    any = true;
                    break;
                }

                if (!any)
                {
                    break;
                }
            }

            return taken > 0;
        }

        internal static int CountWood(Map map)
        {
            if (map == null || ThingDefOf.WoodLog == null)
            {
                return 0;
            }

            int count = 0;
            List<Thing> logs = map.listerThings.ThingsOfDef(ThingDefOf.WoodLog);
            for (int i = 0; i < logs.Count; i++)
            {
                Thing log = logs[i];
                if (log != null && log.Spawned && !log.Destroyed && log.stackCount > 0)
                {
                    count += log.stackCount;
                }
            }

            return count;
        }

        internal static bool SpendWood(Map map, int amount)
        {
            if (map == null || amount < 0 || CountWood(map) < amount)
            {
                return false;
            }

            List<Thing> logs = map.listerThings.ThingsOfDef(ThingDefOf.WoodLog);
            int left = amount;
            for (int i = logs.Count - 1; i >= 0 && left > 0; i--)
            {
                Thing log = logs[i];
                if (log == null || !log.Spawned || log.Destroyed || log.stackCount <= 0)
                {
                    continue;
                }

                int have = log.stackCount < left ? log.stackCount : left;
                log.SplitOff(have).Destroy();
                left -= have;
            }

            return left == 0;
        }

        internal static void RemoveHole(Map map, MapComponent_RHAH_Map component)
        {
            Thing hole = FindHole(map, component);
            if (hole != null && !hole.Destroyed)
            {
                hole.Destroy(DestroyMode.Vanish);
            }

            if (component != null)
            {
                component.HoleThingId = 0;
            }
        }

        static void TryPlace(Map map, MapComponent_RHAH_Map component, SuiyinBook book, SuiyinN006Case record, int tick)
        {
            if (record.IgnoreUntil >= 0 && tick >= record.IgnoreUntil)
            {
                Current.Game?.GetComponent<NarrativeState>()?.Commit(item => item.ChooseHole(record, SuiyinN006Action.Ignore, tick));
                RemoveHole(map, component);
                RemoveLetters(record.MapId);
                return;
            }

            Thing food = FirstFood(map);
            if (food == null || component == null)
            {
                return;
            }

            record.FoodPresent = true;
            if (!book.PlaceHole(record))
            {
                return;
            }

            IntVec3 cell;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(HoleDefName);
            if (def == null || !TryCell(map, food.PositionHeld, out cell))
            {
                record.Hole = false;
                return;
            }

            Thing hole = ThingMaker.MakeThing(def);
            GenSpawn.Spawn(hole, cell, map);
            component.HoleThingId = hole.thingIDNumber;
            record.NextLossTick = tick + DayTicks;
            OpenLetter(map, record, false);
        }

        static void TickOpen(Map map, MapComponent_RHAH_Map component, SuiyinBook book, SuiyinN006Case record, int tick)
        {
            if (!Refresh(map, record, tick))
            {
                return;
            }

            Thing hole = FindHole(map, component);
            if (record.Outcome == SuiyinN006Outcome.BaitSet && record.BaitUntil >= 0 && tick >= record.BaitUntil && !FollowOpen(map))
            {
                RemoveLetters(record.MapId);
                OpenLetter(map, record, true);
            }

            int maxLosses = Core.RHAH_Mod.Settings == null ? MaxLosses : Core.RHAH_Mod.Settings.holeMaxLosses;
            if (record.NextLossTick < 0 || tick < record.NextLossTick || record.Losses >= maxLosses)
            {
                return;
            }

            TakeFood(map, hole.Position, 1, Core.RHAH_Mod.Settings == null ? LossRange : Core.RHAH_Mod.Settings.holeLossRange);
            record.Losses++;
            record.NextLossTick += DayTicks;
        }

        internal static bool Refresh(Map map, SuiyinN006Case record, int tick)
        {
            NarrativeState state = Current.Game?.GetComponent<NarrativeState>();
            if (state == null || record == null || (record.Outcome != SuiyinN006Outcome.Pending && record.Outcome != SuiyinN006Outcome.BaitSet))
            {
                return false;
            }

            MapComponent_RHAH_Map component = map?.GetComponent<MapComponent_RHAH_Map>();
            if (record.Outcome == SuiyinN006Outcome.Pending && record.IgnoreUntil >= 0 && tick >= record.IgnoreUntil)
            {
                state.Commit(book => book.ChooseHole(record, SuiyinN006Action.Ignore, tick));
            }
            else if (map == null || (record.Hole && FindHole(map, component) == null))
            {
                state.Commit(book => book.LoseHole(record));
            }
            else
            {
                return record.Hole;
            }

            RemoveHole(map, component);
            RemoveLetters(record.MapId);
            return false;
        }

        internal static void ExpireMissingMaps(NarrativeState state, int tick)
        {
            for (int i = 0; state?.Book?.N006 != null && i < state.Book.N006.Count; i++)
            {
                SuiyinN006Case record = state.Book.N006[i];
                if (record == null || (record.Outcome != SuiyinN006Outcome.Pending && record.Outcome != SuiyinN006Outcome.BaitSet))
                {
                    continue;
                }

                bool found = false;
                for (int m = 0; Find.Maps != null && m < Find.Maps.Count; m++)
                {
                    if (Find.Maps[m]?.uniqueID == record.MapId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    state.Commit(book => book.LoseHole(record));
                    RemoveLetters(record.MapId);
                }
            }
        }

        internal static void RemoveLetters(int mapId)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            for (int i = letters == null ? -1 : letters.Count - 1; i >= 0; i--)
            {
                ChoiceLetter_RHAH_GrainHole letter = letters[i] as ChoiceLetter_RHAH_GrainHole;
                if (letter != null && letter.mapId == mapId)
                {
                    Find.LetterStack.RemoveLetter(letter);
                }
            }
        }

        static void OpenLetter(Map map, SuiyinN006Case record, bool follow)
        {
            if (Find.LetterStack == null)
            {
                return;
            }

            LetterDef def = DefDatabase<LetterDef>.GetNamedSilentFail(LetterDefName);
            if (def == null)
            {
                return;
            }

            ChoiceLetter_RHAH_GrainHole letter = (ChoiceLetter_RHAH_GrainHole)LetterMaker.MakeLetter(def);
            letter.mapId = map.uniqueID;
            letter.followUp = follow;
            string key = follow ? "RHAH_Suiyin_N006BaitGone" : "RHAH_Suiyin_N006Entry";
            letter.Label = (key + "_Label").Translate();
            letter.Text = follow
                ? (key + "_Text").Translate()
                : (key + "_Text").Translate(RHAH_Mod.Settings == null ? WoodCost : RHAH_Mod.Settings.holeWoodCost,
                    RHAH_Mod.Settings == null ? CleanPortions : RHAH_Mod.Settings.holeCleanPortions,
                    Current.Game.GetComponent<NarrativeState>().Book.Config.HoleIgnoreDays);
            Find.LetterStack.ReceiveLetter(letter);
        }

        static bool FollowOpen(Map map)
        {
            if (Find.LetterStack == null)
            {
                return false;
            }

            List<Letter> letters = Find.LetterStack.LettersListForReading;
            for (int i = 0; i < letters.Count; i++)
            {
                ChoiceLetter_RHAH_GrainHole letter = letters[i] as ChoiceLetter_RHAH_GrainHole;
                if (letter != null && letter.followUp && letter.mapId == map.uniqueID)
                {
                    return true;
                }
            }

            return false;
        }

        static Thing FirstFood(Map map)
        {
            List<Thing> foods = Foods(map);
            return foods.Count == 0 ? null : foods[0];
        }

        static List<Thing> Foods(Map map)
        {
            List<Thing> result = new List<Thing>();
            if (map?.listerThings == null)
            {
                return result;
            }

            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            if (things == null)
            {
                return result;
            }

            for (int i = 0; i < things.Count; i++)
            {
                if (Qualifies(things[i]))
                {
                    result.Add(things[i]);
                }
            }

            return result;
        }

        static bool Qualifies(Thing thing)
        {
            return thing != null &&
                thing.Spawned &&
                !thing.Destroyed &&
                thing.stackCount > 0 &&
                thing.def != null &&
                thing.def.IsNutritionGivingIngestible &&
                !thing.def.IsPlant &&
                thing.def.ingestible != null &&
                thing.def.ingestible.preferability >= FoodPreferability.MealAwful &&
                thing.IsInAnyStorage();
        }

        static bool InRange(Thing thing, IntVec3 origin, int range)
        {
            return Qualifies(thing) && thing.PositionHeld.InHorDistOf(origin, range);
        }

        static bool TryCell(Map map, IntVec3 food, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!map.cellIndices.Contains(food))
            {
                return false;
            }

            IntVec3 center = map.Center;
            int best = int.MaxValue;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(food, 3f, true))
            {
                if (!Fits(map, candidate))
                {
                    continue;
                }

                int score = (candidate - center).LengthHorizontalSquared;
                if (score < best)
                {
                    best = score;
                    cell = candidate;
                }
            }

            return cell.IsValid;
        }

        static bool Fits(Map map, IntVec3 cell)
        {
            return cell.InBounds(map) &&
                cell.Standable(map) &&
                cell.GetEdifice(map) == null &&
                !cell.GetThingList(map).Any(thing => thing.def != null && thing.def.defName == HoleDefName);
        }

        static Thing FindHole(Map map, MapComponent_RHAH_Map component)
        {
            if (map == null || component == null || component.HoleThingId <= 0)
            {
                return null;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(HoleDefName);
            if (def == null || map.listerThings == null)
            {
                return null;
            }

            List<Thing> holes = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < holes.Count; i++)
            {
                Thing hole = holes[i];
                if (hole != null && hole.thingIDNumber == component.HoleThingId && hole.Spawned && !hole.Destroyed)
                {
                    return hole;
                }
            }

            return null;
        }
    }
}
