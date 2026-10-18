// Derived from AdvancedRimTalk Source/Arti (MIT License).
// Copyright (c) 2026 Nanaloveyuki
// Adapted for Oxygendsl.Language; original prompt/RimTalk/Verse host interfaces are not retained.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Oxygendsl.Language
{
    public sealed class ArtiParseResult
    {
        public ArtiParseResult(string source, ArtiProgram program, IList<ArtiToken> tokens, IList<ArtiDiagnostic> diagnostics)
        {
            Source = source ?? string.Empty;
            Program = program;
            Tokens = tokens;
            Diagnostics = diagnostics;
        }

        public string Source { get; }
        public ArtiProgram Program { get; }
        public IList<ArtiToken> Tokens { get; }
        public IList<ArtiDiagnostic> Diagnostics { get; }
        public bool HasErrors { get { return Diagnostics.Any(diagnostic => diagnostic.Severity == ArtiDiagnosticSeverity.Error); } }
    }

    public sealed class ArtiParser : ArtiDiagnosticReporter
    {
        private IList<ArtiToken> _tokens;
        private int _index;
        private int _expressionDepth;

        public ArtiParseResult Parse(string source)
        {
            return Parse(source, 0, 1, 1);
        }

        public ArtiParseResult Parse(string source, int sourceOffset, int startLine, int startColumn)
        {
            source = source ?? string.Empty;
            ArtiLexResult lexResult = new ArtiLexer().Lex(source, sourceOffset, startLine, startColumn);
            _tokens = lexResult.Tokens;
            _index = 0;
            _expressionDepth = 0;
            ClearDiagnostics();
            foreach (ArtiDiagnostic diagnostic in lexResult.Diagnostics)
            {
                AddDiagnostic(diagnostic);
            }

            ArtiProgram program = new ArtiProgram(new ArtiSourceSpan(sourceOffset, source.Length, startLine, startColumn));
            ConsumeSeparators();
            while (!Check(ArtiTokenKind.EndOfFile))
            {
                ArtiStatement statement = ParseStatement();
                if (statement != null)
                {
                    program.Statements.Add(statement);
                }

                if (!ConsumeSeparators() && !Check(ArtiTokenKind.EndOfFile))
                {
                    ReportError(2001, Current.Span);
                    SynchronizeStatement();
                }
            }

            return new ArtiParseResult(source, program, _tokens, SnapshotDiagnostics());
        }

        private ArtiStatement ParseStatement()
        {
            if (Check(ArtiTokenKind.Optional) && Peek(1).Kind == ArtiTokenKind.Use)
            {
                Advance();
                return ParseUse(true);
            }
            if (Check(ArtiTokenKind.Pub))
            {
                return ParsePubStatement();
            }

            switch (Current.Kind)
            {
                case ArtiTokenKind.Use:
                    return ParseUse(false);
                case ArtiTokenKind.Const:
                    return ParseVariableDeclaration(true, Advance(), false);
                case ArtiTokenKind.Let:
                    return ParseVariableDeclaration(false, Advance(), false);
                case ArtiTokenKind.Fn:
                    return ParseFunction(false, false, true);
                case ArtiTokenKind.Static:
                    return ParseStaticFunction(false);
                case ArtiTokenKind.Struct:
                    return ParseStruct(false);
                case ArtiTokenKind.Trait:
                    return ParseTrait(false);
                case ArtiTokenKind.Impl:
                    return ParseImpl();
                case ArtiTokenKind.If:
                    return ParseIf();
                case ArtiTokenKind.For:
                    return ParseFor();
                case ArtiTokenKind.While:
                    return ParseWhile();
                case ArtiTokenKind.Return:
                    return ParseReturn();
                case ArtiTokenKind.Break:
                    return new ArtiBreakStatement(Advance().Span);
                case ArtiTokenKind.Continue:
                    return new ArtiContinueStatement(Advance().Span);
                case ArtiTokenKind.LeftBrace:
                    return ParseBlock();
                default:
                    return ParseExpressionOrAssignment();
            }
        }

        private ArtiStatement ParsePubStatement()
        {
            ArtiToken pub = Advance();
            SkipNewLines();
            if (Check(ArtiTokenKind.Fn))
            {
                ArtiFunctionDeclarationStatement function = ParseFunction(true, false, true);
                function.Span = Combine(pub.Span, function.Span);
                return function;
            }

            if (Check(ArtiTokenKind.Static))
            {
                ArtiFunctionDeclarationStatement function = ParseStaticFunction(true);
                function.Span = Combine(pub.Span, function.Span);
                return function;
            }

            if (Check(ArtiTokenKind.Struct))
            {
                ArtiStructDeclarationStatement declaration = ParseStruct(true);
                declaration.Span = Combine(pub.Span, declaration.Span);
                return declaration;
            }

            if (Check(ArtiTokenKind.Trait))
            {
                ArtiTraitDeclarationStatement declaration = ParseTrait(true);
                declaration.Span = Combine(pub.Span, declaration.Span);
                return declaration;
            }

            if (Check(ArtiTokenKind.Impl))
            {
                ReportError(2035, pub.Span);
                ArtiImplDeclarationStatement declaration = ParseImpl();
                declaration.IsPublic = true;
                declaration.Span = Combine(pub.Span, declaration.Span);
                return declaration;
            }

            if (Check(ArtiTokenKind.Const))
            {
                ArtiToken keyword = Advance();
                ArtiStatement statement = ParseVariableDeclaration(true, keyword, true);
                statement.Span = Combine(pub.Span, statement.Span);
                return statement;
            }

            ReportError(2027, pub.Span);
            return null;
        }

        private ArtiUseStatement ParseUse(bool optional)
        {
            ArtiToken useToken = Advance();
            string modulePath = ParseQualifiedName(false);
            if (!Match(ArtiTokenKind.At))
            {
                ReportError(2021, Current.Span);
            }

            string packageId = ParseProvider();
            string alias = string.Empty;
            if (Match(ArtiTokenKind.As))
            {
                alias = ExpectIdentifier().Text;
            }

            if (string.IsNullOrEmpty(alias))
            {
                int separator = modulePath.LastIndexOf('.');
                alias = separator >= 0 ? modulePath.Substring(separator + 1) : modulePath;
            }

            ArtiSourceSpan span = Combine(useToken.Span, Previous.Span);
            return new ArtiUseStatement(span, modulePath, packageId, alias, optional);
        }

        private string ParseProvider()
        {
            if (Check(ArtiTokenKind.String))
            {
                return Convert.ToString(Advance().Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return ParseQualifiedName(false);
        }

        private ArtiStatement ParseVariableDeclaration(bool isConst, ArtiToken keyword, bool isPublic)
        {
            ArtiToken name = ExpectIdentifier();
            if (!isConst && !isPublic && Check(ArtiTokenKind.Comma))
            {
                var targets = new ArtiArrayExpression(name.Span);
                targets.Items.Add(new ArtiNameExpression(name.Span, name.Text));
                while (Match(ArtiTokenKind.Comma))
                {
                    ArtiToken next = ExpectIdentifier();
                    targets.Items.Add(new ArtiNameExpression(next.Span, next.Text));
                }

                Expect(ArtiTokenKind.Equal, 2009);
                ArtiExpression values = ParseExpressionList();
                return new ArtiUnpackDeclarationStatement(Combine(keyword.Span, values.Span), targets, values);
            }

            ArtiTypeReference type = null;
            if (Match(ArtiTokenKind.Colon))
            {
                type = ParseType();
            }

            Expect(ArtiTokenKind.Equal, 2009);
            ArtiExpression value = ParseExpressionList();
            ArtiVariableDeclarationStatement variable = new ArtiVariableDeclarationStatement(Combine(keyword.Span, value.Span), name.Text, isConst, value);
            variable.Type = type;
            variable.IsPublic = isPublic;
            return variable;
        }

        private ArtiFunctionDeclarationStatement ParseStaticFunction(bool isPublic)
        {
            ArtiToken keyword = Advance();
            SkipNewLines();
            if (!Check(ArtiTokenKind.Fn))
            {
                ReportError(2010, Current.Span);
            }

            ArtiFunctionDeclarationStatement function = ParseFunction(isPublic, true, false);
            function.Span = Combine(keyword.Span, function.Span);
            return function;
        }

        private ArtiFunctionDeclarationStatement ParseFunction(bool isPublic, bool isStatic, bool allowTypeParameters, bool requireBody = true)
        {
            ArtiToken fnToken = Expect(ArtiTokenKind.Fn, 2010);
            ArtiToken name = ExpectIdentifier();
            List<ArtiTypeParameter> typeParameters = null;
            if (Check(ArtiTokenKind.Less))
            {
                typeParameters = ParseTypeParameters();
                if (!allowTypeParameters)
                {
                    ReportError(3030, fnToken.Span);
                }
            }

            Expect(ArtiTokenKind.LeftParen, 2011);
            List<ArtiParameter> parameters = ParseGroup(ParseParameters);
            SkipNewLines();
            ArtiTypeReference returnType = null;
            if (Match(ArtiTokenKind.Colon))
            {
                returnType = ParseType();
                SkipNewLines();
            }

            ArtiBlockStatement body = null;
            if (requireBody || Check(ArtiTokenKind.LeftBrace))
            {
                body = ParseBlock();
                if (!requireBody)
                {
                    ReportError(3031, body.Span);
                }
            }

            ArtiSourceSpan end = body != null ? body.Span : returnType != null ? returnType.Span : Previous.Span;
            ArtiFunctionDeclarationStatement function = new ArtiFunctionDeclarationStatement(Combine(fnToken.Span, end), name.Text, body);
            foreach (ArtiParameter parameter in parameters)
            {
                function.Parameters.Add(parameter);
            }

            if (typeParameters != null)
            {
                foreach (ArtiTypeParameter typeParameter in typeParameters)
                {
                    function.TypeParameters.Add(typeParameter);
                }
            }

            function.ReturnType = returnType;
            function.IsPublic = isPublic;
            function.IsStatic = isStatic;
            return function;
        }

        private List<ArtiTypeParameter> ParseTypeParameters()
        {
            List<ArtiTypeParameter> parameters = new List<ArtiTypeParameter>();
            Advance();
            SkipNewLines();
            if (!Check(ArtiTokenKind.Greater))
            {
                while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.Greater))
                {
                    parameters.Add(ParseTypeParameter());
                    SkipNewLines();
                    if (!Match(ArtiTokenKind.Comma))
                    {
                        break;
                    }

                    SkipNewLines();
                }
            }

            Expect(ArtiTokenKind.Greater, 2032);
            return parameters;
        }

        private ArtiTypeParameter ParseTypeParameter()
        {
            ArtiToken name = ExpectIdentifier();
            if (!Match(ArtiTokenKind.Colon))
            {
                ReportError(3036, name.Span);
                return new ArtiTypeParameter(name.Span, name.Text, null);
            }

            ArtiTypeReference constraint = ParseType();
            return new ArtiTypeParameter(Combine(name.Span, constraint.Span), name.Text, constraint);
        }

        private List<ArtiParameter> ParseParameters()
        {
            List<ArtiParameter> parameters = new List<ArtiParameter>();
            SkipNewLines();
            if (!Check(ArtiTokenKind.RightParen))
            {
                while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.RightParen))
                {
                    parameters.Add(ParseTypedName());
                    SkipNewLines();
                    if (!Match(ArtiTokenKind.Comma))
                    {
                        break;
                    }

                    SkipNewLines();
                }
            }

            Expect(ArtiTokenKind.RightParen, 2012);
            return parameters;
        }

        private ArtiParameter ParseTypedName()
        {
            ArtiToken name = ExpectIdentifier();
            ArtiTypeReference type = null;
            if (Match(ArtiTokenKind.Colon))
            {
                type = ParseType();
            }

            ArtiSourceSpan span = type == null ? name.Span : Combine(name.Span, type.Span);
            return new ArtiParameter(span, name.Text, type);
        }

        private ArtiStructDeclarationStatement ParseStruct(bool isPublic)
        {
            ArtiToken keyword = Expect(ArtiTokenKind.Struct, 2024);
            ArtiToken name = ExpectIdentifier();
            SkipNewLines();
            if (!Check(ArtiTokenKind.LeftBrace))
            {
                ReportError(2004, Current.Span);
                ArtiStructDeclarationStatement missing = new ArtiStructDeclarationStatement(Combine(keyword.Span, name.Span), name.Text);
                missing.IsPublic = isPublic;
                return missing;
            }

            ArtiToken leftBrace = Advance();
            ArtiStructDeclarationStatement declaration = new ArtiStructDeclarationStatement(leftBrace.Span, name.Text);
            declaration.IsPublic = isPublic;
            ConsumeSeparators();
            while (!Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
            {
                if (Check(ArtiTokenKind.Fn) || Check(ArtiTokenKind.Static))
                {
                    declaration.Methods.Add(Check(ArtiTokenKind.Static) ? ParseStaticFunction(false) : ParseFunction(false, false, false));
                }
                else
                {
                    declaration.Fields.Add(ParseStructField());
                }

                bool separated = ConsumeSeparators();
                if (!separated && !Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile)
                    && !Check(ArtiTokenKind.Fn) && !Check(ArtiTokenKind.Static))
                {
                    ReportError(2001, Current.Span);
                    SynchronizeStatement();
                }
            }

            ArtiToken rightBrace = Expect(ArtiTokenKind.RightBrace, 2023);
            declaration.Span = Combine(keyword.Span, rightBrace.Span);
            return declaration;
        }

        private ArtiTraitDeclarationStatement ParseTrait(bool isPublic)
        {
            ArtiToken keyword = Expect(ArtiTokenKind.Trait, 2036);
            ArtiToken name = ExpectIdentifier();
            SkipNewLines();
            if (!Check(ArtiTokenKind.LeftBrace))
            {
                ReportError(2004, Current.Span);
                ArtiTraitDeclarationStatement missing = new ArtiTraitDeclarationStatement(Combine(keyword.Span, name.Span), name.Text);
                missing.IsPublic = isPublic;
                return missing;
            }

            ArtiToken leftBrace = Advance();
            ArtiTraitDeclarationStatement declaration = new ArtiTraitDeclarationStatement(leftBrace.Span, name.Text);
            declaration.IsPublic = isPublic;
            ConsumeSeparators();
            while (!Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
            {
                if (Check(ArtiTokenKind.Static))
                {
                    declaration.Methods.Add(ParseStaticSignature());
                }
                else
                {
                    declaration.Methods.Add(ParseFunction(false, false, false, false));
                }

                bool separated = ConsumeSeparators();
                if (!separated && !Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile)
                    && !Check(ArtiTokenKind.Fn) && !Check(ArtiTokenKind.Static))
                {
                    ReportError(2001, Current.Span);
                    SynchronizeStatement();
                }
            }

            ArtiToken rightBrace = Expect(ArtiTokenKind.RightBrace, 2029);
            declaration.Span = Combine(keyword.Span, rightBrace.Span);
            return declaration;
        }

        private ArtiFunctionDeclarationStatement ParseStaticSignature()
        {
            ArtiToken keyword = Advance();
            SkipNewLines();
            if (!Check(ArtiTokenKind.Fn))
            {
                ReportError(2010, Current.Span);
            }

            ArtiFunctionDeclarationStatement function = ParseFunction(false, true, false, false);
            function.Span = Combine(keyword.Span, function.Span);
            return function;
        }

        private ArtiImplDeclarationStatement ParseImpl()
        {
            ArtiToken keyword = Expect(ArtiTokenKind.Impl, 2036);
            ArtiTypeReference first = ParseType();
            ArtiTypeReference trait = null;
            ArtiTypeReference target = first;
            SkipNewLines();
            if (Match(ArtiTokenKind.For))
            {
                trait = first;
                SkipNewLines();
                if (Check(ArtiTokenKind.LeftBrace) || Check(ArtiTokenKind.EndOfFile))
                {
                    ReportError(2031, Current.Span);
                    target = new ArtiTypeReference(Current.Span, string.Empty);
                }
                else
                {
                    target = ParseType();
                }

                SkipNewLines();
            }

            if (!Check(ArtiTokenKind.LeftBrace))
            {
                ReportError(2004, Current.Span);
                return new ArtiImplDeclarationStatement(Combine(keyword.Span, target.Span), target, trait);
            }

            ArtiToken leftBrace = Advance();
            ArtiImplDeclarationStatement declaration = new ArtiImplDeclarationStatement(leftBrace.Span, target, trait);
            ConsumeSeparators();
            while (!Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
            {
                if (Check(ArtiTokenKind.Fn) || Check(ArtiTokenKind.Static))
                {
                    declaration.Methods.Add(Check(ArtiTokenKind.Static) ? ParseStaticFunction(false) : ParseFunction(false, false, false));
                }
                else
                {
                    ReportError(2010, Current.Span);
                    SynchronizeStatement();
                }

                bool separated = ConsumeSeparators();
                if (!separated && !Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile)
                    && !Check(ArtiTokenKind.Fn) && !Check(ArtiTokenKind.Static))
                {
                    ReportError(2001, Current.Span);
                    SynchronizeStatement();
                }
            }

            ArtiToken rightBrace = Expect(ArtiTokenKind.RightBrace, 2030);
            declaration.Span = Combine(keyword.Span, rightBrace.Span);
            return declaration;
        }

        private ArtiStructField ParseStructField()
        {
            bool embedded = Match(ArtiTokenKind.Embed);
            ArtiToken name = ExpectIdentifier();
            ArtiTypeReference type = null;
            if (Match(ArtiTokenKind.Colon))
            {
                type = ParseType();
            }
            else if (embedded)
            {
                ReportError(2025, name.Span);
            }

            ArtiExpression defaultValue = null;
            if (Match(ArtiTokenKind.Equal))
            {
                defaultValue = ParseExpression();
            }

            ArtiSourceSpan end = defaultValue != null ? defaultValue.Span : type != null ? type.Span : name.Span;
            return new ArtiStructField(Combine(name.Span, end), name.Text, type, defaultValue, embedded);
        }

        private ArtiTypeReference ParseType()
        {
            ArtiToken start = Current;
            string name;
            ArtiTypeReference element = null;
            if (Check(ArtiTokenKind.Identifier) && (Current.Text == "list" || Current.Text == "array") && Peek(1).Kind == ArtiTokenKind.Less)
            {
                name = Advance().Text;
                Advance();
                element = ParseGroup(ParseType);
                Expect(ArtiTokenKind.Greater, 2022);
            }
            else
            {
                name = ParseQualifiedName(true);
            }

            if (string.IsNullOrEmpty(name))
            {
                ReportError(2022, start.Span);
                return new ArtiTypeReference(start.Span, string.Empty);
            }

            bool nullable = Match(ArtiTokenKind.Question);
            ArtiSourceSpan span = Combine(start.Span, Previous.Span);
            return new ArtiTypeReference(span, name, element, nullable);
        }

        private ArtiIfStatement ParseIf()
        {
            ArtiToken ifToken = Advance();
            ArtiIfStatement statement = new ArtiIfStatement(ifToken.Span);
            ArtiExpression condition = ParseExpression();
            SkipNewLines();
            ArtiBlockStatement body = ParseBlock();
            statement.Branches.Add(new ArtiIfBranch(Combine(condition.Span, body.Span), condition, body));

            while (true)
            {
                int savedIndex = _index;
                SkipNewLines();
                if (!Match(ArtiTokenKind.Else))
                {
                    _index = savedIndex;
                    break;
                }

                if (Match(ArtiTokenKind.If))
                {
                    ArtiExpression elseIfCondition = ParseExpression();
                    SkipNewLines();
                    ArtiBlockStatement elseIfBody = ParseBlock();
                    statement.Branches.Add(new ArtiIfBranch(Combine(elseIfCondition.Span, elseIfBody.Span), elseIfCondition, elseIfBody));
                    continue;
                }

                SkipNewLines();
                statement.ElseBody = ParseBlock();
                break;
            }

            ArtiSourceSpan endSpan = statement.ElseBody == null ? statement.Branches[statement.Branches.Count - 1].Span : statement.ElseBody.Span;
            statement.Span = Combine(ifToken.Span, endSpan);
            return statement;
        }

        private ArtiForStatement ParseFor()
        {
            ArtiToken forToken = Advance();
            ArtiToken variable = ExpectIdentifier();
            Expect(ArtiTokenKind.In, 2013);
            ArtiExpression source = ParseExpression();
            SkipNewLines();
            ArtiBlockStatement body = ParseBlock();
            return new ArtiForStatement(Combine(forToken.Span, body.Span), variable.Text, source, body);
        }

        private ArtiWhileStatement ParseWhile()
        {
            ArtiToken whileToken = Advance();
            ArtiExpression condition = ParseExpression();
            SkipNewLines();
            ArtiBlockStatement body = ParseBlock();
            return new ArtiWhileStatement(Combine(whileToken.Span, body.Span), condition, body);
        }

        private ArtiReturnStatement ParseReturn()
        {
            ArtiToken returnToken = Advance();
            ArtiExpression value = IsStatementTerminator(Current.Kind) ? null : ParseExpressionList();
            return new ArtiReturnStatement(Combine(returnToken.Span, value == null ? returnToken.Span : value.Span), value);
        }

        private ArtiBlockStatement ParseBlock()
        {
            if (!Check(ArtiTokenKind.LeftBrace))
            {
                ReportError(2004, Current.Span);
                return new ArtiBlockStatement(Current.Span);
            }

            ArtiToken leftBrace = Advance();
            ArtiBlockStatement block = new ArtiBlockStatement(leftBrace.Span);
            ConsumeSeparators();
            while (!Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
            {
                ArtiStatement statement = ParseStatement();
                if (statement != null)
                {
                    block.Statements.Add(statement);
                }

                if (!ConsumeSeparators() && !Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
                {
                    ReportError(2001, Current.Span);
                    SynchronizeStatement();
                }
            }

            ArtiToken rightBrace = Expect(ArtiTokenKind.RightBrace, 2008);
            block.Span = Combine(leftBrace.Span, rightBrace.Span);
            return block;
        }

        private ArtiStatement ParseExpressionOrAssignment()
        {
            ArtiExpression target = ParseExpressionList();
            if (IsAssignmentOperator(Current.Kind))
            {
                ArtiTokenKind op = Advance().Kind;
                ArtiExpression value = ParseExpressionList();
                return new ArtiAssignmentStatement(Combine(target.Span, value.Span), target, op, value);
            }

            return new ArtiExpressionStatement(target.Span, target);
        }

        private ArtiExpression ParseExpressionList()
        {
            ArtiExpression first = ParseExpression();
            if (!Check(ArtiTokenKind.Comma)) return first;
            // Multiple values use the existing ordered array representation.
            var values = new ArtiArrayExpression(first.Span);
            values.Items.Add(first);
            while (Match(ArtiTokenKind.Comma))
            {
                if (Check(ArtiTokenKind.RightParen) || IsStatementTerminator(Current.Kind)) break;
                values.Items.Add(ParseExpression());
            }
            values.Span = Combine(first.Span, values.Items[values.Items.Count - 1].Span);
            return values;
        }

        private ArtiExpression ParseExpression(int minimumPrecedence = 0)
        {
            ArtiExpression left = ParseUnary();
            while (true)
            {
                int precedence = GetPrecedence(Current.Kind);
                if (precedence < minimumPrecedence)
                {
                    break;
                }

                ArtiToken op = Advance();
                ArtiExpression right = ParseExpression(precedence + 1);
                left = new ArtiBinaryExpression(Combine(left.Span, right.Span), left, op.Kind, right);
            }

            return left;
        }

        private T ParseGroup<T>(Func<T> parse)
        {
            _expressionDepth++;
            try { return parse(); }
            finally { _expressionDepth--; }
        }

        private ArtiExpression ParseUnary()
        {
            if (Current.Kind == ArtiTokenKind.Minus && IsSignedMinimumMagnitude(Peek(1)))
            {
                ArtiToken minus = Advance();
                ArtiToken magnitude = Advance();
                return new ArtiLiteralExpression(Combine(minus.Span, magnitude.Span), int.MinValue);
            }

            if (Current.Kind == ArtiTokenKind.Bang || Current.Kind == ArtiTokenKind.Plus || Current.Kind == ArtiTokenKind.Minus)
            {
                ArtiToken op = Advance();
                ArtiExpression operand = ParseUnary();
                return new ArtiUnaryExpression(Combine(op.Span, operand.Span), op.Kind, operand);
            }

            return ParsePostfix(ParsePrimary());
        }

        private static bool IsSignedMinimumMagnitude(ArtiToken token)
        {
            return token != null && token.Kind == ArtiTokenKind.Integer && token.Value == null && token.Text == "2147483648";
        }

        private ArtiExpression ParsePostfix(ArtiExpression expression)
        {
            while (true)
            {
                if (Match(ArtiTokenKind.Dot))
                {
                    ArtiToken member = ExpectIdentifier();
                    expression = new ArtiMemberExpression(Combine(expression.Span, member.Span), expression, member.Text);
                    continue;
                }

                if (Match(ArtiTokenKind.LeftBracket))
                {
                    expression = ParseGroup(() =>
                    {
                        ArtiExpression index = ParseExpression();
                        ArtiToken rightBracket = Expect(ArtiTokenKind.RightBracket, 2014);
                        return new ArtiIndexExpression(Combine(expression.Span, rightBracket.Span), expression, index);
                    });
                    continue;
                }

                if (Check(ArtiTokenKind.Less) && LooksLikeTypeArguments())
                {
                    bool collection = IsCollectionConstructor(expression);
                    int saved = _index;
                    ArtiToken less = Advance();
                    List<ArtiTypeReference> typeArguments = ParseGroup(ParseTypeArgumentList);
                    ArtiToken greater = Expect(ArtiTokenKind.Greater, 2033);
                    if (collection && !Check(ArtiTokenKind.LeftParen))
                    {
                        ReportError(2015, Current.Span);
                        ArtiCallExpression missing = new ArtiCallExpression(Combine(expression.Span, greater.Span), expression);
                        foreach (ArtiTypeReference typeArgument in typeArguments)
                        {
                            missing.TypeArguments.Add(typeArgument);
                        }

                        expression = missing;
                        continue;
                    }

                    if (!Match(ArtiTokenKind.LeftParen))
                    {
                        _index = saved;
                        break;
                    }

                    if (!collection && typeArguments.Count == 0)
                    {
                        ReportError(2033, less.Span);
                    }

                    ArtiCallExpression typedCall = new ArtiCallExpression(Combine(expression.Span, less.Span), expression);
                    foreach (ArtiTypeReference typeArgument in typeArguments)
                    {
                        typedCall.TypeArguments.Add(typeArgument);
                    }

                    ParseGroup(() => { ParseArguments(typedCall); return typedCall; });
                    expression = typedCall;
                    continue;
                }


                if (Match(ArtiTokenKind.LeftParen))
                {
                    ArtiCallExpression call = new ArtiCallExpression(Combine(expression.Span, Previous.Span), expression);
                    ParseGroup(() => { ParseArguments(call); return call; });
                    expression = call;
                    continue;
                }

                break;
            }

            return expression;
        }

        private bool LooksLikeTypeArguments()
        {
            int saved = _index;
            int depth = _expressionDepth;
            _expressionDepth++;
            bool matched = false;
            try
            {
                Advance();
                int angle = 1;
                while (!Check(ArtiTokenKind.EndOfFile) && angle > 0)
                {
                    if (Check(ArtiTokenKind.Less))
                    {
                        angle++;
                    }
                    else if (Check(ArtiTokenKind.Greater))
                    {
                        angle--;
                    }
                    else if (!IsTypeArgumentToken(Current.Kind))
                    {
                        return false;
                    }

                    Advance();
                }

                matched = angle == 0 && Check(ArtiTokenKind.LeftParen);
            }
            finally
            {
                _index = saved;
                _expressionDepth = depth;
            }

            return matched;
        }

        private static bool IsTypeArgumentToken(ArtiTokenKind kind)
        {
            return kind == ArtiTokenKind.Identifier
                || kind == ArtiTokenKind.Dot
                || kind == ArtiTokenKind.Comma
                || kind == ArtiTokenKind.Colon
                || kind == ArtiTokenKind.Question
                || kind == ArtiTokenKind.NewLine
                || IsKeywordToken(kind);
        }

        private List<ArtiTypeReference> ParseTypeArgumentList()
        {
            List<ArtiTypeReference> arguments = new List<ArtiTypeReference>();
            SkipNewLines();
            if (!Check(ArtiTokenKind.Greater))
            {
                while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.Greater))
                {
                    arguments.Add(ParseType());
                    SkipNewLines();
                    if (!Match(ArtiTokenKind.Comma))
                    {
                        break;
                    }

                    SkipNewLines();
                }
            }

            return arguments;
        }

        private void ParseArguments(ArtiCallExpression call)
        {
            SkipNewLines();
            if (Match(ArtiTokenKind.RightParen))
            {
                call.Span = Combine(call.Span, Previous.Span);
                return;
            }

            bool sawNamed = false;
            while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.RightParen))
            {
                string name = null;
                if (Check(ArtiTokenKind.Identifier) && Peek(1).Kind == ArtiTokenKind.Colon)
                {
                    name = Advance().Text;
                    Advance();
                }
                if (name != null) sawNamed = true;
                else if (sawNamed) ReportError(2028, Current.Span);

                ArtiExpression value = ParseExpression();
                call.Arguments.Add(new ArtiArgument(name, value));
                SkipNewLines();
                if (!Match(ArtiTokenKind.Comma))
                {
                    break;
                }

                SkipNewLines();
            }

            ArtiToken rightParen = Expect(ArtiTokenKind.RightParen, 2015);
            call.Span = Combine(call.Span, rightParen.Span);
        }

        private static bool IsCollectionConstructor(ArtiExpression expression)
        {
            ArtiNameExpression name = expression as ArtiNameExpression;
            return name != null && (name.Name == "list" || name.Name == "array");
        }
        private ArtiExpression ParsePrimary()
        {
            ArtiToken token = Current;
            switch (token.Kind)
            {
                case ArtiTokenKind.String:
                case ArtiTokenKind.InterpolatedStringStart:
                    return ParseStringSequence();
                case ArtiTokenKind.Integer:
                case ArtiTokenKind.Float:
                    Advance();
                    if (token.Kind == ArtiTokenKind.Integer && token.Value == null)
                    {
                        ReportError(1004, token.Span);
                        return new ArtiErrorExpression(token.Span);
                    }

                    return new ArtiLiteralExpression(token.Span, token.Value);
                case ArtiTokenKind.True:
                    Advance();
                    return new ArtiLiteralExpression(token.Span, true);
                case ArtiTokenKind.False:
                    Advance();
                    return new ArtiLiteralExpression(token.Span, false);
                case ArtiTokenKind.Null:
                    Advance();
                    return new ArtiLiteralExpression(token.Span, null);
                case ArtiTokenKind.Identifier:
                    Advance();
                    return new ArtiNameExpression(token.Span, token.Text);
                case ArtiTokenKind.LeftParen:
                    Advance();
                    return ParseGroup(() =>
                    {
                        ArtiExpression grouped = Check(ArtiTokenKind.RightParen)
                            ? new ArtiArrayExpression(token.Span) : ParseExpressionList();
                        Expect(ArtiTokenKind.RightParen, 2016);
                        return grouped;
                    });
                case ArtiTokenKind.LeftBracket:
                    return ParseGroup(ParseArray);
                case ArtiTokenKind.LeftBrace:
                    return ParseGroup(ParseObject);
                default:
                    ReportError(2005, token.Span);
                    if (!Check(ArtiTokenKind.EndOfFile))
                    {
                        Advance();
                    }

                    return new ArtiErrorExpression(token.Span);
            }
        }

        private ArtiExpression ParseStringSequence()
        {
            var joined = new ArtiInterpolatedStringExpression(Current.Span);
            do
            {
                if (Check(ArtiTokenKind.String))
                {
                    ArtiToken token = Advance();
                    joined.Parts.Add(new ArtiLiteralExpression(token.Span, token.Value));
                }
                else joined.Parts.Add(ParseInterpolatedString());
            } while (Check(ArtiTokenKind.String) || Check(ArtiTokenKind.InterpolatedStringStart));
            joined.Span = Combine(joined.Span, joined.Parts[joined.Parts.Count - 1].Span);
            return joined.Parts.Count == 1 ? joined.Parts[0] : joined;
        }

        private ArtiInterpolatedStringExpression ParseInterpolatedString()
        {
            ArtiToken start = Advance();
            var result = new ArtiInterpolatedStringExpression(start.Span);
            while (!Check(ArtiTokenKind.InterpolatedStringEnd) && !Check(ArtiTokenKind.EndOfFile))
            {
                if (Check(ArtiTokenKind.String))
                {
                    ArtiToken text = Advance();
                    result.Parts.Add(new ArtiLiteralExpression(text.Span, text.Value));
                }
                else if (Match(ArtiTokenKind.InterpolationStart))
                {
                    result.Parts.Add(ParseGroup(() =>
                    {
                        ArtiExpression value = ParseExpression();
                        Expect(ArtiTokenKind.InterpolationEnd, 1009);
                        return value;
                    }));
                }
                else
                {
                    ReportError(2005, Current.Span);
                    Advance();
                }
            }
            ArtiToken end = Expect(ArtiTokenKind.InterpolatedStringEnd, 1006);
            result.Span = Combine(start.Span, end.Span);
            return result;
        }

        private ArtiArrayExpression ParseArray()
        {
            ArtiToken leftBracket = Advance();
            ArtiArrayExpression array = new ArtiArrayExpression(leftBracket.Span);
            SkipNewLines();
            while (!Check(ArtiTokenKind.RightBracket) && !Check(ArtiTokenKind.EndOfFile))
            {
                array.Items.Add(ParseExpression());
                SkipNewLines();
                if (!Match(ArtiTokenKind.Comma))
                {
                    break;
                }

                SkipNewLines();
            }

            ArtiToken rightBracket = Expect(ArtiTokenKind.RightBracket, 2017);
            array.Span = Combine(leftBracket.Span, rightBracket.Span);
            return array;
        }

        private ArtiObjectExpression ParseObject()
        {
            ArtiToken leftBrace = Advance();
            ArtiObjectExpression obj = new ArtiObjectExpression(leftBrace.Span);
            SkipNewLines();
            while (!Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.EndOfFile))
            {
                ArtiToken key = Current;
                if (!Match(ArtiTokenKind.Identifier) && !Match(ArtiTokenKind.String))
                {
                    ReportError(2006, Current.Span);
                    SynchronizeObjectMember();
                    continue;
                }

                Expect(ArtiTokenKind.Colon, 2018);
                ArtiExpression value = ParseExpression();
                obj.Members.Add(new ArtiObjectMember(key.Kind == ArtiTokenKind.String ? Convert.ToString(key.Value, CultureInfo.InvariantCulture) : key.Text, value));
                SkipNewLines();
                if (!Match(ArtiTokenKind.Comma))
                {
                    break;
                }

                SkipNewLines();
            }

            ArtiToken rightBrace = Expect(ArtiTokenKind.RightBrace, 2019);
            obj.Span = Combine(leftBrace.Span, rightBrace.Span);
            return obj;
        }

        private string ParseQualifiedName(bool allowKeywordsAsTypeNames)
        {
            ArtiToken first = ExpectTypeName(allowKeywordsAsTypeNames);
            StringBuilder name = new StringBuilder(first.Text);
            while (Match(ArtiTokenKind.Dot))
            {
                name.Append('.');
                name.Append(ExpectTypeName(allowKeywordsAsTypeNames).Text);
            }

            return name.ToString();
        }

        private ArtiToken ExpectTypeName(bool allowKeywords)
        {
            if (Check(ArtiTokenKind.Identifier) || (allowKeywords && IsKeywordToken(Current.Kind) && Current.Text.Length > 0))
            {
                return Advance();
            }

            ReportError(2007, Current.Span);
            return new ArtiToken(ArtiTokenKind.Identifier, string.Empty, string.Empty, Current.Span);
        }


        private static bool IsKeywordToken(ArtiTokenKind kind)
        {
            switch (kind)
            {
                case ArtiTokenKind.Use:
                case ArtiTokenKind.As:
                case ArtiTokenKind.Const:
                case ArtiTokenKind.Let:
                case ArtiTokenKind.Fn:
                case ArtiTokenKind.Struct:
                case ArtiTokenKind.Trait:
                case ArtiTokenKind.Impl:
                case ArtiTokenKind.Static:
                case ArtiTokenKind.Embed:
                case ArtiTokenKind.Pub:
                case ArtiTokenKind.If:
                case ArtiTokenKind.Else:
                case ArtiTokenKind.For:
                case ArtiTokenKind.In:
                case ArtiTokenKind.While:
                case ArtiTokenKind.Return:
                case ArtiTokenKind.Break:
                case ArtiTokenKind.Continue:
                case ArtiTokenKind.True:
                case ArtiTokenKind.False:
                case ArtiTokenKind.Null:
                    return true;
                default:
                    return false;
            }
        }

        private void SynchronizeStatement()
        {
            while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.NewLine) && !Check(ArtiTokenKind.Semicolon) && !Check(ArtiTokenKind.RightBrace))
            {
                Advance();
            }
        }

        private void SynchronizeObjectMember()
        {
            while (!Check(ArtiTokenKind.EndOfFile) && !Check(ArtiTokenKind.RightBrace) && !Check(ArtiTokenKind.Comma) && !Check(ArtiTokenKind.NewLine))
            {
                Advance();
            }

            Match(ArtiTokenKind.Comma);
            SkipNewLines();
        }

        private ArtiToken ExpectIdentifier()
        {
            if (Check(ArtiTokenKind.Identifier))
            {
                return Advance();
            }

            ReportError(2007, Current.Span);
            return new ArtiToken(ArtiTokenKind.Identifier, string.Empty, string.Empty, Current.Span);
        }

        private ArtiToken Expect(ArtiTokenKind kind, int diagnosticCode)
        {
            if (Check(kind))
            {
                return Advance();
            }

            ReportError(diagnosticCode, Current.Span);
            return new ArtiToken(kind, string.Empty, null, Current.Span);
        }

        private bool Match(ArtiTokenKind kind)
        {
            if (!Check(kind))
            {
                return false;
            }

            Advance();
            return true;
        }

        private ArtiToken Advance()
        {
            if (_expressionDepth > 0)
                while (_tokens[_index].Kind == ArtiTokenKind.NewLine) _index++;
            ArtiToken token = Current;
            if (_index < _tokens.Count - 1)
            {
                _index++;
            }

            return token;
        }

        private ArtiToken Peek(int distance)
        {
            int index = _index;
            for (int remaining = distance; index < _tokens.Count - 1; index++)
            {
                if (_expressionDepth > 0 && _tokens[index].Kind == ArtiTokenKind.NewLine) continue;
                if (remaining-- == 0) break;
            }
            return index >= 0 && index < _tokens.Count ? _tokens[index] : _tokens[_tokens.Count - 1];
        }

        private bool Check(ArtiTokenKind kind)
        {
            return Current.Kind == kind;
        }

        private ArtiToken Current
        {
            get { return Peek(0); }
        }

        private ArtiToken Previous
        {
            get { return _tokens[Math.Max(0, _index - 1)]; }
        }

        private bool ConsumeSeparators()
        {
            bool consumed = false;
            while (Match(ArtiTokenKind.NewLine) || Match(ArtiTokenKind.Semicolon))
            {
                consumed = true;
            }

            return consumed;
        }

        private void SkipNewLines()
        {
            while (Match(ArtiTokenKind.NewLine))
            {
            }
        }

        private static bool IsStatementTerminator(ArtiTokenKind kind)
        {
            return kind == ArtiTokenKind.NewLine || kind == ArtiTokenKind.Semicolon || kind == ArtiTokenKind.RightBrace || kind == ArtiTokenKind.EndOfFile;
        }

        private static bool IsAssignmentOperator(ArtiTokenKind kind)
        {
            return kind == ArtiTokenKind.Equal || kind == ArtiTokenKind.PlusEqual || kind == ArtiTokenKind.MinusEqual || kind == ArtiTokenKind.StarEqual || kind == ArtiTokenKind.SlashEqual || kind == ArtiTokenKind.PercentEqual;
        }

        private static int GetPrecedence(ArtiTokenKind kind)
        {
            switch (kind)
            {
                case ArtiTokenKind.Pipe: return 1;
                case ArtiTokenKind.OrOr: return 2;
                case ArtiTokenKind.AndAnd: return 3;
                case ArtiTokenKind.QuestionQuestion: return 4;
                case ArtiTokenKind.EqualEqual:
                case ArtiTokenKind.BangEqual: return 5;
                case ArtiTokenKind.Less:
                case ArtiTokenKind.LessEqual:
                case ArtiTokenKind.Greater:
                case ArtiTokenKind.GreaterEqual: return 6;
                case ArtiTokenKind.Plus:
                case ArtiTokenKind.Minus: return 7;
                case ArtiTokenKind.Star:
                case ArtiTokenKind.Slash:
                case ArtiTokenKind.Percent: return 8;
                default: return -1;
            }
        }

        private static ArtiSourceSpan Combine(ArtiSourceSpan first, ArtiSourceSpan last)
        {
            int start = Math.Min(first.StartOffset, last.StartOffset);
            int end = Math.Max(first.EndOffset, last.EndOffset);
            return new ArtiSourceSpan(start, end - start, first.Line, first.Column);
        }

    }
}
