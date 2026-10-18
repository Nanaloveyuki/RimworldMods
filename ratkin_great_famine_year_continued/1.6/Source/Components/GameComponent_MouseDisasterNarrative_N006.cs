using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public enum MouseDisasterN006Phase
    {
        Tracking,
        Decision,
        BaitWaiting,
        BaitReady,
        IgnoredWaiting,
        Resolved
    }

    public enum MouseDisasterN006Outcome
    {
        Pending,
        Sealed,
        BaitTraced,
        BaitLost,
        ForceCleaned,
        LostClue
    }

    public enum MouseDisasterN006InitialDecision
    {
        Seal,
        LeaveBait,
        ForceClean,
        Ignore
    }

    public enum MouseDisasterN006BaitDecision
    {
        Trace,
        Stop
    }

    public enum MouseDisasterN006LetterStage
    {
        Entry,
        BaitFollowup
    }

    public sealed class MouseDisasterN006Record : IExposable
    {
        public int mapId = -1;
        public IntVec3 foodCell = IntVec3.Invalid;
        public int theftIncidentCount;
        public int createdTick;
        public int nextCheckTick = -1;
        public MouseDisasterN006Phase phase;
        public MouseDisasterN006Outcome outcome;
        public Thing burrow;
        public bool burrowSpawned;
        public int nextLossTick;
        public int losses;

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref foodCell, "foodCell", IntVec3.Invalid);
            Scribe_Values.Look(ref theftIncidentCount, "theftIncidentCount", 0);
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", -1);
            Scribe_Values.Look(ref phase, "phase", MouseDisasterN006Phase.Tracking);
            Scribe_Values.Look(ref outcome, "outcome", MouseDisasterN006Outcome.Pending);
            Scribe_References.Look(ref burrow, "burrow");
            Scribe_Values.Look(ref burrowSpawned, "burrowSpawned");
            Scribe_Values.Look(ref nextLossTick, "nextLossTick");
            Scribe_Values.Look(ref losses, "losses");
        }
    }

    public partial class GameComponent_MouseDisasterNarrative
    {
        private const int N006TheftIncidentThreshold = 2;
        private const int N006BaitWaitTicks = GenDate.TicksPerDay;
        private const int N006IgnoreWaitTicks = GenDate.TicksPerDay * 3;
        private static readonly HashSet<string> N006TheftIncidentDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MouseDisaster_ThiefRatkinGroup",
            "MouseDisaster_ThiefRatkinChildGroup",
            "MouseDisaster_PlagueThiefGroup"
        };

        private List<MouseDisasterN006Record> n006Records = new List<MouseDisasterN006Record>();

        private void ExposeN006Data()
        {
            Scribe_Collections.Look(ref n006Records, "mouseDisaster_narrativeN006Records", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                n006Records ??= new List<MouseDisasterN006Record>();
                n006Records.RemoveAll(record => record == null || record.mapId < 0);
            }
        }

        public void NotifyN006TheftIncident(IncidentDef incidentDef, IncidentParms parms)
        {
            if (!NarrativeEnabled("N006") || incidentDef == null ||
                !N006TheftIncidentDefNames.Contains(incidentDef.defName))
            {
                return;
            }

            Map map = parms?.target as Map;
            if (map == null || !map.IsPlayerHome || n006Records == null)
            {
                return;
            }

            MouseDisasterN006Record record = FindN006Record(map.uniqueID);
            if (record != null && record.phase != MouseDisasterN006Phase.Tracking)
            {
                return;
            }

            if (record == null)
            {
                record = new MouseDisasterN006Record
                {
                    mapId = map.uniqueID,
                    createdTick = CurrentNarrativeTick,
                    phase = MouseDisasterN006Phase.Tracking,
                    outcome = MouseDisasterN006Outcome.Pending
                };
                n006Records.Add(record);
            }

            record.theftIncidentCount++;
            if (TryFindN006FoodCell(map, out IntVec3 foodCell))
            {
                record.foodCell = foodCell;
            }

            if (record.theftIncidentCount < (narrativeDebugForce ? 1 : MouseDisasterMod.Settings?.narrativeTheftGoal ?? N006TheftIncidentThreshold) || !record.foodCell.IsValid)
            {
                return;
            }

            if (EnsureN006Burrow(record, map) && TrySendN006ChoiceLetter(record, map, MouseDisasterN006LetterStage.Entry))
            {
                record.phase = MouseDisasterN006Phase.Decision;
            }
        }

        public bool CanShowN006Letter(int mapId, MouseDisasterN006LetterStage stage)
        {
            MouseDisasterN006Record record = FindN006Record(mapId);
            if (record == null)
            {
                return false;
            }

            return stage == MouseDisasterN006LetterStage.Entry
                ? record.phase == MouseDisasterN006Phase.Decision
                : record.phase == MouseDisasterN006Phase.BaitReady;
        }

        public bool ResolveN006Initial(int mapId, MouseDisasterN006InitialDecision decision, out string message)
        {
            message = "MouseDisaster_N006_ActionFailed".Translate().ToString();
            MouseDisasterN006Record record = FindN006Record(mapId);
            if (record == null || record.phase != MouseDisasterN006Phase.Decision)
            {
                return false;
            }

            Map map = ResolveN006Map(record);
            if (map == null || !EnsureN006Burrow(record, map)) return false;
            switch (decision)
            {
                case MouseDisasterN006InitialDecision.Seal:
                    if (!TryConsumeNarrativeMaterial(map, ThingDefOf.WoodLog, 20))
                    { message = "MouseDisaster_Story_NoWood".Translate(); return false; }
                    ResolveN006(record, MouseDisasterN006Outcome.Sealed, map);
                    message = "MouseDisaster_N006_Sealed_Message".Translate().ToString();
                    return true;
                case MouseDisasterN006InitialDecision.LeaveBait:
                    if (map == null || !TryConsumeN006Bait(map, ref record.foodCell))
                    {
                        message = "MouseDisaster_N006_Bait_NoFood".Translate().ToString();
                        return false;
                    }

                    record.phase = MouseDisasterN006Phase.BaitWaiting;
                    record.nextCheckTick = CurrentNarrativeTick + N006BaitWaitTicks;
                    message = "MouseDisaster_N006_Bait_Message".Translate().ToString();
                    return true;
                case MouseDisasterN006InitialDecision.ForceClean:
                    for (int loss = 0; loss < 5; loss++) TryConsumeN006Bait(map, ref record.foodCell);
                    ResolveN006(record, MouseDisasterN006Outcome.ForceCleaned, map);
                    message = "MouseDisaster_N006_ForceCleaned_Message".Translate().ToString();
                    return true;
                case MouseDisasterN006InitialDecision.Ignore:
                    record.phase = MouseDisasterN006Phase.IgnoredWaiting;
                    record.nextCheckTick = CurrentNarrativeTick + N006IgnoreWaitTicks;
                    message = "MouseDisaster_N006_Ignore_Message".Translate().ToString();
                    return true;
                default:
                    return false;
            }
        }

        public bool ResolveN006Bait(int mapId, MouseDisasterN006BaitDecision decision, out string message)
        {
            message = "MouseDisaster_N006_ActionFailed".Translate().ToString();
            MouseDisasterN006Record record = FindN006Record(mapId);
            if (record == null || record.phase != MouseDisasterN006Phase.BaitReady)
            {
                return false;
            }

            Map map = ResolveN006Map(record);
            if (decision == MouseDisasterN006BaitDecision.Stop)
            {
                ResolveN006(record, MouseDisasterN006Outcome.BaitLost, map);
                message = "MouseDisaster_N006_BaitLost_Message".Translate().ToString();
                return true;
            }

            if (decision != MouseDisasterN006BaitDecision.Trace)
            {
                return false;
            }

            if (map == null || !MouseDisasterPhase2Utility.TryCreateIntelSite(
                    map,
                    MouseDisasterIntelSiteKind.Treasure,
                    out Site site,
                    out _))
            {
                ResolveN006(record, MouseDisasterN006Outcome.BaitLost, map);
                message = "MouseDisaster_N006_BaitLost_Message".Translate().ToString();
                return true;
            }

            if (!ResolveN006(record, MouseDisasterN006Outcome.BaitTraced, map))
            {
                Find.WorldObjects.Remove(site);
                return false;
            }

            Find.LetterStack.ReceiveLetter(
                "MouseDisaster_N006_BaitTraced_SiteLabel".Translate(),
                "MouseDisaster_N006_BaitTraced_SiteText".Translate(),
                LetterDefOf.PositiveEvent,
                site);
            message = "MouseDisaster_N006_BaitTraced_Message".Translate().ToString();
            return true;
        }

        private void ProcessN006()
        {
            if (n006Records == null || n006Records.Count == 0)
            {
                return;
            }

            int now = CurrentNarrativeTick;
            for (int i = 0; i < n006Records.Count; i++)
            {
                MouseDisasterN006Record record = n006Records[i];
                if (record == null || record.phase == MouseDisasterN006Phase.Resolved) continue;
                Map activeMap = ResolveN006Map(record);
                if (activeMap == null)
                {
                    ResolveN006(record, MouseDisasterN006Outcome.LostClue, null);
                    continue;
                }
                if (record.phase == MouseDisasterN006Phase.Tracking && NarrativeEnabled("N006") &&
                    record.theftIncidentCount >= (MouseDisasterMod.Settings?.narrativeTheftGoal ?? N006TheftIncidentThreshold) &&
                    TryFindN006FoodCell(activeMap, out IntVec3 foodCell))
                {
                    record.foodCell = foodCell;
                    record.phase = MouseDisasterN006Phase.Decision;
                    TrySendN006ChoiceLetter(record, activeMap, MouseDisasterN006LetterStage.Entry);
                }
                if (record.phase != MouseDisasterN006Phase.Tracking)
                {
                    if (!EnsureN006Burrow(record, activeMap))
                    { ResolveN006(record, MouseDisasterN006Outcome.LostClue, activeMap); continue; }
                    if (MouseDisasterRuntime.AllowsNewContent && now >= record.nextLossTick && record.losses < 3)
                    {
                        if (TryConsumeN006Bait(activeMap, ref record.foodCell)) record.losses++;
                        record.nextLossTick = now + GenDate.TicksPerDay;
                    }
                }
                if (record == null || record.nextCheckTick < 0 || now < record.nextCheckTick)
                {
                    continue;
                }

                Map map = ResolveN006Map(record);
                if (record.phase == MouseDisasterN006Phase.IgnoredWaiting)
                {
                    ResolveN006(record, MouseDisasterN006Outcome.LostClue, map);
                    continue;
                }

                if (record.phase != MouseDisasterN006Phase.BaitWaiting)
                {
                    record.nextCheckTick = -1;
                    continue;
                }

                if (map == null)
                {
                    continue;
                }

                if (!TrySendN006ChoiceLetter(record, map, MouseDisasterN006LetterStage.BaitFollowup))
                {
                    record.nextCheckTick = now + N006BaitWaitTicks;
                    continue;
                }

                record.phase = MouseDisasterN006Phase.BaitReady;
                record.nextCheckTick = -1;
            }
        }

        private bool TrySendN006ChoiceLetter(MouseDisasterN006Record record, Map map, MouseDisasterN006LetterStage stage)
        {
            if (record == null || map == null || MouseDisasterDefOf.MouseDisaster_N006Letter == null)
            {
                return false;
            }

            IntVec3 targetCell = record.foodCell.IsValid && record.foodCell.InBounds(map) ? record.foodCell : map.Center;
            string labelKey = stage == MouseDisasterN006LetterStage.Entry
                ? "MouseDisaster_N006_EntryLabel"
                : "MouseDisaster_N006_BaitReadyLabel";
            string textKey = stage == MouseDisasterN006LetterStage.Entry
                ? "MouseDisaster_N006_EntryText"
                : "MouseDisaster_N006_BaitReadyText";
            ChoiceLetter_MouseDisasterN006 letter = LetterMaker.MakeLetter(
                labelKey.Translate(),
                textKey.Translate(),
                MouseDisasterDefOf.MouseDisaster_N006Letter,
                new TargetInfo(targetCell, map)) as ChoiceLetter_MouseDisasterN006;
            if (letter == null)
            {
                Log.Error("[MouseDisaster] Could not create N-006 choice letter.");
                return false;
            }

            letter.map = map;
            letter.mapId = record.mapId;
            letter.stage = stage;
            Find.LetterStack.ReceiveLetter(letter, null);
            return true;
        }

        private bool ResolveN006(MouseDisasterN006Record record, MouseDisasterN006Outcome outcome, Map map)
        {
            if (record == null || record.phase == MouseDisasterN006Phase.Resolved)
            {
                return false;
            }

            record.outcome = outcome;
            CompleteNarrativeFlag("N006");
            if (outcome == MouseDisasterN006Outcome.BaitTraced) CompleteNarrativeFlag("N006Trace");
            if (record.burrow != null && !record.burrow.Destroyed) record.burrow.Destroy();
            record.phase = MouseDisasterN006Phase.Resolved;
            record.nextCheckTick = -1;
            ChangeNarratorTrust(TrustDeltaForN006Outcome(outcome));
            if (IsNarratorActive())
            {
                string suffix = outcome switch
                {
                    MouseDisasterN006Outcome.Sealed => "Sealed",
                    MouseDisasterN006Outcome.BaitTraced => "BaitTraced",
                    MouseDisasterN006Outcome.BaitLost => "BaitLost",
                    MouseDisasterN006Outcome.ForceCleaned => "ForceCleaned",
                    MouseDisasterN006Outcome.LostClue => "LostClue",
                    _ => "LostClue"
                };
                ReceiveNarrativeLetter(
                    "MouseDisaster_N006_" + suffix + "_Label",
                    "MouseDisaster_N006_" + suffix + "_Text",
                    map);
            }

            return true;
        }

        private static int TrustDeltaForN006Outcome(MouseDisasterN006Outcome outcome)
        {
            switch (outcome)
            {
                case MouseDisasterN006Outcome.Sealed:
                    return 1;
                case MouseDisasterN006Outcome.BaitTraced:
                    return 3;
                case MouseDisasterN006Outcome.ForceCleaned:
                    return -1;
                case MouseDisasterN006Outcome.BaitLost:
                case MouseDisasterN006Outcome.LostClue:
                    return 0;
                default:
                    return 0;
            }
        }

        private MouseDisasterN006Record FindN006Record(int mapId)
        {
            return n006Records?.FirstOrDefault(record => record != null && record.mapId == mapId);
        }

        private static Map ResolveN006Map(MouseDisasterN006Record record)
        {
            return Find.Maps?.FirstOrDefault(map => map != null && map.uniqueID == record?.mapId);
        }

        private static bool TryFindN006FoodCell(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map?.listerThings == null)
            {
                return false;
            }

            Thing food = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(thing => thing != null && thing.Spawned && thing.def?.category == ThingCategory.Item &&
                                thing.def.IsNutritionGivingIngestible && thing.IngestibleNow && thing.stackCount > 0 && thing.GetSlotGroup() != null)
                .OrderBy(thing => thing.Position.DistanceToSquared(map.Center))
                .FirstOrDefault();
            if (food == null)
            {
                return false;
            }

            cell = food.Position;
            return true;
        }

        private static bool EnsureN006Burrow(MouseDisasterN006Record record, Map map)
        {
            if (record.burrowSpawned) return record.burrow != null && !record.burrow.Destroyed;
            if (!MouseDisasterRuntime.AllowsNewContent || !record.foodCell.IsValid) return false;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(record.foodCell, map, 3);
            if (!cell.Standable(map) || cell.GetEdifice(map) != null) return false;
            record.burrow = GenSpawn.Spawn(MouseDisasterDefOf.MouseDisaster_GrainBurrow, cell, map);
            record.burrowSpawned = true;
            record.nextLossTick = Current.Game.tickManager.TicksGame + GenDate.TicksPerDay;
            return true;
        }

        private static bool TryConsumeNarrativeMaterial(Map map, ThingDef def, int count)
        {
            var stacks = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList();
            if (stacks.Sum(t => t.stackCount) < count) return false;
            foreach (var stack in stacks)
            {
                int take = System.Math.Min(count, stack.stackCount);
                stack.SplitOff(take).Destroy();
                count -= take;
                if (count == 0) break;
            }
            return true;
        }

        private static bool TryConsumeN006Bait(Map map, ref IntVec3 preferredCell)
        {
            if (map?.listerThings == null)
            {
                return false;
            }

            IntVec3 targetCell = preferredCell;
            List<Thing> foods = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(thing => thing != null && thing.Spawned && thing.def?.category == ThingCategory.Item &&
                                thing.def.IsNutritionGivingIngestible && thing.IngestibleNow && thing.stackCount > 0 &&
                                (!targetCell.IsValid || thing.Position.DistanceToSquared(targetCell) <= 144))
                .OrderBy(thing => targetCell.IsValid ? thing.Position.DistanceToSquared(targetCell) : thing.Position.DistanceToSquared(map.Center))
                .ToList();
            Thing bait = foods.FirstOrDefault();
            if (bait == null)
            {
                return false;
            }

            preferredCell = bait.Position;
            if (bait.stackCount > 1)
            {
                Thing split = bait.SplitOff(1);
                split?.Destroy(DestroyMode.Vanish);
            }
            else
            {
                bait.Destroy(DestroyMode.Vanish);
            }

            return true;
        }
    }
}
