extern alias iris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Data;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Storyteller.Suiyin;
using iris::IrisMenus;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal static class RHAH_IrisMenusCompat
    {
        internal const string PackageId = "Nanaloveyuki.IrisMenus";
        internal const string SupportedVersion = "1.6";

        static bool registered;

        internal static void TryRegister(RHAH_Mod owner)
        {
            if (registered || owner == null)
            {
                return;
            }

            ModMetaData meta = ModLister.GetActiveModWithIdentifier(PackageId, false);
            if (meta == null)
            {
                Log.Message("[RHAH] IrisMenus is not active. Menu pages were not registered.");
                return;
            }

            string version = string.IsNullOrEmpty(meta.ModVersion) ? "unspecified" : meta.ModVersion;
            bool supported = meta.SupportedVersionsReadOnly != null &&
                meta.SupportedVersionsReadOnly.Any(item => item != null && item.Major == 1 && item.Minor == 6);
            if (!supported)
            {
                Log.Warning("[RHAH] IrisMenus " + version + " does not declare RimWorld " +
                    SupportedVersion + " support. Menu pages were not registered.");
                return;
            }

            try
            {
                new RHAH_IrisMenusPages(meta.ModVersion).Register(owner);
                registered = true;
                Log.Message("[RHAH] IrisMenus " + version + " pages registered.");
            }
            catch (Exception exception)
            {
                Log.Error("[RHAH] IrisMenus registration failed. The mod will keep loading without menu pages.\n" +
                    exception);
            }
        }

        internal static void ResetForTests()
        {
            registered = false;
        }
    }

    internal sealed class RHAH_IrisMenusPages
    {
        const float ControlRow = 34f;
        const float IncidentMetaHeight = 42f;
        const float IncidentButtonHeight = 28f;

        readonly string irisVersion;
        readonly Dictionary<string, string> debugResults = new Dictionary<string, string>();
        readonly Dictionary<string, string> pointBuffers = new Dictionary<string, string>();
        readonly Dictionary<string, string> weightBuffers = new Dictionary<string, string>();
        string foodQuery = string.Empty;
        string giveFoodQuery = string.Empty;
        string begFoodQuery = string.Empty;
        string apparelQuery = string.Empty;
        string pendingApparelGroup;
        readonly HashSet<string> collapsedApparelGroups = new HashSet<string>();
        string pendingGiveFoodGroup;
        string pendingBegFoodGroup;
        string pendingFoodMod;
        readonly HashSet<string> collapsedFoodMods = new HashSet<string>();
        readonly HashSet<string> collapsedGiveFoodGroups = new HashSet<string>();
        readonly HashSet<string> collapsedBegFoodGroups = new HashSet<string>();
        string contentQuery = string.Empty;
        string pendingContentGroup;
        readonly HashSet<string> collapsedContentGroups = new HashSet<string>();

        string selectedPawnLabel = string.Empty;
        Game settlementGame;
        IReadOnlyList<RHAH_SettlementGroup> settlementModel;
        object settlementLanguage;
        readonly List<SettlementDay> settlementDays = new List<SettlementDay>();
        readonly HashSet<int> expandedSettlementDays = new HashSet<int>();
        readonly HashSet<SettlementEventKey> expandedSettlementEvents = new HashSet<SettlementEventKey>();
        RHAH_SettlementGroup selectedSettlementGroup;
        readonly List<SettlementRow> settlementRows = new List<SettlementRow>();
        RHAH_IrisMenusWidgets.Viewport settlementTreeViewport;
        RHAH_IrisMenusWidgets.Viewport settlementDetailViewport;

        internal RHAH_IrisMenusPages(string irisVersion)
        {
            this.irisVersion = irisVersion;
            ResetSettlementViewports();
            RHAH_Mod.SettingsReset += ResetBuffers;
        }

        void ResetBuffers()
        {
            pointBuffers.Clear();
            weightBuffers.Clear();
        }

        internal void Register(Mod owner)
        {
            RegisterPage(owner, "overview", "RHAH_Menu_Overview", DrawOverview, SearchOverview);
            RegisterPage(owner, "relief", "RHAH_Menu_Relief", DrawRelief, SearchRelief);
            RegisterPage(owner, "visitors", "RHAH_Menu_Visitors", DrawVisitors, SearchVisitors);
            RegisterPage(owner, "plague", "RHAH_Menu_Plague", DrawPlague, SearchPlague);
            RegisterPage(owner, "events", "RHAH_Menu_Events", DrawEvents, SearchCatalogEvents);
            RegisterPage(owner, "pawns", "RHAH_Menu_Pawns", DrawPawns, SearchPawns);
            RegisterPage(owner, "narrative", "RHAH_Menu_Narrative", DrawNarrative, SearchNarrative);
            MenuRegistry.RegisterSubItem(owner, "settlement-progress", () => "RHAH_Menu_Settlement".Translate(), DrawSettlement);
            MenuRegistry.RegisterSearchProvider(owner, "settlement-progress", SearchSettlement, FocusSettlement);
            RegisterPage(owner, "other", "RHAH_Menu_Other", DrawOther, SearchOther);
            RegisterPage(owner, "environment", "RHAH_Menu_Environment", DrawEnvironment, SearchEnvironment);
            RegisterPage(owner, "compat-diagnostics", "RHAH_Menu_CompatDiagnostics", DrawCompatDiagnostics, SearchCompatDiagnostics);
            RegisterPage(owner, "genes", "RHAH_Menu_Genes", DrawGenes, SearchGenes);
            RegisterPage(owner, "dev-events", "RHAH_Menu_DevEvents", DrawDevEvents, SearchDevEvents);
            RegisterPage(owner, "event-frequency", "RHAH_Menu_EventFrequency", DrawFrequency, SearchFrequency);
            RegisterPage(owner, "pawn-history", "RHAH_Menu_PawnHistory", DrawPawnHistory, SearchPawnHistory);
            RegisterPage(owner, "developer", "RHAH_Menu_Developer", DrawDeveloper, SearchDeveloper);
            RegisterPage(owner, "experimental", "RHAH_Menu_Experimental", DrawExperimental, SearchExperimental);
            RegisterPage(owner, "removal", "RHAH_Menu_Removal", DrawRemoval, SearchRemoval);
        }

        static void RegisterPage(
            Mod owner,
            string pageId,
            string titleKey,
            Action<Listing_Standard> draw,
            Func<IEnumerable<MenuSearchEntry>> search)
        {
            RHAH_IrisMenusWidgets.Viewport viewport = new RHAH_IrisMenusWidgets.Viewport(draw);
            MenuRegistry.RegisterSubItem(owner, pageId, () => titleKey.Translate(), viewport.Draw);
            MenuRegistry.RegisterSearchProvider(owner, pageId, search, viewport.Focus);
        }

        void DrawOverview(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Overview");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            MenuControls.Anchor(list, "enable-new-content");
            RHAH_IrisMenusWidgets.Checkbox(
                list,
                "RHAH_Settings_EnableNewContent".Translate(),
                ref settings.enableNewContent,
                "RHAH_Settings_EnableNewContent_Tooltip".Translate());

            MenuControls.Anchor(list, "reset-settings");
            RHAH_Mod.DrawResetSettings(list);
        }

        IEnumerable<MenuSearchEntry> SearchOverview()
        {
            yield return Entry("enable-new-content", "RHAH_Settings_EnableNewContent", "toggle content");
            yield return Entry("reset-settings", "RHAH_Settings_Reset", "reset defaults configuration");
        }
        void DrawRemoval(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Removal");
            GameComponent_RHAH_Game game = Current.ProgramState == ProgramState.Playing
                ? Current.Game?.GetComponent<GameComponent_RHAH_Game>()
                : null;
            if (game == null)
            {
                Empty(list, "RHAH_RemovalNoGame");
                return;
            }

            RHAH_IrisMenusWidgets.Label(list, "RHAH_RemovalSection".Translate());
            if (game.NewContentDisabled)
            {
                RHAH_IrisMenusWidgets.Label(list, "RHAH_RemovalDisabled".Translate());
            }
            else if (list.ButtonText("RHAH_RemovalDisable".Translate()))
            {
                game.DisableNewContent();
            }

            if (list.ButtonText("RHAH_RemovalExport".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RHAH_RemovalConfirm".Translate(),
                    () => LongEventHandler.QueueLongEvent(RHAH_SaveExport.Export, "SavingLongEvent", false, null)));
            }
        }

        IEnumerable<MenuSearchEntry> SearchRemoval()
        {
            yield return Entry("removal-disable", "RHAH_RemovalDisable", "save unload");
            yield return Entry("removal-export", "RHAH_RemovalExport", "backup clean save");
        }

        void DrawEvents(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Events");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                DrawIncident(list, RHAH_IncidentGroups.Scenarios(i)[0], false, settings);
            }
        }

        void DrawDevEvents(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_DevEvents");
            RHAH_Settings settings = RHAH_Mod.Settings;
            IReadOnlyList<RHAH_IncidentEntry> entries = RHAH_IncidentCatalog.All;
            for (int i = 0; i < entries.Count; i++)
            {
                DrawIncident(list, entries[i], true, settings);
            }
        }

        void DrawIncident(Listing_Standard list, RHAH_IncidentEntry entry, bool debug, RHAH_Settings settings)
        {
            string anchor = (debug ? "dev-" : "event-") + entry.DisplayId;
            float height = IncidentMetaHeight + RHAH_IrisMenusWidgets.CardPad * 2f;
            if (debug)
            {
                height += IncidentButtonHeight * 2f;
            }

            if (settings != null && !debug)
            {
                height += ControlRow * (entry.DisplayId == "I-051" ? 8f : 5f);
            }

            if (!RHAH_IrisMenusWidgets.Card(list, anchor, height, out Rect inner))
            {
                return;
            }
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f),
                debug ? "RHAH_Menu_Format_IdentifiedLabel".Translate(entry.DisplayId, entry.LabelKey.Translate()).ToString()
                    : RHAH_IncidentGroups.LabelKey(RHAH_IncidentGroups.GroupOf(entry.DisplayId)).Translate().ToString());
            Widgets.Label(new Rect(inner.x, inner.y + 20f, inner.width, 22f),
                "RHAH_Menu_EventMeta".Translate(
                    FamilyLabel(entry.Family),
                    OriginLabel(entry.Origin),
                    CategoryLabel(entry.Category),
                    TargetLabel(entry.Target),
                    entry.BroadcastEligible ? "RHAH_Menu_Yes".Translate() : "RHAH_Menu_No".Translate()));
            float cursor = inner.y + IncidentMetaHeight;
            if (debug)
            {
                float buttonWidth = Mathf.Min(160f, (inner.width - 8f) * 0.5f);
                Rect button = new Rect(inner.x, cursor, buttonWidth, 24f);
                if (Widgets.ButtonText(button, "RHAH_Menu_Trigger".Translate()))
                {
                    debugResults[entry.DisplayId] = Queue(entry);
                }

                Rect instant = new Rect(button.xMax + 8f, cursor, buttonWidth, 24f);
                if (Widgets.ButtonText(instant, "RHAH_Menu_TriggerInstant".Translate()))
                {
                    debugResults[entry.DisplayId] = SpawnInstant(entry);
                }
                TooltipHandler.TipRegion(instant, "RHAH_Menu_TriggerInstant_Tip".Translate());
                cursor += IncidentButtonHeight;

                string result;
                if (debugResults.TryGetValue(entry.DisplayId, out result))
                {
                    Widgets.Label(new Rect(inner.x, cursor, inner.width, 24f), result);
                }

                cursor += IncidentButtonHeight;
            }

            if (settings != null && !debug)
            {
                DrawIncidentControls(inner, cursor, entry, settings);
            }
        }

        void DrawIncidentControls(Rect inner, float y, RHAH_IncidentEntry entry, RHAH_Settings settings)
        {
            bool enabled = settings.IsIncidentEnabled(entry.DisplayId);
            Widgets.CheckboxLabeled(
                new Rect(inner.x, y, inner.width, 24f),
                "RHAH_Menu_EventEnabled".Translate(),
                ref enabled);
            settings.SetIncidentEnabled(entry.DisplayId, enabled);
            y += ControlRow;

            float points = settings.IncidentDebugPoints(entry.DisplayId, entry.DebugPoints);
            float previousPoints = points;
            string pointBuffer = Buffer(pointBuffers, entry.DisplayId, points, "0");
            points = RHAH_IrisMenusWidgets.TunedValue(
                new Rect(inner.x, y, inner.width, 30f),
                "RHAH_Menu_DebugPoints".Translate(),
                points,
                ref pointBuffer,
                RHAH_IncidentTuning.MinDebugPoints,
                RHAH_IncidentTuning.MaxDebugPoints,
                "0",
                "RHAH_Menu_DebugPoints_Tip".Translate());
            pointBuffers[entry.DisplayId] = pointBuffer;
            if (points != previousPoints) settings.SetIncidentDebugPoints(entry.DisplayId, points);
            y += ControlRow;

            float weight = settings.IncidentWeight(entry.DisplayId);
            string weightBuffer = Buffer(weightBuffers, entry.DisplayId, weight, "0");
            weight = RHAH_IrisMenusWidgets.TunedValue(
                new Rect(inner.x, y, inner.width, 30f),
                "RHAH_Menu_IncidentWeight".Translate(),
                weight,
                ref weightBuffer,
                RHAH_IncidentTuning.MinWeight,
                RHAH_IncidentTuning.MaxWeight,
                "0",
                "RHAH_Menu_IncidentWeight_Tip".Translate());
            weightBuffers[entry.DisplayId] = weightBuffer;
            settings.SetIncidentWeight(entry.DisplayId, weight);
            y += ControlRow;
            DrawAttitude(new Rect(inner.x, y, inner.width, 24f), entry, settings);
            y += ControlRow;
            DrawChain(new Rect(inner.x, y, inner.width, 24f), entry, settings);
            if (entry.DisplayId != "I-051")
            {
                return;
            }

            y += ControlRow;
            string chanceBuffer = Buffer(weightBuffers, "predation-chance", settings.refugeePredationChancePercent, "0");
            settings.refugeePredationChancePercent = RHAH_IrisMenusWidgets.TunedValue(
                new Rect(inner.x, y, inner.width, 30f),
                "RHAH_Settings_PredationChance".Translate(settings.refugeePredationChancePercent.ToString("0")),
                settings.refugeePredationChancePercent,
                ref chanceBuffer,
                0f,
                100f,
                "0",
                "RHAH_Settings_PredationChance_Tooltip".Translate());
            weightBuffers["predation-chance"] = chanceBuffer;
            y += ControlRow;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 24f), "RHAH_Settings_PredationFightBack".Translate(), ref settings.refugeePredationFightBack);
            TooltipHandler.TipRegion(new Rect(inner.x, y, inner.width, 24f), "RHAH_Settings_PredationFightBack_Tooltip".Translate());
            y += ControlRow;
            Widgets.CheckboxLabeled(new Rect(inner.x, y, inner.width, 24f), "RHAH_Settings_PredationFollowDifficulty".Translate(), ref settings.outsidePredatorsFollowDifficulty);
            TooltipHandler.TipRegion(new Rect(inner.x, y, inner.width, 24f), "RHAH_Settings_PredationFollowDifficulty_Tooltip".Translate());
        }

        static void DrawAttitude(Rect row, RHAH_IncidentEntry entry, RHAH_Settings settings)
        {
            RHAH_Attitude attitude = settings.IncidentAttitude(entry.DisplayId, entry.DefaultAttitude);
            string label = "RHAH_Menu_Format_LabelValue".Translate("RHAH_Menu_Attitude".Translate(), AttitudeLabel(attitude));
            if (Widgets.ButtonText(row, label))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                RHAH_Attitude[] values =
                {
                    RHAH_Attitude.Hostile,
                    RHAH_Attitude.LeaningHostile,
                    RHAH_Attitude.Neutral,
                    RHAH_Attitude.LeaningFriendly,
                    RHAH_Attitude.Friendly
                };
                for (int i = 0; i < values.Length; i++)
                {
                    RHAH_Attitude next = values[i];
                    options.Add(new FloatMenuOption(AttitudeLabel(next), () => settings.SetIncidentAttitude(entry.DisplayId, next)));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            TooltipHandler.TipRegion(row, "RHAH_Menu_Attitude_Tip".Translate());
        }

        static void DrawChain(Rect row, RHAH_IncidentEntry entry, RHAH_Settings settings)
        {
            bool longChain = settings.IncidentUsesLongChain(entry.DisplayId);
            string label = "RHAH_Menu_Format_LabelValue".Translate("RHAH_Menu_Chain".Translate(), longChain ? "RHAH_Menu_Chain_Long".Translate() : "RHAH_Menu_Chain_Short".Translate());
            if (Widgets.ButtonText(row, label))
            {
                settings.SetIncidentLongChain(entry.DisplayId, !longChain);
            }

            TooltipHandler.TipRegion(row, "RHAH_Menu_Chain_Tip".Translate());
        }

        static string AttitudeLabel(RHAH_Attitude attitude)
        {
            switch (attitude)
            {
                case RHAH_Attitude.Hostile:
                    return "RHAH_Menu_Attitude_Hostile".Translate();
                case RHAH_Attitude.LeaningHostile:
                    return "RHAH_Menu_Attitude_LeaningHostile".Translate();
                case RHAH_Attitude.LeaningFriendly:
                    return "RHAH_Menu_Attitude_LeaningFriendly".Translate();
                case RHAH_Attitude.Friendly:
                    return "RHAH_Menu_Attitude_Friendly".Translate();
                default:
                    return "RHAH_Menu_Attitude_Neutral".Translate();
            }
        }

        static string Buffer(Dictionary<string, string> buffers, string id, float value, string format)
        {
            string buffer;
            if (buffers.TryGetValue(id, out buffer))
            {
                return buffer;
            }

            return value.ToString(format);
        }

        static string Queue(RHAH_IncidentEntry entry)
        {
            if (!RHAH_Runtime.AllowsNewContent)
            {
                return "RHAH_Menu_Queue_Disabled".Translate();
            }

            if (Current.Game == null)
            {
                return "RHAH_Menu_Queue_NoGame".Translate();
            }

            if (entry.Target == RHAH_IncidentTarget.Map && RHAH_MapResolver.Resolve() == null)
            {
                return "RHAH_Menu_Queue_NoMap".Translate();
            }

            if (entry.Target == RHAH_IncidentTarget.Caravan &&
                Caravan.CaravanTargetResolver.ResolvePlayerCaravan() == null)
            {
                return "RHAH_Menu_Queue_NoCaravan".Translate();
            }

            if (RHAH_Scheduler.QueueDebugIncident(entry.DisplayId))
            {
                GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
                return game != null && game.PendingIncidentDisplayIds.Contains(entry.DisplayId)
                    ? "RHAH_Menu_Queue_Queued".Translate()
                    : "RHAH_Menu_Queue_Fired".Translate();
            }

            return "RHAH_Menu_Queue_Failed".Translate();
        }

        static string SpawnInstant(RHAH_IncidentEntry entry)
        {
            if (Current.Game == null)
            {
                return "RHAH_Menu_Queue_NoGame".Translate();
            }

            if (!RHAH_Runtime.AllowsNewContent)
            {
                return "RHAH_Menu_Queue_Disabled".Translate();
            }

            Map map = RHAH_MapResolver.Resolve(Find.CurrentMap);
            if (map == null)
            {
                return "RHAH_Menu_Queue_NoMap".Translate();
            }

            return RHAH_Scheduler.SpawnDebugIncidentOnMap(entry.DisplayId, map)
                ? "RHAH_Menu_Queue_Fired".Translate()
                : "RHAH_Menu_Instant_Failed".Translate();
        }

        static IEnumerable<MenuSearchEntry> SearchCatalogEvents()
        {
            return SearchIncidentEntries("event-", string.Empty);
        }

        static IEnumerable<MenuSearchEntry> SearchDevEvents()
        {
            return SearchIncidentEntries("dev-", "debug ");
        }

        static IEnumerable<MenuSearchEntry> SearchIncidentEntries(string anchorPrefix, string keywordPrefix)
        {
            IReadOnlyList<RHAH_IncidentEntry> entries = RHAH_IncidentCatalog.All;
            for (int i = 0; i < entries.Count; i++)
            {
                RHAH_IncidentEntry entry = entries[i];
                int group = RHAH_IncidentGroups.GroupOf(entry.DisplayId);
                if (anchorPrefix == "event-" && (group < 0 || RHAH_IncidentGroups.Scenarios(group)[0] != entry))
                {
                    continue;
                }
                string keywords = keywordPrefix + entry.DefName + " " + entry.DisplayId;
                if (anchorPrefix == "event-")
                {
                    IReadOnlyList<RHAH_IncidentEntry> scenarios = RHAH_IncidentGroups.Scenarios(group);
                    for (int j = 1; j < scenarios.Count; j++)
                    {
                        keywords += " " + scenarios[j].DefName + " " + scenarios[j].DisplayId;
                    }
                }
                yield return new MenuSearchEntry(
                    anchorPrefix + entry.DisplayId,
                    () => anchorPrefix == "event-" ? RHAH_IncidentGroups.LabelKey(group).Translate().ToString()
                        : "RHAH_Menu_Format_IdentifiedLabel".Translate(entry.DisplayId, entry.LabelKey.Translate()).ToString(),
                    () => keywords,
                    () => "RHAH_Menu_Format_IncidentContext".Translate(FamilyLabel(entry.Family), CategoryLabel(entry.Category)));
            }
        }

        void DrawPawns(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Pawns");
            Map map = Find.CurrentMap;
            if (map == null || map.mapPawns == null)
            {
                Empty(list, "RHAH_Menu_Pawns_NoMap");
                return;
            }

            IReadOnlyList<Verse.Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            bool any = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (pawn == null || pawn.Destroyed || !RHAH_Api.IsOrigin(pawn))
                {
                    continue;
                }

                any = true;
                DrawPawn(list, pawn, RHAH_Api.Get(pawn));
            }

            if (!any)
            {
                Empty(list, "RHAH_Menu_Pawns_None");
            }
        }

        void DrawPawn(Listing_Standard list, Verse.Pawn pawn, IRHAH_Pawn snapshot)
        {
            if (pawn == null || pawn.Destroyed || snapshot == null)
            {
                Empty(list, "RHAH_Menu_Pawns_Invalid");
                return;
            }

            string anchor = "pawn-" + pawn.thingIDNumber;
            if (!RHAH_IrisMenusWidgets.Card(list, anchor, 68f, out Rect inner))
            {
                return;
            }
            string name = pawn.LabelShort;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), name);
            Widgets.Label(new Rect(inner.x, inner.y + 20f, inner.width, 22f),
                "RHAH_Menu_PawnMeta".Translate(
                    RoleLabel(snapshot.Role),
                    LifecycleLabel(snapshot.Lifecycle),
                    snapshot.SourceIncidentDisplayId ?? "RHAH_Menu_None".Translate()));
            Widgets.Label(new Rect(inner.x, inner.y + 40f, inner.width, 22f),
                "RHAH_Menu_PawnRelation".Translate(
                    snapshot.RelationshipGroupId,
                    snapshot.ParentPawnLoadId,
                    snapshot.ChildPawnLoadIds == null ? 0 : snapshot.ChildPawnLoadIds.Count,
                    snapshot.HasBeenFed ? "RHAH_Menu_Yes".Translate() : "RHAH_Menu_No".Translate(),
                    snapshot.CarriesPlague ? "RHAH_Menu_Yes".Translate() : "RHAH_Menu_No".Translate()));
            selectedPawnLabel = name;
        }

        IEnumerable<MenuSearchEntry> SearchPawns()
        {
            yield return Entry("pawns-list", "RHAH_Menu_Pawns");
        }

        void DrawNarrative(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Narrative");
            MenuControls.Anchor(list, "narrative-kingdom");
            Section(list, "RHAH_Menu_Narrative_Kingdom");
            Note(list, "RHAH_Settings_Kingdom_Summary");
            KingdomArcRuntime.DrawEntry(list);
            list.Gap(6f);

            if (Current.Game == null)
            {
                Empty(list, "RHAH_Menu_Narrative_NoGame");
                return;
            }

            NarrativeState state = Current.Game.GetComponent<NarrativeState>();
            if (state == null)
            {
                Empty(list, "RHAH_Menu_Unavailable");
                return;
            }

            NarrativeSnapshot snapshot = state.Snapshot();
            if (snapshot == null)
            {
                Empty(list, "RHAH_Menu_Unavailable");
                return;
            }

            Status(list, "RHAH_Menu_Narrative_Revealed", snapshot.RevealedCount.ToString());
            Status(list, "RHAH_Menu_Narrative_Trust", snapshot.Trust.ToString());
            Status(list, "RHAH_Menu_Narrative_Rescued", snapshot.Rescued.ToString());
            Status(list, "RHAH_Menu_Narrative_Lost", snapshot.Lost.ToString());
            Status(list, "RHAH_Menu_Narrative_Failed",
                snapshot.Failed ? "RHAH_Menu_Yes".Translate() : "RHAH_Menu_No".Translate());
            Status(list, "RHAH_Menu_Narrative_Outcome", OutcomeLabel(state.CalculateOutcome()));
            Section(list, "RHAH_Menu_Narrative_Nodes");
            DrawNode(list, state, SuiyinNode.N001, "N-001");
            DrawNode(list, state, SuiyinNode.N002, "N-002");
            DrawNode(list, state, SuiyinNode.N003, "N-003");
            DrawNode(list, state, SuiyinNode.N004, "N-004");
            DrawNode(list, state, SuiyinNode.N005, "N-005");
            DrawNode(list, state, SuiyinNode.N006, "N-006");
            DrawNode(list, state, SuiyinNode.N008, "N-008");
            DrawNode(list, state, SuiyinNode.N009, "N-009");
            Section(list, "RHAH_Menu_Narrative_Thresholds");
            RHAH_Settings narrative = RHAH_Mod.Settings;
            if (narrative != null)
            {
                DrawNarrativeNumbers(list, narrative);
                MenuControls.Anchor(list, "narrative-identity");
                RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Ending_Identity".Translate(), ref narrative.endingIdentity, "RHAH_Ending_Identity_Tooltip".Translate());
                narrative.endingTrustFloor = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "identity-partial", "RHAH_Settings_IdentityPartialTrust".Translate(narrative.endingTrustFloor), narrative.endingTrustFloor, -100f, 100f, "RHAH_Settings_IdentityPartialTrust_Tooltip".Translate());
                narrative.endingHopeTrust = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "identity-full", "RHAH_Settings_IdentityFullTrust".Translate(narrative.endingHopeTrust), narrative.endingHopeTrust, narrative.endingTrustFloor, 100f, "RHAH_Settings_IdentityFullTrust_Tooltip".Translate());
                narrative.CopyNarrative(state.Book.Config);
            }
            Section(list, "RHAH_Menu_Narrative_Journals");
            for (int journal = 1; journal <= 14; journal++)
            {
                string label = "RHAH_Suiyin_Journal_Label".Translate(journal);
                bool known = RHAH_JournalRuntime.HasJournal(journal);
                if (!known)
                {
                    RHAH_IrisMenusWidgets.Label(list, label);
                    continue;
                }

                if (list.ButtonText(label))
                {
                    Find.LetterStack.ReceiveLetter(
                        label,
                        ("RHAH_Journal_" + journal).Translate(),
                        LetterDefOf.NeutralEvent);
                }
            }

            int cutoff = RHAH_Mod.Settings == null ? -75 : RHAH_Mod.Settings.narrativeAsideCutoff;
            if (snapshot.Trust <= cutoff)
            {
                Note(list, "RHAH_Menu_Narrative_TrustClosed");
            }
        }

        IEnumerable<MenuSearchEntry> SearchNarrative()
        {
            yield return new MenuSearchEntry("narrative-kingdom",
                () => "RHAH_Menu_Narrative_Kingdom".Translate(),
                context: () => "RHAH_Settings_Kingdom_Summary".Translate());
            yield return Entry("narrative-outcome", "RHAH_Menu_Narrative_Outcome");
            yield return Entry("narrative-nodes", "RHAH_Menu_Narrative_Nodes");
            yield return Entry("narrative-thresholds", "RHAH_Menu_Narrative_Thresholds");
            yield return Entry("narrative-identity", "RHAH_Ending_Identity");
            yield return Entry("identity-partial", "RHAH_Settings_IdentityPartialTrust");
            yield return Entry("identity-full", "RHAH_Settings_IdentityFullTrust");
            yield return Entry("narrative-journals", "RHAH_Menu_Narrative_Journals");
        }

        void DrawSettlement(Rect rect)
        {
            SyncSettlementGame();
            NarrativeState state = Current.ProgramState == ProgramState.Playing
                ? Current.Game?.GetComponent<NarrativeState>()
                : null;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            try
            {
                string title = "RHAH_Menu_Settlement".Translate();
                string refreshLabel = "RHAH_Settlement_Refresh".Translate();
                bool stackedHeader = rect.width < 420f;
                float refreshWidth = stackedHeader ? rect.width : Mathf.Min(210f, rect.width * 0.36f);
                float titleWidth = stackedHeader ? rect.width : rect.width - refreshWidth - 12f;
                float titleHeight = Text.CalcHeight(title, Mathf.Max(1f, titleWidth));
                float refreshHeight = Mathf.Max(34f, Text.CalcHeight(refreshLabel, Mathf.Max(1f, refreshWidth - 16f)) + 8f);
                float headerHeight = stackedHeader ? titleHeight + 4f + refreshHeight : Mathf.Max(titleHeight, refreshHeight);
                Widgets.Label(new Rect(rect.x, rect.y, titleWidth, titleHeight), title);
                Rect refresh = new Rect(stackedHeader ? rect.x : rect.xMax - refreshWidth,
                    stackedHeader ? rect.y + titleHeight + 4f : rect.y, refreshWidth, refreshHeight);
                if (state != null && Widgets.ButtonText(refresh, refreshLabel))
                {
                    RHAH_SettlementProgress.Invalidate(state);
                }

                if (state != null)
                {
                    CacheSettlementTree(RHAH_SettlementProgress.Read(state, Find.TickManager?.TicksGame ?? 0));
                }
                else if (settlementModel != null)
                {
                    ClearSettlementNavigation();
                }

                float bodyY = rect.y + headerHeight + 8f;
                float bodyHeight = Mathf.Max(0f, rect.yMax - bodyY);
                float gap = Mathf.Min(12f, rect.width * 0.04f);
                float availableWidth = Mathf.Max(2f, rect.width - gap);
                float sidebarWidth = availableWidth >= 480f
                    ? Mathf.Clamp(availableWidth * 0.34f, 180f, 330f)
                    : availableWidth * 0.4f;
                Rect sidebar = new Rect(rect.x, bodyY, sidebarWidth, bodyHeight);
                Rect detail = new Rect(sidebar.xMax + gap, bodyY, availableWidth - sidebarWidth, bodyHeight);
                string treeHeading = "RHAH_Settlement_TreeHeading".Translate();
                string detailHeading = "RHAH_Settlement_DetailHeading".Translate();
                float paneHeadingHeight = Mathf.Max(30f, Mathf.Max(
                    Text.CalcHeight(treeHeading, Mathf.Max(1f, sidebar.width)),
                    Text.CalcHeight(detailHeading, Mathf.Max(1f, detail.width))) + 6f);
                Widgets.Label(new Rect(sidebar.x, sidebar.y, sidebar.width, paneHeadingHeight), treeHeading);
                Widgets.Label(new Rect(detail.x, detail.y, detail.width, paneHeadingHeight), detailHeading);
                Widgets.DrawLineVertical(sidebar.xMax + gap * 0.5f, bodyY, bodyHeight);
                float viewportHeight = Mathf.Max(0f, bodyHeight - paneHeadingHeight);
                if (viewportHeight > 0f)
                {
                    settlementTreeViewport.Draw(new Rect(sidebar.x, sidebar.y + paneHeadingHeight, sidebar.width, viewportHeight));
                    settlementDetailViewport.Draw(new Rect(detail.x, detail.y + paneHeadingHeight, detail.width, viewportHeight));
                }
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
            }
        }

        IEnumerable<MenuSearchEntry> SearchSettlement()
        {
            yield return new MenuSearchEntry("settlement-progress-tree",
                () => "RHAH_Menu_Settlement".Translate(),
                () => "RHAH_Settlement_SearchTerms".Translate(),
                context: () => "RHAH_Settlement_Note".Translate());
        }

        void SyncSettlementGame()
        {
            if (!ReferenceEquals(settlementGame, Current.Game))
            {
                settlementGame = Current.Game;
                ClearSettlementNavigation();
            }
        }

        void ClearSettlementNavigation()
        {
            settlementModel = null;
            settlementLanguage = null;
            settlementDays.Clear();
            expandedSettlementDays.Clear();
            expandedSettlementEvents.Clear();
            selectedSettlementGroup = null;
            settlementRows.Clear();
            ResetSettlementViewports();
        }

        void ResetSettlementViewports()
        {
            settlementTreeViewport = new RHAH_IrisMenusWidgets.Viewport(DrawSettlementTree);
            settlementDetailViewport = new RHAH_IrisMenusWidgets.Viewport(DrawSettlementDetails);
        }

        void FocusSettlement(string anchor)
        {
            SyncSettlementGame();
            settlementTreeViewport.Focus(anchor);
        }

        void CacheSettlementTree(IReadOnlyList<RHAH_SettlementGroup> groups)
        {
            if (ReferenceEquals(settlementModel, groups) &&
                ReferenceEquals(settlementLanguage, LanguageDatabase.activeLanguage))
            {
                return;
            }

            RHAH_SettlementGroup selection = FindSettlementSelection(groups, selectedSettlementGroup);
            bool selectionRemoved = selectedSettlementGroup != null && selection == null;
            settlementModel = groups;
            settlementLanguage = LanguageDatabase.activeLanguage;
            settlementDays.Clear();
            Dictionary<int, SettlementDay> days = new Dictionary<int, SettlementDay>();
            Dictionary<SettlementEventKey, SettlementEvent> events = new Dictionary<SettlementEventKey, SettlementEvent>();
            for (int i = 0; i < groups.Count; i++)
            {
                RHAH_SettlementGroup group = groups[i];
                int day = SettlementRecordedDay(group.StartedTick);
                if (!days.TryGetValue(day, out SettlementDay date))
                {
                    date = new SettlementDay(day);
                    days.Add(day, date);
                    settlementDays.Add(date);
                }

                SettlementEventKey eventKey = new SettlementEventKey(day,
                    RHAH_IncidentCatalog.GetByDisplayId(group.SourceDisplayId)?.DisplayId);
                if (!events.TryGetValue(eventKey, out SettlementEvent incident))
                {
                    incident = new SettlementEvent(eventKey, BuildSettlementSourceText(group));
                    events.Add(eventKey, incident);
                    date.Events.Add(incident);
                }

                incident.Groups.Add(new SettlementLeaf(group, BuildSettlementLeafText(group)));
            }

            settlementDays.Sort((left, right) => right.Day.CompareTo(left.Day));
            expandedSettlementDays.RemoveWhere(day => !days.ContainsKey(day));
            expandedSettlementEvents.RemoveWhere(key => !events.ContainsKey(key));
            selectedSettlementGroup = selection;
            CacheSelectedSettlementRows();
            if (selectionRemoved)
            {
                settlementDetailViewport = new RHAH_IrisMenusWidgets.Viewport(DrawSettlementDetails);
            }
        }

        static int SettlementRecordedDay(int startedTick)
        {
            return Math.Max(0, startedTick) / GenDate.TicksPerDay + 1;
        }

        static RHAH_SettlementGroup FindSettlementSelection(IReadOnlyList<RHAH_SettlementGroup> groups,
            RHAH_SettlementGroup selected)
        {
            if (selected != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    if (SameSettlementGroup(groups[i], selected))
                    {
                        return groups[i];
                    }
                }
            }

            return null;
        }

        static bool SameSettlementGroup(RHAH_SettlementGroup left, RHAH_SettlementGroup right)
        {
            return left != null && right != null && left.Kind == right.Kind && left.Id == right.Id &&
                left.BatchId == right.BatchId && left.MapId == right.MapId;
        }

        void SelectSettlementGroup(RHAH_SettlementGroup group)
        {
            if (SameSettlementGroup(group, selectedSettlementGroup))
            {
                return;
            }

            selectedSettlementGroup = group;
            CacheSelectedSettlementRows();
            settlementDetailViewport = new RHAH_IrisMenusWidgets.Viewport(DrawSettlementDetails);
        }

        void CacheSelectedSettlementRows()
        {
            settlementRows.Clear();
            RHAH_SettlementGroup group = selectedSettlementGroup;
            if (group == null)
            {
                return;
            }

            settlementRows.Add(new SettlementRow("settlement-detail-group", BuildSettlementGroupText(group)));
            if (group.Kind == RHAH_SettlementKind.Entrust)
            {
                settlementRows.Add(new SettlementRow("settlement-detail-mother", BuildSettlementMotherText(group), 8f));
            }

            for (int i = 0; i < group.Members.Count; i++)
            {
                settlementRows.Add(new SettlementRow("settlement-detail-member-" + i,
                    BuildSettlementMemberText(group, group.Members[i]), 8f));
            }
        }

        void DrawSettlementTree(Listing_Standard list)
        {
            MenuControls.Anchor(list, "settlement-progress-tree");
            if (settlementModel == null || settlementModel.Count == 0)
            {
                RHAH_IrisMenusWidgets.Quote(list, "settlement-tree-empty",
                    (settlementModel == null ? "RHAH_Settlement_NoGame" : "RHAH_Settlement_None").Translate());
                return;
            }

            for (int dayIndex = 0; dayIndex < settlementDays.Count; dayIndex++)
            {
                SettlementDay day = settlementDays[dayIndex];
                bool dayOpen = expandedSettlementDays.Contains(day.Day);
                if (DrawSettlementTreeRow(list, day.Label, 0, dayOpen, false, true))
                {
                    if (dayOpen) expandedSettlementDays.Remove(day.Day);
                    else expandedSettlementDays.Add(day.Day);
                    dayOpen = !dayOpen;
                }

                if (!dayOpen)
                {
                    continue;
                }

                for (int eventIndex = 0; eventIndex < day.Events.Count; eventIndex++)
                {
                    SettlementEvent incident = day.Events[eventIndex];
                    bool eventOpen = expandedSettlementEvents.Contains(incident.Key);
                    if (DrawSettlementTreeRow(list, incident.Label, 1, eventOpen, false, true))
                    {
                        if (eventOpen) expandedSettlementEvents.Remove(incident.Key);
                        else expandedSettlementEvents.Add(incident.Key);
                        eventOpen = !eventOpen;
                    }

                    if (!eventOpen)
                    {
                        continue;
                    }

                    for (int groupIndex = 0; groupIndex < incident.Groups.Count; groupIndex++)
                    {
                        SettlementLeaf leaf = incident.Groups[groupIndex];
                        if (DrawSettlementTreeRow(list, leaf.Label, 2, false,
                            SameSettlementGroup(leaf.Group, selectedSettlementGroup), false))
                        {
                            SelectSettlementGroup(leaf.Group);
                        }
                    }
                }
            }
        }

        static bool DrawSettlementTreeRow(Listing_Standard list, SettlementTreeLabel label, int depth,
            bool open, bool selected, bool fold)
        {
            float indent = Mathf.Min(depth * 14f, list.ColumnWidth * 0.12f);
            float markerWidth = Mathf.Min(24f, list.ColumnWidth * 0.15f);
            float labelWidth = Mathf.Max(1f, list.ColumnWidth - indent - markerWidth - 8f);
            float height = label.Height(labelWidth);
            Rect row = list.GetRect(height);
            list.Gap(2f);
            if (!RHAH_IrisMenusWidgets.IsVisible(row))
            {
                return false;
            }

            if (selected)
            {
                Widgets.DrawHighlightSelected(row);
            }
            Widgets.DrawHighlightIfMouseover(row);
            Rect marker = new Rect(row.x + indent, row.y + 4f, markerWidth, 24f);
            if (fold)
            {
                Widgets.DrawTextureFitted(marker, open ? TexButton.Collapse : TexButton.Reveal, 0.7f);
            }
            else if (selected)
            {
                Widgets.DrawBox(marker.ContractedBy(6f));
            }
            Widgets.Label(new Rect(row.x + indent + markerWidth + 4f, row.y + 4f, labelWidth, height - 8f), label.Text);
            return Widgets.ButtonInvisible(row);
        }

        void DrawSettlementDetails(Listing_Standard list)
        {
            if (settlementModel == null || settlementModel.Count == 0 || selectedSettlementGroup == null)
            {
                string key = settlementModel == null ? "RHAH_Settlement_NoGame"
                    : settlementModel.Count == 0 ? "RHAH_Settlement_None" : "RHAH_Settlement_SelectGroup";
                RHAH_IrisMenusWidgets.Quote(list, "settlement-detail-empty", key.Translate());
            }
            else
            {
                for (int i = 0; i < settlementRows.Count; i++)
                {
                    SettlementRow row = settlementRows[i];
                    list.Gap(row.GapBefore);
                    RHAH_IrisMenusWidgets.Quote(list, row.Anchor, row.Text);
                }
            }

            list.Gap(12f);
            RHAH_IrisMenusWidgets.Quote(list, "settlement-detail-note", "RHAH_Settlement_Note".Translate());
            RHAH_IrisMenusWidgets.Quote(list, "settlement-detail-updates", "RHAH_Settlement_UpdateNote".Translate());
        }

        static string BuildSettlementSourceText(RHAH_SettlementGroup group)
        {
            RHAH_IncidentEntry source = RHAH_IncidentCatalog.GetByDisplayId(group.SourceDisplayId);
            return source == null ? "RHAH_Settlement_UnknownSource".Translate().ToString() : source.LabelKey.Translate().ToString();
        }

        static string SettlementKindKey(RHAH_SettlementKind kind)
        {
            switch (kind)
            {
                case RHAH_SettlementKind.Exchange: return "RHAH_Settlement_ExchangeKind";
                case RHAH_SettlementKind.Entrust: return "RHAH_Settlement_EntrustKind";
                default: return "RHAH_Settlement_ReliefKind";
            }
        }

        static string BuildSettlementLeafText(RHAH_SettlementGroup group)
        {
            string name = null;
            int loadId = group.MotherLoadId;
            for (int i = 0; i < group.Members.Count; i++)
            {
                RHAH_SettlementMember member = group.Members[i];
                if (i == 0 && string.IsNullOrEmpty(name))
                {
                    loadId = member.LoadId;
                }
                if (!string.IsNullOrEmpty(member.Name))
                {
                    name = member.Name;
                    break;
                }
            }
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(group.MotherName))
            {
                name = group.MotherName;
            }

            return "RHAH_Settlement_GroupLeaf".Translate(SettlementName(name, loadId),
                group.Members.Count, SettlementKindKey(group.Kind).Translate()).ToString();
        }

        static string BuildSettlementGroupText(RHAH_SettlementGroup group)
        {
            StringBuilder text = new StringBuilder("RHAH_Settlement_GroupSource".Translate(
                BuildSettlementSourceText(group), SettlementKindKey(group.Kind).Translate()).ToString());
            text.AppendLine().Append("RHAH_Settlement_RecordedDay".Translate(SettlementRecordedDay(group.StartedTick)).ToString());
            text.AppendLine().Append("RHAH_Settlement_RecordDateNote".Translate().ToString());
            switch (group.Kind)
            {
                case RHAH_SettlementKind.Relief:
                    text.AppendLine().Append("RHAH_Settlement_ReliefTimerNote".Translate().ToString());
                    break;
                case RHAH_SettlementKind.Exchange:
                    text.AppendLine().Append("RHAH_Settlement_ExchangeTimerNote".Translate().ToString());
                    break;
                case RHAH_SettlementKind.Entrust:
                    text.AppendLine().Append("RHAH_Settlement_EntrustGrowthNote".Translate().ToString());
                    break;
            }

            if (group.RemainingObservationTicks >= 0)
            {
                string key = group.Kind == RHAH_SettlementKind.Entrust
                    ? "RHAH_Settlement_GrowthObservationRemaining"
                    : "RHAH_Settlement_ObservationRemaining";
                text.AppendLine().Append(key.Translate(FormatSettlementDays(group.RemainingObservationTicks)).ToString());
            }

            if (group.Failed)
            {
                text.AppendLine().Append("RHAH_Settlement_GroupFailed".Translate().ToString());
            }

            if (group.Driven)
            {
                text.AppendLine().Append("RHAH_Settlement_GroupDriven".Translate().ToString());
            }

            if (group.Kind == RHAH_SettlementKind.Relief && !group.Delivered)
            {
                for (int i = 0; i < group.Members.Count; i++)
                {
                    if ((group.Members[i].Blockers & RHAH_SettlementBlocker.Left) != 0)
                    {
                        text.AppendLine().Append("RHAH_Settlement_FoodNotDelivered".Translate().ToString());
                        break;
                    }
                }
            }

            return text.ToString();
        }

        static string BuildSettlementMotherText(RHAH_SettlementGroup group)
        {
            SuiyinPresence presence = SuiyinPresence.Unknown;
            for (int i = 0; i < group.Members.Count; i++)
            {
                if (group.Members[i].MotherPresence.HasValue)
                {
                    presence = group.Members[i].MotherPresence.Value;
                    break;
                }
            }

            string presenceKey;
            switch (presence)
            {
                case SuiyinPresence.Here: presenceKey = "RHAH_Settlement_PresenceHere"; break;
                case SuiyinPresence.Dead: presenceKey = "RHAH_Settlement_Dead"; break;
                case SuiyinPresence.Left: presenceKey = "RHAH_Settlement_Left"; break;
                case SuiyinPresence.Missing: presenceKey = "RHAH_Settlement_Missing"; break;
                default: presenceKey = "RHAH_Settlement_Unknown"; break;
            }

            return "RHAH_Settlement_Mother".Translate(
                SettlementName(group.MotherName, group.MotherLoadId), presenceKey.Translate());
        }

        static string BuildSettlementMemberText(RHAH_SettlementGroup group, RHAH_SettlementMember member)
        {
            StringBuilder text = new StringBuilder(SettlementName(member.Name, member.LoadId));
            if (member.RemainingTicks >= 0 && group.Kind != RHAH_SettlementKind.Entrust)
            {
                text.AppendLine().Append("RHAH_Settlement_AccumulatedCareRemaining".Translate(
                    FormatSettlementDays(member.RemainingTicks)).ToString());
            }

            StringBuilder conditions = new StringBuilder();
            if (group.Kind == RHAH_SettlementKind.Entrust)
            {
                AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Hungry, "RHAH_Settlement_Hungry");
                AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Plague, "RHAH_Settlement_Plague");
                AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Captive, "RHAH_Settlement_Captive");
            }
            else
            {
                AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Malnutrition, "RHAH_Settlement_Malnutrition");
            }
            AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Unknown, "RHAH_Settlement_Unknown");
            AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Missing, "RHAH_Settlement_Missing");
            AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Dead, "RHAH_Settlement_Dead");
            AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Left, "RHAH_Settlement_Left");
            AppendSettlementCondition(conditions, member.Blockers, RHAH_SettlementBlocker.Growing, "RHAH_Settlement_Growing");
            text.AppendLine().Append("RHAH_Settlement_Conditions".Translate(conditions.Length > 0
                ? conditions.ToString()
                : "RHAH_Settlement_NoConcerns".Translate().ToString()).ToString());

            string statusKey = !member.Ready ? "RHAH_Settlement_Pending"
                : group.Failed || group.Driven ? "RHAH_Settlement_ReadyFailedGroup"
                : "RHAH_Settlement_ReadyWaiting";
            text.AppendLine().Append(statusKey.Translate().ToString());
            return text.ToString();
        }

        static void AppendSettlementCondition(StringBuilder text, RHAH_SettlementBlocker blockers,
            RHAH_SettlementBlocker condition, string key)
        {
            if ((blockers & condition) == 0)
            {
                return;
            }

            if (text.Length > 0)
            {
                text.Append("RHAH_Settlement_ConditionSeparator".Translate().ToString());
            }

            text.Append(key.Translate().ToString());
        }

        static string SettlementName(string name, int loadId)
        {
            return string.IsNullOrEmpty(name) ? "RHAH_Settlement_UnknownPerson".Translate(loadId).ToString() : name;
        }

        static string FormatSettlementDays(int ticks)
        {
            return (Math.Ceiling(Math.Max(0, ticks) * 10d / GenDate.TicksPerDay) / 10d).ToString("0.0");
        }

        sealed class SettlementDay
        {
            internal readonly int Day;
            internal readonly SettlementTreeLabel Label;
            internal readonly List<SettlementEvent> Events = new List<SettlementEvent>();

            internal SettlementDay(int day)
            {
                Day = day;
                Label = new SettlementTreeLabel("RHAH_Settlement_RecordedDay".Translate(day).ToString());
            }
        }

        struct SettlementEventKey : IEquatable<SettlementEventKey>
        {
            readonly int day;
            readonly string source;

            internal SettlementEventKey(int day, string source)
            {
                this.day = day;
                this.source = source;
            }

            public bool Equals(SettlementEventKey other)
            {
                return day == other.day && string.Equals(source, other.source, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SettlementEventKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked(day * 397 ^ (source == null ? 0 : StringComparer.Ordinal.GetHashCode(source)));
            }
        }

        sealed class SettlementEvent
        {
            internal readonly SettlementEventKey Key;
            internal readonly SettlementTreeLabel Label;
            internal readonly List<SettlementLeaf> Groups = new List<SettlementLeaf>();

            internal SettlementEvent(SettlementEventKey key, string label)
            {
                Key = key;
                Label = new SettlementTreeLabel(label);
            }
        }

        sealed class SettlementLeaf
        {
            internal readonly RHAH_SettlementGroup Group;
            internal readonly SettlementTreeLabel Label;

            internal SettlementLeaf(RHAH_SettlementGroup group, string label)
            {
                Group = group;
                Label = new SettlementTreeLabel(label);
            }
        }

        sealed class SettlementTreeLabel
        {
            internal readonly string Text;
            float measuredWidth = -1f;
            float measuredHeight;

            internal SettlementTreeLabel(string text)
            {
                Text = text;
            }

            internal float Height(float width)
            {
                if (measuredWidth != width)
                {
                    measuredWidth = width;
                    measuredHeight = Mathf.Max(32f, Verse.Text.CalcHeight(Text, width) + 8f);
                }

                return measuredHeight;
            }
        }

        sealed class SettlementRow
        {
            internal readonly string Anchor;
            internal readonly string Text;
            internal readonly float GapBefore;

            internal SettlementRow(string anchor, string text, float gapBefore = 0f)
            {
                Anchor = anchor;
                Text = text;
                GapBefore = gapBefore;
            }
        }

        void DrawOther(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Other");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            DrawOtherNumbers(list, settings);
        }

        static IEnumerable<MenuSearchEntry> SearchOther()
        {
            yield break;
        }
        void DrawOtherNumbers(Listing_Standard list, RHAH_Settings settings)
        {
            string minAge = Buffer(weightBuffers, "age-min", settings.minGeneratedAge, "0.0");
            settings.minGeneratedAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MinAge".Translate(settings.minGeneratedAge.ToString("0.0")), settings.minGeneratedAge, ref minAge, 0f, 100f, "0.0", "RHAH_Settings_Age_Tooltip".Translate());
            weightBuffers["age-min"] = minAge;
            string maxAge = Buffer(weightBuffers, "age-max", settings.maxGeneratedAge, "0.0");
            settings.maxGeneratedAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaxAge".Translate(settings.maxGeneratedAge.ToString("0.0")), settings.maxGeneratedAge, ref maxAge, settings.minGeneratedAge, 100f, "0.0", "RHAH_Settings_Age_Tooltip".Translate());
            weightBuffers["age-max"] = maxAge;
            string motherAge = Buffer(weightBuffers, "mother-age", settings.minMotherAge, "0");
            settings.minMotherAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MotherAge".Translate(settings.minMotherAge.ToString("0")), settings.minMotherAge, ref motherAge, RHAH_VisitorRules.MinMotherAge, RHAH_VisitorRules.MaxMotherAge, "0", "RHAH_Settings_MotherAge_Tooltip".Translate());
            weightBuffers["mother-age"] = motherAge;
            string shelter = Buffer(weightBuffers, "shelter-days", settings.shelterDays, "0");
            settings.shelterDays = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_ShelterDays".Translate(RHAH_VisitorRules.StayLabel(settings.shelterDays)), settings.shelterDays, ref shelter, RHAH_VisitorRules.MinShelterDays, RHAH_VisitorRules.MaxShelterDays, "0", "RHAH_Settings_ShelterDays_Tooltip".Translate());
            weightBuffers["shelter-days"] = shelter;
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ShortStayLockApparel".Translate(), ref settings.shortStayLockApparel, "RHAH_Settings_ShortStayLockApparel_Tooltip".Translate());
            string hire = Buffer(weightBuffers, "hire-days", settings.hireDays, "0");
            settings.hireDays = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_HireDays".Translate(RHAH_VisitorRules.StayLabel(settings.hireDays)), settings.hireDays, ref hire, RHAH_VisitorRules.MinHireDays, RHAH_VisitorRules.MaxHireDays, "0", "RHAH_Settings_HireDays_Tooltip".Translate());
            weightBuffers["hire-days"] = hire;
        }

        void DrawEnvironment(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Environment");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            Note(list, "RHAH_Menu_Environment_Note");
            MenuControls.Anchor(list, "cold-clothes");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ColdClothes".Translate(), ref settings.coldClothesEnabled, "RHAH_Settings_ColdClothes_Tooltip".Translate());
            string cold = Buffer(weightBuffers, "temp-min", settings.minimumEventTemperature, "0");
            settings.minimumEventTemperature = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_TempMin".Translate(settings.minimumEventTemperature.ToString("0")), settings.minimumEventTemperature, ref cold, -35f, 70f, "0", "RHAH_Settings_Temp_Tooltip".Translate());
            weightBuffers["temp-min"] = cold;
            string heat = Buffer(weightBuffers, "temp-max", settings.maximumEventTemperature, "0");
            settings.maximumEventTemperature = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_TempMax".Translate(settings.maximumEventTemperature.ToString("0")), settings.maximumEventTemperature, ref heat, settings.minimumEventTemperature, 70f, "0", "RHAH_Settings_Temp_Tooltip".Translate());
            weightBuffers["temp-max"] = heat;
            if (!settings.coldClothesEnabled)
            {
                Note(list, "RHAH_Menu_Environment_Disabled");
                return;
            }

            DrawTemperatureGroup(list, settings, "RHAH_Menu_Environment_Cold", RHAH_VisitorRules.ColdApparel);
            DrawTemperatureGroup(list, settings, "RHAH_Menu_Environment_Heat", RHAH_VisitorRules.HeatApparel);
        }

        void DrawTemperatureGroup(Listing_Standard list, RHAH_Settings settings, string titleKey, string[] names)
        {
            Section(list, titleKey);
            string direction = titleKey.Translate();
            for (int i = 0; i < names.Length; i++)
            {
                string defName = names[i];
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                string label = def == null ? defName : def.LabelCap;
                bool enabled = settings.IsTemperatureApparelEnabled(defName);
                MenuControls.Anchor(list, "temp-" + defName);
                RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_TemperatureApparel".Translate(label), ref enabled, "RHAH_Settings_TemperatureApparel_Tooltip".Translate());
                if (enabled != settings.IsTemperatureApparelEnabled(defName))
                {
                    settings.SetTemperatureApparelEnabled(defName, enabled);
                }

                float insulation = settings.TemperatureApparelInsulation(defName);
                string buffer = Buffer(weightBuffers, "temp-insulation-" + defName, insulation, "0.0");
                insulation = RHAH_IrisMenusWidgets.TunedValue(
                    list,
                    "RHAH_Settings_TemperatureInsulation".Translate(label, insulation.ToString("0.0"), direction),
                    insulation,
                    ref buffer,
                    0f,
                    100f,
                    "0.0",
                    "RHAH_Settings_TemperatureInsulation_Tooltip".Translate());
                weightBuffers["temp-insulation-" + defName] = buffer;
                settings.SetTemperatureApparelInsulation(defName, insulation);
            }
        }

        IEnumerable<MenuSearchEntry> SearchEnvironment()
        {
            yield return Entry("cold-clothes", "RHAH_Settings_ColdClothes");
            yield return Entry("temp-min", "RHAH_Settings_TempMin");
            yield return Entry("temp-max", "RHAH_Settings_TempMax");
            string[] names = RHAH_VisitorRules.ColdApparel;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    names = RHAH_VisitorRules.HeatApparel;
                }

                for (int i = 0; i < names.Length; i++)
                {
                    string defName = names[i];
                    yield return new MenuSearchEntry("temp-" + defName, () => TemperatureLabel(defName), () => defName);
                }
            }
        }

        static string TemperatureLabel(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def == null ? defName : def.LabelCap;
        }


        void DrawRelief(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Relief");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            DrawReliefToggle(list, "relief-enabled", ref settings.reliefEnabled,
                "RHAH_Settings_ReliefEnabled", settings);
            DrawReliefToggle(list, "relief-outside", ref settings.allowEatOutsideRelief,
                "RHAH_Settings_EatOutsideRelief", settings);
            DrawReliefToggle(list, "relief-ignore", ref settings.ignoreReliefAfterFed,
                "RHAH_Settings_IgnoreReliefAfterFed", settings);
            DrawReliefToggle(list, "relief-leave", ref settings.leaveAfterFed,
                "RHAH_Settings_LeaveAfterFed", settings);
            DrawVisitorNumbers(list, settings);
            DrawFoodList(list, settings);
            DrawGiveFoodList(list, settings);
            DrawBegFoodList(list, settings);
        }
        void DrawVisitors(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Visitors");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            MenuControls.Anchor(list, "famine-year");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamineYear".Translate(), ref settings.famineYearEnabled, "RHAH_Settings_FamineYear_Tooltip".Translate());
            settings.famineYearChancePercent = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-year-chance", "RHAH_Settings_FamineYearChance".Translate(settings.famineYearChancePercent.ToString("0.0")), GameComponent_RHAH_FamineYear.ClampPercent(settings.famineYearChancePercent, 40f), 0f, 100f, "0.0", "RHAH_Settings_FamineYearChance_Tooltip".Translate());
            settings.famineYearPawnMultiplier = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-year-multiplier", "RHAH_Settings_FamineYearMultiplier".Translate(settings.famineYearPawnMultiplier.ToString("0.00")), GameComponent_RHAH_FamineYear.ClampMultiplier(settings.famineYearPawnMultiplier), 1f, 4f, "0.00", "RHAH_Settings_FamineYearMultiplier_Tooltip".Translate());
            settings.famineYearDisasterChancePercent = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-year-disaster", "RHAH_Settings_FamineYearDisasterChance".Translate(settings.famineYearDisasterChancePercent.ToString("0.0")), GameComponent_RHAH_FamineYear.ClampPercent(settings.famineYearDisasterChancePercent, 20f), 0f, 100f, "0.0", "RHAH_Settings_FamineYearDisasterChance_Tooltip".Translate());

            MenuControls.Anchor(list, "famine-arrival");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamineArrival".Translate(), ref settings.famineArrivalEnabled, "RHAH_Settings_FamineArrival_Tooltip".Translate());
            settings.famineArrivalMalnutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-malnutrition", "RHAH_Settings_FamineMalnutrition".Translate(settings.famineArrivalMalnutrition.ToString("0.00")), settings.famineArrivalMalnutrition, 0.01f, 0.5f, "0.00", "RHAH_Settings_FamineMalnutrition_Tooltip".Translate());
            MenuControls.Anchor(list, "famine-mood");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamineMood".Translate(), ref settings.famineMoodEnabled, "RHAH_Settings_FamineMood_Tooltip".Translate());
            MenuControls.Anchor(list, "clay-mood");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ClayMood".Translate(), ref settings.clayMoodEnabled, "RHAH_Settings_ClayMood_Tooltip".Translate());
            MenuControls.Anchor(list, "famine-trade");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamineTrade".Translate(), ref settings.famineTradeEnabled, "RHAH_Settings_FamineTrade_Tooltip".Translate());
            settings.famineFoodSaleFactor = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-food-sale", "RHAH_Settings_FoodSaleFactor".Translate(settings.famineFoodSaleFactor.ToString("0.00")), settings.famineFoodSaleFactor, 1f, 3f, "0.00", "RHAH_Settings_FoodSaleFactor_Tooltip".Translate());
            settings.famineGoodsBuyFactor = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "famine-goods-buy", "RHAH_Settings_GoodsBuyFactor".Translate(settings.famineGoodsBuyFactor.ToString("0.00")), settings.famineGoodsBuyFactor, 0.1f, 1f, "0.00", "RHAH_Settings_GoodsBuyFactor_Tooltip".Translate());
            MenuControls.Anchor(list, "armed-demand");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ArmedDemand".Translate(), ref settings.armedDemandEnabled, "RHAH_Settings_ArmedDemand_Tooltip".Translate());
            MenuControls.Anchor(list, "armed-weapons");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ArmedWeapons".Translate(), ref settings.armedWeaponsEnabled, "RHAH_Settings_ArmedWeapons_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Begging".Translate(), ref settings.beggingEnabled, "RHAH_Settings_Begging_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_BegAutoGive".Translate(), ref settings.begAutoGiveEnabled, "RHAH_Settings_BegAutoGive_Tooltip".Translate());
            string begChance = Buffer(weightBuffers, "beg-chance", settings.begSuccessChancePercent, "0");
            settings.begSuccessChancePercent = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_BegSuccessChance".Translate(settings.begSuccessChancePercent), settings.begSuccessChancePercent, ref begChance, 0f, 100f, "0", "RHAH_Settings_BegSuccessChance_Tooltip".Translate());
            weightBuffers["beg-chance"] = begChance;
            string begSocial = Buffer(weightBuffers, "beg-social", settings.begSocialBonusPercent, "0");
            settings.begSocialBonusPercent = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_BegSocialBonus".Translate(settings.begSocialBonusPercent), settings.begSocialBonusPercent, ref begSocial, 0f, 20f, "0", "RHAH_Settings_BegSocialBonus_Tooltip".Translate());
            weightBuffers["beg-social"] = begSocial;
            string begHours = Buffer(weightBuffers, "beg-hours", settings.begFailCooldownHours, "0");
            settings.begFailCooldownHours = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_BegFailCooldown".Translate(settings.begFailCooldownHours), settings.begFailCooldownHours, ref begHours, 3f, 12f, "0", "RHAH_Settings_BegFailCooldown_Tooltip".Translate());
            weightBuffers["beg-hours"] = begHours;
            string begSlap = Buffer(weightBuffers, "beg-slap", settings.begSlapChancePercent, "0");
            settings.begSlapChancePercent = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_BegSlapChance".Translate(settings.begSlapChancePercent), settings.begSlapChancePercent, ref begSlap, 0f, 100f, "0", "RHAH_Settings_BegSlapChance_Tooltip".Translate());
            weightBuffers["beg-slap"] = begSlap;
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Stealing".Translate(), ref settings.stealingEnabled, "RHAH_Settings_Stealing_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Fighting".Translate(), ref settings.fightingEnabled, "RHAH_Settings_Fighting_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Gnawing".Translate(), ref settings.gnawingEnabled, "RHAH_Settings_Gnawing_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_BatchHostile".Translate(), ref settings.batchTurnsHostile, "RHAH_Settings_BatchHostile_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_BatchLeave".Translate(), ref settings.batchLeavesTogether, "RHAH_Settings_BatchLeave_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamilyDrop".Translate(), ref settings.familyDropEnabled, "RHAH_Settings_FamilyDrop_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_MotherFeed".Translate(), ref settings.motherFeedEnabled, "RHAH_Settings_MotherFeed_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_PrisonerScavenge".Translate(), ref settings.prisonerScavengeEnabled, "RHAH_Settings_PrisonerScavenge_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_TailBite".Translate(), ref settings.tailBiteEnabled, "RHAH_Settings_TailBite_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FamineDoors".Translate(), ref settings.famineVisitorsOpenDoors, "RHAH_Settings_FamineDoors_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_GreenLetters".Translate(), ref settings.greenIncidentLetters, "RHAH_Settings_GreenLetters_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Broadcast".Translate(), ref settings.broadcastEnabled, "RHAH_Settings_Broadcast_Tooltip".Translate());
            string cooldown = Buffer(weightBuffers, "broadcast-days", settings.broadcastCooldownDays, "0");
            settings.broadcastCooldownDays = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_BroadcastCooldown".Translate(settings.broadcastCooldownDays), settings.broadcastCooldownDays, ref cooldown, 0f, 10f, "0", "RHAH_Settings_BroadcastCooldown_Tooltip".Translate());
            weightBuffers["broadcast-days"] = cooldown;
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_RefugeeCamp".Translate(), ref settings.refugeeCampEnabled, "RHAH_Settings_RefugeeCamp_Tooltip".Translate());
            DrawVisitorIntensity(list, settings);
        }

        static IEnumerable<MenuSearchEntry> SearchVisitors()
        {
            yield return Entry("visitors-beg", "RHAH_Settings_Begging");
            yield return Entry("visitors-beg-give", "RHAH_Settings_BegAutoGive");
            yield return Entry("visitors-broadcast", "RHAH_Settings_Broadcast");
            yield return Entry("visitors-camp", "RHAH_Settings_RefugeeCamp");
            yield return Entry("famine-year", "RHAH_Settings_FamineYear");
            yield return Entry("famine-year-chance", "RHAH_Settings_FamineYearChance");
            yield return Entry("famine-year-multiplier", "RHAH_Settings_FamineYearMultiplier");
            yield return Entry("famine-year-disaster", "RHAH_Settings_FamineYearDisasterChance");
            yield return Entry("famine-arrival", "RHAH_Settings_FamineArrival");
            yield return Entry("famine-malnutrition", "RHAH_Settings_FamineMalnutrition");
            yield return Entry("famine-mood", "RHAH_Settings_FamineMood");
            yield return Entry("clay-mood", "RHAH_Settings_ClayMood");
            yield return Entry("famine-trade", "RHAH_Settings_FamineTrade");
            yield return Entry("famine-food-sale", "RHAH_Settings_FoodSaleFactor");
            yield return Entry("famine-goods-buy", "RHAH_Settings_GoodsBuyFactor");
            yield return Entry("armed-demand", "RHAH_Settings_ArmedDemand");
            yield return Entry("armed-weapons", "RHAH_Settings_ArmedWeapons");
        }

        void DrawPlague(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Plague");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Plague".Translate(), ref settings.plagueEnabled, "RHAH_Settings_Plague_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_PlagueSafe".Translate(), ref settings.plagueSafeMode, "RHAH_Settings_PlagueSafe_Tooltip".Translate());
            settings.plagueSeverityMax = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "plague-severity", "RHAH_Settings_PlagueSeverity".Translate(settings.plagueSeverityMax.ToString("0.00")), settings.plagueSeverityMax, 0.01f, 1f, "0.00", "RHAH_Settings_PlagueSeverity_Tooltip".Translate());
            string per = Buffer(weightBuffers, "plague-per", settings.plagueSpreadChancePerCarrier * 100f, "0.0");
            float perPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlaguePerCarrier".Translate((settings.plagueSpreadChancePerCarrier * 100f).ToString("0.0")), settings.plagueSpreadChancePerCarrier * 100f, ref per, 0f, 100f, "0.0", "RHAH_Settings_PlaguePerCarrier_Tooltip".Translate());
            settings.plagueSpreadChancePerCarrier = perPercent / 100f;
            weightBuffers["plague-per"] = per;
            string cap = Buffer(weightBuffers, "plague-cap", settings.plagueSpreadChanceCap * 100f, "0");
            float capPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlagueCap".Translate((settings.plagueSpreadChanceCap * 100f).ToString("0")), settings.plagueSpreadChanceCap * 100f, ref cap, 0f, 100f, "0", "RHAH_Settings_PlagueCap_Tooltip".Translate());
            settings.plagueSpreadChanceCap = capPercent / 100f;
            weightBuffers["plague-cap"] = cap;
            string days = Buffer(weightBuffers, "plague-days", settings.plagueSpreadDayInterval, "0");
            settings.plagueSpreadDayInterval = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlagueDays".Translate(settings.plagueSpreadDayInterval), settings.plagueSpreadDayInterval, ref days, 1f, 30f, "0", "RHAH_Settings_PlagueDays_Tooltip".Translate());
            weightBuffers["plague-days"] = days;
            string hour = Buffer(weightBuffers, "plague-hour", settings.plagueSpreadHour, "0");
            settings.plagueSpreadHour = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlagueHour".Translate(settings.plagueSpreadHour), settings.plagueSpreadHour, ref hour, 0f, 23f, "0", "RHAH_Settings_PlagueHour_Tooltip".Translate());
            weightBuffers["plague-hour"] = hour;
            string blood = Buffer(weightBuffers, "plague-blood", settings.plagueBloodPumpingSkipPercent, "0");
            settings.plagueBloodPumpingSkipPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlagueBlood".Translate(settings.plagueBloodPumpingSkipPercent.ToString("0")), settings.plagueBloodPumpingSkipPercent, ref blood, 0f, 300f, "0", "RHAH_Settings_PlagueBlood_Tooltip".Translate());
            weightBuffers["plague-blood"] = blood;
        }

        static IEnumerable<MenuSearchEntry> SearchPlague()
        {
            yield return Entry("plague-enabled", "RHAH_Settings_Plague");
        }


        static void DrawReliefToggle(
            Listing_Standard list,
            string anchor,
            ref bool value,
            string key,
            RHAH_Settings settings)
        {
            bool before = value;
            MenuControls.Anchor(list, anchor);
            RHAH_IrisMenusWidgets.Checkbox(list, key.Translate(), ref value, (key + "_Tooltip").Translate());
            if (before != value)
            {
                settings.InvalidateReliefSearch();
            }
        }
        void DrawVisitorNumbers(Listing_Standard list, RHAH_Settings settings)
        {
            string cap = Buffer(weightBuffers, "event-cap", settings.maxEventPawns, "0");
            settings.maxEventPawns = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaxEventPawns".Translate(settings.maxEventPawns), settings.maxEventPawns, ref cap, 1f, 100f, "0", "RHAH_Settings_MaxEventPawns_Tooltip".Translate());
            weightBuffers["event-cap"] = cap;
            string bonus = Buffer(weightBuffers, "relief-bonus", settings.reliefFoodScoreBonus * 100f, "0");
            float percent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_ReliefScore".Translate((settings.reliefFoodScoreBonus * 100f).ToString("0")), settings.reliefFoodScoreBonus * 100f, ref bonus, 0f, 100f, "0", "RHAH_Settings_ReliefScore_Tooltip".Translate());
            settings.reliefFoodScoreBonus = percent / 100f;
            weightBuffers["relief-bonus"] = bonus;
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FedWander".Translate(), ref settings.fedWanderEnabled, "RHAH_Settings_FedWander_Tooltip".Translate());
            string stay = Buffer(weightBuffers, "fed-wander", settings.fedWanderHours, "0");
            settings.fedWanderHours = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_FedWanderHours".Translate(settings.fedWanderHours), settings.fedWanderHours, ref stay, 1f, 48f, "0", "RHAH_Settings_FedWanderHours_Tooltip".Translate());
            weightBuffers["fed-wander"] = stay;
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_WaitFood".Translate(), ref settings.waitWhenNoFood, "RHAH_Settings_WaitFood_Tooltip".Translate());
            string wait = Buffer(weightBuffers, "food-wait", settings.noFoodWaitDays, "0.00");
            settings.noFoodWaitDays = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_FoodWait".Translate(settings.noFoodWaitDays.ToString("0.00")), settings.noFoodWaitDays, ref wait, 0f, 5f, "0.00", "RHAH_Settings_FoodWait_Tooltip".Translate());
            weightBuffers["food-wait"] = wait;
            settings.satisfiedFoodPercent = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "food-full", "RHAH_Settings_FoodFull".Translate(settings.satisfiedFoodPercent.ToString("0")), settings.satisfiedFoodPercent, 1f, 100f, "0", "RHAH_Settings_FoodFull_Tooltip".Translate());
            settings.refeedMalnutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "food-refeed", "RHAH_Settings_FoodRefeed".Translate(settings.refeedMalnutrition.ToString("0.00")), settings.refeedMalnutrition, 0f, 1f, "0.00", "RHAH_Settings_FoodRefeed_Tooltip".Translate());
        }

        void DrawFoodList(Listing_Standard list, RHAH_Settings settings)
        {
            MenuControls.Anchor(list, "relief-foods", 86f);
            RHAH_IrisMenusWidgets.Quote(list, "relief-foods-note", "RHAH_Settings_ReliefFoods_Quote".Translate());
            Rect buttons = list.GetRect(28f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 140f, 26f),
                "RHAH_Settings_ReliefFoods_All".Translate()))
            {
                settings.SetAllReliefFood(true, null);
            }

            if (Widgets.ButtonText(new Rect(buttons.x + 148f, buttons.y, 140f, 26f),
                "RHAH_Settings_ReliefFoods_None".Translate()))
            {
                List<ThingDef> foods = new List<ThingDef>();
                RHAH_ReliefFood.AppendCandidateFoods(foods);
                List<string> names = new List<string>(foods.Count);
                for (int i = 0; i < foods.Count; i++)
                {
                    names.Add(foods[i].defName);
                }

                settings.SetAllReliefFood(false, names);
            }

            list.Gap(4f);
            Rect search = list.GetRect(28f);
            foodQuery = Widgets.TextField(search, foodQuery ?? string.Empty);
            if (string.IsNullOrEmpty(foodQuery))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(search.x + 6f, search.y, search.width - 8f, search.height),
                    "RHAH_Settings_ReliefFoods_Search".Translate());
                GUI.color = Color.white;
            }

            list.Gap(4f);
            List<ThingDef> listed = new List<ThingDef>();
            RHAH_ReliefFood.AppendCandidateFoods(listed);
            List<List<ThingDef>> groups = RHAH_ReliefFood.GroupBySourceMod(listed);
            bool searching = !string.IsNullOrEmpty(foodQuery);
            bool any = false;
            for (int i = 0; i < groups.Count; i++)
            {
                List<ThingDef> matched = MatchedFoods(groups[i]);
                if (matched.Count == 0)
                {
                    continue;
                }

                any = true;
                string modName = RHAH_ReliefFood.SourceModName(groups[i][0]);
                string key = modName ?? string.Empty;
                if (pendingFoodMod != null && string.Equals(pendingFoodMod, key, StringComparison.Ordinal))
                {
                    collapsedFoodMods.Remove(key);
                    pendingFoodMod = null;
                }

                bool open = searching || !collapsedFoodMods.Contains(key);
                string title = "RHAH_Menu_Format_GroupCount".Translate(modName ?? "RHAH_Menu_Genes_UnknownMod".Translate(), matched.Count);
                MenuControls.Anchor(list, "relief-food-mod-" + key, 28f);
                if (DrawFoodFold(list, title, open))
                {
                    if (open)
                    {
                        collapsedFoodMods.Add(key);
                    }
                    else
                    {
                        collapsedFoodMods.Remove(key);
                    }

                    open = !open;
                }

                if (!open)
                {
                    continue;
                }

                for (int foodIndex = 0; foodIndex < matched.Count; foodIndex++)
                {
                    ThingDef food = matched[foodIndex];
                    bool enabled = settings.IsReliefFoodEnabled(food.defName);
                    if (RHAH_IrisMenusWidgets.Checkbox(list, "relief-food-" + food.defName, food.LabelCap, ref enabled))
                    {
                        settings.SetReliefFoodEnabled(food.defName, enabled);
                    }
                }
            }

            if (!any)
            {
                Empty(list, "RHAH_Settings_ReliefFoods_Empty");
            }
        }

        void DrawGiveFoodList(Listing_Standard list, RHAH_Settings settings)
        {
            MenuControls.Anchor(list, "give-foods", 120f);
            RHAH_IrisMenusWidgets.Quote(list, "give-foods-note", "RHAH_Settings_GiveFoods_Quote".Translate());
            DrawModeSelect(list, "give-food-sort", "RHAH_Settings_GiveFoods_Sort", settings.giveFoodListMode, 3, mode => settings.giveFoodListMode = mode);
            Rect buttons = list.GetRect(28f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 140f, 26f),
                "RHAH_Settings_GiveFoods_All".Translate()))
            {
                settings.SetAllGiveFood(true, null);
            }

            if (Widgets.ButtonText(new Rect(buttons.x + 148f, buttons.y, 140f, 26f),
                "RHAH_Settings_GiveFoods_None".Translate()))
            {
                List<ThingDef> foods = new List<ThingDef>();
                RHAH_ReliefFood.AppendCandidateFoods(foods);
                List<string> names = new List<string>(foods.Count);
                for (int i = 0; i < foods.Count; i++)
                {
                    names.Add(foods[i].defName);
                }

                settings.SetAllGiveFood(false, names);
            }

            list.Gap(4f);
            Rect search = list.GetRect(28f);
            giveFoodQuery = Widgets.TextField(search, giveFoodQuery ?? string.Empty);
            if (string.IsNullOrEmpty(giveFoodQuery))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(search.x + 6f, search.y, search.width - 8f, search.height),
                    "RHAH_Settings_GiveFoods_Search".Translate());
                GUI.color = Color.white;
            }

            list.Gap(4f);
            List<ThingDef> listed = new List<ThingDef>();
            RHAH_ReliefFood.AppendCandidateFoods(listed);
            List<List<ThingDef>> groups = RHAH_ReliefFood.GroupFoods(settings.giveFoodListMode, listed);
            bool searching = !string.IsNullOrEmpty(giveFoodQuery);
            bool any = false;
            for (int i = 0; i < groups.Count; i++)
            {
                List<ThingDef> matched = MatchedGiveFoods(groups[i]);
                if (matched.Count == 0)
                {
                    continue;
                }

                any = true;
                string key = RHAH_ReliefFood.GroupKey(settings.giveFoodListMode, groups[i][0]);
                if (pendingGiveFoodGroup != null && string.Equals(pendingGiveFoodGroup, key, StringComparison.Ordinal))
                {
                    collapsedGiveFoodGroups.Remove(key);
                    pendingGiveFoodGroup = null;
                }

                bool open = searching || !collapsedGiveFoodGroups.Contains(key);
                string title = "RHAH_Menu_Format_GroupCount".Translate(GiveFoodTitle(settings.giveFoodListMode, key), matched.Count);
                MenuControls.Anchor(list, "give-food-group-" + key, 28f);
                if (DrawFoodFold(list, title, open))
                {
                    if (open)
                    {
                        collapsedGiveFoodGroups.Add(key);
                    }
                    else
                    {
                        collapsedGiveFoodGroups.Remove(key);
                    }

                    open = !open;
                }

                if (!open)
                {
                    continue;
                }

                for (int foodIndex = 0; foodIndex < matched.Count; foodIndex++)
                {
                    ThingDef food = matched[foodIndex];
                    bool enabled = settings.IsGiveFoodEnabled(food.defName);
                    if (RHAH_IrisMenusWidgets.Checkbox(list, "give-food-" + food.defName, food.LabelCap, ref enabled))
                    {
                        settings.SetGiveFoodEnabled(food.defName, enabled);
                    }
                }
            }

            if (!any)
            {
                Empty(list, "RHAH_Settings_GiveFoods_Empty");
            }
        }


        void DrawBegFoodList(Listing_Standard list, RHAH_Settings settings)
        {
            MenuControls.Anchor(list, "beg-foods", 120f);
            RHAH_IrisMenusWidgets.Quote(list, "beg-foods-note", "RHAH_Settings_BegFoods_Quote".Translate());
            DrawModeSelect(list, "beg-food-sort", "RHAH_Settings_BegFoods_Sort", settings.begFoodListMode, 3, mode => settings.begFoodListMode = mode);
            Rect buttons = list.GetRect(28f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 140f, 26f),
                "RHAH_Settings_BegFoods_All".Translate()))
            {
                settings.SetAllBegFood(true, null);
            }

            if (Widgets.ButtonText(new Rect(buttons.x + 148f, buttons.y, 140f, 26f),
                "RHAH_Settings_BegFoods_None".Translate()))
            {
                List<ThingDef> foods = new List<ThingDef>();
                RHAH_ReliefFood.AppendBegFoods(foods);
                List<string> names = new List<string>(foods.Count);
                for (int i = 0; i < foods.Count; i++)
                {
                    names.Add(foods[i].defName);
                }

                settings.SetAllBegFood(false, names);
            }

            list.Gap(4f);
            Rect search = list.GetRect(28f);
            begFoodQuery = Widgets.TextField(search, begFoodQuery ?? string.Empty);
            if (string.IsNullOrEmpty(begFoodQuery))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(search.x + 6f, search.y, search.width - 8f, search.height),
                    "RHAH_Settings_BegFoods_Search".Translate());
                GUI.color = Color.white;
            }

            list.Gap(4f);
            List<ThingDef> listed = new List<ThingDef>();
            RHAH_ReliefFood.AppendBegFoods(listed);
            List<List<ThingDef>> groups = RHAH_ReliefFood.GroupFoods(settings.begFoodListMode, listed);
            bool searching = !string.IsNullOrEmpty(begFoodQuery);
            bool any = false;
            for (int i = 0; i < groups.Count; i++)
            {
                List<ThingDef> matched = MatchedBegFoods(groups[i]);
                if (matched.Count == 0)
                {
                    continue;
                }

                any = true;
                string key = RHAH_ReliefFood.GroupKey(settings.begFoodListMode, groups[i][0]);
                if (pendingBegFoodGroup != null && string.Equals(pendingBegFoodGroup, key, StringComparison.Ordinal))
                {
                    collapsedBegFoodGroups.Remove(key);
                    pendingBegFoodGroup = null;
                }

                bool open = searching || !collapsedBegFoodGroups.Contains(key);
                string title = "RHAH_Menu_Format_GroupCount".Translate(GiveFoodTitle(settings.begFoodListMode, key), matched.Count);
                MenuControls.Anchor(list, "beg-food-group-" + key, 28f);
                if (DrawFoodFold(list, title, open))
                {
                    if (open)
                    {
                        collapsedBegFoodGroups.Add(key);
                    }
                    else
                    {
                        collapsedBegFoodGroups.Remove(key);
                    }

                    open = !open;
                }

                if (!open)
                {
                    continue;
                }

                for (int foodIndex = 0; foodIndex < matched.Count; foodIndex++)
                {
                    ThingDef food = matched[foodIndex];
                    bool enabled = settings.IsBegFoodEnabled(food.defName);
                    if (RHAH_IrisMenusWidgets.Checkbox(list, "beg-food-" + food.defName, food.LabelCap, ref enabled))
                    {
                        settings.SetBegFoodEnabled(food.defName, enabled);
                    }
                }
            }

            if (!any)
            {
                Empty(list, "RHAH_Settings_BegFoods_Empty");
            }
        }

        List<ThingDef> MatchedBegFoods(List<ThingDef> foods)
        {
            List<ThingDef> matched = new List<ThingDef>();
            for (int i = 0; i < foods.Count; i++)
            {
                if (RHAH_ReliefFood.MatchesQuery(foods[i], begFoodQuery))
                {
                    matched.Add(foods[i]);
                }
            }

            return matched;
        }

        List<ThingDef> MatchedGiveFoods(List<ThingDef> foods)
        {
            List<ThingDef> matched = new List<ThingDef>();
            for (int i = 0; i < foods.Count; i++)
            {
                if (RHAH_ReliefFood.MatchesQuery(foods[i], giveFoodQuery))
                {
                    matched.Add(foods[i]);
                }
            }

            return matched;
        }

        static string GiveFoodTitle(int mode, string key)
        {
            string title = RHAH_ReliefFood.GroupTitle(mode, key);
            return title ?? (mode == 2 ? "RHAH_Settings_GiveFoods_Other".Translate() : "RHAH_Menu_Genes_UnknownMod".Translate());
        }

        List<ThingDef> MatchedFoods(List<ThingDef> foods)
        {
            List<ThingDef> matched = new List<ThingDef>();
            for (int i = 0; i < foods.Count; i++)
            {
                if (RHAH_ReliefFood.MatchesQuery(foods[i], foodQuery))
                {
                    matched.Add(foods[i]);
                }
            }

            return matched;
        }

        static bool DrawFoodFold(Listing_Standard list, string title, bool open)
        {
            Rect row = list.GetRect(28f);
            if (!RHAH_IrisMenusWidgets.IsVisible(row))
            {
                return false;
            }

            Widgets.DrawHighlightIfMouseover(row);
            Rect mark = new Rect(row.x, row.y + 2f, 24f, 24f);
            Widgets.DrawTextureFitted(mark, open ? TexButton.Collapse : TexButton.Reveal, 0.7f);
            Widgets.Label(new Rect(row.x + 26f, row.y, row.width - 26f, row.height), title);
            return Widgets.ButtonInvisible(row);
        }

        IEnumerable<MenuSearchEntry> SearchRelief()
        {
            yield return Entry("relief-enabled", "RHAH_Settings_ReliefEnabled");
            yield return Entry("relief-outside", "RHAH_Settings_EatOutsideRelief");
            yield return Entry("relief-foods", "RHAH_Settings_ReliefFoods");
            yield return Entry("give-foods", "RHAH_Settings_GiveFoods");
            yield return Entry("beg-foods", "RHAH_Settings_BegFoods");
            yield return Entry("give-food-sort", "RHAH_Settings_GiveFoods_Sort");
            List<ThingDef> foods = new List<ThingDef>();
            RHAH_ReliefFood.AppendCandidateFoods(foods);
            for (int i = 0; i < foods.Count; i++)
            {
                ThingDef food = foods[i];
                if (food == null || string.IsNullOrEmpty(food.defName))
                {
                    continue;
                }

                string modName = RHAH_ReliefFood.SourceModName(food);
                string key = modName ?? string.Empty;
                string id = "relief-food-" + food.defName;
                string label = food.LabelCap;
                yield return new MenuSearchEntry(
                    id,
                    () => label,
                    () => food.defName + " " + (modName ?? string.Empty),
                    () =>
                    {
                        pendingFoodMod = key;
                        return modName;
                    });
            }

            List<ThingDef> giveFoods = new List<ThingDef>();
            RHAH_ReliefFood.AppendCandidateFoods(giveFoods);
            int mode = RHAH_Mod.Settings == null ? 0 : RHAH_Mod.Settings.giveFoodListMode;
            for (int i = 0; i < giveFoods.Count; i++)
            {
                ThingDef food = giveFoods[i];
                if (food == null || string.IsNullOrEmpty(food.defName))
                {
                    continue;
                }

                string key = RHAH_ReliefFood.GroupKey(mode, food);
                string id = "give-food-" + food.defName;
                string label = food.LabelCap;
                yield return new MenuSearchEntry(
                    id,
                    () => label,
                    () => food.defName + " " + key,
                    () =>
                    {
                        pendingGiveFoodGroup = key;
                        return GiveFoodTitle(mode, key);
                    });
            }
        }
        void DrawCompatDiagnostics(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_CompatDiagnostics");
            Status(list, "RHAH_Menu_Field_Mod", "RHAH_ModName".Translate());
            Status(list, "RHAH_Menu_Field_Version", ContentFinderVersion());
            Status(list, "RHAH_Menu_Field_Game",
                Current.Game == null ? "RHAH_Menu_NoGame".Translate() : "RHAH_Menu_GameLoaded".Translate());
            DrawModRow(list, "ratkin", "NewRatkinPlus", "Solaris.RatkinRaceMod", true);
            DrawModRow(list, "harmony", "Harmony", "brrainz.harmony", true);
            DrawModRow(list, "biotech", "Biotech", "Ludeon.RimWorld.Biotech", true);
            DrawModRow(list, "iris", "IrisMenus", PackageId(), false);
            DrawModRow(list, "toddlers", "Toddlers", "cyanobot.toddlers", false);
            DrawModRow(list, "leash", "Lead Your Pet", "nanaloveyuki.leadyourpet.continued", false);
            DrawModRow(list, "prisoner", "Prisoner Work", "LeZhizhong.PrisonerWorkExpansion", false);
            Status(list, "RHAH_Menu_Field_IrisMenus", string.IsNullOrEmpty(irisVersion) ? "RHAH_Menu_VersionUnknown".Translate() : irisVersion);
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (game == null)
            {
                Empty(list, "RHAH_Menu_Diagnostics_NoGame");
                return;
            }

            Status(list, "RHAH_Menu_Diagnostics_Pending", game.PendingIncidentDisplayIds.Count.ToString());
            Status(list, "RHAH_Menu_Diagnostics_Batches", game.ActiveGenerationBatches.Count.ToString());
            Map map = RHAH_MapResolver.Resolve();
            Status(list, "RHAH_Menu_Diagnostics_Map",
                map == null ? "RHAH_Menu_Queue_NoMap".Translate() : map.uniqueID.ToString());
        }

        IEnumerable<MenuSearchEntry> SearchCompatDiagnostics()
        {
            yield return Entry("compat-mod", "RHAH_Menu_Field_Mod");
            yield return Entry("compat-version", "RHAH_Menu_Field_Version");
            yield return Entry("compat-game", "RHAH_Menu_Field_Game");
            yield return Entry("compat-iris", "RHAH_Menu_Field_IrisMenus");
            yield return Entry("diagnostics-pending", "RHAH_Menu_Diagnostics_Pending");
        }

        static void DrawModRow(Listing_Standard list, string anchor, string label, string packageId, bool required)
        {
            MenuControls.Anchor(list, "compat-" + anchor);
            ModMetaData meta = ModLister.GetActiveModWithIdentifier(packageId, false);
            string state = meta == null
                ? (required ? "RHAH_Menu_Compat_MissingRequired".Translate() : "RHAH_Menu_Compat_Inactive".Translate())
                : "RHAH_Menu_Compat_Active".Translate(VersionOf(meta));
            Status(list, label, state);
        }

        static string PackageId()
        {
            return RHAH_IrisMenusCompat.PackageId;
        }



        void DrawGenes(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Genes");
            if (!ModsConfig.BiotechActive)
            {
                Empty(list, "RHAH_Menu_Genes_NoBiotech");
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            RHAH_IrisMenusWidgets.Quote(list, "genes-note", "RHAH_Menu_Genes_Note".Translate());
            if (list.ButtonText("RHAH_Menu_Genes_Reset".Translate()))
            {
                settings.ResetXenotypeWeights();
            }

            List<XenotypeDef> xenotypes = RHAH_XenotypeResolver.LoadedCandidates(settings);
            float total = 0f;
            for (int i = 0; i < xenotypes.Count; i++)
            {
                float weight = settings.XenotypeWeight(xenotypes[i].defName);
                if (weight > 0f)
                {
                    total += weight;
                }
            }

            ReservedNote(list, "genes-fallback", "RHAH_Menu_Genes_Fallback", total <= 0f);

            DrawXenotypesByMod(list, xenotypes, group =>
            {
                for (int i = 0; i < group.Count; i++)
                {
                    DrawXenotypeBar(list, settings, group[i], total);
                }
            });

            DrawMissingXenotypes(list, settings, xenotypes);
            DrawJoinableXenotypes(list, settings);
            DrawGeneSwitches(list, settings);
        }

        static void DrawJoinableXenotypes(Listing_Standard list, RHAH_Settings settings)
        {
            List<XenotypeDef> available = RHAH_XenotypeResolver.AvailableToJoin(settings);
            if (available.Count == 0)
            {
                return;
            }

            Section(list, "RHAH_Menu_Genes_Join");
            DrawXenotypesByMod(list, available, group =>
            {
                for (int i = 0; i < group.Count; i++)
                {
                    XenotypeDef xenotype = group[i];
                    if (!RHAH_IrisMenusWidgets.IsVisible(new Rect(0f, list.CurHeight, list.ColumnWidth, 30f)))
                    {
                        list.GetRect(30f);
                        list.Gap(list.verticalSpacing);
                        continue;
                    }

                    if (list.ButtonText("RHAH_Menu_Genes_JoinOne".Translate(xenotype.LabelCap)))
                    {
                        settings.SetXenotypeEnabled(xenotype.defName, true);
                    }
                }
            });
        }

        static void DrawXenotypesByMod(Listing_Standard list, List<XenotypeDef> xenotypes, Action<List<XenotypeDef>> drawGroup)
        {
            List<List<XenotypeDef>> groups = RHAH_XenotypeResolver.GroupBySourceMod(xenotypes);
            for (int i = 0; i < groups.Count; i++)
            {
                string name = RHAH_XenotypeResolver.SourceModName(groups[i][0]);
                RHAH_IrisMenusWidgets.Section(list, name ?? "RHAH_Menu_Genes_UnknownMod".Translate());
                drawGroup(groups[i]);
            }
        }

        void DrawXenotypeBar(Listing_Standard list, RHAH_Settings settings, XenotypeDef xenotype, float total)
        {
            float height = 30f + RHAH_IrisMenusWidgets.CardGap;
            if (!RHAH_GeneCatalog.IsBuiltin(xenotype.defName))
            {
                height += 30f;
            }

            if (!RHAH_IrisMenusWidgets.IsVisible(new Rect(0f, list.CurHeight, list.ColumnWidth, height)))
            {
                list.Gap(height);
                return;
            }

            float weight = settings.XenotypeWeight(xenotype.defName);
            float share = total <= 0f || weight <= 0f ? 0f : weight / total;
            string buffer = Buffer(weightBuffers, "xeno-" + xenotype.defName, weight, "0");
            weight = RHAH_IrisMenusWidgets.TunedValue(
                list,
                "RHAH_Menu_Format_XenotypeShare".Translate(xenotype.LabelCap, share.ToString("P0")),
                weight,
                ref buffer,
                RHAH_XenotypeWeightTable.MinWeight,
                RHAH_XenotypeWeightTable.MaxWeight,
                "0",
                "RHAH_Menu_Genes_WeightTip".Translate(xenotype.defName, RHAH_GeneCatalog.SuggestedWeight(xenotype.defName).ToString("0")));
            weightBuffers["xeno-" + xenotype.defName] = buffer;
            settings.SetXenotypeWeight(xenotype.defName, weight);
            if (!RHAH_GeneCatalog.IsBuiltin(xenotype.defName))
            {
                Rect remove = list.GetRect(26f);
                if (RHAH_IrisMenusWidgets.IsVisible(remove) &&
                    Widgets.ButtonText(new Rect(remove.xMax - 72f, remove.y, 68f, 24f), "RHAH_Menu_Genes_Remove".Translate()))
                {
                    settings.SetXenotypeEnabled(xenotype.defName, false);
                }

                list.Gap(4f);
            }
        }



        static void DrawMissingXenotypes(Listing_Standard list, RHAH_Settings settings, List<XenotypeDef> loaded)
        {
            List<string> stored = settings.MissingXenotypeNames();
            for (int i = 0; i < stored.Count; i++)
            {
                if (Loaded(loaded, stored[i]) || RHAH_GeneCatalog.IsBuiltin(stored[i]))
                {
                    continue;
                }

                RHAH_IrisMenusWidgets.Label(list, "RHAH_Menu_Genes_Missing".Translate(stored[i]));
            }
        }

        static bool Loaded(List<XenotypeDef> loaded, string defName)
        {
            for (int i = 0; i < loaded.Count; i++)
            {
                if (loaded[i].defName == defName)
                {
                    return true;
                }
            }

            return false;
        }

        void DrawGeneSwitches(Listing_Standard list, RHAH_Settings settings)
        {
            Section(list, "RHAH_Menu_Genes_Switches");
            List<GeneDef> genes = RHAH_XenotypeResolver.LoadedOwnedGenes();
            if (genes.Count == 0)
            {
                Empty(list, "RHAH_Menu_Genes_NoOwned");
                return;
            }

            for (int i = 0; i < genes.Count; i++)
            {
                bool enabled = settings.IsGeneEnabled(genes[i].defName);
                if (RHAH_IrisMenusWidgets.Checkbox(list, "gene-" + genes[i].defName, genes[i].LabelCap, ref enabled, genes[i].description))
                {
                    settings.SetGeneEnabled(genes[i].defName, enabled);
                }
                DrawFertility(list, settings, genes[i].defName);
                DrawParentalInstinct(list, settings, genes[i].defName);
            }
        }
        void DrawFertility(Listing_Standard list, RHAH_Settings settings, string defName)
        {
            if (!settings.IsGeneEnabled(defName))
            {
                return;
            }

            if (defName == RHAH_FertilityRules.LargeLitter)
            {
                string minimum = Buffer(weightBuffers, "litter-min", settings.litterMin, "0");
                settings.litterMin = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_LitterMin".Translate(settings.litterMin), settings.litterMin, ref minimum, RHAH_FertilityRules.MinLitter, RHAH_FertilityRules.MaxLitter, "0", "RHAH_Menu_Genes_LitterMinTip".Translate());
                weightBuffers["litter-min"] = minimum;
                string maximum = Buffer(weightBuffers, "litter-max", settings.litterMax, "0");
                settings.litterMax = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_LitterMax".Translate(settings.litterMax), settings.litterMax, ref maximum, settings.litterMin, RHAH_FertilityRules.MaxLitter, "0", "RHAH_Menu_Genes_LitterMaxTip".Translate());
                weightBuffers["litter-max"] = maximum;
                string peak = Buffer(weightBuffers, "litter-peak", settings.litterPeak, "0");
                settings.litterPeak = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_LitterPeak".Translate(settings.litterPeak), settings.litterPeak, ref peak, settings.litterMin, settings.litterMax, "0", "RHAH_Menu_Genes_LitterPeakTip".Translate());
                weightBuffers["litter-peak"] = peak;
                RHAH_FertilityRules.ClampLitter(ref settings.litterMin, ref settings.litterPeak, ref settings.litterMax);
                RHAH_IrisMenusWidgets.LitterCurve(list, "litter-curve", settings.litterMin, settings.litterPeak, settings.litterMax);
                return;
            }

            if (defName == RHAH_FertilityRules.EarlyFertility)
            {
                string age = Buffer(weightBuffers, "fertile-age", settings.fertileMinAge, "0");
                settings.fertileMinAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_FertileAge".Translate(settings.fertileMinAge.ToString("0")), settings.fertileMinAge, ref age, RHAH_FertilityRules.MinFertileAge, RHAH_FertilityRules.MaxFertileAge, "0", "RHAH_Menu_Genes_FertileAgeTip".Translate());
                weightBuffers["fertile-age"] = age;
                return;
            }

            if (defName == RHAH_FertilityRules.HighFertility)
            {
                string percent = Buffer(weightBuffers, "fertility-percent", settings.fertilityPercent, "0");
                settings.fertilityPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_FertilityPercent".Translate(settings.fertilityPercent.ToString("0")), settings.fertilityPercent, ref percent, RHAH_FertilityRules.MinFertilityPercent, RHAH_FertilityRules.MaxFertilityPercent, "0", "RHAH_Menu_Genes_FertilityTip".Translate());
                weightBuffers["fertility-percent"] = percent;
                return;
            }

            if (defName == RHAH_FertilityRules.RoomFertility)
            {
                string percent = Buffer(weightBuffers, "lovin-pregnancy", settings.lovinPregnancyPercent, "0");
                settings.lovinPregnancyPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_LovinPregnancy".Translate(settings.lovinPregnancyPercent.ToString("0")), settings.lovinPregnancyPercent, ref percent, RHAH_FertilityRules.MinLovinPregnancyPercent, RHAH_FertilityRules.MaxLovinPregnancyPercent, "0", "RHAH_Menu_Genes_LovinPregnancyTip".Translate());
                weightBuffers["lovin-pregnancy"] = percent;
                return;
            }

            if (defName == RHAH_FertilityRules.FastBirth)
            {
                string days = Buffer(weightBuffers, "gestation-days", settings.gestationDays, "0.0");
                settings.gestationDays = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_GestationDays".Translate(settings.gestationDays.ToString("0.0")), settings.gestationDays, ref days, RHAH_FertilityRules.MinGestationDays, RHAH_FertilityRules.VanillaGestationFloorDays, "0.0", "RHAH_Menu_Genes_GestationTip".Translate());
                weightBuffers["gestation-days"] = days;
            }
        }

        void DrawParentalInstinct(Listing_Standard list, RHAH_Settings settings, string defName)
        {
            if (!settings.IsGeneEnabled(defName))
            {
                return;
            }

            if (defName == RHAH_ParentalGenes.MaternalInstinct)
            {
                MenuControls.Anchor(list, "maternal-milk");
                string milk = Buffer(weightBuffers, "maternal-milk", settings.maternalMilkPercent, "0");
                settings.maternalMilkPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaternalMilk".Translate(settings.maternalMilkPercent.ToString("0")), settings.maternalMilkPercent, ref milk, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_MaternalMilkTip".Translate());
                weightBuffers["maternal-milk"] = milk;
                MenuControls.Anchor(list, "maternal-hunger");
                string hunger = Buffer(weightBuffers, "maternal-hunger", settings.maternalHungerPercent, "0");
                settings.maternalHungerPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaternalHunger".Translate(settings.maternalHungerPercent.ToString("0")), settings.maternalHungerPercent, ref hunger, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_MaternalHungerTip".Translate());
                weightBuffers["maternal-hunger"] = hunger;
                MenuControls.Anchor(list, "maternal-nutrition");
                string nutrition = Buffer(weightBuffers, "maternal-nutrition", settings.maternalNutritionPercent, "0");
                settings.maternalNutritionPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaternalNutrition".Translate(settings.maternalNutritionPercent.ToString("0")), settings.maternalNutritionPercent, ref nutrition, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_MaternalNutritionTip".Translate());
                weightBuffers["maternal-nutrition"] = nutrition;
                return;
            }

            if (defName == RHAH_ParentalGenes.PaternalInstinct)
            {
                MenuControls.Anchor(list, "paternal-work");
                string work = Buffer(weightBuffers, "paternal-work", settings.paternalWorkPercent, "0");
                settings.paternalWorkPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PaternalWork".Translate(settings.paternalWorkPercent.ToString("0")), settings.paternalWorkPercent, ref work, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_PaternalWorkTip".Translate());
                weightBuffers["paternal-work"] = work;
                MenuControls.Anchor(list, "paternal-hunger");
                string hunger = Buffer(weightBuffers, "paternal-hunger", settings.paternalHungerPercent, "0");
                settings.paternalHungerPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PaternalHunger".Translate(settings.paternalHungerPercent.ToString("0")), settings.paternalHungerPercent, ref hunger, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_PaternalHungerTip".Translate());
                weightBuffers["paternal-hunger"] = hunger;
                MenuControls.Anchor(list, "paternal-learning");
                string learning = Buffer(weightBuffers, "paternal-learning", settings.paternalLearningPercent, "0");
                settings.paternalLearningPercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PaternalLearning".Translate(settings.paternalLearningPercent.ToString("0")), settings.paternalLearningPercent, ref learning, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxPercent, "0", "RHAH_Menu_Genes_PaternalLearningTip".Translate());
                weightBuffers["paternal-learning"] = learning;
                MenuControls.Anchor(list, "paternal-damage");
                string damage = Buffer(weightBuffers, "paternal-damage", settings.paternalDamagePercent, "0");
                settings.paternalDamagePercent = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PaternalDamage".Translate(settings.paternalDamagePercent.ToString("0")), settings.paternalDamagePercent, ref damage, RHAH_ParentalGenes.MinPercent, RHAH_ParentalGenes.MaxDamagePercent, "0", "RHAH_Menu_Genes_PaternalDamageTip".Translate());
                weightBuffers["paternal-damage"] = damage;
            }
        }

        void DrawPawnHistory(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_PawnHistory");
            RHAH_IrisMenusWidgets.Quote(list, "history-note", "RHAH_Menu_PawnHistory_Note".Translate());
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }
            DrawBodyControls(list, settings);
            DrawContentSearch(list);
            DrawContentToggles(list, settings);
        }

        void DrawBodyControls(Listing_Standard list, RHAH_Settings settings)
        {
            string minAge = Buffer(weightBuffers, "body-age-min", settings.minGeneratedAge, "0.0");
            settings.minGeneratedAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MinAge".Translate(settings.minGeneratedAge.ToString("0.0")), settings.minGeneratedAge, ref minAge, 0f, 100f, "0.0", "RHAH_Settings_Age_Tooltip".Translate());
            weightBuffers["body-age-min"] = minAge;
            string maxAge = Buffer(weightBuffers, "body-age-max", settings.maxGeneratedAge, "0.0");
            settings.maxGeneratedAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaxAge".Translate(settings.maxGeneratedAge.ToString("0.0")), settings.maxGeneratedAge, ref maxAge, settings.minGeneratedAge, 100f, "0.0", "RHAH_Settings_Age_Tooltip".Translate());
            weightBuffers["body-age-max"] = maxAge;
            MenuControls.Anchor(list, "mother-age");
            string motherAge = Buffer(weightBuffers, "body-mother-age", settings.minMotherAge, "0");
            settings.minMotherAge = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MotherAge".Translate(settings.minMotherAge.ToString("0")), settings.minMotherAge, ref motherAge, RHAH_VisitorRules.MinMotherAge, RHAH_VisitorRules.MaxMotherAge, "0", "RHAH_Settings_MotherAge_Tooltip".Translate());
            weightBuffers["body-mother-age"] = motherAge;
            MenuControls.Anchor(list, "young-age");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_YoungAge".Translate(), ref settings.youngAgeFollowsRange, "RHAH_Settings_YoungAge_Tooltip".Translate());
            MenuControls.Anchor(list, "immobile-babies");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ImmobileBabies".Translate(), ref settings.allowImmobileBabies, ToddlersActive() ? "RHAH_Settings_ImmobileBabies_Toddlers".Translate() : "RHAH_Settings_ImmobileBabies_Tooltip".Translate());
            DrawModeSelect(list, "gender-mode", "RHAH_Settings_Gender", settings.genderMode, 4, mode => settings.genderMode = mode);
            if (settings.genderMode == 1)
            {
                string share = Buffer(weightBuffers, "female-share", settings.femaleSharePercent, "0");
                settings.femaleSharePercent = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_FemaleShare".Translate(settings.femaleSharePercent), settings.femaleSharePercent, ref share, 0f, 100f, "0", "RHAH_Settings_FemaleShare_Tooltip".Translate());
                weightBuffers["female-share"] = share;
            }

            string ideo = Buffer(weightBuffers, "player-ideo", settings.playerIdeoPercent, "0");
            settings.playerIdeoPercent = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_PlayerIdeo".Translate(settings.playerIdeoPercent), settings.playerIdeoPercent, ref ideo, 0f, 100f, "0", "RHAH_Settings_PlayerIdeo_Tooltip".Translate());
            weightBuffers["player-ideo"] = ideo;

            DrawModeSelect(list, "apparel-mode", "RHAH_Settings_Apparel", settings.apparelMode, 4, mode => settings.apparelMode = mode);
            DrawApparelIntensity(list, settings);
            DrawApparelList(list, settings);
            string traits = Buffer(weightBuffers, "owned-traits", settings.maxOwnedTraits, "0");
            settings.maxOwnedTraits = (int)RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Settings_MaxTraits".Translate(settings.maxOwnedTraits), settings.maxOwnedTraits, ref traits, 0f, 3f, "0", "RHAH_Settings_MaxTraits_Tooltip".Translate());
            weightBuffers["owned-traits"] = traits;
            MenuControls.Anchor(list, "vanilla-traits");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_VanillaTraits".Translate(), ref settings.allowVanillaTraits, "RHAH_Settings_VanillaTraits_Tooltip".Translate());
            MenuControls.Anchor(list, "trait-age");
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_TraitAge".Translate(), ref settings.traitAgeFilter, "RHAH_Settings_TraitAge_Tooltip".Translate());
            DrawModeSelect(list, "content-sort", "RHAH_Settings_ContentSort", settings.contentListMode, 3, mode => settings.contentListMode = mode);
        }


        void DrawApparelList(Listing_Standard list, RHAH_Settings settings)
        {
            MenuControls.Anchor(list, "refugee-apparel", 120f);
            RHAH_IrisMenusWidgets.Quote(list, "refugee-apparel-note", "RHAH_Settings_RefugeeApparel_Quote".Translate());
            DrawModeSelect(list, "refugee-apparel-sort", "RHAH_Settings_RefugeeApparel_Sort", settings.apparelListMode, 3, mode => settings.apparelListMode = mode);
            Rect buttons = list.GetRect(28f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, 140f, 26f), "RHAH_Settings_RefugeeApparel_All".Translate()))
            {
                settings.SetAllRefugeeApparel(true, null);
            }

            if (Widgets.ButtonText(new Rect(buttons.x + 148f, buttons.y, 140f, 26f), "RHAH_Settings_RefugeeApparel_None".Translate()))
            {
                List<ThingDef> apparel = new List<ThingDef>();
                RHAH_ApparelAssigner.AppendCandidates(apparel);
                List<string> names = new List<string>(apparel.Count);
                for (int i = 0; i < apparel.Count; i++)
                {
                    names.Add(apparel[i].defName);
                }

                settings.SetAllRefugeeApparel(false, names);
            }

            list.Gap(4f);
            Rect search = list.GetRect(28f);
            apparelQuery = Widgets.TextField(search, apparelQuery ?? string.Empty);
            if (string.IsNullOrEmpty(apparelQuery))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(search.x + 6f, search.y, search.width - 8f, search.height), "RHAH_Settings_RefugeeApparel_Search".Translate());
                GUI.color = Color.white;
            }

            list.Gap(4f);
            List<ThingDef> listed = new List<ThingDef>();
            RHAH_ApparelAssigner.AppendCandidates(listed);
            List<List<ThingDef>> groups = RHAH_ReliefFood.GroupFoods(settings.apparelListMode, listed);
            bool searching = !string.IsNullOrEmpty(apparelQuery);
            bool any = false;
            for (int i = 0; i < groups.Count; i++)
            {
                List<ThingDef> matched = MatchedApparel(groups[i]);
                if (matched.Count == 0)
                {
                    continue;
                }

                any = true;
                string key = RHAH_ReliefFood.GroupKey(settings.apparelListMode, groups[i][0]);
                if (pendingApparelGroup != null && string.Equals(pendingApparelGroup, key, StringComparison.Ordinal))
                {
                    collapsedApparelGroups.Remove(key);
                    pendingApparelGroup = null;
                }

                bool open = searching || !collapsedApparelGroups.Contains(key);
                string title = "RHAH_Menu_Format_GroupCount".Translate(ApparelTitle(settings.apparelListMode, key), matched.Count);
                MenuControls.Anchor(list, "refugee-apparel-group-" + key, 28f);
                if (DrawFoodFold(list, title, open))
                {
                    if (open)
                    {
                        collapsedApparelGroups.Add(key);
                    }
                    else
                    {
                        collapsedApparelGroups.Remove(key);
                    }

                    open = !open;
                }

                if (!open)
                {
                    continue;
                }

                for (int apparelIndex = 0; apparelIndex < matched.Count; apparelIndex++)
                {
                    ThingDef def = matched[apparelIndex];
                    bool enabled = settings.IsRefugeeApparelEnabled(def.defName);
                    if (RHAH_IrisMenusWidgets.Checkbox(list, "refugee-apparel-" + def.defName, def.LabelCap, ref enabled,
                        "RHAH_Settings_RefugeeApparel_Tooltip".Translate()))
                    {
                        settings.SetRefugeeApparelEnabled(def.defName, enabled);
                    }
                }
            }

            if (!any)
            {
                Empty(list, "RHAH_Settings_RefugeeApparel_Empty");
            }
        }

        List<ThingDef> MatchedApparel(List<ThingDef> apparel)
        {
            List<ThingDef> matched = new List<ThingDef>();
            for (int i = 0; i < apparel.Count; i++)
            {
                if (RHAH_ReliefFood.MatchesQuery(apparel[i], apparelQuery))
                {
                    matched.Add(apparel[i]);
                }
            }

            return matched;
        }

        static string ApparelTitle(int mode, string key)
        {
            string title = RHAH_ReliefFood.GroupTitle(mode, key);
            return title ?? (mode == 2 ? "RHAH_Settings_GiveFoods_Other".Translate() : "RHAH_Menu_Genes_UnknownMod".Translate());
        }

        void DrawModeSelect(Listing_Standard list, string anchor, string key, int selected, int count, Action<int> assign)
        {
            List<int> options = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                options.Add(i);
            }

            MenuControls.Anchor(list, anchor);
            MenuControls.Select(list, key.Translate(), selected, options, mode => (key + "_" + mode).Translate(), assign);
        }

        void DrawContentSearch(Listing_Standard list)
        {
            MenuControls.Anchor(list, "content-search", 32f);
            Rect search = list.GetRect(28f);
            contentQuery = Widgets.TextField(search, contentQuery ?? string.Empty);
            if (string.IsNullOrEmpty(contentQuery))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(search.x + 6f, search.y, search.width - 8f, search.height), "RHAH_Settings_ContentSearch".Translate());
                GUI.color = Color.white;
            }
        }

        static bool ToddlersActive()
        {
            return ModsConfig.IsActive("cyanobot.toddlers");
        }

        void DrawContentToggles(Listing_Standard list, RHAH_Settings settings)
        {
            DrawGrouped(list, settings, true);
            Section(list, "RHAH_Menu_PawnTrait");
            DrawGrouped(list, settings, false);
        }

        void DrawGrouped(Listing_Standard list, RHAH_Settings settings, bool histories)
        {
            List<string> ids = new List<string>();
            if (histories)
            {
                RHAH_Api.CopyHistoryIds(ids);
            }
            else
            {
                RHAH_Api.CopyTraitIds(ids);
            }

            Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
            List<string> order = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                string title = histories ? HistoryLabel(id) : TraitLabel(id);
                RHAH_ContentCategory category = histories
                    ? (RHAH_ContentCatalog.FindHistory(id)?.Category ?? RHAH_ContentCategory.None)
                    : (RHAH_ContentCatalog.FindTrait(id)?.Category ?? RHAH_ContentCategory.None);
                if (!RHAH_ContentSelector.MatchesQuery(title, id, category.ToString(), contentQuery))
                {
                    continue;
                }
                string key = RHAH_ContentSelector.ListKey(settings.contentListMode, id, category, "RHAH_Menu_Content_Owned".Translate(), true);
                List<string> bucket;
                if (!groups.TryGetValue(key, out bucket))
                {
                    bucket = new List<string>();
                    groups[key] = bucket;
                    order.Add(key);
                }

                bucket.Add(id);
            }

            order.Sort(StringComparer.CurrentCultureIgnoreCase);
            bool searching = !string.IsNullOrEmpty(contentQuery);
            for (int i = 0; i < order.Count; i++)
            {
                string key = order[i];
                string fold = (histories ? "history-" : "trait-") + key;
                if (pendingContentGroup == fold)
                {
                    collapsedContentGroups.Remove(fold);
                    pendingContentGroup = null;
                }

                bool open = searching || !collapsedContentGroups.Contains(fold);
                MenuControls.Anchor(list, fold, 28f);
                if (DrawFoodFold(list, "RHAH_Menu_Format_GroupCount".Translate(GroupTitle(key), groups[key].Count), open))
                {
                    if (open)
                    {
                        collapsedContentGroups.Add(fold);
                    }
                    else
                    {
                        collapsedContentGroups.Remove(fold);
                    }

                    open = !open;
                }

                if (!open)
                {
                    continue;
                }

                List<string> bucket = groups[key];
                for (int j = 0; j < bucket.Count; j++)
                {
                    DrawContentRow(list, settings, bucket[j], histories);
                }
            }
        }

        void DrawContentRow(Listing_Standard list, RHAH_Settings settings, string id, bool history)
        {
            string anchor = (history ? "history-" : "trait-") + id;
            bool enabled = history ? settings.IsHistoryEnabled(id) : settings.IsTraitEnabled(id);
            string label = "RHAH_Menu_Format_IdentifiedLabel".Translate(id, history ? HistoryLabel(id) : TraitLabel(id));
            bool visible = RHAH_IrisMenusWidgets.Checkbox(list, anchor, label, ref enabled,
                (history ? "RHAH_Menu_PawnHistory_ItemTip" : "RHAH_Menu_PawnTrait_ItemTip").Translate());
            if (history)
            {
                if (visible)
                {
                    settings.SetHistoryEnabled(id, enabled);
                }
                return;
            }

            if (visible)
            {
                settings.SetTraitEnabled(id, enabled);
            }
            if (!RHAH_IrisMenusWidgets.IsVisible(new Rect(0f, list.CurHeight, list.ColumnWidth, 30f)))
            {
                list.Gap(30f + RHAH_IrisMenusWidgets.CardGap);
                return;
            }

            float weight = settings.TraitWeight(id);
            string buffer = Buffer(weightBuffers, "trait-" + id, weight, "0");
            weight = RHAH_IrisMenusWidgets.TunedValue(list, "RHAH_Menu_PawnTrait_Weight".Translate(), weight, ref buffer, 0f, 100f, "0", "RHAH_Menu_PawnTrait_WeightTip".Translate());
            weightBuffers["trait-" + id] = buffer;
            settings.SetTraitWeight(id, weight);
        }

        static string HistoryLabel(string id)
        {
            string defName;
            return RHAH_Api.TryGetHistory(id, out defName) ? BackstoryTitle(defName) : id;
        }

        static string TraitLabel(string id)
        {
            string defName;
            return RHAH_Api.TryGetTrait(id, out defName) ? TraitTitle(defName) : id;
        }

        static string GroupTitle(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "RHAH_Menu_Genes_UnknownMod".Translate();
            }

            string named = "RHAH_Menu_Content_" + key;
            string translated = named.Translate();
            return translated == named ? key : translated;
        }

        static string BackstoryTitle(string defName)
        {
            BackstoryDef backstory = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            string title = backstory?.title;
            return string.IsNullOrEmpty(title) ? defName : title;
        }

        static string TraitTitle(string defName)
        {
            TraitDef trait = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (trait?.degreeDatas == null || trait.degreeDatas.Count == 0 || trait.degreeDatas[0] == null)
            {
                return defName;
            }

            string label = trait.degreeDatas[0].label;
            return string.IsNullOrEmpty(label) ? defName : label;
        }

        void DrawExperimental(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Experimental");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            MenuControls.Anchor(list, "optimize-generation");
            RHAH_IrisMenusWidgets.Checkbox(
                list,
                "RHAH_Settings_OptimizeGeneration".Translate(),
                ref settings.optimizeGeneration,
                "RHAH_Settings_OptimizeGeneration_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_AidRequests".Translate(), ref settings.aidRequestsEnabled, "RHAH_Settings_AidRequests_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_IntelTrades".Translate(), ref settings.intelTradesEnabled, "RHAH_Settings_IntelTrades_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_VisitorChoices".Translate(), ref settings.visitorChoicesEnabled, "RHAH_Settings_VisitorChoices_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_FoodGiveHint".Translate(), ref settings.foodGiveHintDismissed, "RHAH_Settings_FoodGiveHint_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_TraderIgnoreEnvironment".Translate(), ref settings.traderIgnoresHarshEnvironment, "RHAH_Settings_TraderIgnoreEnvironment_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_TraderIgnoreEnclosed".Translate(), ref settings.traderIgnoresEnclosedSpace, "RHAH_Settings_TraderIgnoreEnclosed_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_SpaceApproach".Translate(), ref settings.spaceApproachEnabled, "RHAH_Settings_SpaceApproach_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_ChildExchangeFood".Translate(), ref settings.childExchangeFoodSubstitution, "RHAH_Settings_ChildExchangeFood_Tooltip".Translate());
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_Stagger".Translate(), ref settings.staggerGeneration, "RHAH_Settings_Stagger_Tooltip".Translate());
        }

        void DrawFrequency(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_EventFrequency");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            MenuControls.Anchor(list, "frequency-window");
            settings.frequencyWindowDays = PoolWindow(
                list,
                "frequency-window",
                settings.frequencyWindowDays);
            PacePreview(out float today, out int trust, out int season);

            MenuControls.Anchor(list, "frequency-positive");
            settings.positiveIncidentDays = PoolDays(
                list,
                "frequency-positive",
                "RHAH_Menu_Frequency_Positive".Translate(),
                settings.positiveIncidentDays);
            settings.positiveIncidentPace = PaceField(
                list,
                "frequency-positive-pace",
                settings.positiveIncidentPace);
            RHAH_IrisMenusWidgets.OccurrenceCurve(
                list,
                "frequency-positive-curve",
                settings.positiveIncidentPace,
                settings.positiveIncidentDays,
                settings.frequencyWindowDays,
                today,
                trust,
                season,
                new Color(0.45f, 0.78f, 0.48f));

            MenuControls.Anchor(list, "frequency-negative");
            settings.negativeIncidentDays = PoolDays(
                list,
                "frequency-negative",
                "RHAH_Menu_Frequency_Negative".Translate(),
                settings.negativeIncidentDays);
            settings.negativeIncidentPace = PaceField(
                list,
                "frequency-negative-pace",
                settings.negativeIncidentPace);
            RHAH_IrisMenusWidgets.OccurrenceCurve(
                list,
                "frequency-negative-curve",
                settings.negativeIncidentPace,
                settings.negativeIncidentDays,
                settings.frequencyWindowDays,
                today,
                trust,
                season,
                new Color(0.86f, 0.42f, 0.36f));
            Factor(list, settings, "weight-wild", "RHAH_Settings_WeightWild", ref settings.weightWild);
            Factor(list, settings, "weight-beggar", "RHAH_Settings_WeightBeggar", ref settings.weightBeggar);
            Factor(list, settings, "weight-thief", "RHAH_Settings_WeightThief", ref settings.weightThief);
            Factor(list, settings, "weight-trade", "RHAH_Settings_WeightTrade", ref settings.weightTrade);
            Factor(list, settings, "weight-siege", "RHAH_Settings_WeightSiege", ref settings.weightSiege);
            Factor(list, settings, "weight-aid", "RHAH_Settings_WeightAid", ref settings.weightAid);
            Factor(list, settings, "weight-special", "RHAH_Settings_WeightSpecial", ref settings.weightSpecial);
            Factor(list, settings, "weight-intel", "RHAH_Settings_WeightIntel", ref settings.weightIntel);
            Factor(list, settings, "weight-season", "RHAH_Settings_WeightSeason", ref settings.weightSeason);
            Factor(list, settings, "weight-plague", "RHAH_Settings_WeightPlague", ref settings.weightPlague);
            RHAH_IrisMenusWidgets.Checkbox(list, "RHAH_Settings_SuiyinTempo".Translate(), ref settings.suiYinThreatTempo, "RHAH_Settings_SuiyinTempo_Tooltip".Translate());
            list.Gap(4f);
        }

        float PoolDays(Listing_Standard list, string id, string label, float days)
        {
            string buffer = Buffer(weightBuffers, id, days, "0.#");
            days = RHAH_IrisMenusWidgets.TunedValue(
                list,
                label,
                days,
                ref buffer,
                RHAH_IncidentSchedule.MinDays,
                RHAH_IncidentSchedule.MaxDays,
                "0.#",
                "RHAH_Menu_Frequency_Gap".Translate());
            weightBuffers[id] = buffer;
            return RHAH_IncidentSchedule.ClampDays(days);
        }

        float PoolWindow(Listing_Standard list, string id, float days)
        {
            string buffer = Buffer(weightBuffers, id, days, "0.#");
            days = RHAH_IrisMenusWidgets.TunedValue(
                list,
                "RHAH_Menu_Frequency_Window".Translate(),
                days,
                ref buffer,
                RHAH_IncidentSchedule.MinWindowDays,
                RHAH_IncidentSchedule.MaxWindowDays,
                "0.#",
                "RHAH_Menu_Frequency_WindowTip".Translate());
            weightBuffers[id] = buffer;
            return RHAH_IncidentSchedule.ClampWindowDays(days);
        }

        static void PacePreview(out float today, out int trust, out int season)
        {
            today = 1f;
            trust = 0;
            season = 0;
            if (Current.Game == null)
            {
                return;
            }

            today = Mathf.Max(0f, GenDate.DaysPassedSinceSettleFloat);
            trust = Current.Game.GetComponent<NarrativeState>()?.Snapshot().Trust ?? 0;
            season = RHAH_IncidentSchedule.SeasonIndex(RHAH_IncidentSchedule.SeasonOf(Find.AnyPlayerHomeMap));
        }

        string PaceField(Listing_Standard list, string id, string formula)
        {
            string shown = weightBuffers.TryGetValue(id, out string stored) ? stored : formula ?? RHAH_IncidentPace.DefaultFormula;
            float width = Mathf.Max(1f, list.ColumnWidth);
            float height = Text.CalcHeight("RHAH_Menu_Frequency_PaceTip".Translate(), width);
            MenuControls.Anchor(list, id, 30f + height + 8f);
            Rect row = list.GetRect(30f);
            Rect label = new Rect(row.x, row.y, row.width * 0.28f, row.height);
            Widgets.Label(label, "RHAH_Menu_Frequency_Pace".Translate());
            TooltipHandler.TipRegion(label, "RHAH_Menu_Frequency_PaceTip".Translate());
            string typed = Widgets.TextField(new Rect(label.xMax + 8f, row.y, row.width - label.width - 8f, row.height), shown);
            weightBuffers[id] = typed;
            Widgets.Label(list.GetRect(height), "RHAH_Menu_Frequency_PaceTip".Translate());
            list.Gap(4f);
            if (string.IsNullOrWhiteSpace(typed))
            {
                return RHAH_IncidentPace.DefaultFormula;
            }

            string trimmed = typed.Trim();
            return RHAH_IncidentPace.TryEvaluate(trimmed, new RHAH_IncidentPaceContext(1f, RHAH_IncidentSchedule.DefaultDays, 0, 0), out _)
                ? trimmed
                : formula ?? RHAH_IncidentPace.DefaultFormula;
        }


        void Factor(Listing_Standard list, RHAH_Settings settings, string id, string key, ref float value)
        {
            string buffer = Buffer(weightBuffers, id, value, "0.00");
            value = RHAH_IrisMenusWidgets.TunedValue(list, key.Translate(value.ToString("0.00")), value, ref buffer, 0f, 5f, "0.00", "RHAH_Settings_Weight_Tooltip".Translate());
            weightBuffers[id] = buffer;
        }

        IEnumerable<MenuSearchEntry> SearchFrequency()
        {
            yield return Entry("frequency-window", "RHAH_Menu_Frequency_Window");
            yield return Entry("frequency-positive", "RHAH_Menu_Frequency_Positive");
            yield return Entry("frequency-positive-pace", "RHAH_Menu_Frequency_Pace");
            yield return Entry("frequency-positive-curve", "RHAH_Menu_Frequency_Curve");
            yield return Entry("frequency-negative", "RHAH_Menu_Frequency_Negative");
            yield return Entry("frequency-negative-pace", "RHAH_Menu_Frequency_Pace");
            yield return Entry("frequency-negative-curve", "RHAH_Menu_Frequency_Curve");
        }

        void DrawDeveloper(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Developer");
            if (!string.IsNullOrEmpty(selectedPawnLabel))
            {
                Status(list, "RHAH_Menu_Developer_LastPawn", selectedPawnLabel);
            }
        }

        IEnumerable<MenuSearchEntry> SearchDeveloper()
        {
            yield return Entry("developer-last-pawn", "RHAH_Menu_Developer_LastPawn");
        }


        static IEnumerable<MenuSearchEntry> SearchGenes()
        {
            yield return Entry("genes-weights", "RHAH_Menu_Genes");
            yield return Entry("genes-switches", "RHAH_Menu_Genes_Switches");
            yield return Entry("litter-curve", "RHAH_Menu_Genes_LitterCurve");
            yield return Entry("fertile-age", "RHAH_Settings_FertileAge");
            yield return Entry("fertility-percent", "RHAH_Settings_FertilityPercent");
            yield return Entry("gestation-days", "RHAH_Settings_GestationDays");
            yield return Entry("lovin-pregnancy", "RHAH_Settings_LovinPregnancy");
            yield return Entry("maternal-milk", "RHAH_Settings_MaternalMilk");
            yield return Entry("maternal-hunger", "RHAH_Settings_MaternalHunger");
            yield return Entry("maternal-nutrition", "RHAH_Settings_MaternalNutrition");
            yield return Entry("paternal-work", "RHAH_Settings_PaternalWork");
            yield return Entry("paternal-hunger", "RHAH_Settings_PaternalHunger");
            yield return Entry("paternal-learning", "RHAH_Settings_PaternalLearning");
            yield return Entry("paternal-damage", "RHAH_Settings_PaternalDamage");
        }

        IEnumerable<MenuSearchEntry> SearchPawnHistory()
        {
            yield return Entry("young-age", "RHAH_Settings_YoungAge");
            yield return Entry("mother-age", "RHAH_Settings_MotherAge");
            yield return Entry("immobile-babies", "RHAH_Settings_ImmobileBabies");
            yield return Entry("gender-mode", "RHAH_Settings_Gender");
            yield return Entry("apparel-mode", "RHAH_Settings_Apparel");
            yield return Entry("refugee-apparel", "RHAH_Settings_RefugeeApparel");
            yield return Entry("refugee-apparel-sort", "RHAH_Settings_RefugeeApparel_Sort");
            List<ThingDef> apparel = new List<ThingDef>();
            RHAH_ApparelAssigner.AppendCandidates(apparel);
            int apparelMode = RHAH_Mod.Settings == null ? 0 : RHAH_Mod.Settings.apparelListMode;
            for (int i = 0; i < apparel.Count; i++)
            {
                ThingDef def = apparel[i];
                if (def == null || string.IsNullOrEmpty(def.defName))
                {
                    continue;
                }

                string key = RHAH_ReliefFood.GroupKey(apparelMode, def);
                string id = "refugee-apparel-" + def.defName;
                string label = def.LabelCap;
                yield return new MenuSearchEntry(id, () => label, () => def.defName + " " + key, () =>
                {
                    pendingApparelGroup = key;
                    return ApparelTitle(apparelMode, key);
                });
            }
            yield return Entry("vanilla-traits", "RHAH_Settings_VanillaTraits");
            yield return Entry("trait-age", "RHAH_Settings_TraitAge");
            yield return Entry("content-sort", "RHAH_Settings_ContentSort");
            yield return Entry("content-search", "RHAH_Settings_ContentSearch");
            List<string> histories = new List<string>();
            RHAH_Api.CopyHistoryIds(histories);
            for (int i = 0; i < histories.Count; i++)
            {
                string id = histories[i];
                string label = HistoryLabel(id);
                yield return new MenuSearchEntry("history-" + id, () => label, () => id, () =>
                {
                    pendingContentGroup = "history-" + RHAH_ContentSelector.ListKey(RHAH_Mod.Settings == null ? 0 : RHAH_Mod.Settings.contentListMode, id, RHAH_ContentCatalog.FindHistory(id)?.Category ?? RHAH_ContentCategory.None, "RHAH_Menu_Content_Owned".Translate(), true);
                    return label;
                });
            }

            List<string> traits = new List<string>();
            RHAH_Api.CopyTraitIds(traits);
            for (int i = 0; i < traits.Count; i++)
            {
                string id = traits[i];
                string label = TraitLabel(id);
                yield return new MenuSearchEntry("trait-" + id, () => label, () => id, () =>
                {
                    pendingContentGroup = "trait-" + RHAH_ContentSelector.ListKey(RHAH_Mod.Settings == null ? 0 : RHAH_Mod.Settings.contentListMode, id, RHAH_ContentCatalog.FindTrait(id)?.Category ?? RHAH_ContentCategory.None, "RHAH_Menu_Content_Owned".Translate(), true);
                    return label;
                });
            }
        }

        static IEnumerable<MenuSearchEntry> SearchExperimental()
        {
            yield return Entry("optimize-generation", "RHAH_Settings_OptimizeGeneration", "generation optimizer");
        }

        static void Unavailable(Listing_Standard list, string titleKey, string gapKey)
        {
            Section(list, titleKey);
            Empty(list, "RHAH_Menu_Unavailable");
            Note(list, gapKey);
        }
        void DrawNarrativeNumbers(Listing_Standard list, RHAH_Settings settings)
        {
            settings.narrativeProgressKinds = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-progress", "RHAH_Menu_Narrative_Progress".Translate(settings.narrativeProgressKinds), settings.narrativeProgressKinds, 1f, 14f, "RHAH_Settings_NarrativeKinds_Tooltip".Translate());
            settings.narrativeRewardKinds = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-reward", "RHAH_Menu_Narrative_Reward".Translate(settings.narrativeRewardKinds), settings.narrativeRewardKinds, 1f, 14f, "RHAH_Settings_NarrativeKinds_Tooltip".Translate());
            settings.narrativeEnvoyKinds = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-envoy", "RHAH_Menu_Narrative_Envoy".Translate(settings.narrativeEnvoyKinds), settings.narrativeEnvoyKinds, 1f, 14f, "RHAH_Settings_NarrativeKinds_Tooltip".Translate());
            settings.narrativeRelicKinds = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-relic", "RHAH_Menu_Narrative_Relic".Translate(settings.narrativeRelicKinds), settings.narrativeRelicKinds, 1f, 14f, "RHAH_Settings_NarrativeKinds_Tooltip".Translate());
            settings.narrativeTheftKinds = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-theft", "RHAH_Settings_NarrativeTheft".Translate(settings.narrativeTheftKinds), settings.narrativeTheftKinds, 1f, 14f, "RHAH_Settings_NarrativeTheft_Tooltip".Translate());
            settings.narrativeRewardSilver = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-silver", "RHAH_Settings_NarrativeSilver".Translate(settings.narrativeRewardSilver), settings.narrativeRewardSilver, 0f, 10000f, "RHAH_Settings_NarrativeSilver_Tooltip".Translate());
            settings.narrativeTrustBonusPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-bonus", "RHAH_Settings_NarrativeBonus".Translate(settings.narrativeTrustBonusPercent), settings.narrativeTrustBonusPercent, 0f, 100f, "RHAH_Settings_NarrativeBonus_Tooltip".Translate());
            settings.narrativeRescueCost = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-rescue", "RHAH_Settings_NarrativeRescue".Translate(settings.narrativeRescueCost), settings.narrativeRescueCost, 0f, 10000f, "RHAH_Settings_NarrativeRescue_Tooltip".Translate());
            settings.narrativeRescueReward = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-reward-silver", "RHAH_Settings_NarrativeRescueReward".Translate(settings.narrativeRescueReward), settings.narrativeRescueReward, 0f, 20000f, "RHAH_Settings_NarrativeRescueReward_Tooltip".Translate());
            settings.narrativeRelicTake = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-take", "RHAH_Settings_NarrativeTake".Translate(settings.narrativeRelicTake), settings.narrativeRelicTake, 0f, 10000f, "RHAH_Settings_NarrativeTake_Tooltip".Translate());
            settings.narrativeRelicHand = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-hand", "RHAH_Settings_NarrativeHand".Translate(settings.narrativeRelicHand), settings.narrativeRelicHand, 0f, 10000f, "RHAH_Settings_NarrativeHand_Tooltip".Translate());
            settings.narrativeMissingDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-missing", "RHAH_Settings_NarrativeMissing".Translate(settings.narrativeMissingDays), settings.narrativeMissingDays, 0f, 60f, "RHAH_Settings_NarrativeMissing_Tooltip".Translate());
            settings.narrativeObserveDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-observe", "RHAH_Settings_NarrativeObserve".Translate(settings.narrativeObserveDays), settings.narrativeObserveDays, 0f, 120f, "RHAH_Settings_NarrativeObserve_Tooltip".Translate());
            settings.narrativeHoleIgnoreDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-hole", "RHAH_Settings_NarrativeHole".Translate(settings.narrativeHoleIgnoreDays), settings.narrativeHoleIgnoreDays, 0f, 30f, "RHAH_Settings_NarrativeHole_Tooltip".Translate());
            settings.narrativeEnvoyWaitDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-envoy-wait", "RHAH_Settings_NarrativeEnvoyWait".Translate(settings.narrativeEnvoyWaitDays), settings.narrativeEnvoyWaitDays, 0f, 30f, "RHAH_Settings_NarrativeEnvoyWait_Tooltip".Translate());
            settings.narrativeEnvoyCheckDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-envoy-check", "RHAH_Settings_NarrativeEnvoyCheck".Translate(settings.narrativeEnvoyCheckDays), settings.narrativeEnvoyCheckDays, 0f, 30f, "RHAH_Settings_NarrativeEnvoyCheck_Tooltip".Translate());
            settings.narrativeRelicDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-relic-days", "RHAH_Settings_NarrativeRelicDays".Translate(settings.narrativeRelicDays), settings.narrativeRelicDays, 0f, 120f, "RHAH_Settings_NarrativeRelicDays_Tooltip".Translate());
            settings.narrativeRevisitYears = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-years", "RHAH_Settings_NarrativeYears".Translate(settings.narrativeRevisitYears), settings.narrativeRevisitYears, 0f, 20f, "RHAH_Settings_NarrativeYears_Tooltip".Translate());
            settings.narrativeAsideCooldownDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-aside", "RHAH_Settings_NarrativeAside".Translate(settings.narrativeAsideCooldownDays), settings.narrativeAsideCooldownDays, 0f, 30f, "RHAH_Settings_NarrativeAside_Tooltip".Translate());
            settings.narrativeAsideCutoff = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-cutoff", "RHAH_Settings_NarrativeCutoff".Translate(settings.narrativeAsideCutoff), settings.narrativeAsideCutoff, -100f, 0f, "RHAH_Settings_NarrativeCutoff_Tooltip".Translate());
            settings.narrativeAdultYears = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "narrative-adult", "RHAH_Settings_NarrativeAdult".Translate(settings.narrativeAdultYears), settings.narrativeAdultYears, 1f, 80f, "RHAH_Settings_NarrativeAdult_Tooltip".Translate());
            DrawTrustNumbers(list, settings);
        }

        void DrawTrustNumbers(Listing_Standard list, RHAH_Settings settings)
        {
            Section(list, "RHAH_Menu_Narrative_TrustDeltas");
            settings.trustKill = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-kill", "RHAH_Settings_TrustKill".Translate(settings.trustKill), settings.trustKill, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustCaptive = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-captive", "RHAH_Settings_TrustCaptive".Translate(settings.trustCaptive), settings.trustCaptive, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEntrustGood = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-entrust", "RHAH_Settings_TrustEntrust".Translate(settings.trustEntrustGood), settings.trustEntrustGood, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEntrustCaptive = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-entrust-captive", "RHAH_Settings_TrustEntrustCaptive".Translate(settings.trustEntrustCaptive), settings.trustEntrustCaptive, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEntrustStory = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-story", "RHAH_Settings_TrustStory".Translate(settings.trustEntrustStory), settings.trustEntrustStory, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEntrustRegret = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-regret", "RHAH_Settings_TrustRegret".Translate(settings.trustEntrustRegret), settings.trustEntrustRegret, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEntrustBanished = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-banished", "RHAH_Settings_TrustBanished".Translate(settings.trustEntrustBanished), settings.trustEntrustBanished, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustExchange = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-exchange", "RHAH_Settings_TrustExchange".Translate(settings.trustExchange), settings.trustExchange, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustHoleOpen = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-hole-open", "RHAH_Settings_TrustHoleOpen".Translate(settings.trustHoleOpen), settings.trustHoleOpen, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustHoleIgnore = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-hole-ignore", "RHAH_Settings_TrustHoleIgnore".Translate(settings.trustHoleIgnore), settings.trustHoleIgnore, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustHoleBait = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-hole-bait", "RHAH_Settings_TrustHoleBait".Translate(settings.trustHoleBait), settings.trustHoleBait, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustEnvoyFail = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-envoy", "RHAH_Settings_TrustEnvoy".Translate(settings.trustEnvoyFail), settings.trustEnvoyFail, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustRelicFail = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-relic", "RHAH_Settings_TrustRelic".Translate(settings.trustRelicFail), settings.trustRelicFail, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustHold = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-hold", "RHAH_Settings_TrustHold".Translate(settings.trustHold), settings.trustHold, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustDeliver = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-deliver", "RHAH_Settings_TrustDeliver".Translate(settings.trustDeliver), settings.trustDeliver, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustLeave = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-leave", "RHAH_Settings_TrustLeave".Translate(settings.trustLeave), settings.trustLeave, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
            settings.trustExpel = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "trust-expel", "RHAH_Settings_TrustExpel".Translate(settings.trustExpel), settings.trustExpel, -100f, 100f, "RHAH_Settings_TrustRange_Tooltip".Translate());
        }

        void DrawVisitorIntensity(Listing_Standard list, RHAH_Settings settings)
        {
            settings.begFailMood = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "beg-fail-mood", "RHAH_Settings_BegFailMood".Translate(settings.begFailMood), settings.begFailMood, -50f, 50f, "RHAH_Settings_BegFailMood_Tooltip".Translate());
            settings.begSuccessMood = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "beg-success-mood", "RHAH_Settings_BegSuccessMood".Translate(settings.begSuccessMood), settings.begSuccessMood, -50f, 50f, "RHAH_Settings_BegSuccessMood_Tooltip".Translate());
            settings.begSlapMood = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "beg-slap-mood", "RHAH_Settings_BegSlapMood".Translate(settings.begSlapMood), settings.begSlapMood, -50f, 50f, "RHAH_Settings_BegSlapMood_Tooltip".Translate());
            settings.begSlapKnockoutHours = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "beg-knock", "RHAH_Settings_BegKnock".Translate(settings.begSlapKnockoutHours), settings.begSlapKnockoutHours, 0f, 24f, "RHAH_Settings_BegKnock_Tooltip".Translate());
            settings.begBruiseSeverity = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "beg-bruise", "RHAH_Settings_BegBruise".Translate(settings.begBruiseSeverity.ToString("0")), settings.begBruiseSeverity, 0f, 40f, "0", "RHAH_Settings_BegBruise_Tooltip".Translate());
            settings.begBruiseStep = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "beg-bruise-step", "RHAH_Settings_BegBruiseStep".Translate(settings.begBruiseStep.ToString("0")), settings.begBruiseStep, 0f, 40f, "0", "RHAH_Settings_BegBruiseStep_Tooltip".Translate());
            settings.begBruiseMax = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "beg-bruise-max", "RHAH_Settings_BegBruiseMax".Translate(settings.begBruiseMax.ToString("0")), settings.begBruiseMax, settings.begBruiseSeverity, 40f, "0", "RHAH_Settings_BegBruiseMax_Tooltip".Translate());
            settings.barkNutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "gnaw-bark", "RHAH_Settings_GnawBark".Translate(settings.barkNutrition.ToString("0.00")), settings.barkNutrition, 0f, 2f, "0.00", "RHAH_Settings_GnawBark_Tooltip".Translate());
            settings.barkDamage = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "gnaw-bark-damage", "RHAH_Settings_GnawBarkDamage".Translate(settings.barkDamage.ToString("0")), settings.barkDamage, 0f, 50f, "0", "RHAH_Settings_GnawBarkDamage_Tooltip".Translate());
            settings.wallNutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "gnaw-wall", "RHAH_Settings_GnawWall".Translate(settings.wallNutrition.ToString("0.00")), settings.wallNutrition, 0f, 2f, "0.00", "RHAH_Settings_GnawWall_Tooltip".Translate());
            settings.wallDamage = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "gnaw-wall-damage", "RHAH_Settings_GnawWallDamage".Translate(settings.wallDamage.ToString("0")), settings.wallDamage, 0f, 50f, "0", "RHAH_Settings_GnawWallDamage_Tooltip".Translate());
            settings.clayMaxBites = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "clay-bites", "RHAH_Settings_ClayBites".Translate(settings.clayMaxBites), settings.clayMaxBites, 1f, 3f, "RHAH_Settings_ClayBites_Tooltip".Translate());
            settings.clayWindowDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "clay-days", "RHAH_Settings_ClayDays".Translate(settings.clayWindowDays), settings.clayWindowDays, 1f, 60f, "RHAH_Settings_ClayDays_Tooltip".Translate());
            settings.claySeverityPerBite = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "clay-severity", "RHAH_Settings_ClaySeverity".Translate(settings.claySeverityPerBite.ToString("0.00")), settings.claySeverityPerBite, 0f, 1f, "0.00", "RHAH_Settings_ClaySeverity_Tooltip".Translate());
            settings.childHungryPercent = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "child-hungry", "RHAH_Settings_ChildHungry".Translate(settings.childHungryPercent.ToString("0")), settings.childHungryPercent, 0f, 100f, "0", "RHAH_Settings_ChildHungry_Tooltip".Translate());
            settings.prisonerHungryPercent = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "prisoner-hungry", "RHAH_Settings_PrisonerHungry".Translate(settings.prisonerHungryPercent.ToString("0")), settings.prisonerHungryPercent, 0f, 100f, "0", "RHAH_Settings_PrisonerHungry_Tooltip".Translate());
            settings.tailBiteAge = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "tail-age", "RHAH_Settings_TailAge".Translate(settings.tailBiteAge.ToString("0.0")), settings.tailBiteAge, 0f, 18f, "0.0", "RHAH_Settings_TailAge_Tooltip".Translate());
            settings.scavengeNutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "scavenge", "RHAH_Settings_Scavenge".Translate(settings.scavengeNutrition.ToString("0.00")), settings.scavengeNutrition, 0f, 2f, "0.00", "RHAH_Settings_Scavenge_Tooltip".Translate());
            settings.tailNutrition = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "tail-food", "RHAH_Settings_TailFood".Translate(settings.tailNutrition.ToString("0.00")), settings.tailNutrition, 0f, 2f, "0.00", "RHAH_Settings_TailFood_Tooltip".Translate());
            settings.tailFailDamage = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "tail-fail", "RHAH_Settings_TailFail".Translate(settings.tailFailDamage.ToString("0")), settings.tailFailDamage, 0f, 50f, "0", "RHAH_Settings_TailFail_Tooltip".Translate());
            settings.followPredatorPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-predator", "RHAH_Settings_FollowPredator".Translate(settings.followPredatorPercent), settings.followPredatorPercent, 0f, 100f, "RHAH_Settings_FollowPredator_Tooltip".Translate());
            settings.followBirthWatchDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-birth", "RHAH_Settings_FollowBirth".Translate(settings.followBirthWatchDays), settings.followBirthWatchDays, 0f, 60f, "RHAH_Settings_FollowBirth_Tooltip".Translate());
            settings.followPlagueBirthDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-plague-birth", "RHAH_Settings_FollowPlagueBirth".Translate(settings.followPlagueBirthDays), settings.followPlagueBirthDays, 0f, 60f, "RHAH_Settings_FollowPlagueBirth_Tooltip".Translate());
            settings.followMotherReturnDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-mother", "RHAH_Settings_FollowMother".Translate(settings.followMotherReturnDays), settings.followMotherReturnDays, 0f, 120f, "RHAH_Settings_FollowMother_Tooltip".Translate());
            settings.followLongReturnYears = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-years", "RHAH_Settings_FollowYears".Translate(settings.followLongReturnYears), settings.followLongReturnYears, 0f, 20f, "RHAH_Settings_FollowYears_Tooltip".Translate());
            settings.followAdultAge = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "follow-adult", "RHAH_Settings_FollowAdult".Translate(settings.followAdultAge.ToString("0")), settings.followAdultAge, 1f, 80f, "0", "RHAH_Settings_FollowAdult_Tooltip".Translate());
            settings.followMoodScalePercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "follow-mood", "RHAH_Settings_FollowMood".Translate(settings.followMoodScalePercent), settings.followMoodScalePercent, 0f, 300f, "RHAH_Settings_FollowMood_Tooltip".Translate());
            settings.requestMinSimple = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-simple-min", "RHAH_Settings_RequestSimpleMin".Translate(settings.requestMinSimple), settings.requestMinSimple, 0f, 200f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMaxSimple = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-simple-max", "RHAH_Settings_RequestSimpleMax".Translate(settings.requestMaxSimple), settings.requestMaxSimple, settings.requestMinSimple, 200f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMinFine = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-fine-min", "RHAH_Settings_RequestFineMin".Translate(settings.requestMinFine), settings.requestMinFine, 0f, 200f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMaxFine = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-fine-max", "RHAH_Settings_RequestFineMax".Translate(settings.requestMaxFine), settings.requestMaxFine, settings.requestMinFine, 200f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMinMedicine = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-med-min", "RHAH_Settings_RequestMedicineMin".Translate(settings.requestMinMedicine), settings.requestMinMedicine, 0f, 100f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMaxMedicine = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-med-max", "RHAH_Settings_RequestMedicineMax".Translate(settings.requestMaxMedicine), settings.requestMaxMedicine, settings.requestMinMedicine, 100f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMinHerbal = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-herb-min", "RHAH_Settings_RequestHerbalMin".Translate(settings.requestMinHerbal), settings.requestMinHerbal, 0f, 100f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMaxHerbal = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-herb-max", "RHAH_Settings_RequestHerbalMax".Translate(settings.requestMaxHerbal), settings.requestMaxHerbal, settings.requestMinHerbal, 100f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMinSilver = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-silver-min", "RHAH_Settings_RequestSilverMin".Translate(settings.requestMinSilver), settings.requestMinSilver, 0f, 10000f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestMaxSilver = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-silver-max", "RHAH_Settings_RequestSilverMax".Translate(settings.requestMaxSilver), settings.requestMaxSilver, settings.requestMinSilver, 10000f, "RHAH_Settings_RequestRange_Tooltip".Translate());
            settings.requestDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "req-days", "RHAH_Settings_RequestDays".Translate(settings.requestDays), settings.requestDays, 0f, 30f, "RHAH_Settings_RequestDays_Tooltip".Translate());
            settings.requestPointScale = RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, "req-points", "RHAH_Settings_RequestPoints".Translate(settings.requestPointScale.ToString("0")), settings.requestPointScale, 1f, 10000f, "0", "RHAH_Settings_RequestPoints_Tooltip".Translate());
            settings.foodPerChild = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "food-child", "RHAH_Settings_FoodPerChild".Translate(settings.foodPerChild), settings.foodPerChild, 0f, 100f, "RHAH_Settings_FoodPerChild_Tooltip".Translate());
            settings.foodPerVisitor = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "food-visitor", "RHAH_Settings_FoodPerVisitor".Translate(settings.foodPerVisitor), settings.foodPerVisitor, 0f, 20f, "RHAH_Settings_FoodPerVisitor_Tooltip".Translate());
            settings.maxFoodRequest = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "food-cap", "RHAH_Settings_FoodRequestCap".Translate(settings.maxFoodRequest), settings.maxFoodRequest, 0f, 100f, "RHAH_Settings_FoodRequestCap_Tooltip".Translate());
            settings.envoyMealCost = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "envoy-meals", "RHAH_Settings_EnvoyMeals".Translate(settings.envoyMealCost), settings.envoyMealCost, 0f, 100f, "RHAH_Settings_EnvoyMeals_Tooltip".Translate());
            settings.campMinAdults = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-adult-min", "RHAH_Settings_CampAdultMin".Translate(settings.campMinAdults), settings.campMinAdults, 0f, 40f, "RHAH_Settings_CampRange_Tooltip".Translate());
            settings.campMaxAdults = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-adult-max", "RHAH_Settings_CampAdultMax".Translate(settings.campMaxAdults), settings.campMaxAdults, settings.campMinAdults, 40f, "RHAH_Settings_CampRange_Tooltip".Translate());
            settings.campMinChildren = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-child-min", "RHAH_Settings_CampChildMin".Translate(settings.campMinChildren), settings.campMinChildren, 0f, 80f, "RHAH_Settings_CampRange_Tooltip".Translate());
            settings.campMaxChildren = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-child-max", "RHAH_Settings_CampChildMax".Translate(settings.campMaxChildren), settings.campMaxChildren, settings.campMinChildren, 80f, "RHAH_Settings_CampRange_Tooltip".Translate());
            settings.campGoodwill = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-goodwill", "RHAH_Settings_CampGoodwill".Translate(settings.campGoodwill), settings.campGoodwill, -100f, 100f, "RHAH_Settings_CampGoodwill_Tooltip".Translate());
            settings.campDays = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-days", "RHAH_Settings_CampDays".Translate(settings.campDays), settings.campDays, 1f, 120f, "RHAH_Settings_CampDays_Tooltip".Translate());
            settings.campHuts = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "camp-huts", "RHAH_Settings_CampHuts".Translate(settings.campHuts), settings.campHuts, 0f, 12f, "RHAH_Settings_CampHuts_Tooltip".Translate());
            settings.holeWoodCost = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "hole-wood", "RHAH_Settings_HoleWood".Translate(settings.holeWoodCost), settings.holeWoodCost, 0f, 200f, "RHAH_Settings_HoleWood_Tooltip".Translate());
            settings.holeCleanPortions = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "hole-clean", "RHAH_Settings_HoleClean".Translate(settings.holeCleanPortions), settings.holeCleanPortions, 0f, 50f, "RHAH_Settings_HoleClean_Tooltip".Translate());
            settings.holeLossRange = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "hole-range", "RHAH_Settings_HoleRange".Translate(settings.holeLossRange), settings.holeLossRange, 1f, 60f, "RHAH_Settings_HoleRange_Tooltip".Translate());
            settings.holeMaxLosses = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "hole-losses", "RHAH_Settings_HoleLosses".Translate(settings.holeMaxLosses), settings.holeMaxLosses, 0f, 20f, "RHAH_Settings_HoleLosses_Tooltip".Translate());
        }

        void DrawApparelIntensity(Listing_Standard list, RHAH_Settings settings)
        {
            settings.apparelAwfulPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-awful", "RHAH_Settings_ApparelAwful".Translate(settings.apparelAwfulPercent), settings.apparelAwfulPercent, 0f, 100f, "RHAH_Settings_ApparelAwful_Tooltip".Translate());
            settings.apparelPoorPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-poor", "RHAH_Settings_ApparelPoor".Translate(settings.apparelPoorPercent), settings.apparelPoorPercent, 0f, 100f - settings.apparelAwfulPercent, "RHAH_Settings_ApparelPoor_Tooltip".Translate());
            settings.apparelMinDurabilityPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-min", "RHAH_Settings_ApparelMin".Translate(settings.apparelMinDurabilityPercent), settings.apparelMinDurabilityPercent, 1f, 100f, "RHAH_Settings_ApparelDurability_Tooltip".Translate());
            settings.apparelMaxDurabilityPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-max", "RHAH_Settings_ApparelMax".Translate(settings.apparelMaxDurabilityPercent), settings.apparelMaxDurabilityPercent, settings.apparelMinDurabilityPercent, 100f, "RHAH_Settings_ApparelDurability_Tooltip".Translate());
            settings.apparelCorpsePercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-corpse", "RHAH_Settings_ApparelCorpse".Translate(settings.apparelCorpsePercent), settings.apparelCorpsePercent, 0f, 100f, "RHAH_Settings_ApparelCorpse_Tooltip".Translate());
            settings.apparelClothPercent = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-cloth", "RHAH_Settings_ApparelCloth".Translate(settings.apparelClothPercent), settings.apparelClothPercent, 0f, 100f, "RHAH_Settings_ApparelCloth_Tooltip".Translate());
            settings.apparelMaxPieces = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, "apparel-pieces", "RHAH_Settings_ApparelPieces".Translate(settings.apparelMaxPieces), settings.apparelMaxPieces, 1f, 8f, "RHAH_Settings_ApparelPieces_Tooltip".Translate());
        }

        static void DrawNode(Listing_Standard list, NarrativeState state, SuiyinNode node, string label)
        {
            bool enabled = state.SuiyinEnabled(node);
            RHAH_IrisMenusWidgets.Checkbox(list, label, ref enabled);
            state.SetSuiyinEnabled(node, enabled);
        }

        static int ClampKinds(Listing_Standard list, string anchor, string key, int value)
        {
            MenuControls.Anchor(list, anchor, 28f);
            value = (int)list.Slider(value, 1f, 14f);
            RHAH_IrisMenusWidgets.Label(list, "RHAH_Menu_Format_LabelValue".Translate(key.Translate(), value));
            if (value < 1)
            {
                return 1;
            }

            return value > 14 ? 14 : value;
        }


        static void Section(Listing_Standard list, string titleKey)
        {
            RHAH_IrisMenusWidgets.Section(list, titleKey.Translate());
        }

        static void Status(Listing_Standard list, string labelKey, string value)
        {
            string label = labelKey.StartsWith("RHAH_", StringComparison.Ordinal) ? labelKey.Translate() : labelKey;
            RHAH_IrisMenusWidgets.Label(list, "RHAH_Menu_Format_LabelValue".Translate(label, value));
        }

        static void Note(Listing_Standard list, string key)
        {
            RHAH_IrisMenusWidgets.Label(list, key.Translate());
            list.Gap(4f);
        }

        static void ReservedNote(Listing_Standard list, string anchor, string key, bool visible)
        {
            string text = key.Translate();
            float width = Mathf.Max(1f, list.ColumnWidth);
            float height = Text.CalcHeight(text, width);
            MenuControls.Anchor(list, anchor, height + 4f);
            Rect rect = list.GetRect(height);
            if (visible)
            {
                Widgets.Label(rect, text);
            }

            list.Gap(4f);
        }

        static void Empty(Listing_Standard list, string key)
        {
            MenuControls.Anchor(list, "empty-" + key);
            RHAH_IrisMenusWidgets.Label(list, key.Translate());
            list.Gap(4f);
        }

        static string ContentFinderVersion()
        {
            ModMetaData meta = ModLister.GetActiveModWithIdentifier(RHAH_Runtime.PackageId, false);
            return meta == null ? "1.0.0" : VersionOf(meta);
        }

        static string VersionOf(ModMetaData meta)
        {
            return string.IsNullOrEmpty(meta.ModVersion) ? "RHAH_Menu_VersionUnknown".Translate() : meta.ModVersion;
        }

        static MenuSearchEntry Entry(string id, string titleKey, string keywords = null)
        {
            return new MenuSearchEntry(id, () => titleKey.Translate(), keywords == null ? null : () => keywords);
        }

        static string FamilyLabel(RHAH_IncidentFamily family)
        {
            return ("RHAH_Menu_Family_" + family).Translate();
        }

        static string OriginLabel(RHAH_IncidentOrigin origin)
        {
            return ("RHAH_Menu_Origin_" + origin).Translate();
        }

        static string CategoryLabel(RHAH_IncidentCategory category)
        {
            return ("RHAH_Menu_Category_" + category).Translate();
        }

        static string TargetLabel(RHAH_IncidentTarget target)
        {
            return ("RHAH_Menu_Target_" + target).Translate();
        }

        static string RoleLabel(RHAH_PawnRole role)
        {
            return ("RHAH_Role_" + role).Translate();
        }

        static string LifecycleLabel(RHAH_Lifecycle lifecycle)
        {
            return ("RHAH_Menu_Lifecycle_" + lifecycle).Translate();
        }

        static string OutcomeLabel(NarrativeOutcome outcome)
        {
            return ("RHAH_Menu_Outcome_" + outcome).Translate();
        }
    }
}
