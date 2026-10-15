// Derived from AdvancedRimTalk Source/Arti (MIT License).
// Copyright (c) 2026 Nanaloveyuki
// Adapted for Oxygendsl.Language; original prompt/RimTalk/Verse host interfaces are not retained.
using System;
using System.Collections.Generic;

namespace Oxygendsl.Language
{
    public sealed class ArtiAnalysisResult
    {
        public ArtiAnalysisResult(IList<ArtiDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IList<ArtiDiagnostic> Diagnostics { get; }
        public bool HasErrors { get { return HasError(Diagnostics); } }

        private static bool HasError(IEnumerable<ArtiDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return false;
            }

            foreach (ArtiDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic != null && diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class ArtiAnalyzer : ArtiDiagnosticReporter
    {
        private static readonly string[] BuiltinNames = CreateBuiltinNames();

        private static string[] CreateBuiltinNames()
        {
            string[] language =
            {
                "core", "range", "exists", "len", "lang", "int", "float", "bool", "string", "list", "array",
                "remove_space", "remove_spaces", "append", "prepend", "upper", "uppercase",
                "lower", "lowercase", "capitalize", "title", "trim", "trim_start", "trim_end",
                "replace", "contains", "starts_with", "ends_with", "substring", "split",
                "repeat", "is_empty", "random", "pawn", "game"
            };
            IReadOnlyList<OxygenDeclaration> declarations = OxygenDeclarations.All;
            string[] names = new string[language.Length + declarations.Count];
            Array.Copy(language, names, language.Length);
            for (int index = 0; index < declarations.Count; index++)
            {
                names[language.Length + index] = declarations[index].Name;
            }

            return names;
        }

        private static readonly string[] BuiltinTypeNames =
        {
            "any", "int", "float", "bool", "string", "list", "array", "object", "void"
        };

        private bool _isModule;
        private bool _importsClosed;
        private Scope _root;
        private readonly HashSet<string> _traitImpls = new HashSet<string>(StringComparer.Ordinal);

        public ArtiAnalysisResult Analyze(ArtiProgram program)
        {
            return Analyze(program, false);
        }

        public ArtiAnalysisResult Analyze(ArtiProgram program, bool isModule)
        {
            ClearDiagnostics();
            _isModule = isModule;
            _importsClosed = false;
            _root = null;
            _traitImpls.Clear();
            if (program == null)
            {
                ReportError(3000);
                return new ArtiAnalysisResult(SnapshotDiagnostics());
            }

            _root = new Scope(null);
            _root.Statements = program.Statements;
            AnalyzeStatements(program.Statements, _root, 0, 0, true, null);
            return new ArtiAnalysisResult(SnapshotDiagnostics());
        }

        private void AnalyzeStatements(IList<ArtiStatement> statements, Scope scope, int functionDepth, int loopDepth, bool isRoot, ArtiFunctionDeclarationStatement currentFunction)
        {
            if (statements == null)
            {
                return;
            }

            if (isRoot)
            {
                PredeclareTopLevel(statements, scope);
            }
            else
            {
                PredeclareFunctions(statements, scope);
            }

            foreach (ArtiStatement statement in statements)
            {
                AnalyzeStatement(statement, scope, functionDepth, loopDepth, isRoot, currentFunction);
            }
        }

        private void PredeclareTopLevel(IList<ArtiStatement> statements, Scope scope)
        {
            foreach (ArtiStatement statement in statements)
            {
                ArtiUseStatement use = statement as ArtiUseStatement;
                if (use != null)
                {
                    DeclareImport(use, scope);
                    continue;
                }

                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    DeclareName(function.Name, BindingKind.Function, function.Span, scope, false);
                    continue;
                }

                ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
                if (declaration != null)
                {
                    DeclareName(declaration.Name, BindingKind.Struct, declaration.Span, scope, false);
                    continue;
                }

                ArtiTraitDeclarationStatement trait = statement as ArtiTraitDeclarationStatement;
                if (trait != null)
                {
                    DeclareName(trait.Name, BindingKind.Trait, trait.Span, scope, false);
                }
            }
        }

        private void PredeclareFunctions(IList<ArtiStatement> statements, Scope scope)
        {
            foreach (ArtiStatement statement in statements)
            {
                ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                if (function != null)
                {
                    DeclareName(function.Name, BindingKind.Function, function.Span, scope, false);
                }
            }
        }

        private void AnalyzeStatement(ArtiStatement statement, Scope scope, int functionDepth, int loopDepth, bool isRoot, ArtiFunctionDeclarationStatement currentFunction)
        {
            if (statement == null)
            {
                return;
            }

            ArtiUseStatement use = statement as ArtiUseStatement;
            if (use != null)
            {
                if (!isRoot || _importsClosed)
                {
                    ReportError(3017, use.Span);
                }

                if (string.IsNullOrEmpty(use.ModulePath) || string.IsNullOrEmpty(use.PackageId))
                {
                    ReportError(3010, use.Span);
                }

                if (!IsValidIdentifierName(use.Alias))
                {
                    ReportError(3015, use.Span, use.Alias);
                }

                return;
            }

            _importsClosed = true;

            if (isRoot && _isModule && !IsLibraryStatement(statement))
            {
                ReportError(3028, statement.Span);
            }

            ArtiVariableDeclarationStatement variable = statement as ArtiVariableDeclarationStatement;
            if (variable != null)
            {
                CheckType(variable.Type, false, scope);
                if (variable.IsPublic && (!_isModule || !isRoot || !variable.IsConst))
                {
                    ReportError(3027, variable.Span);
                }

                if (variable.IsConst && !ArtiConstantExpression.IsStatic(variable.Value, name =>
                {
                    Binding binding;
                    return scope.TryResolve(name, out binding) && binding.Kind == BindingKind.Constant;
                }))
                {
                    ReportError(3016, variable.Span);
                }

                DeclareName(variable.Name, variable.IsConst ? BindingKind.Constant : BindingKind.Variable, variable.Span, scope, true);
                CheckValue(variable.Value, variable.Type, scope);
                return;
            }

            ArtiUnpackDeclarationStatement unpack = statement as ArtiUnpackDeclarationStatement;
            if (unpack != null)
            {
                AnalyzeExpression(unpack.Value, scope);
                foreach (ArtiExpression target in unpack.Targets.Items)
                {
                    ArtiNameExpression binding = target as ArtiNameExpression;
                    if (binding == null)
                    {
                        ReportError(3014, target.Span);
                    }
                    else
                    {
                        DeclareName(binding.Name, BindingKind.Variable, binding.Span, scope, true);
                    }
                }

                return;
            }

            ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
            if (function != null)
            {
                AnalyzeFunction(function, scope, functionDepth, isRoot, false, null);
                return;
            }

            ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
            if (declaration != null)
            {
                if (!isRoot)
                {
                    ReportError(2024, declaration.Span);
                }

                if (declaration.IsPublic && (!isRoot || !_isModule))
                {
                    ReportError(3027, declaration.Span);
                }

                AnalyzeStruct(declaration, scope);
                return;
            }

            ArtiTraitDeclarationStatement trait = statement as ArtiTraitDeclarationStatement;
            if (trait != null)
            {
                if (!isRoot)
                {
                    ReportError(2036, trait.Span);
                }

                if (trait.IsPublic && (!isRoot || !_isModule))
                {
                    ReportError(3027, trait.Span);
                }

                AnalyzeTrait(trait, scope);
                return;
            }

            ArtiImplDeclarationStatement implementation = statement as ArtiImplDeclarationStatement;
            if (implementation != null)
            {
                if (!isRoot)
                {
                    ReportError(2036, implementation.Span);
                }

                AnalyzeImpl(implementation, scope);
                return;
            }


            ArtiAssignmentStatement assignment = statement as ArtiAssignmentStatement;
            if (assignment != null)
            {
                AnalyzeAssignment(assignment, scope);
                return;
            }

            ArtiIfStatement conditional = statement as ArtiIfStatement;
            if (conditional != null)
            {
                foreach (ArtiIfBranch branch in conditional.Branches)
                {
                    AnalyzeExpression(branch.Condition, scope);
                    AnalyzeStatements(branch.Body.Statements, new Scope(scope, true), functionDepth, loopDepth, false, currentFunction);
                }

                if (conditional.ElseBody != null)
                {
                    AnalyzeStatements(conditional.ElseBody.Statements, new Scope(scope, true), functionDepth, loopDepth, false, currentFunction);
                }

                return;
            }

            ArtiForStatement loop = statement as ArtiForStatement;
            if (loop != null)
            {
                AnalyzeExpression(loop.Source, scope);
                Scope loopScope = new Scope(scope, true);
                DeclareName(loop.VariableName, BindingKind.Variable, loop.Span, loopScope, true);
                AnalyzeStatements(loop.Body.Statements, loopScope, functionDepth, loopDepth + 1, false, currentFunction);
                return;
            }

            ArtiWhileStatement whileLoop = statement as ArtiWhileStatement;
            if (whileLoop != null)
            {
                AnalyzeExpression(whileLoop.Condition, scope);
                AnalyzeStatements(whileLoop.Body.Statements, new Scope(scope, true), functionDepth, loopDepth + 1, false, currentFunction);
                return;
            }

            ArtiReturnStatement returnStatement = statement as ArtiReturnStatement;
            if (returnStatement != null)
            {
                if (functionDepth == 0)
                {
                    ReportError(3004, returnStatement.Span);
                }

                if (currentFunction != null && IsVoid(currentFunction.ReturnType) && returnStatement.Value != null)
                {
                    ReportError(3020, returnStatement.Span, "void");
                }
                else if (currentFunction != null && !IsVoid(currentFunction.ReturnType))
                {
                    CheckValue(returnStatement.Value, currentFunction.ReturnType, scope);
                }
                else
                {
                    AnalyzeExpression(returnStatement.Value, scope);
                }

                return;
            }

            if (statement is ArtiBreakStatement)
            {
                if (loopDepth == 0)
                {
                    ReportError(3005, statement.Span);
                }

                return;
            }

            if (statement is ArtiContinueStatement)
            {
                if (loopDepth == 0)
                {
                    ReportError(3006, statement.Span);
                }

                return;
            }

            ArtiBlockStatement block = statement as ArtiBlockStatement;
            if (block != null)
            {
                AnalyzeStatements(block.Statements, new Scope(scope, true), functionDepth, loopDepth, false, currentFunction);
                return;
            }

            ArtiExpressionStatement expression = statement as ArtiExpressionStatement;
            if (expression != null)
            {
                if (isRoot && IsRuleRegistration(expression.Expression))
                {
                    AnalyzeRuleRegistration((ArtiCallExpression)expression.Expression, scope);
                    return;
                }

                AnalyzeExpression(expression.Expression, scope);
            }
        }

        private void AnalyzeStruct(ArtiStructDeclarationStatement declaration, Scope scope)
        {
            HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
            List<string> embedded = new List<string>();
            Scope fieldScope = new Scope(scope);
            fieldScope.TryDeclare("Self", BindingKind.Struct);
            foreach (ArtiStructField field in declaration.Fields)
            {
                if (field == null)
                {
                    continue;
                }

                if (field.Name == "self" || !members.Add(field.Name))
                {
                    ReportError(3023, field.Span, field.Name);
                }

                if (field.IsEmbedded)
                {
                    if (field.Type == null || field.Type.IsNullable || field.Type.ElementType != null
                        || (!IsStructType(field.Type, fieldScope) && !IsImportedTypeName(field.Type.Name)))
                    {
                        ReportError(3022, field.Span, field.Type == null ? string.Empty : Display(field.Type));
                    }
                    else
                    {
                        embedded.Add(field.Type.Name);
                    }
                }
                else
                {
                    CheckType(field.Type, false, fieldScope);
                }

                if (field.DefaultValue != null)
                {
                    CheckValue(field.DefaultValue, field.Type, fieldScope);
                }
            }

            foreach (ArtiFunctionDeclarationStatement method in declaration.Methods)
            {
                if (method == null)
                {
                    continue;
                }

                if (method.Name == "self" || !members.Add(method.Name))
                {
                    ReportError(3023, method.Span, method.Name);
                }
            }

            DetectEmbedCycle(declaration.Name, embedded, declaration.Span, new List<string>());
            Scope typeScope = new Scope(scope);
            typeScope.TryDeclare("Self", BindingKind.Struct);
            Scope methodScope = new Scope(typeScope, false, true);
            foreach (ArtiFunctionDeclarationStatement method in declaration.Methods)
            {
                bool closed = _importsClosed;
                _importsClosed = true;
                AnalyzeFunction(method, method.IsStatic ? typeScope : methodScope, 0, false, true, declaration.Name);
                _importsClosed = closed;
            }
        }

        private void AnalyzeFunction(ArtiFunctionDeclarationStatement function, Scope scope, int functionDepth, bool isRoot, bool isMethod, string selfType)
        {
            if (function == null)
            {
                return;
            }

            if (function.IsPublic && (!isRoot || !_isModule))
            {
                ReportError(3027, function.Span);
            }

            if (function.IsStatic && !isMethod)
            {
                ReportError(3033, function.Span);
            }

            if (function.TypeParameters != null && function.TypeParameters.Count > 0 && isMethod)
            {
                ReportError(3030, function.Span);
            }

            Scope typeScope = scope;
            if (!isMethod && function.TypeParameters != null && function.TypeParameters.Count > 0)
            {
                typeScope = new Scope(scope);
                foreach (ArtiTypeParameter typeParameter in function.TypeParameters)
                {
                    if (typeParameter == null)
                    {
                        continue;
                    }

                    if (typeParameter.Constraint == null)
                    {
                        ReportError(3036, typeParameter.Span, typeParameter.Name);
                    }
                    else
                    {
                        CheckType(typeParameter.Constraint, false, scope, true);
                    }

                    DeclareName(typeParameter.Name, BindingKind.TypeParameter, typeParameter.Span, typeScope, false);
                }
            }

            CheckType(function.ReturnType, true, typeScope);
            if (IsVoid(function.ReturnType) == false && ImplicitNullConflicts(function))
            {
                ReportError(3020, function.Span, Display(function.ReturnType));
            }

            Scope functionScope = new Scope(typeScope);
            if (isMethod && !function.IsStatic)
            {
                functionScope.TryDeclare("self", BindingKind.Self);
            }
            else if (isMethod)
            {
                functionScope.TryDeclare("Self", BindingKind.Struct);
            }

            foreach (ArtiParameter parameter in function.Parameters)
            {
                if (parameter == null)
                {
                    continue;
                }

                if (isMethod && parameter.Name == "self")
                {
                    ReportError(3025, parameter.Span);
                }

                CheckType(parameter.Type, false, typeScope);
                DeclareName(parameter.Name, BindingKind.Parameter, parameter.Span, functionScope, false);
            }

            AnalyzeStatements(function.Body == null ? null : function.Body.Statements, functionScope, functionDepth + 1, 0, false, function);
        }

        private void AnalyzeTrait(ArtiTraitDeclarationStatement trait, Scope scope)
        {
            HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
            Scope signatureScope = new Scope(scope);
            signatureScope.TryDeclare("Self", BindingKind.Struct);
            foreach (ArtiFunctionDeclarationStatement method in trait.Methods)
            {
                if (method == null)
                {
                    continue;
                }

                if (method.Name == "self" || !members.Add(method.Name))
                {
                    ReportError(3037, method.Span, method.Name);
                }

                Scope methodScope = signatureScope;
                if (!IsTraitSignature(method, methodScope))
                {
                    ReportError(3031, method.Span);
                }

                CheckType(method.ReturnType, true, methodScope);
                if (method.Parameters != null)
                {
                    foreach (ArtiParameter parameter in method.Parameters)
                    {
                        if (parameter == null)
                        {
                            continue;
                        }

                        if (parameter.Name == "self")
                        {
                            ReportError(3025, parameter.Span);
                        }

                        CheckType(parameter.Type, false, methodScope);
                    }
                }
            }

        }

        private void AnalyzeImpl(ArtiImplDeclarationStatement implementation, Scope scope)
        {
            if (implementation.IsPublic)
            {
                ReportError(3032, implementation.Span);
            }

            if (implementation.Target == null || !IsLocalStruct(implementation.Target))
            {
                ReportError(3039, implementation.Target == null ? implementation.Span : implementation.Target.Span);
            }

            ArtiTraitDeclarationStatement trait = implementation.Trait == null ? null : FindTrait(implementation.Trait, scope);
            if (implementation.Trait != null && trait == null && !IsImportedTypeName(implementation.Trait.Name))
            {
                ReportError(3018, implementation.Trait.Span, Display(implementation.Trait));
            }

            if (trait != null && implementation.Target != null && !string.IsNullOrEmpty(implementation.Target.Name)
                && !_traitImpls.Add(trait.Name + "\n" + implementation.Target.Name))
            {
                ReportError(3040, implementation.Span, trait.Name);
            }


            HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
            string selfType = implementation.Target == null ? string.Empty : implementation.Target.Name;
            Scope typeScope = new Scope(scope);
            typeScope.TryDeclare("Self", BindingKind.Struct);
            Scope methodScope = new Scope(typeScope, false, true);
            foreach (ArtiFunctionDeclarationStatement method in implementation.Methods)
            {
                if (method == null)
                {
                    continue;
                }

                if (method.Name == "self" || !members.Add(method.Name))
                {
                    ReportError(3037, method.Span, method.Name);
                }

                if (trait != null && FindMethod(trait.Methods, method.Name) == null)
                {
                    ReportError(3041, method.Span, method.Name);
                }
                else if (trait != null && !SignaturesMatch(FindMethod(trait.Methods, method.Name), method, selfType))
                {
                    ReportError(3040, method.Span, method.Name);
                }

                bool closed = _importsClosed;
                _importsClosed = true;
                AnalyzeFunction(method, method.IsStatic ? typeScope : methodScope, 0, false, true, selfType);
                _importsClosed = closed;
            }

            if (trait != null)
            {
                foreach (ArtiFunctionDeclarationStatement required in trait.Methods)
                {
                    if (required != null && FindMethod(implementation.Methods, required.Name) == null)
                    {
                        ReportError(3040, implementation.Span, required.Name);
                    }
                }
            }
        }

        private void DetectEmbedCycle(string name, IList<string> embedded, ArtiSourceSpan span, List<string> chain)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (chain.Contains(name))
            {
                chain.Add(name);
                ReportError(3024, span, string.Join(" -> ", chain.ToArray()));
                return;
            }

            chain.Add(name);
            foreach (string next in embedded)
            {
                ArtiStructDeclarationStatement target = FindStruct(next);
                if (target == null)
                {
                    continue;
                }

                List<string> nextEmbedded = new List<string>();
                foreach (ArtiStructField field in target.Fields)
                {
                    if (field != null && field.IsEmbedded && field.Type != null && !field.Type.IsNullable)
                    {
                        nextEmbedded.Add(field.Type.Name);
                    }
                }

                DetectEmbedCycle(next, nextEmbedded, span, chain);
            }

            chain.RemoveAt(chain.Count - 1);
        }

        private void AnalyzeAssignment(ArtiAssignmentStatement assignment, Scope scope)
        {
            if (assignment.Target is ArtiArrayExpression targets)
            {
                if (assignment.Operator != ArtiTokenKind.Equal)
                {
                    ReportError(3014, assignment.Span);
                }

                foreach (ArtiExpression target in targets.Items)
                {
                    if (target is ArtiArrayExpression)
                    {
                        ReportError(3014, target.Span);
                    }
                    else
                    {
                        AnalyzeAssignment(new ArtiAssignmentStatement(target.Span, target, assignment.Operator, null), scope);
                    }
                }

                AnalyzeExpression(assignment.Value, scope);
                return;
            }

            ArtiNameExpression name = assignment.Target as ArtiNameExpression;
            if (name != null)
            {
                Binding binding;
                if (name.Name == "_")
                {
                    if (assignment.Operator != ArtiTokenKind.Equal)
                    {
                        ReportError(3014, name.Span);
                    }
                }
                else if (!scope.TryResolve(name.Name, out binding))
                {
                    ReportError(3007, name.Span, name.Name);
                }
                else if (binding.Kind == BindingKind.Constant
                    || binding.Kind == BindingKind.Function
                    || binding.Kind == BindingKind.Struct
                    || binding.Kind == BindingKind.Trait
                    || binding.Kind == BindingKind.TypeParameter
                    || binding.Kind == BindingKind.Module
                    || binding.Kind == BindingKind.Self)
                {
                    ReportError(binding.Kind == BindingKind.TypeParameter ? 3035 : name.Name == "self" ? 3026 : 3008, name.Span, name.Name);
                }
            }
            else if (assignment.Target is ArtiMemberExpression || assignment.Target is ArtiIndexExpression)
            {
                AnalyzeExpression(assignment.Target, scope);
            }
            else
            {
                if (!(assignment.Target is ArtiErrorExpression))
                {
                    ReportError(3014, assignment.Target.Span);
                }

                AnalyzeExpression(assignment.Target, scope);
            }

            AnalyzeExpression(assignment.Value, scope);
        }

        private static bool IsRuleRegistration(ArtiExpression expression)
        {
            ArtiCallExpression call = expression as ArtiCallExpression;
            ArtiNameExpression name = call == null ? null : call.Target as ArtiNameExpression;
            return name != null && OxygenDeclarations.IsRegistration(name.Name);
        }

        private void AnalyzeRuleRegistration(ArtiCallExpression call, Scope scope)
        {
            foreach (ArtiArgument argument in call.Arguments)
            {
                if (argument != null && argument.Name != "when")
                {
                    AnalyzeExpression(argument.Value, scope);
                }
            }
        }

        private void AnalyzeExpression(ArtiExpression expression, Scope scope)
        {
            if (expression == null || expression is ArtiErrorExpression || expression is ArtiLiteralExpression)
            {
                return;
            }

            ArtiInterpolatedStringExpression interpolated = expression as ArtiInterpolatedStringExpression;
            if (interpolated != null)
            {
                foreach (ArtiExpression part in interpolated.Parts)
                {
                    AnalyzeExpression(part, scope);
                }

                return;
            }

            ArtiNameExpression name = expression as ArtiNameExpression;
            if (name != null)
            {
                Binding ignored;
                if (!scope.TryResolve(name.Name, out ignored) && !IsBuiltinName(name.Name))
                {
                    ReportError(3009, name.Span, name.Name);
                }

                return;
            }

            ArtiMemberExpression member = expression as ArtiMemberExpression;
            if (member != null)
            {
                AnalyzeExpression(member.Target, scope);
                return;
            }

            ArtiIndexExpression index = expression as ArtiIndexExpression;
            if (index != null)
            {
                AnalyzeExpression(index.Target, scope);
                AnalyzeExpression(index.Index, scope);
                return;
            }

            ArtiCallExpression call = expression as ArtiCallExpression;
            if (call != null)
            {
                if (call.TypeArguments != null)
                {
                    foreach (ArtiTypeReference typeArgument in call.TypeArguments)
                    {
                        CheckType(typeArgument, false, scope);
                    }
                }

                if (call.Target is ArtiNameExpression check && check.Name == "exists" && !scope.TryResolve(check.Name, out Binding overridden))
                {
                    foreach (ArtiArgument argument in call.Arguments)
                    {
                        AnalyzeExistenceTarget(argument.Value, scope);
                    }

                    return;
                }

                if (call.Target is ArtiMemberExpression probe && probe.Member == "exists" && call.Arguments.Count == 0)
                {
                    AnalyzeExistenceTarget(probe.Target, scope);
                    return;
                }

                AnalyzeExpression(call.Target, scope);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    AnalyzeExpression(argument == null ? null : argument.Value, scope);
                }

                return;
            }

            ArtiArrayExpression array = expression as ArtiArrayExpression;
            if (array != null)
            {
                foreach (ArtiExpression item in array.Items)
                {
                    AnalyzeExpression(item, scope);
                }

                return;
            }

            ArtiObjectExpression obj = expression as ArtiObjectExpression;
            if (obj != null)
            {
                foreach (ArtiObjectMember memberValue in obj.Members)
                {
                    AnalyzeExpression(memberValue.Value, scope);
                }

                return;
            }

            ArtiUnaryExpression unary = expression as ArtiUnaryExpression;
            if (unary != null)
            {
                AnalyzeExpression(unary.Operand, scope);
                return;
            }

            ArtiBinaryExpression binary = expression as ArtiBinaryExpression;
            if (binary != null)
            {
                AnalyzeExpression(binary.Left, scope);
                AnalyzeExpression(binary.Right, scope);
            }
        }

