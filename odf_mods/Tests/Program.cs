using System;
using System.Collections.Generic;
using Odf.PrisonerNames;
using Oxygendsl.Core;

internal static class Program
{
    static int passed;
    static void Main()
    {
        NamingRules rules = NamingRules.Compile("fn codename(pawn) {\n if pawn.bleeding { return \"urgent\" }\n if pawn.resistance <= 0 { return pawn.original_name + \" ready\" }\n return null\n}");
        var values = new Dictionary<string, object> { ["bleeding"] = false, ["resistance"] = 5d, ["original_name"] = "Ada" };
        Check(rules.Evaluate(Context(values), new GameContext()) == null, "null preserves original");
        values["resistance"] = 0d;
        Check(rules.Evaluate(Context(values), new GameContext()) == "Ada ready", "changed resistance changes alias");
        values["bleeding"] = true;
        Check(rules.Evaluate(Context(values), new GameContext()) == "urgent", "earlier health branch wins");
        Reject(() => NamingRules.Compile("fn codename(pawn) { return 2 }").Evaluate(Context(values), new GameContext()), "nonstring result rejected");
        Reject(() => NamingRules.Compile("fn codename(pawn) { return pawn.missing }").Evaluate(Context(values), new GameContext()), "unknown fact surfaces error");
        Reject(() => NamingRules.Compile("fn codename(pawn) { while true {} }").Evaluate(Context(values), new GameContext()), "runaway rule bounded");
        Reject(() => NamingRules.ValidateAlias("line\nbreak"), "multiline manual alias rejected");
        Reject(() => NamingRules.ValidateAlias("<color=red>alias</color>"), "rich text injection rejected");
        Reject(() => NamingRules.ValidateAlias(new string('a', 121)), "oversized alias rejected");
        Check(NamingRules.ValidateAlias("  ") == null, "blank resumes original");
        Check(NameFormatter.Format("{{grade}}", new NameTokens()) == "{grade}", "escaped braces stay literal");
        Reject(() => NameFormatter.Format("{missing}", new NameTokens()), "unknown placeholder keeps the previous config");
        var unicode = new NameTokens { Number = 7, NumberText = "007", Grade = "合格", OriginalName = "ユキ", RaceLabel = "종족" };
        Check(NameFormatter.Format("囚{number_text}·{grade}·{original_name}·{race_label}", unicode) == "囚007·合格·ユキ·종족", "English keys preserve multilingual text and values");
        Reject(() => NameFormatter.Format("{等级}", unicode), "Chinese placeholder aliases are rejected");
        Reject(() => NameFormatter.Format("{名前}", unicode), "unknown Japanese placeholder is rejected");
        Check(NameFormatter.Format("{{等级}}", unicode) == "{等级}", "escaped Unicode text stays literal");
        AgeNamingChecks.Run(Check);
        var policy = NamePolicy.CreateDefault();
        policy.Grades.Clear();
        policy.Grades.Add(new GradeBand { Minimum = 10d, Name = "Later" });
        policy.Grades.Add(new GradeBand { Minimum = 0d, Name = "First" });
        Check(policy.GradeFor(10d) == "Later", "higher inclusive threshold wins regardless of UI order");
        Check(policy.NextThreshold(0d) == 10d, "next threshold is the following lower bound");
        string preview = NamingPreview.Evaluate("fn codename(pawn): string? { return pawn.formatted(\"{grade}-{number_text}\") }", "{\"race\":\"Ratkin\",\"original_name\":\"Ada\"}", policy, false);
        Check(preview == "First-001", "preview uses the same formatted naming result");
        Reject(() => JsonFacts.ReadObject("{\"score\":1e999}"), "nonfinite preview facts rejected");
        Reject(() => NamingPreview.Evaluate("fn codename(pawn) { return pawn.missing }", "{}", policy, false), "unknown preview fact surfaces");
        var first = new ScalarBag();
        var second = new ScalarBag();
        first.Set("mark", "one");
        Check(second.Get("mark", null) == null, "each prisoner keeps a separate value bag");
        var copy = first.Copy();
        var state = new EventState(copy, 4d, false);
        state.TryGetMember("set", out object writer);
        ((Oxygendsl.Core.IOxygenHostCallable)writer).Invoke(new object[] { "mark", "changed" }, null);
        object stored;
        Check(first.TryGet("mark", out stored) && (string)stored == "one", "event copy does not change the saved bag");
        first.Replace(copy);
        Check(first.TryGet("mark", out stored) && (string)stored == "changed", "successful event replaces the saved bag");
        Console.WriteLine("Prisoner naming behavior checks passed: " + passed);
    }
    static PawnContext Context(Dictionary<string, object> facts) => new PawnContext { Name = "Ada", Facts = new DictionaryOxygenHostValue(facts) };
    static void Check(bool condition, string scenario) { if (!condition) throw new Exception(scenario); passed++; }
    static void Reject(Action action, string scenario)
    {
        try { action(); } catch (Exception) { passed++; return; }
        throw new Exception(scenario);
    }
}
