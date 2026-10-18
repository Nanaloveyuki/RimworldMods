// Call, assignment, and checked conversion helpers for the Oxygen evaluator.
// Copyright (c) 2026 Nanaloveyuki
using System;
using System.Collections.Generic;
using System.Globalization;
using Oxygendsl.Core;
namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private static object UnwrapExport(object value, bool typePosition)
        {
            OxygenTypeExport exported = value as OxygenTypeExport;
            if (exported == null)
            {
                return value;
            }

            return typePosition ? (object)exported.Type : exported;
        }

        private static string LastPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int dot = path.LastIndexOf('.');
            return dot < 0 ? path : path.Substring(dot + 1);
        }

        private static string LocalScopeKey(RuntimeScope scope)
        {
            return scope == null ? "0" : scope.Id.ToString(CultureInfo.InvariantCulture);
        }

        private object CoerceReturn(OxygenType expected, object value, ArtiSourceSpan span, bool hasValue)
        {
            if (expected != null && expected.Kind == OxygenTypeKind.Void)
            {
                if (hasValue)
                {
                    RuntimeError(span, "void does not accept a value.");
                }

                return null;
            }

            return Coerce(expected, value, span, "return");
        }

        private void AssignMemberOrIndex(ArtiAssignmentStatement assignment, RuntimeScope scope)
        {
            ArtiMemberExpression member = assignment.Target as ArtiMemberExpression;
            ArtiIndexExpression index = assignment.Target as ArtiIndexExpression;
            if (member == null && index == null)
            {
                RuntimeError(assignment.Span, "Only a variable, member, or index can be assigned.");
            }

            object target = Evaluate(member == null ? index.Target : member.Target, scope);
            if (IsReadOnlyHost(target))
            {
                RuntimeError(assignment.Span, "Host values are read-only.");
            }

            bool compound = assignment.Operator != ArtiTokenKind.Equal;
            ArtiTokenKind op = compound ? CompoundOperator(assignment.Operator) : ArtiTokenKind.Equal;
            OxygenStruct instance = target as OxygenStruct;
            OxygenCollection collection = target as OxygenCollection;
            RuntimeObject runtimeObject = target as RuntimeObject;
            OxygenType expected = null;
            object indexValue = null;
            string key = null;
            if (index != null)
            {
                indexValue = Evaluate(index.Index, scope);
                if (collection != null) expected = collection.ElementType;
                else if (runtimeObject != null) key = indexValue as string;
            }
            else if (instance != null)
            {
                expected = DestinationFieldType(instance, member.Member);
            }

            object value = Evaluate(assignment.Value, scope, compound ? null : expected);
            if (member != null)
            {
                if (instance != null)
                {
                    if (!TryWriteStruct(instance, member.Member, value, assignment.Span, compound, op))
                        RuntimeError(member.Span, "The member '" + member.Member + "' is not available.");
                    return;
                }

                if (runtimeObject != null)
                {
                    WriteObject(runtimeObject, member.Member, value, compound, op, assignment.Span);
                    return;
                }

                RuntimeError(member.Span, "The member '" + member.Member + "' cannot be assigned.");
            }

            if (collection != null)
            {
                WriteCollection(collection, indexValue, value, assignment.Span, compound, op);
                return;
            }

            if (runtimeObject != null && key != null)
            {
                WriteObject(runtimeObject, key, value, compound, op, assignment.Span);
                return;
            }

            RuntimeError(index.Span, "The value cannot be indexed.");
        }

        private static bool IsReadOnlyHost(object target)
        {
            return target is PawnContext || target is GameContext || target is IOxygenHostValue && !(target is RuntimeObject);
        }

        private readonly HostArgumentList _hostPositional = new HostArgumentList();
        private readonly Dictionary<string, object> _hostNamed = new Dictionary<string, object>(StringComparer.Ordinal);

        private object InvokeHostCallable(IOxygenHostCallable callable, RuntimeArguments arguments)
        {
            _hostPositional.Clear();
            _hostNamed.Clear();
            foreach (RuntimeArgument argument in arguments.Items)
            {
                if (argument.Name == null)
                {
                    _hostPositional.Add(argument.Value);
                }
                else
                {
                    if (_hostNamed.ContainsKey(argument.Name))
                    {
                        throw new RuntimeFault("The named argument '" + argument.Name + "' was supplied more than once.");
                    }

                    _hostNamed.Add(argument.Name, argument.Value);
                }
            }

            return callable.Invoke(_hostPositional, _hostNamed);
        }

        private static OxygenType DestinationFieldType(OxygenStruct instance, string member)
        {
            if (instance == null) return null;
            int index = FieldIndex(instance.Shape, member);
            if (index >= 0) return instance.Shape.Fields[index].Type;
            OxygenPromotion promotion;
            if (!instance.Shape.Promoted.TryGetValue(member, out promotion) || promotion.Method) return null;
            OxygenStruct embedded = instance.Fields[promotion.Slot] as OxygenStruct;
            return embedded == null ? null : DestinationFieldType(embedded, promotion.Member);
        }
        private object EvaluateCall(ArtiCallExpression call, RuntimeScope scope)
        {
            ArtiMemberExpression memberTarget = call.Target as ArtiMemberExpression;
            object receiver = null;
            object target;
            OxygenMethod direct = null;
            if (memberTarget != null)
            {
                if (memberTarget.Member == "exists")
                {
                    if (!TryEvaluateExisting(memberTarget.Target, scope, out receiver)) receiver = MissingValue;
                }
                else receiver = Evaluate(memberTarget.Target, scope);
                object resolvedStatic;
                if (_directStatics.TryGetValue(memberTarget, out resolvedStatic)) target = resolvedStatic;
                else if (_directMethods.TryGetValue(memberTarget, out direct)) target = null;
                else if (!TryGetMember(receiver, memberTarget.Member, out target))
                {
                    RuntimeError(memberTarget.Span, "The member '" + memberTarget.Member + "' is not available.");
                }
            }
            else
            {
                target = UnwrapExport(Evaluate(call.Target, scope), false);
            }

            if (target is OxygenTypeExport constructorType) target = constructorType.Constructor;
            if (target is OxygenGenericFunction generic)
                target = SpecializeCall(generic, call, scope);
            else if (call.TypeArguments.Count != 0 && !(target is RuntimeCallable collectionCtor
                && (collectionCtor.Name == "list" || collectionCtor.Name == "array")))
                RuntimeError(call.Span, "Type arguments require a generic function or collection constructor.");

            RuntimeArguments arguments = new RuntimeArguments();
            IOxygenTypedCallable typed = target as IOxygenTypedCallable;
            int position = 0;
            foreach (ArtiArgument argument in call.Arguments)
            {
                object value;
                if (target is ExistsCallable)
                {
                    if (!TryEvaluateExisting(argument == null ? null : argument.Value, scope, out value)) value = MissingValue;
                }
                else
                {
                    OxygenType parameterType = direct != null && argument != null
                        ? ParameterType(direct.Function, argument.Name == null ? position : -1, argument.Name)
                        : typed == null || argument == null ? null : typed.GetParameterType(argument.Name == null ? position : -1, argument.Name);
                    value = Evaluate(argument == null ? null : argument.Value, scope, parameterType);
                }

                arguments.Add(new RuntimeArgument(argument == null ? null : argument.Name, value));
                if (argument == null || argument.Name == null) position++;
            }

            try
            {
                if (direct != null) return InvokeBound(direct, (OxygenStruct)receiver, arguments);
                RuntimeCallable runtime = target as RuntimeCallable;
                if (runtime != null && (runtime.Name == "list" || runtime.Name == "array"))
                {
                    if (call.TypeArguments.Count > 1) RuntimeError(call.Span, "Collection constructors accept one type argument.");
                    return InvokeCollectionConstructor(runtime.Name, call.TypeArguments.Count == 0 ? null : call.TypeArguments[0], arguments, scope, call.Span);
                }

                IOxygenHostCallable hostCallable = target as IOxygenHostCallable;
                if (hostCallable != null)
                {
                    return InvokeHostCallable(hostCallable, arguments);
                }

                return memberTarget == null ? InvokeCallable(target, arguments) : InvokeCallable(target, arguments, receiver);
            }
            catch (ExecutionStopException)
            {
                throw;
            }
            catch (RuntimeFault fault)
            {
                RuntimeError(call.Span, fault.Message);
                return null;
            }
            catch (Exception exception)
            {
                RuntimeError(call.Span, "Function call failed: " + exception.Message);
                return null;
            }
        }

        private object InvokeBuiltinConstructor(string name, ArtiTypeReference typeArgument, RuntimeArguments arguments, RuntimeScope scope, ArtiSourceSpan span)
        {
            if (name == "list" || name == "array")
            {
                return InvokeCollectionConstructor(name, typeArgument, arguments, scope, span);
            }

            if (arguments.Items.Count != 1 || arguments.Items[0].Name != null)
            {
                throw new RuntimeFault(name + " expects one positional value.");
            }

            return ConvertChecked(name, arguments.Items[0].Value, span);
        }
    }
}
