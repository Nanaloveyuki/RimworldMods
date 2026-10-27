using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using IrisMenus.UI;

namespace IrisMenus
{
    public sealed partial class MenuWindow
    {
        private sealed partial class NativeSurface
        {
            private sealed class OwnerGroup
            {
                internal ModContentPack Content;
                internal readonly List<MenuPage> Pages = new List<MenuPage>();
                internal readonly UiTabs Tabs;
                internal OwnerGroup(UiContext ui, ModContentPack content)
                { Content = content; Tabs = new UiTabs(ui, Array.Empty<string>()); }

                internal void SyncTabs()
                {
                    var labels = new List<string>();
                    var ids = new List<string>();
                    foreach (MenuPage page in Pages)
                    {
                        if (page.ActionLauncher) continue;
                        labels.Add(page.DisplayTitle); ids.Add(page.Id);
                    }
                    Tabs.SetItems(labels.ToArray(), ids.ToArray());
                }
                internal string Name => Pages[0].DisplayModName;
                internal bool Hosted
                {
                    get
                    {
                        foreach (MenuPage page in Pages) if (!page.Adapted) return false;
                        return true;
                    }
                }
            }

            private sealed class PageState : IDisposable
            {
                internal readonly MenuPage Page;
                internal readonly MenuPresentation Presentation;
                internal readonly UiId Id;
                private readonly UiContext ui;
                internal MenuNativePage Native;
                internal bool Failed;
                internal OwnerGroup Group;
                internal int TabIndex;
                internal UiButton[] Chapters;
                internal UiTween[] ChapterInk;
                internal NoteState[] Notes;
                internal UiTween ChapterMarker;
                internal readonly Dictionary<string, UiButton> Actions = new Dictionary<string, UiButton>(StringComparer.Ordinal);
                internal PageState(MenuPage page, UiContext ui)
                {
                    Page = page; Id = ui.CreateId(); this.ui = ui;
                    Presentation = MenuRegistry.PresentationFor(page);
                    CreateNative();
                }

                internal void CreateNative()
                {
                    Failed = false;
                    try
                    {
                        Native = Presentation?.Create(ui);
                        if (Presentation != null && Native == null) throw new InvalidOperationException("Native presentation returned no page.");
                        if (Native != null)
                        {
                            Chapters = new UiButton[Native.Chapters.Count];
                            ChapterInk = new UiTween[Chapters.Length];
                            for (int i = 0; i < Chapters.Length; i++) Chapters[i] = new UiButton(ui);
                            Notes = new NoteState[Native.Notes.Count];
                            for (int i = 0; i < Notes.Length; i++) Notes[i] = new NoteState(Native.Notes[i], ui, i == 0);
                        }
                    }
                    catch (Exception exception)
                    {
                        Dispose();
                        Chapters = Array.Empty<UiButton>(); Notes = Array.Empty<NoteState>();
                        ChapterInk = Array.Empty<UiTween>();
                        Failed = true; ReportFailure("create native " + Page.Id, exception);
                    }
                }
                public void Dispose()
                {
                    MenuNativePage page = Native; Native = null;
                    try { page?.Dispose(); }
                    catch (Exception exception) { ReportFailure("dispose native " + Page.Id, exception); }
                }
            }

            private sealed class NoteState
            {
                private readonly MenuNote note;
                private readonly UiContext ui;
                internal readonly UiAccordion Accordion;
                internal readonly Action<Rect> Draw;
                internal bool Open;
                internal string Title => note.Title() ?? "";
                internal float Height(float width, float scale) => Mathf.Max(48f * scale, Text.CalcHeight(note.Text() ?? "", Mathf.Max(1f, width)) + 16f * scale);
                internal NoteState(MenuNote note, UiContext ui, bool open)
                {
                    this.note = note; this.ui = ui; Open = open;
                    Accordion = new UiAccordion(ui, open);
                    Draw = DrawText;
                }
                private void DrawText(Rect rect) => ui.Label(new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, rect.height - 16f)), note.Text() ?? "", ui.Palette.Muted, TextAnchor.UpperLeft, true);
            }

