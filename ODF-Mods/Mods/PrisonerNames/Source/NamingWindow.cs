using System;
using System.Text;
using Oxygendsl.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class NamingWindow : Window
    {
        const float RowHeight = 32f;
        const float Gap = 8f;
        const float ButtonHeight = 32f;

        readonly GameComponent_PrisonerNames runtime;
        Vector2 listScroll;
        Vector2 helpScroll;
        string feedback = string.Empty;
        string manualDraft = string.Empty;
        int manualNumber = -1;
        string scoreDraft = string.Empty;
        string gradeDraft = string.Empty;
        bool helpOpen;
        string preview = string.Empty;
        string previewError = string.Empty;
        int previewRevision = -1;
        int previewNumber = -1;
        string previewSource;

        public NamingWindow(GameComponent_PrisonerNames runtime)
        {
            this.runtime = runtime;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            resizeable = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
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

        public override void DoWindowContents(Rect inRect)
        {
            if (!ReferenceEquals(GameComponent_PrisonerNames.Current, runtime))
            {
                Widgets.Label(inRect, T("OPN_GameChanged"));
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 132f, 32f), T("OPN_Title"));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.width - 124f, 0f, 124f, ButtonHeight), T(helpOpen ? "OPN_HideHelp" : "OPN_ShowHelp")))
                helpOpen = !helpOpen;

            bool dirty = !string.Equals(runtime.DraftSource ?? string.Empty, runtime.ActiveSource ?? string.Empty, StringComparison.Ordinal);
            string state = runtime.Enabled ? T("OPN_Enabled") : T("OPN_Disabled");
            string status = state + "  ·  " + T("OPN_Revision") + " " + runtime.Revision;
            if (dirty) status += "  ·  " + T("OPN_Unapplied");
            Widgets.Label(new Rect(0f, 36f, inRect.width, 22f), status);

            float top = 62f;
            DrawActions(inRect, ref top);
            float bodyTop = top + 4f;
            float bodyHeight = inRect.height - bodyTop;
            if (helpOpen) bodyHeight = Mathf.Max(180f, bodyHeight - 118f);

            float leftWidth = Mathf.Clamp(inRect.width * 0.42f, 280f, inRect.width - 360f);
            DrawRoster(new Rect(0f, bodyTop, leftWidth, bodyHeight));
            DrawPrisoners(new Rect(leftWidth + 12f, bodyTop, inRect.width - leftWidth - 12f, bodyHeight));
            if (helpOpen) DrawHelp(new Rect(0f, inRect.height - 110f, inRect.width, 110f));
        }

        void DrawActions(Rect inRect, ref float top)
        {
            float width = (inRect.width - Gap * 2f) / 3f;
            if (Widgets.ButtonText(new Rect(0f, top, width, ButtonHeight), T(runtime.Enabled ? "OPN_Disable" : "OPN_Enable")))
            {
                runtime.SetEnabled(!runtime.Enabled);
                feedback = runtime.Enabled ? T("OPN_Enabled") : T("OPN_Disabled");
            }
            if (Widgets.ButtonText(new Rect(width + Gap, top, width, ButtonHeight), T("OPN_Refresh")))
            {
                runtime.RefreshNow();
                feedback = T("OPN_Refreshed");
            }
            if (Widgets.ButtonText(new Rect((width + Gap) * 2f, top, width, ButtonHeight), T("OPN_OpenEditor")))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("OPN_OpenEditorConfirm".Translate(), () => Oxygendsl.Mod.OxygenEditorHost.Open(PrisonerNamesHost.PackageId)));
            top += ButtonHeight + 6f;


            string error = FormatError(runtime.LastError);
            string message = !string.IsNullOrEmpty(error) ? T("OPN_Error") + ": " + error
                : !string.IsNullOrEmpty(previewError) ? T("OPN_PreviewFailed") + ": " + previewError
                : feedback;
            Color previous = GUI.color;
            if (!string.IsNullOrEmpty(error) || !string.IsNullOrEmpty(previewError)) GUI.color = new Color(1f, 0.55f, 0.45f);
            Widgets.Label(new Rect(0f, top, inRect.width, 36f), message);
            GUI.color = previous;
            top += 40f;
        }

        void DrawRoster(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 22f), T("OPN_Roster"));
            Widgets.Label(new Rect(rect.x, rect.y + 28f, rect.width, 66f), T("OPN_BrowserEditing"));
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 100f, rect.width, 32f), T("OPN_OpenEditor")))
                Oxygendsl.Mod.OxygenEditorHost.Open(PrisonerNamesHost.PackageId);
        }

        void DrawPrisoners(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 22f), T("OPN_Prisoners"));
            Rect view = new Rect(rect.x, rect.y + 24f, rect.width, rect.height - 24f);
            var prisoners = runtime.Prisoners;
            PrisonerEntry selected = FindSelected(prisoners);
            SyncManual(selected);
            float content = 4f + (selected == null ? 48f : 150f + (!string.IsNullOrEmpty(previewError) ? 40f : !string.IsNullOrEmpty(preview) ? 26f : 0f));
            if (prisoners.Count == 0) content += 48f;
            else content += prisoners.Count * (RowHeight + 4f);
            Widgets.BeginScrollView(view, ref listScroll, new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height, content)));
            float y = 4f;
            float inner = view.width - 22f;
            if (selected == null)
            {
                Widgets.Label(new Rect(4f, y, inner, 40f), T("OPN_NoSelection"));
                y += 44f;
            }
            else
            {
                y = DrawSelected(selected, y, inner);
                if (!string.IsNullOrEmpty(previewError))
                {
                    Widgets.Label(new Rect(4f, y, inner, 36f), T("OPN_PreviewFailed") + ": " + previewError);
                    y += 40f;
                }
                else if (!string.IsNullOrEmpty(preview))
                {
                    Widgets.Label(new Rect(4f, y, inner, 22f), T("OPN_Preview") + ": " + preview);
                    y += 26f;
                }
            }
            if (prisoners.Count == 0)
            {
                Widgets.Label(new Rect(4f, y, inner, 40f), T("OPN_NoPrisoners"));
            }
            else
            {
                for (int i = 0; i < prisoners.Count; i++)
                    y = DrawRow(prisoners[i], y, inner, selected != null && ReferenceEquals(prisoners[i].Pawn, selected.Pawn));
            }
            Widgets.EndScrollView();
        }

        float DrawSelected(PrisonerEntry entry, float y, float width)
        {
            Pawn pawn = entry.Pawn;
            string shown = DisplayAlias(entry);
            Widgets.Label(new Rect(4f, y, width, 22f), T("OPN_Shown") + ": " + shown);
            y += 22f;
            Widgets.Label(new Rect(4f, y, width, 22f), T("OPN_Original") + ": " + OriginalName(pawn) + "  ·  #" + entry.Number.ToString("D3"));
            y += 22f;
            string next = entry.NextThreshold.HasValue ? NamePolicy.FormatNumber(entry.NextThreshold.Value - entry.Score) : T("OPN_NoNextGrade");
            Widgets.Label(new Rect(4f, y, width, 22f), T("OPN_Score") + " " + NamePolicy.FormatNumber(entry.Score) + "  ·  " + T("OPN_Grade") + " " + entry.Grade + "  ·  " + T("OPN_NextGrade") + " " + next);
            y += 22f;
            string mode = string.IsNullOrEmpty(entry.Alias) ? T("OPN_UsingOriginal") : (string.IsNullOrEmpty(entry.ManualAlias) ? T("OPN_UsingRule") : T("OPN_UsingManual"));
            Widgets.Label(new Rect(4f, y, width - 96f, 22f), mode);
            if (Widgets.ButtonText(new Rect(width - 88f, y - 2f, 88f, 26f), T("OPN_Jump"))) Jump(pawn);
            y += 26f;

            Widgets.Label(new Rect(4f, y + 3f, 72f, 24f), T("OPN_Manual"));
            string edited = Widgets.TextField(new Rect(78f, y, width - 170f, 28f), manualDraft ?? string.Empty);
            if (!string.Equals(edited, manualDraft ?? string.Empty, StringComparison.Ordinal)) manualDraft = edited;
            if (Widgets.ButtonText(new Rect(width - 84f, y, 84f, 28f), T("OPN_Clear")))
            {
                if (runtime.SetManual(pawn, string.Empty))
                {
                    manualDraft = string.Empty;
                    feedback = T("OPN_ManualCleared");
                }
                else feedback = T("OPN_ManualFailed");
            }
            y += 32f;
            if (Widgets.ButtonText(new Rect(4f, y, width, 28f), T("OPN_SetManual")))
            {
                if (runtime.SetManual(pawn, manualDraft ?? string.Empty)) feedback = T("OPN_ManualSet");
                else feedback = T("OPN_ManualFailed");
            }
            y += 32f;
            Widgets.Label(new Rect(4f, y + 3f, 72f, 24f), T("OPN_Score"));
            scoreDraft = Widgets.TextField(new Rect(78f, y, width - 170f, 28f), scoreDraft ?? string.Empty);
            if (Widgets.ButtonText(new Rect(width - 84f, y, 84f, 28f), T("OPN_SetScore")))
            {
                double score;
                if (string.IsNullOrWhiteSpace(scoreDraft))
                    feedback = runtime.SetScore(pawn, null) ? T("OPN_ScoreAutomatic") : T("OPN_ManualFailed");
                else if (double.TryParse(scoreDraft, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out score)
                    && runtime.SetScore(pawn, score)) { feedback = T("OPN_ScoreSet"); scoreDraft = NamePolicy.FormatNumber(score); }
                else feedback = T("OPN_ManualFailed");
            }
            y += 32f;
            Widgets.Label(new Rect(4f, y + 3f, 72f, 24f), T("OPN_Grade"));
            gradeDraft = Widgets.TextField(new Rect(78f, y, width - 170f, 28f), gradeDraft ?? string.Empty);
            if (Widgets.ButtonText(new Rect(width - 84f, y, 84f, 28f), T("OPN_SetGrade")))
                feedback = runtime.SetGrade(pawn, gradeDraft) ? T("OPN_GradeSet") : T("OPN_ManualFailed");
            return y + 36f;
        }

        float DrawRow(PrisonerEntry entry, float y, float width, bool selected)
        {
            Rect row = new Rect(0f, y, width, RowHeight);
            if (selected) Widgets.DrawHighlightSelected(row);
            else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            if (Widgets.ButtonInvisible(row))
            {
                Select(entry.Pawn);
                manualDraft = entry.ManualAlias ?? string.Empty;
                manualNumber = entry.Number;
            }
            string shown = DisplayAlias(entry);
            Widgets.Label(new Rect(6f, y + 6f, width * 0.42f - 10f, 22f), shown.Truncate(width * 0.42f - 10f));
            Widgets.Label(new Rect(width * 0.42f, y + 6f, width * 0.34f - 8f, 22f), OriginalName(entry.Pawn).Truncate(width * 0.34f - 8f));
            TooltipHandler.TipRegion(row, shown + "\n" + OriginalName(entry.Pawn));
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(width * 0.76f, y + 6f, width * 0.22f, 22f), "#" + entry.Number.ToString("D3"));
            Text.Anchor = TextAnchor.UpperLeft;
            return y + RowHeight + 4f;
        }

        void DrawHelp(Rect rect)
        {
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            string help = T("OPN_Help");
            float height = Mathf.Max(rect.height - 8f, Text.CalcHeight(help, rect.width - 24f) + 8f);
            Widgets.BeginScrollView(new Rect(rect.x, rect.y + 6f, rect.width, rect.height - 6f), ref helpScroll, new Rect(0f, 0f, rect.width - 18f, height));
            Widgets.Label(new Rect(4f, 0f, rect.width - 26f, height), help);
            Widgets.EndScrollView();
        }

        void SyncManual(PrisonerEntry selected)
        {
            if (selected == null)
            {
                manualNumber = -1;
                return;
            }
            if (manualNumber != selected.Number)
            {
                manualDraft = selected.ManualAlias ?? string.Empty;
                scoreDraft = NamePolicy.FormatNumber(selected.Score);
                gradeDraft = selected.ManualGrade ?? string.Empty;
                manualNumber = selected.Number;
            }
            string source = runtime.DraftSource ?? string.Empty;
            if (previewNumber != selected.Number || previewRevision != runtime.Revision || !string.Equals(previewSource, source, StringComparison.Ordinal))
                RefreshPreview(selected, source);
        }

        static PrisonerEntry FindSelected(System.Collections.Generic.IReadOnlyList<PrisonerEntry> prisoners)
        {
            Pawn selected = Find.Selector == null || Find.Selector.SingleSelectedThing == null ? null : Find.Selector.SingleSelectedThing as Pawn;
            if (selected == null || prisoners == null) return null;
            for (int i = 0; i < prisoners.Count; i++)
                if (ReferenceEquals(prisoners[i].Pawn, selected)) return prisoners[i];
            return null;
        }

        static string DisplayAlias(PrisonerEntry entry)
        {
            return string.IsNullOrEmpty(entry.Alias) ? OriginalName(entry.Pawn) : entry.Alias;
        }

        static string OriginalName(Pawn pawn)
        {
            if (pawn == null || pawn.Name == null) return T("OPN_Unnamed");
            string name = pawn.Name.ToStringShort;
            return string.IsNullOrEmpty(name) ? T("OPN_Unnamed") : name;
        }

        void Select(Pawn pawn)
        {
            if (pawn == null || Find.Selector == null) return;
            if (pawn.MapHeld != null && pawn.MapHeld != Find.CurrentMap) CameraJumper.TryJump(pawn);
            Find.Selector.ClearSelection();
            Find.Selector.Select(pawn, false, true);
        }

        void Jump(Pawn pawn)
        {
            if (pawn == null) return;
            CameraJumper.TryJumpAndSelect(pawn);
            manualDraft = string.Empty;
            manualNumber = -1;
        }

        void RefreshPreview(PrisonerEntry entry, string source)
        {
            previewNumber = entry.Number;
            previewRevision = runtime.Revision;
            previewSource = source;
            preview = string.Empty;
            previewError = string.Empty;
            try
            {
                string result = runtime.Preview(source, entry.Pawn);
                preview = string.IsNullOrEmpty(result) ? OriginalName(entry.Pawn) : result;
            }
            catch (Exception exception)
            {
                previewError = FormatError(exception);
            }
        }

        static string FormatError(object error)
        {
            ScriptEvaluationException script = error as ScriptEvaluationException;
            if (script == null)
            {
                Exception exception = error as Exception;
                if (exception != null) script = exception as ScriptEvaluationException;
            }
            if (script == null) return error == null ? string.Empty : error.ToString();
            StringBuilder text = new StringBuilder(script.Message);
            if (script.Line > 0) text.Append(" (").Append(script.Line).Append(':').Append(Math.Max(1, script.Column)).Append(')');
            return text.ToString();
        }

        static bool ChineseActive
        {
            get
            {
                return NamingLanguage.Chinese;
            }
        }

        static string T(string key)
        {
            return key.Translate().ToString();
        }
    }
}
