using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using IrisMenus.UI;

namespace IrisMenus
{
    public sealed partial class MenuWindow
    {
        private sealed partial class NativeSurface
        {
            private sealed class ManagementEntry
            {
                internal readonly UiCheckbox Visibility;
                internal ModContentPack Content;
                internal MenuPage Adapter;
                internal string Name, Metadata;
                internal float Y, Height, TitleHeight, MetadataHeight, ActionHeight;

                internal ManagementEntry(UiContext ui) { Visibility = new UiCheckbox(ui); }
            }

            private readonly List<ManagementEntry> managementEntries = new List<ManagementEntry>();
            private readonly Dictionary<string, ManagementEntry> managementStates = new Dictionary<string, ManagementEntry>(StringComparer.Ordinal);
            private readonly Dictionary<ModContentPack, Texture2D> managementPreviews = new Dictionary<ModContentPack, Texture2D>();
            private Action<Rect> drawManagementEntries;
            private object managementLanguage;
            private int managementRevision = int.MinValue;
            private float managementWidth = -1f, managementScale = -1f, managementRowHeight = -1f;
            private float managementContentHeight, managementPadding, managementGap, managementPreviewWidth, managementPreviewHeight;
            private float managementTextWidth, managementActionWidth;
            private float managementHeadingHeight, managementTitleHeight, managementHintHeight;
            private bool managementCompact;
            private string managementTitle, managementHint, managementShown, managementHidden, managementAlwaysShown, managementNoPreview;

            private void InitializeManagement()
            {
                drawManagementEntries = DrawManagementEntries;
            }

            private void SyncManagement()
            {
                if (managementRevision == host.catalogRevision && ReferenceEquals(managementLanguage, host.catalogLanguage)) return;
                managementRevision = host.catalogRevision;
                managementLanguage = host.catalogLanguage;
                managementTitle = T("IM_NativeManagementTitle");
                managementHint = T("IM_NativeManagementHint");
                managementShown = T("IM_NativeManagementShown");
                managementHidden = T("IM_NativeManagementHidden");
                managementAlwaysShown = T("IM_NativeManagementAlwaysShown");
                managementNoPreview = T("IM_NativeManagementNoPreview");
                managementEntries.Clear();
                foreach (OwnerGroup group in registeredOwners)
                    AddManagementEntry("registered:" + MenuOwnerActions.ContentOwnerId(group.Content), group.Content, null);
                foreach (MenuPage adapter in host.adapters)
                {
                    if (!MenuRegistry.HasPages(adapter.Owner)) AddManagementEntry(adapter.Id, adapter.Content, adapter);
                }
                managementEntries.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
                managementWidth = -1f;
            }

            private void AddManagementEntry(string id, ModContentPack content, MenuPage adapter)
            {
                if (!managementStates.TryGetValue(id, out ManagementEntry entry))
                {
                    entry = new ManagementEntry(ui);
                    managementStates.Add(id, entry);
                }
                entry.Content = content;
                entry.Adapter = adapter;
                entry.Name = MenuRegistry.GetModDisplayName(content);
                entry.Metadata = T(adapter == null ? "IM_NativeRegistered" : "IM_NativeHosted");
                managementEntries.Add(entry);
            }

            private float MeasureManagementText(string text, float width, float fontSize, FontStyle style = FontStyle.Normal)
            {
                float oldSize = ui.FontSize;
                FontStyle oldStyle = ui.FontStyle;
                try
                {
                    ui.FontSize = fontSize;
                    ui.FontStyle = style;
                    return Mathf.Ceil(UiTextMetrics.Height(ui, text, Mathf.Max(1f, width))) + 2f * scale;
                }
                finally { ui.FontSize = oldSize; ui.FontStyle = oldStyle; }
            }

