using System;
using System.Collections.Generic;
using HarmonyLib;
using IrisMenus.UI;
using IrisUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisUI.Inspector
{
    internal sealed class InspectorSurface
    {
        private static readonly AccessTools.FieldRef<WindowStack, Window> FocusedWindow =
            AccessTools.FieldRefAccess<WindowStack, Window>("focusedWindow");
        private readonly UiContext ui = new UiContext();
        private readonly UiTabs tabs;
        private readonly UiScrollView railScroll;
        private readonly UiButton minimum, normal, fullscreen, close;
        private readonly Action<Rect> drawRail;
        private string[] labels = Array.Empty<string>();
        private UiGlyphKind[] glyphs = Array.Empty<UiGlyphKind>();
        private UiTween[] railHover = Array.Empty<UiTween>();
        private string minimumText, normalText, fullText, closeText, backText, overviewText;
        private object language;
        private UiTween portraitFade = new UiTween(0f), contentFade = new UiTween(0f);
        private Vector2 summaryScroll;
        private Pawn previousPawn;
        private double enteredAt;
        private double floorTime;
        private double sampledAt;
        private bool restart = true;
        private float portraitOpacity;
        private bool railOverflow;
        private float railRowHeight, railGap, railTop;
        private int railSelection = -1;
        private int railFocusPending = -1;
        private InspectorMode previousMode;
        private Type previousTab;
        private int selected, requested;
        private float scale;
        internal static float ContentOpacity { get; private set; } = 1f;

        internal InspectorSurface()
        {
            tabs = new UiTabs(ui, Array.Empty<string>());
            railScroll = new UiScrollView(ui);
            minimum = new UiButton(ui); normal = new UiButton(ui);
            fullscreen = new UiButton(ui); close = new UiButton(ui);
            drawRail = DrawRail;
        }

        internal void RestartFade()
        {
            restart = true;
            portraitFade.Snap(0f);
            contentFade.Snap(0f);
            portraitOpacity = 0f;
            ContentOpacity = 0f;
        }

        internal void ResetSession()
        {
            previousPawn = null;
            previousTab = null;
            summaryScroll = Vector2.zero;
            railScroll.Reset();
            railSelection = railFocusPending = -1;
            floorTime = sampledAt = 0d;
            RestartFade();
        }

        internal void UpdateTabs(IReadOnlyList<InspectTabBase> current)
        {
            RefreshText();
            labels = new string[current.Count + 1];
            glyphs = new UiGlyphKind[labels.Length];
            railHover = new UiTween[labels.Length];
            var keys = new string[labels.Length];
            labels[0] = overviewText; keys[0] = "overview";
            glyphs[0] = UiGlyphKind.Grid;
            for (int i = 0; i < current.Count; i++)
            {
                labels[i + 1] = current[i].labelKey.Translate();
                keys[i + 1] = current[i].GetType().AssemblyQualifiedName;
                glyphs[i + 1] = GlyphFor(current[i].GetType());
            }
            tabs.SetItems(labels, keys);
            railSelection = -1;
            railFocusPending = -1;
        }

        private void RefreshText()
        {
            if (language == LanguageDatabase.activeLanguage) return;
            language = LanguageDatabase.activeLanguage;
            minimumText = "IUI_InspectorMinimum".Translate();
            normalText = "IUI_InspectorNormal".Translate();
            fullText = "IUI_InspectorFullscreen".Translate();
            closeText = "IUI_InspectorClose".Translate();
            backText = "IUI_InspectorReturn".Translate();
            overviewText = "IUI_InspectorOverview".Translate();
        }

        internal void Draw(Rect localRect, MainTabWindow_Inspect pane, Pawn pawn)
        {
            RefreshText();
            UiLayout layout = IrisLayout.Current;
            scale = layout.Scale;
            Rect root = InspectorController.ShellRect(layout);
            bool keyboard = FocusedWindow(Find.WindowStack) == pane && GUIUtility.keyboardControl == 0;
            UiInputEligibility input = IrisLayout.InputFor(pane, root, keyboard);
            ui.BeginFrame(localRect, layout.Fullscreen ? IrisLayout.Theme : UiTheme.Dark,
                IrisLayout.ReducedMotion, input, scale);
            ui.FontSize = 14f * scale;
            if (previousMode != layout.Mode || previousTab != pane.OpenTabType || previousPawn != pawn)
            {
                if (previousPawn != pawn) summaryScroll = Vector2.zero;
                previousPawn = pawn;
                previousMode = layout.Mode; previousTab = pane.OpenTabType;
                RestartFade();
            }
            StepEntrance(layout.Fullscreen);
            try
            {
                Rect paneRect = Local(layout.Inspector, root);
                ui.Fill(paneRect, ui.Palette.Surface);
                ui.Border(paneRect, ui.Palette.Line);
                if (layout.Fullscreen) DrawTechnicalBackground(paneRect, pawn);
                DrawHeader(paneRect, pane, pawn, layout);
                if (layout.Mode != InspectorMode.Minimum)
                {
                    selected = 0;
                    var current = InspectorController.VisibleTabs;
                    for (int i = 0; i < current.Count; i++)
                        if (current[i].GetType() == pane.OpenTabType) { selected = i + 1; break; }
                    requested = selected;
                    Rect navigation = Local(layout.Navigation, root);
                    if (layout.Fullscreen)
                    {
                        railGap = 7f * scale;
                        railRowHeight = 106f * scale;
                        float height = labels.Length * (railRowHeight + railGap) - railGap;
                        railTop = Mathf.Max(0f, (navigation.height - height) * 0.5f);
                        railOverflow = height > navigation.height;
                        if (railSelection != selected)
                        {
                            float top = selected * (railRowHeight + railGap);
                            float position = railScroll.Position.y;
                            if (top < position) position = top;
                            else if (top + railRowHeight > position + navigation.height)
                                position = top + railRowHeight - navigation.height;
                            railScroll.SetPosition(position);
                            railSelection = selected;
                        }
                        railScroll.Draw(navigation, Mathf.Max(navigation.height, height), drawRail);
                    }
                    else
                    {
                        requested = tabs.Draw(navigation, selected, scale: scale);
                        for (int i = 0; i < current.Count; i++)
                            if (current[i].GetType() != pane.OpenTabType && !string.IsNullOrEmpty(current[i].TutorHighlightTagClosed))
                                UIHighlighter.HighlightOpportunity(navigation, current[i].TutorHighlightTagClosed);
                    }
                    if (requested != selected) InspectorController.SelectPage(requested);
                }
                if (layout.Fullscreen)
                {
                    Rect portrait = Local(layout.Portrait, root);
                    if (pane.OpenTabType != null) portrait.height = Mathf.Max(1f, portrait.height - 160f * scale);
                    DrawPortrait(portrait, pawn);
                    Rect overview = pane.OpenTabType == null
                        ? Local(layout.Body, root)
                        : new Rect(Local(layout.Portrait, root).x, paneRect.yMax - 160f * scale,
                            Local(layout.Portrait, root).width, 136f * scale);
                    DrawNativeSummary(overview, pane, pawn, input);
                }
                else
                {
                    float top = paneRect.y + 78f * scale;
                    Rect summary = new Rect(paneRect.x + 12f * scale, top,
                        Mathf.Max(1f, paneRect.width - 24f * scale), Mathf.Max(1f, paneRect.yMax - top - 10f * scale));
                    if (layout.Mode == InspectorMode.Minimum)
                    {
                        if (pane.ShouldShowPaneContents) DrawMinimumCore(summary, pawn);
                    }
                    else DrawNativeSummary(summary, pane, pawn, input);
                }
                tabs.DrawTooltip(localRect, scale);
            }
            finally { ui.EndFrame(); }
        }

        private void StepEntrance(bool fullscreenStage)
        {
            if (sampledAt > 0d && !restart && !ui.ReducedMotion) floorTime += Math.Max(0d, ui.Time - sampledAt);
            sampledAt = ui.Time;
            if (restart)
            {
                enteredAt = ui.Time;
                restart = false;
            }
            portraitOpacity = portraitFade.Step(1f, ui.Time, 0.4f, ui.ReducedMotion, UiEase.Iris);
            double delay = fullscreenStage ? 0.16d : 0d;
            ContentOpacity = ui.ReducedMotion || ui.Time >= enteredAt + delay
                ? contentFade.Step(1f, ui.Time, 0.22f, ui.ReducedMotion, UiEase.Iris)
                : 0f;
        }

        private void DrawHeader(Rect rect, MainTabWindow_Inspect pane, Pawn pawn, UiLayout layout)
        {
            float inset = layout.Fullscreen ? 24f * scale : 12f * scale;
            float y = rect.y + 8f * scale;
            Rect title = new Rect(rect.x + inset, y, Mathf.Max(1f, rect.width - inset * 2f - 40f * scale), 30f * scale);
            ui.FontSize = (layout.Fullscreen ? 28f : 20f) * scale;
            using (ui.Clip(title)) ui.Label(title.AtZero(), pawn.LabelShortCap, ui.Palette.Ink);
            ui.FontSize = 13f * scale;
            Rect closeRect = new Rect(rect.xMax - inset - 32f * scale, y, 32f * scale, 30f * scale);
            if (close.Draw(closeRect, string.Empty, icon: layout.Fullscreen ? UiGlyphKind.ChevronRight : UiGlyphKind.Close,
                iconRotation: layout.Fullscreen ? 180f : 0f, scale: scale))
            {
                if (layout.Fullscreen) InspectorController.ReturnFromFullscreen();
                else InspectorController.Close();
            }
            TooltipHandler.TipRegion(closeRect, layout.Fullscreen ? backText : closeText);
            float modeWidth = Mathf.Min(116f * scale, (rect.width - inset * 2f - 8f * scale) / 3f);
            float controlsY = y + 34f * scale;
            if (layout.Fullscreen)
            {
                modeWidth = Mathf.Min(136f * scale, (rect.width - inset * 2f) / 3f);
                controlsY = y + 40f * scale;
            }
            Rect one = new Rect(rect.x + inset, controlsY, modeWidth, 28f * scale);
            Rect two = new Rect(one.xMax + 4f * scale, one.y, modeWidth, one.height);
            Rect three = new Rect(two.xMax + 4f * scale, one.y, modeWidth, one.height);
            if (DrawModeButton(minimum, one, minimumText, layout.Mode == InspectorMode.Minimum))
                InspectorController.SetMode(InspectorMode.Minimum);
            if (DrawModeButton(normal, two, normalText, layout.Mode == InspectorMode.Normal))
                InspectorController.SetMode(InspectorMode.Normal);
            if (DrawModeButton(fullscreen, three, fullText, layout.Mode == InspectorMode.Fullscreen))
                InspectorController.SetMode(InspectorMode.Fullscreen);
        }

        private bool DrawModeButton(UiButton button, Rect rect, string label, bool active)
        {
            bool clicked = button.Draw(rect, label, variant: UiButtonVariant.Ghost, scale: scale);
            if (active) ui.Fill(new Rect(rect.x, rect.yMax - 2f * scale, rect.width, 2f * scale), ui.Palette.Signal);
            return clicked;
        }

        private void DrawRail(Rect content)
        {
            var current = InspectorController.VisibleTabs;
            if (railFocusPending >= 0)
            {
                ui.Focus(tabs.ItemId(railFocusPending));
                railFocusPending = -1;
            }
            bool navigated = false;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect item = new Rect(content.x + (railOverflow ? 0f : 10f),
                    content.y + railTop + i * (railRowHeight + railGap),
                    railOverflow ? content.width : content.width + 10f, railRowHeight);
                UiId id = tabs.ItemId(i);
                UiInteraction interaction = ui.Interact(id, item);
                bool active = i == selected;
                if (interaction.Clicked) requested = i;
                if (interaction.Focused)
                {
                    int next = i;
                    if (ui.ConsumeKey(KeyCode.UpArrow)) next = (i + labels.Length - 1) % labels.Length;
                    else if (ui.ConsumeKey(KeyCode.DownArrow)) next = (i + 1) % labels.Length;
                    else if (ui.ConsumeKey(KeyCode.Home)) next = 0;
                    else if (ui.ConsumeKey(KeyCode.End)) next = labels.Length - 1;
                    if (next != i && !navigated)
                    {
                        requested = next;
                        railFocusPending = next;
                        navigated = true;
                    }
                }
                float hover = railHover[i].Step(interaction.Hovered || interaction.Pressed ? 1f : 0f,
                    ui.Time, UiMotion.Fast, ui.ReducedMotion);
                if (!ui.IsRectVisible(item)) continue;
                Color fill = active ? UiPalette.Light.Raised : Color.Lerp(UiPalette.Dark.Sunken, UiPalette.Dark.Raised, hover);
                Color ink = active ? UiPalette.Light.Ink : UiPalette.Dark.Ink;
                ui.Fill(item, fill);
                if (active) ui.Fill(new Rect(item.x, item.y, item.width, 2f * scale), ui.Palette.Signal);
                if (interaction.Pressed) ui.Border(item, ui.Palette.Signal, scale);
                if (interaction.Focused && ui.FocusVisible) ui.Border(item.ContractedBy(2f * scale), ui.Palette.Signal, scale);
                ui.FontSize = 11f * scale;
                using (ui.Clip(new Rect(item.x + 3f * scale, item.y + 5f * scale,
                    Mathf.Max(1f, item.width - 6f * scale), 32f * scale)))
                    ui.Label(new Rect(0f, 0f, Mathf.Max(1f, item.width - 6f * scale), 32f * scale),
                        labels[i], ink, TextAnchor.UpperCenter, wrap: true);
                UiGlyph.Draw(ui, new Rect(item.center.x - 10f * scale, item.yMax - 29f * scale,
                    20f * scale, 20f * scale), glyphs[i], ink);
                TooltipHandler.TipRegion(item, labels[i]);
                if (i > 0 && !active && !string.IsNullOrEmpty(current[i - 1].TutorHighlightTagClosed))
                    UIHighlighter.HighlightOpportunity(item, current[i - 1].TutorHighlightTagClosed);
            }
            ui.FontSize = 13f * scale;
        }

        private static UiGlyphKind GlyphFor(Type type)
        {
            if (typeof(ITab_Pawn_Gear).IsAssignableFrom(type)) return UiGlyphKind.Layers;
            if (typeof(ITab_Pawn_Health).IsAssignableFrom(type)) return UiGlyphKind.Plus;
            if (typeof(ITab_Pawn_Needs).IsAssignableFrom(type)) return UiGlyphKind.Sun;
            if (typeof(ITab_Pawn_Social).IsAssignableFrom(type)) return UiGlyphKind.Menu;
            if (typeof(ITab_Pawn_Character).IsAssignableFrom(type)) return UiGlyphKind.Pin;
            if (typeof(ITab_Pawn_Training).IsAssignableFrom(type)) return UiGlyphKind.Check;
            return UiGlyphKind.Grid;
        }

        private void DrawMinimumCore(Rect rect, Pawn pawn)
        {
            if (!ui.IsRepaint) return;
            using (ui.Opacity(ContentOpacity))
            using (ui.Clip(rect))
            {
                ui.FontSize = 13f * scale;
                ui.Label(new Rect(0f, 0f, rect.width, 22f * scale),
                    HealthUtility.GetGeneralConditionLabel(pawn, shortVersion: true), ui.Palette.Ink);
                string report = pawn.GetJobReport();
                if (!string.IsNullOrEmpty(report))
                    ui.Label(new Rect(0f, 23f * scale, rect.width, 20f * scale), report, ui.Palette.Muted);
                float y = Mathf.Max(0f, rect.height - 5f * scale);
                ui.Fill(new Rect(0f, y, rect.width, 3f * scale), ui.Palette.StrongLine);
                ui.Fill(new Rect(0f, y, rect.width * Mathf.Clamp01(pawn.health.summaryHealth.SummaryHealthPercent),
                    3f * scale), ui.Palette.Ink);
            }
        }

        private void DrawNativeSummary(Rect rect, MainTabWindow_Inspect pane, Pawn pawn, UiInputEligibility input)
        {
            if (rect.width <= 0f || rect.height <= 0f || !pane.ShouldShowPaneContents) return;
            if (ContentOpacity <= 0f && ui.IsRepaint) return;
            using (ui.Opacity(ContentOpacity)) ui.Fill(rect, UiPalette.Dark.Surface);
            Event evt = Event.current;
            EventType previous = evt.type;
            bool keyboard = previous == EventType.KeyDown || previous == EventType.KeyUp;
            bool blocked = ContentOpacity <= 0f || (keyboard ? (input & UiInputEligibility.Keyboard) == 0 :
                previous != EventType.Layout && previous != EventType.Repaint && (input & UiInputEligibility.Pointer) == 0);
            bool enabled = GUI.enabled;
            Color color = GUI.color;
            GUI.enabled = enabled && !blocked;
            if (blocked && previous != EventType.Used && previous != EventType.Layout && previous != EventType.Repaint)
                evt.type = EventType.Ignore;
            GUI.color = new Color(color.r, color.g, color.b, color.a * ContentOpacity);
            using var text = new TextBlock(GameFont.Small);
            Widgets.BeginGroup(rect);
            try
            {
                // Preserve the vanilla status-row widths and filler height; its inspect string
                // already supplies vertical scrolling, while this outer viewport protects controls.
                int bars = 1;
                if (pawn.IsGhoul && pawn.needs?.food != null || !pawn.IsGhoul && pawn.needs?.mood != null) bars++;
                if (pawn.timetable != null && !pawn.IsPrisonerOfColony) bars++;
                if (pawn.playerSettings != null && pawn.playerSettings.SupportsAllowedAreas && pawn.Faction == Faction.OfPlayer &&
                    pawn.HostFaction == null && (!pawn.IsMutant || pawn.mutant.Def.respectsAllowedArea)) bars++;
                if (pawn.needs?.energy != null) bars++;
                float width = Mathf.Max(rect.width, Mathf.Max(pane.RequestedTabSize.x - 24f, bars * 99f - 6f));
                float height = Mathf.Max(131f, rect.height);
                bool horizontal = width > rect.width;
                bool vertical = height > rect.height - (horizontal ? 16f : 0f);
                horizontal |= width > rect.width - (vertical ? 16f : 0f);
                height = Mathf.Max(131f, rect.height - (horizontal ? 16f : 0f));
                vertical = height > rect.height - (horizontal ? 16f : 0f);
                bool scrolling = horizontal || vertical;
                if (scrolling)
                {
                    float viewWidth = Mathf.Max(1f, rect.width - (vertical ? 16f : 0f));
                    float viewHeight = Mathf.Max(1f, rect.height - (horizontal ? 16f : 0f));
                    summaryScroll.x = Mathf.Clamp(summaryScroll.x, 0f, Mathf.Max(0f, width - viewWidth));
                    summaryScroll.y = Mathf.Clamp(summaryScroll.y, 0f, Mathf.Max(0f, height - viewHeight));
                    Widgets.BeginScrollView(rect.AtZero(), ref summaryScroll, new Rect(0f, 0f, width, height));
                }
                else summaryScroll = Vector2.zero;
                try
                {
                    float lineEnd = 24f;
                    pane.DoInspectPaneButtons(new Rect(0f, 0f, width, 26f), ref lineEnd);
                    if (pane.ShouldShowSelectNextInCellButton)
                    {
                        Rect next = new Rect(Mathf.Max(0f, width - 24f), 0f, 24f, 24f);
                        if (Widgets.ButtonImage(next, TexButton.SelectOverlappingNext)) pane.SelectNextInCell();
                        TooltipHandler.TipRegionByKey(next, "SelectNextInSquareTip", KeyBindingDefOf.SelectNextInCell.MainKeyLabel);
                    }
                    pane.DoPaneContents(new Rect(0f, 28f, width, Mathf.Max(1f, height - 28f)));
                }
                finally { if (scrolling) Widgets.EndScrollView(); }
            }
            finally
            {
                Widgets.EndGroup(); GUI.enabled = enabled; GUI.color = color;
                if (blocked && previous != EventType.Used && previous != EventType.Layout && previous != EventType.Repaint)
                    evt.type = previous;
            }
        }

        private void DrawPortrait(Rect rect, Pawn pawn)
        {
            if (!ui.IsRepaint || rect.width <= 0f || rect.height <= 0f || portraitOpacity <= 0f) return;
            float side = Mathf.Min(rect.width, rect.height * 0.76f);
            float offset = ui.ReducedMotion ? 0f : (1f - portraitOpacity) * 24f * scale;
            Rect image = new Rect(rect.center.x - side * 0.5f + offset, rect.y, side, Mathf.Min(rect.height, side * 1.32f));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, portraitOpacity);
            try { GUI.DrawTexture(image, PortraitTexture(pawn), ScaleMode.ScaleToFit); }
            finally { GUI.color = old; }
        }

        private static Texture PortraitTexture(Pawn pawn) => PortraitsCache.Get(pawn, new Vector2(256f, 338f), Rot4.South,
            new Vector3(0f, 0f, 0.12f), 1.1f);

        private void DrawTechnicalBackground(Rect rect, Pawn pawn)
        {
            if (!ui.IsRepaint) return;
            using (ui.Clip(rect))
            {
                Rect stage = rect.AtZero();
                for (int i = 0; i < 24; i++)
                {
                    float progress = (i + 0.5f) / 24f;
                    Color shade = Color.Lerp(ui.Palette.Raised, ui.Palette.Preview, progress * 0.65f);
                    ui.Fill(new Rect(0f, stage.height * i / 24f, stage.width, stage.height / 24f + 1f), shade);
                }
                DrawPortraitEcho(stage, pawn);
                Color wedge = ui.Palette.Signal; wedge.a = 0.7f;
                ui.Triangle(new Vector2(stage.width * 0.86f, 0f), new Vector2(stage.width, 0f),
                    new Vector2(stage.width, stage.height), wedge);
                ui.Triangle(new Vector2(stage.width * 0.86f, 0f), new Vector2(stage.width, stage.height),
                    new Vector2(stage.width * 0.68f, stage.height), wedge);
                float horizon = stage.height * 0.55f;
                float depth = stage.height - horizon;
                float center = stage.width * 0.58f;
                float phase = (float)(floorTime % 8d) / 8f;
                for (int row = 0; row < 12; row++)
                {
                    float t = (row + 1f - phase) / 12f;
                    float y = horizon + depth * t * t;
                    Color ink = ui.Palette.Ink;
                    ink.a = 0.15f * Mathf.Sin(t * Mathf.PI);
                    ui.Line(new Vector2(0f, y), new Vector2(stage.width, y), ink, scale);
                }
                for (int column = -8; column <= 8; column++)
                {
                    float farX = center + column * 7f * scale;
                    float nearX = center + column * stage.width * 0.16f;
                    for (int segment = 0; segment < 8; segment++)
                    {
                        float a = segment / 8f, b = (segment + 1f) / 8f;
                        Color ink = ui.Palette.Ink;
                        ink.a = 0.09f * Mathf.Sin((a + b) * 0.5f * Mathf.PI);
                        ui.Line(new Vector2(Mathf.Lerp(farX, nearX, a), horizon + depth * a),
                            new Vector2(Mathf.Lerp(farX, nearX, b), horizon + depth * b), ink, scale);
                    }
                }
            }
        }

        private void DrawPortraitEcho(Rect stage, Pawn pawn)
        {
            if (portraitOpacity <= 0f) return;
            float height = stage.height * 1.3f;
            float width = height * 256f / 338f;
            float left = -width * 0.24f;
            Rect image = new Rect(left, -stage.height * 0.17f, width, height);
            Texture texture = PortraitTexture(pawn);
            Color old = GUI.color;
            try
            {
                for (int band = 0; band < 16; band++)
                {
                    float x = left + width * band / 16f;
                    float alpha = 0.11f * portraitOpacity * (1f - band / 15f);
                    using (ui.Clip(new Rect(x, 0f, width / 16f + 0.5f, stage.height)))
                    {
                        GUI.color = new Color(0.5f, 0.53f, 0.5f, alpha);
                        GUI.DrawTexture(new Rect(image.x - x, image.y, image.width, image.height), texture, ScaleMode.ScaleToFit);
                    }
                }
            }
            finally { GUI.color = old; }
        }

        private static Rect Local(Rect rect, Rect root) => new Rect(rect.x - root.x, rect.y - root.y, rect.width, rect.height);
    }
}
