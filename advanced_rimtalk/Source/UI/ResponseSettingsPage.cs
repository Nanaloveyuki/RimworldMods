using UnityEngine;
using Verse;

namespace AdvancedRimTalk.UI
{
    internal sealed class ResponseSettingsPage
    {
        private Vector2 scroll;
        private string intervalBuffer;
        internal void Draw(Rect rect)
        {
            Rect view = new Rect(0f, 0f, rect.width - 24f, 760f);
            ArtiUi.BeginScrollView(rect, ref scroll, view);
            Listing_Standard l = new Listing_Standard(); l.Begin(view);
            var s = AdvancedRimTalk.AdvancedRimTalkMod.Settings;
            Checkbox(l, "AdvancedRimTalk.Response.Enable".Translate(), ref s.EnableResponseProcessing);
            Checkbox(l, "AdvancedRimTalk.Response.Json".Translate(), ref s.EnableJsonFormatting);
            Checkbox(l, "AdvancedRimTalk.Response.RegexIgnore".Translate(), ref s.IgnoreByRegex);
            Checkbox(l, "AdvancedRimTalk.Response.Whitelist".Translate(), ref s.RegexWhitelist);
            Label(l, "AdvancedRimTalk.Response.WhitelistPatterns".Translate());
            s.ResponseWhitelistRegex = TextEntry(l, s.ResponseWhitelistRegex, 5);
            Checkbox(l, "AdvancedRimTalk.Response.Blacklist".Translate(), ref s.RegexBlacklist);
            Label(l, "AdvancedRimTalk.Response.BlacklistPatterns".Translate());
            s.ResponseBlacklistRegex = TextEntry(l, s.ResponseBlacklistRegex, 5);
            Checkbox(l, "AdvancedRimTalk.Response.IntervalIgnore".Translate(), ref s.IgnoreByInterval);
            Label(l, "AdvancedRimTalk.Response.IntervalSeconds".Translate());
            if (intervalBuffer == null) intervalBuffer = s.IgnoreIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            intervalBuffer = TextEntry(l, intervalBuffer, 1);
            if (float.TryParse(intervalBuffer, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float interval)
                && !float.IsNaN(interval) && !float.IsInfinity(interval) && interval >= 0)
                s.IgnoreIntervalSeconds = interval;
            Checkbox(l, "AdvancedRimTalk.Response.ModelIgnore".Translate(), ref s.IgnoreByModel);
            Label(l, "AdvancedRimTalk.Response.ModelIds".Translate());
            s.ResponseModelIds = TextEntry(l, s.ResponseModelIds, 5);
            l.End(); ArtiUi.EndScrollView();
        }

        private static void Checkbox(Listing_Standard listing, string label, ref bool value)
        {
            ArtiUi.CheckboxLabeled(listing.GetRect(Text.CalcHeight(label, listing.ColumnWidth)), label, ref value);
            listing.Gap(listing.verticalSpacing);
        }

        private static void Label(Listing_Standard listing, string label)
        {
            ArtiUi.Label(listing.GetRect(Text.CalcHeight(label, listing.ColumnWidth)), label);
            listing.Gap(listing.verticalSpacing);
        }

        private static string TextEntry(Listing_Standard listing, string value, int lines)
        {
            Rect rect = listing.GetRect(Text.LineHeight * lines);
            string result = lines == 1 ? ArtiUi.TextField(rect, value) : ArtiUi.TextArea(rect, value);
            listing.Gap(listing.verticalSpacing);
            return result;
        }
    }
}
