using System;
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
                internal string Name => Pages[0].DisplayModName;
                internal bool Hosted => Pages[0].Adapted;
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
                internal UiTabs Tabs;
                internal UiSelect TabSelect;
                internal int TabIndex;
                internal UiButton[] Chapters;
                internal NoteState[] Notes;
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
                            for (int i = 0; i < Chapters.Length; i++) Chapters[i] = new UiButton(ui);
                            Notes = new NoteState[Native.Notes.Count];
                            for (int i = 0; i < Notes.Length; i++) Notes[i] = new NoteState(Native.Notes[i], ui, i == 0);
                        }
                    }
                    catch (Exception exception)
                    {
                        Dispose();
                        Chapters = Array.Empty<UiButton>(); Notes = Array.Empty<NoteState>();
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
                internal float Height(float width) => Mathf.Max(60f, Text.CalcHeight(note.Text() ?? "", Mathf.Max(1f, width - 28f)) + 28f);
                internal NoteState(MenuNote note, UiContext ui, bool open)
                {
                    this.note = note; this.ui = ui; Open = open;
                    Accordion = new UiAccordion(ui, open);
                    Draw = DrawText;
                }
                private void DrawText(Rect rect) => ui.Label(rect.ContractedBy(14f), note.Text() ?? "", ui.Palette.Muted, TextAnchor.UpperLeft, true);
            }

            private void SyncCatalog()
            {
                if (revision == host.catalogRevision && ReferenceEquals(language, host.catalogLanguage)) return;
                revision = host.catalogRevision; language = host.catalogLanguage;
                var valid = new HashSet<string>(StringComparer.Ordinal);
                owners.Clear(); registeredOwners.Clear(); hostedOwners.Clear();
                foreach (MenuPage page in host.pages)
                {
                    valid.Add(page.Id);
                    OwnerGroup group = null;
                    foreach (OwnerGroup candidate in owners) if (ReferenceEquals(candidate.Content, page.Content)) { group = candidate; break; }
                    if (group == null) { group = new OwnerGroup { Content = page.Content }; owners.Add(group); }
                    group.Pages.Add(page);
                    if (states.TryGetValue(page.Id, out PageState previous) &&
                        (!ReferenceEquals(previous.Page, page) || !ReferenceEquals(previous.Presentation, MenuRegistry.PresentationFor(page))))
                    { previous.Dispose(); states.Remove(page.Id); }
                }
                var removed = new List<string>();
                foreach (var pair in states) if (!valid.Contains(pair.Key)) { pair.Value.Dispose(); removed.Add(pair.Key); }
                foreach (string id in removed) states.Remove(id);
                foreach (OwnerGroup group in owners) (group.Hosted ? hostedOwners : registeredOwners).Add(group);
                registeredLabels = new string[registeredOwners.Count]; hostedLabels = new string[hostedOwners.Count];
                for (int i = 0; i < registeredLabels.Length; i++) registeredLabels[i] = registeredOwners[i].Name;
                for (int i = 0; i < hostedLabels.Length; i++) hostedLabels[i] = hostedOwners[i].Name;
                registeredRail = new UiSideRail(ui, registeredLabels); hostedRail = new UiSideRail(ui, hostedLabels);
                foreach (PageState state in states.Values) BindTabs(state);
                foreach (MenuPage adapter in host.adapters)
                    if (!adapterChecks.ContainsKey(adapter.Id)) adapterChecks.Add(adapter.Id, new UiCheckbox(ui));
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
                foreach (OwnerGroup group in owners)
                {
                    if (!ReferenceEquals(group.Content, state.Page.Content)) continue;
                    state.Group = group;
                    var labels = new List<string>();
                    foreach (MenuPage page in group.Pages)
                        if (!page.ActionLauncher) labels.Add(page.DisplayTitle);
                    if (labels.Count == 0) { state.Tabs = null; state.TabSelect = null; return; }
                    state.Tabs = new UiTabs(ui, labels.ToArray());
                    state.TabSelect = new UiSelect(ui, labels.ToArray());
                    int index = 0;
                    state.TabIndex = -1;
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
                if (selected?.Tabs == null) return;
                float width = Mathf.Max(1f, main.width - 108f * scale);
                int index = selected.TabIndex;
                Rect rect = new Rect(main.x, main.y, Mathf.Min(width, selected.Tabs.Count * 180f * scale), 44f * scale);
                if (selected.Tabs.Count * 120f * scale <= width) index = selected.Tabs.Draw(rect, index);
                else index = selected.TabSelect.Draw(new Rect(rect.x, rect.y, Mathf.Min(340f * scale, width), rect.height), index);
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
                MenuPage old = host.session.Selected;
                host.Select(page);
                if (!ReferenceEquals(host.session.Selected, page)) return;
                navigationOpen = contextOpen = false;
                pageChanged = true;
                if (!ReferenceEquals(old, page)) selected?.TabSelect?.Close();
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
                ui.Fill(rect, WithAlpha(ui.Palette.Surface, 0.52f));
                ui.Label(new Rect(rect.x + 20f * scale, rect.y, rect.width - 40f * scale, 52f * scale), T("IM_NativeLibrary"), ui.Palette.Ink);
                ui.Fill(new Rect(rect.x, rect.y + 52f * scale, rect.width, 1f), ui.Palette.Line);
                Rect list = new Rect(rect.x + 8f * scale, rect.y + 66f * scale, rect.width - 16f * scale, Mathf.Max(1f, rect.height - 130f * scale));
                railScroll.Draw(list, RailHeight(), drawRail);
                if (manage.Draw(new Rect(rect.x + 18f * scale, rect.yMax - 52f * scale, rect.width - 36f * scale, 38f * scale), T("IM_Manage"))) managementOpen = true;
            }

            private float RailHeight() => 76f * scale + registeredRail.NaturalHeight(rowHeight) + hostedRail.NaturalHeight(rowHeight);

            private void DrawRail(Rect rect)
            {
                float y = 0f;
                DrawRailGroup(registeredOwners, registeredRail, T("IM_NativeRegistered"), ref y, rect.width);
                DrawRailGroup(hostedOwners, hostedRail, T("IM_NativeHosted"), ref y, rect.width);
            }

            private void DrawRailGroup(List<OwnerGroup> groups, UiSideRail rail, string label, ref float y, float width)
            {
                if (groups.Count == 0) return;
                ui.Label(new Rect(14f * scale, y, width - 28f * scale, 30f * scale), label, ui.Palette.Muted);
                y += 34f * scale;
                int active = -1;
                if (!host.browse.ShowResults)
                    for (int i = 0; i < groups.Count; i++) if (ReferenceEquals(groups[i].Content, host.session.Selected?.Content)) active = i;
                float height = rail.NaturalHeight(rowHeight);
                int next = rail.Draw(new Rect(0f, y, width, height), active, rowHeight: rowHeight);
                if (next >= 0 && next != active) NavigateOwner(groups[next]);
                y += height + 12f * scale;
            }

            private void ChangeQuery(string query)
            {
                if (host.browse.Query.Length == 0 && query.Length > 0 && !host.browse.ViewingTarget)
                    host.browse.SetSearchScope(Settings.SearchCurrentMod ? host.session.Selected?.OwnerId : null);
                host.browse.Search(query);
                host.resultScroll = host.adapterScroll = Vector2.zero;
                resultsScroll.Reset(); host.catalog.Query(query, host.browse.Scope); host.RebuildNavigation();
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

            private float DrawNativeActions(Rect rect)
            {
                var actions = MenuOwnerActions.For(selected.Page);
                float x = rect.x, y = rect.y;
                bool any = false;
                foreach (MenuOwnerAction action in actions)
                {
                    if (!action.IsVisible()) continue;
                    if (!selected.Actions.TryGetValue(action.Id, out UiButton button))
                    { button = new UiButton(ui); selected.Actions.Add(action.Id, button); }
                    string label = action.DisplayLabel();
                    float width = Mathf.Clamp(Text.CalcSize(label).x + 24f * scale, 100f * scale, Mathf.Max(100f * scale, rect.width));
                    if (x > rect.x && x + width > rect.xMax) { x = rect.x; y += 44f * scale; }
                    if (button.Draw(new Rect(x, y, Mathf.Min(width, rect.width), 36f * scale), label, action.IsEnabled())) Activate(action);
                    x += width + 8f * scale; any = true;
                }
                return any ? y - rect.y + 48f * scale : 0f;
            }
        }
    }
}