        private void CheckValue(ArtiExpression value, ArtiTypeReference expected, Scope scope)
        {
            AnalyzeExpression(value, scope);
            if (expected == null || IsAny(expected) || value == null)
            {
                return;
            }

            string actual = Infer(value);
            if (actual == null)
            {
                return;
            }

            if (!IsAssignable(actual, expected))
            {
                ReportError(3021, value.Span, Display(expected));
            }
        }

        private void CheckType(ArtiTypeReference type, bool allowVoid, Scope scope)
        {
            CheckType(type, allowVoid, scope, false);
        }

        private void CheckType(ArtiTypeReference type, bool allowVoid, Scope scope, bool constraint)
        {
            if (type == null)
            {
                return;
            }

            if (type.Name == "Self" && !IsSelfInScope(scope))
            {
                ReportError(3034, type.Span);
            }

            if (type.ElementType != null)
            {
                if (type.Name != "list" && type.Name != "array")
                {
                    ReportError(3018, type.Span, Display(type));
                }

                CheckType(type.ElementType, false, scope, constraint);
            }
            else if (IsBuiltinTypeName(type.Name) || type.Name == "Self" || IsTypeParameter(type, scope))
            {
                if (type.Name == "void" && !allowVoid)
                {
                    ReportError(3019, type.Span);
                }
            }
            else if (constraint)
            {
                if (!IsTraitType(type, scope) && !IsImportedTypeName(type.Name))
                {
                    ReportError(3018, type.Span, Display(type));
                }
            }
            else if (!IsStructType(type, scope) && !IsImportedTypeName(type.Name))
            {
                ReportError(3018, type.Span, Display(type));
            }

            if (type.IsNullable && type.Name == "void")
            {
                ReportError(3019, type.Span);
            }
        }

