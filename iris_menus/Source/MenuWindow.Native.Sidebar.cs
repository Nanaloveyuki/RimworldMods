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
            private UiTween sidebarMenuOpacity;
            private bool sidebarContextOpen, sidebarAliasOpen, sidebarMenuVisible, sidebarAliasFocus;
            private bool sidebarPreviousFocusVisible;
            private string sidebarContextKey, sidebarAliasTarget;
            private string sidebarAliasValue = string.Empty;
            private Vector2 sidebarMenuScreenAnchor;
            private int sidebarMenuFocused;

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
                    sidebarContextOpen ? UiMotion.Enter : UiMotion.Exit, ui.ReducedMotion);
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
                return action != 4 || sidebarOwners[index].Hosted;
            }

            private int SidebarCategoryStart(int index) => index < pinnedCount ? 0 : index < registeredCount ? pinnedCount : registeredCount;
            private int SidebarCategoryEnd(int index) => index < pinnedCount ? pinnedCount : index < registeredCount ? registeredCount : sidebarOwners.Count;

            private void DrawSidebarMenu()
            {
                int index = SidebarContextIndex();
                if (index < 0) { CloseSidebarMenu(); return; }
                float inset = Mathf.Min(8f * scale, viewport.width * 0.5f);
                float width = Mathf.Min(250f * scale, Mathf.Max(1f, viewport.width - 2f * inset));
                float row = Mathf.Min(32f * scale, Mathf.Max(1f, (viewport.height - 16f * scale) / 5f));
                float height = row * 5f + 8f * scale;
                Vector2 anchor = UiCoordinates.FromScreen(sidebarMenuScreenAnchor);
                Rect panel = new Rect(Mathf.Clamp(anchor.x, inset, Mathf.Max(inset, viewport.width - width - inset)),
                    Mathf.Clamp(anchor.y, 0f, Mathf.Max(0f, viewport.height - height)), width, height);
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
                        Rect shadow = panel; shadow.position += new Vector2(0f, 4f * scale);
                        ui.Fill(shadow, ui.Palette.Shadow);
                        ui.Fill(panel, ui.Palette.Overlay);
                        ui.Border(panel, ui.Palette.Line);
                        bool pinned = index < pinnedCount;
                        for (int action = 0; action < 5; action++)
                        {
                            string label = T(action == 0 ? pinned ? "IM_NativeUnpin" : "IM_NativePin" : action == 1 ? "IM_NativeAlias" :
                                action == 2 ? "IM_NativeMoveUp" : action == 3 ? "IM_NativeMoveDown" : "IM_NativeRemove");
                            Rect item = new Rect(panel.x + scale, panel.y + 4f * scale + action * row, panel.width - 2f * scale, row);
                            bool enabled = SidebarMenuEnabled(action, index);
                            UiInteraction interaction = ui.Interact(sidebarMenuItems[action], item, enabled);
                            bool focused = ui.IsFocused(sidebarMenuItems[action]);
                            if (interaction.Hovered || focused)
                            {
                                ui.Fill(item, ui.Palette.Hover);
                                ui.Fill(new Rect(item.x, item.y, 3f * scale, item.height), ui.Palette.Ink);
                            }
                            if (action == 4) ui.Fill(new Rect(item.x + 8f * scale, item.y, Mathf.Max(0f, item.width - 16f * scale), scale), ui.Palette.Line);
                            using (ui.Opacity(enabled ? 1f : 0.42f))
                                ui.Label(new Rect(item.x + 12f * scale, item.y, Mathf.Max(0f, item.width - 24f * scale), item.height), label, ui.Palette.Ink);
                            if (interaction.Clicked) { ApplySidebarAction(action, index); break; }
                        }
                    }
                }
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
                    if (selectedOwner) { host.browse.Navigate(null); host.RebuildNavigation(); }
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
