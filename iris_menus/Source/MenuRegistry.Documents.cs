using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public static partial class MenuRegistry
    {
        private sealed class DocumentRegistration
        {
            internal Mod Owner;
            internal string ParentId;
            internal DocumentItem Item;
            internal MenuPage Page;
            internal DocumentPageView View;
        }

        private static readonly List<DocumentRegistration> documents = new List<DocumentRegistration>();

        public static void RegisterDocument(Mod owner, DocumentItem item, string parentId = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (parentId != null && !documents.Any(entry => ReferenceEquals(entry.Owner, owner) && entry.ParentId == null && entry.Item.Id == parentId))
                throw new ArgumentException("Register the parent document first.", nameof(parentId));
            string localId = "document/" + (parentId == null ? item.Id : parentId + "/" + item.Id);
            var view = new DocumentPageView(item);
            RegisterSubItem(owner, localId, item.Title, view.Draw);
            MenuPage page = FindRegistered(owner, localId);
            page.Focus = view.Focus;
            documents.Add(new DocumentRegistration { Owner = owner, ParentId = parentId, Item = item, Page = page, View = view });
            RegisterPresentation(owner, localId, new MenuPresentation(context => new DocumentNativePage(context, item)));
        }

        public static bool OpenUri(string value)
        {
            if (DocumentUri.TryParse(value, out DocumentUri uri)) return OpenUri(uri);
            Messages.Message("IM_LinkUnavailable".Translate("Invalid URI"), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public static bool OpenUri(DocumentUri uri)
        {
            if (!TryResolveUri(uri, out MenuPage page, out Action focus, out string error))
            {
                Messages.Message("IM_LinkUnavailable".Translate(error), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            MenuWindow window = Find.WindowStack.WindowOfType<MenuWindow>();
            if (window == null)
            {
                window = new MenuWindow();
                Find.WindowStack.Add(window);
            }
            return window.NavigateUri(page, focus, uri.Kind == DocumentTargetKind.DocumentItem ? uri.Line.ToString(CultureInfo.InvariantCulture) : null);
        }

        internal static bool TryResolveUri(DocumentUri uri, out MenuPage page, out Action focus, out string error)
        {
            page = null;
            focus = null;
            error = null;
            if (!uri.IsValid) { error = "Invalid URI"; return false; }
            var owners = LoadedModManager.ModHandles.Where(mod => string.Equals(mod.Content.PackageIdPlayerFacing, uri.PackageId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (owners.Count == 0) { error = "Mod is not enabled: " + uri.PackageId; return false; }
            if (!owners.Any(HasPages)) { error = "Mod has not registered IrisMenus pages: " + uri.PackageId; return false; }
            if (uri.Kind == DocumentTargetKind.SubItem)
            {
                var matches = registered.Where(candidate => owners.Contains(candidate.Owner) && candidate.IsSubItem &&
                    !documents.Any(entry => ReferenceEquals(entry.Page, candidate)) && PageLocalId(candidate.Id, candidate.OwnerId) == uri.PageId).ToList();
                if (matches.Count != 1) { error = "Unknown or ambiguous SubItem: " + uri.PageId; return false; }
                page = matches[0];
                return true;
            }
            var targets = documents.Where(entry => owners.Contains(entry.Owner) &&
                (uri.ChildId == null ? entry.ParentId == null && entry.Item.Id == uri.PageId : entry.ParentId == uri.PageId && entry.Item.Id == uri.ChildId)).ToList();
            if (targets.Count != 1) { error = "Unknown or ambiguous document: " + uri.LocalId; return false; }
            DocumentRegistration target = targets[0];
            if (uri.Line > 0 && !target.Item.TryGetLinePosition(600f, uri.Line, out _))
            {
                error = "Unknown document line: " + uri.Line.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            page = target.Page;
            string anchor = uri.Line.ToString(CultureInfo.InvariantCulture);
            MenuPage destination = page;
            focus = () => destination.Focus?.Invoke(anchor);
            return true;
        }

        private sealed class DocumentPageView
        {
            private readonly DocumentItem item;
            private Vector2 position;
            private int pendingLine = -1;
            internal DocumentPageView(DocumentItem item) { this.item = item; }
            internal void Focus(string anchor)
            {
                if (int.TryParse(anchor, NumberStyles.None, CultureInfo.InvariantCulture, out int line)) pendingLine = line;
            }
            internal void Draw(Rect viewport)
            {
                float width = Mathf.Max(1f, viewport.width - 20f);
                float height = item.Measure(width);
                if (pendingLine >= 0)
                {
                    if (item.TryGetLinePosition(width, pendingLine, out float y)) position.y = y;
                    pendingLine = -1;
                }
                position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, height - viewport.height));
                Widgets.BeginScrollView(viewport, ref position, new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
                try { item.Draw(new Rect(0f, 0f, width, height), uri => OpenUri(uri)); }
                finally { Widgets.EndScrollView(); }
            }
        }
    }
}
