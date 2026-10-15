using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Comparisons accept nullable operands and always return a boolean: null
// equals null, an ordering comparison against null is false, and a null value
// is in a list exactly when the list holds a null. These are the C# semantics
// of == and < on nullable values. Strings order ordinally (by code point in
// SQL's "C" collation), uuids by their hexadecimal digits and false before
// true.
internal static class Comparison
{
    private static readonly MethodInfo CompareOrdinal = typeof(string).GetMethod(
        nameof(string.CompareOrdinal),
        [typeof(string), typeof(string)]
    )!;

    public static Expression Equal(object node, Scope scope)
    {
        return Compared(node, scope, Expression.Equal);
    }

    public static Expression NotEqual(object node, Scope scope)
    {
        return Compared(node, scope, Expression.NotEqual);
    }

    public static Expression GreaterThan(object node, Scope scope)
    {
        return Ordered(node, scope, ExpressionType.GreaterThan);
    }

    public static Expression GreaterThanOrEqual(object node, Scope scope)
    {
        return Ordered(node, scope, ExpressionType.GreaterThanOrEqual);
    }

    public static Expression LessThan(object node, Scope scope)
    {
        return Ordered(node, scope, ExpressionType.LessThan);
    }

    public static Expression LessThanOrEqual(object node, Scope scope)
    {
        return Ordered(node, scope, ExpressionType.LessThanOrEqual);
    }

    public static Expression In(object node, Scope scope)
    {
        Expression value = scope.Compile(ModelNode.Child(node, "Operand"));
        object list = ModelNode.Child(node, "List");
        Type type = Typed.Common(value.Type, Lists.Item(list));

        return Linq.Contains(Lists.Values(list, scope, type), Typed.As(value, type));
    }

    private static Expression Compared(
        object node,
        Scope scope,
        Func<Expression, Expression, BinaryExpression> comparison
    )
    {
        Expression left = scope.Compile(ModelNode.Child(node, "Left"));
        Expression right = scope.Compile(ModelNode.Child(node, "Right"));
        Type type = Typed.Common(left.Type, right.Type);

        return comparison(Typed.As(left, type), Typed.As(right, type));
    }

    private static Expression Ordered(object node, Scope scope, ExpressionType order)
    {
        Expression left = scope.Compile(ModelNode.Child(node, "Left"));
        Expression right = scope.Compile(ModelNode.Child(node, "Right"));
        Type type = Typed.Common(left.Type, right.Type);

        Expression l = Typed.As(left, type);
        Expression r = Typed.As(right, type);

        return type == typeof(string) ? Strings(l, r, order)
            : type == typeof(bool?) ? Booleans(l, r, order)
            : Expression.MakeBinary(order, l, r);
    }

    private static Expression Strings(
        Expression left,
        Expression right,
        ExpressionType order
    )
    {
        return Expression.AndAlso(
            Expression.AndAlso(
                Expression.Not(Typed.IsNull(left)),
                Expression.Not(Typed.IsNull(right))
            ),
            Expression.MakeBinary(
                order,
                Expression.Call(CompareOrdinal, left, right),
                Expression.Constant(0)
            )
        );
    }

    // false < true: left > right exactly when left is true and right false.
    private static Expression Booleans(
        Expression left,
        Expression right,
        ExpressionType order
    )
    {
        Expression IsTrue(Expression value)
        {
            return Expression.Equal(value, Expression.Constant(true, typeof(bool?)));
        }

        Expression IsFalse(Expression value)
        {
            return Expression.Equal(value, Expression.Constant(false, typeof(bool?)));
        }

        Expression both = Expression.AndAlso(
            Expression.Not(Typed.IsNull(left)),
            Expression.Not(Typed.IsNull(right))
        );

        return order == ExpressionType.GreaterThan
                ? Expression.AndAlso(IsTrue(left), IsFalse(right))
            : order == ExpressionType.LessThan
                ? Expression.AndAlso(IsFalse(left), IsTrue(right))
            : order == ExpressionType.GreaterThanOrEqual
                ? Expression.AndAlso(
                    both,
                    Expression.OrElse(IsTrue(left), IsFalse(right))
                )
            : Expression.AndAlso(both, Expression.OrElse(IsFalse(left), IsTrue(right)));
    }
}
