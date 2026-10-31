using System;
using System.Collections.Generic;
using IrisMenus;
using Verse;

namespace RatkinRecycle.IrisMenus;

[StaticConstructorOnStartup]
public static class RatkinRecycleMenus
{
	private static string indexedRaceKeywords;

	static RatkinRecycleMenus() => LongEventHandler.ExecuteWhenFinished(Register);

	private static void Register()
	{
		RatkinRecycleMod owner = LoadedModManager.GetMod<RatkinRecycleMod>();
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		indexedRaceKeywords = settings.raceKeywords;
		foreach (string id in RatkinRecycleSettings.PageIds)
		{
			string pageId = id;
			MenuRegistry.RegisterSubItem(owner, pageId, () => RatkinRecycleSettings.PageTitle(pageId), rect =>
			{
				settings.DrawPageWindow(pageId, rect);
				RefreshSearchIndex(settings);
			});
			MenuRegistry.RegisterPresentation(owner, pageId,
				new MenuPresentation(context => new RatkinRecycleNativePage(context, settings, pageId)));
			MenuRegistry.RegisterSearchProvider(owner, pageId, () => SearchEntries(pageId));
		}
	}

	internal static void RefreshSearchIndex(RatkinRecycleSettings settings)
	{
		if (indexedRaceKeywords == settings.raceKeywords) return;
		indexedRaceKeywords = settings.raceKeywords;
		MenuRegistry.InvalidateSearchIndex();
	}

	public static IEnumerable<MenuSearchEntry> SearchEntries(string pageId)
	{
		var renderer = new SearchRenderer(pageId);
		RatkinRecycleMod.Settings.DrawPage(pageId, renderer);
		return renderer.Entries;
	}

	// Capture the same fields used by both UIs, including dynamic body parts. Search IDs
	// cannot drift from their actual controls when a setting is added, removed or renamed.
	private sealed class SearchRenderer : IRatkinRecycleSettingsRenderer
	{
		private readonly string pageId;
		private string section;
		public readonly List<MenuSearchEntry> Entries = new List<MenuSearchEntry>();
		public bool IsMeasuring => true;

		public SearchRenderer(string pageId) => this.pageId = pageId;

		private void Add(string id, string label, string keywords = null)
		{
			string context = section;
			Entries.Add(new MenuSearchEntry(id, () => label,
				() => id + " " + keywords,
				() => RatkinRecycleSettings.PageTitle(pageId) + " · " + context));
		}

		public void Section(string id, string label)
		{
			section = label;
			Add("section:" + id, label);
		}

		public void Label(string text) { }
		public void Check(string id, string label, ref bool value, string tooltip = null) => Add(id, label, tooltip);
		public void Number(string id, string label, ref float value, float min = 0f, float max = float.MaxValue, bool enabled = true) => Add(id, label);
		public void Integer(string id, string label, ref int value, int min = 0, bool enabled = true) => Add(id, label);
		public void TextField(string id, ref string value, int maxLength) => Add(id, section, value);
		public bool Button(string id, string label)
		{
			Add(id, label);
			return false;
		}
	}
}
