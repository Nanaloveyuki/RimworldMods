using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Oxygendsl.Language;

namespace Oxygendsl.Editor
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static int Main()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            while (true)
            {
                string line;
                try
                {
                    line = Console.In.ReadLine();
                }
                catch (Exception exception)
                {
                    WriteProtocolError(0, exception);
                    return 1;
                }

                if (line == null)
                {
                    return 0;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                try
                {
                    WriteResponse(AnalyzeRequest(line));
                }
                catch (Exception exception)
                {
                    WriteProtocolError(0, exception);
                }
            }
        }

        private static EditorResponse AnalyzeRequest(string line)
        {
            EditorRequest request;
            try
            {
                request = JsonSerializer.Deserialize<EditorRequest>(line, JsonOptions);
            }
            catch (JsonException exception)
            {
                return ProtocolError(0, "invalid request JSON: " + exception.Message);
            }

            if (request == null)
            {
                return ProtocolError(0, "request must be a JSON object");
            }

            if (request.Language != "zh-CN" && request.Language != "en")
            {
                return ProtocolError(request.Id, "language must be zh-CN or en");
            }

            string source = request.Source ?? string.Empty;
            try
            {
                return AnalyzeSource(request.Id, source, request.IsModule, request.Language, request.AutoMode);
            }
            catch (Exception exception)
            {
                return ProtocolError(request.Id, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static EditorResponse AnalyzeSource(int id, string source, bool isModule, string language, bool autoMode)
        {
            int utf16Length = source.Length;
            ArtiParseResult parsed = new ArtiParser().Parse(source);
            List<EditorDiagnostic> diagnostics = new List<EditorDiagnostic>();
            AppendDiagnostics(diagnostics, parsed.Diagnostics, language, utf16Length);
            if (!parsed.HasErrors && parsed.Program != null)
            {
                ArtiAnalysisResult analyzed = new ArtiAnalyzer().Analyze(parsed.Program, autoMode ? OxygenFileCompiler.IsLibrary(parsed.Program) : isModule);
                AppendDiagnostics(diagnostics, analyzed.Diagnostics, language, utf16Length);
            }

            List<EditorSymbol> symbols = Outline.Extract(parsed.Program, parsed.Tokens, utf16Length);
            return new EditorResponse
            {
                Id = id,
                Diagnostics = diagnostics,
                Symbols = symbols
            };
        }

        private static void AppendDiagnostics(List<EditorDiagnostic> destination, IList<ArtiDiagnostic> source, string language, int utf16Length)
        {
            if (source == null)
            {
                return;
            }

            ArtiDiagnosticLanguage diagnosticLanguage = language == "en"
                ? ArtiDiagnosticLanguage.English
                : ArtiDiagnosticLanguage.ChineseSimplified;
            foreach (ArtiDiagnostic diagnostic in source)
            {
                if (diagnostic == null)
                {
                    continue;
                }

                int start = Clamp(diagnostic.Span.StartOffset, 0, utf16Length);
                int end = Clamp(diagnostic.Span.EndOffset, start, utf16Length);
                destination.Add(new EditorDiagnostic
                {
                    Code = diagnostic.Code ?? string.Empty,
                    Message = diagnostic.GetMessage(diagnosticLanguage) ?? string.Empty,
                    Severity = SeverityName(diagnostic.Severity),
                    Start = start,
                    Length = end - start
                });
            }
        }

        private static string SeverityName(ArtiDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case ArtiDiagnosticSeverity.Warning:
                    return "warning";
                case ArtiDiagnosticSeverity.Info:
                    return "info";
                default:
                    return "error";
            }
        }

        private static EditorResponse ProtocolError(int id, string message)
        {
            return new EditorResponse
            {
                Id = id,
                Diagnostics = new List<EditorDiagnostic>(),
                Symbols = new List<EditorSymbol>(),
                Error = message ?? "request failed"
            };
        }

        private static void WriteProtocolError(int id, Exception exception)
        {
            string message = exception == null
                ? "request failed"
                : exception.GetType().Name + ": " + exception.Message;
            WriteResponse(ProtocolError(id, message));
        }

        private static void WriteResponse(EditorResponse response)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            Console.Out.Flush();
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }

    internal sealed class EditorRequest
    {
        public int Id { get; set; }
        public string Source { get; set; }
        public bool IsModule { get; set; }
        public bool AutoMode { get; set; }
        public string Language { get; set; }
    }

    internal sealed class EditorResponse
    {
        public int Id { get; set; }
        public List<EditorDiagnostic> Diagnostics { get; set; }
        public List<EditorSymbol> Symbols { get; set; }
        public string Error { get; set; }
    }

    internal sealed class EditorDiagnostic
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
    }

    internal sealed class EditorSymbol
    {
        public string Name { get; set; }
        public string Detail { get; set; }
        public string Kind { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }
        public List<EditorSymbol> Children { get; set; }
    }

    internal static class Outline
    {
        public static List<EditorSymbol> Extract(ArtiProgram program, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> symbols = new List<EditorSymbol>();
            if (program == null || program.Statements == null)
            {
                return symbols;
            }

            foreach (ArtiStatement statement in program.Statements)
            {
                EditorSymbol symbol = StatementSymbol(statement, tokens, utf16Length);
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        private static EditorSymbol StatementSymbol(ArtiStatement statement, IList<ArtiToken> tokens, int utf16Length)
        {
            if (statement is ArtiUseStatement use)
            {
                EditorSymbol imported = Named(use.Alias, ImportDetail(use), "namespace", use.Span, use.Alias, tokens, utf16Length, null);
                if (imported != null && tokens != null)
                {
                    bool afterAlias = false;
                    foreach (ArtiToken token in tokens)
                    {
                        if (token == null || token.Span.StartOffset < imported.Start || token.Span.EndOffset > imported.Start + imported.Length) continue;
                        if (afterAlias && token.Kind == ArtiTokenKind.Identifier && token.Text == use.Alias)
                        {
                            imported.SelectionStart = token.Span.StartOffset;
                            imported.SelectionLength = token.Span.Length;
                            break;
                        }
                        afterAlias = token.Kind == ArtiTokenKind.As;
                    }
                }
                return imported;
            }

            if (statement is ArtiVariableDeclarationStatement variable)
            {
                return Named(
                    variable.Name,
                    TypeDetail(variable.Type),
                    variable.IsConst ? "constant" : "variable",
                    variable.Span,
                    variable.Name,
                    tokens,
                    utf16Length,
                    null);
            }

            if (statement is ArtiUnpackDeclarationStatement unpack)
            {
                return UnpackSymbol(unpack, tokens, utf16Length);
            }

            if (statement is ArtiFunctionDeclarationStatement function)
            {
                return FunctionSymbol(function, tokens, utf16Length);
            }

            if (statement is ArtiStructDeclarationStatement structure)
            {
                return Named(
                    structure.Name,
                    "struct",
                    "struct",
                    structure.Span,
                    structure.Name,
                    tokens,
                    utf16Length,
                    StructChildren(structure, tokens, utf16Length));
            }

            if (statement is ArtiTraitDeclarationStatement trait)
            {
                return Named(
                    trait.Name,
                    "trait",
                    "trait",
                    trait.Span,
                    trait.Name,
                    tokens,
                    utf16Length,
                    Functions(trait.Methods, tokens, utf16Length));
            }

            if (statement is ArtiImplDeclarationStatement implementation)
            {
                string name = ImplName(implementation);
                return Named(
                    name,
                    "impl",
                    "namespace",
                    implementation.Span,
                    ImplSelection(implementation),
                    tokens,
                    utf16Length,
                    Functions(implementation.Methods, tokens, utf16Length));
            }

            if (statement is ArtiBlockStatement block)
            {
                return null;
            }

            return NestedContainer(statement, tokens, utf16Length);
        }

        private static EditorSymbol UnpackSymbol(ArtiUnpackDeclarationStatement unpack, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> children = new List<EditorSymbol>();
            if (unpack.Targets != null && unpack.Targets.Items != null)
            {
                foreach (ArtiExpression target in unpack.Targets.Items)
                {
                    ArtiNameExpression name = target as ArtiNameExpression;
                    if (name == null || name.Name == "_")
                    {
                        continue;
                    }

                    EditorSymbol child = Named(name.Name, string.Empty, "variable", name.Span, name.Name, tokens, utf16Length, null);
                    if (child != null)
                    {
                        children.Add(child);
                    }
                }
            }

            if (children.Count == 0)
            {
                return null;
            }

            if (children.Count == 1)
            {
                EditorSymbol only = children[0];
                Place(only, unpack.Span, utf16Length);
                return only;
            }

            return Container(unpack.Span, utf16Length, children);
        }

        private static EditorSymbol NestedContainer(ArtiStatement statement, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> nested = NestedDeclarations(statement, tokens, utf16Length);
            if (nested.Count == 0)
            {
                return null;
            }

            if (nested.Count == 1 && Contains(statement.Span, nested[0]))
            {
                return nested[0];
            }

            return Container(statement.Span, utf16Length, nested);
        }

        private static List<EditorSymbol> NestedDeclarations(ArtiNode node, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> symbols = new List<EditorSymbol>();
            if (node == null)
            {
                return symbols;
            }

            if (node is ArtiFunctionDeclarationStatement function)
            {
                EditorSymbol symbol = FunctionSymbol(function, tokens, utf16Length);
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }

                return symbols;
            }

            if (node is ArtiVariableDeclarationStatement variable)
            {
                EditorSymbol symbol = Named(
                    variable.Name,
                    TypeDetail(variable.Type),
                    variable.IsConst ? "constant" : "variable",
                    variable.Span,
                    variable.Name,
                    tokens,
                    utf16Length,
                    null);
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }

                return symbols;
            }

            foreach (ArtiNode child in Children(node))
            {
                symbols.AddRange(NestedDeclarations(child, tokens, utf16Length));
            }

            return symbols;
        }

        private static IEnumerable<ArtiNode> Children(ArtiNode node)
        {
            if (node is ArtiBlockStatement block && block.Statements != null)
            {
                foreach (ArtiStatement statement in block.Statements)
                {
                    yield return statement;
                }
            }
            else if (node is ArtiIfStatement conditional)
            {
                if (conditional.Branches != null)
                {
                    foreach (ArtiIfBranch branch in conditional.Branches)
                    {
                        yield return branch;
                    }
                }

                if (conditional.ElseBody != null)
                {
                    yield return conditional.ElseBody;
                }
            }
            else if (node is ArtiIfBranch branch)
            {
                if (branch.Body != null)
                {
                    yield return branch.Body;
                }
            }
            else if (node is ArtiForStatement loop)
            {
                if (loop.Body != null)
                {
                    yield return loop.Body;
                }
            }
            else if (node is ArtiWhileStatement whileLoop)
            {
                if (whileLoop.Body != null)
                {
                    yield return whileLoop.Body;
                }
            }
        }

        private static List<EditorSymbol> StructChildren(ArtiStructDeclarationStatement structure, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> children = new List<EditorSymbol>();
            if (structure.Fields != null)
            {
                foreach (ArtiStructField field in structure.Fields)
                {
                    if (field == null)
                    {
                        continue;
                    }

                    string detail = TypeDetail(field.Type);
                    if (field.IsEmbedded)
                    {
                        detail = detail.Length == 0 ? "embed" : "embed " + detail;
                    }

                    EditorSymbol symbol = Named(field.Name, detail, "field", field.Span, field.Name, tokens, utf16Length, null);
                    if (symbol != null)
                    {
                        children.Add(symbol);
                    }
                }
            }

            children.AddRange(Functions(structure.Methods, tokens, utf16Length));
            return children;
        }

        private static List<EditorSymbol> Functions(IList<ArtiFunctionDeclarationStatement> functions, IList<ArtiToken> tokens, int utf16Length)
        {
            List<EditorSymbol> symbols = new List<EditorSymbol>();
            if (functions == null)
            {
                return symbols;
            }

            foreach (ArtiFunctionDeclarationStatement function in functions)
            {
                EditorSymbol symbol = FunctionSymbol(function, tokens, utf16Length, true);
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }
            }

            return symbols;
        }

        private static EditorSymbol FunctionSymbol(ArtiFunctionDeclarationStatement function, IList<ArtiToken> tokens, int utf16Length)
        {
            return FunctionSymbol(function, tokens, utf16Length, false);
        }

        private static EditorSymbol FunctionSymbol(ArtiFunctionDeclarationStatement function, IList<ArtiToken> tokens, int utf16Length, bool method)
        {
            if (function == null)
            {
                return null;
            }

            List<EditorSymbol> children = function.Body == null
                ? new List<EditorSymbol>()
                : NestedDeclarations(function.Body, tokens, utf16Length);
            return Named(function.Name, FunctionDetail(function), method ? "method" : "function", function.Span, function.Name, tokens, utf16Length, children);
        }

        private static string FunctionDetail(ArtiFunctionDeclarationStatement function)
        {
            StringBuilder detail = new StringBuilder();
            if (function.IsStatic)
            {
                detail.Append("static ");
            }

            detail.Append("fn ").Append(function.Name);
            if (function.TypeParameters != null && function.TypeParameters.Count > 0)
            {
                detail.Append('<');
                for (int index = 0; index < function.TypeParameters.Count; index++)
                {
                    if (index > 0)
                    {
                        detail.Append(", ");
                    }

                    ArtiTypeParameter parameter = function.TypeParameters[index];
                    detail.Append(parameter == null ? string.Empty : parameter.Name);
                    if (parameter != null && parameter.Constraint != null)
                    {
                        detail.Append(": ").Append(TypeText(parameter.Constraint));
                    }
                }

                detail.Append('>');
            }

            detail.Append('(');
            if (function.Parameters != null)
            {
                for (int index = 0; index < function.Parameters.Count; index++)
                {
                    if (index > 0)
                    {
                        detail.Append(", ");
                    }

                    ArtiParameter parameter = function.Parameters[index];
                    if (parameter == null)
                    {
                        continue;
                    }

                    detail.Append(parameter.Name);
                    if (parameter.Type != null)
                    {
                        detail.Append(": ").Append(TypeText(parameter.Type));
                    }
                }
            }

            detail.Append(')');
            if (function.ReturnType != null)
            {
                detail.Append(": ").Append(TypeText(function.ReturnType));
            }

            return detail.ToString();
        }

        private static string ImplSelection(ArtiImplDeclarationStatement implementation)
        {
            ArtiTypeReference selected = implementation.Target ?? implementation.Trait;
            if (selected == null || string.IsNullOrEmpty(selected.Name))
            {
                return string.Empty;
            }

            int dot = selected.Name.LastIndexOf('.');
            return dot >= 0 ? selected.Name.Substring(dot + 1) : selected.Name;
        }

        private static string TypeDetail(ArtiTypeReference type)
        {
            return type == null ? string.Empty : TypeText(type);
        }

        private static string TypeText(ArtiTypeReference type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            string name = type.ElementType == null ? type.Name : type.Name + "<" + TypeText(type.ElementType) + ">";
            return type.IsNullable ? name + "?" : name;
        }

        private static string ImportDetail(ArtiUseStatement use)
        {
            string path = use.ModulePath ?? string.Empty;
            string package = use.PackageId ?? string.Empty;
            string detail = path + "@" + package;
            if (use.Optional)
            {
                detail = "optional " + detail;
            }

            return detail;
        }

        private static string ImplName(ArtiImplDeclarationStatement implementation)
        {
            string target = implementation.Target == null ? string.Empty : TypeText(implementation.Target);
            if (implementation.Trait == null)
            {
                return "impl " + target;
            }

            return "impl " + TypeText(implementation.Trait) + " for " + target;
        }

        private static EditorSymbol Named(
            string name,
            string detail,
            string kind,
            ArtiSourceSpan span,
            string selectionText,
            IList<ArtiToken> tokens,
            int utf16Length,
            List<EditorSymbol> children)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            EditorSymbol symbol = new EditorSymbol
            {
                Name = name,
                Detail = detail ?? string.Empty,
                Kind = kind,
                Children = children ?? new List<EditorSymbol>()
            };
            Place(symbol, span, utf16Length);
            SelectIdentifier(symbol, selectionText, tokens, utf16Length);
            ContainChildren(symbol, utf16Length);
            return symbol;
        }

        private static EditorSymbol Container(ArtiSourceSpan span, int utf16Length, List<EditorSymbol> children)
        {
            EditorSymbol symbol = new EditorSymbol
            {
                Name = string.Empty,
                Detail = string.Empty,
                Kind = "namespace",
                Children = children
            };
            Place(symbol, span, utf16Length);
            symbol.SelectionStart = symbol.Start;
            symbol.SelectionLength = 0;
            ContainChildren(symbol, utf16Length);
            return symbol;
        }

        private static void Place(EditorSymbol symbol, ArtiSourceSpan span, int utf16Length)
        {
            int start = Program.Clamp(span.StartOffset, 0, utf16Length);
            int end = Program.Clamp(span.EndOffset, start, utf16Length);
            symbol.Start = start;
            symbol.Length = end - start;
            symbol.SelectionStart = start;
            symbol.SelectionLength = 0;
        }

        private static void SelectIdentifier(EditorSymbol symbol, string text, IList<ArtiToken> tokens, int utf16Length)
        {
            int end = symbol.Start + symbol.Length;
            if (tokens != null && !string.IsNullOrEmpty(text))
            {
                foreach (ArtiToken token in tokens)
                {
                    if (token == null || token.Kind != ArtiTokenKind.Identifier || token.Text != text)
                    {
                        continue;
                    }

                    int tokenStart = token.Span.StartOffset;
                    int tokenEnd = token.Span.EndOffset;
                    if (tokenStart >= symbol.Start && tokenEnd <= end && tokenEnd >= tokenStart)
                    {
                        symbol.SelectionStart = Program.Clamp(tokenStart, 0, utf16Length);
                        symbol.SelectionLength = Program.Clamp(tokenEnd, symbol.SelectionStart, utf16Length) - symbol.SelectionStart;
                        return;
                    }
                }
            }

            symbol.SelectionStart = symbol.Start;
            symbol.SelectionLength = 0;
        }

        private static void ContainChildren(EditorSymbol symbol, int utf16Length)
        {
            if (symbol.Children == null || symbol.Children.Count == 0)
            {
                return;
            }

            int start = symbol.Start;
            int end = symbol.Start + symbol.Length;
            foreach (EditorSymbol child in symbol.Children)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.Start < start)
                {
                    start = child.Start;
                }

                int childEnd = child.Start + child.Length;
                if (childEnd > end)
                {
                    end = childEnd;
                }
            }

            start = Program.Clamp(start, 0, utf16Length);
            end = Program.Clamp(end, start, utf16Length);
            symbol.Start = start;
            symbol.Length = end - start;
            if (symbol.SelectionStart < symbol.Start || symbol.SelectionStart > symbol.Start + symbol.Length)
            {
                symbol.SelectionStart = symbol.Start;
                symbol.SelectionLength = 0;
            }
            else if (symbol.SelectionStart + symbol.SelectionLength > symbol.Start + symbol.Length)
            {
                symbol.SelectionLength = symbol.Start + symbol.Length - symbol.SelectionStart;
            }
        }

        private static bool Contains(ArtiSourceSpan span, EditorSymbol symbol)
        {
            int end = span.StartOffset + Math.Max(0, span.Length);
            return symbol.Start >= span.StartOffset && symbol.Start + symbol.Length <= end;
        }
    }
}
