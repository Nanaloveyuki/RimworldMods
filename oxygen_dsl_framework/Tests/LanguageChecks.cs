using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class LanguageChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            Expect(check, "let x = 1; x = \"one\"; x", "one", "unannotated bindings remain dynamic");
            Expect(check, "fn a(b: int) {}; a(1)", null, "unannotated returns allow implicit null");
            Expect(check, "fn add(a: int, b: int): int { return a + b }; add(1, b: 2)", 3, "typed named function result");
            Expect(check, "let x: float = 1; x", 1d, "integer widening yields float");
            Expect(check, "let x: string? = null; x = \"text\"; x", "text", "nullable binding accepts both alternatives");
            Fail(check, "let x: int = \"1\"", "text does not satisfy integer annotation");
            Fail(check, "fn f(x: int) { x = \"bad\" }; f(1)", "parameter constraint survives assignment");
            Fail(check, "fn f(): int {}; f()", "implicit null violates integer return");
            Expect(check, "fn f(): int { return 1; let unused = 2 }; f()", 1, "reachable return is not replaced by unreachable fallthrough");
            Fail(check, "fn f(value): int { if value { return 1 } }; f(false)", "uncertain fallthrough checks actual implicit null");
            Fail(check, "fn f(): void { return 1 }; f()", "void rejects value returns");
            Fail(check, "fn f(a, b) {}; f(a: 1, 2)", "positionals cannot follow named arguments");
            Fail(check, "fn f(a, b) {}; f(1, a: 2, b: 3)", "parameter cannot bind twice");
            Fail(check, "fn ignore(_: int) {}; let value = \"bad\"; ignore(value)", "discarded parameters still enforce their declared type");
            Fail(check, "let x: Missing = null", "unknown annotation fails without use");
            Fail(check, "let x: list<void> = []", "void is not an element type");
            Fail(check, "fn outer() { let x: Missing = null }; 1", "unused function annotations validate at preparation");
            Fail(check, "struct Bad { x: Missing }; 1", "unused struct field annotation validates");
            Fail(check, "use cs@std\nfn unused() { let value: cs.Missing = null }; 1", "unused imported annotations validate before execution");
            Fail(check, "let int = 1", "builtin type names cannot be shadowed");
            Expect(check, "int(\"42\") + int(2.0)", 44, "explicit integer conversion");
            Fail(check, "int(1.5)", "fractional integers are not rounded");
            Fail(check, "int(true)", "boolean is not a numeric conversion");
            Fail(check, "float(null)", "null is not a numeric conversion");
            Expect(check, "\"2\" + 1", "21", "text concatenation retains text conversion");
            Fail(check, "\"2\" - 1", "arithmetic does not parse numeric text");
            Fail(check, "2147483647 + 1", "integer addition is checked");
            Expect(check, "-2147483648", int.MinValue, "signed integer minimum literal");
            Fail(check, "-(-2147483648)", "integer negation is checked");
            Fail(check, "2147483648", "positive out of range literal rejects");
            Fail(check, "2147483647 * 2", "integer multiplication is checked");
            Fail(check, "1 / 0", "division by zero fails");
            Expect(check, "let x = 0; if 1 { x = 7 }; x", 7, "condition truthiness stays separate from annotation");
            Fail(check, "let x: bool = 1", "boolean annotation is strict");
            Expect(check, "let xs: list<string> = split(\"A,B\", \",\"); xs[1]", "B", "standard split preserves string element type");
            Expect(check, "let xs: list<int> = range(0, 3); xs[2]", 2, "standard range preserves integer element type");
            Expect(check, "let first: int = 1; let second: int = 2; first, second = second, first; first * 10 + second", 21, "typed multiassignment accepts ordered value snapshot");
            Fail(check, "let first: int = 1; let second = 2; first, second = \"bad\", 3", "multiassignment cannot bypass typed binding constraints");
            Expect(check, "fn pair(value) { return value, value + 1 }; let first, second = pair(3); first * 10 + second", 34, "multi-return list supports declaration unpacking");
            Expect(check, "fn take(\n xs: array<int>,\n): int { return xs[0] }; take(\n [\n 3,\n ],\n)", 3, "multiline delimiters retain contextual literal typing");
            Expect(check, "bool(\" \" )", false, "whitespace text is false");
            Expect(check, "let xs: list<int> = [1]; xs.add(2); xs[0] += 3; xs.remove_at(1); xs[0]", 4, "typed list mutation preserves element semantics");
            Expect(check, "let xs: array<int> = [1, 2]; xs[1] = 7; xs[1]", 7, "fixed array elements are mutable");
            Fail(check, "let xs: array<int> = []; xs[0] = 1", "empty array cannot expand by indexing");
            Fail(check, "let xs: array<int> = [1]; xs.add(2)", "array cannot append");
            Fail(check, "let xs: list<int> = [1]; let alias: any = xs; alias.add(\"bad\")", "any alias retains element constraint");
            Fail(check, "let xs = [1]; let typed: list<int> = xs", "existing dynamic list is not relabeled");
            Fail(check, "let xs: list<int> = [1]; let wide: list<float> = xs", "mutable collection types are invariant");
            Expect(check, "let xs = [1, 2]; let typed = array<int>(xs); typed[0] = 9; xs[0]", 1, "explicit collection conversion creates independent container");
            Expect(check, "fn first(xs: array<int>): int { return xs[0] }; first([3])", 3, "direct literals inherit array argument context");
            Expect(check, "let xs: list<array<int>> = [[1], [2]]; xs[1][0]", 2, "nested literal constraints recurse");
            Expect(check, "let xs: list<array<int>> = [[1]]; xs[0] = [2]; xs[0][0]", 2, "indexed literal assignment inherits nested array constraint");
            Expect(check, "let xs = [0]; let order = 0; fn index(): int { order = order * 10 + 1; return 0 }; fn value(): int { order = order * 10 + 2; return 3 }; xs[index()] = value(); order", 12, "lvalue index evaluates before assignment value");
            Expect(check, "let xs: list<int?> = [null, 2]; xs[0]", null, "nullable elements differ from nullable collection");
            Fail(check, "let xs: list<int> = [1]; xs[1] = 2", "list index assignment cannot implicitly grow");
            Fail(check, "let xs = [1]; xs[-1]", "negative index rejects instead of wrapping");
            Fail(check, "let xs = [1]; xs[0.0]", "index type is strictly integer");
            Expect(check, "let xs = [null]; exists(xs[0]) && !exists(xs[1])", true, "collection existence distinguishes present null from missing index");
            Expect(check, "let o = {level: 1}; o.level += 2; o[\"enabled\"] = true; o.level", 3, "dynamic object fields are writable");
            Fail(check, "{key: 1, key: 2}", "duplicate object keys do not overwrite");
            Fail(check, "let o = {}; o.missing", "missing object key is an error");
            Expect(check, "len({})", 0, "object key count handles empty object");

            const string structs = "struct Stats { health: int = 100; tags: list<string> = []; fn alive(): bool { return self.health > 0 } }\n"
                + "struct Actor { name: string; embed stats: Stats = Stats() }\n";
            Expect(check, structs + "let a = Stats(); let b = a; b.health = 70; a.health", 70, "struct assignment aliases instance");
            Expect(check, structs + "let a = Stats(); let b = Stats(); a.tags.add(\"A\"); len(b.tags)", 0, "mutable field defaults are fresh");
            Expect(check, structs + "let a = Actor(name: \"A\"); a.health = 0; a.alive()", false, "embedded methods bind embedded receiver");
            Expect(check, structs + "let a = Actor(name: \"A\"); let saved = a.alive; a.stats = Stats(health: 0); saved() && !a.alive()", true, "saved bound method keeps original receiver");
            Expect(check, structs + "let a = Stats(); let b = Stats(); a != b && a == a", true, "struct equality is identity");
            Expect(check, "struct State { slots: array<int> = [1] }; let state = State(); state.slots = [2]; state.slots[0]", 2, "member literal assignment inherits array field constraint");
            Expect(check, "struct Slots { slots: array<int> = [1] }; struct Owner { embed value: Slots = Slots() }; let owner = Owner(); owner.slots = [2]; owner.slots[0]", 2, "promoted field assignment retains literal context");
            Expect(check, "struct State { fn read(values: array<int>): int { return values[0] } }; State().read([3])", 3, "bound methods provide contextual array argument typing");
            Expect(check, "struct State { fn read(values: array<int>): int { return values[0] } }; let state = State(); let saved = state.read; saved([4])", 4, "saved method retains contextual parameter types");
            Fail(check, structs + "Actor(name: \"A\", health: 80)", "promoted fields are not constructor parameters");
            Fail(check, structs + "Actor()", "required constructor field is enforced");
            Fail(check, structs + "Stats(1)", "struct constructor only accepts names");
            Fail(check, structs + "let a = Stats(); a.extra = 1", "struct shape is fixed");
            Fail(check, structs + "let a: Stats = {health: 100, tags: []}", "object is not a nominal struct");
            Fail(check, structs + "struct Other { health: int = 100; tags: list<string> = [] }; let a: Stats = Other()", "struct typing is nominal not structural");
            Fail(check, "struct Stats { health: int = 1 }; struct Bad { health: int = 2; embed stats: Stats }", "direct and promoted members conflict at declaration");
            Fail(check, "struct A { embed b: B }; struct B { embed a: A }", "embed cycles reject unconstructed declarations");
            Expect(check, "struct Node { next: Node? = null }; let a = Node(); a.next = a; a.next == a", true, "ordinary nullable fields allow reference cycles");
            Fail(check, "struct Bad { embed x: any }", "embedded type must be a nonnullable struct");

            var modules = new OxygenModuleRegistry();
            modules.RegisterSource("example.balance", "helpers", "pub struct Stats { bonus: int = 5 }; pub const amount = 7; pub fn plus(value: int): int { return value + amount }; fn hidden() { return 99 }");
            Expect(check, "use helpers@EXAMPLE.BALANCE as h\nh.plus(h.Stats().bonus)", 12, "module provider identity and exports", modules);
            Expect(check, "use helpers@example.balance as h\nuse helpers@example.balance as other\nlet value: other.Stats = h.Stats(); value.bonus", 5, "nominal imported type ignores alias", modules);
            Fail(check, "use helpers@example.balance as h\nh.hidden()", "private functions are not exported", modules);
            Fail(check, "use helpers@example.balance as h\nh.amount = 1", "namespace writes cannot replace an exported constant", modules);
            Fail(check, "use math@std\nmath.clamp = 1", "standard namespace writes cannot replace a function");
            Fail(check, "use missing@example.balance\n1", "missing module is not guessed", modules);
            Fail(check, "use math@std as int", "module alias cannot shadow type");
            Fail(check, "let x = 1\nuse math@std", "imports must precede declarations");
            Fail(check, "fn f() { use math@std }; f()", "imports cannot appear inside functions");
            Fail(check, "pub let x = 1", "mutable public globals reject");
            Fail(check, "use some.mod", "old unqualified imports reject");
            modules.RegisterSource("example.balance", "a", "use b@example.balance\npub fn value() { return b.value() }");
            modules.RegisterSource("example.balance", "b", "use a@example.balance\npub fn value() { return a.value() }");
            Fail(check, "use a@example.balance\na.value()", "module cycle rejects", modules);
            modules.RegisterSource("example.balance", "state", "let count = 0; pub fn next(): int { count += 1; return count }");
            Expect(check, "use state@example.balance as a\nuse state@example.balance as b\na.next() + b.next()", 3, "aliases share one module instance", modules);
            Expect(check, "use state@example.balance\nstate.next()", 1, "module state is isolated across script instances", modules);
            modules.RegisterSource("example.balance", "spin", "pub fn run() { while true {} }");
            Fail(check, "use spin@example.balance\nspin.run()", "module calls share execution budget", modules);
            modules.RegisterSource("example.balance", "defaults", "const amount = 6; fn default_value(): int { return amount }; pub struct State { value: int = default_value(); names: list<string> = [] }");
            Expect(check, "use defaults@example.balance\nlet state = defaults.State(names: [\"A\"]); state.value", 6, "module struct defaults capture defining scope", modules);
            modules.RegisterSource("example.balance", "forward", "pub fn make(): Item { return Item() }; pub struct Item { value: int = 4 }");
            Expect(check, "use forward@example.balance\nlet value: forward.Item = forward.make(); value.value", 4, "module function signatures resolve later nominal declarations", modules);
            modules.RegisterSource("example.balance", "outer", "use helpers@example.balance\npub struct Stats { bonus: int = 6 }; pub fn make(): Stats { return Stats() }");
            Expect(check, "use outer@example.balance\nlet value: outer.Stats = outer.make(); value.bonus", 6, "nested imports do not replace importing module type identity", modules);
            Fail(check, "use outer@example.balance\nuse helpers@example.balance\nlet value: outer.Stats = helpers.Stats()", "same named types from different modules remain nominally distinct", modules);
            modules.RegisterSource("example.balance", "broken", "pub fn incomplete(");
            ArtiExecutionResult moduleFailure = new ArtiExecutor().Execute("use broken@example.balance", new ArtiExecutionContext(modules: modules));
            check(moduleFailure.HasErrors && moduleFailure.Diagnostics[0].MessageEn.Contains("broken@example.balance"), "imported syntax diagnostics identify original module");
            modules.RegisterSource("example.balance", "runtime_error", "pub fn value(): int { return 1 / 0 }");
            moduleFailure = new ArtiExecutor().Execute("use runtime_error@example.balance\nruntime_error.value()", new ArtiExecutionContext(modules: modules));
            check(moduleFailure.HasErrors && moduleFailure.Diagnostics[0].MessageEn.Contains("runtime_error@example.balance"), "imported runtime diagnostics identify defining module");
            Expect(check, "optional use missing@example.balance\nif missing.available { 2 } else { 1 }", 1, "optional absent module reports unavailable");
            Expect(check, "optional use helpers@example.balance\nif helpers.available { helpers.plus(1) } else { 0 }", 8, "optional present module remains available", modules);
            Fail(check, "optional use missing@example.balance as absent\nuse missing@example.balance as required\n1", "mandatory import of cached missing module still errors", modules);
            modules.RegisterSource("example.balance", "corrupt", "pub fn broken(");
            ArtiExecutionResult optionalFailure = new ArtiExecutor().Execute("optional use corrupt@example.balance\n1", new ArtiExecutionContext(modules: modules));
            check(optionalFailure.HasErrors && optionalFailure.Diagnostics[0].MessageEn.Contains("corrupt@example.balance"), "optional import does not mask corrupt source");
            optionalFailure = new ArtiExecutor().Execute("optional use boom@example.fault\n1", new ArtiExecutionContext(modules: new ThrowingModuleCatalog()));
            check(optionalFailure.HasErrors && optionalFailure.Diagnostics[0].MessageEn.Contains("provider failed"), "optional import does not mask provider exceptions");
            var hostModules = new OxygenHostModuleRegistry();
            hostModules.Register("example.host", "bridge", new DictionaryOxygenHostValue(new Dictionary<string, object>
            {
                { "label", "ready" },
                { "Missing", "not-a-type" }
            }));
            Expect(check, "optional use bridge@example.host\nif bridge.available { bridge.label } else { \"missing\" }", "ready", "host module namespace is available", null, hostModules);
            Fail(check, "optional use bridge@example.host\nfn unused() { let value: bridge.Missing = null }; if false { unused() }; 1", "guarded unknown host type rejects at preparation", null, hostModules);
            Fail(check, "optional use missing@std\nfn unused() { let value: missing.Type = null }; if false { unused() }; 1", "guarded unknown optional type rejects at preparation");
            Fail(check, "optional use absent@missing.mod as cs\nfn unused(value: cs.Int32) {}\n1", "missing module cannot gain native types through cs alias");
            ArtiExecutor callbackExecutor = new ArtiExecutor();
            ArtiExecutionResult preparedCallback = callbackExecutor.Execute("fn twice(value: int): int { return value + value }; twice", new ArtiExecutionContext());
            if (preparedCallback.HasErrors) throw new InvalidOperationException("callback preparation failed: " + string.Join("; ", preparedCallback.Diagnostics));
            check(Equals(callbackExecutor.InvokePrepared(preparedCallback.Value, 3), 6), "prepared script callback uses a fresh budget");
            check(Equals(callbackExecutor.InvokePrepared(preparedCallback.Value, 4), 8), "prepared script callback can be invoked again");
            Expect(check, "struct Actor { embed stats: Stats = Stats() }; struct Stats { health: int = 100 }; Actor().health", 100, "forward embedded type promotes members after all definitions");
            Fail(check, "struct Actor { health: int = 0; embed stats: Stats }; struct Stats { health: int = 100 }", "forward embedded conflicts fail before constructor");
            Expect(check, "fn make(): array<int> { return [1] }; let xs: array<int> = make(); xs = [2]; xs[0]", 2, "return and reassignment literal contexts");
            Expect(check, "let xs: array<int> = [1]; let ys: list<array<int>> = []; ys.add([2]); ys[0][0]", 2, "collection add inherits nested element context");
            Expect(check, "use math@std\nmath.clamp(2, 0, 1)", 1d, "math extension widens integer arguments");
            Fail(check, "use math@std\nmath.clamp(1, 2, 0)", "clamp rejects inverted interval");
            Expect(check, "use logic@std\nlogic.all([]) && !logic.any([])", true, "empty logical collections honor identities");
            Expect(check, "use logic@std\nlogic.all([true, false])", false, "logic arguments inherit bool collection context");
            Fail(check, "use logic@std\nlogic.all([1])", "logical collection annotation remains strict");
            Fail(check, "use logic@std\nlet values = [true]; logic.all(values)", "logical fold rejects existing list<any> rather than relabeling");
            Expect(check, "use cs@std\nlet native: cs.Int32 = cs.int32(42); int(native)", 42, "controlled Csharp wrapper explicitly unwraps");
            Fail(check, "use cs@std\nlet native: cs.Int32 = 42", "Csharp wrapper is not an implicit alias");
            Fail(check, "use cs@std\ncs.GetType()", "Csharp namespace does not expose reflection");
            ScriptCompilation hostAlias = ScriptCompiler.Compile("fn mutate(p) { p.food = 1 }; fn check(pawn) { mutate(pawn); return true }\nmood(\"alias\", label: \"alias\", description: \"alias\", offset: 1, target: \"colonists\", when: check)");
            if (!hostAlias.Success) throw new InvalidOperationException("Host alias script compilation failed: " + string.Join("; ", hostAlias.Diagnostics));
            var protectedPawn = new PawnContext { Food = 0.5 };
            try
            {
                hostAlias.Program.Rules[0].Condition.Evaluate(protectedPawn, new GameContext());
                check(false, "readonly host alias mutation must fail");
            }
            catch (ScriptEvaluationException)
            {
                check(protectedPawn.Food == 0.5, "readonly host writes remain guarded through aliases");
            }

            var executor = new ArtiExecutor();
            ArtiExecutionResult rejectedMutation = executor.Execute("fn bad() { return \"bad\" }; let xs: list<int> = [1]; xs[0] = bad()");
            check(rejectedMutation.HasErrors, "invalid collection mutation reports failure");
            ArtiExpression element = ((ArtiExpressionStatement)new ArtiParser().Parse("xs[0]").Program.Statements[0]).Expression;
            check(Equals(executor.EvaluatePrepared(element), 1), "failed element mutation retains old value");
            rejectedMutation = executor.Execute("fn bad() { return \"bad\" }; let first: int = 1; let second: int = 2; first, second = 3, bad()");
            check(rejectedMutation.HasErrors, "invalid multiassignment reports failure");
            ArtiExpression snapshot = ((ArtiExpressionStatement)new ArtiParser().Parse("first * 10 + second").Program.Statements[0]).Expression;
            check(Equals(executor.EvaluatePrepared(snapshot), 12), "failed multiassignment preserves every prior binding");
            rejectedMutation = executor.Execute("struct State { value: int = 1 }; let state = State(); fn bad() { return \"bad\" }; state.value = bad()");
            check(rejectedMutation.HasErrors, "invalid struct field write reports failure");
            ArtiExpression field = ((ArtiExpressionStatement)new ArtiParser().Parse("state.value").Program.Statements[0]).Expression;
            check(Equals(executor.EvaluatePrepared(field), 1), "failed field write preserves prior field value");

            for (int value = -32; value <= 32; value++)
            {
                string text = value.ToString(CultureInfo.InvariantCulture);
                Expect(check, "fn f(value: int): int { return value }; f(" + text + ")", value, "integer argument family " + text);
                Expect(check, "let xs: array<int> = [" + text + "]; xs[0] += 1; xs[0]", value + 1, "array update family " + text);
            }
            var random = new Random(20261009);
            for (int trace = 0; trace < 20; trace++)
            {
                var model = new List<int>();
                var script = new StringBuilder("let xs: list<int> = []; ");
                int checksum = 0;
                for (int step = 0; step < 30; step++)
                {
                    int value = random.Next(-100, 101);
                    int action = model.Count == 0 ? 0 : random.Next(3);
                    int index = model.Count == 0 ? 0 : random.Next(model.Count);
                    if (action == 0)
                    {
                        model.Add(value);
                        script.Append("xs.add(").Append(value).Append("); ");
                    }
                    else if (action == 1)
                    {
                        model[index] = value;
                        script.Append("xs[").Append(index).Append("] = ").Append(value).Append("; ");
                    }
                    else
                    {
                        model.RemoveAt(index);
                        script.Append("xs.remove_at(").Append(index).Append("); ");
                    }
                    for (int i = 0; i < model.Count; i++)
                        script.Append("if xs[").Append(i).Append("] != ").Append(model[i]).Append(" { return false }; ");
                    script.Append("if len(xs) != ").Append(model.Count).Append(" { return false }; ");
                    checksum += model.Count;
                    for (int i = 0; i < model.Count; i++) checksum += model[i];
                }
                Expect(check, "fn verify(): bool { " + script + "return true }; verify()", true,
                    "list state model seed 20261009 trace " + trace + " checksum " + checksum);
            }
        }

        private static void Expect(Action<bool, string> check, string source, object expected, string scenario, IOxygenModuleCatalog modules = null, IOxygenModuleProvider hostModules = null)
        {
            ArtiExecutionResult result = new ArtiExecutor().Execute(source, new ArtiExecutionContext(modules: modules, hostModules: hostModules));
            if (result.HasErrors) throw new InvalidOperationException(scenario + ": " + string.Join("; ", result.Diagnostics));
            check(Equals(result.Value, expected), scenario + ": expected " + expected + ", received " + result.Value);
        }

        private static void Fail(Action<bool, string> check, string source, string scenario, IOxygenModuleCatalog modules = null, IOxygenModuleProvider hostModules = null)
        {
            ArtiExecutionResult result = new ArtiExecutor().Execute(source, new ArtiExecutionContext(modules: modules, hostModules: hostModules));
            check(result.HasErrors, scenario);
            bool positioned = false;
            foreach (ArtiDiagnostic diagnostic in result.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error && diagnostic.Span.Line >= 1 && diagnostic.Span.Column >= 1) positioned = true;
            check(positioned, scenario + " has source location");
        }

        private sealed class ThrowingModuleCatalog : IOxygenModuleCatalog
        {
            public bool TryGetSource(string packageId, string modulePath, out string source)
            {
                throw new InvalidOperationException("provider failed");
            }
        }
    }
}
