using System.Linq.Expressions;
using System.Reflection;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Aggregates fold the rows of the current group, or every row left by where
// with over "all" (the only rows a plain or total query has), or the rows of
// a window: LINQ's aggregates over the group, the query's rows or the
// window's, which SQL runs as an aggregate, a scalar subquery or a window
// aggregate. A predicate keeps the rows it is true for, and null selector
// values are skipped. count, countDistinct and sum over no rows are 0,
// concatAll is the empty string, any is false and all is true; every other
// aggregate is null. first, last and concatAll sort the rows they see by
// their own orderBy. The statistics are functions of PureQLAggregates over
// the values, computed exactly and rounded once.
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

    private static readonly MethodInfo JoinMethod = Functions.Method(
        typeof(string),
        nameof(string.Join),
        typeof(string),
        typeof(IEnumerable<string>)
    );

    public static Expression Count(object node, Scope scope)
    {
        Expression rows = scope.Rows(Over(node));
        LambdaExpression? predicate = Predicate(node, scope);

        return predicate is null
            ? Linq.LongCount(rows)
            : Linq.LongCount(rows, predicate);
    }

    // The number of different non-null values, compared as equal compares.
    public static Expression CountDistinct(object node, Scope scope)
    {
        return Linq.LongCount(Linq.Distinct(Values(node, scope)));
    }

    public static Expression Sum(object node, Scope scope)
    {
        return Linq.Sum(
            Filtered(node, scope),
            Selector(node, scope, ValueTypes.Clr(((IOperator)node).Type))
        );
    }

    // Numbers average as divide does. A date averages its day numbers and
    // rounds to a whole day; a time or datetime averages its microseconds
    // the same way, the time since midnight without wrapping. A mean exactly
    // halfway is the later value: the numbers are never negative, so half
    // away from zero is half up.
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

    // The selector's value in the first of the rows sorted by the
    // aggregate's own orderBy, null or not.
    public static Expression First(object node, Scope scope)
    {
        return Linq.FirstOrDefault(Selected(node, scope, Sorted(node, scope, false)));
    }

    // The last row is the first in the opposite order.
    public static Expression Last(object node, Scope scope)
    {
        return Linq.FirstOrDefault(Selected(node, scope, Sorted(node, scope, true)));
    }

    // The non-null strings of the rows, in the aggregate's own order, joined
    // with the separator; with distinct each value once, at its first place.
    public static Expression ConcatAll(object node, Scope scope)
    {
        Expression values = NonNull(Selected(node, scope, Sorted(node, scope, false)));

        return Expression.Call(
            JoinMethod,
            scope.Compile(ModelNode.Child(node, "Separator")),
            (bool)ModelNode.Child(node, "Distinct") ? Linq.Distinct(values) : values
        );
    }

    public static Expression Median(object node, Scope scope)
    {
        return Statistic(node, scope, nameof(PureQLAggregates.Median));
    }

    public static Expression PercentileContinuous(object node, Scope scope)
    {
        return Statistic(
            node,
            scope,
            nameof(PureQLAggregates.PercentileContinuous),
            Fraction(node, scope)
        );
    }

    // One of the values, so of any type: the function is generic over it.
    public static Expression PercentileDiscrete(object node, Scope scope)
    {
        return Picked(
            node,
            scope,
            nameof(PureQLAggregates.PercentileDiscrete),
            Fraction(node, scope)
        );
    }

    public static Expression Mode(object node, Scope scope)
    {
        return Picked(node, scope, nameof(PureQLAggregates.Mode));
    }

    public static Expression VariancePopulation(object node, Scope scope)
    {
        return Statistic(node, scope, nameof(PureQLAggregates.VariancePopulation));
    }

    public static Expression VarianceSample(object node, Scope scope)
    {
        return Statistic(node, scope, nameof(PureQLAggregates.VarianceSample));
    }

    public static Expression StandardDeviationPopulation(object node, Scope scope)
    {
        return Statistic(
            node,
            scope,
            nameof(PureQLAggregates.StandardDeviationPopulation)
        );
    }

    public static Expression StandardDeviationSample(object node, Scope scope)
    {
        return Statistic(node, scope, nameof(PureQLAggregates.StandardDeviationSample));
    }

    public static Expression CovariancePopulation(object node, Scope scope)
    {
        return Paired(
            node,
            scope,
            nameof(PureQLAggregates.CovariancePopulation),
            "Left",
            "Right"
        );
    }

    public static Expression CovarianceSample(object node, Scope scope)
    {
        return Paired(
            node,
            scope,
            nameof(PureQLAggregates.CovarianceSample),
            "Left",
            "Right"
        );
    }

    public static Expression Correlation(object node, Scope scope)
    {
        return Paired(node, scope, nameof(PureQLAggregates.Correlation), "Left", "Right");
    }

    public static Expression RegressionSlope(object node, Scope scope)
    {
        return Paired(node, scope, nameof(PureQLAggregates.RegressionSlope), "Y", "X");
    }

    public static Expression RegressionIntercept(object node, Scope scope)
    {
        return Paired(node, scope, nameof(PureQLAggregates.RegressionIntercept), "Y", "X");
    }

    public static Expression RegressionRSquared(object node, Scope scope)
    {
        return Paired(node, scope, nameof(PureQLAggregates.RegressionRSquared), "Y", "X");
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
            ? byComparer(Linq.Select(rows, selector), Comparison.Ordinal)
            : byValue(rows, selector);
    }

    // A statistic of one column: a function over the selector's values, in
    // the type the selector is read in (a decimal for numbers).
    private static Expression Statistic(
        object node,
        Scope scope,
        string name,
        params Expression[] arguments
    )
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression values = Linq.Select(
            Filtered(node, scope),
            Selector(node, scope, type)
        );

        return Expression.Call(
            Functions.Method(
                typeof(PureQLAggregates),
                name,
                [
                    typeof(IEnumerable<>).MakeGenericType(type),
                    .. arguments.Select(argument => argument.Type),
                ]
            ),
            [values, .. arguments]
        );
    }

    // A statistic that picks one of the values.
    private static Expression Picked(
        object node,
        Scope scope,
        string name,
        params Expression[] arguments
    )
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression values = Linq.Select(
            Filtered(node, scope),
            Selector(node, scope, type)
        );

        return Expression.Call(
            typeof(PureQLAggregates)
                .GetMethods()
                .Single(method => method.Name == name)
                .MakeGenericMethod(type),
            [values, .. arguments]
        );
    }

    // A statistic of two columns: a function over the pairs of their values.
    private static Expression Paired(
        object node,
        Scope scope,
        string name,
        string first,
        string second
    )
    {
        ParameterExpression row = scope.RowParameter();
        Scope body = scope.Row(row);
        LambdaExpression pair = Expression.Lambda(
            Tuples.New(
                [
                    Arithmetic.Decimal(body.Compile(ModelNode.Child(node, first))),
                    Arithmetic.Decimal(body.Compile(ModelNode.Child(node, second))),
                ]
            ),
            row
        );

        return Expression.Call(
            Functions.Method(
                typeof(PureQLAggregates),
                name,
                typeof(IEnumerable<(decimal?, decimal?)>)
            ),
            Linq.Select(Filtered(node, scope), pair)
        );
    }

    private static Expression Fraction(object node, Scope scope)
    {
        return Typed.As(
            scope.Compile(ModelNode.Child(node, "Fraction")),
            typeof(decimal)
        );
    }

    private static AggregateOver Over(object node)
    {
        return ModelNode.Optional(node, "Over") is AggregateOver over
            ? over
            : AggregateOver.All;
    }

    private static Expression Filtered(object node, Scope scope)
    {
        Expression rows = scope.Rows(Over(node));
        LambdaExpression? predicate = Predicate(node, scope);

        return predicate is null ? rows : Linq.Where(rows, predicate);
    }

    // The rows the aggregate sees, sorted by its own orderBy, or the other
    // way round.
    private static Expression Sorted(object node, Scope scope, bool reversed)
    {
        ParameterExpression row = scope.RowParameter();
        Scope body = scope.Row(row);

        return Ordering.Sorted(
            Filtered(node, scope),
            row,
            ModelNode
                .Children(node, "OrderBy")
                .Select(item =>
                    (
                        ModelNode.Child(item, "Expression"),
                        (SortDirection)ModelNode.Child(item, "Direction")
                            == SortDirection.Desc != reversed
                    )
                ),
            body.Compile
        );
    }

    private static Expression Selected(object node, Scope scope, Expression rows)
    {
        return Linq.Select(
            rows,
            Selector(node, scope, ValueTypes.Clr(((IOperator)node).Type))
        );
    }

    // The selector's values that are not null.
    private static Expression Values(object node, Scope scope)
    {
        ParameterExpression row = scope.RowParameter();

        return NonNull(
            Linq.Select(
                Filtered(node, scope),
                Expression.Lambda(
                    scope.Row(row).Compile(ModelNode.Child(node, "Selector")),
                    row
                )
            )
        );
    }

    private static Expression NonNull(Expression values)
    {
        ParameterExpression value = Expression.Parameter(
            Linq.ElementType(values.Type),
            "value"
        );

        return Linq.Where(
            values,
            Expression.Lambda(Expression.Not(Typed.IsNull(value)), value)
        );
    }

    private static LambdaExpression? Predicate(object node, Scope scope)
    {
        if (ModelNode.Optional(node, "Predicate") is not object predicate)
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
