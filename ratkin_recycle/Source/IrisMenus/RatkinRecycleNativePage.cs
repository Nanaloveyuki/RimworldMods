using System;
using System.Collections.Generic;
using System.Globalization;
using IrisMenus;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace RatkinRecycle.IrisMenus;

internal sealed class RatkinRecycleNativePage : MenuNativePage, IRatkinRecycleSettingsRenderer
{
	private enum RowKind { Section, Label, Check, Number, Integer, Text, Button }

	private sealed class Row
	{
		public RowKind Kind;
		public string Id, Label, Buffer;
		public Rect Rect, LabelRect, FieldRect;
		public int Anchor = -1;
		public UiSwitch Switch;
		public UiCheckbox Checkbox;
		public UiTextField Field;
		public UiButton Button;
		public float LastNumber;
		public int LastInteger;
		public bool Invalid;
		public UiId FocusTarget => Field != null ? Field.Id : Switch != null ? Switch.Id : Checkbox != null ? Checkbox.Id : Button != null ? Button.Id : default;
	}

	private readonly UiContext context;
	private readonly RatkinRecycleSettings settings;
	private readonly string pageId;
	private readonly List<Row> rows = new List<Row>();
	private readonly Dictionary<string, Row> anchors = new Dictionary<string, Row>(StringComparer.Ordinal);
	private readonly Action<Rect> drawContent;
	private readonly UiTooltip tooltip;
	private readonly GUIContent measuring = new GUIContent();
	private UiScrollView scroll;
	private MenuChapter[] chapters = Array.Empty<MenuChapter>();
	private GUIStyle textStyle;
	private object language;
	private string keywords, pendingAnchor;
	private int editRevision, cursor;
	private bool capturing, tooltipObserved;
	private float scale, width = -1f, contentHeight;

	public RatkinRecycleNativePage(UiContext context, RatkinRecycleSettings settings, string pageId)
	{
		this.context = context ?? throw new ArgumentNullException(nameof(context));
		this.settings = settings;
		this.pageId = pageId;
		tooltip = new UiTooltip(context);
		drawContent = DrawContent;
		RebuildRows();
	}

	public bool IsMeasuring => capturing;
	public override IReadOnlyList<MenuChapter> Chapters => chapters;
	public override bool CanReset => true;
	public override string ActiveChapterId
	{
		get
		{
			string active = null;
			float top = scroll.VisibleRect.y;
			foreach (Row row in rows)
			{
				if (row.Kind != RowKind.Section) continue;
				if (active != null && row.Rect.y > top + 16f * scale) break;
				active = row.Id;
			}
			return active;
		}
	}

	public override void Prepare()
	{
		RatkinRecycleMenus.RefreshSearchIndex(settings);
		if (!ReferenceEquals(language, LanguageDatabase.activeLanguage)
			|| (pageId == "chewing" && keywords != settings.raceKeywords)) RebuildRows();
		if (editRevision == settings.EditRevision) return;
		editRevision = settings.EditRevision;
		foreach (Row row in rows)
		{
			if (row.Kind == RowKind.Number || row.Kind == RowKind.Integer) row.Buffer = null;
			row.Invalid = false;
		}
	}

	private void RebuildRows()
	{
		float position = scroll?.Position.y ?? 0f;
		ReleaseControls();
		rows.Clear();
		anchors.Clear();
		capturing = true;
		try { settings.DrawPage(pageId, this); }
		finally { capturing = false; }
		var nextChapters = new List<MenuChapter>();
		int index = 0;
		foreach (Row row in rows)
		{
			if (row.Id == null) continue;
			row.Anchor = index++;
			anchors.Add(row.Id, row);
			if (row.Kind == RowKind.Section)
			{
				Row section = row;
				nextChapters.Add(new MenuChapter(section.Id, () => section.Label));
			}
		}
		chapters = nextChapters.ToArray();
		scroll = new UiScrollView(context, index);
		scroll.SetPosition(position);
		language = LanguageDatabase.activeLanguage;
		keywords = settings.raceKeywords;
		editRevision = settings.EditRevision;
		width = -1f;
	}

