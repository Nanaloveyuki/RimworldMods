using System;
using UnityEngine;
using Verse;
using IrisMenus.UI;

namespace IrisMenus
{
    public sealed partial class MenuWindow
    {
        private sealed partial class NativeSurface
        {
            private readonly UiButton navigationToggle;
            private readonly Action<Rect> drawManagementRows;
            private readonly Action<Rect> drawContextContents;
            private readonly Action<Rect> drawRailPanel;

            private void DrawOverlays()
            {
                if (navigationDrawer.IsVisible && navigationDrawer.Draw(viewport, Mathf.Min(320f * scale, viewport.width), T("IM_NativeLibrary"), drawRailPanel, UiDrawerSide.Left)) navigationOpen = false;
                if (contextDrawer.IsVisible && contextDrawer.Draw(viewport, Mathf.Min(380f * scale, viewport.width), T("IM_NativeThisPage"), drawContext)) contextOpen = false;
                if (managementDialog.IsVisible && managementDialog.Draw(viewport, new Vector2(620f * scale, 560f * scale), T("IM_Manage"), drawManagement, true)) managementOpen = false;
                if (resetDialog.IsVisible && resetDialog.Draw(viewport, new Vector2(520f * scale, 280f * scale), T("IM_NativeResetTitle"), drawReset)) resetOpen = false;
                if (helpDialog.IsVisible && helpDialog.Draw(viewport, new Vector2(600f * scale, 400f * scale), "IrisMenus", drawHelp, true)) helpOpen = false;
            }

            private void DrawContextPanel(Rect rect)
            {
                ui.Fill(rect, WithAlpha(ui.Palette.Surface, 0.94f));
                DrawContext(rect.ContractedBy(14f * scale));
            }

            private void DrawContext(Rect rect)
            {
                if (selected?.Native == null) return;
                float height = 40f * scale + selected.Chapters.Length * 42f * scale;
                foreach (NoteState note in selected.Notes) height += note.Accordion.Measure(note.Open, note.Height(rect.width - 20f), 42f * scale);
                if (selected.Native.HasPreview) height += 420f * scale;
                contextScroll.Draw(rect, height, drawContextContents);
            }

            private void DrawContextContents(Rect rect)
            {
                if (selected?.Native == null) return;
                float y = 0f;
                ui.Label(new Rect(0f, y, rect.width, 30f * scale), T("IM_NativeThisPage"), ui.Palette.Muted);
                y += 38f * scale;
                for (int i = 0; i < selected.Chapters.Length; i++)
                {
                    MenuChapter chapter = selected.Native.Chapters[i];
                    if (selected.Chapters[i].Draw(new Rect(0f, y, rect.width, 36f * scale), chapter.Title() ?? ""))
                    {
                        try { selected.Native.Focus(chapter.Id); contextOpen = false; }
                        catch (Exception exception) { ReportFailure("focus native " + selected.Page.Id + "/" + chapter.Id, exception); }
                    }
                    y += 42f * scale;
                }
                foreach (NoteState note in selected.Notes)
                {
                    float contentHeight = note.Height(rect.width);
                    float height = note.Accordion.Measure(note.Open, contentHeight, 42f * scale);
                    note.Open = note.Accordion.Draw(new Rect(0f, y, rect.width, height), note.Title, note.Open, contentHeight, note.Draw, headerHeight: 42f * scale);
                    y += height + 8f * scale;
                }
                if (selected.Native.HasPreview) selected.Native.DrawPreview(new Rect(0f, y + 16f * scale, rect.width, 400f * scale));
            }

            private void DrawManagement(Rect rect)
            {
                Rect list = new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 54f * scale));
                managementScroll.Draw(list, (host.adapters.Count + registeredOwners.Count + 1) * rowHeight, drawManagementRows);
                if (done.Draw(new Rect(Mathf.Max(0f, rect.width - 140f * scale), rect.height - 40f * scale, Mathf.Min(140f * scale, rect.width), 36f * scale), T("IM_NativeDone"), primary: true)) managementOpen = false;
            }

            private void DrawManagementRows(Rect rect)
            {
                float y = 0f;
                foreach (OwnerGroup group in registeredOwners)
                {
                    ui.Label(new Rect(0f, y, rect.width, rowHeight), group.Name + "  ·  " + T("IM_NativeRegistered"), ui.Palette.Muted);
                    y += rowHeight;
                }
                foreach (MenuPage page in host.adapters)
                {
                    if (MenuRegistry.HasPages(page.Owner)) continue;
                    bool enabled = Settings.EnabledAdapters.Contains(page.Id);
                    bool next = adapterChecks[page.Id].Draw(new Rect(0f, y, rect.width, rowHeight), enabled, page.DisplayModName);
                    if (next != enabled)
                    {
                        if (!next && host.session.Selected?.Id == page.Id && !host.session.Select(null)) return;
                        if (next) Settings.EnabledAdapters.Add(page.Id);
                        else Settings.EnabledAdapters.RemoveAll(id => id == page.Id);
                        if (!next && host.session.Selected == null) host.browse.Navigate(null);
                        host.catalogRevision = -1;
                    }
                    y += rowHeight;
                }
                if (y == 0f) ui.Label(rect, T("IM_Empty"), ui.Palette.Muted);
            }

            private void DrawReset(Rect rect)
            {
                ui.Label(new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 60f * scale)), T("IM_NativeResetDescription"), ui.Palette.Ink, TextAnchor.UpperLeft, true);
                float width = Mathf.Min(150f * scale, (rect.width - 12f * scale) * 0.5f);
                if (cancelReset.Draw(new Rect(rect.width - width * 2f - 12f * scale, rect.height - 40f * scale, width, 36f * scale), T("CancelButton"))) resetOpen = false;
                if (confirmReset.Draw(new Rect(rect.width - width, rect.height - 40f * scale, width, 36f * scale), T("IM_NativeReset"),
                    ReferenceEquals(resetTarget, selected) && resetTarget?.Native?.CanReset == true, true))
                {
                    try { resetTarget.Native.Reset(); resetOpen = false; resetTarget = null; }
                    catch (Exception exception) { ReportFailure("reset native " + selected.Page.Id, exception); }
                }
            }

            private void DrawHelp(Rect rect)
            {
                ui.Label(new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 116f * scale)), T("IM_NativeHelpDescription"), ui.Palette.Ink, TextAnchor.UpperLeft, true);
                ui.Label(new Rect(0f, rect.height - 112f * scale, rect.width, 32f * scale), "Ctrl K  ·  " + T("IM_SearchGlobal") + "     Esc  ·  " + T("Back"), ui.Palette.Muted);
                if (license.Draw(new Rect(0f, rect.height - 74f * scale, rect.width, 32f * scale), "endfield-ui · Apache-2.0")) Application.OpenURL("https://github.com/icaruszezen/endfield-ui");
                if (closeHelp.Draw(new Rect(Mathf.Max(0f, rect.width - 140f * scale), rect.height - 36f * scale, Mathf.Min(rect.width, 140f * scale), 32f * scale), T("IM_NativeDone"), primary: true)) helpOpen = false;
            }
        }
    }
}
