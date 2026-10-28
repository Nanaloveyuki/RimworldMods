using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiScrollView : IDisposable
    {
        private readonly UiContext context;
        private readonly UiId[] anchorIds;
        private readonly UiId scrollbarId;
        private readonly Rect[] anchorRects;
        private readonly int[] anchorPasses;
        private Vector2 position;
        private Vector2 contentScreenOrigin;
        private Rect viewport;
        private float contentHeight;
        private UiTween scroll;
        private UiTween highlight;
        private float scrollTarget;
        private float focusMargin = 12f;
        private int pass;
        private int pendingAnchor = -1;
        private int highlightedAnchor = -1;
        private int focusAfterScroll = -1;
        private double highlightUntil;
        private bool drawing;
        private bool animating;
        private bool scrollbarDragging;
        private float dragMouse, dragPosition, dragTravel, dragRange;

        public UiScrollView(UiContext context, int anchorCount = 0)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (anchorCount < 0) throw new ArgumentOutOfRangeException(nameof(anchorCount));
            Id = context.CreateId();
            scrollbarId = context.CreateId();
            anchorIds = new UiId[anchorCount];
            anchorRects = new Rect[anchorCount];
            anchorPasses = new int[anchorCount];
            for (int i = 0; i < anchorCount; i++) anchorIds[i] = context.CreateId();
        }

        public UiId Id { get; }
        public int AnchorCount => anchorIds.Length;
        public Vector2 Position => position;
        public Rect VisibleRect { get; private set; }
        public int HighlightedAnchor => highlightedAnchor;
        public UiId AnchorId(int index) => anchorIds[index];

        public void SetPosition(float y)
        {
            if (float.IsNaN(y) || float.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y));
            position = new Vector2(0f, Mathf.Max(0f, y));
            scroll.Snap(position.y);
            animating = false;
            pendingAnchor = focusAfterScroll = -1;
        }

        public void Reset() { SetPosition(0f); highlightedAnchor = -1; highlight.Snap(0f); }

        // TOC selection requests an existing, currently registered section. It does not expand
        // hidden containers; unavailable/unregistered anchors are rejected by TryFocusAnchor.
        public bool TryFocusAnchor(int index, float margin = 12f)
        {
            ValidateIndex(index);
            if (float.IsNaN(margin) || float.IsInfinity(margin) || margin < 0f) throw new ArgumentOutOfRangeException(nameof(margin));
            if (anchorPasses[index] != pass || pass == 0) return false;
            pendingAnchor = index;
            focusMargin = margin;
            return true;
        }

        // Unconditional request is useful for TOCs drawn before the first content pass.
        // Returns resolved success from Draw; never scrolls to stale cached geometry.
        public void FocusAnchor(int index, float margin = 12f)
        {
            ValidateIndex(index);
            if (float.IsNaN(margin) || float.IsInfinity(margin) || margin < 0f) throw new ArgumentOutOfRangeException(nameof(margin));
            pendingAnchor = index;
            focusMargin = margin;
        }

        // Call from the cached draw callback. rect is LOCAL to the current GUI group;
        // nested accordion groups are converted back to natural scroll-content coordinates.
        // Pass available=false for a logically hidden/collapsing section. Register each once.
        public void Anchor(int index, Rect rect, bool available = true, UiId focusTarget = default(UiId))
        {
            ValidateIndex(index);
            if (!drawing) throw new InvalidOperationException("Anchors must be registered inside UiScrollView.Draw.");
            if (!available) return;
            if (anchorPasses[index] == pass) throw new InvalidOperationException("An anchor can only be registered once per scroll pass.");
            Vector2 origin = UiCoordinates.FromScreen(contentScreenOrigin);
            Rect contentRect = new Rect(rect.x - origin.x, rect.y - origin.y, rect.width, rect.height);
            anchorRects[index] = contentRect;
            anchorPasses[index] = pass;
            if (highlightedAnchor == index)
            {
                Color color = context.Palette.Signal;
                color.a *= highlight.Value * 0.16f;
                context.Fill(rect, color);
                Color edge = context.Palette.Signal;
                edge.a *= highlight.Value;
                context.Fill(new Rect(rect.x, rect.y, 3f, rect.height), edge);
            }
            if (focusAfterScroll == index && !animating && IsInsideVisible(contentRect)
                && context.IsInputAllowed && context.IsRectVisible(rect))
            {
                if (focusTarget.Value != 0) context.Focus(focusTarget);
                focusAfterScroll = -1;
            }
        }

        // Natural content height must come from layout, not the clipped viewport. The callback
        // runs once per IMGUI pass and receives a natural LOCAL content rect. Cache it at setup.
        // Returns true if this pass resolved a pending TOC focus request.
        public bool Draw(Rect rect, float naturalContentHeight, Action<Rect> drawContent, bool enabled = true)
        {
            if (drawContent == null) throw new ArgumentNullException(nameof(drawContent));
            if (float.IsNaN(naturalContentHeight) || float.IsInfinity(naturalContentHeight) || naturalContentHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(naturalContentHeight));
            if (drawing) throw new InvalidOperationException("A scroll view instance cannot draw recursively.");
            if (rect.width <= 0f || rect.height <= 0f) return false;
            viewport = new Rect(0f, 0f, rect.width, rect.height);
            contentHeight = Mathf.Max(naturalContentHeight, rect.height);
            float maximum = Mathf.Max(0f, contentHeight - rect.height);
            if (animating)
            {
                scrollTarget = Mathf.Clamp(scrollTarget, 0f, maximum);
                position.y = scroll.Step(scrollTarget, context.Time, UiMotion.Enter, context.ReducedMotion);
                animating = scroll.IsRunning;
            }
            position.y = Mathf.Clamp(position.y, 0f, maximum);
            position.x = 0f;
            float expected = position.y;
            float pulse = highlight.Step(highlightedAnchor >= 0 && context.Time < highlightUntil ? 1f : 0f,
                context.Time, UiMotion.Standard, context.ReducedMotion);
            if (pulse <= 0f && context.Time >= highlightUntil) highlightedAnchor = -1;
            if (pass == int.MaxValue)
            {
                Array.Clear(anchorPasses, 0, anchorPasses.Length);
                pass = 0;
            }
            pass++;
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, rect.width - 20f), contentHeight);
            bool resolved = false;
            Event inputEvent = context.Event;
            EventType previousEventType = inputEvent != null ? inputEvent.type : EventType.Ignore;
            using (context.Input(enabled))
            using (context.Clip(rect))
            {
                bool previousEnabled = GUI.enabled;
                bool blocked = !context.IsInputAllowed || !previousEnabled ||
                    (inputEvent != null && previousEventType != EventType.Layout && previousEventType != EventType.Repaint
                    && !context.IsEventInputAllowed(previousEventType));
                bool suppressEvent = blocked && inputEvent != null && previousEventType != EventType.Ignore
                    && previousEventType != EventType.Used && previousEventType != EventType.Layout && previousEventType != EventType.Repaint;
                GUI.enabled = previousEnabled && !blocked;
                if (suppressEvent)
                    inputEvent.type = EventType.Ignore;
                try { position = GUI.BeginScrollView(viewport, position, content, GUIStyle.none, GUIStyle.none); }
                finally
                {
                    GUI.enabled = previousEnabled;
                    if (suppressEvent) inputEvent.type = previousEventType;
                }
                if (position.y != expected)
                {
                    scroll.Snap(position.y);
                    animating = false;
                    focusAfterScroll = -1;
                }
                VisibleRect = new Rect(0f, position.y, content.width, rect.height);
                try
                {
                    using (context.Clip(content))
                    {
                        contentScreenOrigin = UiCoordinates.ToScreen(Vector2.zero);
                        drawing = true;
                        try { drawContent(content); }
                        finally { drawing = false; }
                    }
                }
                finally
                {
                    if (suppressEvent) inputEvent.type = EventType.Ignore;
                    try { GUI.EndScrollView(); }
                    finally { if (suppressEvent) inputEvent.type = previousEventType; }
                }
                DrawScrollbar(viewport, maximum, enabled);
                UiInteraction interaction = context.Interact(Id, viewport, enabled);
                if (interaction.Focused || context.IsFocused(scrollbarId))
                {
                    float requested = position.y;
                    bool moved = false;
                    if (context.ConsumeKey(KeyCode.PageDown)) { requested += rect.height * 0.85f; moved = true; }
                    else if (context.ConsumeKey(KeyCode.PageUp)) { requested -= rect.height * 0.85f; moved = true; }
                    else if (context.ConsumeKey(KeyCode.Home)) { requested = 0f; moved = true; }
                    else if (context.ConsumeKey(KeyCode.End)) { requested = maximum; moved = true; }
                    else if (context.ConsumeKey(KeyCode.DownArrow)) { requested += 36f; moved = true; }
                    else if (context.ConsumeKey(KeyCode.UpArrow)) { requested -= 36f; moved = true; }
                    if (moved)
                    {
                        scroll.Snap(position.y);
                        scrollTarget = Mathf.Clamp(requested, 0f, maximum);
                        animating = true;
                        focusAfterScroll = -1;
                    }
                }
                if (pendingAnchor >= 0)
                {
                    int index = pendingAnchor;
                    pendingAnchor = -1;
                    if (anchorPasses[index] == pass)
                    {
                        scroll.Snap(position.y);
                        scrollTarget = Mathf.Clamp(anchorRects[index].y - focusMargin, 0f, maximum);
                        animating = true;
                        highlightedAnchor = index;
                        highlightUntil = context.Time + 2.5;
                        focusAfterScroll = index;
                        // Focusing the scroll container now removes focus from the TOC without
                        // giving a not-yet-visible descendant keyboard input during animation.
                        if (context.IsInputAllowed) context.Focus(Id);
                        resolved = true;
                    }
                }
            }
            return resolved;
        }

        private void DrawScrollbar(Rect rect, float maximum, bool enabled)
        {
            Rect track = new Rect(rect.xMax - 12f, rect.y, 12f, rect.height);
            Event input = context.Event;
            EventType type = input != null ? input.type : EventType.Ignore;
            bool captured = context.IsCaptured(scrollbarId);
            float mouse = input != null ? UiCoordinates.ToScreen(input.mousePosition).y : 0f;
            UiInteraction interaction = context.Interact(scrollbarId, maximum > 0f ? track : default(Rect), enabled && maximum > 0f, false);
            if (maximum <= 0f) { scrollbarDragging = false; return; }
            float height = Mathf.Min(track.height, Mathf.Max(24f, track.height * rect.height / contentHeight));
            float travel = Mathf.Max(1f, track.height - height);
            float top = track.y + position.y / maximum * travel;
            if (!captured && context.IsCaptured(scrollbarId) && type == EventType.MouseDown)
            {
                scrollbarDragging = true;
                dragMouse = mouse;
                dragTravel = Mathf.Max(0.01f, Mathf.Abs(UiCoordinates.ToScreen(new Vector2(track.x, track.y + travel)).y - UiCoordinates.ToScreen(track.position).y));
                dragRange = maximum;
                if (input.mousePosition.y < top || input.mousePosition.y > top + height)
                    position.y = Mathf.Clamp01((input.mousePosition.y - track.y - height * 0.5f) / travel) * maximum;
                dragPosition = position.y;
                animating = false;
                focusAfterScroll = -1;
                scroll.Snap(position.y);
            }
            if (scrollbarDragging && captured && enabled && context.IsInputAllowed && (type == EventType.MouseDrag || type == EventType.MouseUp))
            {
                position.y = Mathf.Clamp(dragPosition + (mouse - dragMouse) / dragTravel * dragRange, 0f, maximum);
                scroll.Snap(position.y);
                if (type == EventType.MouseDrag) context.Consume();
            }
            if (!context.IsCaptured(scrollbarId)) scrollbarDragging = false;
            Color ink = Color.Lerp(Color.black, Color.white, context.ThemeAmount);
            context.Fill(new Rect(track.xMax - 3f, track.y + position.y / maximum * travel, 3f, height), ink);
            if (interaction.Focused && context.FocusVisible) context.Border(track, context.Palette.Ink);
        }

        public void Dispose()
        {
            drawing = animating = scrollbarDragging = false;
            pendingAnchor = highlightedAnchor = focusAfterScroll = -1;
            Release(Id);
            Release(scrollbarId);
            for (int i = 0; i < anchorIds.Length; i++) Release(anchorIds[i]);
        }

        private void Release(UiId id)
        {
            context.Release(id);
            if (context.IsFocused(id)) context.Focus(default(UiId));
        }

        private bool IsInsideVisible(Rect rect)
            => rect.yMax > position.y && rect.y < position.y + viewport.height;

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= anchorIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