            private void SyncCatalog()
            {
                if (revision == host.catalogRevision && ReferenceEquals(language, host.catalogLanguage)) return;
                revision = host.catalogRevision; language = host.catalogLanguage;
                var valid = new HashSet<string>(StringComparer.Ordinal);
                OwnerGroup[] previousGroups = owners.ToArray();
                foreach (OwnerGroup group in previousGroups) group.Pages.Clear();
                owners.Clear(); registeredOwners.Clear(); hostedOwners.Clear();
                foreach (MenuPage page in host.pages)
                {
                    valid.Add(page.Id);
                    OwnerGroup group = null;
                    foreach (OwnerGroup candidate in owners) if (ReferenceEquals(candidate.Content, page.Content)) { group = candidate; break; }
                    if (group == null)
                    {
                        foreach (OwnerGroup candidate in previousGroups)
                            if (ReferenceEquals(candidate.Content, page.Content)) { group = candidate; break; }
                        if (group == null) group = new OwnerGroup(ui, page.Content);
                        owners.Add(group);
                    }
                    group.Pages.Add(page);
                    if (states.TryGetValue(page.Id, out PageState previous) &&
                        (!ReferenceEquals(previous.Page, page) || !ReferenceEquals(previous.Presentation, MenuRegistry.PresentationFor(page))))
                    { previous.Dispose(); states.Remove(page.Id); }
                }
                var removed = new List<string>();
                foreach (var pair in states) if (!valid.Contains(pair.Key)) { pair.Value.Dispose(); removed.Add(pair.Key); }
                foreach (string id in removed) states.Remove(id);
                ApplySidebarOrder();
                foreach (OwnerGroup group in owners)
                {
                    group.SyncTabs();
                    (group.Hosted ? hostedOwners : registeredOwners).Add(group);
                }
                sidebarOwners.Clear();
                foreach (string key in Settings.SidebarPins)
                    foreach (OwnerGroup group in owners)
                        if (MenuRegistry.SidebarIdentity(group.Content) == key && !sidebarOwners.Contains(group)) sidebarOwners.Add(group);
                pinnedCount = sidebarOwners.Count;
                foreach (OwnerGroup group in registeredOwners)
                    if (!Settings.SidebarPins.Contains(MenuRegistry.SidebarIdentity(group.Content))) sidebarOwners.Add(group);
                registeredCount = sidebarOwners.Count;
                foreach (OwnerGroup group in hostedOwners)
                    if (!Settings.SidebarPins.Contains(MenuRegistry.SidebarIdentity(group.Content))) sidebarOwners.Add(group);
                sidebarLabels = new string[sidebarOwners.Count];
                sidebarIcons = new Texture2D[sidebarOwners.Count];
                sidebarKeys = new string[sidebarOwners.Count];
                sidebarRects = new Rect[sidebarOwners.Count];
                for (int i = 0; i < sidebarOwners.Count; i++)
                {
                    sidebarLabels[i] = sidebarOwners[i].Name;
                    sidebarIcons[i] = ModIcon(sidebarOwners[i].Content);
                    sidebarKeys[i] = MenuRegistry.SidebarIdentity(sidebarOwners[i].Content);
                }
                sidebarRail = sidebarRail ?? new UiSideRail(ui, sidebarLabels, sidebarKeys);
                sidebarRail.SetItems(sidebarLabels, sidebarKeys);
                foreach (PageState state in states.Values) BindTabs(state);
            }

            private Rect[] sidebarRects = Array.Empty<Rect>();
            private int pinnedCount, registeredCount;
            private void ApplySidebarOrder()
            {
                owners.Sort((left, right) =>
                {
                    string lk = MenuRegistry.SidebarIdentity(left.Content), rk = MenuRegistry.SidebarIdentity(right.Content);
                    int li = Settings.SidebarOrder.IndexOf(lk), ri = Settings.SidebarOrder.IndexOf(rk);
                    if (li < 0) li = int.MaxValue; if (ri < 0) ri = int.MaxValue;
                    if (li != ri) return li.CompareTo(ri);
                    return StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
                });
            }


            private readonly List<OwnerGroup> sidebarOwners = new List<OwnerGroup>();
            private UiSideRail sidebarRail;
            private string[] sidebarLabels = Array.Empty<string>(), sidebarKeys = Array.Empty<string>();
            private Texture2D[] sidebarIcons = Array.Empty<Texture2D>();
            private readonly Dictionary<ModContentPack, Texture2D> modIcons = new Dictionary<ModContentPack, Texture2D>();

