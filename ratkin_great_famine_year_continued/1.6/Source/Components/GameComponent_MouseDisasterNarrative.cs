using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterNarrative : GameComponent
    {
        public const string NarratorDefName = "MouseDisaster_Suin";

        private const int ChapterProgressEventCount = 3;
        private const int HiddenRewardEventCount = 8;
        private const int HiddenRewardSilverCount = 300;

        private int narrativeChapter;
        private bool openingLetterSent;
        private bool progressLetterSent;
        private bool hiddenRewardClaimed;
        private List<string> observedIncidentDefNames = new List<string>();

        public GameComponent_MouseDisasterNarrative(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref narrativeChapter, "mouseDisaster_narrativeChapter", 0);
            Scribe_Values.Look(ref openingLetterSent, "mouseDisaster_narrativeOpeningLetterSent", false);
            Scribe_Values.Look(ref progressLetterSent, "mouseDisaster_narrativeProgressLetterSent", false);
            Scribe_Values.Look(ref hiddenRewardClaimed, "mouseDisaster_narrativeHiddenRewardClaimed", false);
            Scribe_Collections.Look(ref observedIncidentDefNames, "mouseDisaster_narrativeObservedIncidentDefNames", LookMode.Value);
            Scribe_Values.Look(ref narratorTrust, "mouseDisaster_narrativeTrust", 0);
            Scribe_Collections.Look(ref n004Records, "mouseDisaster_narrativeN004Records", LookMode.Deep);
            ExposeN005Data();
            ExposeN006Data();
            ExposeN007Data();
            ExposeJournalData();
            ExposeNarrativeAlerts();
            ExposeStoryTaskData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                observedIncidentDefNames ??= new List<string>();
                observedIncidentDefNames.RemoveAll(string.IsNullOrWhiteSpace);
                n004Records ??= new List<MouseDisasterN004Record>();
                n004Records.RemoveAll(record => record == null ||
                    (record.mother == null && record.outcome != MouseDisasterN004Outcome.FamilySeparated));
                for (int i = 0; i < n004Records.Count; i++)
                {
                    MouseDisasterN004Record record = n004Records[i];
                    if (record.outcome == MouseDisasterN004Outcome.FamilySeparated && record.revisitDeadlineTick < 0)
                    {
                        record.revisitDeadlineTick = (Find.TickManager?.TicksGame ?? record.createdTick) + N004RevisitDurationTicks;
                    }

                    if (record.outcome == MouseDisasterN004Outcome.FamilySeparated &&
                        record.revisitDecision == MouseDisasterN004RevisitDecision.Pending &&
                        !record.revisitTriggered)
                    {
                        RetainN004RevisitPawns(record);
                    }
                }
                if (hiddenRewardClaimed)
                {
                    narrativeChapter = Math.Max(narrativeChapter, 3);
                }
                else if (progressLetterSent)
                {
                    narrativeChapter = Math.Max(narrativeChapter, 2);
                }
            }
        }

        public void RecordIncidentExecuted(IncidentDef incidentDef, IncidentParms parms, List<Pawn> participants = null)
        {
            if (!MouseDisasterRuntime.AllowsNewContent || incidentDef == null || !MouseDisasterIncidentCatalog.IsKnownIncident(incidentDef.defName))
            {
                return;
            }

            NotifyN006TheftIncident(incidentDef, parms);
            NotifyN007PlagueIncident(incidentDef, parms, participants);
            TrackNarrativeVisit(MouseDisasterNarrativePolicy.Scene(incidentDef.defName), parms?.target as Map, participants);
            if (incidentDef.defName == "MouseDisaster_ChildExchange" && NarrativeEnabled("N005") && participants != null)
                foreach (Pawn pawn in participants)
                    if (MouseDisasterUtility.IsChildExchangeTrader(pawn) && !n005StartedTraderIds.Contains(pawn.thingIDNumber))
                        n005StartedTraderIds.Add(pawn.thingIDNumber);
            if (CountsNarrativeState && !ContainsObservedIncident(incidentDef.defName)) observedIncidentDefNames.Add(incidentDef.defName);
            ProcessOpeningNarrative(ResolvePlayerHomeMap(parms));
        }

        private void ProcessOpeningNarrative(Map map)
        {
            if (observedIncidentDefNames.Count == 0) return;
            if (!IsNarratorActive())
            {
                return;
            }

            if (!openingLetterSent && NarrativeEnabled("N001"))
            {
                openingLetterSent = true;
                narrativeChapter = Math.Max(narrativeChapter, 1);
                ReceiveNarrativeLetter(
                    "MouseDisaster_Narrative_OpeningLabel",
                    "MouseDisaster_Narrative_OpeningText",
                    map);
            }

            if (!progressLetterSent && NarrativeEnabled("N002") && observedIncidentDefNames.Count >= (MouseDisasterMod.Settings?.narrativeProgressGoal ?? ChapterProgressEventCount))
            {
                progressLetterSent = true;
                narrativeChapter = Math.Max(narrativeChapter, 2);
                ReceiveNarrativeLetter(
                    "MouseDisaster_Narrative_ProgressLabel",
                    "MouseDisaster_Narrative_ProgressText",
                    map,
                    observedIncidentDefNames.Count);
            }

            if (!hiddenRewardClaimed && NarrativeEnabled("N003") && observedIncidentDefNames.Count >= (MouseDisasterMod.Settings?.narrativeRewardGoal ?? HiddenRewardEventCount))
            {
                TryGrantHiddenReward(map);
            }
        }

        private static bool IsNarratorActive()
        {
            return DebugForcing || Find.Storyteller?.def?.defName == NarratorDefName;
        }

        private static bool CountsNarrativeState => IsNarratorActive() || MouseDisasterMod.Settings?.countWithoutSuin != false;

        private bool ContainsObservedIncident(string defName)
        {
            for (int i = 0; i < observedIncidentDefNames.Count; i++)
            {
                if (string.Equals(observedIncidentDefNames[i], defName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Map ResolvePlayerHomeMap(IncidentParms parms)
        {
            Map targetMap = parms?.target as Map;
            if (targetMap != null && targetMap.IsPlayerHome)
            {
                return targetMap;
            }

            return Find.AnyPlayerHomeMap ?? Find.CurrentMap;
        }

        private static void ReceiveNarrativeLetter(string labelKey, string textKey, Map map)
        {
            ReceiveNarrativeLetterText(
                labelKey.Translate().ToString(),
                textKey.Translate().ToString(),
                map);
        }

        private static void ReceiveNarrativeLetter(string labelKey, string textKey, Map map, int count)
        {
            ReceiveNarrativeLetterText(
                labelKey.Translate().ToString(),
                textKey.Translate(count).ToString(),
                map);
        }

        private static void ReceiveNarrativeLetterText(string label, string text, Map map)
        {
            var component = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            if (component == null) return;
            component.journalEntries.Add(new MouseDisasterJournalEntry { label = label, text = text });
            component.nextAlertRefresh = -1;
        }

        private bool TryGrantHiddenReward(Map map)
        {
            if (map == null)
            {
                return false;
            }

            Thing reward = ThingMaker.MakeThing(ThingDefOf.Silver);
            reward.stackCount = NarrativeReward(HiddenRewardSilverCount);
            List<Thing> payload = new List<Thing> { reward };
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                payload,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: true,
                forbid: false,
                allowFogged: true,
                faction: null);

            hiddenRewardClaimed = true;
            narrativeChapter = Math.Max(narrativeChapter, 3);
            Find.LetterStack.ReceiveLetter(
                "MouseDisaster_Narrative_RewardLabel".Translate(),
                "MouseDisaster_Narrative_RewardText".Translate(reward.stackCount),
                LetterDefOf.PositiveEvent,
                reward);
            return true;
        }
    }
}
