using System;
using Odf.PrisonerNames;

static class AgeNamingChecks
{
    public static void Run(Action<bool, string> check)
    {
        NamePolicy policy = NamePolicy.CreateDefault();
        policy.ValidateNaming();
        double[] ages = { 0d, 0.999d, 1d, 6.999d, 7d, 13.999d, 14d, 17.999d, 18d, 100d };
        string[] expected = { "幼001", "幼001", "童001", "童001", "少001", "少001", "青001", "青001", "P001", "P001" };
        for (int i = 0; i < ages.Length; i++)
        {
            string facts = "{\"race\":\"Human\",\"age\":" + ages[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";
            check(NamingPreview.Evaluate("fn codename(pawn) { return pawn.formatted() }", facts, policy, true) == expected[i], "biological age boundary " + ages[i]);
        }
        var race = new RaceNameRule { Race = "Ratkin", Enabled = true };
        race.Ages.Add(new AgeNameRule { MinimumAge = 1d, Pattern = "Kit {number_text}", ChinesePattern = "鼠童{number_text}" });
        policy.RaceNames.Add(race);
        policy.ValidateNaming();
        check(policy.PatternFor("Ratkin", 1.5d, true) == "鼠童{number_text}", "matching race band overrides default");
        check(policy.PatternFor("Human", 1.5d, true) == "童{number_text}", "race override does not affect other races");
        check(policy.PatternFor("Ratkin", 7d, true) == "少{number_text}", "missing race band inherits exact global band rather than earlier override");
        race.Enabled = false;
        check(policy.PatternFor("Ratkin", 1.5d, true) == "童{number_text}", "disabled race override inherits global rule");
        race.Enabled = true;
        NamePolicy copy = policy.Clone();
        copy.Ages[0].ChinesePattern = "clone";
        copy.RaceNames[0].Ages[0].ChinesePattern = "clone-race";
        check(policy.Ages[0].ChinesePattern == "幼{number_text}" && policy.RaceNames[0].Ages[0].ChinesePattern == "鼠童{number_text}", "deep clone isolates global and race edits");
        policy.UseAgePatterns = false;
        policy.ChinesePattern = "旧{number_text}";
        check(policy.PatternFor("Ratkin", 1.5d, true) == "旧{number_text}", "disabled age naming preserves legacy base pattern");
        NamePolicy invalid = policy.Clone();
        invalid.Ages.Add(invalid.Ages[0].Clone());
        Reject(invalid.ValidateNaming, check, "duplicate default age boundaries rejected");
        invalid = policy.Clone(); invalid.Ages[1].MinimumAge = -1;
        Reject(invalid.ValidateNaming, check, "negative age boundary rejected");
        invalid = policy.Clone(); invalid.Ages[1].MinimumAge = double.NaN;
        Reject(invalid.ValidateNaming, check, "nonfinite age boundary rejected");
        invalid = policy.Clone(); invalid.Ages[0].MinimumAge = 0.5d;
        Reject(invalid.ValidateNaming, check, "global bands must cover age zero");
        invalid = policy.Clone(); invalid.RaceNames[0].Ages[0].MinimumAge = 2d;
        Reject(invalid.ValidateNaming, check, "race override must match a global boundary");
        invalid = policy.Clone(); invalid.RaceNames[0].Ages[0].Pattern = "{等级}";
        Reject(invalid.ValidateNaming, check, "inactive language patterns are validated");
        check(NamingPreview.Evaluate("fn codename(pawn) { return pawn.formatted(pattern: \"明{number_text}\") }", "{\"age\":0.5}", policy, true) == "明001", "explicit named pattern bypasses age selection");
    }

    static void Reject(Action action, Action<bool, string> check, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        check(rejected, message);
    }
}
