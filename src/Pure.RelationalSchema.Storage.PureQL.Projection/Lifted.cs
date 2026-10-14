using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Lifted operators: a null operand makes the result null. The tree checks
// the operands for null and applies the operation to their values; an
// operand that cannot be null is not checked.
internal static class Lifted
{
    public static Expression Unary(
        Expression operand,
        Type result,
        Func<Expression, Expression> operation
    )
    {
        return All([operand], result, values => operation(values[0]));
    }

    public static Expression Binary(
        Expression left,
        Expression right,
        Type result,
        Func<Expression, Expression, Expression> operation
    )
    {
        return All([left, right], result, values => operation(values[0], values[1]));
    }

    public static Expression All(
        IReadOnlyList<Expression> operands,
        Type result,
        Func<IReadOnlyList<Expression>, Expression> operation
    )
    {
        Expression value = Typed.As(
            operation([.. operands.Select(Typed.ValueOf)]),
            Typed.OrNull(result)
        );

        List<Expression> checks =
        [
            .. operands
                .Select(Typed.IsNull)
                .Where(check => check is not ConstantExpression),
        ];

        return checks.Count == 0
            ? value
            : Expression.Condition(
                checks.Aggregate(Expression.OrElse),
                Typed.Null(result),
                value
            );
    }
}
