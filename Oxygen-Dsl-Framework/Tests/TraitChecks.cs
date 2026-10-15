using System;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class TraitChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            const string named = "trait Named { fn name(): string }\n"
                + "struct Point { x: int = 0; y: int = 0 }\n"
                + "impl Point { fn label(): string { return string(self.x) + \",\" + string(self.y) } static fn origin(): Self { return Point(x: 0, y: 0) } }\n"
                + "impl Named for Point { fn name(): string { return self.label() } }\n";

            Expect(check, named + "let point = Point(x: 3, y: 4); point.label()", "3,4", "inherent impl callback uses instance self");
            Expect(check, named + "Point.origin().label()", "0,0", "static impl is callable on the concrete type");
            Fail(check, named + "let point = Point(x: 1, y: 2); point.origin()", "static method is not available through an instance");
            Expect(check, named + "let point: Point = Point.origin(); point.name()", "0,0", "static Self return satisfies the concrete annotation");
            Fail(check, named + "origin()", "static method is not promoted to a free function");
            Fail(check, "struct Box { value: int = 1; static fn make(): int { return 1 } }\nBox().make()", "static method is not available as an instance member");
            Fail(check, "static fn free(): int { return 1 }", "static is rejected on an ordinary function");
            Fail(check, "fn outer() { trait Nested { fn value(): int }; 1 }", "trait cannot be declared inside a function");
            Fail(check, "fn outer() { struct Local { value: int = 1 }; impl Local { fn read(): int { return self.value } }; 1 }", "impl cannot be declared inside a function");

            Fail(check, "trait Broken { fn value(item) }", "trait parameter requires an explicit type");
            Fail(check, "trait Broken { fn value(item: int) }", "trait method requires an explicit return type");
            Fail(check, "trait Broken { fn value(item: any): void { return } }", "trait method cannot have a body");
            Fail(check, "trait Broken { fn value<T: Named>(item: T): T }", "trait method cannot be generic");
            Expect(check, "trait Echo { fn echo(value: any): any; static fn blank(): void }\nstruct Unit { }\nimpl Echo for Unit { fn echo(value: any): any { return value } static fn blank(): void { } }\nUnit().echo(7)", 7, "trait signatures accept any and void");
            Fail(check, named + "impl Named for Point { fn name(value: string): string { return value } }", "trait impl rejects a different parameter name");
            Fail(check, named + "impl Named for Point { fn name(): int { return self.x } }", "trait impl rejects a different return type");
            Fail(check, named + "impl Named for Point { static fn name(): string { return \"x\" } }", "trait impl rejects a static mismatch");
            Fail(check, named + "impl Named for Point { fn name(): string { return \"x\" } fn extra(): int { return 1 } }", "trait impl rejects an extra method");
            Fail(check, named + "struct Other { x: int = 1; fn name(): string { return \"x\" } }\nfn describe<T: Named>(value: T): string { return value.name() }\ndescribe<Other>(Other())", "inherent method is not an implicit trait implementation");
            Fail(check, "trait Named { fn name(): string }\nstruct Point { x: int = 1 }\nimpl Named for Point { fn name(): string { return \"a\" } }\nimpl Named for Point { fn name(): string { return \"b\" } }\n1", "duplicate trait implementation is rejected");

            const string generic = named
                + "fn describe<T: Named>(value: T): string { return value.name() }\n";
            Expect(check, generic + "let value: Point = Point(x: 8, y: 1); describe(value)", "8,1", "generic type is inferred from an annotated variable");
            Expect(check, generic + "describe(Point(x: 2, y: 9))", "2,9", "generic type is inferred from a constructor expression");
            Expect(check, generic + "fn make(): Point { return Point(x: 5, y: 6) }\ndescribe(make())", "5,6", "generic type is inferred from an explicit return");
            Expect(check, generic + "let loose = Point(x: 4, y: 4); describe<Point>(loose)", "4,4", "explicit specialization checks the actual value");
            Fail(check, generic + "let loose = Point(x: 1, y: 1); describe(loose)", "unannotated value does not infer a generic type");
            Fail(check, generic + "fn make() { return Point(x: 1, y: 1) }\ndescribe(make())", "unannotated return does not infer a generic type");
            Fail(check, generic + "describe<Point>(\"bad\")", "explicit specialization rejects a value of another type");
            Fail(check, generic + "fn unused<T: Named>(value: T): int { if false { return value.missing }; return 1 }\n1", "uncalled generic rejects a member outside the trait");
            Fail(check, "fn describe<T>(value: T): int { return 1 }", "generic parameter requires one trait bound");
            Fail(check, named + "struct Box { fn read<T: Named>(value: T): string { return value.name() } }\n1", "generic methods are not supported");
            Fail(check, "fn collide<T: Named, T: Named>(left: T, right: T): int { return 1 }", "duplicate generic parameter names are rejected");
            Expect(check, "1 < 2 && !(3 < 1)", true, "comparison remains distinct from a generic call");

            const string pair = "trait Named { fn name(): string }\n"
                + "struct Point { x: int = 0 }\n"
                + "struct Label { text: string = \"\" }\n"
                + "impl Named for Point { fn name(): string { return string(self.x) } }\n"
                + "impl Named for Label { fn name(): string { return self.text } }\n"
                + "fn pair<T: Named, U: Named>(left: T, right: U): list<string> { return [left.name(), right.name()] }\n"
                + "fn boxes<T: Named>(values: list<array<T>>): array<string> { return [values[0][0].name()] }\n";
            Expect(check, pair + "let left: Point = Point(x: 3); let right: Label = Label(text: \"z\"); pair(left, right)[1]", "z", "two generic parameters infer from annotated arguments");
            Expect(check, pair + "let values: list<array<Point>> = [[Point(x: 6)]]; boxes(values)[0]", "6", "generic inference preserves nested collection constraints");
            const string factory = "trait Factory { static fn create(name: string): Self }\n"
                + "struct A { name: string }; struct B { name: string }\n"
                + "impl Factory for A { static fn create(name: string): Self { return Self(name: name) } }\n"
                + "impl Factory for B { static fn create(name: string): Self { return Self(name: name + \"!\") } }\n"
                + "fn make<T: Factory>(name: string): T { return T.create(name) }\n";
            Expect(check, factory + "make<A>(\"a\").name + make<B>(\"b\").name", "ab!", "static generic factories have separate Self specializations");
            Fail(check, factory + "fn unused<T: Factory>(): int { return T.create(\"x\").missing }; 1", "static Self return retains the trait member restriction");
            Expect(check, generic + "fn forward<T: Named>(value: T): string { return describe(value) }; forward(Point(x: 5, y: 2))", "5,2", "generic forwarding binds a concrete specialization");
            Fail(check, generic + "fn unused<T: Named>(value: T): string { let alias = value; return describe(alias) }; 1", "dynamic aliases do not silently acquire generic inference");
            Fail(check, generic + "fn unused<T: Named>(value: T): int { let alias = 0; alias = value; return alias.missing }; 1", "assignment aliases cannot bypass generic capabilities");
            Fail(check, generic + "fn unused<T: Named>(value: T): string { let T = 1; return value.name() }; 1", "generic type parameter cannot be shadowed by a value");
            Expect(check, "struct Link { next: Self? = null }; impl Link { fn same(other: Self): bool { return self == other } }; let value: Link = Link(); value.same(value)", true, "Self fields and method parameters resolve to their enclosing struct");
            Expect(check, "trait First { fn label(): int }; trait Second { fn label(): int }; struct One {} ; struct Two {}; impl First for One { fn label(): int { return 1 } }; impl Second for Two { fn label(): int { return 2 } }; One().label() + Two().label()", 3, "same trait member names are allowed on different concrete types");
            Fail(check, "trait Echo { fn echo(value: int): int }; struct Unit {}; impl Echo for Unit { fn echo(other: int): int { return other } }; 1", "trait implementation parameter names preserve named-call contract");

            var modules = new OxygenModuleRegistry();
            modules.RegisterSource("example.traits", "shapes",
                "pub trait Named { fn name(): string }\n"
                + "pub struct Point { x: int = 1 }\n"
                + "impl Named for Point { fn name(): string { return string(self.x) } }\n"
                + "impl Point { static fn zero(): Self { return Point(x: 0) } }\n");
            modules.RegisterSource("example.traits", "other",
                "pub trait Named { fn name(): string }\n");
            Expect(check, "use shapes@example.traits as left\nuse shapes@example.traits as right\nfn describe<T: left.Named>(value: T): string { return value.name() }\nlet value: right.Point = left.Point(x: 9); describe(value)", "9", "trait identity ignores the import alias", modules);
            Expect(check, "use shapes@example.traits as shapes\nshapes.Point.zero().x", 0, "exported struct carries its static implementation", modules);
            modules.RegisterSource("example.traits", "generic", "use shapes@example.traits as shapes\npub fn describe<T: shapes.Named>(value: T): string { return value.name() }");
            Expect(check, "use shapes@example.traits as shapes\nuse generic@example.traits as helper\nhelper.describe(shapes.Point(x: 11))", "11", "exported generic function retains its defining trait contract", modules);
            Fail(check, "use shapes@example.traits as shapes\nuse other@example.traits as other\nfn describe<T: other.Named>(value: T): string { return value.name() }\nlet value: shapes.Point = shapes.Point(); describe(value)", "same trait name from another module is a different trait", modules);
            Fail(check, "use shapes@example.traits as shapes\nimpl shapes.Named for shapes.Point { fn name(): string { return \"foreign\" } }\n1", "impl cannot extend a struct from another module", modules);
            Fail(check, "pub impl Point { fn hidden(): int { return 1 } }", "impl cannot be exported");
            Fail(check, "trait Named { fn name(): string }\ntrait Alias { fn name(): int }\nstruct Point { x: int = 1 }\nimpl Named for Point { fn name(): string { return \"a\" } }\nimpl Alias for Point { fn name(): int { return self.x } }\n1", "two traits cannot require the same member name");

            const string embedded = "trait Named { fn name(): string }\n"
                + "struct Stats { health: int = 4; fn read(): int { return self.health }; static fn baseline(): int { return 1 } }\n"
                + "impl Named for Stats { fn name(): string { return string(self.health) } }\n"
                + "struct Actor { embed stats: Stats = Stats() }\n"
                + "fn describe<T: Named>(value: T): string { return value.name() }\n";
            Expect(check, embedded + "Actor().read()", 4, "embed forwards an instance method");
            Fail(check, embedded + "Actor.baseline()", "embed does not promote a static method");
            Fail(check, embedded + "describe(Actor())", "embed does not forward trait identity");

            Fail(check, "optional use missing@example.traits\nfn unused<T: missing.Named>(value: T): int { return 1 }\n1", "optional missing trait fails preparation");
            Fail(check, "fn outside(value: int): Self { return value }", "Self is not available outside struct or impl");
            Fail(check, named + "impl Point { fn bad(self: int): int { return self } }", "method parameter cannot be named self");

            Expect(check, "let first = 1 /* same line */ + 2; first", 3, "same-line block comment is not a statement separator");
            Expect(check, "let first = 1\n/* between\nstatements */\nlet second = first + 4; second", 5, "block comment newline separates statements");
            Expect(check, "let first = 1\r\n/* crlf */\r\nlet second = 6; second", 6, "CRLF inside a block comment is one separator");
            Expect(check, "let text = \"/* not a comment */\"; text", "/* not a comment */", "block comment delimiters stay literal in a string");
            Expect(check, "let text = f\"/* kept */{1}\"; text", "/* kept */1", "block comment delimiters stay literal in interpolation text");
            Expect(check, "let nested = 1 /* outer /* still */ + 2; nested", 3, "block comments do not nest");
            Fail(check, "let broken = 1 /* unterminated\nlet next = 2", "unterminated block comment reports its opening position");
            Expect(check, "let a = 1 /* c */ ; let b = a + 1; b", 2, "same-line block comment still allows an explicit separator");
            Expect(check, "f\"value={1 /* } \\\" { */ + 2}\"", "value=3", "interpolation comments do not close braces or strings");
            ArtiLexResult unterminated = new ArtiLexer().Lex("let a = 1\r\n  /* missing");
            check(unterminated.Diagnostics.Count == 1 && unterminated.Diagnostics[0].Code == "ART1001"
                && unterminated.Diagnostics[0].Span.Line == 2 && unterminated.Diagnostics[0].Span.Column == 3,
                "unterminated block comment locates its opening delimiter after CRLF");
        }

        private static void Expect(Action<bool, string> check, string source, object expected, string scenario, IOxygenModuleCatalog modules = null)
        {
            ArtiExecutionResult result = new ArtiExecutor().Execute(source, new ArtiExecutionContext(modules: modules));
            if (result.HasErrors) throw new InvalidOperationException(scenario + ": " + string.Join("; ", result.Diagnostics));
            check(Equals(result.Value, expected), scenario + ": expected " + expected + ", received " + result.Value);
        }

        private static void Fail(Action<bool, string> check, string source, string scenario, IOxygenModuleCatalog modules = null)
        {
            ArtiExecutionResult result = new ArtiExecutor().Execute(source, new ArtiExecutionContext(modules: modules));
            check(result.HasErrors, scenario);
            bool positioned = false;
            foreach (ArtiDiagnostic diagnostic in result.Diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error && diagnostic.Span.Line >= 1 && diagnostic.Span.Column >= 1) positioned = true;
            check(positioned, scenario + " has source location");
        }
    }
}
