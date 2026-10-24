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
        private readonly int maxVisibleItems;
        private string[] options;
        private UiId[] optionIds;
        private UiControlVisual[] optionVisuals;
        private UiTween[] selection;
        private UiTween[] highlights;
        private UiControlVisual visual;
        private UiControlVisual scrollbarVisual;
        private UiTween visibility;
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

        public UiSelect(UiContext context, string[] options, int maxVisibleItems = 8, float rowHeight = 32f)
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
            open = true;
            focusPending = true;
            restoreFocus = true;
            search = string.Empty;
            context.SetModal(overlayId);
        }

        public void Close()
        {
            open = false;
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

        public int Draw(Rect rect, int selected, bool enabled = true, string placeholder = "")
        {
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
                context.Fill(rect, visual.Background(palette, palette.Raised));
                context.Border(rect, Color.Lerp(palette.Line, palette.StrongLine, visual.Hover));
                string label = selected >= 0 && selected < options.Length ? options[selected] : placeholder ?? string.Empty;
                context.Label(new Rect(rect.x + 10f, rect.y, Mathf.Max(0f, rect.width - 38f), rect.height), label, selected >= 0 && selected < options.Length ? palette.Ink : palette.Muted);
                float centerX = rect.xMax - 16f;
                float centerY = rect.center.y;
                float direction = UiMotion.Lerp(1f, -1f, turn);
                context.Line(new Vector2(centerX - 4f, centerY - 2f * direction), new Vector2(centerX, centerY + 2f * direction), palette.Muted, 1.5f);
                context.Line(new Vector2(centerX, centerY + 2f * direction), new Vector2(centerX + 4f, centerY - 2f * direction), palette.Muted, 1.5f);
                visual.DrawFocus(context, rect);
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
                float offset = (1f - amount) * 6f * (popup.y >= anchor.yMax ? -1f : 1f);
                popup.y = Mathf.Clamp(popup.y + offset, bounds.y, bounds.yMax - popup.height);
                float contentHeight = Mathf.Max(0f, popup.height - 8f);
                float maxScroll = Mathf.Max(0f, options.Length * rowHeight - contentHeight);
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
                        else if (context.ConsumeKey(KeyCode.PageUp)) next -= Mathf.Max(1, Mathf.FloorToInt(contentHeight / rowHeight));
                        else if (context.ConsumeKey(KeyCode.PageDown)) next += Mathf.Max(1, Mathf.FloorToInt(contentHeight / rowHeight));
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
                        scroll = Mathf.Clamp(scroll + current.delta.y * rowHeight, 0f, maxScroll);
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
                    context.Fill(popup, palette.Raised);
                    context.Border(popup, palette.StrongLine);
                    float scrollbarWidth = maxScroll > 0f ? 12f : 0f;
                    Rect viewport = new Rect(popup.x + 4f, popup.y + 4f, Mathf.Max(0f, popup.width - 8f - scrollbarWidth), contentHeight);
                    using (context.Clip(viewport))
                    {
                        int first = Mathf.Clamp(Mathf.FloorToInt(drawnScroll / rowHeight), 0, options.Length - 1);
                        int last = Mathf.Min(options.Length - 1, Mathf.CeilToInt((drawnScroll + contentHeight) / rowHeight));
                        for (int i = first; i <= last; i++)
                        {
                            Rect row = new Rect(0f, i * rowHeight - drawnScroll, viewport.width, rowHeight);
                            UiInteraction interaction = context.Interact(optionIds[i], row, open && enabled, false);
                            if (interaction.Hovered && current != null && (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)) highlighted = i;
                            if (interaction.Clicked) { selected = i; highlighted = i; Close(); }
                            optionVisuals[i].Step(context, interaction, enabled && open);
                            float chosen = selection[i].Step(selected == i ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
                            float keyboardHover = highlights[i].Step(highlighted == i && open ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                            float hover = Mathf.Max(optionVisuals[i].Hover, keyboardHover);
                            Color background = Color.Lerp(palette.Raised, palette.Hover, hover);
                            background = Color.Lerp(background, palette.Selected, optionVisuals[i].Press * 0.6f);
                            background = Color.Lerp(background, palette.Selected, chosen);
                            using (context.Opacity(optionVisuals[i].Opacity))
                            {
                                context.Fill(row, background);
                                context.Label(new Rect(row.x + 8f, row.y, Mathf.Max(0f, row.width - 28f), row.height), options[i], Color.Lerp(palette.Ink, palette.OnSelected, chosen));
                                if (chosen > 0f)
                                {
                                    Color checkInk = palette.OnSelected;
                                    checkInk.a *= chosen;
                                    Vector2 start = new Vector2(row.xMax - 19f, row.center.y);
                                    Vector2 elbow = new Vector2(row.xMax - 15f, row.center.y + 4f);
                                    Vector2 end = new Vector2(row.xMax - 8f, row.center.y - 4f);
                                    context.Line(start, elbow, checkInk, 1.5f);
                                    context.Line(elbow, end, checkInk, 1.5f);
                                }
                                optionVisuals[i].DrawFocus(context, row);
                            }
                        }
                    }
                    if (maxScroll > 0f) DrawScrollbar(new Rect(popup.xMax - 12f, popup.y + 4f, 8f, contentHeight), maxScroll, contentHeight, enabled && open);
                }
                if (!open && input && current != null && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel || current.type == EventType.KeyDown || current.type == EventType.KeyUp)) context.Consume();
            }
            return selected;
        }

        private float StepVisibility()
        {
            return visibility.Step(open ? 1f : 0f, context.Time, open ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
        }

        private Rect PlacePopup(Rect anchor, Rect bounds)
        {
            float width = Mathf.Max(0f, Mathf.Min(Mathf.Max(anchor.width, 120f), bounds.width));
            float desired = Mathf.Min(options.Length, maxVisibleItems) * rowHeight + 8f;
            float below = Mathf.Max(0f, bounds.yMax - anchor.yMax - 4f);
            float above = Mathf.Max(0f, anchor.y - bounds.y - 4f);
            bool down = below >= desired || below >= above;
            float height = Mathf.Min(desired, down ? below : above);
            if (height < Mathf.Min(rowHeight + 8f, bounds.height))
            {
                height = Mathf.Min(desired, bounds.height);
                return new Rect(Mathf.Clamp(anchor.x, bounds.x, bounds.xMax - width), Mathf.Clamp(anchor.yMax + 4f, bounds.y, bounds.yMax - height), width, height);
            }
            float y = down ? anchor.yMax + 4f : anchor.y - 4f - height;
            return new Rect(Mathf.Clamp(anchor.x, bounds.x, bounds.xMax - width), Mathf.Clamp(y, bounds.y, bounds.yMax - height), width, height);
        }

        private void EnsureHighlightedVisible(float height, float maximum)
        {
            float top = highlighted * rowHeight;
            if (top < scroll) scroll = top;
            else if (top + rowHeight > scroll + height) scroll = top + rowHeight - height;
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