        private bool IsStructType(ArtiTypeReference type, Scope scope)
        {
            if (type == null || type.ElementType != null || type.Name.IndexOf('.') >= 0)
            {
                return false;
            }

            Binding binding;
            return scope.TryResolve(type.Name, out binding) && binding.Kind == BindingKind.Struct;
        }

        private bool IsImportedTypeName(string name)
        {
            int separator = name == null ? -1 : name.IndexOf('.');
            if (separator <= 0 || _root == null)
            {
                return false;
            }

            Binding binding;
            return _root.TryResolve(name.Substring(0, separator), out binding) && binding.Kind == BindingKind.Module;
        }

        private ArtiStructDeclarationStatement FindStruct(string name)
        {
            if (_root == null || _root.Statements == null)
            {
                return null;
            }

            foreach (ArtiStatement statement in _root.Statements)
            {
                ArtiStructDeclarationStatement declaration = statement as ArtiStructDeclarationStatement;
                if (declaration != null && declaration.Name == name)
                {
                    return declaration;
                }
            }

            return null;
        }

        private static bool IsSelfInScope(Scope scope)
        {
            Binding binding;
            return scope != null && scope.TryResolve("Self", out binding);
        }

        private static bool IsTypeParameter(ArtiTypeReference type, Scope scope)
        {
            if (type == null || type.ElementType != null || scope == null || type.Name.IndexOf('.') >= 0)
            {
                return false;
            }

            Binding binding;
            return scope.TryResolve(type.Name, out binding) && binding.Kind == BindingKind.TypeParameter;
        }

