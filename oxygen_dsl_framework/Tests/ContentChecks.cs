using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class ContentChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            ContentCompilation composition = Compile(@"
template(""rare"", type: ""TraitDef"", fields: { commonality: 0, exclusionTags: [""base""] })
template(""worker"", type: ""TraitDef"", extends: ""rare"", fields: { allowOnHostileSpawn: false })
define(""TraitDef"", ""First"", mixins: [""worker""], fields: { commonality: 2 })
define(""TraitDef"", ""Second"", extends: ""worker"", fields: {})");
            ContentDefinition first = composition.Plan.Definitions[0];
            ContentDefinition second = composition.Plan.Definitions[1];
            check(Convert.ToDouble(first.Fields["commonality"]) == 2 && Convert.ToDouble(second.Fields["commonality"]) == 0,
                "derived override leaves template sibling generation weight unchanged");
            IList<object> firstTags = (IList<object>)first.Fields["exclusionTags"];
            firstTags.Add("first-only");
            check(((IList<object>)second.Fields["exclusionTags"]).Count == 1,
                "native template composition does not share mutable lists between definitions");

            Reject("template(\"loop\", type: \"TraitDef\", extends: \"loop\", fields: {})\ndefine(\"TraitDef\", \"Cycle\", extends: \"loop\", fields: {})",
                check, "cyclic template inheritance rejected before registration");

            OxygenModuleRegistry modules = new OxygenModuleRegistry();
            modules.RegisterSource("example.mod", "bad", "define(\"TraitDef\", \"Unexpected\", fields: {})");
            ContentCompilation imported = ContentCompiler.Compile("use bad@example.mod\ndefine(\"TraitDef\", \"Expected\", fields: {})", modules);
            check(!imported.Success, "library import cannot silently enable native content");

            ContentCompilation rules = Compile(@"
fn choose(pawn) { if pawn.young_sick { return 1 }; if pawn.young_hungry { return 0 }; return -1 }
fn allow(pawn) { if pawn.colonist { return null }; return !pawn.downed }
thought_rule(""children"", thought: def_ref(""ThoughtDef"", ""Children""), evaluate: choose)
gate_rule(""begging"", gate: ""Beg"", evaluate: allow)");
            MutableFacts facts = new MutableFacts();
            PawnContext pawn = new PawnContext { Facts = facts };
            GameContext game = new GameContext();
            check(rules.Plan.Thoughts[0].Evaluator.Evaluate(pawn, game) == -1, "no group need leaves native thought inactive");
            facts.Hungry = true;
            check(rules.Plan.Thoughts[0].Evaluator.Evaluate(pawn, game) == 0, "hunger selects native stage zero");
            facts.Sick = true;
            check(rules.Plan.Thoughts[0].Evaluator.Evaluate(pawn, game) == 1, "illness wins when hunger and illness both hold");
            check(rules.Plan.Gates[0].Evaluator.Evaluate(pawn, game) == true, "gate explicitly allows eligible non-colonist");
            pawn.Downed = true;
            check(rules.Plan.Gates[0].Evaluator.Evaluate(pawn, game) == false, "gate explicitly denies downed non-colonist");
            pawn.Colonist = true;
            check(rules.Plan.Gates[0].Evaluator.Evaluate(pawn, game) == null, "colonist gate abstention is distinct from denial");

            ContentCompilation wrong = Compile("fn bad(pawn) { return 1 }\ngate_rule(\"bad\", gate: \"Beg\", evaluate: bad)");
            try
            {
                wrong.Plan.Gates[0].Evaluator.Evaluate(pawn, game);
                throw new InvalidOperationException("Expected invalid gate return failure.");
            }
            catch (ScriptEvaluationException) { check(true, "gate does not coerce an integer into a permission"); }

            ContentCompilation mutate = Compile("fn bad(pawn) { let alias = pawn; alias.young_sick = false; return 0 }\nthought_rule(\"bad\", thought: def_ref(\"ThoughtDef\", \"Children\"), evaluate: bad)");
            try
            {
                mutate.Plan.Thoughts[0].Evaluator.Evaluate(pawn, game);
                throw new InvalidOperationException("Expected host mutation failure.");
            }
            catch (ScriptEvaluationException) { check(facts.Sick, "aliased Pawn facts remain read-only"); }

            OxygenDeclarations.Register(new OxygenDeclaration("custom_def", OxygenDeclarationKind.Registration, registration =>
            {
                string id = null;
                foreach (ContentRegistrationArgument argument in registration.Arguments)
                {
                    if (argument.Name == null) id = argument.Value as string;
                }
                return new ContentRegistrationResult
                {
                    Definitions = new[]
                    {
                        new ContentDefinition
                        {
                            Type = "TraitDef",
                            Id = id,
                            Fields = new Dictionary<string, object>(),
                            Line = registration.Line,
                            Column = registration.Column
                        }
                    }
                };
            }));
            ContentCompilation duplicate = ContentCompiler.Compile("custom_def(\"Registered\")\ncustom_def(\"Registered\")");
            check(!duplicate.Success && duplicate.Plan == null
                && duplicate.Diagnostics.Count == 1
                && duplicate.Diagnostics[0].Line == 2
                && duplicate.Diagnostics[0].Message.Contains("declared more than once"),
                "registered declaration duplicate id is rejected at its source position");
        }

        static ContentCompilation Compile(string source)
        {
            ContentCompilation result = ContentCompiler.Compile(source);
            if (!result.Success) throw new InvalidOperationException(string.Join("; ", result.Diagnostics));
            return result;
        }

        static void Reject(string source, Action<bool, string> check, string reason)
        {
            ContentCompilation result = ContentCompiler.Compile(source);
            check(!result.Success && result.Plan == null, reason);
        }

        sealed class MutableFacts : IOxygenHostValue
        {
            public bool Hungry;
            public bool Sick;
            public bool TryGetMember(string name, out object value)
            {
                value = null;
                if (name == "young_hungry") { value = Hungry; return true; }
                if (name == "young_sick") { value = Sick; return true; }
                return false;
            }
        }
    }
}
