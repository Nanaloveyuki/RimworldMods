extern alias iris;
using System;
using System.Collections.Generic;
using iris::IrisMenus;
using iris::IrisMenus.UI;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal sealed class RHAH_IrisMenusChapter
    {
        internal RHAH_IrisMenusChapter(string id, string titleKey, Action<Listing_Standard> draw)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A chapter ID is required.", nameof(id));
            Id = id;
            TitleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
            Draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        internal string Id { get; }
        internal string TitleKey { get; }
        internal Action<Listing_Standard> Draw { get; }
        internal string Title() => TitleKey.Translate().ToString();
    }

    internal sealed class RHAH_IrisMenusNativePage : MenuNativePage
    {
        private readonly UiContext context;
        private readonly MenuScrollView scroll;
        private readonly UiPanel panel;
        private static readonly Action<Rect> noPanelContent = IgnorePanelContent;
        private readonly ChapterState[] chapters;
        private readonly MenuChapter[] navigation;
        private readonly Action<Listing_Standard> drawContent;
        private readonly Action<string> beforeFocus;
        private ChapterState current;
        private string activeChapter;
        private float contentHeight;
        private int pass;
        private bool disposed;

        private sealed class ChapterState
        {
            internal readonly RHAH_IrisMenusChapter Chapter;
            internal float Top;
            internal readonly Dictionary<string, Control> Controls = new Dictionary<string, Control>(StringComparer.Ordinal);
            internal readonly List<Control> Retained = new List<Control>();
            internal readonly Dictionary<string, MeasuredLabel> Labels = new Dictionary<string, MeasuredLabel>(StringComparer.Ordinal);
            internal ChapterState(RHAH_IrisMenusChapter chapter) { Chapter = chapter; }
        }

        private sealed class MeasuredLabel
        {
            internal float Width, Size, Height;
            internal FontStyle Style;
            internal object Language;
        }

        private sealed class Control
        {
            internal UiSwitch Toggle;
            internal UiButton Button;
            internal UiTextField Text;
            internal UiSlider Slider;
            internal UiSelect Select;
            internal UiHoverCard Tooltip;
            internal string[] Options;
            internal string Label;
            internal float LabelWidth, LabelSize, LabelHeight;
            internal FontStyle LabelStyle;
            internal int Seen, Selected, PendingSelection = -1;
            internal bool Active;
        }

        internal RHAH_IrisMenusNativePage(UiContext context, IReadOnlyList<RHAH_IrisMenusChapter> chapters,
            Action<string> beforeFocus = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (chapters == null) throw new ArgumentNullException(nameof(chapters));
            this.beforeFocus = beforeFocus;
            this.chapters = new ChapterState[chapters.Count];
            navigation = new MenuChapter[chapters.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < chapters.Count; i++)
            {
                RHAH_IrisMenusChapter chapter = chapters[i] ?? throw new ArgumentException("Chapters cannot be null.", nameof(chapters));
                if (!ids.Add(chapter.Id)) throw new ArgumentException("Duplicate chapter ID: " + chapter.Id, nameof(chapters));
                this.chapters[i] = new ChapterState(chapter);
                navigation[i] = new MenuChapter(chapter.Id, chapter.Title);
            }
            scroll = new MenuScrollView(context);
            drawContent = DrawContent;
            SectionTitle = new UiSectionTitle(context);
            panel = new UiPanel(context);
        }

        internal UiContext Context => context;
        internal UiSectionTitle SectionTitle { get; }
        internal Rect VisibleRect => scroll.VisibleRect;
        internal float Scale => Mathf.Max(0.01f, context.Scale);
        public override IReadOnlyList<MenuChapter> Chapters => navigation;
        public override string ActiveChapterId => activeChapter;

        internal void DrawCard(Rect rect) => panel.Draw(rect, noPanelContent, 0f);
        private static void IgnorePanelContent(Rect rect) { }

        public override void Draw(Rect rect)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RHAH_IrisMenusNativePage));
            pass++;
            RHAH_IrisMenusNativePage previous = RHAH_IrisMenusWidgets.Native;
            RHAH_IrisMenusWidgets.Native = this;
            try
            {
                scroll.Draw(rect, drawContent);
                UpdateActiveChapter(scroll.VisibleRect);
            }
            finally
            {
                RHAH_IrisMenusWidgets.Native = previous;
                current = null;
                for (int i = 0; i < chapters.Length; i++)
                    for (int j = 0; j < chapters[i].Retained.Count; j++)
                    {
                        Control control = chapters[i].Retained[j];
                        if (control.Active && control.Seen != pass) Suspend(control);
                    }
            }
        }

        private void UpdateActiveChapter(Rect visible)
        {
            activeChapter = chapters.Length == 0 ? null : chapters[0].Chapter.Id;
            float maximum = Mathf.Max(0f, contentHeight - visible.height);
            bool atBottom = maximum > 0f && visible.y >= maximum - 1f;
            float top = visible.y + 20f * Scale;
            for (int i = 1; i < chapters.Length; i++)
                if (chapters[i].Top <= top || atBottom) activeChapter = chapters[i].Chapter.Id;
        }

        private void DrawContent(Listing_Standard list)
        {
            for (int i = 0; i < chapters.Length; i++)
            {
                current = chapters[i];
                current.Top = list.CurHeight;
                MenuControls.Anchor(list, current.Chapter.Id, 44f * Scale);
                current.Chapter.Draw(list);
                if (i + 1 < chapters.Length) list.Gap(12f * Scale);
            }
            contentHeight = list.CurHeight + 4f;
        }

        public override void Focus(string anchor)
        {
            if (string.IsNullOrWhiteSpace(anchor)) return;
            beforeFocus?.Invoke(anchor);
            for (int i = 0; i < chapters.Length; i++)
                for (int j = 0; j < chapters[i].Retained.Count; j++) Suspend(chapters[i].Retained[j]);
            scroll.Focus(anchor);
        }

        public override void Prepare()
        {
            for (int i = 0; i < chapters.Length; i++)
                for (int j = 0; j < chapters[i].Retained.Count; j++)
                {
                    Control control = chapters[i].Retained[j];
                    if (control.Active && control.Seen == pass) control.Select?.Prepare();
                }
        }

        public override void DrawOverlay(Rect viewport)
        {
            for (int i = 0; i < chapters.Length; i++)
                for (int j = 0; j < chapters[i].Retained.Count; j++)
                {
                    Control control = chapters[i].Retained[j];
                    if (!control.Active || control.Seen != pass) continue;
                    if (control.Select != null)
                    {
                        int selected = control.Select.DrawPopup(viewport, control.Selected, GUI.enabled);
                        if (selected != control.Selected) control.PendingSelection = selected;
                    }
                    control.Tooltip?.DrawOverlay(viewport, Scale);
                }
        }

        internal float LabelHeight(string text, float width)
        {
            text = text ?? string.Empty;
            if (!current.Labels.TryGetValue(text, out MeasuredLabel label))
            {
                label = new MeasuredLabel();
                current.Labels.Add(text, label);
            }
            if (label.Width != width || label.Size != context.FontSize || label.Style != context.FontStyle ||
                !ReferenceEquals(label.Language, LanguageDatabase.activeLanguage))
            {
                label.Width = width;
                label.Size = context.FontSize;
                label.Style = context.FontStyle;
                label.Language = LanguageDatabase.activeLanguage;
                label.Height = Measure(text, width);
            }
            return label.Height;
        }

        internal float Measure(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return UnityStubs.Gui.GuiBackend.Current?.MeasureText(1, context.FontSize, context.FontStyle,
                text, Mathf.Max(1f, width)).y ?? Text.CalcHeight(text, Mathf.Max(1f, width));
        }

        private Control Get(string id, bool drawn = true)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable widget ID is required.", nameof(id));
            if (!current.Controls.TryGetValue(id, out Control control))
            {
                control = new Control();
                current.Controls.Add(id, control);
                current.Retained.Add(control);
            }
            if (drawn)
            {
                control.Seen = pass;
                control.Active = true;
            }
            return control;
        }

        private float ControlLabelHeight(Control control, string label, float width)
        {
            if (control.Label != label || control.LabelWidth != width || control.LabelSize != context.FontSize ||
                control.LabelStyle != context.FontStyle)
            {
                control.Label = label;
                control.LabelWidth = width;
                control.LabelSize = context.FontSize;
                control.LabelStyle = context.FontStyle;
                control.LabelHeight = Measure(label, width);
            }
            return control.LabelHeight;
        }

        internal float SwitchHeight(string id, string label, float width)
        {
            Control control = Get(id, false);
            return Mathf.Max(36f * Scale, ControlLabelHeight(control, label, Mathf.Max(1f, width - 68f * Scale)));
        }

        internal bool Switch(Rect row, string id, string label, bool value, string tip)
        {
            Control control = Get(id);
            if (control.Toggle == null) control.Toggle = new UiSwitch(context);
            float width = Mathf.Min(52f * Scale, row.width);
            context.Label(new Rect(row.x, row.y, Mathf.Max(0f, row.width - width - 16f * Scale), row.height),
                label, context.Palette.Ink, wrap: true);
            float height = Mathf.Min(row.height, 36f * Scale);
            bool result = control.Toggle.Draw(new Rect(row.xMax - width, row.center.y - height * 0.5f, width, height),
                value, enabled: GUI.enabled, scale: Scale);
            Observe(control, row, tip, control.Toggle.Id);
            return result;
        }

        internal bool Button(Rect row, string id, string label)
        {
            Control control = Get(id);
            if (control.Button == null) control.Button = new UiButton(context);
            return control.Button.Draw(row, label, GUI.enabled, scale: Scale);
        }

        internal string TextField(Rect row, string id, string value, string placeholder)
        {
            Control control = Get(id);
            if (control.Text == null) control.Text = new UiTextField(context);
            return control.Text.Draw(row, value, placeholder, GUI.enabled, scale: Scale);
        }

        internal int Choice(Rect row, string id, string label, int selected, string[] options)
        {
            Control control = Get(id);
            if (control.Select == null)
            {
                control.Options = CopyOptions(options);
                control.Select = new UiSelect(context, control.Options);
            }
            else if (!SameOptions(control.Options, options))
            {
                control.Options = CopyOptions(options);
                control.Select.SetOptions(control.Options);
            }
            if (control.PendingSelection >= 0)
            {
                selected = options.Length == 0 ? -1 : Mathf.Clamp(control.PendingSelection, 0, options.Length - 1);
                control.PendingSelection = -1;
                GUI.changed = true;
            }
            control.Selected = selected;
            return control.Select.Draw(row, selected, GUI.enabled, label ?? string.Empty, Scale);
        }

        private static string[] CopyOptions(string[] options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return (string[])options.Clone();
        }

        private static bool SameOptions(string[] left, string[] right)
        {
            if (right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }

        internal float NumberHeight(string id, string label, float width)
        {
            Control control = Get(id, false);
            float labelWidth = Mathf.Max(1f, width * 0.34f);
            return Mathf.Max(36f * Scale, ControlLabelHeight(control, label, labelWidth));
        }

        internal float Number(Rect row, string id, string label, float value, ref string buffer,
            float minimum, float maximum, string format, string tip)
        {
            Control control = Get(id);
            if (control.Slider == null) control.Slider = new UiSlider(context);
            if (control.Text == null) control.Text = new UiTextField(context);
            maximum = Mathf.Max(minimum, maximum);
            value = Mathf.Clamp(value, minimum, maximum);
            Rect labelRect = new Rect(row.x, row.y, row.width * 0.34f, row.height);
            context.Label(labelRect, label, context.Palette.Ink, wrap: true);
            float fieldWidth = Mathf.Min(88f * Scale, row.width * 0.28f);
            float controlHeight = Mathf.Min(row.height, 36f * Scale);
            Rect field = new Rect(row.xMax - fieldWidth, row.center.y - controlHeight * 0.5f, fieldWidth, controlHeight);
            Rect slider = new Rect(labelRect.xMax + 8f * Scale, field.y,
                Mathf.Max(1f, field.x - labelRect.xMax - 16f * Scale), field.height);
            float range = maximum - minimum;
            float normalized = range > 0f ? Mathf.Clamp01((value - minimum) / range) : 0f;
            float next = control.Slider.Draw(slider, normalized, GUI.enabled && range > 0f, Scale);
            if (next != normalized)
            {
                value = minimum + next * range;
                buffer = value.ToString(format);
            }
            buffer = control.Text.Draw(field, buffer ?? value.ToString(format), enabled: GUI.enabled, scale: Scale);
            if (float.TryParse(buffer, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed))
                value = Mathf.Clamp(parsed, minimum, maximum);
            Observe(control, row, tip, control.Text.Id);
            return value;
        }

        private void Observe(Control control, Rect row, string tip, UiId id)
        {
            if (string.IsNullOrEmpty(tip)) { control.Tooltip?.Dismiss(); return; }
            if (control.Tooltip == null) control.Tooltip = new UiHoverCard(context);
            control.Tooltip.Observe(row, tip, id, GUI.enabled);
        }

        private void Release(UiId id)
        {
            context.Release(id);
            if (context.IsFocused(id)) context.Focus(default(UiId));
        }

        private void Suspend(Control control)
        {
            if (control.Toggle != null) Release(control.Toggle.Id);
            if (control.Button != null) Release(control.Button.Id);
            if (control.Slider != null) Release(control.Slider.Id);
            control.Text?.Dispose();
            if (control.Select != null)
            {
                control.Select.Close();
                Release(control.Select.Id);
                for (int i = 0; i < control.Options.Length; i++) Release(control.Select.GetOptionId(i));
            }
            control.Tooltip?.Dismiss();
            control.PendingSelection = -1;
            control.Active = false;
        }

        public override void Dispose()
        {
            if (disposed) return;
            for (int i = 0; i < chapters.Length; i++)
                for (int j = 0; j < chapters[i].Retained.Count; j++) Suspend(chapters[i].Retained[j]);
            scroll.Dispose();
            disposed = true;
        }
    }
}