            private Texture2D ModIcon(ModContentPack content)
            {
                if (content == null) return null;
                if (modIcons.TryGetValue(content, out Texture2D cached)) return cached;
                string path = Path.Combine(content.RootDir, "About", "ModIcon.png");
                Texture2D icon = null;
                try
                {
                    if (File.Exists(path))
                    {
                        icon = new Texture2D(0, 0);
                        if (!icon.LoadImage(File.ReadAllBytes(path)))
                        { UnityEngine.Object.Destroy(icon); icon = null; }
                    }
                }
                catch (Exception exception)
                {
                    if (icon != null) UnityEngine.Object.Destroy(icon);
                    icon = null;
                    Log.Warning("[IrisMenus] Cannot load ModIcon at " + path + ": " + exception.Message);
                }
                modIcons.Add(content, icon);
                return icon;
            }

            private void DisposeModIcons()
            {
                foreach (Texture2D icon in modIcons.Values) if (icon != null) UnityEngine.Object.Destroy(icon);
                modIcons.Clear();
            }

            private PageState GetPage(MenuPage page)
            {
                if (page == null) return null;
                if (!states.TryGetValue(page.Id, out PageState state))
                {
                    state = new PageState(page, ui); states.Add(page.Id, state); BindTabs(state);
                }
                return state;
            }

            private void BindTabs(PageState state)
            {
                state.Group = null; state.TabIndex = -1;
                foreach (OwnerGroup group in owners)
                {
                    if (!ReferenceEquals(group.Content, state.Page.Content)) continue;
                    state.Group = group;
                    int index = 0;
                    foreach (MenuPage page in group.Pages)
                    {
                        if (page.ActionLauncher) continue;
                        if (page.Id == state.Page.Id) state.TabIndex = index;
                        index++;
                    }
                    return;
                }
            }

            private void DrawTabs(Rect main)
            {
                UiTabs tabs = selected?.Group?.Tabs;
                if (tabs == null || tabs.Count == 0) return;
                float width = Mathf.Max(1f, main.width - 108f * scale);
                Rect pager = new Rect(main.xMax - 196f * scale, main.y - 64f * scale, 196f * scale, 40f * scale);
                int index = tabs.Draw(new Rect(main.x, main.y, width, 44f * scale), selected.TabIndex, scale: scale, paginationRect: pager);
                if (index != selected.TabIndex) SelectTab(index);
            }

            private void SelectTab(int index)
            {
                if (selected?.Group == null) return;
                int current = 0;
                foreach (MenuPage page in selected.Group.Pages)
                {
                    if (page.ActionLauncher) continue;
                    if (current++ == index) { Navigate(page); return; }
                }
            }

            private void Navigate(MenuPage page)
            {
                host.Select(page);
                if (!ReferenceEquals(host.session.Selected, page)) return;
#if RIMWORLD_1_7
                mainButtonsOpen = false;
#endif
                managementOpen = false;
                navigationOpen = contextOpen = false;
                pageChanged = true;
            }

            private void NavigateOwner(OwnerGroup group)
            {
                MenuPage first = group.Pages[0];
                foreach (MenuPage page in group.Pages)
                    if (Settings.LastSubItems.TryGetValue(page.OwnerId, out string remembered) && page.Id == remembered) { first = page; break; }
                Navigate(first);
            }

            private void DrawRailPanel(Rect rect)
            {
                UiPalette palette = ui.Palette;
                ui.Fill(rect, palette.Rail);
                float oldSize = ui.FontSize;
                ui.FontSize = 15f * scale;
                ui.Label(new Rect(rect.x + 22f * scale, rect.y, rect.width - 44f * scale, 64f * scale), T("IM_NativeLibrary"), palette.Ink);
                ui.Fill(new Rect(rect.x, rect.y + 64f * scale, rect.width, 1f), palette.Line);
                Rect list = new Rect(rect.x, rect.y + 82f * scale, rect.width, Mathf.Max(1f, rect.height - 158f * scale));
                railScroll.Draw(list, RailHeight(), drawRail);
                ui.Fill(new Rect(rect.x, rect.yMax - 70f * scale, rect.width, 1f), palette.Line);
                ui.FontSize = 12f * scale;
                if (manage.Draw(new Rect(rect.x + 18f * scale, rect.yMax - 52f * scale, rect.width - 36f * scale, 38f * scale),
                    T("IM_Manage"), variant: UiButtonVariant.Light, icon: UiGlyphKind.Plus, scale: scale))
                {
#if RIMWORLD_1_7
                    mainButtonsOpen = false;
#endif
                    managementOpen = true;
                    navigationOpen = contextOpen = false;
                    ui.Focus(default(UiId));
                }
                ui.FontSize = oldSize;
            }

