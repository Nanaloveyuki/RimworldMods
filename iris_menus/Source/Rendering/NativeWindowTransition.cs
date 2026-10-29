using System;
using IrisMenus.UI;
using UnityEngine;

namespace IrisMenus
{
    internal sealed class NativeWindowTransition : IDisposable
    {
#if RIMWORLD_1_7
        private const float Duration = 0.72f;
        private NativeGuiSnapshot17 snapshot;
        private double sampled;
        private float progress;
        private bool closing, frozen, failed;
        private Rect capturedViewport;

        internal float Value => progress;
        internal bool Active => !failed && (closing ? progress > 0f : progress < 1f);

        internal void Open()
        {
            snapshot?.ReleaseTexture();
            progress = 0f;
            closing = frozen = false;
            sampled = Time.realtimeSinceStartup;
        }

        internal float Sample(bool close, bool reducedMotion)
        {
            if (close != closing)
            {
                closing = close;
                // An early close reverses the same frozen opening frame.
                if (!frozen) sampled = Time.realtimeSinceStartup;
            }
            double now = Time.realtimeSinceStartup;
            if (reducedMotion || failed) progress = closing ? 0f : 1f;
            else if (frozen) progress = Mathf.Clamp01(progress + (float)(now - sampled) / Duration * (closing ? -1f : 1f));
            sampled = now;
            return progress;
        }

        internal bool Draw(Rect viewport, bool reducedMotion, Action draw)
        {
            if (closing && progress <= 0f)
            {
                snapshot?.ReleaseTexture();
                frozen = false;
                return true;
            }
            if (reducedMotion || failed || !Active)
            {
                snapshot?.ReleaseTexture();
                frozen = false;
                return false;
            }
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.Repaint)
            {
                if (evt != null && evt.type != EventType.Layout && evt.type != EventType.Used) evt.Use();
                return true;
            }
            if (!frozen || capturedViewport != viewport)
            {
                try
                {
                    if (snapshot == null) snapshot = new NativeGuiSnapshot17();
                    snapshot.Capture(draw);
                    capturedViewport = viewport;
                    frozen = true;
                    sampled = Time.realtimeSinceStartup;
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception)
                {
                    failed = true;
                    Verse.Log.Error("[IrisMenus] Could not capture the interface transition: " + exception);
                    return true;
                }
            }
            Color saved = GUI.color;
            try
            {
                GUI.color = Color.white;
                const int rows = 8;
                float side = viewport.height / rows;
                int columns = Mathf.Max(1, Mathf.CeilToInt(viewport.width / side));
                for (int column = 0; column < columns; column++)
                {
                    float amount = Mathf.Clamp01(progress * columns - column);
                    if (amount <= 0f) continue;
                    amount = amount * amount * (3f - 2f * amount);
                    for (int row = 0; row < rows; row++)
                    {
                        Rect tile = new Rect(viewport.x + column * side, viewport.y + row * side,
                            Mathf.Min(side, viewport.width - column * side), side);
                        Rect destination = new Rect(tile.center.x - tile.width * amount * 0.5f,
                            tile.center.y - tile.height * amount * 0.5f, tile.width * amount, tile.height * amount);
                        snapshot.DrawTile(destination, tile);
                    }
                }
            }
            finally { GUI.color = saved; }
            return true;
        }

        public void Dispose() { snapshot?.Dispose(); snapshot = null; }
#else
        private UiTween presence;
        internal float Value => presence.Value;
        internal bool Active => presence.IsRunning;
        internal void Open() => presence.Snap(0f);
        internal float Sample(bool close, bool reducedMotion) => presence.Step(close ? 0f : 1f,
            Time.realtimeSinceStartup, close ? UiMotion.Exit : UiMotion.Enter, reducedMotion);
        internal bool Draw(Rect viewport, bool reducedMotion, Action draw) => false;
        public void Dispose() { }
#endif
    }
}