	public override void Draw(Rect rect)
	{
		if (rect.width <= 20f || rect.height <= 0f) return;
		float previousFont = context.FontSize;
		FontStyle previousStyle = context.FontStyle;
		try
		{
			float nextScale = context.Scale;
			float nextWidth = Math.Max(1f, rect.width - 20f);
			if (textStyle == null)
				textStyle = new GUIStyle(Text.CurFontStyle) { richText = false, wordWrap = true, alignment = TextAnchor.UpperLeft };
			if (scale != nextScale || width != nextWidth)
			{
				scale = nextScale;
				width = nextWidth;
				textStyle.font = Text.CurFontStyle.font;
				textStyle.fontSize = Mathf.RoundToInt(16f * scale);
				MeasureRows();
			}
			context.FontSize = 16f * scale;
			context.FontStyle = FontStyle.Normal;
			if (pendingAnchor != null && anchors.TryGetValue(pendingAnchor, out Row target))
			{
				scroll.FocusAnchor(target.Anchor, 12f * scale);
				pendingAnchor = null;
			}
			scroll.Draw(rect, contentHeight, drawContent);
			RatkinRecycleMenus.RefreshSearchIndex(settings);
		}
		finally
		{
			context.FontSize = previousFont;
			context.FontStyle = previousStyle;
		}
	}

	private float Height(string text, float availableWidth)
	{
		measuring.text = text;
		return textStyle.CalcHeight(measuring, Math.Max(1f, availableWidth));
	}

	private void MeasureRows()
	{
		float y = 0f;
		float x = 8f * scale;
		float available = Math.Max(1f, width - 16f * scale);
		foreach (Row row in rows)
		{
			float height;
			if (row.Kind == RowKind.Section)
			{
				y += 16f * scale;
				height = Math.Max(36f * scale, Height(row.Label, available) + 12f * scale);
			}
			else if (row.Kind == RowKind.Label) height = Height(row.Label, available) + 8f * scale;
			else if (row.Kind == RowKind.Check)
				height = Math.Max(40f * scale, Height(row.Label, available - 68f * scale) + 12f * scale);
			else if (row.Kind == RowKind.Number || row.Kind == RowKind.Integer)
			{
				float fieldWidth = Math.Min(180f * scale, available * 0.36f);
				float labelWidth = Math.Max(1f, available - fieldWidth - 12f * scale);
				height = Math.Max(40f * scale, Height(row.Label, labelWidth) + 12f * scale);
				row.LabelRect = new Rect(x, y, labelWidth, height);
				row.FieldRect = new Rect(x + available - fieldWidth, y, fieldWidth, 40f * scale);
			}
			else height = 40f * scale;
			row.Rect = new Rect(x, y, available, height);
			y += height + (row.Kind == RowKind.Label ? 8f : 6f) * scale;
		}
		contentHeight = y + 12f * scale;
	}

	private void DrawContent(Rect rect)
	{
		cursor = 0;
		tooltipObserved = false;
		settings.DrawPage(pageId, this);
		if (!tooltipObserved) tooltip.Dismiss();
	}

	private Row Next(RowKind kind, string id, string label)
	{
		if (capturing)
		{
			var row = new Row { Kind = kind, Id = id, Label = label };
			switch (kind)
			{
				case RowKind.Check:
					if (id.StartsWith("part:", StringComparison.Ordinal) || id.IndexOf(':') >= 0) row.Checkbox = new UiCheckbox(context);
					else row.Switch = new UiSwitch(context);
					break;
				case RowKind.Number:
				case RowKind.Integer: row.Field = new UiTextField(context); break;
				case RowKind.Text: row.Field = new UiTextField(context, 400); break;
				case RowKind.Button: row.Button = new UiButton(context); break;
			}
			rows.Add(row);
			return row;
		}
		Row current = rows[cursor++];
		if (current.Kind != kind || current.Id != id) throw new InvalidOperationException("Settings layout changed without rebuilding native controls.");
		if (current.Anchor >= 0) scroll.Anchor(current.Anchor, current.Rect, focusTarget: current.FocusTarget);
		return current;
	}

