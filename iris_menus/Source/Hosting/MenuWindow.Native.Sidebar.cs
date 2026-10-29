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
            private UiDialog sidebarAliasDialog;
            private UiTextField sidebarAliasField;
            private UiButton sidebarAliasSave;
            private Action<Rect> drawSidebarAliasContent;
            private UiId sidebarMenuId, sidebarPreviousFocus;
            private UiId[] sidebarMenuItems;
            private UiTween sidebarMenuOpacity, sidebarMenuEntrance;
            private bool sidebarContextOpen, sidebarAliasOpen, sidebarMenuVisible, sidebarAliasFocus;
            private bool sidebarPreviousFocusVisible;
            private string sidebarContextKey, sidebarAliasTarget;
            private string sidebarAliasValue = string.Empty;
            private Vector2 sidebarMenuScreenAnchor;
            private int sidebarMenuFocused;
            private readonly string[] sidebarActionLabels = new string[5];
            private readonly string[] sidebarActionDisplay = new string[5];
            private object sidebarMenuLanguage;
            private string sidebarMenuHeading;
            private float sidebarMenuScale = -1f, sidebarMenuViewport = -1f, sidebarMenuWidth;
            private bool sidebarMenuPinned;

            private void InitializeSidebarContext()
            {
                sidebarAliasDialog = new UiDialog(ui);
                sidebarAliasField = new UiTextField(ui);
                sidebarAliasSave = new UiButton(ui);
                drawSidebarAliasContent = DrawSidebarAliasContent;
                sidebarMenuId = ui.CreateId();
                sidebarMenuItems = new UiId[5];
                for (int i = 0; i < sidebarMenuItems.Length; i++) sidebarMenuItems[i] = ui.CreateId();
            }

            internal void PrepareSidebarContext()
            {
                float opacity = sidebarMenuOpacity.Step(sidebarContextOpen ? 1f : 0f, ui.Time,
                    UiMotion.Fast, ui.ReducedMotion);
                if (sidebarContextOpen) sidebarMenuEntrance.Step(1f, ui.Time, UiMotion.Enter, ui.ReducedMotion, UiEase.Exit);
                sidebarMenuVisible = sidebarContextOpen || opacity > 0f;
                if (sidebarMenuVisible) ui.SetModal(sidebarMenuId);
                sidebarAliasDialog.Prepare(sidebarAliasOpen);
            }

            internal void DrawSidebarContext()
            {
                if (sidebarMenuVisible || sidebarContextOpen) DrawSidebarMenu();
                if (sidebarAliasDialog.IsVisible) DrawSidebarAliasDialog();
            }

            private void HandleSidebarContext(Rect row, int index)
            {
                Event input = ui.Event;
                if (!ui.IsInputAllowed || input == null || input.type != EventType.MouseDown || input.button != 1 || !ui.Hit(row)) return;
                sidebarPreviousFocus = ui.Focused;
                sidebarPreviousFocusVisible = ui.FocusVisible;
                sidebarContextKey = sidebarKeys[index];
                sidebarMenuScreenAnchor = UiCoordinates.ToScreen(input.mousePosition);
                sidebarMenuOpacity.Snap(0f);
                sidebarMenuEntrance.Snap(0f);
                sidebarMenuOpacity.Step(1f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                sidebarMenuEntrance.Step(1f, ui.Time, UiMotion.Enter, ui.ReducedMotion, UiEase.Exit);
                sidebarContextOpen = sidebarMenuVisible = true;
                sidebarMenuFocused = 0;
                ui.SetModal(sidebarMenuId);
                ui.Focus(sidebarMenuItems[0]);
                ui.Consume();
            }

            private void CloseSidebarMenu()
            {
                sidebarContextOpen = false;
                ui.RestoreFocus(sidebarPreviousFocus, sidebarPreviousFocusVisible);
            }

            private int SidebarContextIndex()
            {
                for (int i = 0; i < sidebarKeys.Length; i++) if (sidebarKeys[i] == sidebarContextKey) return i;
                return -1;
            }

            private bool SidebarMenuEnabled(int action, int index)
            {
                if (action == 2) return index > SidebarCategoryStart(index);
                if (action == 3) return index + 1 < SidebarCategoryEnd(index);
                return action != 4 || (sidebarOwners[index].Hosted && !Settings.HostAllModSettings);
            }

            private int SidebarCategoryStart(int index) => index < pinnedCount ? 0 : index < registeredCount ? pinnedCount : registeredCount;
            private int SidebarCategoryEnd(int index) => index < pinnedCount ? pinnedCount : index < registeredCount ? registeredCount : sidebarOwners.Count;

            private void DrawSidebarMenu()
            {
                int index = SidebarContextIndex();
                if (index < 0) { CloseSidebarMenu(); return; }
                float oldSize = ui.FontSize;
                FontStyle oldStyle = ui.FontStyle;
                ui.FontSize = 14f * scale;
                ui.FontStyle = FontStyle.Normal;
                bool pinned = index < pinnedCount;
                SyncSidebarMenuLabels(pinned);
                float inset = Mathf.Min(8f * scale, viewport.width * 0.5f);
                float width = sidebarMenuWidth;
                float unit = Mathf.Min(scale, Mathf.Max(0.1f, (viewport.height - 2f * inset) / 230f));
                float row = 36f * unit;
                float heading = 32f * unit;
                float height = heading + row * 5f + 18f * unit;
                Vector2 anchor = UiCoordinates.FromScreen(sidebarMenuScreenAnchor);
                bool above = anchor.y + height > viewport.height - inset;
                float y = above ? anchor.y - height : anchor.y;
                float shift = sidebarContextOpen ? (1f - sidebarMenuEntrance.Value) * 4f * unit * (above ? 1f : -1f) : 0f;
                Rect panel = new Rect(Mathf.Clamp(anchor.x, inset, Mathf.Max(inset, viewport.width - width - inset)),
                    Mathf.Clamp(y, inset, Mathf.Max(inset, viewport.height - height - inset)) + shift, width, height);
                try
                {
                using (ui.Layer(sidebarMenuId))
                using (ui.Input(sidebarContextOpen))
                {
                    Event input = ui.Event;
                    if (ui.IsInputAllowed && input != null && input.type == EventType.MouseDown && !panel.Contains(input.mousePosition))
                    { CloseSidebarMenu(); ui.Consume(); return; }
                    if (ui.ConsumeKey(KeyCode.Escape)) { CloseSidebarMenu(); return; }
                    int direction = ui.ConsumeKey(KeyCode.UpArrow) ? -1 : ui.ConsumeKey(KeyCode.DownArrow) ? 1 : 0;
                    if (direction != 0)
                    {
                        for (int step = 0; step < 5; step++)
                        {
                            sidebarMenuFocused = (sidebarMenuFocused + direction + 5) % 5;
                            if (SidebarMenuEnabled(sidebarMenuFocused, index)) break;
                        }
                        ui.Focus(sidebarMenuItems[sidebarMenuFocused]);
                    }
                    if (ui.ConsumeKey(KeyCode.Home)) { sidebarMenuFocused = 0; ui.Focus(sidebarMenuItems[0]); }
                    if (ui.ConsumeKey(KeyCode.End))
                    {
                        sidebarMenuFocused = 4;
                        while (!SidebarMenuEnabled(sidebarMenuFocused, index)) sidebarMenuFocused--;
                        ui.Focus(sidebarMenuItems[sidebarMenuFocused]);
                    }
                    using (ui.Opacity(sidebarMenuOpacity.Value))
                    {
                        UiPalette palette = ui.Palette;
                        for (int edge = 8; edge > 0; edge--)
                        {
                            Rect shadow = panel;
                            shadow.xMin -= edge * unit; shadow.xMax += edge * unit;
                            shadow.yMin -= edge * unit; shadow.yMax += edge * unit;
                            ui.Border(shadow, new Color(0f, 0f, 0f, 0.032f * (1f - edge / 9f)), unit);
                        }
                        ui.Fill(panel, palette.MenuSurface);
                        ui.Border(panel, palette.MenuLine);
                        ui.FontSize = 12f * unit;
                        ui.Label(new Rect(panel.x + 12f * unit, panel.y + 4f * unit, Mathf.Max(0f, panel.width - 24f * unit), heading),
                            sidebarMenuHeading, palette.MenuMuted);
                        ui.FontSize = 14f * unit;
                        for (int action = 0; action < 5; action++)
                        {
                            string label = sidebarActionDisplay[action];
                            Rect item = new Rect(panel.x + unit, panel.y + heading + 4f * unit + action * row + (action == 4 ? 9f * unit : 0f),
                                panel.width - 2f * unit, row);
                            bool enabled = SidebarMenuEnabled(action, index);
                            UiInteraction interaction = ui.Interact(sidebarMenuItems[action], item, enabled);
                            bool focused = ui.IsFocused(sidebarMenuItems[action]);
                            if (enabled && (interaction.Hovered || focused)) ui.Fill(item, palette.MenuHover);
                            if (enabled && focused && ui.FocusVisible) ui.Border(item.ContractedBy(2f * unit), palette.MenuFocus);
                            if (action == 4) ui.Fill(new Rect(panel.x + unit, item.y - 5f * unit, panel.width - 2f * unit, unit), palette.MenuLine);
                            Color ink = !enabled ? palette.MenuDisabled : action == 4 ? palette.MenuDanger : palette.MenuInk;
                            UiGlyphKind glyph = action == 0 ? pinned ? UiGlyphKind.Unpin : UiGlyphKind.Pin :
                                action == 1 ? UiGlyphKind.Edit : action == 2 ? UiGlyphKind.ArrowUp : action == 3 ? UiGlyphKind.ArrowDown : UiGlyphKind.Trash;
                            UiGlyph.Draw(ui, new Rect(item.x + 12f * unit, item.center.y - 8f * unit, 16f * unit, 16f * unit), glyph, ink);
                            Rect text = new Rect(item.x + 36f * unit, item.y, Mathf.Max(0f, item.width - 48f * unit), item.height);
                            ui.Label(text, label, ink);
                            if (interaction.Clicked) { ApplySidebarAction(action, index); break; }
                        }
                        float entry = sidebarMenuEntrance.Value;
                        ui.Fill(new Rect(panel.x, panel.y, panel.width * 0.5f * entry, 3f * unit), palette.MenuAction);
                        ui.Triangle(new Vector2(panel.x + 28f * unit * entry, panel.y + 3f * unit),
                            new Vector2(panel.x + 36f * unit * entry, panel.y + 3f * unit),
                            new Vector2(panel.x + 32f * unit * entry, panel.y + 7f * unit), palette.MenuAction);
                    }
                }
                }
                finally { ui.FontSize = oldSize; ui.FontStyle = oldStyle; }
            }

            private static string SidebarActionLabel(int action, bool pinned) =>
                T(action == 0 ? pinned ? "IM_NativeUnpin" : "IM_NativePin" : action == 1 ? "IM_NativeAlias" :
                    action == 2 ? "IM_NativeMoveUp" : action == 3 ? "IM_NativeMoveDown" : "IM_NativeRemove");

            private void SyncSidebarMenuLabels(bool pinned)
            {
                if (ReferenceEquals(sidebarMenuLanguage, host.catalogLanguage) && sidebarMenuScale == scale &&
                    sidebarMenuViewport == viewport.width && sidebarMenuPinned == pinned) return;
                sidebarMenuLanguage = host.catalogLanguage;
                sidebarMenuScale = scale;
                sidebarMenuViewport = viewport.width;
                sidebarMenuPinned = pinned;
                sidebarMenuHeading = T("IM_NativeSidebarActions");
                float width = 200f * scale;
                for (int i = 0; i < sidebarActionLabels.Length; i++)
                {
                    sidebarActionLabels[i] = SidebarActionLabel(i, pinned);
                    width = Mathf.Max(width, UiTextMetrics.Width(ui, sidebarActionLabels[i]) + 64f * scale);
                }
                sidebarMenuWidth = Mathf.Min(width, Mathf.Max(1f, viewport.width - Mathf.Min(16f * scale, viewport.width)));
                for (int i = 0; i < sidebarActionLabels.Length; i++)
                    sidebarActionDisplay[i] = UiTextMetrics.Ellipsize(ui, sidebarActionLabels[i], Mathf.Max(0f, sidebarMenuWidth - 50f * scale));
            }

            private void ApplySidebarAction(int action, int index)
            {
                if (!SidebarMenuEnabled(action, index)) return;
                OwnerGroup group = sidebarOwners[index];
                string key = sidebarKeys[index];
                CloseSidebarMenu();
                if (action == 1)
                {
                    sidebarAliasTarget = key;
                    sidebarAliasValue = Settings.SidebarAliases.TryGetValue(key, out string alias) ? alias : group.Name;
                    sidebarAliasOpen = sidebarAliasFocus = true;
                    sidebarAliasDialog.Prepare(true);
                    return;
                }
                if (action == 0)
                {
                    if (index < pinnedCount) Settings.SidebarPins.RemoveAll(value => value == key);
                    else Settings.SidebarPins.Add(key);
                }
                else if (action == 2 || action == 3)
                {
                    int next = index + (action == 2 ? -1 : 1);
                    List<string> order = index < pinnedCount ? Settings.SidebarPins : Settings.SidebarOrder;
                    if (index < pinnedCount)
                    {
                        int from = order.IndexOf(key), to = order.IndexOf(sidebarKeys[next]);
                        order[from] = sidebarKeys[next]; order[to] = key;
                    }
                    else
                    {
                        var category = new List<string>();
                        for (int i = SidebarCategoryStart(index); i < SidebarCategoryEnd(index); i++) category.Add(sidebarKeys[i]);
                        int from = index - SidebarCategoryStart(index), to = next - SidebarCategoryStart(index);
                        category[from] = sidebarKeys[next]; category[to] = key;
                        foreach (string member in category) order.RemoveAll(value => value == member);
                        order.AddRange(category);
                    }
                }
                else if (action == 4)
                {
                    bool selectedOwner = ReferenceEquals(host.session.Selected?.Content, group.Content);
                    if (selectedOwner && !host.session.Select(null)) return;
                    foreach (MenuPage page in group.Pages) Settings.EnabledAdapters.RemoveAll(id => id == page.Id);
                    if (selectedOwner) host.browse.Navigate(null);
                }
                SidebarSettingsChanged();
            }

            private void SidebarSettingsChanged()
            {
                host.catalogRevision = -1;
                revision = int.MinValue;
                MenuRegistry.InvalidateSearchIndex();
                IrisMenusMod.Instance.WriteSettings();
            }

            private void DrawSidebarAliasDialog()
            {
                if (sidebarAliasDialog.Draw(viewport, new Vector2(520f * scale, 300f * scale), T("IM_NativeAliasTitle"), drawSidebarAliasContent, true))
                    sidebarAliasOpen = false;
            }

            private void DrawSidebarAliasContent(Rect rect)
            {
                ui.Label(new Rect(0f, 0f, rect.width, 40f * scale), T("IM_NativeAliasDescription"), ui.Palette.Muted, TextAnchor.UpperLeft, true);
                if (sidebarAliasFocus && ui.IsInputAllowed) { ui.Focus(sidebarAliasField.Id); sidebarAliasFocus = false; }
                sidebarAliasValue = sidebarAliasField.Draw(new Rect(0f, 48f * scale, rect.width, 38f * scale), sidebarAliasValue, T("IM_NativeAliasPlaceholder"), scale: scale);
                float width = Mathf.Min(130f * scale, rect.width);
                if (sidebarAliasSave.Draw(new Rect(rect.width - width, rect.height - 38f * scale, width, 34f * scale), T("IM_NativeSave"), primary: true, scale: scale) || sidebarAliasField.Submitted)
                {
                    string value = (sidebarAliasValue ?? string.Empty).Trim();
                    if (value.Length == 0) Settings.SidebarAliases.Remove(sidebarAliasTarget);
                    else Settings.SidebarAliases[sidebarAliasTarget] = value;
                    sidebarAliasOpen = false;
                    SidebarSettingsChanged();
                }
            }
        }
    }
}
