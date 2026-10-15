// Derived from AdvancedRimTalk Source/Arti (MIT License).
// Copyright (c) 2026 Nanaloveyuki
// Adapted for Oxygendsl.Language; original prompt/RimTalk/Verse host interfaces are not retained.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oxygendsl.Language
{
    public enum ArtiDiagnosticLanguage
    {
        ChineseSimplified,
        English
    }

    public sealed class ArtiDiagnosticDefinition
    {
        public ArtiDiagnosticDefinition(int number, string messageZhCn, string messageEn)
        {
            Number = number;
            Code = "ART" + number.ToString("D4", CultureInfo.InvariantCulture);
            MessageZhCn = messageZhCn ?? string.Empty;
            MessageEn = messageEn ?? string.Empty;
        }

        public int Number { get; }
        public string Code { get; }
        public string MessageZhCn { get; }
        public string MessageEn { get; }
    }

    public static class ArtiDiagnosticCatalog
    {
        private static readonly IDictionary<int, ArtiDiagnosticDefinition> Definitions =
            new Dictionary<int, ArtiDiagnosticDefinition>
            {
                { 1000, Define(1000, "无法识别的字符 '{0}'。", "Unrecognized character '{0}'.") },
                { 1001, Define(1001, "块注释缺少结束标记 */。", "The block comment is missing its closing */.") },
                { 1002, Define(1002, "数字的指数部分缺少数字。", "The numeric exponent is missing digits.") },
                { 1003, Define(1003, "无法解析浮点数字。", "The floating-point number could not be parsed.") },
                { 1004, Define(1004, "整数超出支持范围。", "The integer is outside the supported range.") },
                { 1005, Define(1005, "不支持的字符串转义 \\{0}。", "Unsupported string escape \\{0}.") },
                { 1006, Define(1006, "字符串缺少结束引号。", "The string is missing its closing quote.") },
                { 1007, Define(1007, "Unicode 转义需要四位十六进制数字。", "A Unicode escape requires four hexadecimal digits.") },
                { 1008, Define(1008, "运算符必须写成完整形式。", "The operator must use its complete form.") },
                { 1009, Define(1009, "字符串插值缺少匹配的大括号；字面大括号请写成 {{ 或 }}。", "Interpolation braces must match; use {{ or }} for literal braces.") },

                { 2001, Define(2001, "语句之间需要换行或分号。", "Statements must be separated by a newline or semicolon.") },
                { 2004, Define(2004, "代码块需要使用大括号包围。", "A code block must be enclosed in braces.") },
                { 2005, Define(2005, "需要一个表达式。", "An expression is required.") },
                { 2006, Define(2006, "对象成员需要名称。", "An object member requires a name.") },
                { 2007, Define(2007, "需要一个有效的标识符。", "A valid identifier is required.") },
                { 2008, Define(2008, "代码块缺少结束大括号。", "The code block is missing its closing brace.") },
                { 2009, Define(2009, "变量声明需要 = 和初始值。", "A variable declaration requires = and an initializer.") },
                { 2010, Define(2010, "函数声明需要 fn。", "A function declaration requires fn.") },
                { 2011, Define(2011, "函数名称后需要参数括号。", "A function declaration needs a parameter list after its name.") },
                { 2012, Define(2012, "函数参数列表缺少结束括号。", "The function parameter list is missing its closing parenthesis.") },
                { 2013, Define(2013, "for 循环需要 in。", "A for loop requires in.") },
                { 2014, Define(2014, "索引表达式缺少结束方括号。", "The index expression is missing its closing bracket.") },
                { 2015, Define(2015, "函数调用缺少结束括号。", "The function call is missing its closing parenthesis.") },
                { 2016, Define(2016, "表达式缺少结束括号。", "The expression is missing its closing parenthesis.") },
                { 2017, Define(2017, "数组缺少结束方括号。", "The array is missing its closing bracket.") },
                { 2018, Define(2018, "对象成员名称后需要冒号。", "An object member name must be followed by a colon.") },
                { 2019, Define(2019, "对象缺少结束大括号。", "The object is missing its closing brace.") },
                { 2020, Define(2020, "Arti 代码块缺少结束标记 %}}。", "The Arti code block is missing its closing %}} marker.") },
                { 2021, Define(2021, "use 需要 module@provider 形式。", "use requires the form module@provider.") },
                { 2022, Define(2022, "类型标注不完整。", "The type annotation is incomplete.") },
                { 2023, Define(2023, "struct 声明缺少结束大括号。", "The struct declaration is missing its closing brace.") },
                { 2024, Define(2024, "struct 只能在文件顶层声明。", "struct can only be declared at file scope.") },
                { 2025, Define(2025, "embed 字段必须标注类型。", "An embedded field requires a type annotation.") },
                { 2026, Define(2026, "泛型调用只允许 list 和 array。", "Type arguments are only allowed on list and array.") },
                { 2027, Define(2027, "pub 只能修饰 fn、struct、const 或 trait。", "pub can only modify fn, struct, const, or trait.") },
                { 2028, Define(2028, "位置实参必须出现在命名实参之前。", "Positional arguments must precede named arguments.") },
                { 2029, Define(2029, "trait 声明缺少结束大括号。", "The trait declaration is missing its closing brace.") },
                { 2030, Define(2030, "impl 声明缺少结束大括号。", "The impl declaration is missing its closing brace.") },
                { 2031, Define(2031, "impl 声明不完整，需要目标类型。", "The impl declaration is incomplete and requires a target type.") },
                { 2032, Define(2032, "泛型类型参数列表不完整。", "The generic type parameter list is incomplete.") },
                { 2033, Define(2033, "泛型调用的类型实参列表不完整。", "The generic call type argument list is incomplete.") },
                { 2034, Define(2034, "static 只能用于类型方法。", "static is only allowed on type methods.") },
                { 2035, Define(2035, "pub 不能修饰 impl。", "pub cannot modify impl.") },
                { 2036, Define(2036, "trait 和 impl 只能在文件顶层声明。", "trait and impl can only be declared at file scope.") },

                { 3000, Define(3000, "不能分析空的 Arti 程序。", "A null Arti program cannot be analyzed.") },
                { 3001, Define(3001, "名称 '{0}' 在当前作用域中重复声明。", "The name '{0}' is declared more than once in the current scope.") },
                { 3003, Define(3003, "函数参数 '{0}' 重复声明。", "The function parameter '{0}' is declared more than once.") },
                { 3004, Define(3004, "return 只能出现在函数内部。", "return is only allowed inside a function.") },
                { 3005, Define(3005, "break 只能出现在循环内部。", "break is only allowed inside a loop.") },
                { 3006, Define(3006, "continue 只能出现在循环内部。", "continue is only allowed inside a loop.") },
                { 3007, Define(3007, "赋值目标 '{0}' 尚未声明。", "The assignment target '{0}' has not been declared.") },
                { 3008, Define(3008, "名称 '{0}' 不能被赋值。", "The name '{0}' cannot be assigned to.") },
                { 3009, Define(3009, "名称 '{0}' 未声明。", "The name '{0}' has not been declared.") },
                { 3010, Define(3010, "模块名称不能为空。", "A module name cannot be empty.") },
                { 3013, Define(3013, "名称 '{0}' 会覆盖内置名称或类型。", "The name '{0}' would shadow a builtin name or type.") },
                { 3014, Define(3014, "赋值目标必须是已声明的变量、成员或索引。", "An assignment target must be a declared variable, member, or index.") },
                { 3015, Define(3015, "模块别名 '{0}' 不是有效的标识符，请使用 as 指定别名。", "The module alias '{0}' is not a valid identifier; specify an alias with as.") },
                { 3016, Define(3016, "const 必须使用静态表达式；动态值请通过函数获取。", "const requires a static expression; use a function for dynamic values.") },
                { 3017, Define(3017, "use 只能出现在文件顶层，且必须位于其他声明之前。", "use is only allowed at file scope and must precede other declarations.") },
                { 3018, Define(3018, "类型 '{0}' 无法解析。", "The type '{0}' could not be resolved.") },
                { 3019, Define(3019, "void 只能用于函数返回类型。", "void can only be used as a function return type.") },
                { 3020, Define(3020, "返回值不符合返回类型 '{0}'。", "The returned value does not satisfy return type '{0}'.") },
                { 3021, Define(3021, "值不符合类型 '{0}'。", "The value does not satisfy type '{0}'.") },
                { 3022, Define(3022, "embed 类型 '{0}' 必须是非空 struct。", "Embedded type '{0}' must be a non-nullable struct.") },
                { 3023, Define(3023, "struct 成员 '{0}' 与已有成员冲突。", "Struct member '{0}' conflicts with an existing member.") },
                { 3024, Define(3024, "embed 类型图存在环：{0}。", "The embedded type graph contains a cycle: {0}.") },
                { 3025, Define(3025, "方法参数不能命名为 self。", "A method parameter cannot be named self.") },
                { 3026, Define(3026, "self 不能被重新绑定。", "self cannot be rebound.") },
                { 3027, Define(3027, "pub 只能用于脚本库模块顶层的 fn、struct、const 或 trait。", "pub is only allowed on top-level fn, struct, const, or trait in a library module.") },
                { 3028, Define(3028, "脚本库模块顶层只允许 use、fn、struct、trait、impl、let 和 const。", "A library module may only contain top-level use, fn, struct, trait, impl, let, and const.") },
                { 3029, Define(3029, "导入别名 '{0}' 与已有声明冲突。", "Import alias '{0}' conflicts with an existing declaration.") },
                { 3030, Define(3030, "泛型方法不受支持，类型参数只能用于函数。", "Generic methods are not supported; type parameters are only allowed on functions.") },
                { 3031, Define(3031, "trait 方法需要显式的参数类型和返回类型，不能有函数体或类型参数。", "A trait method requires explicit parameter and return types, and cannot have a body or type parameters.") },
                { 3032, Define(3032, "impl 不能使用 pub。", "impl cannot be public.") },
                { 3033, Define(3033, "static 只能用于类型上的方法，不能用于普通函数。", "static is only allowed on type methods, not ordinary functions.") },
                { 3034, Define(3034, "Self 只能在 struct 或 impl 内部使用。", "Self is only allowed inside a struct or impl.") },
                { 3035, Define(3035, "类型参数 '{0}' 是只读的，不能赋值。", "The type parameter '{0}' is read-only and cannot be assigned.") },
                { 3036, Define(3036, "泛型参数 '{0}' 需要恰好一个 trait 约束。", "Generic parameter '{0}' requires exactly one trait bound.") },
                { 3037, Define(3037, "成员 '{0}' 与已有成员冲突。", "Member '{0}' conflicts with an existing member.") },
                { 3038, Define(3038, "两个 trait 声明了相同的成员 '{0}'。", "Two traits declare the same member '{0}'.") },
                { 3039, Define(3039, "impl 的目标必须是本模块中定义的 struct。", "An impl target must be a struct defined in this module.") },
                { 3040, Define(3040, "impl 成员 '{0}' 与 trait 签名不匹配。", "Impl member '{0}' does not match the trait signature.") },
                { 3041, Define(3041, "trait impl 不能包含 trait 未声明的成员 '{0}'。", "A trait impl cannot contain member '{0}', which the trait does not declare.") },
                { 3042, Define(3042, "名称 '{0}' 与已有 trait 冲突。", "The name '{0}' conflicts with an existing trait.") },
                { 4000, Define(4000, "Arti 运行时错误：{0}", "Arti runtime error: {0}") },
            };

        public static IEnumerable<ArtiDiagnosticDefinition> All
        {
            get { return Definitions.Values; }
        }

        public static ArtiDiagnosticDefinition GetDefinition(int number)
        {
            ArtiDiagnosticDefinition definition;
            return Definitions.TryGetValue(number, out definition) ? definition : null;
        }

        public static ArtiDiagnostic Create(ArtiDiagnosticSeverity severity, int number, ArtiSourceSpan span, params object[] arguments)
        {
            ArtiDiagnosticDefinition definition = GetDefinition(number);
            string code = definition == null ? FormatCode(number) : definition.Code;
            string messageZhCn;
            string messageEn;
            if (definition == null)
            {
                messageZhCn = "未定义的诊断 " + code + "。";
                messageEn = "Undefined diagnostic " + code + ".";
            }
            else
            {
                messageZhCn = Format(definition.MessageZhCn, arguments);
                messageEn = Format(definition.MessageEn, arguments);
            }

            return new ArtiDiagnostic(severity, code, messageZhCn, messageEn, span);
        }

        private static ArtiDiagnosticDefinition Define(int number, string messageZhCn, string messageEn)
        {
            return new ArtiDiagnosticDefinition(number, messageZhCn, messageEn);
        }

        private static string Format(string format, object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return format;
            }

            return string.Format(CultureInfo.InvariantCulture, format, arguments);
        }

        private static string FormatCode(int number)
        {
            return "ART" + number.ToString("D4", CultureInfo.InvariantCulture);
        }
    }

    public abstract class ArtiDiagnosticReporter
    {
        protected readonly List<ArtiDiagnostic> ReportedDiagnostics = new List<ArtiDiagnostic>();

        protected void ClearDiagnostics()
        {
            ReportedDiagnostics.Clear();
        }

        protected List<ArtiDiagnostic> SnapshotDiagnostics()
        {
            return new List<ArtiDiagnostic>(ReportedDiagnostics);
        }

        protected void AddDiagnostic(ArtiDiagnostic diagnostic)
        {
            if (diagnostic != null)
            {
                ReportedDiagnostics.Add(diagnostic);
            }
        }

        protected void ReportError(int code, ArtiSourceSpan span, params object[] arguments)
        {
            AddDiagnostic(ArtiDiagnosticCatalog.Create(ArtiDiagnosticSeverity.Error, code, span, arguments));
        }

        protected void ReportError(int code, params object[] arguments)
        {
            ReportError(code, ArtiSourceSpan.Empty, arguments);
        }

        protected void ReportWarning(int code, ArtiSourceSpan span, params object[] arguments)
        {
            AddDiagnostic(ArtiDiagnosticCatalog.Create(ArtiDiagnosticSeverity.Warning, code, span, arguments));
        }

        protected void ReportWarning(int code, params object[] arguments)
        {
            ReportWarning(code, ArtiSourceSpan.Empty, arguments);
        }
    }
}