            private void LayoutManagement(float width)
            {
                if (managementWidth == width && managementScale == scale && managementRowHeight == rowHeight) return;
                managementWidth = width;
                managementScale = scale;
                managementRowHeight = rowHeight;
                managementCompact = width < 620f * scale;
                managementPadding = Mathf.Min((Settings.Density == 0 ? 12f : Settings.Density == 2 ? 20f : 16f) * scale, width * 0.04f);
                managementGap = 14f * scale;
                managementPreviewWidth = managementCompact ? Mathf.Clamp(width * 0.22f, 48f * scale, 96f * scale) : 128f * scale;
                managementPreviewHeight = managementCompact ? 64f * scale : 80f * scale;
                managementActionWidth = managementCompact ? 0f : Mathf.Min(180f * scale, width * 0.28f);
                managementTextWidth = Mathf.Max(1f, width - managementPadding * 2f - managementPreviewWidth - managementGap
                    - (managementCompact ? 0f : managementActionWidth + managementGap));
                float actionTextWidth = Mathf.Max(1f, (managementCompact ? managementTextWidth : managementActionWidth) - 30f * scale);
                float adapterActionHeight = Mathf.Max(36f * scale, Mathf.Max(
                    MeasureManagementText(managementShown, actionTextWidth, 12f * scale),
                    MeasureManagementText(managementHidden, actionTextWidth, 12f * scale)));
                float registeredActionHeight = Mathf.Max(36f * scale,
                    MeasureManagementText(managementAlwaysShown, actionTextWidth, 12f * scale));
                managementTitleHeight = MeasureManagementText(managementTitle, width, 22f * scale, FontStyle.Bold);
                managementHintHeight = MeasureManagementText(managementHint, width, 12f * scale);
                managementHeadingHeight = managementTitleHeight + managementHintHeight + 34f * scale;
                float y = managementHeadingHeight;
                foreach (ManagementEntry entry in managementEntries)
                {
                    entry.Y = y;
                    entry.TitleHeight = MeasureManagementText(entry.Name, managementTextWidth, 16f * scale, FontStyle.Bold);
                    entry.MetadataHeight = MeasureManagementText(entry.Metadata, managementTextWidth, 11f * scale);
                    entry.ActionHeight = entry.Adapter == null ? registeredActionHeight : adapterActionHeight;
                    float textHeight = entry.TitleHeight + 8f * scale + entry.MetadataHeight;
                    if (managementCompact) textHeight += 10f * scale + entry.ActionHeight;
                    else textHeight = Mathf.Max(textHeight, entry.ActionHeight);
                    entry.Height = Mathf.Max(rowHeight + 62f * scale,
                        Mathf.Max(managementPreviewHeight, textHeight) + managementPadding * 2f);
                    y += entry.Height + 8f * scale;
                }
                managementContentHeight = managementEntries.Count == 0
                    ? managementHeadingHeight + 100f * scale : y - 8f * scale;
            }

            private void DrawManagementPage(Rect rect)
            {
                SyncManagement();
                float oldSize = ui.FontSize;
                FontStyle oldStyle = ui.FontStyle;
                try
                {
                    float footerY = Mathf.Max(rect.y, rect.yMax - 52f * scale);
                    Rect list = new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, footerY - rect.y - 14f * scale));
                    LayoutManagement(Mathf.Max(1f, list.width - 20f));
                    managementScroll.Draw(list, managementContentHeight, drawManagementEntries);
                    ui.Fill(new Rect(rect.x, footerY, rect.width, 1f), ui.Palette.Line);
                    ui.FontSize = 13f * scale;
                    if (done.Draw(new Rect(Mathf.Max(rect.x, rect.xMax - 140f * scale), footerY + 14f * scale,
                        Mathf.Min(140f * scale, rect.width), 36f * scale), T("IM_NativeDone"), primary: true, scale: scale)) CloseManagement();
                }
                finally { ui.FontSize = oldSize; ui.FontStyle = oldStyle; }
            }