        private bool IsTraitType(ArtiTypeReference type, Scope scope)
        {
            if (type == null || type.ElementType != null || type.Name.IndexOf('.') >= 0)
            {
                return false;
            }

            Binding binding;
            return scope.TryResolve(type.Name, out binding) && binding.Kind == BindingKind.Trait;
        }

        private bool IsLocalStruct(ArtiTypeReference type)
        {
            return type != null && !type.IsNullable && type.ElementType == null && type.Name.IndexOf('.') < 0 && FindStruct(type.Name) != null;
        }

        private ArtiTraitDeclarationStatement FindTrait(ArtiTypeReference type, Scope scope)
        {
            if (type == null || !IsTraitType(type, scope) || _root == null || _root.Statements == null)
            {
                return null;
            }

            foreach (ArtiStatement statement in _root.Statements)
            {
                ArtiTraitDeclarationStatement trait = statement as ArtiTraitDeclarationStatement;
                if (trait != null && trait.Name == type.Name)
                {
                    return trait;
                }
            }

            return null;
        }


        private static bool IsTraitSignature(ArtiFunctionDeclarationStatement method, Scope scope)
        {
            if (method == null || method.Body != null || method.ReturnType == null
                || (method.TypeParameters != null && method.TypeParameters.Count > 0))
            {
                return false;
            }

            if (method.Parameters != null)
            {
                foreach (ArtiParameter parameter in method.Parameters)
                {
                    if (parameter == null || parameter.Type == null)
                    {
                        return false;
                    }
                }
            }

            return scope != null;
        }