            private float RailHeight() => 3f * 34f * scale + sidebarOwners.Count * rowHeight + 16f * scale;

            private void DrawRail(Rect rect)
            {
                float oldSize = ui.FontSize;
                float y = 0f;
                for (int category = 0; category < 3; category++)
                {
                    int first = category == 0 ? 0 : category == 1 ? pinnedCount : registeredCount;
                    int end = category == 0 ? pinnedCount : category == 1 ? registeredCount : sidebarOwners.Count;
                    ui.FontSize = 10f * scale;
                    ui.Label(new Rect(22f * scale, y, Mathf.Max(0f, rect.width - 44f * scale), 30f * scale),
                        "// " + T(category == 0 ? "IM_NativePinned" : category == 1 ? "IM_NativeRegistered" : "IM_NativeHosted"), ui.Palette.Marker);
                    y += 34f * scale;
                    for (int i = first; i < end; i++)
                    {
                        sidebarRects[i] = new Rect(0f, y, rect.width, rowHeight);
                        HandleSidebarContext(sidebarRects[i], i);
                        y += rowHeight;
                    }
                }
                int active = -1;
                if (!MainButtonsOpen && !host.browse.ShowResults)
                    for (int i = 0; i < sidebarOwners.Count; i++)
                        if (ReferenceEquals(sidebarOwners[i].Content, host.session.Selected?.Content)) { active = i; break; }
                ui.FontSize = 13f * scale;
                int next = sidebarRail.Draw(new Rect(0f, 0f, rect.width, RailHeight()), active,
                    rowHeight: rowHeight, gap: 0f, icons: sidebarIcons, itemRects: sidebarRects);
                if (next >= 0 && next != active) NavigateOwner(sidebarOwners[next]);
                ui.FontSize = oldSize;
            }

            private void ChangeQuery(string query)
            {
#if RIMWORLD_1_7
                mainButtonsOpen = false;
#endif
                if (host.browse.Query.Length == 0 || host.browse.Managing)
                    host.browse.SetSearchScope(Settings.SearchCurrentMod ? host.session.Selected?.OwnerId : null);
                managementOpen = navigationOpen = contextOpen = false;
                host.browse.Search(query);
                host.resultScroll = Vector2.zero;
                resultsScroll.Reset(); host.catalog.Query(query, host.browse.Scope);
            }

            private int resultStart, resultEnd;
            private void DrawResultArea(Rect rect)
            {
                float scopeWidth = Mathf.Min(260f * scale, rect.width * 0.5f);
                string scopeLabel = host.browse.Scope == null ? T("IM_SearchGlobal") : T("IM_NativeCurrentMod");
                if (scope.Draw(new Rect(rect.x, rect.y, scopeWidth, 36f * scale), scopeLabel))
                {
                    host.browse.SetSearchScope(host.browse.Scope == null ? host.session.Selected?.OwnerId : null);
                    host.catalog.Query(host.browse.Query, host.browse.Scope); resultsScroll.Reset();
                }
                int count = host.catalog.Results.Count;
                int pageCount = Math.Max(1, (count + MenuCatalog.PageSize - 1) / MenuCatalog.PageSize);
                if (!host.catalog.Pending) host.browse.ResultPage = Math.Min(host.browse.ResultPage, pageCount - 1);
                resultStart = host.browse.ResultPage * MenuCatalog.PageSize;
                resultEnd = Math.Min(count, resultStart + MenuCatalog.PageSize);
                if (host.catalog.Pending) searchLoader.Draw(new Rect(rect.x, rect.y + 42f * scale, rect.width, 4f * scale), T("IM_Indexing"), scale: scale);
                Rect body = new Rect(rect.x, rect.y + 50f * scale, rect.width, Mathf.Max(1f, rect.height - 98f * scale));
                if (resultEnd <= resultStart) ui.Label(body, T(host.catalog.Pending ? "IM_Indexing" : "IM_NoResults"), ui.Palette.Muted, TextAnchor.MiddleCenter, true);
                else
                {
                    if (host.restoreResultScroll && !host.catalog.Pending) { resultsScroll.SetPosition(host.resultScroll.y); host.restoreResultScroll = false; }
                    resultsScroll.Draw(body, (resultEnd - resultStart) * 112f * scale, drawResults);
                    if (!host.restoreResultScroll) host.resultScroll = resultsScroll.Position;
                }
                float y = rect.yMax - 36f * scale;
                if (previous.Draw(new Rect(rect.x, y, 110f * scale, 34f * scale), T("IM_Previous"), host.browse.ResultPage > 0))
                { host.browse.ResultPage--; resultsScroll.Reset(); }
                if (next.Draw(new Rect(rect.xMax - 110f * scale, y, 110f * scale, 34f * scale), T("IM_Next"), resultEnd < count))
                { host.browse.ResultPage++; resultsScroll.Reset(); }
                ui.Label(new Rect(rect.x + 118f * scale, y, Mathf.Max(1f, rect.width - 236f * scale), 34f * scale),
                    "IM_ResultPage".Translate(host.browse.ResultPage + 1, pageCount, count) + (host.catalog.Pending ? "  " + T("IM_Indexing") : ""), ui.Palette.Muted, TextAnchor.MiddleCenter);
            }

