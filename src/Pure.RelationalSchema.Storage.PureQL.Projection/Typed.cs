using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Typing helpers for the tree's values. An integer stands wherever a decimal
// is expected and a non-null value wherever a nullable one is, so an operand
// is converted to the type its operator works in; a condition holds where its
// value is true, and null is not true.
internal static class Typed
{
    public static Expression As(Expression value, Type type)
    {
        if (value.Type == type)
        {
            return value;
        }

        Type? underlying = Nullable.GetUnderlyingType(type);

        return underlying is not null
            && value.Type.IsValueType
            && Nullable.GetUnderlyingType(value.Type) is null
            && value.Type != underlying
            ? Expression.Convert(Expression.Convert(value, underlying), type)
            : Expression.Convert(value, type);
    }

    public static Type OrNull(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null
            ? typeof(Nullable<>).MakeGenericType(type)
            : type;
    }

    // The type two operands compare or combine in: decimal when one is an
    // integer and the other a decimal, their common nullable type otherwise.
    public static Type Common(Type left, Type right)
    {
        Type first = OrNull(left);
        Type second = OrNull(right);

        return first == second ? first
            : (first == typeof(long?) && second == typeof(decimal?))
                || (first == typeof(decimal?) && second == typeof(long?))
                ? typeof(decimal?)
            : throw new InvalidOperationException(
                $"Values of {left.Name} and {right.Name} cannot be compared."
            );
    }

    public static Expression Null(Type type)
    {
        return Expression.Constant(null, OrNull(type));
    }

    public static Expression IsNull(Expression value)
    {
        return value.Type.IsValueType && Nullable.GetUnderlyingType(value.Type) is null
            ? Expression.Constant(false)
            : Expression.Equal(value, Expression.Constant(null, value.Type));
    }

    // The value of a nullable value, which the caller has checked is not null.
    public static Expression ValueOf(Expression value)
    {
        return Nullable.GetUnderlyingType(value.Type) is not null
            ? Expression.Property(value, "Value")
            : value;
    }

    public static Expression Test(Expression condition)
    {
        return condition.Type == typeof(bool)
            ? condition
            : Expression.Equal(
                As(condition, typeof(bool?)),
                Expression.Constant(true, typeof(bool?))
            );
    }

    public static Expression All(IEnumerable<Expression> conditions)
    {
        return conditions.DefaultIfEmpty(Expression.Constant(true)).Aggregate(
            Expression.AndAlso
        );
    }
}
