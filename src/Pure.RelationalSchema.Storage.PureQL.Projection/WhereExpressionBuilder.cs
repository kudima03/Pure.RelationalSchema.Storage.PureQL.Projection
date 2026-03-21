using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Returnings;
using ModelEquality = PureQL.CSharp.Model.Equality;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

/// <summary>
/// Translates a PureQL <see cref="BooleanReturning"/> AST node into a LINQ expression tree
/// suitable for use with <see cref="IQueryable{T}.Where"/>.
/// </summary>
internal static class WhereExpressionBuilder
{
    // *ArrayReturning field position is T1 for all types.
    // Scalar position differs: T0 for BooleanArrayReturning, T2 for all others.

    private static readonly MethodInfo GetTextValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetTextValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetDoubleValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetDoubleValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetBoolValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetBoolValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetDateOnlyValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetDateOnlyValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetDateTimeValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetDateTimeValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetTimeOnlyValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetTimeOnlyValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo GetGuidValueMethod =
        typeof(CellValueExtractor).GetMethod(
            nameof(CellValueExtractor.GetGuidValue),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    internal static Expression<Func<IRow, bool>> Build(BooleanReturning returning)
    {
        ParameterExpression rowParam = Expression.Parameter(typeof(IRow), "row");
        Expression body = BuildBoolean(returning, rowParam);
        return Expression.Lambda<Func<IRow, bool>>(body, rowParam);
    }

    private static Expression BuildBoolean(
        BooleanReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ =>
                throw new NotSupportedException(
                    "Parameter binding is not supported in expression tree translation."
                ),
            scalar => Expression.Constant(scalar.Value),
            equality => BuildEquality(equality, row),
            op => BuildBooleanOperator(op, row),
            comparison => BuildComparison(comparison, row)
        );
    }

    private static Expression BuildEquality(
        ModelEquality equality,
        ParameterExpression row
    )
    {
        return equality.Match(
            single => BuildSingleValueEquality(single, row),
            array => BuildArrayEquality(array, row)
        );
    }

    private static Expression BuildSingleValueEquality(
        SingleValueEquality equality,
        ParameterExpression row
    )
    {
        return equality.Match(
            eq =>
                Expression.Equal(
                    BuildBoolReturningAsExpr(eq.Left),
                    BuildBoolReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildDateReturningAsExpr(eq.Left),
                    BuildDateReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildDateTimeReturningAsExpr(eq.Left),
                    BuildDateTimeReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildNumberReturningAsExpr(eq.Left),
                    BuildNumberReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildStringReturningAsExpr(eq.Left),
                    BuildStringReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildTimeReturningAsExpr(eq.Left),
                    BuildTimeReturningAsExpr(eq.Right)
                ),
            eq =>
                Expression.Equal(
                    BuildUuidReturningAsExpr(eq.Left),
                    BuildUuidReturningAsExpr(eq.Right)
                )
        );
    }

