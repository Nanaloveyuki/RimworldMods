using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public static class ContentCompiler
    {
        public static ContentCompilation Compile(string source, IOxygenModuleCatalog modules = null, IOxygenLocalization localization = null)
        {
            ArtiParseResult parsed = new ArtiParser().Parse(source ?? string.Empty);
            ArtiAnalysisResult analysis = new ArtiAnalyzer().Analyze(parsed.Program);
            List<ScriptDiagnostic> diagnostics = new List<ScriptDiagnostic>();
            AddDiagnostics(diagnostics, parsed.Diagnostics);
            AddDiagnostics(diagnostics, analysis.Diagnostics);
            if (diagnostics.Count > 0)
            {
                return new ContentCompilation(null, diagnostics, Array.Empty<ContentImport>());
            }

            try
            {
                CompiledContent compiled = CompiledContent.Prepare(parsed.Program, modules, localization);
                ContentPlan plan = compiled.Register(parsed.Program, diagnostics);
                return diagnostics.Count == 0
                    ? new ContentCompilation(plan, diagnostics, compiled.Imports)
                    : new ContentCompilation(null, diagnostics, compiled.Imports);
            }
            catch (ContentRegistrationException exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, exception.Line, exception.Column));
                return new ContentCompilation(null, diagnostics, Array.Empty<ContentImport>());
            }
            catch (ScriptEvaluationException exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, exception.Line, exception.Column));
                return new ContentCompilation(null, diagnostics, Array.Empty<ContentImport>());
            }
            catch (Exception exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, 1, 1));
                return new ContentCompilation(null, diagnostics, Array.Empty<ContentImport>());
            }
        }

        internal static ContentCompilation RegisterPrepared(ArtiProgram program, ArtiExecutor executor, IOxygenModuleCatalog modules)
        {
            var diagnostics = new List<ScriptDiagnostic>();
            try
            {
                var compiled = new CompiledContent(program, executor, executor.TakeImportFingerprints(modules));
                return new ContentCompilation(compiled.Register(program, diagnostics, true), diagnostics, compiled.Imports);
            }
            catch (ContentRegistrationException exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, exception.Line, exception.Column));
            }
            catch (ScriptEvaluationException exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, exception.Line, exception.Column));
            }
            catch (Exception exception)
            {
                diagnostics.Add(new ScriptDiagnostic(exception.Message, 1, 1));
            }
            return new ContentCompilation(null, diagnostics, Array.Empty<ContentImport>());
        }

        private static void AddDiagnostics(List<ScriptDiagnostic> destination, IEnumerable<ArtiDiagnostic> source)
        {
            if (source == null) return;
            foreach (ArtiDiagnostic diagnostic in source)
            {
                if (diagnostic != null && diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                {
                    destination.Add(new ScriptDiagnostic(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column));
                }
            }
        }

        private sealed class CompiledContent
        {
            private readonly ArtiExecutor _executor;
            private readonly Dictionary<string, ArtiFunctionDeclarationStatement> _functions =
                new Dictionary<string, ArtiFunctionDeclarationStatement>(StringComparer.Ordinal);
            private readonly Dictionary<string, Template> _templates =
                new Dictionary<string, Template>(StringComparer.Ordinal);

            public CompiledContent(ArtiProgram program, ArtiExecutor executor, IReadOnlyList<ContentImport> imports)
            {
                _executor = executor;
                Imports = imports ?? Array.Empty<ContentImport>();
                if (program == null) return;
                foreach (ArtiStatement statement in program.Statements)
                {
                    ArtiFunctionDeclarationStatement function = statement as ArtiFunctionDeclarationStatement;
                    if (function != null) _functions[function.Name] = function;
                }
            }

            public IReadOnlyList<ContentImport> Imports { get; }

            public static CompiledContent Prepare(ArtiProgram program, IOxygenModuleCatalog modules, IOxygenLocalization localization)
            {
                ArtiExecutor executor = new ArtiExecutor();
                ArtiExecutionResult prepared = executor.Execute(program, new ArtiExecutionContext(modules: modules, localization: localization));
                if (prepared.HasErrors)
                {
                    ArtiDiagnostic diagnostic = FirstError(prepared.Diagnostics);
                    throw new ContentRegistrationException(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column);
                }

                return new CompiledContent(program, executor, executor.TakeImportFingerprints(modules));
            }

            public ContentPlan Register(ArtiProgram program, List<ScriptDiagnostic> diagnostics, bool allowStatements = false)
            {
                List<ContentDefinition> definitions = new List<ContentDefinition>();
                List<ContentPatch> patches = new List<ContentPatch>();
                List<ThoughtRuleDefinition> thoughts = new List<ThoughtRuleDefinition>();
                List<GateRuleDefinition> gates = new List<GateRuleDefinition>();
                HashSet<string> definitionIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> ruleIds = new HashSet<string>(StringComparer.Ordinal);
                if (program == null)
                {
                    return new ContentPlan(definitions, patches, thoughts, gates);
                }

                foreach (ArtiStatement statement in program.Statements)
                {
                    if (statement is ArtiFunctionDeclarationStatement || statement is ArtiVariableDeclarationStatement
                        || statement is ArtiStructDeclarationStatement || statement is ArtiUseStatement
                        || statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement)
                    {
                        continue;
                    }

                    ArtiExpressionStatement expression = statement as ArtiExpressionStatement;
                    ArtiCallExpression call = expression == null ? null : expression.Expression as ArtiCallExpression;
                    ArtiNameExpression target = call == null ? null : call.Target as ArtiNameExpression;
                    if (target == null || !OxygenDeclarations.IsRegistration(target.Name))
                    {
                        if (allowStatements) continue;
                        throw new ContentRegistrationException(
                            "Only imports, structs, traits, impls, functions, constants, variables, and registered declarations are allowed.",
                            statement.Span.Line, statement.Span.Column);
                    }

                    OxygenDeclaration declaration;
                    if (!OxygenDeclarations.TryGet(target.Name, out declaration))
                    {
                        throw new ContentRegistrationException("Declaration '" + target.Name + "' is not a content registration.", call.Span.Line, call.Span.Column);
                    }

                    switch (target.Name)
                    {
                        case "template":
                            ReadTemplate(call);
                            break;
                        case "define":
                            AcceptDefinition(ReadDefinition(call), definitions, definitionIds);
                            break;
                        case "patch":
                            patches.Add(ReadPatch(call));
                            break;
                        case "thought_rule":
                            AcceptThought(ReadThought(call), thoughts, ruleIds);
                            break;
                        case "gate_rule":
                            AcceptGate(ReadGate(call), gates, ruleIds);
                            break;
                        default:
                            if (declaration.Reader == null)
                            {
                                throw new ContentRegistrationException("Declaration '" + target.Name + "' is not a content registration.", call.Span.Line, call.Span.Column);
                            }
                            ReadRegistered(declaration, call, definitions, patches, thoughts, gates, definitionIds, ruleIds);
                            break;
                    }
                }

                if (diagnostics.Count > 0)
                {
                    return null;
                }

                return new ContentPlan(definitions, patches, thoughts, gates);
            }

            private void ReadRegistered(
                OxygenDeclaration declaration,
                ArtiCallExpression call,
                List<ContentDefinition> definitions,
                List<ContentPatch> patches,
                List<ThoughtRuleDefinition> thoughts,
                List<GateRuleDefinition> gates,
                HashSet<string> definitionIds,
                HashSet<string> ruleIds)
            {
                ContentRegistration registration = new ContentRegistration(declaration.Name, call.Span.Line, call.Span.Column, EvaluateArguments(call));
                ContentRegistrationResult result;
                try
                {
                    result = declaration.Reader(registration);
                }
                catch (ContentRegistrationException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new ContentRegistrationException(exception.Message, call.Span.Line, call.Span.Column);
                }

                if (result == null)
                {
                    throw new ContentRegistrationException("Declaration '" + declaration.Name + "' did not return content.", call.Span.Line, call.Span.Column);
                }

                if (result.Definitions != null)
                {
                    foreach (ContentDefinition definition in result.Definitions) AcceptDefinition(definition, definitions, definitionIds);
                }
                if (result.Patches != null)
                {
                    foreach (ContentPatch patch in result.Patches)
                    {
                        if (patch != null) patches.Add(patch);
                    }
                }
                if (result.Thoughts != null)
                {
                    foreach (ThoughtRuleDefinition thought in result.Thoughts) AcceptThought(thought, thoughts, ruleIds);
                }
                if (result.Gates != null)
                {
                    foreach (GateRuleDefinition gate in result.Gates) AcceptGate(gate, gates, ruleIds);
                }
            }

            private static void AcceptDefinition(ContentDefinition definition, List<ContentDefinition> definitions, HashSet<string> definitionIds)
            {
                if (definition == null) return;
                if (!definitionIds.Add((definition.Type ?? string.Empty) + "\n" + (definition.Id ?? string.Empty)))
                {
                    throw new ContentRegistrationException("Definition '" + definition.Id + "' is declared more than once.", definition.Line, definition.Column);
                }
                definitions.Add(definition);
            }

            private static void AcceptThought(ThoughtRuleDefinition thought, List<ThoughtRuleDefinition> thoughts, HashSet<string> ruleIds)
            {
                if (thought == null) return;
                if (!ruleIds.Add("thought\n" + (thought.Id ?? string.Empty)))
                {
                    throw new ContentRegistrationException("Rule id '" + thought.Id + "' is declared more than once.", thought.Line, thought.Column);
                }
                thoughts.Add(thought);
            }

            private static void AcceptGate(GateRuleDefinition gate, List<GateRuleDefinition> gates, HashSet<string> ruleIds)
            {
                if (gate == null) return;
                if (!ruleIds.Add("gate\n" + (gate.Id ?? string.Empty)))
                {
                    throw new ContentRegistrationException("Rule id '" + gate.Id + "' is declared more than once.", gate.Line, gate.Column);
                }
                gates.Add(gate);
            }

            private IReadOnlyList<ContentRegistrationArgument> EvaluateArguments(ArtiCallExpression call)
            {
                List<ContentRegistrationArgument> arguments = new List<ContentRegistrationArgument>();
                if (call.Arguments == null) return arguments;
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    ArtiSourceSpan span = argument.Value.Span;
                    arguments.Add(new ContentRegistrationArgument(argument.Name, Publish(_executor.EvaluatePrepared(argument.Value), span), span.Line, span.Column));
                }
                return arguments;
            }


            private void ReadTemplate(ArtiCallExpression call)
            {
                string name = null;
                string type = null;
                object fields = null;
                object extends = null;
                object mixins = null;
                bool sawPositional = false;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    if (argument.Name == null)
                    {
                        if (sawPositional || name != null)
                        {
                            throw Located("Only the template name may be positional.", argument.Value);
                        }
                        sawPositional = true;
                        name = ReadText(argument.Value, "name");
                        continue;
                    }
                    if (!seen.Add(argument.Name)) throw Located("Argument '" + argument.Name + "' was supplied more than once.", argument.Value);
                    switch (argument.Name)
                    {
                        case "name": name = ReadText(argument.Value, "name"); break;
                        case "type": type = ReadText(argument.Value, "type"); break;
                        case "fields": fields = EvaluatePublic(argument.Value); break;
                        case "extends": extends = EvaluatePublic(argument.Value); break;
                        case "mixins": mixins = EvaluatePublic(argument.Value); break;
                        default: throw Located("Unknown template argument '" + argument.Name + "'.", argument.Value);
                    }
                }

                RequireText(name, "name", call.Span);
                RequireText(type, "type", call.Span);
                if (_templates.ContainsKey(name))
                {
                    throw Located("Template '" + name + "' is declared more than once.", call);
                }

                _templates.Add(name, new Template(name, type, AsObject(fields, "fields", call.Span), extends, AsNameList(mixins, "mixins", call.Span), call.Span));
            }

            private ContentDefinition ReadDefinition(ArtiCallExpression call)
            {
                string type = null;
                string id = null;
                object fields = null;
                object extends = null;
                object mixins = null;
                int positional = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    if (argument.Name == null)
                    {
                        positional++;
                        if (positional == 1) type = ReadText(argument.Value, "type");
                        else if (positional == 2) id = ReadText(argument.Value, "id");
                        else throw Located("define accepts only type and id as positional arguments.", argument.Value);
                        continue;
                    }
                    if (!seen.Add(argument.Name)) throw Located("Argument '" + argument.Name + "' was supplied more than once.", argument.Value);
                    switch (argument.Name)
                    {
                        case "type": type = ReadText(argument.Value, "type"); break;
                        case "id": id = ReadText(argument.Value, "id"); break;
                        case "fields": fields = EvaluatePublic(argument.Value); break;
                        case "extends": extends = EvaluatePublic(argument.Value); break;
                        case "mixins": mixins = EvaluatePublic(argument.Value); break;
                        default: throw Located("Unknown define argument '" + argument.Name + "'.", argument.Value);
                    }
                }

                RequireText(type, "type", call.Span);
                RequireText(id, "id", call.Span);
                Composition composition = Compose(type, AsObject(fields, "fields", call.Span), extends, AsNameList(mixins, "mixins", call.Span), call.Span);
                return new ContentDefinition
                {
                    Id = id,
                    Type = type,
                    Fields = composition.Fields,
                    BaseDef = composition.BaseDef,
                    Line = call.Span.Line,
                    Column = call.Span.Column
                };
            }

            private ContentPatch ReadPatch(ArtiCallExpression call)
            {
                string type = null;
                string id = null;
                object set = null;
                object append = null;
                object remove = null;
                object requires = null;
                int positional = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    if (argument.Name == null)
                    {
                        positional++;
                        if (positional == 1) type = ReadText(argument.Value, "type");
                        else if (positional == 2) id = ReadText(argument.Value, "id");
                        else throw Located("patch accepts only type and id as positional arguments.", argument.Value);
                        continue;
                    }
                    if (!seen.Add(argument.Name)) throw Located("Argument '" + argument.Name + "' was supplied more than once.", argument.Value);
                    switch (argument.Name)
                    {
                        case "type": type = ReadText(argument.Value, "type"); break;
                        case "id": id = ReadText(argument.Value, "id"); break;
                        case "set": set = EvaluatePublic(argument.Value); break;
                        case "append": append = EvaluatePublic(argument.Value); break;
                        case "remove": remove = EvaluatePublic(argument.Value); break;
                        case "requires": requires = EvaluatePublic(argument.Value); break;
                        default: throw Located("Unknown patch argument '" + argument.Name + "'.", argument.Value);
                    }
                }

                RequireText(type, "type", call.Span);
                RequireText(id, "id", call.Span);
                IDictionary<string, object> setFields = AsObject(set, "set", call.Span);
                IDictionary<string, object> appendFields = AsPathMap(append, "append", call.Span);
                IDictionary<string, object> removeFields = AsPathMap(remove, "remove", call.Span);
                RejectIdentity(setFields, "set", call.Span);
                RejectIdentity(appendFields, "append", call.Span);
                RejectIdentity(removeFields, "remove", call.Span);
                return new ContentPatch
                {
                    Type = type,
                    Id = id,
                    Set = setFields,
                    Append = appendFields,
                    Remove = removeFields,
                    Requires = AsNameList(requires, "requires", call.Span),
                    Line = call.Span.Line,
                    Column = call.Span.Column
                };
            }

            private ThoughtRuleDefinition ReadThought(ArtiCallExpression call)
            {
                string id = null;
                object thought = null;
                ArtiExpression evaluate = null;
                bool sawPositional = false;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    if (argument.Name == null)
                    {
                        if (sawPositional || id != null) throw Located("Only the rule id may be positional.", argument.Value);
                        sawPositional = true;
                        id = ReadText(argument.Value, "id");
                        continue;
                    }
                    if (!seen.Add(argument.Name)) throw Located("Argument '" + argument.Name + "' was supplied more than once.", argument.Value);
                    switch (argument.Name)
                    {
                        case "id": id = ReadText(argument.Value, "id"); break;
                        case "thought": thought = EvaluatePublic(argument.Value); break;
                        case "evaluate": evaluate = argument.Value; break;
                        default: throw Located("Unknown thought_rule argument '" + argument.Name + "'.", argument.Value);
                    }
                }

                RequireText(id, "id", call.Span);
                DefReference reference = thought as DefReference;
                if (reference == null || reference.Type != "ThoughtDef" || string.IsNullOrEmpty(reference.Id))
                {
                    throw Located("thought must be a ThoughtDef reference.", call);
                }
                if (evaluate == null) throw Located("evaluate is required.", call);
                return new ThoughtRuleDefinition
                {
                    Id = id,
                    Thought = reference,
                    Evaluator = new ThoughtEvaluator(this, ReadFunction(evaluate, "evaluate")),
                    Line = call.Span.Line,
                    Column = call.Span.Column
                };
            }

            private GateRuleDefinition ReadGate(ArtiCallExpression call)
            {
                string id = null;
                string gate = null;
                ArtiExpression evaluate = null;
                bool sawPositional = false;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (ArtiArgument argument in call.Arguments)
                {
                    RequireArgument(argument, call);
                    if (argument.Name == null)
                    {
                        if (sawPositional || id != null) throw Located("Only the rule id may be positional.", argument.Value);
                        sawPositional = true;
                        id = ReadText(argument.Value, "id");
                        continue;
                    }
                    if (!seen.Add(argument.Name)) throw Located("Argument '" + argument.Name + "' was supplied more than once.", argument.Value);
                    switch (argument.Name)
                    {
                        case "id": id = ReadText(argument.Value, "id"); break;
                        case "gate": gate = ReadText(argument.Value, "gate"); break;
                        case "evaluate": evaluate = argument.Value; break;
                        default: throw Located("Unknown gate_rule argument '" + argument.Name + "'.", argument.Value);
                    }
                }

                RequireText(id, "id", call.Span);
                RequireText(gate, "gate", call.Span);
                if (evaluate == null) throw Located("evaluate is required.", call);
                return new GateRuleDefinition
                {
                    Id = id,
                    Gate = gate,
                    Evaluator = new GateEvaluator(this, ReadFunction(evaluate, "evaluate")),
                    Line = call.Span.Line,
                    Column = call.Span.Column
                };
            }

            private Composition Compose(string type, IDictionary<string, object> fields, object extends, IReadOnlyList<string> mixins, ArtiSourceSpan span)
            {
                IDictionary<string, object> merged = new Dictionary<string, object>(StringComparer.Ordinal);
                DefReference nativeBase = null;
                if (mixins != null)
                {
                    foreach (string mixin in mixins)
                    {
                        Composition expanded = RequireTemplate(mixin, type, span).Expand(this, span);
                        MergeInto(merged, expanded.Fields);
                        nativeBase = expanded.BaseDef ?? nativeBase;
                    }
                }

                if (extends != null)
                {
                    string templateName = extends as string;
                    DefReference reference = extends as DefReference;
                    if (templateName != null)
                    {
                        Template template = RequireTemplate(templateName, type, span);
                        Composition expanded = template.Expand(this, span);
                        MergeInto(merged, expanded.Fields);
                        nativeBase = expanded.BaseDef ?? nativeBase;
                    }
                    else if (reference != null)
                    {
                        if (reference.Type != type)
                        {
                            throw new ContentRegistrationException("extends reference type '" + reference.Type + "' does not match '" + type + "'.", span.Line, span.Column);
                        }
                        nativeBase = reference;
                    }
                    else
                    {
                        throw new ContentRegistrationException("extends must name a template or a typed Def reference.", span.Line, span.Column);
                    }
                }

                MergeInto(merged, fields);
                return new Composition(merged, nativeBase);
            }

            private Template RequireTemplate(string name, string type, ArtiSourceSpan span)
            {
                Template template;
                if (string.IsNullOrEmpty(name) || !_templates.TryGetValue(name, out template))
                {
                    throw new ContentRegistrationException("Template '" + (name ?? string.Empty) + "' is not declared.", span.Line, span.Column);
                }
                if (!string.Equals(template.Type, type, StringComparison.Ordinal))
                {
                    throw new ContentRegistrationException("Template '" + name + "' is " + template.Type + ", not " + type + ".", span.Line, span.Column);
                }
                return template;
            }

            private ArtiFunctionDeclarationStatement ReadFunction(ArtiExpression expression, string name)
            {
                ArtiNameExpression functionName = expression as ArtiNameExpression;
                ArtiFunctionDeclarationStatement function;
                if (functionName == null || !_functions.TryGetValue(functionName.Name, out function) || function.Parameters.Count != 1)
                {
                    throw Located(name + " must name a function that accepts one pawn.", expression);
                }
                return function;
            }

            private object Invoke(ArtiFunctionDeclarationStatement function, PawnContext pawn, GameContext game)
            {
                return _executor.InvokeIsolated(function, pawn, game);
            }

            private object EvaluatePublic(ArtiExpression expression)
            {
                return Publish(_executor.EvaluatePrepared(expression), expression == null ? ArtiSourceSpan.Empty : expression.Span);
            }

            private string ReadText(ArtiExpression expression, string name)
            {
                string value = EvaluatePublic(expression) as string;
                if (value == null) throw Located(name + " must be a string.", expression);
                if ((name == "id" || name == "name") && value.Length == 0) throw Located(name + " cannot be empty.", expression);
                return value;
            }

            private static object Publish(object value, ArtiSourceSpan span)
            {
                if (value == null || value is string || value is bool || value is DefReference || value is NativeTypeReference)
                {
                    return value;
                }
                if (value is int || value is long || value is double || value is float || value is decimal)
                {
                    return value;
                }
                if (value is byte || value is sbyte || value is short || value is ushort || value is uint || value is ulong)
                {
                    return value;
                }
                RuntimeObject runtime = value as RuntimeObject;
                if (runtime != null)
                {
                    Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (string key in runtime.Keys)
                    {
                        object member;
                        runtime.TryGet(key, out member);
                        copy[key] = Publish(member, span);
                    }
                    return copy;
                }
                OxygenStruct structure = value as OxygenStruct;
                if (structure != null)
                {
                    Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (OxygenField field in structure.Shape.Fields)
                        copy[field.Name] = Publish(structure.Fields[field.Slot], span);
                    return copy;
                }
                IList<object> collection = value as IList<object>;
                if (collection != null)
                {
                    List<object> copy = new List<object>(collection.Count);
                    foreach (object item in collection) copy.Add(Publish(item, span));
                    return copy;
                }

                IDictionary<string, object> dictionary = value as IDictionary<string, object>;
                if (dictionary != null)
                {
                    Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, object> entry in dictionary)
                    {
                        copy[entry.Key] = Publish(entry.Value, span);
                    }
                    return copy;
                }

                IList list = value as IList;
                if (list != null && !(value is string))
                {
                    List<object> copy = new List<object>(list.Count);
                    for (int index = 0; index < list.Count; index++)
                    {
                        copy.Add(Publish(list[index], span));
                    }
                    return copy;
                }

                throw new ContentRegistrationException("A content value must be a scalar, list, object, Def reference, or native type.", span.Line, span.Column);
            }

            private static IDictionary<string, object> AsObject(object value, string name, ArtiSourceSpan span)
            {
                if (value == null) return new Dictionary<string, object>(StringComparer.Ordinal);
                IDictionary<string, object> dictionary = value as IDictionary<string, object>;
                if (dictionary == null)
                {
                    throw new ContentRegistrationException(name + " must be an object.", span.Line, span.Column);
                }
                return dictionary;
            }

            private static IDictionary<string, object> AsPathMap(object value, string name, ArtiSourceSpan span)
            {
                IDictionary<string, object> source = AsObject(value, name, span);
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> entry in source)
                {
                    ValidatePath(entry.Key, span);
                    IList list = entry.Value as IList;
                    if (list == null || entry.Value is string)
                    {
                        throw new ContentRegistrationException(name + " path '" + entry.Key + "' must map to a list.", span.Line, span.Column);
                    }
                    result[entry.Key] = entry.Value;
                }
                return result;
            }

            private static IReadOnlyList<string> AsNameList(object value, string name, ArtiSourceSpan span)
            {
                if (value == null) return Array.Empty<string>();
                IList list = value as IList;
                if (list == null || value is string)
                {
                    throw new ContentRegistrationException(name + " must be a list of strings.", span.Line, span.Column);
                }
                string[] names = new string[list.Count];
                for (int index = 0; index < list.Count; index++)
                {
                    string text = list[index] as string;
                    if (string.IsNullOrEmpty(text))
                    {
                        throw new ContentRegistrationException(name + " entries must be non-empty strings.", span.Line, span.Column);
                    }
                    names[index] = text;
                }
                return names;
            }

            private static void RejectIdentity(IDictionary<string, object> fields, string name, ArtiSourceSpan span)
            {
                if (fields == null) return;
                foreach (string key in fields.Keys)
                {
                    string root = Root(key);
                    if (root == "defName" || root == "defNameHash" || root == "fileName" || root == "modContentPack")
                    {
                        throw new ContentRegistrationException(name + " cannot change identity field '" + key + "'.", span.Line, span.Column);
                    }
                }
            }

            private static string Root(string path)
            {
                if (string.IsNullOrEmpty(path)) return string.Empty;
                int dot = path.IndexOf('.');
                return dot < 0 ? path : path.Substring(0, dot);
            }

            private static void ValidatePath(string path, ArtiSourceSpan span)
            {
                if (string.IsNullOrEmpty(path) || path[0] == '.' || path[path.Length - 1] == '.')
                {
                    throw new ContentRegistrationException("Field path '" + (path ?? string.Empty) + "' is not valid.", span.Line, span.Column);
                }
                string[] parts = path.Split('.');
                for (int index = 0; index < parts.Length; index++)
                {
                    if (parts[index].Length == 0)
                    {
                        throw new ContentRegistrationException("Field path '" + path + "' is not valid.", span.Line, span.Column);
                    }
                    int number;
                    bool numeric = int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out number);
                    if (index == 0 && numeric)
                    {
                        throw new ContentRegistrationException("Field path '" + path + "' cannot start with an index.", span.Line, span.Column);
                    }
                    if (!numeric && !IsFieldName(parts[index]))
                    {
                        throw new ContentRegistrationException("Field path '" + path + "' is not valid.", span.Line, span.Column);
                    }
                }
            }

            private static bool IsFieldName(string value)
            {
                if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
                for (int index = 1; index < value.Length; index++)
                {
                    char item = value[index];
                    if (!(char.IsLetterOrDigit(item) || item == '_')) return false;
                }
                return true;
            }

            private static void MergeInto(IDictionary<string, object> destination, IDictionary<string, object> source)
            {
                if (source == null) return;
                foreach (KeyValuePair<string, object> entry in source)
                {
                    object existing;
                    IDictionary<string, object> left = destination.TryGetValue(entry.Key, out existing) ? existing as IDictionary<string, object> : null;
                    IDictionary<string, object> right = entry.Value as IDictionary<string, object>;
                    if (left != null && right != null)
                    {
                        IDictionary<string, object> merged = CopyObject(left);
                        MergeInto(merged, right);
                        destination[entry.Key] = merged;
                    }
                    else
                    {
                        destination[entry.Key] = DeepCopy(entry.Value);
                    }
                }
            }

            private static IDictionary<string, object> CopyObject(IDictionary<string, object> source)
            {
                IDictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                if (source == null) return copy;
                foreach (KeyValuePair<string, object> entry in source)
                {
                    copy[entry.Key] = DeepCopy(entry.Value);
                }
                return copy;
            }

            private static object DeepCopy(object value)
            {
                IDictionary<string, object> dictionary = value as IDictionary<string, object>;
                if (dictionary != null) return CopyObject(dictionary);
                IList list = value as IList;
                if (list != null && !(value is string))
                {
                    List<object> copy = new List<object>(list.Count);
                    for (int index = 0; index < list.Count; index++) copy.Add(DeepCopy(list[index]));
                    return copy;
                }
                DefReference reference = value as DefReference;
                if (reference != null)
                {
                    return new DefReference { Type = reference.Type, Id = reference.Id, PackageId = reference.PackageId };
                }
                NativeTypeReference native = value as NativeTypeReference;
                if (native != null) return new NativeTypeReference { Name = native.Name };
                return value;
            }

            private static void RequireArgument(ArtiArgument argument, ArtiCallExpression call)
            {
                if (argument == null || argument.Value == null)
                {
                    throw new ContentRegistrationException("A declaration argument is missing.", call.Span.Line, call.Span.Column);
                }
            }

            private static void RequireText(string value, string name, ArtiSourceSpan span)
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ContentRegistrationException("Argument '" + name + "' is required.", span.Line, span.Column);
                }
            }

            private static ContentRegistrationException Located(string message, ArtiExpression expression)
            {
                ArtiSourceSpan span = expression == null ? ArtiSourceSpan.Empty : expression.Span;
                return new ContentRegistrationException(message, span.Line, span.Column);
            }

            private static ArtiDiagnostic FirstError(IEnumerable<ArtiDiagnostic> diagnostics)
            {
                if (diagnostics != null)
                {
                    foreach (ArtiDiagnostic diagnostic in diagnostics)
                    {
                        if (diagnostic != null && diagnostic.Severity == ArtiDiagnosticSeverity.Error) return diagnostic;
                    }
                }
                return new ArtiDiagnostic(ArtiDiagnosticSeverity.Error, "ART4000", "Content preparation failed.", ArtiSourceSpan.Empty);
            }

            private sealed class Template
            {
                private readonly object _extends;
                private readonly IReadOnlyList<string> _mixins;
                private readonly IDictionary<string, object> _fields;
                private readonly ArtiSourceSpan _span;
                private Composition _expanded;
                private bool _expanding;

                public Template(string name, string type, IDictionary<string, object> fields, object extends, IReadOnlyList<string> mixins, ArtiSourceSpan span)
                {
                    Name = name;
                    Type = type;
                    _fields = fields;
                    _extends = extends;
                    _mixins = mixins;
                    _span = span;
                }

                public string Name { get; }
                public string Type { get; }

                public Composition Expand(CompiledContent owner, ArtiSourceSpan span)
                {
                    if (_expanded != null) return _expanded;
                    if (_expanding)
                    {
                        throw new ContentRegistrationException("Template '" + Name + "' is part of a composition cycle.", span.Line, span.Column);
                    }
                    _expanding = true;
                    try
                    {
                        _expanded = owner.Compose(Type, _fields, _extends, _mixins, _span);
                        return _expanded;
                    }
                    finally
                    {
                        _expanding = false;
                    }
                }
            }

            private sealed class Composition
            {
                public Composition(IDictionary<string, object> fields, DefReference baseDef)
                {
                    Fields = fields ?? new Dictionary<string, object>(StringComparer.Ordinal);
                    BaseDef = baseDef;
                }

                public IDictionary<string, object> Fields { get; }
                public DefReference BaseDef { get; }
            }

            private sealed class ThoughtEvaluator : IThoughtEvaluator
            {
                private readonly CompiledContent _owner;
                private readonly ArtiFunctionDeclarationStatement _function;

                public ThoughtEvaluator(CompiledContent owner, ArtiFunctionDeclarationStatement function)
                {
                    _owner = owner;
                    _function = function;
                }

                public int Evaluate(PawnContext pawn, GameContext game)
                {
                    object value = _owner.Invoke(_function, pawn, game);
                    int stage;
                    if (!TryStage(value, out stage))
                    {
                        throw new ScriptEvaluationException("evaluate must return an int stage.", _function.Span.Line, _function.Span.Column);
                    }
                    if (stage < -1)
                    {
                        throw new ScriptEvaluationException("evaluate cannot return a stage below -1.", _function.Span.Line, _function.Span.Column);
                    }
                    return stage;
                }

                private static bool TryStage(object value, out int stage)
                {
                    stage = 0;
                    if (value is int)
                    {
                        stage = (int)value;
                        return true;
                    }
                    if (value is long)
                    {
                        long number = (long)value;
                        if (number < int.MinValue || number > int.MaxValue) return false;
                        stage = (int)number;
                        return true;
                    }
                    if (value is double)
                    {
                        double number = (double)value;
                        if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue) return false;
                        stage = (int)number;
                        return true;
                    }
                    return false;
                }
            }

            private sealed class GateEvaluator : IGateEvaluator
            {
                private readonly CompiledContent _owner;
                private readonly ArtiFunctionDeclarationStatement _function;

                public GateEvaluator(CompiledContent owner, ArtiFunctionDeclarationStatement function)
                {
                    _owner = owner;
                    _function = function;
                }

                public bool? Evaluate(PawnContext pawn, GameContext game)
                {
                    object value = _owner.Invoke(_function, pawn, game);
                    if (value == null) return null;
                    if (value is bool) return (bool)value;
                    throw new ScriptEvaluationException("evaluate must return a bool or null.", _function.Span.Line, _function.Span.Column);
                }
            }
        }

        private sealed class ContentRegistrationException : Exception
        {
            public ContentRegistrationException(string message, int line, int column)
                : base(message)
            {
                Line = line;
                Column = column;
            }

            public int Line { get; }
            public int Column { get; }
        }
    }

    public partial class ArtiExecutor
    {
        private readonly List<ContentImport> _imports = new List<ContentImport>();

        internal IReadOnlyList<ContentImport> TakeImportFingerprints(IOxygenModuleCatalog modules)
        {
            return _imports.ToArray();
        }
    }

    internal static class ContentValueCallables
    {
        private static readonly RuntimeCallable DefRef = new RuntimeCallable(InvokeDefRef, "def_ref");
        private static readonly RuntimeCallable NativeType = new RuntimeCallable(InvokeNativeType, "native_type");

        public static object Get(string name)
        {
            if (name == "def_ref") return DefRef;
            if (name == "native_type") return NativeType;
            return null;
        }

        private static object InvokeDefRef(RuntimeArguments arguments)
        {
            if (arguments == null) throw new RuntimeFault("def_ref expects a type, an id, and an optional package.");
            string type = RequiredString(arguments, 0, "type");
            string id = RequiredString(arguments, 1, "id");
            string package = null;
            int namedPackage = 0;
            foreach (RuntimeArgument argument in arguments.Items)
            {
                if (argument.Name == null) continue;
                if (argument.Name != "package") throw new RuntimeFault("Unknown argument '" + argument.Name + "'.");
                namedPackage++;
                package = argument.Value as string;
                if (package == null) throw new RuntimeFault("def_ref package must be a string.");
            }
            if (namedPackage > 1) throw new RuntimeFault("The named argument 'package' was supplied more than once.");
            if (arguments.Positional.Count == 3)
            {
                if (namedPackage != 0) throw new RuntimeFault("def_ref package was supplied more than once.");
                package = arguments.Positional[2] as string;
                if (package == null) throw new RuntimeFault("def_ref package must be a string.");
            }
            else if (arguments.Positional.Count != 2)
            {
                throw new RuntimeFault("def_ref expects a type, an id, and an optional package.");
            }
            return new DefReference { Type = type, Id = id, PackageId = package };
        }

        private static string RequiredString(RuntimeArguments arguments, int position, string name)
        {
            if (arguments == null || arguments.Positional.Count <= position)
            {
                throw new RuntimeFault("def_ref expects a type, an id, and an optional package.");
            }
            string value = arguments.Positional[position] as string;
            if (string.IsNullOrEmpty(value))
            {
                throw new RuntimeFault("def_ref " + name + " must be a non-empty string.");
            }
            return value;
        }

        private static object InvokeNativeType(RuntimeArguments arguments)
        {
            if (arguments == null || arguments.Positional.Count != 1 || arguments.Items.Count != 1)
            {
                throw new RuntimeFault("native_type expects one type name.");
            }
            string name = arguments.Positional[0] as string;
            if (string.IsNullOrEmpty(name))
            {
                throw new RuntimeFault("native_type expects a non-empty type name.");
            }
            return new NativeTypeReference { Name = name };
        }

    }
}
