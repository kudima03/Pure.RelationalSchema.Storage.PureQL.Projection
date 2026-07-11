using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Returnings;
using ModelEquality = PureQL.CSharp.Model.Equality;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Evaluates single-value expressions over a group of rows: aggregates fold the
// per-row values of their argument array, scalars are constants, and boolean
// composites (equality / comparison / and / or / not) recurse over both. Used
// for aggregate select projections and for HAVING.
internal static class AggregateEvaluator
{
    private const string ParameterNotSupported =
        "Parameter binding is not supported in group evaluation.";
    private const string ArithmeticNotSupported =
        "Single-value arithmetic is not supported in group evaluation.";
    private const string TemporalAverageNotSupported =
        "Average over date/time/datetime values is not supported "
        + "(undefined rounding semantics).";
    private const string PerRowConditionNotSupported =
        "Per-row (each*) conditions are not supported in group evaluation.";

    internal static bool IsAggregate(SingleValueReturning returning)
    {
        return returning.Match(
            _ => false,
            date => date.IsT2,
            dateTime => dateTime.IsT2,
            number => number.IsT3 || number.IsT4,
            text => text.IsT2,
            time => time.IsT2,
            _ => false
        );
    }

    internal static Func<IReadOnlyList<IRow>, string?> BuildText(
        SingleValueReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(
                "Boolean values have no aggregate to project."
            ),
            date => Text(BuildDate(date), ValueText.From),
            dateTime => Text(BuildDateTime(dateTime), ValueText.From),
            number => Text(BuildNumber(number), ValueText.From),
            BuildString,
            time => Text(BuildTime(time), ValueText.From),
            _ => throw new NotSupportedException(
                "Uuid values have no aggregate to project."
            )
        );
    }

    internal static Func<IReadOnlyList<IRow>, bool> BuildCondition(
        BooleanReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            BuildEquality,
            BuildBooleanOperator,
            BuildComparison
        );
    }

    private static Func<IReadOnlyList<IRow>, string?> Text<T>(
        Func<IReadOnlyList<IRow>, T?> value,
        Func<T, string> format
    )
        where T : struct
    {
        return rows => value(rows) is T folded ? format(folded) : null;
    }

    private static Func<IReadOnlyList<IRow>, double?> BuildNumber(
        NumberReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            _ => throw new NotSupportedException(ArithmeticNotSupported),
            BuildNumberAggregate,
            BuildCount
        );
    }

    private static Func<IReadOnlyList<IRow>, double?> BuildNumberAggregate(
        NumberAggregate aggregate
    )
    {
        return aggregate.Match(
            average => Fold(
                WhereExpressionBuilder.BuildNumberSelector(average.Argument),
                values => values.Average()
            ),
            max => Fold(
                WhereExpressionBuilder.BuildNumberSelector(max.Argument),
                values => values.Max()
            ),
            min => Fold(
                WhereExpressionBuilder.BuildNumberSelector(min.Argument),
                values => values.Min()
            ),
            sum => Fold(
                WhereExpressionBuilder.BuildNumberSelector(sum.Argument),
                values => values.Sum()
            )
        );
    }

    private static Func<IReadOnlyList<IRow>, double?> BuildCount(Count count)
    {
        Func<IRow, bool> hasValue = HasValueSelector(count.Argument);
        return rows => rows.Count(hasValue);
    }

    private static Func<IRow, bool> HasValueSelector(ArrayReturning argument)
    {
        return argument.Match(
            boolean => HasValue(WhereExpressionBuilder.BuildBoolSelector(boolean)),
            date => HasValue(WhereExpressionBuilder.BuildDateSelector(date)),
            dateTime => HasValue(
                WhereExpressionBuilder.BuildDateTimeSelector(dateTime)
            ),
            number => HasValue(WhereExpressionBuilder.BuildNumberSelector(number)),
            text =>
            {
                Func<IRow, string?> selector =
                    WhereExpressionBuilder.BuildStringSelector(text);
                return row => selector(row) is not null;
            },
            time => HasValue(WhereExpressionBuilder.BuildTimeSelector(time)),
            uuid => HasValue(WhereExpressionBuilder.BuildUuidSelector(uuid))
        );
    }

    private static Func<IRow, bool> HasValue<T>(Func<IRow, T?> selector)
        where T : struct
    {
        return row => selector(row).HasValue;
    }

    private static Func<IReadOnlyList<IRow>, string?> BuildString(
        StringReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            aggregate => aggregate.Match(
                max => FoldString(
                    max.Argument,
                    values => values.Max(StringComparer.Ordinal)
                ),
                min => FoldString(
                    min.Argument,
                    values => values.Min(StringComparer.Ordinal)
                )
            )
        );
    }

    private static Func<IReadOnlyList<IRow>, DateOnly?> BuildDate(
        DateReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            aggregate => aggregate.Match(
                max => Fold(
                    WhereExpressionBuilder.BuildDateSelector(max.Argument),
                    values => values.Max()
                ),
                min => Fold(
                    WhereExpressionBuilder.BuildDateSelector(min.Argument),
                    values => values.Min()
                ),
                _ => throw new NotSupportedException(TemporalAverageNotSupported)
            )
        );
    }

    private static Func<IReadOnlyList<IRow>, DateTime?> BuildDateTime(
        DateTimeReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            aggregate => aggregate.Match(
                max => Fold(
                    WhereExpressionBuilder.BuildDateTimeSelector(max.Argument),
                    values => values.Max()
                ),
                min => Fold(
                    WhereExpressionBuilder.BuildDateTimeSelector(min.Argument),
                    values => values.Min()
                ),
                _ => throw new NotSupportedException(TemporalAverageNotSupported)
            )
        );
    }

    private static Func<IReadOnlyList<IRow>, TimeOnly?> BuildTime(
        TimeReturning returning
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value,
            aggregate => aggregate.Match(
                max => Fold(
                    WhereExpressionBuilder.BuildTimeSelector(max.Argument),
                    values => values.Max()
                ),
                min => Fold(
                    WhereExpressionBuilder.BuildTimeSelector(min.Argument),
                    values => values.Min()
                ),
                _ => throw new NotSupportedException(TemporalAverageNotSupported)
            )
        );
    }

    private static Func<IReadOnlyList<IRow>, Guid?> BuildUuid(
        UuidReturning returning
    )
    {
        return returning.Match<Func<IReadOnlyList<IRow>, Guid?>>(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => _ => scalar.Value
        );
    }

    private static Func<IReadOnlyList<IRow>, T?> Fold<T>(
        Func<IRow, T?> selector,
        Func<IReadOnlyList<T>, T> fold
    )
        where T : struct
    {
        return rows =>
        {
            List<T> values = [.. rows.Select(selector).OfType<T>()];
            return values.Count == 0 ? null : fold(values);
        };
    }

    private static Func<IReadOnlyList<IRow>, string?> FoldString(
        StringArrayReturning argument,
        Func<IReadOnlyList<string>, string?> fold
    )
    {
        Func<IRow, string?> selector =
            WhereExpressionBuilder.BuildStringSelector(argument);
        return rows =>
        {
            List<string> values = [.. rows.Select(selector).OfType<string>()];
            return values.Count == 0 ? null : fold(values);
        };
    }

    private static Func<IReadOnlyList<IRow>, bool> BuildEquality(
        ModelEquality equality
    )
    {
        return equality.Match(
            BuildSingleValueEquality,
            _ => throw new NotSupportedException(PerRowConditionNotSupported)
        );
    }

    private static Func<IReadOnlyList<IRow>, bool> BuildSingleValueEquality(
        SingleValueEquality equality
    )
    {
        return equality.Match(
            eq => BooleanEqualityOf(
                BuildCondition(eq.Left),
                BuildCondition(eq.Right)
            ),
            eq => EqualityOf(BuildDate(eq.Left), BuildDate(eq.Right)),
            eq => EqualityOf(BuildDateTime(eq.Left), BuildDateTime(eq.Right)),
            eq => EqualityOf(BuildNumber(eq.Left), BuildNumber(eq.Right)),
            eq => StringEqualityOf(BuildString(eq.Left), BuildString(eq.Right)),
            eq => EqualityOf(BuildTime(eq.Left), BuildTime(eq.Right)),
            eq => EqualityOf(BuildUuid(eq.Left), BuildUuid(eq.Right))
        );
    }

    private static Func<IReadOnlyList<IRow>, bool> BooleanEqualityOf(
        Func<IReadOnlyList<IRow>, bool> left,
        Func<IReadOnlyList<IRow>, bool> right
    )
    {
        return rows => left(rows) == right(rows);
    }

    private static Func<IReadOnlyList<IRow>, bool> EqualityOf<T>(
        Func<IReadOnlyList<IRow>, T?> left,
        Func<IReadOnlyList<IRow>, T?> right
    )
        where T : struct
    {
        return rows =>
            left(rows) is T leftValue
            && right(rows) is T rightValue
            && leftValue.Equals(rightValue);
    }

    private static Func<IReadOnlyList<IRow>, bool> StringEqualityOf(
        Func<IReadOnlyList<IRow>, string?> left,
        Func<IReadOnlyList<IRow>, string?> right
    )
    {
        return rows =>
            left(rows) is string leftValue
            && right(rows) is string rightValue
            && string.Equals(leftValue, rightValue, System.StringComparison.Ordinal);
    }

    private static Func<IReadOnlyList<IRow>, bool> BuildBooleanOperator(
        BooleanOperator booleanOperator
    )
    {
        return booleanOperator.Match(
            and => and.Conditions.Match(
                conditions =>
                {
                    List<Func<IReadOnlyList<IRow>, bool>> compiled =
                        [.. conditions.Select(BuildCondition)];
                    return new Func<IReadOnlyList<IRow>, bool>(rows =>
                        compiled.All(condition => condition(rows))
                    );
                },
                _ => throw new NotSupportedException(PerRowConditionNotSupported)
            ),
            or => or.Conditions.Match(
                conditions =>
                {
                    List<Func<IReadOnlyList<IRow>, bool>> compiled =
                        [.. conditions.Select(BuildCondition)];
                    return new Func<IReadOnlyList<IRow>, bool>(rows =>
                        compiled.Any(condition => condition(rows))
                    );
                },
                _ => throw new NotSupportedException(PerRowConditionNotSupported)
            ),
            not =>
            {
                Func<IReadOnlyList<IRow>, bool> inner =
                    BuildCondition(not.Condition);
                return rows => !inner(rows);
            }
        );
    }

    private static Func<IReadOnlyList<IRow>, bool> BuildComparison(
        Comparison comparison
    )
    {
        return comparison.Match(
            c => ComparisonOf(BuildDate(c.Left), BuildDate(c.Right), c.Operator),
            c => ComparisonOf(
                BuildDateTime(c.Left),
                BuildDateTime(c.Right),
                c.Operator
            ),
            c => ComparisonOf(BuildNumber(c.Left), BuildNumber(c.Right), c.Operator),
            c => StringComparisonOf(
                BuildString(c.Left),
                BuildString(c.Right),
                c.Operator
            ),
            c => ComparisonOf(BuildTime(c.Left), BuildTime(c.Right), c.Operator)
        );
    }

    private static Func<IReadOnlyList<IRow>, bool> ComparisonOf<T>(
        Func<IReadOnlyList<IRow>, T?> left,
        Func<IReadOnlyList<IRow>, T?> right,
        ComparisonOperator comparisonOperator
    )
        where T : struct, IComparable<T>
    {
        return rows =>
            left(rows) is T leftValue
            && right(rows) is T rightValue
            && Satisfies(leftValue.CompareTo(rightValue), comparisonOperator);
    }

    private static Func<IReadOnlyList<IRow>, bool> StringComparisonOf(
        Func<IReadOnlyList<IRow>, string?> left,
        Func<IReadOnlyList<IRow>, string?> right,
        ComparisonOperator comparisonOperator
    )
    {
        return rows =>
            left(rows) is string leftValue
            && right(rows) is string rightValue
            && Satisfies(
                string.CompareOrdinal(leftValue, rightValue),
                comparisonOperator
            );
    }

    private static bool Satisfies(
        int comparison,
        ComparisonOperator comparisonOperator
    )
    {
        return comparisonOperator switch
        {
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new NotSupportedException(
                $"Unknown comparison operator: {comparisonOperator}."
            ),
        };
    }
}
