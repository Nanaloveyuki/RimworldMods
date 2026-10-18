using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterNarrative
    {
        private bool narrativeDebugForce;
        internal static bool DebugForcing => Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.narrativeDebugForce == true;
        private bool NarrativeEnabled(string id) => narrativeDebugForce ||
            (MouseDisasterRuntime.AllowsNewContent && (MouseDisasterMod.Settings?.IsNarrativeEnabled(id) ?? true));

        internal static readonly string[] NarrativeDebugIds = {
            "N001", "N002", "N003", "N004", "N005", "N006", "N007", "N008", "N009", "N004Return", "N007Return",
            "E01", "E02", "E03", "E04", "E05", "R01", "S01", "S02", "S03", "S04", "S05", "S06", "S07",
            "S08", "S09", "S10", "S11", "S12", "S13", "S14", "Check", "Inspect"
        };

        private static readonly Dictionary<string, string> DebugIncidentIds = new Dictionary<string, string> {
            { "N004", "MouseDisaster_PlagueAbandonedBabies" }, { "N005", "MouseDisaster_ChildExchange" },
            { "N006", "MouseDisaster_ThiefRatkinGroup" }, { "N007", "MouseDisaster_PlagueBeggarGroup" },
            { "S01", "MouseDisaster_AirdropMistake" }, { "S02", "MouseDisaster_MisguidedKinship" },
            { "S03", "MouseDisaster_LaboringRefugees" }, { "S04", "MouseDisaster_Passersby" },
            { "S05", "MouseDisaster_ThiefRatkinGroup" }, { "S06", "MouseDisaster_LargeRefugeeWave" },
            { "S07", "MouseDisaster_Intel_Treasure_Silver" }, { "S08", "MouseDisaster_RatkinTraderCaravan" },
            { "S09", "MouseDisaster_StrongSiege" }, { "S10", "MouseDisaster_GreatFamine" },
            { "S11", "MouseDisaster_PlagueRevenge" }, { "S12", "MouseDisaster_CaravanMuggers" },
            { "S13", "MouseDisaster_WildRatkinWandersIn" }, { "S14", "MouseDisaster_Aid_SimpleMeal" }
        };

        public static void OpenNarrativeDebugMenu()
        {
            if (!Prefs.DevMode || Current.Game == null) return;
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                new FloatMenuOption("MouseDisaster_Story_DebugOriginal".Translate(), () => OpenIncidentDebugMenu(true)),
                new FloatMenuOption("MouseDisaster_Story_DebugContinued".Translate(), () => OpenIncidentDebugMenu(false)),
                new FloatMenuOption("MouseDisaster_Story_DebugNarratives".Translate(), () => Find.WindowStack.Add(
                    new FloatMenu(NarrativeDebugIds.Select(id => new FloatMenuOption(
                        ("MouseDisaster_Story_Debug" + id).Translate(), () => RunNarrativeDebug(id))).ToList())))
            }));
        }

        private static IEnumerable<MouseDisasterIncidentEntry> IncidentDebugEntries(bool original) =>
            MouseDisasterIncidentCatalog.AllEntries.Where(entry => entry.IsOriginal == original);

        private static void OpenIncidentDebugMenu(bool original)
        {
            Find.WindowStack.Add(new FloatMenu(IncidentDebugEntries(original).Select(entry =>
                new FloatMenuOption(entry.DisplayLabel, () => RunNarrativeDebug(entry.DefName))).ToList()));
        }

        [DebugAction("鼠灾事件", "剧情（强制触发）", allowedGameStates = AllowedGameStates.Playing)]
        private static DebugActionNode NarrativeDebugRoot()
        {
            var root = new DebugActionNode();
            foreach (bool original in new[] { true, false })
            {
                bool group = original;
                root.AddChild(new DebugActionNode((original ? "MouseDisaster_Story_DebugOriginal" : "MouseDisaster_Story_DebugContinued").Translate()) {
                    childGetter = () => IncidentDebugEntries(group).Select(entry => new DebugActionNode(
                        entry.DisplayLabel, DebugActionType.Action, () => RunNarrativeDebug(entry.DefName))).ToList()
                });
            }
            var stories = new DebugActionNode("MouseDisaster_Story_DebugNarratives".Translate());
            root.AddChild(stories);
            foreach (string id in NarrativeDebugIds)
            {
                string action = id;
                stories.AddChild(new DebugActionNode(("MouseDisaster_Story_Debug" + action).Translate(), DebugActionType.Action,
                    () => RunNarrativeDebug(action)));
            }
            return root;
        }

        internal static void RunNarrativeDebug(string id)
        {
            var component = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            if (component == null || !Prefs.DevMode) return;
            bool previous = component.narrativeDebugForce;
            try
            {
                component.narrativeDebugForce = id != "Check" && id != "Inspect";
                string result;
                bool ok = component.TryForceNarrative(id, out result);
                Messages.Message(result, ok ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
                Log.Message($"[MouseDisaster Narrative Debug] {id}: success={ok}; {result}");
            }
            catch (Exception exception)
            {
                Log.Error($"[MouseDisaster Narrative Debug] {id}: {exception}");
                Messages.Message("MouseDisaster_Story_DebugFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            finally { component.narrativeDebugForce = previous; }
        }

        private bool TryForceNarrative(string id, out string result)
        {
            result = "MouseDisaster_Story_DebugSuccess".Translate(id);
            if (id == "Inspect") { DebugInspectNarrative(); return true; }
            if (id == "Check")
            {
                ProcessN004RescueRewards(); ProcessN006(); ProcessN007(); ProcessStoryTasks(); ProcessNarrativeEndings();
                return true;
            }
            Map map = Find.CurrentMap;
            if (MouseDisasterIncidentCatalog.IsKnownIncident(id))
            {
                var entry = MouseDisasterIncidentCatalog.AllEntries.First(e => e.DefName == id);
                IIncidentTarget eventTarget = entry.TargetKind == MouseDisasterIncidentTargetKind.Caravan
                    ? (IIncidentTarget)Find.WorldObjects.Caravans.FirstOrDefault(c => c.IsPlayerControlled) : map;
                var eventDef = DefDatabase<IncidentDef>.GetNamedSilentFail(id);
                if (eventTarget == null || eventDef == null) { result = "MouseDisaster_Story_DebugSpawnFailed".Translate(); return false; }
                var eventParms = StorytellerUtility.DefaultParmsNow(eventDef.category, eventTarget);
                eventParms.points = entry.DebugPoints;
                bool executed = eventDef.Worker.TryExecute(eventParms);
                if (!executed) result = "MouseDisaster_Story_DebugSpawnFailed".Translate();
                return executed;
            }
            if (id.StartsWith("E") || id == "R01")
            {
                string key = id == "R01" ? "IdentityFull" : id;
                if (id.StartsWith("E"))
                    ShowNarrativeEnding(("MouseDisaster_Story_" + key + "_Label").Translate(),
                        ("MouseDisaster_Story_" + key + "_Text").Translate(aidCompleted, successfulBroadcasts, adultRatkinCount));
                else
                    ReceiveNarrativeLetterText(("MouseDisaster_Story_" + key + "_Label").Translate(),
                        ("MouseDisaster_Story_" + key + "_Text").Translate(), map);
                result = "MouseDisaster_Story_DebugPreview".Translate(id);
                return true;
            }
            if (id == "N004Return")
            {
                Caravan caravan = Find.WorldObjects.Caravans.FirstOrDefault(c => c.IsPlayerControlled);
                var record = n004Records.FirstOrDefault(r => r.outcome == MouseDisasterN004Outcome.FamilySeparated &&
                    !r.revisitTriggered && r.revisitDecision == MouseDisasterN004RevisitDecision.Pending && GetN004RevisitPawns(r).Count > 0);
                if (caravan == null || record == null) { result = "MouseDisaster_Story_DebugNeedReturn".Translate(); return false; }
                record.revisitTriggered = true;
                OpenN004RevisitDialog(caravan, record, GetN004RevisitPawns(record));
                return true;
            }
            if (id != "S12" && (map == null || !map.IsPlayerHome))
            { result = "MouseDisaster_Story_DebugNeedHome".Translate(); return false; }
            if (id == "N001")
            {
                openingLetterSent = true;
                narrativeChapter = Math.Max(narrativeChapter, 1);
                ReceiveNarrativeLetter("MouseDisaster_Narrative_OpeningLabel", "MouseDisaster_Narrative_OpeningText", map);
                return true;
            }
            if (id == "N002")
            {
                progressLetterSent = true;
                narrativeChapter = Math.Max(narrativeChapter, 2);
                ReceiveNarrativeLetter("MouseDisaster_Narrative_ProgressLabel", "MouseDisaster_Narrative_ProgressText", map, observedIncidentDefNames.Count);
                return true;
            }
            if (id == "N003")
            {
                if (hiddenRewardClaimed) { result = "MouseDisaster_Story_DebugAlready".Translate(); return false; }
                return TryGrantHiddenReward(map);
            }
            if (id == "N008")
            {
                if (envoyPhase != 0) { result = "MouseDisaster_Story_DebugAlready".Translate(); return false; }
                StartEnvoy(map);
                if (envoyPhase == 0) { result = "MouseDisaster_Story_DebugSpawnFailed".Translate(); return false; }
                return true;
            }
            if (id == "N009")
            {
                if (relicStarted) { result = "MouseDisaster_Story_DebugAlready".Translate(); return false; }
                if (!TryStartRelic(map)) { result = "MouseDisaster_Story_DebugSpawnFailed".Translate(); return false; }
                return true;
            }
            if (id == "N007Return")
            {
                if (returnPhase == 0)
                    foreach (var record in n007Records.Where(r => r.outcome == MouseDisasterN007Outcome.RecoveredAndLeft)) ScheduleRecoveredReturn(record);
                if (returnPhase != 1) { result = "MouseDisaster_Story_DebugNeedReturn".Translate(); return false; }
                returnDueTick = CurrentNarrativeTick;
                ProcessRecoveredReturn(map);
                if (returnPhase != 2) { result = "MouseDisaster_Story_DebugNeedReturn".Translate(); return false; }
                return true;
            }
            if (id == "N006")
            {
                var record = FindN006Record(map.uniqueID);
                if (record != null && record.phase != MouseDisasterN006Phase.Tracking)
                { result = "MouseDisaster_Story_DebugAlready".Translate(); return false; }
                if (!TryFindN006FoodCell(map, out _)) { result = "MouseDisaster_Story_DebugNeedFood".Translate(); return false; }
            }
            if (!DebugIncidentIds.TryGetValue(id, out string defName)) return false;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            IIncidentTarget target = id == "S12" ? (IIncidentTarget)Find.WorldObjects.Caravans.FirstOrDefault(c => c.IsPlayerControlled) : map;
            if (incident == null || target == null) { result = "MouseDisaster_Story_DebugSpawnFailed".Translate(); return false; }
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, target);
            parms.points = 300f;
            if (!incident.Worker.TryExecute(parms)) { result = "MouseDisaster_Story_DebugSpawnFailed".Translate(); return false; }
            if (id == "N006" && FindN006Record(map.uniqueID)?.phase != MouseDisasterN006Phase.Decision ||
                id == "N007" && FindActiveN007Record(map.uniqueID) == null)
            { result = "MouseDisaster_Story_DebugNoNarrative".Translate(); return false; }
            return true;
        }
    }
}
