using System.Linq.Expressions;
using System.Reflection;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Aggregates fold the rows of the current group, or every row left by where
// with over "all" (the only rows a plain or total query has): LINQ's
// aggregates over the group or over the query's rows, which SQL runs as an
// aggregate or a scalar subquery. A predicate keeps the rows it is true for,
// and null selector values are skipped. count and sum over no rows are 0, any
// is false and all is true; average, min and max are null.
internal static class Aggregates
{
    private static readonly MethodInfo FromDayNumberMethod = typeof(DateOnly).GetMethod(
        nameof(DateOnly.FromDayNumber),
        [typeof(int)]
    )!;

    private static readonly ConstructorInfo TimeConstructor =
        typeof(TimeOnly).GetConstructor([typeof(long)])!;

    private static readonly ConstructorInfo DateTimeConstructor =
        typeof(DateTime).GetConstructor([typeof(long), typeof(DateTimeKind)])!;

    public static Expression Count(object node, Scope scope)
    {
        Expression rows = scope.Rows(Over(node));
        LambdaExpression? predicate = Predicate(node, scope);

        return predicate is null
            ? Linq.LongCount(rows)
            : Linq.LongCount(rows, predicate);
    }

    public static Expression SumIntegers(object node, Scope scope)
    {
        return Linq.Sum(Filtered(node, scope), Selector(node, scope, typeof(long?)));
    }

    public static Expression SumDecimals(object node, Scope scope)
    {
        return Linq.Sum(Filtered(node, scope), Selector(node, scope, typeof(decimal?)));
    }

    // Numbers average as divide does. A date averages its day numbers and
    // rounds half away from zero to a whole day; a time or datetime averages
    // its microseconds the same way, the time since midnight without wrapping.
    public static Expression Average(object node, Scope scope)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression rows = Filtered(node, scope);

        if (type == typeof(decimal?))
        {
            return Linq.Average(rows, Selector(node, scope, typeof(decimal?)));
        }

        Func<Expression, Expression> number = type == typeof(DateOnly?)
            ? date => Expression.Convert(
                Expression.Property(date, nameof(DateOnly.DayNumber)),
                typeof(decimal)
            )
            : value => Temporal.Microseconds(Expression.Property(value, "Ticks"));

        Func<Expression, Expression> value = type == typeof(DateOnly?)
            ? mean => Expression.Call(
                FromDayNumberMethod,
                Expression.Convert(Arithmetic.AwayFromZero(mean), typeof(int))
            )
            : type == typeof(TimeOnly?)
                ? mean => Expression.New(TimeConstructor, Temporal.MicrosecondTicks(mean))
            : mean => Expression.New(
                DateTimeConstructor,
                Temporal.MicrosecondTicks(mean),
                Expression.Constant(DateTimeKind.Utc)
            );

        LambdaExpression selector = Selector(node, scope, type);
        LambdaExpression numbers = Expression.Lambda(
            Lifted.Unary(selector.Body, typeof(decimal?), number),
            selector.Parameters
        );

        return Lifted.Unary(Linq.Average(rows, numbers), type, value);
    }

    public static Expression Min(object node, Scope scope)
    {
        return Extreme(node, scope, Linq.Min, Linq.Min);
    }

    public static Expression Max(object node, Scope scope)
    {
        return Extreme(node, scope, Linq.Max, Linq.Max);
    }

    public static Expression Any(object node, Scope scope)
    {
        return Linq.Any(scope.Rows(Over(node)), Predicate(node, scope)!);
    }

    public static Expression All(object node, Scope scope)
    {
        return Linq.All(scope.Rows(Over(node)), Predicate(node, scope)!);
    }

    // Strings order ordinally, like every string comparison of the tree.
    private static Expression Extreme(
        object node,
        Scope scope,
        Func<Expression, LambdaExpression, Expression> byValue,
        Func<Expression, Expression, Expression> byComparer
    )
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression rows = Filtered(node, scope);
        LambdaExpression selector = Selector(node, scope, type);

        return type == typeof(string)
            ? byComparer(
                Linq.Select(rows, selector),
                Expression.Constant(StringComparer.Ordinal, typeof(IComparer<string>))
            )
            : byValue(rows, selector);
    }

    private static AggregateOver Over(object node)
    {
        return ModelNode.Has(node, "Over")
            ? (AggregateOver)ModelNode.Child(node, "Over")
            : AggregateOver.All;
    }

    private static Expression Filtered(object node, Scope scope)
    {
        Expression rows = scope.Rows(Over(node));
        LambdaExpression? predicate = Predicate(node, scope);

        return predicate is null ? rows : Linq.Where(rows, predicate);
    }

    private static LambdaExpression? Predicate(object node, Scope scope)
    {
        object? predicate = ModelNode.Has(node, "Predicate")
            ? ModelNode.Property(node, "Predicate")
            : null;

        if (predicate is null)
        {
            return null;
        }

        ParameterExpression row = scope.RowParameter();
        return Expression.Lambda(scope.Row(row).Test(predicate), row);
    }

    private static LambdaExpression Selector(object node, Scope scope, Type type)
    {
        ParameterExpression row = scope.RowParameter();

        return Expression.Lambda(
            Typed.As(scope.Row(row).Compile(ModelNode.Child(node, "Selector")), type),
            row
        );
    }
}
