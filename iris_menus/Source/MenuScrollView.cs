using System;
using System.Collections.Generic;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class MenuScrollView : IDisposable
    {
        private Vector2 position;
        private float contentHeight;
        private string pendingFocus;
        private string highlighted;
        private float highlightUntil;
        private float? restoredPosition;
        private readonly Dictionary<string, Rect> anchors = new Dictionary<string, Rect>();
        private readonly Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
        private readonly UiScrollView nativeScroll;
        private readonly Action<Rect> drawNativeContent;
        private Action<Listing_Standard> nativeDrawer;
        internal static MenuScrollView Active { get; private set; }
        // Valid while Draw is executing; coordinates match Listing_Standard content.
        public Rect VisibleRect { get; private set; }

        public MenuScrollView() { }

        // The caller owns the prepared context and its frame/overlay lifetime.
        public MenuScrollView(UiContext context)
        {
            nativeScroll = new UiScrollView(context ?? throw new ArgumentNullException(nameof(context)), 1);
            drawNativeContent = DrawNativeContent;
        }

        public void Reset()
        {
            position = Vector2.zero;
            restoredPosition = null;
            pendingFocus = highlighted = null;
            nativeScroll?.Reset();
        }

        internal float PositionY
        {
            get => restoredPosition ?? (nativeScroll == null ? position.y : nativeScroll.Position.y);
            set => restoredPosition = Mathf.Max(0f, MenuSettings.Finite(value, 0f));
        }

        public void Focus(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId)) throw new ArgumentException("An anchor ID is required.", nameof(anchorId));
            pendingFocus = highlighted = anchorId;
            highlightUntil = Time.realtimeSinceStartup + 4f;
        }

        internal void Anchor(string id, Rect rect)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An anchor ID is required.", nameof(id));
            if (anchors.ContainsKey(id)) throw new ArgumentException("Duplicate anchor ID in this page: " + id);
            anchors.Add(id, rect);
            if (nativeScroll != null)
            {
                if (id == highlighted) nativeScroll.Anchor(0, rect);
            }
            else if (id == highlighted && Time.realtimeSinceStartup <= highlightUntil) Widgets.DrawHighlight(rect);
        }

        public void Draw(Rect viewport, Action<Listing_Standard> draw)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            if (nativeScroll != null)
            {
                DrawNative(viewport, draw);
                return;
            }
            float height = Mathf.Max(viewport.height, contentHeight);
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f), height);
            position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref position, content);
            VisibleRect = new Rect(position.x, position.y, content.width, viewport.height);
            MenuScrollView previous = Active;
            Active = this;
            anchors.Clear();
            try
            {
                DrawListing(content, draw);
                if (restoredPosition.HasValue)
                {
                    position.y = Mathf.Clamp(restoredPosition.Value, 0f, Mathf.Max(0f, contentHeight - viewport.height));
                    restoredPosition = null;
                }
                if (pendingFocus != null)
                {
                    if (anchors.TryGetValue(pendingFocus, out Rect target))
                        position.y = Mathf.Clamp(target.y - 12f, 0f, Mathf.Max(0f, contentHeight - viewport.height));
                    else Log.Warning("[IrisMenus] Anchor is not currently visible in page content: " + pendingFocus);
                    pendingFocus = null;
                }
            }
            finally { Active = previous; Widgets.EndScrollView(); }
        }

        private void DrawNative(Rect viewport, Action<Listing_Standard> draw)
        {
            if (viewport.width <= 0f || viewport.height <= 0f) return;
            if (restoredPosition.HasValue)
            {
                nativeScroll.SetPosition(restoredPosition.Value);
                restoredPosition = null;
            }
            if (pendingFocus != null) nativeScroll.FocusAnchor(0);
            float naturalHeight = Mathf.Max(viewport.height, contentHeight);
            nativeDrawer = draw;
            try
            {
                bool resolved = nativeScroll.Draw(viewport, naturalHeight, drawNativeContent);
                VisibleRect = new Rect(0f, nativeScroll.Position.y, nativeScroll.VisibleRect.width, viewport.height);
                if (pendingFocus != null && contentHeight <= naturalHeight)
                {
                    if (!resolved) Log.Warning("[IrisMenus] Anchor is not currently visible in page content: " + pendingFocus);
                    pendingFocus = null;
                }
            }
            finally { nativeDrawer = null; }
        }

        private void DrawNativeContent(Rect content)
        {
            VisibleRect = nativeScroll.VisibleRect;
            MenuScrollView previous = Active;
            Active = this;
            anchors.Clear();
            try { DrawListing(content, nativeDrawer); }
            finally { Active = previous; }
        }

        private void DrawListing(Rect content, Action<Listing_Standard> draw)
        {
            listing.Begin(content);
            try { draw(listing); }
            finally
            {
                contentHeight = listing.CurHeight + 4f;
                listing.End();
            }
        }

        public void Dispose()
        {
            nativeScroll?.Dispose();
            anchors.Clear();
            pendingFocus = highlighted = null;
        }
    }
}
