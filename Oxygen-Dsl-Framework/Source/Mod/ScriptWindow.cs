using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class ScriptWindow : Window
    {
        private readonly GameComponent_Oxygendsl runtime;
        private Vector2 sourceScroll;
        private Vector2 inspectionScroll;
        private string path = string.Empty;
        private string intervalText;
        private string feedback = string.Empty;
        private string inspection = string.Empty;
        private int inspectedRevision = -1;
        private int inspectedEvaluation = -1;
        private int inspectedEffects = -1;
        private string inspectedError;
        private bool inspectedEnabled;
        private int inspectedContentRevision = -1;
        private string providerId = string.Empty;
        private string nativePreview = string.Empty;
        private string inspectedEventChains = string.Empty;
        private string eventChainInspection = string.Empty;
        private float nextEventChainInspection;

        public ScriptWindow(GameComponent_Oxygendsl runtime)
        {
            this.runtime = runtime;
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            intervalText = runtime.EvaluationInterval.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(runtime.DraftSource) && string.IsNullOrEmpty(runtime.ActiveSource))
                runtime.DraftSource = ExampleSource;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(Mathf.Min(1080f, UI.screenWidth - 24f), Mathf.Min(670f, UI.screenHeight - 24f)); }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            windowRect.width = Mathf.Min(windowRect.width, UI.screenWidth - 24f);
            windowRect.height = Mathf.Min(windowRect.height, UI.screenHeight - 24f);
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, UI.screenWidth - windowRect.width);
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, UI.screenHeight - windowRect.height);
        }

        public const string ExampleSource = "// 条件成立时保持一个效果；修改后点击应用\nfn comfortable(pawn) {\n    return pawn.food != null && pawn.food > 0.25\n}\n\nmood(\"fed_comfort\",\n    label: \"吃饱了\",\n    description: \"我吃饱了。现在感觉不错。\",\n    offset: 5,\n    target: \"colonists\",\n    when: comfortable)\n\n// 限时记忆按冷却刷新，不因反复应用重复添加\nmemory(\"small_break\",\n    label: \"稍作休息\",\n    description: \"我歇了一会儿。现在感觉好多了。\",\n    offset: 3,\n    target: \"selected\",\n    duration: 2500,\n    cooldown: 2500,\n    mode: \"refresh\")\n";

        public override void DoWindowContents(Rect inRect)
        {
            if (!ReferenceEquals(GameComponent_Oxygendsl.Current, runtime))
            {
                Widgets.Label(inRect, T("Oxygendsl_GameChanged"));
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Oxygendsl");
            Text.Font = GameFont.Small;
            bool dirty = !string.Equals(runtime.DraftSource ?? string.Empty, runtime.ActiveSource ?? string.Empty, StringComparison.Ordinal);
            string state = runtime.Enabled ? T("Oxygendsl_Enabled") : T("Oxygendsl_Disabled");
            Widgets.Label(new Rect(0f, 34f, inRect.width, 25f), state + "  ·  " + T("Oxygendsl_Revision") + " " + runtime.Revision + (dirty ? "  ·  " + T("Oxygendsl_Unapplied") : string.Empty));

            float top = 66f;
            float buttonWidth = (inRect.width - 24f) / 4f;
            if (Widgets.ButtonText(new Rect(0f, top, buttonWidth, 32f), T("Oxygendsl_Apply")))
                feedback = runtime.Apply(runtime.DraftSource ?? string.Empty) ? T("Oxygendsl_Applied") : T("Oxygendsl_KeepActive");
            if (Widgets.ButtonText(new Rect(buttonWidth + 8f, top, buttonWidth, 32f), T("Oxygendsl_Evaluate")))
                feedback = runtime.EvaluateNow() ? T("Oxygendsl_Evaluated") : T("Oxygendsl_EvaluationFailed");
            if (Widgets.ButtonText(new Rect((buttonWidth + 8f) * 2f, top, buttonWidth, 32f), T(runtime.Enabled ? "Oxygendsl_Disable" : "Oxygendsl_Enable")))
                feedback = runtime.SetEnabled(!runtime.Enabled) ? T(runtime.Enabled ? "Oxygendsl_Enabled" : "Oxygendsl_Cleaned") : T("Oxygendsl_EvaluationFailed");
            if (Widgets.ButtonText(new Rect((buttonWidth + 8f) * 3f, top, buttonWidth, 32f), T("Oxygendsl_CaptureSelection")))
            {
                runtime.CaptureSelection();
                feedback = T("Oxygendsl_SelectionCaptured");
            }
            top += 40f;
            Widgets.Label(new Rect(0f, top + 4f, 90f, 24f), T("Oxygendsl_Interval"));
            intervalText = Widgets.TextField(new Rect(94f, top, 70f, 30f), intervalText);
            if (Widgets.ButtonText(new Rect(172f, top, 100f, 30f), T("Oxygendsl_SetInterval")))
            {
                int interval;
                if (int.TryParse(intervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out interval) && interval > 0)
                {
                    runtime.EvaluationInterval = interval;
                    feedback = T("Oxygendsl_IntervalSet");
                }
                else feedback = T("Oxygendsl_PositiveInterval");
            }
            if (Widgets.ButtonText(new Rect(280f, top, 120f, 30f), T("Oxygendsl_RestoreActive")))
            {
                runtime.DraftSource = runtime.ActiveSource ?? string.Empty;
                feedback = T("Oxygendsl_DraftRestored");
            }
            Widgets.Label(new Rect(410f, top + 4f, inRect.width - 410f, 24f), T("Oxygendsl_TimingHint"));
            top += 40f;
            float third = (inRect.width - 16f) / 3f;
            if (Widgets.ButtonText(new Rect(0f, top, third, 32f), T("Oxygendsl_ReloadMods")))
                feedback = runtime.ReloadModRules() ? T("Oxygendsl_ModRulesReloaded") : T("Oxygendsl_ModRulesFailed");
            if (Widgets.ButtonText(new Rect(third + 8f, top, third, 32f), T("Oxygendsl_PreviewPawn")))
            {
                Pawn pawn = Find.Selector == null || Find.Selector.SelectedPawns.Count == 0 ? null : Find.Selector.SelectedPawns[0];
                nativePreview = OxygenContentHost.Preview(pawn);
                inspectedContentRevision = -1;
            }
            if (Widgets.ButtonText(new Rect((third + 8f) * 2f, top, third, 32f), T("Oxygendsl_ChooseProvider")))
            {
                var choices = new System.Collections.Generic.List<FloatMenuOption>();
                foreach (OxygenContentEntry entry in OxygenContentHost.Entries)
                {
                    string id = entry.PackageId;
                    choices.Add(new FloatMenuOption(id, () => { providerId = id; }));
                }
                if (choices.Count != 0) Find.WindowStack.Add(new FloatMenu(choices));
            }
            top += 40f;
            if (!string.IsNullOrEmpty(providerId))
            {
                OxygenContentEntry provider = null;
                foreach (OxygenContentEntry entry in OxygenContentHost.Entries) if (entry.PackageId == providerId) provider = entry;
                if (provider != null)
                {
                    Widgets.Label(new Rect(0f, top + 4f, inRect.width - 210f, 24f), provider.PackageId);
                    if (Widgets.ButtonText(new Rect(inRect.width - 200f, top, 200f, 32f), T(provider.Enabled ? "Oxygendsl_DisableProvider" : "Oxygendsl_EnableProvider")))
                        feedback = runtime.SetModRulesEnabled(providerId, !provider.Enabled) ? T("Oxygendsl_ProviderUpdated") : T("Oxygendsl_ModRulesFailed");
                    top += 40f;
                }
            }

            float leftWidth = Mathf.Max(300f, inRect.width * 0.61f);
            float rightX = leftWidth + 12f;
            float contentHeight = Mathf.Max(48f, inRect.height - top - 170f);
            Widgets.Label(new Rect(0f, top, leftWidth, 24f), T("Oxygendsl_Source"));
            Widgets.Label(new Rect(rightX, top, inRect.width - rightX, 24f), T("Oxygendsl_Inspection"));
            top += 25f;
            DrawSource(new Rect(0f, top, leftWidth, contentHeight));
            RefreshInspection();
            Rect inspectionRect = new Rect(rightX, top, inRect.width - rightX, contentHeight);
            float inspectionHeight = Mathf.Max(contentHeight, Text.CalcHeight(inspection, inspectionRect.width - 24f) + 12f);
            Widgets.BeginScrollView(inspectionRect, ref inspectionScroll, new Rect(0f, 0f, inspectionRect.width - 18f, inspectionHeight));
            Widgets.Label(new Rect(4f, 4f, inspectionRect.width - 24f, inspectionHeight), inspection);
            Widgets.EndScrollView();
            top += contentHeight + 8f;

            string error = runtime.LastError;
            string message = !string.IsNullOrEmpty(error) ? T("Oxygendsl_Error") + ": " + error : feedback;
            Widgets.Label(new Rect(0f, top, inRect.width, 43f), message);
            top += 44f;
            float readWidth = 130f;
            Widgets.Label(new Rect(0f, top + 4f, 78f, 24f), T("Oxygendsl_FilePath"));
            path = Widgets.TextField(new Rect(82f, top, inRect.width - readWidth - 90f, 30f), path);
            TooltipHandler.TipRegion(new Rect(82f, top, inRect.width - readWidth - 90f, 30f), T("Oxygendsl_FileHint"));
            if (Widgets.ButtonText(new Rect(inRect.width - readWidth, top, readWidth, 30f), T("Oxygendsl_ReadFile"))) ReadFile();
        }

        private void DrawSource(Rect rect)
        {
            string source = runtime.DraftSource ?? string.Empty;
            float height = Mathf.Max(rect.height, Text.CalcHeight(source + "\n", rect.width - 26f) + 42f);
            Widgets.BeginScrollView(rect, ref sourceScroll, new Rect(0f, 0f, rect.width - 18f, height));
            GUI.SetNextControlName("Oxygendsl.ScriptSource");
            string edited = Widgets.TextArea(new Rect(0f, 0f, rect.width - 22f, height), source);
            if (!string.Equals(source, edited, StringComparison.Ordinal)) runtime.DraftSource = edited;
            Widgets.EndScrollView();
        }

        private void ReadFile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) { feedback = T("Oxygendsl_FileRequired"); return; }
                string source = File.ReadAllText(path);
                runtime.DraftSource = source;
                feedback = runtime.Apply(source) ? T("Oxygendsl_FileApplied") + ": " + path : T("Oxygendsl_KeepActive");
            }
            catch (Exception exception) { feedback = T("Oxygendsl_FileFailed") + ": " + exception.Message; }
        }

        private void RefreshInspection()
        {
            if (Time.realtimeSinceStartup >= nextEventChainInspection)
            {
                eventChainInspection = runtime.DescribeEventChains();
                nextEventChainInspection = Time.realtimeSinceStartup + 0.5f;
            }
            if (inspectedRevision == runtime.Revision && inspectedEvaluation == runtime.EvaluationCount && inspectedEffects == runtime.ActiveEffectCount && inspectedError == runtime.LastError && inspectedEnabled == runtime.Enabled && inspectedContentRevision == OxygenContentHost.Revision && inspectedEventChains == eventChainInspection) return;
            inspectedRevision = runtime.Revision;
            inspectedEvaluation = runtime.EvaluationCount;
            inspectedEffects = runtime.ActiveEffectCount;
            inspectedError = runtime.LastError;
            inspectedEnabled = runtime.Enabled;
            inspectedContentRevision = OxygenContentHost.Revision;
            inspectedEventChains = eventChainInspection;
            StringBuilder text = new StringBuilder();
            text.Append(T("Oxygendsl_ActiveEffects")).Append(": ").Append(runtime.ActiveEffectCount).AppendLine();
            text.Append(T("Oxygendsl_LastTick")).Append(": ").Append(runtime.LastEvaluationTick).AppendLine();
            text.Append(T("Oxygendsl_EvaluationMs")).Append(": ").Append(runtime.LastEvaluationMs.ToString("F3", CultureInfo.InvariantCulture)).AppendLine(" ms");
            text.Append(T("Oxygendsl_Allocated")).Append(": ").Append(runtime.LastAllocatedBytes < 0 ? T("Oxygendsl_Unavailable") : runtime.LastAllocatedBytes.ToString(CultureInfo.InvariantCulture) + " B").AppendLine();
            text.Append(T("Oxygendsl_EvaluationCount")).Append(": ").Append(runtime.EvaluationCount).AppendLine();
            for (int i = 0; i < runtime.Statuses.Count; i++)
            {
                RuleStatus rule = runtime.Statuses[i];
                text.AppendLine().Append(rule.Id).Append("  ·  ").Append(rule.Label).AppendLine();
                text.Append(T("Oxygendsl_Matched")).Append(' ').Append(rule.Matched).Append(" / ").Append(T("Oxygendsl_Skipped")).Append(' ').Append(rule.Skipped).Append(" / ").Append(T("Oxygendsl_Active")).Append(' ').Append(rule.Active).AppendLine();
                if (!string.IsNullOrEmpty(rule.LastError)) text.Append(rule.LastError).AppendLine();
                if (rule.Details != null) for (int j = 0; j < rule.Details.Count; j++) text.Append("  ").Append(rule.Details[j]).AppendLine();
            }
            text.AppendLine().AppendLine(T("Oxygendsl_ModContent")).AppendLine(OxygenContentHost.Inspect());
            text.AppendLine().AppendLine(T("Oxygendsl_EventChains")).AppendLine(eventChainInspection);
            if (!string.IsNullOrEmpty(nativePreview)) text.AppendLine(T("Oxygendsl_PreviewPawn")).AppendLine(nativePreview);
            inspection = text.ToString();
        }

        private static string T(string key) { return key.Translate().ToString(); }
    }
}