    private static Expression BuildArrayEquality(
        ArrayEquality equality,
        ParameterExpression row
    )
    {
        return equality.Match(
            // BooleanArrayReturning: T0=BooleanArrayScalar, T1=BooleanField, T2=BooleanArrayParameter
            eq => BuildBoolArrayEquality(eq, row),
            // All others: T0=*ArrayParameter, T1=*Field, T2=*ArrayScalar
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<DateOnly>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<DateOnly>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetDateOnlyValueMethod
                ),
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<DateTime>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<DateTime>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetDateTimeValueMethod
                ),
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<double>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<double>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetDoubleValueMethod
                ),
            eq => BuildStringArrayEquality(eq, row),
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<TimeOnly>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<TimeOnly>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetTimeOnlyValueMethod
                ),
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<Guid>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<Guid>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetGuidValueMethod
                )
        );
    }

    // BooleanArrayReturning has T0=Scalar, T1=Field, T2=Parameter (reversed from other types)
    private static Expression BuildBoolArrayEquality(
        BooleanArrayEquality eq,
        ParameterExpression row
    )
    {
        return BuildContainmentEquality(
            left: eq.Left.IsT1,
            leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
            leftScalar: eq.Left.IsT0 ? (IEnumerable<bool>?)eq.Left.AsT0.Value : null,
            right: eq.Right.IsT1,
            rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
            rightScalar: eq.Right.IsT0 ? (IEnumerable<bool>?)eq.Right.AsT0.Value : null,
            row,
            GetBoolValueMethod
        );
    }

    private static Expression BuildStringArrayEquality(
        StringArrayEquality eq,
        ParameterExpression row
    )
    {
        return BuildContainmentEquality(
            left: eq.Left.IsT1,
            leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
            leftScalar: eq.Left.IsT2 ? (IEnumerable<string>?)eq.Left.AsT2.Value : null,
            right: eq.Right.IsT1,
            rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
            rightScalar: eq.Right.IsT2 ? (IEnumerable<string>?)eq.Right.AsT2.Value : null,
            row,
            GetTextValueMethod
        );
    }

    private static Expression BuildContainmentEquality<T>(
        bool left,
        string? leftField,
        IEnumerable<T>? leftScalar,
        bool right,
        string? rightField,
        IEnumerable<T>? rightScalar,
        ParameterExpression row,
        MethodInfo getCellValueMethod
    )
    {
        MethodInfo containsMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
                m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2
            )
            .MakeGenericMethod(typeof(T));

        // field vs scalar  →  scalar.Contains(cellValue)
        if (left && rightScalar is not null)
        {
            Expression fieldExpr = Expression.Call(
                getCellValueMethod,
                row,
                Expression.Constant(leftField)
            );
            Expression scalarExpr = Expression.Constant(
                rightScalar.ToArray(),
                typeof(T[])
            );
            return Expression.Call(containsMethod, scalarExpr, fieldExpr);
        }

        // scalar vs field  →  scalar.Contains(cellValue)
        if (right && leftScalar is not null)
        {
            Expression fieldExpr = Expression.Call(
                getCellValueMethod,
                row,
                Expression.Constant(rightField)
            );
            Expression scalarExpr = Expression.Constant(
                leftScalar.ToArray(),
                typeof(T[])
            );
            return Expression.Call(containsMethod, scalarExpr, fieldExpr);
        }

        // field vs field  →  cellValue1 == cellValue2
        if (left && right)
        {
            Expression left1 = Expression.Call(
                getCellValueMethod,
                row,
                Expression.Constant(leftField)
            );
            Expression right1 = Expression.Call(
                getCellValueMethod,
                row,
                Expression.Constant(rightField)
            );
            return Expression.Equal(left1, right1);
        }

        // scalar vs scalar  →  constant SequenceEqual
        if (leftScalar is not null && rightScalar is not null)
        {
            MethodInfo sequenceEqual = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m =>
                    m.Name == nameof(Enumerable.SequenceEqual)
                    && m.GetParameters().Length == 2
                )
                .MakeGenericMethod(typeof(T));
            Expression leftConst = Expression.Constant(leftScalar.ToArray(), typeof(T[]));
            Expression rightConst = Expression.Constant(
                rightScalar.ToArray(),
                typeof(T[])
            );
            return Expression.Call(sequenceEqual, leftConst, rightConst);
        }

        throw new NotSupportedException(
            "Parameter binding is not supported in expression tree translation."
        );
    }

    private static Expression BuildBooleanOperator(
        BooleanOperator op,
        ParameterExpression row
    )
    {
        return op.Match(
            and =>
                and.Conditions.Match(
                    conditions =>
                        conditions
                            .Select(c => BuildBoolean(c, row))
                            .Aggregate(Expression.AndAlso),
                    arrayReturning =>
                        BuildBoolArrayReturningAsSingleBool(arrayReturning, row)
                ),
            or =>
                or.Conditions.Match(
                    conditions =>
                        conditions
                            .Select(c => BuildBoolean(c, row))
                            .Aggregate(Expression.OrElse),
                    arrayReturning =>
                        BuildBoolArrayReturningAsSingleBool(arrayReturning, row)
                ),
            not => Expression.Not(BuildBoolean(not.Condition, row))
        );
    }

    // BooleanArrayReturning: T0=BooleanArrayScalar, T1=BooleanField, T2=BooleanArrayParameter
    private static Expression BuildBoolArrayReturningAsSingleBool(
        BooleanArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match<Expression>(
            scalar => Expression.Constant(scalar.Value.All(v => v)),
            field =>
                Expression.Equal(
                    Expression.Call(
                        GetBoolValueMethod,
                        row,
                        Expression.Constant(field.Field)
                    ),
                    Expression.Constant((bool?)true, typeof(bool?))
                ),
            _ =>
                throw new NotSupportedException(
                    "Parameter binding is not supported in expression tree translation."
                )
        );
    }

    private static Expression BuildComparison(
        Comparison comparison,
        ParameterExpression row
    )
    {
        return comparison.Match(
            c =>
                BuildTypedComparison(
                    BuildDateReturningAsExpr(c.Left),
                    BuildDateReturningAsExpr(c.Right),
                    c.Operator
                ),
            c =>
                BuildTypedComparison(
                    BuildDateTimeReturningAsExpr(c.Left),
                    BuildDateTimeReturningAsExpr(c.Right),
                    c.Operator
                ),
            c =>
                BuildTypedComparison(
                    BuildNumberReturningAsExpr(c.Left),
                    BuildNumberReturningAsExpr(c.Right),
                    c.Operator
                ),
            c =>
                BuildTypedComparison(
                    BuildStringReturningAsExpr(c.Left),
                    BuildStringReturningAsExpr(c.Right),
                    c.Operator
                ),
            c =>
                BuildTypedComparison(
                    BuildTimeReturningAsExpr(c.Left),
                    BuildTimeReturningAsExpr(c.Right),
                    c.Operator
                )
        );
    }

    private static readonly MethodInfo StringCompareOrdinalMethod =
        typeof(string).GetMethod(
            nameof(string.CompareOrdinal),
            [typeof(string), typeof(string)]
        )!;

    private static Expression BuildTypedComparison(
        Expression left,
        Expression right,
        ComparisonOperator op
    )
    {
        if (left.Type == typeof(string))
        {
            Expression compareExpr = Expression.Call(
                StringCompareOrdinalMethod,
                left,
                right
            );
            Expression zero = Expression.Constant(0);
            return op switch
            {
                ComparisonOperator.GreaterThan => Expression.GreaterThan(compareExpr, zero),
                ComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(
                    compareExpr,
                    zero
                ),
                ComparisonOperator.LessThan => Expression.LessThan(compareExpr, zero),
                ComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(
                    compareExpr,
                    zero
                ),
                _ => throw new NotSupportedException($"Unknown comparison operator: {op}"),
            };
        }

        return op switch
        {
            ComparisonOperator.GreaterThan => Expression.GreaterThan(left, right),
            ComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(
                left,
                right
            ),
            ComparisonOperator.LessThan => Expression.LessThan(left, right),
            ComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            _ => throw new NotSupportedException($"Unknown comparison operator: {op}"),
        };
    }

    // Scalar-only returnings (no field access, row-independent expressions)

    private static Expression BuildBoolReturningAsExpr(BooleanReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(bool)),
            _ =>
                throw new NotSupportedException(
                    "Nested equality in scalar boolean context is not supported."
                ),
            _ =>
                throw new NotSupportedException(
                    "Nested boolean operator in scalar boolean context is not supported."
                ),
            _ =>
                throw new NotSupportedException(
                    "Nested comparison in scalar boolean context is not supported."
                )
        );
    }

    private static Expression BuildNumberReturningAsExpr(NumberReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(double))
        );
    }

    private static Expression BuildStringReturningAsExpr(StringReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(string))
        );
    }

    private static Expression BuildDateReturningAsExpr(DateReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(DateOnly))
        );
    }

    private static Expression BuildDateTimeReturningAsExpr(DateTimeReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(DateTime))
        );
    }

    private static Expression BuildTimeReturningAsExpr(TimeReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(TimeOnly))
        );
    }

    private static Expression BuildUuidReturningAsExpr(UuidReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException("Parameter binding is not supported."),
            scalar => Expression.Constant(scalar.Value, typeof(Guid))
        );
    }
}
