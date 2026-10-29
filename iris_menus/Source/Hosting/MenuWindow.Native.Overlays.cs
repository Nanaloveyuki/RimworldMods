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
            private readonly Action<Rect> drawContextContents;
            private readonly Action<Rect> drawRailPanel;

            private void DrawOverlays()
            {
                if (navigationDrawer.IsVisible && navigationDrawer.Draw(viewport, Mathf.Min(320f * scale, viewport.width), T("IM_NativeLibrary"), drawRailPanel, UiDrawerSide.Left)) navigationOpen = false;
                if (contextDrawer.IsVisible && contextDrawer.Draw(viewport, Mathf.Min(380f * scale, viewport.width), T("IM_NativeThisPage"), drawContext)) contextOpen = false;
                if (resetDialog.IsVisible && resetDialog.Draw(viewport, new Vector2(520f * scale, 280f * scale), T("IM_NativeResetTitle"), drawReset)) resetOpen = false;
                if (helpDialog.IsVisible && helpDialog.Draw(viewport, new Vector2(600f * scale, 400f * scale), "IrisMenus", drawHelp, true)) helpOpen = false;
            }

            private void DrawContextPanel(Rect rect) => DrawContext(rect);

            private void DrawContext(Rect rect)
            {
                if (selected?.Native == null) return;
                float oldSize = ui.FontSize;
                ui.FontSize = 12f * scale;
                float contentWidth = Mathf.Max(1f, rect.width - 20f);
                float height = selected.Chapters.Length > 0 ? 38f * scale + selected.Chapters.Length * 40f * scale + 30f * scale : 0f;
                if (selected.Notes.Length > 0) height += 34f * scale;
                foreach (NoteState note in selected.Notes) height += note.Accordion.Measure(note.Open, note.Height(contentWidth, scale), 42f * scale);
                if (selected.Native.HasPreview) height += 432f * scale;
                contextScroll.Draw(rect, height, drawContextContents);
                ui.FontSize = oldSize;
            }

            private void DrawContextContents(Rect rect)
            {
                if (selected?.Native == null) return;
                UiPalette palette = ui.Palette;
                float y = 0f;
                if (selected.Chapters.Length > 0)
                {
                    ui.FontSize = 11f * scale;
                    ui.Label(new Rect(16f * scale, y, Mathf.Max(0f, rect.width - 16f * scale), 30f * scale), "// " + T("IM_NativeThisPage"), palette.Muted);
                    y += 38f * scale;
                    ui.Fill(new Rect(0f, y, 1f, selected.Chapters.Length * 40f * scale), palette.Line);
                    string active = selected.Native.ActiveChapterId;
                    int activeIndex = -1;
                    for (int i = 0; i < selected.Chapters.Length; i++)
                        if (selected.Native.Chapters[i].Id == active) { activeIndex = i; break; }
                    float markerY = selected.ChapterMarker.Step(y + Mathf.Max(0, activeIndex) * 40f * scale + 10f * scale,
                        ui.Time, UiMotion.Standard, ui.ReducedMotion);
                    if (activeIndex >= 0) ui.Fill(new Rect(0f, markerY, 3f * scale, 20f * scale), palette.Ink);
                    ui.FontSize = 13f * scale;
                    for (int i = 0; i < selected.Chapters.Length; i++)
                    {
                        MenuChapter chapter = selected.Native.Chapters[i];
                        Rect row = new Rect(0f, y, rect.width, 40f * scale);
                        UiInteraction interaction = ui.Interact(selected.Chapters[i].Id, row);
                        float inkAmount = selected.ChapterInk[i].Step(activeIndex == i || interaction.Hovered || interaction.Pressed ? 1f : 0f,
                            ui.Time, UiMotion.Fast, ui.ReducedMotion);
                        ui.Label(new Rect(16f * scale, y, Mathf.Max(0f, rect.width - 20f * scale), row.height), chapter.Title() ?? "",
                            Color.Lerp(palette.Muted, palette.Ink, inkAmount));
                        if (interaction.Focused && ui.FocusVisible) ui.Border(row.ContractedBy(2f), palette.Ink);
                        if (interaction.Clicked)
                        {
                            try { selected.Native.Focus(chapter.Id); contextOpen = false; }
                            catch (Exception exception) { ReportFailure("focus native " + selected.Page.Id + "/" + chapter.Id, exception); }
                        }
                        y += row.height;
                    }
                    y += 30f * scale;
                }
                if (selected.Notes.Length > 0)
                {
                    ui.FontSize = 11f * scale;
                    ui.Label(new Rect(0f, y, rect.width, 22f * scale), T("IM_NativeNotes"), palette.Muted);
                    y += 34f * scale;
                    ui.FontSize = 12f * scale;
                    foreach (NoteState note in selected.Notes)
                    {
                        float contentHeight = note.Height(rect.width, scale);
                        float height = note.Accordion.Measure(note.Open, contentHeight, 42f * scale);
                        bool next = note.Accordion.Draw(new Rect(0f, y, rect.width, height), note.Title, note.Open, contentHeight, note.Draw, headerHeight: 42f * scale);
                        if (next && !note.Open)
                            foreach (NoteState sibling in selected.Notes) if (!ReferenceEquals(sibling, note)) sibling.Open = false;
                        note.Open = next;
                        y += height;
                    }
                }
                if (selected.Native.HasPreview) selected.Native.DrawPreview(new Rect(0f, y + 32f * scale, rect.width, 400f * scale));
            }


            private void DrawReset(Rect rect)
            {
                ui.Label(new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 60f * scale)), T("IM_NativeResetDescription"), ui.Palette.Ink, TextAnchor.UpperLeft, true);
                float width = Mathf.Min(150f * scale, (rect.width - 12f * scale) * 0.5f);
                ui.Fill(new Rect(0f, rect.height - 50f * scale, rect.width, 1f), ui.Palette.Line);
                if (cancelReset.Draw(new Rect(rect.width - width * 2f - 12f * scale, rect.height - 40f * scale, width, 36f * scale), T("CancelButton"), scale: scale)) resetOpen = false;
                if (confirmReset.Draw(new Rect(rect.width - width, rect.height - 40f * scale, width, 36f * scale), T("IM_NativeReset"),
                    ReferenceEquals(resetTarget, selected) && resetTarget?.Native?.CanReset == true, true, scale: scale))
                {
                    try { resetTarget.Native.Reset(); RecordEdit(); resetOpen = false; resetTarget = null; }
                    catch (Exception exception) { ReportFailure("reset native " + selected.Page.Id, exception); }
                }
            }

            private void DrawHelp(Rect rect)
            {
                ui.Label(new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 78f * scale)), T("IM_NativeHelpDescription"), ui.Palette.Ink, TextAnchor.UpperLeft, true);
                ui.Label(new Rect(0f, rect.height - 74f * scale, rect.width, 32f * scale), "Ctrl K  ·  " + T("IM_SearchGlobal") + "     Esc  ·  " + T("Back"), ui.Palette.Muted);
                ui.Fill(new Rect(0f, rect.height - 44f * scale, rect.width, 1f), ui.Palette.Line);
                if (closeHelp.Draw(new Rect(Mathf.Max(0f, rect.width - 140f * scale), rect.height - 36f * scale, Mathf.Min(rect.width, 140f * scale), 32f * scale), T("IM_NativeDone"), primary: true, scale: scale)) helpOpen = false;
            }
        }
    }
}
