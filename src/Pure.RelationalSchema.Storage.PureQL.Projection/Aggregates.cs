using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Aggregates fold the rows of the current group, or every row left by where
// with over "all" (the only rows a plain query has). A predicate keeps the
// rows it is true for, and null selector values are skipped. count and sum
// over no rows are 0, any is false and all is true; average, min and max are
// null.
internal static class Aggregates
{
    public static object? Count(object node, Scope scope)
    {
        return Fold(node, scope, rows => (long)Matching(node, rows).Count());
    }

    public static object? SumIntegers(object node, Scope scope)
    {
        return Fold(
            node,
            scope,
            rows => Selected(node, rows)
                .Aggregate(0L, (sum, value) => checked(sum + (long)value))
        );
    }

    public static object? SumDecimals(object node, Scope scope)
    {
        return Fold(
            node,
            scope,
            rows => Selected(node, rows)
                .Aggregate(0m, (sum, value) => sum + Values.Decimal(value))
        );
    }

    // Numbers average as divide does. A date averages its day numbers and
    // rounds half away from zero to a whole day; a time or datetime averages
    // its ticks, the time as seconds since midnight without wrapping.
    public static object? Average(object node, Scope scope)
    {
        return Fold(node, scope, rows => Mean([.. Selected(node, rows)]));
    }

    public static object? Min(object node, Scope scope)
    {
        return Fold(
            node,
            scope,
            rows => Selected(node, rows)
                .Aggregate(
                    (object?)null,
                    (min, value) =>
                        min is null || Values.Compare(value, min) < 0 ? value : min
                )
        );
    }

    public static object? Max(object node, Scope scope)
    {
        return Fold(
            node,
            scope,
            rows => Selected(node, rows)
                .Aggregate(
                    (object?)null,
                    (max, value) =>
                        max is null || Values.Compare(value, max) > 0 ? value : max
                )
        );
    }

    public static object? Any(object node, Scope scope)
    {
        return Fold(node, scope, rows => Matching(node, rows).Any());
    }

    public static object? All(object node, Scope scope)
    {
        return Fold(
            node,
            scope,
            rows => rows.All(row => row.Test(ModelNode.Child(node, "Predicate")))
        );
    }

    private static object? Fold(
        object node,
        Scope scope,
        Func<IEnumerable<Scope>, object?> fold
    )
    {
        AggregateOver over = ModelNode.Has(node, "Over")
            ? (AggregateOver)ModelNode.Child(node, "Over")
            : AggregateOver.Group;

        IReadOnlyList<JoinedRow> rows = scope.Rows(over);

        return scope.Aggregate(node, rows, () => fold(rows.Select(scope.Row)));
    }

    private static IEnumerable<Scope> Matching(object node, IEnumerable<Scope> rows)
    {
        object? predicate = ModelNode.Has(node, "Predicate")
            ? ModelNode.Property(node, "Predicate")
            : null;

        return predicate is null ? rows : rows.Where(row => row.Test(predicate));
    }

    private static IEnumerable<object> Selected(object node, IEnumerable<Scope> rows)
    {
        object selector = ModelNode.Child(node, "Selector");

        return Matching(node, rows)
            .Select(row => row.Evaluate(selector))
            .OfType<object>();
    }

    private static object? Mean(List<object> values)
    {
        return values.Count == 0
            ? null
            : values[0] switch
            {
                DateOnly => DateOnly.FromDayNumber(
                    (int)RoundedMean(values, value => ((DateOnly)value).DayNumber)
                ),
                TimeOnly => new TimeOnly(
                    (long)RoundedMean(values, value => ((TimeOnly)value).Ticks)
                ),
                DateTime => new DateTime(
                    (long)RoundedMean(values, value => ((DateTime)value).Ticks),
                    DateTimeKind.Utc
                ),
                _ => values.Sum(Values.Decimal) / values.Count,
            };
    }

    private static decimal RoundedMean(List<object> values, Func<object, decimal> number)
    {
        return decimal.Round(
            values.Sum(number) / values.Count,
            MidpointRounding.AwayFromZero
        );
    }
}