	public void Section(string id, string label)
	{
		Row row = Next(RowKind.Section, "section:" + id, label);
		if (capturing) return;
		context.FontStyle = FontStyle.Bold;
		try { context.Label(row.Rect, row.Label, context.Palette.Ink, TextAnchor.MiddleLeft, true); }
		finally { context.FontStyle = FontStyle.Normal; }
		context.Fill(new Rect(row.Rect.x, row.Rect.yMax - scale, row.Rect.width, scale), context.Palette.Line);
	}

	public void Label(string text)
	{
		Row row = Next(RowKind.Label, null, text);
		if (!capturing) context.Label(row.Rect, row.Label, context.Palette.Muted, TextAnchor.UpperLeft, true);
	}

	public void Check(string id, string label, ref bool value, string tooltipText = null)
	{
		Row row = Next(RowKind.Check, id, label);
		if (capturing) return;
		if (row.Switch != null)
		{
			value = row.Switch.Draw(row.Rect, value, scale: scale);
			context.Label(new Rect(row.Rect.x, row.Rect.y, Math.Max(1f, row.Rect.width - 68f * scale), row.Rect.height), row.Label,
				context.Palette.Ink, TextAnchor.MiddleLeft, true);
		}
		else
		{
			value = row.Checkbox.Draw(row.Rect, value, scale: scale);
			context.Label(new Rect(row.Rect.x + 32f * scale, row.Rect.y, Math.Max(1f, row.Rect.width - 32f * scale), row.Rect.height), row.Label,
				context.Palette.Ink, TextAnchor.MiddleLeft, true);
		}
		if (!string.IsNullOrEmpty(tooltipText) && (context.Hit(row.Rect) || context.IsFocused(row.FocusTarget)))
		{
			tooltip.Observe(row.Rect, tooltipText, row.FocusTarget);
			tooltipObserved = true;
		}
	}

	private string DrawNumber(Row row, bool enabled)
	{
		context.Label(row.LabelRect, row.Label, enabled ? context.Palette.Ink : context.Palette.Muted, TextAnchor.MiddleLeft, true);
		return row.Field.Draw(row.FieldRect, row.Buffer, enabled: enabled, invalid: row.Invalid, scale: scale);
	}

	public void Number(string id, string label, ref float value, float min = 0f, float max = float.MaxValue, bool enabled = true)
	{
		Row row = Next(RowKind.Number, id, label);
		if (capturing) return;
		if (row.Buffer == null || row.LastNumber != value)
		{
			row.Buffer = value.ToString("R", CultureInfo.InvariantCulture);
			row.LastNumber = value;
			row.Invalid = false;
		}
		string next = DrawNumber(row, enabled);
		bool valid = float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
			&& !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max;
		row.Invalid = !valid;
		if (enabled && next != row.Buffer && valid) value = row.LastNumber = parsed;
		row.Buffer = next;
	}

	public void Integer(string id, string label, ref int value, int min = 0, bool enabled = true)
	{
		Row row = Next(RowKind.Integer, id, label);
		if (capturing) return;
		if (row.Buffer == null || row.LastInteger != value)
		{
			row.Buffer = value.ToString(CultureInfo.InvariantCulture);
			row.LastInteger = value;
			row.Invalid = false;
		}
		string next = DrawNumber(row, enabled);
		bool valid = int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= min;
		row.Invalid = !valid;
		if (enabled && next != row.Buffer && valid) value = row.LastInteger = parsed;
		row.Buffer = next;
	}

	public void TextField(string id, ref string value, int maxLength)
	{
		Row row = Next(RowKind.Text, id, null);
		if (!capturing) value = row.Field.Draw(row.Rect, value, scale: scale);
	}

	public bool Button(string id, string label)
	{
		Row row = Next(RowKind.Button, id, label);
		return !capturing && row.Button.Draw(row.Rect, row.Label, scale: scale);
	}

	public override void DrawOverlay(Rect viewport) => tooltip.DrawOverlay(viewport, new Vector2(280f * context.Scale, 64f * context.Scale), context.Scale);
	public override void Focus(string anchor) => pendingAnchor = anchor;
	public override void Reset()
	{
		settings.ResetDefaults();
		RatkinRecycleMenus.RefreshSearchIndex(settings);
	}

	private void ReleaseControls()
	{
		foreach (Row row in rows) row.Field?.Dispose();
		scroll?.Dispose();
	}

	public override void Dispose() => ReleaseControls();
}
