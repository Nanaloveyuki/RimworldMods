// Each specialization owns syntax identities and resolved-call caches; values and instances are not copied.
using System;

namespace Oxygendsl.Language
{
    public partial class ArtiExecutor
    {
        private static ArtiFunctionDeclarationStatement CloneFunction(ArtiFunctionDeclarationStatement function)
        {
            var clone = new ArtiFunctionDeclarationStatement(function.Span, function.Name, CloneBlock(function.Body))
            { ReturnType = CloneType(function.ReturnType), IsStatic = function.IsStatic, IsPublic = function.IsPublic };
            foreach (var parameter in function.Parameters) clone.Parameters.Add(new ArtiParameter(parameter.Span, parameter.Name, CloneType(parameter.Type)));
            // Type parameters are bound in the specialization scope, not redeclared as another generic.
            return clone;
        }

        private static ArtiTypeReference CloneType(ArtiTypeReference type)
        { return type == null ? null : new ArtiTypeReference(type.Span, type.Name, CloneType(type.ElementType), type.IsNullable); }

        private static ArtiBlockStatement CloneBlock(ArtiBlockStatement block)
        {
            if (block == null) return null;
            var clone = new ArtiBlockStatement(block.Span);
            foreach (var statement in block.Statements) clone.Statements.Add(CloneStatement(statement));
            return clone;
        }

        private static ArtiStatement CloneStatement(ArtiStatement statement)
        {
            if (statement is ArtiVariableDeclarationStatement variable)
                return new ArtiVariableDeclarationStatement(variable.Span, variable.Name, variable.IsConst, CloneExpression(variable.Value))
                { Type = CloneType(variable.Type), IsPublic = variable.IsPublic };
            if (statement is ArtiExpressionStatement expression) return new ArtiExpressionStatement(expression.Span, CloneExpression(expression.Expression));
            if (statement is ArtiAssignmentStatement assignment) return new ArtiAssignmentStatement(assignment.Span, CloneExpression(assignment.Target), assignment.Operator, CloneExpression(assignment.Value));
            if (statement is ArtiUnpackDeclarationStatement unpack) return new ArtiUnpackDeclarationStatement(unpack.Span, (ArtiArrayExpression)CloneExpression(unpack.Targets), CloneExpression(unpack.Value));
            if (statement is ArtiReturnStatement returned) return new ArtiReturnStatement(returned.Span, CloneExpression(returned.Value));
            if (statement is ArtiBlockStatement block) return CloneBlock(block);
            if (statement is ArtiIfStatement conditional)
            {
                var clone = new ArtiIfStatement(conditional.Span) { ElseBody = CloneBlock(conditional.ElseBody) };
                foreach (var branch in conditional.Branches) clone.Branches.Add(new ArtiIfBranch(branch.Span, CloneExpression(branch.Condition), CloneBlock(branch.Body)));
                return clone;
            }
            if (statement is ArtiForStatement loop) return new ArtiForStatement(loop.Span, loop.VariableName, CloneExpression(loop.Source), CloneBlock(loop.Body));
            if (statement is ArtiWhileStatement loopWhile) return new ArtiWhileStatement(loopWhile.Span, CloneExpression(loopWhile.Condition), CloneBlock(loopWhile.Body));
            if (statement is ArtiBreakStatement) return new ArtiBreakStatement(statement.Span);
            if (statement is ArtiContinueStatement) return new ArtiContinueStatement(statement.Span);
            if (statement is ArtiFunctionDeclarationStatement function)
            {
                var clone = CloneFunction(function);
                foreach (var type in function.TypeParameters) clone.TypeParameters.Add(new ArtiTypeParameter(type.Span, type.Name, CloneType(type.Constraint)));
                return clone;
            }
            throw new InvalidOperationException("Unsupported declaration in a generic function.");
        }

        private static ArtiExpression CloneExpression(ArtiExpression expression)
        {
            if (expression == null) return null;
            if (expression is ArtiLiteralExpression literal) return new ArtiLiteralExpression(literal.Span, literal.Value);
            if (expression is ArtiNameExpression name) return new ArtiNameExpression(name.Span, name.Name);
            if (expression is ArtiMemberExpression member) return new ArtiMemberExpression(member.Span, CloneExpression(member.Target), member.Member);
            if (expression is ArtiIndexExpression index) return new ArtiIndexExpression(index.Span, CloneExpression(index.Target), CloneExpression(index.Index));
            if (expression is ArtiCallExpression call)
            {
                var clone = new ArtiCallExpression(call.Span, CloneExpression(call.Target));
                foreach (var argument in call.Arguments) clone.Arguments.Add(new ArtiArgument(argument.Name, CloneExpression(argument.Value)));
                foreach (var type in call.TypeArguments) clone.TypeArguments.Add(CloneType(type));
                return clone;
            }
            if (expression is ArtiArrayExpression array)
            {
                var clone = new ArtiArrayExpression(array.Span);
                foreach (var item in array.Items) clone.Items.Add(CloneExpression(item));
                return clone;
            }
            if (expression is ArtiObjectExpression obj)
            {
                var clone = new ArtiObjectExpression(obj.Span);
                foreach (var item in obj.Members) clone.Members.Add(new ArtiObjectMember(item.Name, CloneExpression(item.Value)));
                return clone;
            }
            if (expression is ArtiInterpolatedStringExpression text)
            {
                var clone = new ArtiInterpolatedStringExpression(text.Span);
                foreach (var part in text.Parts) clone.Parts.Add(CloneExpression(part));
                return clone;
            }
            if (expression is ArtiUnaryExpression unary) return new ArtiUnaryExpression(unary.Span, unary.Operator, CloneExpression(unary.Operand));
            if (expression is ArtiBinaryExpression binary) return new ArtiBinaryExpression(binary.Span, CloneExpression(binary.Left), binary.Operator, CloneExpression(binary.Right));
            if (expression is ArtiErrorExpression) return new ArtiErrorExpression(expression.Span);
            throw new InvalidOperationException("Unsupported expression in a generic function.");
        }
    }
}