        private static ArtiFunctionDeclarationStatement FindMethod(IList<ArtiFunctionDeclarationStatement> methods, string name)
        {
            if (methods == null)
            {
                return null;
            }

            foreach (ArtiFunctionDeclarationStatement method in methods)
            {
                if (method != null && method.Name == name)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool SignaturesMatch(ArtiFunctionDeclarationStatement required, ArtiFunctionDeclarationStatement actual, string selfType)
        {
            if (required == null || actual == null || required.IsStatic != actual.IsStatic
                || required.Parameters.Count != actual.Parameters.Count)
            {
                return false;
            }

            ArtiTypeReference actualReturn = actual.ReturnType == null ? AnyType(actual.Span) : actual.ReturnType;
            if (!TypesMatch(required.ReturnType, actualReturn, selfType))
            {
                return false;
            }

            for (int index = 0; index < required.Parameters.Count; index++)
            {
                ArtiParameter left = required.Parameters[index];
                ArtiParameter right = actual.Parameters[index];
                ArtiTypeReference actualType = right == null || right.Type == null ? AnyType(right == null ? actual.Span : right.Span) : right.Type;
                if (left == null || right == null || left.Name != right.Name || !TypesMatch(left.Type, actualType, selfType))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TypesMatch(ArtiTypeReference required, ArtiTypeReference actual, string selfType)
        {
            if (required == null || actual == null)
            {
                return required == actual;
            }

            string requiredName = required.Name == "Self" ? selfType : required.Name;
            string actualName = actual.Name == "Self" ? selfType : actual.Name;
            if (requiredName != actualName || required.IsNullable != actual.IsNullable)
            {
                return false;
            }

            return TypesMatch(required.ElementType, actual.ElementType, selfType);
        }

        private static ArtiTypeReference AnyType(ArtiSourceSpan span)
        {
            return new ArtiTypeReference(span, "any");
        }

        private void DeclareImport(ArtiUseStatement use, Scope scope)
        {
            if (!IsValidIdentifierName(use.Alias))
            {
                return;
            }

            if (IsReservedName(use.Alias) || !scope.TryDeclare(use.Alias, BindingKind.Module))
            {
                ReportError(3029, use.Span, use.Alias);
            }
        }

        private void DeclareName(string name, BindingKind kind, ArtiSourceSpan span, Scope scope, bool allowDiscard)
        {
            if (allowDiscard && name == "_")
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            IArtiCallable stringHelper;
            Binding reserved;
            if (name == "Self" || scope.TryResolve(name, out reserved) && reserved.Kind == BindingKind.TypeParameter
                || IsBuiltinTypeName(name) || (kind != BindingKind.Parameter && IsBuiltinName(name)
                && !ArtiStringFunctions.TryGetCallable(name, out stringHelper)))
            {
                ReportError(3013, span, name);
                return;
            }

            if (!scope.TryDeclare(name, kind))
            {
                Binding existing;
                bool traitConflict = scope.TryResolve(name, out existing)
                    && (existing.Kind == BindingKind.Trait || kind == BindingKind.Trait);
                ReportError(kind == BindingKind.Parameter ? 3003 : traitConflict ? 3042 : 3001, span, name);
            }
        }

        private static bool IsLibraryStatement(ArtiStatement statement)
        {
            return statement is ArtiUseStatement
                || statement is ArtiFunctionDeclarationStatement
                || statement is ArtiStructDeclarationStatement
                || statement is ArtiTraitDeclarationStatement
                || statement is ArtiImplDeclarationStatement
                || statement is ArtiVariableDeclarationStatement;
        }

        private static bool ImplicitNullConflicts(ArtiFunctionDeclarationStatement function)
        {
            if (function.ReturnType == null || function.ReturnType.IsNullable || IsAny(function.ReturnType) || IsVoid(function.ReturnType))
            {
                return false;
            }

            return !HasCertainReturn(function.Body);
        }

        private static bool HasCertainReturn(ArtiBlockStatement block)
        {
            if (block == null)
            {
                return false;
            }

            foreach (ArtiStatement statement in block.Statements)
            {
                if (statement is ArtiReturnStatement)
                {
                    return true;
                }

                ArtiIfStatement conditional = statement as ArtiIfStatement;
                if (conditional != null
                    && conditional.ElseBody != null
                    && conditional.Branches.Count > 0
                    && AllBranchesReturn(conditional))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AllBranchesReturn(ArtiIfStatement conditional)
        {
            foreach (ArtiIfBranch branch in conditional.Branches)
            {
                if (branch == null || !HasCertainReturn(branch.Body))
                {
                    return false;
                }
            }

            return HasCertainReturn(conditional.ElseBody);
        }

        private static string Infer(ArtiExpression expression)
        {
            ArtiLiteralExpression literal = expression as ArtiLiteralExpression;
            if (literal != null)
            {
                if (literal.Value == null) return "null";
                if (literal.Value is bool) return "bool";
                if (literal.Value is string) return "string";
                if (literal.Value is int) return "int";
                if (literal.Value is double || literal.Value is float) return "float";
            }

            if (expression is ArtiArrayExpression) return "list";
            if (expression is ArtiObjectExpression) return "object";
            ArtiInterpolatedStringExpression interpolated = expression as ArtiInterpolatedStringExpression;
            if (interpolated != null && interpolated.Parts.Count > 0) return "string";
            return null;
        }

        private static bool IsAssignable(string actual, ArtiTypeReference expected)
        {
            if (expected == null || IsAny(expected))
            {
                return true;
            }

            if (actual == "null")
            {
                return expected.IsNullable;
            }

            if (actual == "list")
            {
                return expected.Name == "list" || expected.Name == "array" || expected.Name == "any";
            }

            if (actual == "int" && expected.Name == "float" && expected.ElementType == null)
            {
                return true;
            }

            return expected.ElementType == null && expected.Name == actual;
        }

        private static bool IsAny(ArtiTypeReference type)
        {
            return type != null && type.Name == "any" && type.ElementType == null;
        }

        private static bool IsVoid(ArtiTypeReference type)
        {
            return type != null && type.Name == "void" && !type.IsNullable && type.ElementType == null;
        }

        private static string Display(ArtiTypeReference type)
        {
            if (type == null)
            {
                return "any";
            }

            string name = type.ElementType == null ? type.Name : type.Name + "<" + Display(type.ElementType) + ">";
            return type.IsNullable ? name + "?" : name;
        }

        private void AnalyzeExistenceTarget(ArtiExpression target, Scope scope)
        {
            if (target is ArtiNameExpression)
            {
                return;
            }

            if (target is ArtiMemberExpression member)
            {
                AnalyzeExistenceTarget(member.Target, scope);
            }
            else if (target is ArtiIndexExpression index)
            {
                AnalyzeExistenceTarget(index.Target, scope);
                AnalyzeExpression(index.Index, scope);
            }
            else
            {
                AnalyzeExpression(target, scope);
            }
        }

        private static bool IsValidIdentifierName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (!char.IsLetter(name[0]) && name[0] != '_')
            {
                return false;
            }

            for (int index = 1; index < name.Length; index++)
            {
                char value = name[index];
                if (!char.IsLetterOrDigit(value) && value != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBuiltinName(string name)
        {
            return Contains(BuiltinNames, name);
        }

        private static bool IsBuiltinTypeName(string name)
        {
            return Contains(BuiltinTypeNames, name);
        }

        private static bool IsReservedName(string name)
        {
            return IsBuiltinName(name) || IsBuiltinTypeName(name);
        }

        private static bool Contains(string[] values, string name)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == name)
                {
                    return true;
                }
            }

            return false;
        }

        private enum BindingKind
        {
            Variable,
            Constant,
            Function,
            Parameter,
            Module,
            Struct,
            Trait,
            TypeParameter,
            Self
        }

        private sealed class Binding
        {
            public Binding(BindingKind kind)
            {
                Kind = kind;
            }

            public BindingKind Kind { get; }
        }

        private sealed class Scope
        {
            private readonly IDictionary<string, Binding> _bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
            private readonly HashSet<string> _declared = new HashSet<string>(StringComparer.Ordinal);
            private readonly bool _transparent;

            public Scope(Scope parent, bool transparent = false, bool isMethod = false)
            {
                _transparent = transparent;
                IsMethod = isMethod || (parent != null && parent.IsMethod && transparent);
                Parent = transparent ? parent.Parent : parent;
                if (transparent)
                {
                    _bindings = parent._bindings;
                }
            }

            public Scope Parent { get; }
            public bool IsMethod { get; }
            public IList<ArtiStatement> Statements { get; set; }

            public bool TryDeclare(string name, BindingKind kind)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }

                Binding existing;
                if (_bindings.TryGetValue(name, out existing))
                {
                    if (!_declared.Contains(name)
                        && kind == BindingKind.Variable
                        && existing.Kind == BindingKind.Variable
                        && _transparent)
                    {
                        _bindings[name] = new Binding(kind);
                        _declared.Add(name);
                        return true;
                    }

                    return false;
                }

                _bindings.Add(name, new Binding(kind));
                _declared.Add(name);
                return true;
            }

            public bool TryResolve(string name, out Binding binding)
            {
                if (_bindings.TryGetValue(name, out binding))
                {
                    return true;
                }

                return Parent != null && Parent.TryResolve(name, out binding);
            }
        }
    }
}
