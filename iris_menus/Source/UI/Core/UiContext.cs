using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    /// <summary>One context per UI surface; begin/end every IMGUI pass, prepare modals before content.</summary>
    public sealed class UiContext : IDisposable
    {
        private static int nextId;
        private readonly Dictionary<UiId, UiId> owners = new Dictionary<UiId, UiId>();
        private readonly List<UiId> currentOrder = new List<UiId>();
        private readonly HashSet<UiId> currentIds = new HashSet<UiId>();
        private UiTween themeBlend;
        private bool initialized;
        private bool disposed;
        private UiId modal;
        private UiId layer;
        private UiId keyboardPressed;
        private Rect screenClip;
        private bool input = true;
        private int capturedFrame = -1;
        private int focusedFrame = -1;
        private int scopeDepth;
        private readonly GUIStyle labelStyle = new GUIStyle { fontSize = 16, richText = false };

        public UiPalette Palette { get; private set; }
        public bool ReducedMotion { get; private set; }
        public double Time { get; private set; }
        public int Frame { get; private set; } = -1;
        public Rect Viewport { get; private set; }
        public UiId Focused { get; private set; }
        public bool FocusVisible { get; private set; }
        public UiId Captured { get; private set; }
        public Event Event => Event.current;
        public bool IsRepaint => Event != null && Event.type == EventType.Repaint;
        public bool IsInputAllowed => input && Alpha > 0f && (modal == default || modal == layer);
        public float Alpha { get; private set; } = 1f;

        public UiId CreateId()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UiContext));
            return new UiId(System.Threading.Interlocked.Increment(ref nextId));
        }

        public void BeginFrame(Rect viewport, UiTheme theme, bool reducedMotion) =>
            BeginFrame(UnityEngine.Time.frameCount, UnityEngine.Time.realtimeSinceStartup, viewport, theme, reducedMotion);

        public void BeginFrame(int frame, double time, Rect viewport, UiTheme theme, bool reducedMotion)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UiContext));
            if (scopeDepth != 0) throw new InvalidOperationException("Unbalanced UI scopes from previous pass.");
            if (frame != Frame)
            {
                if (Captured != default && capturedFrame < Frame) Release(Captured);
                if (Focused != default && focusedFrame < Frame) Focused = default;
                Time = initialized ? Math.Max(Time, time) : time;
                Frame = frame;
            }
            Viewport = viewport;
            if (Event != null)
            {
                if (Event.type == EventType.KeyDown) FocusVisible = true;
                else if (Event.type == EventType.MouseDown) FocusVisible = false;
            }
            ReducedMotion = reducedMotion;
            float dark = theme == UiTheme.Dark ? 1f : 0f;
            if (!initialized) { themeBlend.Snap(dark); initialized = true; }
            Palette = UiPalette.Blend(themeBlend.Step(dark, Time, UiMotion.Standard, reducedMotion));
            modal = layer = default;
            input = true;
            Alpha = 1f;
            screenClip = ToScreen(viewport);
            currentOrder.Clear();
            currentIds.Clear();
            owners.Clear();
        }

        public void EndFrame()
        {
            if (scopeDepth != 0) throw new InvalidOperationException("Unbalanced UI scopes.");
            if (Event != null && Event.type == EventType.KeyDown && Event.keyCode == KeyCode.Tab && input)
            {
                MoveFocus(Event.shift ? -1 : 1);
                Consume();
            }
            if (Captured != default && (!currentIds.Contains(Captured) || (Event != null && Event.type == EventType.MouseUp))) Release(Captured);
        }

        public void SetModal(UiId id)
        {
            modal = id;
        }

        public UiInteraction Interact(UiId id, Rect rect, bool enabled = true, bool activateWithKeyboard = true)
        {
            bool visible = rect.width > 0f && rect.height > 0f && Intersects(ToScreen(rect), screenClip);
            bool allowed = enabled && GUI.enabled && IsInputAllowed && visible;
            if (!allowed)
            {
                Release(id);
                if (Focused == id) Focused = default;
                return default;
            }
            owners[id] = layer;
            if (currentIds.Add(id)) currentOrder.Add(id);
            if (Focused == id) focusedFrame = Frame;
            if (Captured == id) capturedFrame = Frame;
            bool hovered = Hit(rect);
            bool clicked = false;
            Event evt = Event;
            if (evt != null)
            {
                if (evt.type == EventType.MouseDown && evt.button == 0 && hovered && Captured == default)
                {
                    Captured = id;
                    capturedFrame = Frame;
                    Focus(id);
                    Consume();
                }
                else if (evt.type == EventType.MouseUp && evt.button == 0 && Captured == id)
                {
                    clicked = hovered;
                    Release(id);
                    Consume();
                }
                else if (evt.type == EventType.KeyDown && Focused == id)
                {
                    if (evt.keyCode == KeyCode.Escape && (Captured == id || keyboardPressed == id)) { Release(id); Consume(); }
                    else if (activateWithKeyboard && (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                    { keyboardPressed = id; Consume(); }
                }
                else if (evt.type == EventType.KeyUp && Focused == id && activateWithKeyboard && keyboardPressed == id &&
                    (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                { keyboardPressed = default; clicked = true; Consume(); }
            }
            return new UiInteraction(hovered, (Captured == id && hovered) || keyboardPressed == id, Focused == id, clicked);
        }

        public bool IsCaptured(UiId id) => Captured == id;
        public bool IsFocused(UiId id) => Focused == id;
        public void Focus(UiId id) { Focused = id; focusedFrame = Frame; keyboardPressed = default; }
        public bool IsRectVisible(Rect rect) => rect.width > 0f && rect.height > 0f && Intersects(ToScreen(rect), screenClip);
        public void Release(UiId id)
        {
            if (Captured == id) Captured = default;
            if (keyboardPressed == id) keyboardPressed = default;
        }

        public bool Hit(Rect rect)
        {
            if (!IsInputAllowed || Event == null || !rect.Contains(Event.mousePosition)) return false;
            if (!screenClip.Contains(GUIUtility.GUIToScreenPoint(Event.mousePosition))) return false;
#if RIMWORLD_1_7
            if (UnityStubs.Gui.GuiBackend.Current?.MouseOutsideHitClip == true) return false;
#endif
            return true;
        }

        public bool ConsumeKey(KeyCode key)
        {
            if (!IsInputAllowed || Event == null || Event.type != EventType.KeyDown || Event.keyCode != key) return false;
            Consume();
            return true;
        }

        public void Consume() { if (Event != null && Event.type != EventType.Layout && Event.type != EventType.Repaint && Event.type != EventType.Used) Event.Use(); }
        public UiScope Layer(UiId id) => new UiScope(this, 0, id, default, 1f, true);
        public UiScope Clip(Rect rect) => new UiScope(this, 1, default, rect, 1f, true);
        public UiScope Opacity(float multiplier) => new UiScope(this, 2, default, default, multiplier, true);
        public UiScope Input(bool enabled) => new UiScope(this, 3, default, default, 1f, enabled);

        public void Fill(Rect rect, Color color, float radius = 0f)
        {
            if (!IsRepaint || rect.width <= 0f || rect.height <= 0f) return;
            color.a *= Alpha;
#if RIMWORLD_1_7
            var backend = UnityStubs.Gui.GuiBackend.Current;
            if (radius > 0f) backend?.FillRectRounded(rect, color, radius);
            else backend?.FillRect(rect, color);
#else
            Color previous = GUI.color;
            GUI.color = Color.white;
            try { GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true, 0f, color, 0f, radius); }
            finally { GUI.color = previous; }
#endif
        }

        public void Border(Rect rect, Color color, float width = 1f)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        public void Label(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (!IsRepaint) return;
            color.a *= Alpha;
#if RIMWORLD_1_7
            UnityStubs.Gui.GuiBackend.Current?.DrawText(rect, text ?? string.Empty, 1, 16f, FontStyle.Normal, anchor, wrap, color, default, false);
#else
            Color previous = GUI.color;
            Color content = GUI.contentColor;
            labelStyle.font = Text.CurFontStyle.font;
            labelStyle.normal.textColor = color;
            labelStyle.alignment = anchor;
            labelStyle.wordWrap = wrap;
            GUI.color = GUI.contentColor = Color.white;
            try { GUI.Label(rect, text ?? string.Empty, labelStyle); }
            finally { GUI.color = previous; GUI.contentColor = content; }
#endif
        }

        public void Line(Vector2 from, Vector2 to, Color color, float width = 1f)
        {
            if (!IsRepaint) return;
            float dx = to.x - from.x, dy = to.y - from.y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0f) return;
#if RIMWORLD_1_7
            if (Math.Abs(dy) < 0.001f) { Fill(new Rect(Math.Min(from.x, to.x), from.y - width / 2f, length, width), color); return; }
            if (Math.Abs(dx) < 0.001f) { Fill(new Rect(from.x - width / 2f, Math.Min(from.y, to.y), width, length), color); return; }
            float x = -dy * width / (2f * length), y = dx * width / (2f * length);
            color.a *= Alpha;
            Span<UnityStubs.Gui.GuiVertex> vertices = stackalloc UnityStubs.Gui.GuiVertex[4];
            vertices[0] = new UnityStubs.Gui.GuiVertex { position = new Vector2(from.x + x, from.y + y), color = color };
            vertices[1] = new UnityStubs.Gui.GuiVertex { position = new Vector2(to.x + x, to.y + y), color = color };
            vertices[2] = new UnityStubs.Gui.GuiVertex { position = new Vector2(to.x - x, to.y - y), color = color };
            vertices[3] = new UnityStubs.Gui.GuiVertex { position = new Vector2(from.x - x, from.y - y), color = color };
            UnityStubs.Gui.GuiBackend.Current?.DrawPrimitives(7, vertices, null, false);
#else
            Color previous = GUI.color;
            color.a *= Alpha;
            try { Widgets.DrawLine(from, to, color, width); }
            finally { GUI.color = previous; }
#endif
        }

        private void MoveFocus(int direction)
        {
            int count = currentOrder.Count;
            if (count == 0) { Focused = default; return; }
            int index = currentOrder.IndexOf(Focused);
            if (index < 0) index = direction > 0 ? -1 : 0;
            for (int step = 1; step <= count; step++)
            {
                UiId candidate = currentOrder[(index + direction * step + count) % count];
                if (modal == default || owners[candidate] == modal) { Focus(candidate); return; }
            }
            Focused = default;
        }

        private static Rect ToScreen(Rect rect)
        {
            Vector2 min = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMin));
            Vector2 max = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
            return new Rect(Math.Min(min.x, max.x), Math.Min(min.y, max.y), Math.Abs(max.x - min.x), Math.Abs(max.y - min.y));
        }
        private static bool Intersects(Rect a, Rect b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Math.Max(a.xMin, b.xMin), y = Math.Max(a.yMin, b.yMin);
            return new Rect(x, y, Math.Max(0f, Math.Min(a.xMax, b.xMax) - x), Math.Max(0f, Math.Min(a.yMax, b.yMax) - y));
        }

        public void Dispose()
        {
            if (scopeDepth != 0) throw new InvalidOperationException("Cannot dispose UI context inside a scope.");
            disposed = true;
            Captured = Focused = keyboardPressed = default;
            owners.Clear(); currentOrder.Clear(); currentIds.Clear();
        }

        public readonly struct UiScope : IDisposable
        {
            private readonly UiContext owner;
            private readonly int kind;
            private readonly UiId oldLayer;
            private readonly Rect oldClip;
            private readonly float oldAlpha;
            private readonly bool oldInput;
            private readonly bool oldGuiEnabled;
            internal UiScope(UiContext owner, int kind, UiId nextLayer, Rect rect, float alpha, bool input)
            {
                this.owner = owner; this.kind = kind;
                oldLayer = owner.layer; oldClip = owner.screenClip; oldAlpha = owner.Alpha; oldInput = owner.input; oldGuiEnabled = GUI.enabled;
                owner.scopeDepth++;
                if (kind == 0) owner.layer = nextLayer;
                else if (kind == 1) { owner.screenClip = Intersection(owner.screenClip, ToScreen(rect)); GUI.BeginGroup(rect); }
                else if (kind == 2) owner.Alpha *= Mathf.Clamp01(alpha);
                else { owner.input &= input; GUI.enabled &= input && owner.IsInputAllowed; }
            }
            public void Dispose()
            {
                if (owner == null) return;
                if (kind == 1) GUI.EndGroup();
                owner.layer = oldLayer; owner.screenClip = oldClip; owner.Alpha = oldAlpha; owner.input = oldInput;
                GUI.enabled = oldGuiEnabled;
                owner.scopeDepth--;
            }
        }
    }
}