            private void DrawManagementEntries(Rect rect)
            {
                UiPalette palette = ui.Palette;
                ui.FontSize = 22f * scale;
                ui.FontStyle = FontStyle.Bold;
                ui.Label(new Rect(0f, 0f, rect.width, managementTitleHeight), managementTitle, palette.Ink, TextAnchor.UpperLeft, true);
                ui.FontSize = 12f * scale;
                ui.FontStyle = FontStyle.Normal;
                ui.Label(new Rect(0f, managementTitleHeight + 10f * scale, rect.width, managementHintHeight),
                    managementHint, palette.Muted, TextAnchor.UpperLeft, true);
                if (managementEntries.Count == 0)
                    ui.Label(new Rect(0f, managementHeadingHeight, rect.width, Mathf.Max(100f * scale, rect.height - managementHeadingHeight)),
                        T("IM_NativeManagementEmpty"), palette.Muted, TextAnchor.MiddleCenter, true);
                foreach (ManagementEntry entry in managementEntries)
                {
                    Rect row = new Rect(0f, entry.Y, rect.width, entry.Height);
                    if (!row.Overlaps(managementScroll.VisibleRect)) continue;
                    ui.Fill(row, palette.Raised);
                    ui.Fill(new Rect(row.x, row.yMax - 1f, row.width, 1f), palette.Line);
                    Rect image = new Rect(managementPadding, row.y + managementPadding, managementPreviewWidth, managementPreviewHeight);
                    DrawManagementPreview(entry.Content, image);
                    float textX = image.xMax + managementGap;
                    float textY = row.y + managementPadding;
                    ui.FontSize = 16f * scale;
                    ui.FontStyle = FontStyle.Bold;
                    ui.Label(new Rect(textX, textY, managementTextWidth, entry.TitleHeight), entry.Name, palette.Ink, TextAnchor.UpperLeft, true);
                    ui.FontSize = 11f * scale;
                    ui.FontStyle = FontStyle.Normal;
                    float metadataY = textY + entry.TitleHeight + 8f * scale;
                    ui.Label(new Rect(textX, metadataY, managementTextWidth, entry.MetadataHeight), entry.Metadata, palette.Muted, TextAnchor.UpperLeft, true);
                    Rect action = managementCompact
                        ? new Rect(textX, metadataY + entry.MetadataHeight + 10f * scale, managementTextWidth, entry.ActionHeight)
                        : new Rect(row.xMax - managementPadding - managementActionWidth, row.center.y - entry.ActionHeight * 0.5f,
                            managementActionWidth, entry.ActionHeight);
                    bool registered = entry.Adapter == null;
                    bool enabled = registered || Settings.EnabledAdapters.Contains(entry.Adapter.Id);
                    bool next = entry.Visibility.Draw(action, enabled, enabled: !registered, scale: scale);
                    ui.FontSize = 12f * scale;
                    ui.Label(new Rect(action.x + 30f * scale, action.y, Mathf.Max(1f, action.width - 30f * scale), action.height),
                        registered ? managementAlwaysShown : enabled ? managementShown : managementHidden, palette.Muted, TextAnchor.MiddleLeft, true);
                    if (!registered && next != enabled) SetManagementVisibility(entry.Adapter, next);
                }
            }

            private void SetManagementVisibility(MenuPage page, bool visible)
            {
                if (!visible && host.session.Selected?.Id == page.Id && !host.session.Select(null)) return;
                if (visible) Settings.EnabledAdapters.Add(page.Id);
                else Settings.EnabledAdapters.RemoveAll(id => id == page.Id);
                if (!visible && host.session.Selected == null) host.browse.Navigate(null);
                host.catalogRevision = -1;
                revision = int.MinValue;
                managementRevision = int.MinValue;
            }

            private void DrawManagementPreview(ModContentPack content, Rect rect)
            {
                Texture2D texture = ManagementPreview(content);
                ui.Fill(rect, ui.Palette.Sunken);
                if (texture == null)
                {
                    ui.FontSize = 10f * scale;
                    ui.FontStyle = FontStyle.Normal;
                    ui.Label(rect.ContractedBy(4f * scale), managementNoPreview, ui.Palette.Muted, TextAnchor.MiddleCenter, true);
                }
                else if (ui.IsRepaint)
                {
#if RIMWORLD_1_7
                    UnityStubs.Gui.GuiBackend.Current?.DrawTexture(rect, texture, ScaleMode.ScaleToFit, new Color(1f, 1f, 1f, ui.Alpha));
#else
                    Color previous = GUI.color;
                    try
                    {
                        GUI.color = new Color(1f, 1f, 1f, ui.Alpha);
                        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                    }
                    finally { GUI.color = previous; }
#endif
                }
                ui.Border(rect, ui.Palette.Line);
            }

            private Texture2D ManagementPreview(ModContentPack content)
            {
                if (managementPreviews.TryGetValue(content, out Texture2D cached)) return cached;
                Texture2D texture = null;
                string path = Path.Combine(content.RootDir, "About", "Preview.png");
                try
                {
                    if (File.Exists(path))
                    {
                        texture = new Texture2D(0, 0);
                        if (!texture.LoadImage(File.ReadAllBytes(path)))
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                    texture = null;
                    Log.Warning("[IrisMenus] Cannot load preview at " + path + ": " + exception.Message);
                }
                managementPreviews.Add(content, texture);
                return texture;
            }

            private void CloseManagement()
            {
                managementOpen = false;
                pageChanged = true;
                ui.Focus(default);
                GUI.FocusControl(null);
            }

            private void DisposeManagement()
            {
                foreach (Texture2D texture in managementPreviews.Values)
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                managementPreviews.Clear();
                managementEntries.Clear();
                managementStates.Clear();
            }
        }
    }
}
