using System;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class Program
    {
        private static int count;
        private static void Main()
        {
            Reject(Mood("same", "") + "\n" + Mood("same", ""), "duplicate rule identity");
            Reject("mood(\"x\", label: \"x\", description: \"x\", offset: 1, target: \"galaxy\")", "unknown target");
            Reject(Mood("x", ", ofset: 2"), "unknown argument is not silently ignored");
            Reject("memory(\"x\", label: \"x\", description: \"x\", target: \"colonists\", offset: 1, duration: 100, cooldown: 0, mode: \"refresh\")", "zero cooldown");
            Reject("memory(\"x\", label: \"x\", description: \"x\", target: \"colonists\", offset: 1, duration: 100, cooldown: 10, mode: \"stack\")", "unbounded stack");
            Reject("fn check() { return true }\n" + Mood("x", ", when: check"), "wrong condition arity");
            Reject("let broken = 1 / 0\n" + Mood("x", ""), "invalid initialization without any condition");
            ScriptCompilation configured = Compile("const amount = 3\nlet title = \"changed\"\nmood(\"configured\", label: title, description: \"test\", offset: amount * 2, target: \"colonists\")");
            Check(configured.Program.Rules[0].Offset == 6f && configured.Program.Rules[0].Label == "changed", "registration values evaluate expressions and bindings");

            ScriptCompilation dynamic = Compile("fn check(pawn) { return pawn.food != null && pawn.food > 0.25 && game.tick >= 10 }\n" + Mood("x", ", when: check"));
            IRuleCondition condition = dynamic.Program.Rules[0].Condition;
            PawnContext pawn = new PawnContext { Id = 1, Food = 0.5 };
            Check(!condition.Evaluate(pawn, new GameContext { Tick = 9 }), "tick condition before boundary");
            Check(condition.Evaluate(pawn, new GameContext { Tick = 10 }), "tick condition at boundary");
            pawn.Food = 0.25;
            Check(!condition.Evaluate(pawn, new GameContext { Tick = 10 }), "strict food boundary");
            pawn.Food = null;
            Check(!condition.Evaluate(pawn, new GameContext { Tick = 10 }), "missing need is null");
            ScriptCompilation indexed = Compile("fn check(pawn) { return pawn[\"food\"] == 0.5 && game[\"tick\"] == 10 }\n" + Mood("x", ", when: check"));
            pawn.Food = 0.5;
            Check(indexed.Program.Rules[0].Condition.Evaluate(pawn, new GameContext { Tick = 10 }), "context indexed access matches member access");
            ScriptCompilation directWrite = Compile("fn check(pawn) { pawn.food = 1; return true }\n" + Mood("x", ", when: check"));
            ExpectEvaluationFailure(directWrite.Program.Rules[0].Condition, pawn, "readonly context member mutation");
            Check(pawn.Food == 0.5, "readonly context mutation preserves the need");

            ScriptCompilation reusable = Compile("fn check(pawn) { let sum = 0; for value in range(0, 20) { sum += value }; return sum == 190 }\n" + Mood("x", ", when: check"));
            bool allPassed = true;
            for (int i = 0; i < 250; i++) allPassed &= reusable.Program.Rules[0].Condition.Evaluate(pawn, new GameContext());
            Check(allPassed, "condition budget resets across repeated invocations");

            ScriptCompilation broken = Compile("fn check(pawn) { return pawn.nonexistent }\n" + Mood("x", ", when: check"));
            ExpectEvaluationFailure(broken.Program.Rules[0].Condition, pawn, "unknown member");
            ScriptCompilation infinite = Compile("fn check(pawn) { while true { }; return true }\n" + Mood("x", ", when: check"));
            ExpectEvaluationFailure(infinite.Program.Rules[0].Condition, pawn, "infinite loop step budget");
            ScriptCompilation notBoolean = Compile("fn check(pawn) { return 1 }\n" + Mood("x", ", when: check"));
            ExpectEvaluationFailure(notBoolean.Program.Rules[0].Condition, pawn, "condition must return boolean");
            LanguageChecks.Run(Check);
            ContentChecks.Run(Check);
            TraitChecks.Run(Check);
            EventCallbackChecks.Run(Check);
            LocalizationChecks.Run(Check);
            EventChainChecks.Run(Check);
            Console.WriteLine("Oxygendsl behavior checks passed: " + count);
        }

        private static string Mood(string id, string extra)
        {
            return "mood(\"" + id + "\", label: \"test\", description: \"test\", offset: 1, target: \"colonists\"" + extra + ")";
        }

        private static ScriptCompilation Compile(string source)
        {
            ScriptCompilation result = ScriptCompiler.Compile(source);
            if (!result.Success) throw new InvalidOperationException("Compilation failed: " + string.Join("; ", result.Diagnostics));
            return result;
        }
        private static void Reject(string source, string scenario)
        {
            ScriptCompilation result = ScriptCompiler.Compile(source);
            Check(!result.Success && result.Program == null && result.Diagnostics.Count > 0, scenario);
            Check(result.Diagnostics[0].Line >= 1 && result.Diagnostics[0].Column >= 1, scenario + " position");
        }
        private static void ExpectEvaluationFailure(IRuleCondition condition, PawnContext pawn, string scenario)
        {
            try { condition.Evaluate(pawn, new GameContext()); }
            catch (ScriptEvaluationException exception)
            {
                Check(exception.Line >= 1 && exception.Column >= 1 && !string.IsNullOrEmpty(exception.Message), scenario + " diagnostic");
                return;
            }
            throw new InvalidOperationException("Expected failure: " + scenario);
        }
        private static void Check(bool passed, string scenario)
        {
            if (!passed) throw new InvalidOperationException("Failed: " + scenario);
            count++;
        }
    }
}
