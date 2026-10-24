using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public static partial class MenuRegistry
    {
        private static readonly Dictionary<string, MenuPresentation> presentations =
            new Dictionary<string, MenuPresentation>(StringComparer.Ordinal);

        // Register the ordinary page first. This annotation changes only its full-screen presentation.
        public static void RegisterPresentation(Mod owner, string pageId, MenuPresentation presentation)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(pageId)) throw new ArgumentException("A page ID is required.", nameof(pageId));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            MenuPage page = FindRegistered(owner, pageId);
            presentations[page.Id] = presentation;
            Revision++;
        }

        internal static MenuPresentation PresentationFor(MenuPage page) =>
            page != null && presentations.TryGetValue(page.Id, out MenuPresentation presentation) ? presentation : null;
    }

    public sealed class MenuPresentation
    {
        public MenuPresentation(Func<UI.UiContext, MenuNativePage> create)
        {
            Create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public Func<UI.UiContext, MenuNativePage> Create { get; }
    }

    // The host owns the context and the lifetime of this page. Draw owns its content scrolling;
    // Prepare runs before host input, and DrawOverlay runs outside the content clip at the root.
    public abstract class MenuNativePage : IDisposable
    {
        public abstract void Draw(Rect rect);
        public virtual void Prepare() { }
        public virtual void DrawOverlay(Rect viewport) { }
        public virtual void Focus(string anchor) { }
        public virtual IReadOnlyList<MenuChapter> Chapters => Array.Empty<MenuChapter>();
        public virtual IReadOnlyList<MenuNote> Notes => Array.Empty<MenuNote>();
        public virtual bool HasPreview => false;
        public virtual void DrawPreview(Rect rect) { }
        public virtual bool CanReset => false;
        public virtual void Reset() { }
        public virtual void Dispose() { }
    }

    public sealed class MenuChapter
    {
        public MenuChapter(string id, Func<string> title)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A chapter ID is required.", nameof(id));
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        public string Id { get; }
        public Func<string> Title { get; }
    }

    public sealed class MenuNote
    {
        public MenuNote(string id, Func<string> title, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A note ID is required.", nameof(id));
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string Id { get; }
        public Func<string> Title { get; }
        public Func<string> Text { get; }
    }
}