            private void DrawResults(Rect rect)
            {
                for (int i = resultStart; i < resultEnd; i++)
                {
                    MenuSearchHit hit = host.catalog.Results[i];
                    Rect row = new Rect(0f, (i - resultStart) * 112f * scale, rect.width, 104f * scale);
                    ui.Fill(row, WithAlpha(ui.Palette.Raised, 0.8f));
                    ui.Label(new Rect(16f * scale, row.y + 8f * scale, row.width - 32f * scale, 26f * scale), hit.Path, ui.Palette.Muted);
                    ui.Label(new Rect(16f * scale, row.y + 38f * scale, Mathf.Max(1f, row.width - 164f * scale), 28f * scale), hit.Title, ui.Palette.Ink);
                    ui.Label(new Rect(16f * scale, row.y + 70f * scale, Mathf.Max(1f, row.width - 32f * scale), 24f * scale), hit.Context, ui.Palette.Muted);
                    if (resultButtons[i - resultStart].Draw(new Rect(row.xMax - 120f * scale, row.y + 36f * scale, 104f * scale, 36f * scale), T("IM_NativeOpenResult")) && host.session.Select(hit.Page))
                    {
                        host.browse.OpenTarget(); host.RememberPage(hit.Page);
                        PageState target = GetPage(hit.Page);
                        if (hit.Anchor != null)
                        {
                            try { if (target.Native != null) target.Native.Focus(hit.Anchor); else hit.Page.Focus?.Invoke(hit.Anchor); }
                            catch (Exception exception) { ReportFailure("focus " + hit.Page.Id + "/" + hit.Anchor, exception); }
                        }
                        return;
                    }
                }
            }

            internal float DrawNativeActions(Rect rect)
            {
                var actions = MenuOwnerActions.For(selected.Page);
                float x = rect.x, y = rect.y;
                bool any = false;
                float font = ui.FontSize;
                ui.FontSize = 13f * scale;
                foreach (MenuOwnerAction action in actions)
                {
                    if (!action.IsVisible()) continue;
                    if (!selected.Actions.TryGetValue(action.Id, out UiButton button))
                    { button = new UiButton(ui); selected.Actions.Add(action.Id, button); }
                    string label = action.DisplayLabel();
                    float width = Mathf.Clamp(UiTextMetrics.Width(ui, label) + 80f * scale, 140f * scale, Mathf.Max(140f * scale, rect.width));
                    if (x > rect.x && x + width > rect.xMax) { x = rect.x; y += 44f * scale; }
                    if (button.Draw(new Rect(x, y, Mathf.Min(width, rect.width), 36f * scale), label, action.IsEnabled(),
                        variant: UiButtonVariant.Light, scale: scale)) Activate(action);
                    x += width + 8f * scale; any = true;
                }
                ui.FontSize = font;
                return any ? y - rect.y + 48f * scale : 0f;
            }
        }
    }
}
