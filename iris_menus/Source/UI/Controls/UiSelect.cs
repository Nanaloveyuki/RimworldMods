using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSelect
    {
        private readonly UiContext context;
        private readonly UiId overlayId;
        private readonly UiId scrollbarId;
        private readonly float rowHeight;
        private float DrawnRowHeight => rowHeight * Mathf.Max(0.01f, drawingScale);
        private readonly int maxVisibleItems;
        private string[] options;
        private UiId[] optionIds;
        private UiControlVisual[] optionVisuals;
        private UiTween[] selection;
        private UiTween[] highlights;
        private UiControlVisual visual;
        private UiControlLine line;
        private UiControlVisual scrollbarVisual;
        private UiTween visibility;
        private UiTween entrance;
        private float drawingScale = 1f;
        private UiTween arrow;
        private UiTween scrollMotion;
        private Rect anchorScreen;
        private bool hasAnchor;
        private bool open;
        private bool focusPending;
        private bool restoreFocus;
        private int highlighted;
        private float scroll;
        private bool scrollDragging;
        private float scrollDragTravel;
        private float scrollDragRange;
        private float scrollDragMouse;
        private float scrollDragValue;
        private double lastTypeTime;
        private string search = string.Empty;

        public UiSelect(UiContext context, string[] options, int maxVisibleItems = 8, float rowHeight = 36f)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (maxVisibleItems < 1) throw new ArgumentOutOfRangeException(nameof(maxVisibleItems));
            if (float.IsNaN(rowHeight) || float.IsInfinity(rowHeight) || rowHeight < 20f) throw new ArgumentOutOfRangeException(nameof(rowHeight));
            this.maxVisibleItems = maxVisibleItems;
            this.rowHeight = rowHeight;
            Id = context.CreateId();
            overlayId = context.CreateId();
            scrollbarId = context.CreateId();
            SetOptions(options);
        }

        public UiId Id { get; }
        public bool IsOpen => open;
        public bool IsVisible => open || visibility.Value > 0f;
        public UiId GetOptionId(int index) => optionIds[index];

        public void SetOptions(string[] labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] == null) throw new ArgumentException("Option labels cannot be null.", nameof(labels));
            if (optionIds == null || optionIds.Length != labels.Length)
            {
                if (optionIds != null)
                    for (int i = 0; i < optionIds.Length; i++) context.Release(optionIds[i]);
                optionIds = new UiId[labels.Length];
                optionVisuals = new UiControlVisual[labels.Length];
                selection = new UiTween[labels.Length];
                highlights = new UiTween[labels.Length];
                for (int i = 0; i < labels.Length; i++) optionIds[i] = context.CreateId();
            }
            options = labels;
            highlighted = labels.Length == 0 ? -1 : Mathf.Clamp(highlighted, 0, labels.Length - 1);
            scroll = 0f;
            scrollMotion.Snap(0f);
            search = string.Empty;
            if (labels.Length == 0) Close();
        }

        public void Open(int selected)
        {
            if (options.Length == 0) return;
            highlighted = Mathf.Clamp(selected, 0, options.Length - 1);
            if (!IsVisible) entrance.Snap(0f);
            open = true;
            focusPending = true;
            restoreFocus = true;
            search = string.Empty;
            context.SetModal(overlayId);
        }

        public void Close()
        {
            open = false;
            entrance.Snap(entrance.Value);
            scrollDragging = false;
            context.Release(scrollbarId);
        }

        // Call after BeginFrame and BEFORE drawing background controls, on every IMGUI pass.
        public void Prepare()
        {
            StepVisibility();
            if (IsVisible) context.SetModal(overlayId);
            // Focus restoration belongs to Draw, in the trigger's original parent layer.
        }

        public int Draw(Rect rect, int selected, bool enabled = true, string placeholder = "", float scale = 1f)
        {
            drawingScale = Mathf.Max(0f, scale);
            Vector2 screen = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            anchorScreen = new Rect(screen.x, screen.y, rect.width, rect.height);
            hasAnchor = true;
            bool usable = enabled && options.Length > 0;
            if (!IsVisible && restoreFocus && context.IsInputAllowed)
            {
                if (usable) context.Focus(Id);
                restoreFocus = false;
            }
            UiInteraction interaction;
            if (IsVisible)
            {
                using (context.Layer(overlayId)) interaction = context.Interact(Id, rect, usable, !open);
            }
            else interaction = context.Interact(Id, rect, usable, true);
            if (interaction.Clicked)
            {
                if (open) Close();
                else Open(selected);
            }
            if (usable && interaction.Focused)
            {
                // Popup arrows navigate options; the trigger only opens a closed popup.
                if (IsVisible)
                {
                    using (context.Layer(overlayId))
                        if (!open && (context.ConsumeKey(KeyCode.DownArrow) || context.ConsumeKey(KeyCode.UpArrow))) Open(selected);
                }
                else if (context.ConsumeKey(KeyCode.DownArrow) || context.ConsumeKey(KeyCode.UpArrow)) Open(selected);
            }
            if (!usable && open) Close();
            visual.Step(context, interaction, usable);
            float turn = arrow.Step(open ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            using (context.Opacity(visual.Opacity))
            {
                context.Fill(rect, palette.Input);
                line.Draw(context, rect, open || usable && interaction.Focused, visual.Hover, scale: drawingScale);
                string label = selected >= 0 && selected < options.Length ? options[selected] : placeholder ?? string.Empty;
                context.Label(new Rect(rect.x + 12f * drawingScale, rect.y, Mathf.Max(0f, rect.width - 40f * drawingScale), rect.height), label, selected >= 0 && selected < options.Length ? palette.Ink : palette.Muted);
                float angle = UiMotion.Lerp(90f, -90f, turn) * Mathf.Deg2Rad;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                Vector2 center = new Vector2(rect.xMax - 16f * drawingScale, rect.center.y);
                Vector2 a = new Vector2(-2.5f, -4f) * drawingScale;
                Vector2 b = new Vector2(3.5f, 0f) * drawingScale;
                Vector2 d = new Vector2(-2.5f, 4f) * drawingScale;
                context.Triangle(center + new Vector2(a.x * c - a.y * s, a.x * s + a.y * c),
                    center + new Vector2(b.x * c - b.y * s, b.x * s + b.y * c),
                    center + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c), palette.Muted);
            }
            return selected;
        }

        // Draw AFTER content, outside its scroll/clip groups. Return value is the committed index.
        public int DrawPopup(Rect bounds, int selected, bool enabled = true)
        {
            if (!enabled) Close();
            float amount = StepVisibility();
            if (!IsVisible || !hasAnchor || options.Length == 0 || bounds.width <= 0f || bounds.height <= 0f) return selected;
            using (context.Layer(overlayId))
            {
                Vector2 local = GUIUtility.ScreenToGUIPoint(anchorScreen.position);
                Rect anchor = new Rect(local.x, local.y, anchorScreen.width, anchorScreen.height);
                Rect popup = PlacePopup(anchor, bounds);
                // Translate the rect itself, so outside clicks and row hits match the panel.
                float offset = (1f - entrance.Value) * 4f * drawingScale * (popup.y >= anchor.yMax ? -1f : 1f);
                popup.y = Mathf.Clamp(popup.y + offset, bounds.y, bounds.yMax - popup.height);
                float contentHeight = Mathf.Max(0f, popup.height - 8f * drawingScale);
                float maxScroll = Mathf.Max(0f, options.Length * DrawnRowHeight - contentHeight);
                Event current = context.Event;
                bool input = context.IsInputAllowed;
                if (open && enabled && input && current != null)
                {
                    if (context.ConsumeKey(KeyCode.Escape)) Close();
                    else if (current.type == EventType.MouseDown && !popup.Contains(current.mousePosition) && !anchor.Contains(current.mousePosition))
                    {
                        Close();
                        context.Consume();
                    }
                    else if (context.ConsumeKey(KeyCode.Tab)) Close();
                    else
                    {
                        int next = highlighted;
                        if (context.ConsumeKey(KeyCode.UpArrow)) next--;
                        else if (context.ConsumeKey(KeyCode.DownArrow)) next++;
                        else if (context.ConsumeKey(KeyCode.Home)) next = 0;
                        else if (context.ConsumeKey(KeyCode.End)) next = options.Length - 1;
                        else if (context.ConsumeKey(KeyCode.PageUp)) next -= Mathf.Max(1, Mathf.FloorToInt(contentHeight / DrawnRowHeight));
                        else if (context.ConsumeKey(KeyCode.PageDown)) next += Mathf.Max(1, Mathf.FloorToInt(contentHeight / DrawnRowHeight));
                        else if (context.ConsumeKey(KeyCode.Return) || context.ConsumeKey(KeyCode.KeypadEnter) || context.ConsumeKey(KeyCode.Space))
                        {
                            selected = highlighted;
                            Close();
                        }
                        else if (current.type == EventType.KeyDown && current.character != 0 && !char.IsControl(current.character) && !current.control && !current.alt)
                        {
                            if (context.Time - lastTypeTime > 0.75) search = string.Empty;
                            search += current.character;
                            lastTypeTime = context.Time;
                            int match = FindPrefix(search, highlighted + 1);
                            if (match < 0 && search.Length > 1)
                            {
                                search = current.character.ToString();
                                match = FindPrefix(search, highlighted + 1);
                            }
                            if (match >= 0) next = match;
                            context.Consume();
                        }
                        next = Mathf.Clamp(next, 0, options.Length - 1);
                        if (next != highlighted) { highlighted = next; focusPending = true; }
                    }
                    if (open && current.type == EventType.ScrollWheel && context.Hit(popup))
                    {
                        scroll = Mathf.Clamp(scroll + current.delta.y * DrawnRowHeight, 0f, maxScroll);
                        context.Consume();
                    }
                }
                if (focusPending && open && input && amount > 0f)
                {
                    EnsureHighlightedVisible(contentHeight, maxScroll);
                    scrollMotion.Snap(scroll);
                    context.Focus(optionIds[highlighted]);
                    focusPending = false;
                }
                scroll = Mathf.Clamp(scroll, 0f, maxScroll);
                float drawnScroll = scrollMotion.Step(scroll, context.Time, UiMotion.Fast, context.ReducedMotion);
                // Retain modal ownership at zero-alpha entry without letting invisible rows capture.
                if (amount <= 0f) return selected;
                UiPalette palette = context.Palette;
                using (context.Opacity(amount))
                {
                    context.Fill(popup, palette.Overlay);
                    context.Border(popup, palette.Line, drawingScale);
                    float scrollbarWidth = maxScroll > 0f ? 12f * drawingScale : 0f;
                    Rect viewport = new Rect(popup.x, popup.y + 4f * drawingScale, Mathf.Max(0f, popup.width - scrollbarWidth), contentHeight);
                    using (context.Clip(viewport))
                    {
                        int first = Mathf.Clamp(Mathf.FloorToInt(drawnScroll / DrawnRowHeight), 0, options.Length - 1);
                        int last = Mathf.Min(options.Length - 1, Mathf.CeilToInt((drawnScroll + contentHeight) / DrawnRowHeight));
                        for (int i = first; i <= last; i++)
                        {
                            Rect row = new Rect(0f, i * DrawnRowHeight - drawnScroll, viewport.width, DrawnRowHeight);
                            UiInteraction interaction = context.Interact(optionIds[i], row, open && enabled, false);
                            if (interaction.Hovered && current != null && (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)) highlighted = i;
                            if (interaction.Clicked) { selected = i; highlighted = i; Close(); }
                            optionVisuals[i].Step(context, interaction, enabled && open);
                            float chosen = selection[i].Step(selected == i ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
                            float keyboardHover = highlights[i].Step(highlighted == i && open ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                            float hover = Mathf.Max(optionVisuals[i].Hover, keyboardHover);
                            Color hoverInk = palette.Ink;
                            hoverInk.a = 0.05f * hover * (1f - chosen);
                            Color selectedBackground = Color.Lerp(new Color(230f / 255f, 230f / 255f, 230f / 255f), new Color(40f / 255f, 40f / 255f, 40f / 255f), context.ThemeAmount);
                            selectedBackground.a *= chosen;
                            using (context.Opacity(optionVisuals[i].Opacity))
                            {
                                context.Fill(row, hoverInk);
                                context.Fill(row, selectedBackground);
                                Color markerInk = palette.Ink;
                                markerInk.a *= chosen;
                                context.Fill(new Rect(row.x, row.y, 3f * drawingScale, row.height), markerInk);
                                context.Label(new Rect(row.x + 12f * drawingScale, row.y, Mathf.Max(0f, row.width - 24f * drawingScale), row.height), options[i], palette.Ink);
                                optionVisuals[i].DrawFocus(context, row);
                            }
                        }
                    }
                    if (maxScroll > 0f) DrawScrollbar(new Rect(popup.xMax - 12f * drawingScale, popup.y + 4f * drawingScale, 8f * drawingScale, contentHeight), maxScroll, contentHeight, enabled && open);
                    float accentWidth = popup.width * 0.5f * entrance.Value;
                    context.Fill(new Rect(popup.x, popup.y, accentWidth, 3f * drawingScale), palette.Signal);
                    float triangleX = popup.x + 32f * drawingScale * entrance.Value;
                    context.Triangle(new Vector2(triangleX - 4f * drawingScale * entrance.Value, popup.y + 3f * drawingScale),
                        new Vector2(triangleX + 4f * drawingScale * entrance.Value, popup.y + 3f * drawingScale),
                        new Vector2(triangleX, popup.y + 7f * drawingScale), palette.Signal);
                }
                if (!open && input && current != null && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel || current.type == EventType.KeyDown || current.type == EventType.KeyUp)) context.Consume();
            }
            return selected;
        }

        private float StepVisibility()
        {
            if (open) entrance.Step(1f, context.Time, UiMotion.Enter, context.ReducedMotion, UiEase.Exit);
            return visibility.Step(open ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
        }

        private Rect PlacePopup(Rect anchor, Rect bounds)
        {
            float width = Mathf.Max(0f, Mathf.Min(Mathf.Max(anchor.width, 160f * drawingScale), bounds.width));
            float desired = Mathf.Min(options.Length, maxVisibleItems) * DrawnRowHeight + 8f * drawingScale;
            float below = Mathf.Max(0f, bounds.yMax - anchor.yMax - 4f * drawingScale);
            float above = Mathf.Max(0f, anchor.y - bounds.y - 4f * drawingScale);
            bool down = below >= desired || below >= above;
            float height = Mathf.Min(desired, down ? below : above);
            if (height < Mathf.Min(DrawnRowHeight + 8f * drawingScale, bounds.height))
            {
                height = Mathf.Min(desired, bounds.height);
                return new Rect(Mathf.Clamp(anchor.x, bounds.x, bounds.xMax - width), Mathf.Clamp(anchor.yMax + 4f * drawingScale, bounds.y, bounds.yMax - height), width, height);
            }
            float y = down ? anchor.yMax + 4f * drawingScale : anchor.y - 4f * drawingScale - height;
            return new Rect(Mathf.Clamp(anchor.x, bounds.x, bounds.xMax - width), Mathf.Clamp(y, bounds.y, bounds.yMax - height), width, height);
        }

        private void EnsureHighlightedVisible(float height, float maximum)
        {
            float top = highlighted * DrawnRowHeight;
            if (top < scroll) scroll = top;
            else if (top + DrawnRowHeight > scroll + height) scroll = top + DrawnRowHeight - height;
            scroll = Mathf.Clamp(scroll, 0f, maximum);
        }

        private int FindPrefix(string prefix, int start)
        {
            for (int offset = 0; offset < options.Length; offset++)
            {
                int index = (start + offset) % options.Length;
                if (options[index].StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)) return index;
            }
            return -1;
        }

        private void DrawScrollbar(Rect track, float maximum, float viewportHeight, bool enabled)
        {
            Event current = context.Event;
            EventType type = current != null ? current.type : EventType.Ignore;
            float mouse = current != null ? GUIUtility.GUIToScreenPoint(current.mousePosition).y : 0f;
            bool captured = context.IsCaptured(scrollbarId);
            UiInteraction interaction = context.Interact(scrollbarId, track, enabled, false);
            float height = Mathf.Min(track.height, Mathf.Max(18f, track.height * viewportHeight / (viewportHeight + maximum)));
            float travel = Mathf.Max(1f, track.height - height);
            float thumbY = track.y + scroll / maximum * travel;
            if (!captured && context.IsCaptured(scrollbarId) && type == EventType.MouseDown)
            {
                scrollDragging = true;
                scrollDragMouse = mouse;
                scrollDragTravel = travel;
                scrollDragRange = maximum;
                if (current.mousePosition.y < thumbY || current.mousePosition.y > thumbY + height)
                    scroll = Mathf.Clamp01((current.mousePosition.y - track.y - height * 0.5f) / travel) * maximum;
                scrollDragValue = scroll;
                scrollMotion.Snap(scroll);
            }
            if (scrollDragging && enabled && captured && context.IsInputAllowed && (type == EventType.MouseDrag || type == EventType.MouseUp))
            {
                scroll = Mathf.Clamp(scrollDragValue + (mouse - scrollDragMouse) / scrollDragTravel * scrollDragRange, 0f, maximum);
                scrollMotion.Snap(scroll);
                if (type == EventType.MouseDrag) context.Consume();
            }
            if (!context.IsCaptured(scrollbarId) || !enabled) scrollDragging = false;
            scrollbarVisual.Step(context, interaction, enabled);
            UiPalette palette = context.Palette;
            using (context.Opacity(scrollbarVisual.Opacity))
            {
                context.Fill(track, palette.Sunken);
                Color ink = Color.Lerp(palette.StrongLine, palette.Muted, scrollbarVisual.Hover);
                ink = Color.Lerp(ink, palette.Signal, Mathf.Max(scrollbarVisual.Press, scrollbarVisual.Focus));
                context.Fill(new Rect(track.x, track.y + scrollMotion.Value / maximum * travel, track.width, height), ink);
                scrollbarVisual.DrawFocus(context, track);
            }
        }
    }
}
