using System.Linq.Expressions;
using System.Reflection;
using OneOf;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachDateTimeArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Returnings;
using ModelEquality = PureQL.CSharp.Model.Equality;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class WhereExpressionBuilder
{
    private const string ParameterNotSupported =
        "Parameter binding is not supported in expression tree translation.";
    private const string AggregateNotSupported =
        "Aggregate expressions are not supported outside groupBy projection.";
    private const string WholeArrayFieldEqualityNotSupported =
        "Whole-array equal of a field against a literal array requires "
        + "order-sensitive sequence comparison over the full row set, which is "
        + "not implemented (see issue #114); per-row containment is not "
        + "equivalent and would silently produce wrong results.";

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

    internal static Expression<Func<IRow, bool>> BuildPredicate(
        OneOf<BooleanReturning, BooleanArrayReturning> condition
    )
    {
        ParameterExpression rowParam = Expression.Parameter(typeof(IRow), "row");
        Expression body = condition.Match(
            single => BuildBoolean(single, rowParam),
            each => BuildBoolArrayPerRow(each, rowParam)
        );
        return Expression.Lambda<Func<IRow, bool>>(body, rowParam);
    }

    internal static Func<IRow, bool?> BuildBoolSelector(
        BooleanArrayReturning returning
    )
    {
        return CompileSelector<bool?>(row => BuildBoolArrayValuePerRow(returning, row));
    }

    internal static Func<IRow, double?> BuildNumberSelector(
        NumberArrayReturning returning
    )
    {
        return CompileSelector<double?>(row =>
            BuildNumberArrayValuePerRow(returning, row)
        );
    }

    internal static Func<IRow, string?> BuildStringSelector(
        StringArrayReturning returning
    )
    {
        return CompileSelector<string?>(row =>
            BuildStringArrayValuePerRow(returning, row)
        );
    }

    internal static Func<IRow, DateOnly?> BuildDateSelector(
        DateArrayReturning returning
    )
    {
        return CompileSelector<DateOnly?>(row =>
            BuildDateArrayValuePerRow(returning, row)
        );
    }

    internal static Func<IRow, TimeOnly?> BuildTimeSelector(
        TimeArrayReturning returning
    )
    {
        return CompileSelector<TimeOnly?>(row =>
            BuildTimeArrayValuePerRow(returning, row)
        );
    }

    internal static Func<IRow, DateTime?> BuildDateTimeSelector(
        DateTimeArrayReturning returning
    )
    {
        return CompileSelector<DateTime?>(row =>
            BuildDateTimeArrayValuePerRow(returning, row)
        );
    }

    internal static Func<IRow, Guid?> BuildUuidSelector(
        UuidArrayReturning returning
    )
    {
        return CompileSelector<Guid?>(row =>
            BuildUuidArrayValuePerRow(returning, row)
        );
    }

    private static Func<IRow, T> CompileSelector<T>(
        Func<ParameterExpression, Expression> bodyFactory
    )
    {
        ParameterExpression rowParam = Expression.Parameter(typeof(IRow), "row");
        Expression body = bodyFactory(rowParam);
        Expression typedBody = body.Type == typeof(T)
            ? body
            : Expression.Convert(body, typeof(T));
        return Expression.Lambda<Func<IRow, T>>(typedBody, rowParam).Compile();
    }

    private static Expression FieldValue(
        MethodInfo getCellValueMethod,
        ParameterExpression row,
        string entity,
        string field
    )
    {
        return Expression.Call(
            getCellValueMethod,
            row,
            Expression.Constant(entity),
            Expression.Constant(field)
        );
    }

    private static Expression BuildBoolean(
        BooleanReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant(scalar.Value),
            equality => BuildEquality(equality, row),
            op => BuildBooleanOperator(op, row),
            BuildComparison
        );
    }

    private static Expression BuildEquality(
        ModelEquality equality,
        ParameterExpression row
    )
    {
        return equality.Match(
            BuildSingleValueEquality,
            array => BuildArrayEquality(array, row)
        );
    }

    private static Expression BuildSingleValueEquality(
        SingleValueEquality equality
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
            eq => BuildBoolArrayEquality(eq, row),
            eq =>
                BuildContainmentEquality(
                    left: eq.Left.IsT1,
                    leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<DateOnly>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
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
                    leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<DateTime>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
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
                    leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<double>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
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
                    leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<TimeOnly>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
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
                    leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
                    leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
                    leftScalar: eq.Left.IsT2
                        ? (IEnumerable<Guid>?)eq.Left.AsT2.Value
                        : null,
                    right: eq.Right.IsT1,
                    rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
                    rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
                    rightScalar: eq.Right.IsT2
                        ? (IEnumerable<Guid>?)eq.Right.AsT2.Value
                        : null,
                    row,
                    GetGuidValueMethod
                )
        );
    }

    private static Expression BuildBoolArrayEquality(
        BooleanArrayEquality eq,
        ParameterExpression row
    )
    {
        return BuildContainmentEquality(
            left: eq.Left.IsT1,
            leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
            leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
            leftScalar: eq.Left.IsT0 ? (IEnumerable<bool>?)eq.Left.AsT0.Value : null,
            right: eq.Right.IsT1,
            rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
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
            leftEntity: eq.Left.IsT1 ? eq.Left.AsT1.Entity : null,
            leftField: eq.Left.IsT1 ? eq.Left.AsT1.Field : null,
            leftScalar: eq.Left.IsT2 ? (IEnumerable<string>?)eq.Left.AsT2.Value : null,
            right: eq.Right.IsT1,
            rightEntity: eq.Right.IsT1 ? eq.Right.AsT1.Entity : null,
            rightField: eq.Right.IsT1 ? eq.Right.AsT1.Field : null,
            rightScalar: eq.Right.IsT2 ? (IEnumerable<string>?)eq.Right.AsT2.Value : null,
            row,
            GetTextValueMethod
        );
    }

    private static Expression BuildContainmentEquality<T>(
        bool left,
        string? leftEntity,
        string? leftField,
        IEnumerable<T>? leftScalar,
        bool right,
        string? rightEntity,
        string? rightField,
        IEnumerable<T>? rightScalar,
        ParameterExpression row,
        MethodInfo getCellValueMethod
    )
    {
        if (left && rightScalar is not null)
        {
            // Whole-array equal of a field against a literal array is a
            // single order-sensitive sequence comparison over the full row
            // set (SQL result-set semantics), not a per-row membership test.
            // Building that requires the full materialized row sequence
            // before this row-scoped predicate is compiled, which is a
            // larger restructure than this fix covers — see issue #114.
            // Fail fast rather than silently degrading to `Contains`
            // ("IN") membership, which is order-insensitive and wrong.
            throw new NotSupportedException(WholeArrayFieldEqualityNotSupported);
        }

        if (right && leftScalar is not null)
        {
            // See the comment above: same reasoning for the mirrored
            // literal-vs-field operand order.
            throw new NotSupportedException(WholeArrayFieldEqualityNotSupported);
        }

        if (left && right)
        {
            Expression left1 = FieldValue(
                getCellValueMethod,
                row,
                leftEntity!,
                leftField!
            );
            Expression right1 = FieldValue(
                getCellValueMethod,
                row,
                rightEntity!,
                rightField!
            );
            return Expression.Equal(left1, right1);
        }

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

        throw new NotSupportedException(ParameterNotSupported);
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
                        BuildBoolArrayPerRow(arrayReturning, row)
                ),
            or =>
                or.Conditions.Match(
                    conditions =>
                        conditions
                            .Select(c => BuildBoolean(c, row))
                            .Aggregate(Expression.OrElse),
                    arrayReturning =>
                        BuildBoolArrayPerRow(arrayReturning, row)
                ),
            not => Expression.Not(BuildBoolean(not.Condition, row))
        );
    }

    // ===== Per-row evaluation of BooleanArrayReturning =====

    private static Expression BuildBoolArrayPerRow(
        BooleanArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            scalar => Expression.Constant(scalar.Value.All(v => v)),
            field =>
                Expression.Equal(
                    FieldValue(GetBoolValueMethod, row, field.Entity, field.Field),
                    Expression.Constant((bool?)true, typeof(bool?))
                ),
            _ => throw new NotSupportedException(ParameterNotSupported),
            comparison => BuildEachComparisonPerRow(comparison, row),
            equality => BuildEachEqualityPerRow(equality, row),
            and =>
                and.Conditions
                    .Select(c => BuildBoolArrayPerRow(c, row))
                    .Aggregate(Expression.AndAlso),
            or =>
                or.Conditions
                    .Select(c => BuildBoolArrayPerRow(c, row))
                    .Aggregate(Expression.OrElse),
            not => Expression.Not(BuildBoolArrayPerRow(not.Condition, row))
        );
    }

    private static Expression BuildEachEqualityPerRow(
        EachEquality equality,
        ParameterExpression row
    )
    {
        return equality.Match(
            eq =>
                Expression.Equal(
                    BuildBoolArrayValuePerRow(eq.Left, row),
                    BuildBoolPerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildNumberArrayValuePerRow(eq.Left, row),
                    BuildNumberPerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildStringArrayValuePerRow(eq.Left, row),
                    BuildStringPerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildDateArrayValuePerRow(eq.Left, row),
                    BuildDatePerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildTimeArrayValuePerRow(eq.Left, row),
                    BuildTimePerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildDateTimeArrayValuePerRow(eq.Left, row),
                    BuildDateTimePerRow(eq.Right, row)
                ),
            eq =>
                Expression.Equal(
                    BuildUuidArrayValuePerRow(eq.Left, row),
                    BuildUuidPerRow(eq.Right, row)
                )
        );
    }

    private static Expression BuildEachComparisonPerRow(
        EachComparison comparison,
        ParameterExpression row
    )
    {
        return comparison.Match(
            c =>
                BuildTypedComparison(
                    BuildNumberArrayValuePerRow(c.Left, row),
                    BuildNumberPerRow(c.Right, row),
                    ToComparisonOperator(c.Operator)
                ),
            c =>
                BuildTypedComparison(
                    BuildStringArrayValuePerRow(c.Left, row),
                    BuildStringPerRow(c.Right, row),
                    ToComparisonOperator(c.Operator)
                ),
            c =>
                BuildTypedComparison(
                    BuildDateArrayValuePerRow(c.Left, row),
                    BuildDatePerRow(c.Right, row),
                    ToComparisonOperator(c.Operator)
                ),
            c =>
                BuildTypedComparison(
                    BuildDateTimeArrayValuePerRow(c.Left, row),
                    BuildDateTimePerRow(c.Right, row),
                    ToComparisonOperator(c.Operator)
                ),
            c =>
                BuildTypedComparison(
                    BuildTimeArrayValuePerRow(c.Left, row),
                    BuildTimePerRow(c.Right, row),
                    ToComparisonOperator(c.Operator)
                )
        );
    }

    private static ComparisonOperator ToComparisonOperator(EachComparisonOperator op)
    {
        return op switch
        {
            EachComparisonOperator.EachGreaterThan => ComparisonOperator.GreaterThan,
            EachComparisonOperator.EachGreaterThanOrEqual =>
                ComparisonOperator.GreaterThanOrEqual,
            EachComparisonOperator.EachLessThan => ComparisonOperator.LessThan,
            EachComparisonOperator.EachLessThanOrEqual =>
                ComparisonOperator.LessThanOrEqual,
            _ => throw new NotSupportedException($"Unknown each operator: {op}"),
        };
    }

    private static Expression BuildBoolPerRow(
        OneOf<BooleanReturning, BooleanArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildBoolReturningAsExpr,
            each => BuildBoolArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildNumberPerRow(
        OneOf<NumberReturning, NumberArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildNumberReturningAsExpr,
            each => BuildNumberArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildStringPerRow(
        OneOf<StringReturning, StringArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildStringReturningAsExpr,
            each => BuildStringArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildDatePerRow(
        OneOf<DateReturning, DateArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildDateReturningAsExpr,
            each => BuildDateArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildTimePerRow(
        OneOf<TimeReturning, TimeArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildTimeReturningAsExpr,
            each => BuildTimeArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildDateTimePerRow(
        OneOf<DateTimeReturning, DateTimeArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildDateTimeReturningAsExpr,
            each => BuildDateTimeArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildUuidPerRow(
        OneOf<UuidReturning, UuidArrayReturning> value,
        ParameterExpression row
    )
    {
        return value.Match(
            BuildUuidReturningAsExpr,
            each => BuildUuidArrayValuePerRow(each, row)
        );
    }

    private static Expression BuildBoolArrayValuePerRow(
        BooleanArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            scalar => Expression.Constant(
                (bool?)scalar.Value.FirstOrDefault(),
                typeof(bool?)
            ),
            field =>
                FieldValue(GetBoolValueMethod, row, field.Entity, field.Field),
            _ => throw new NotSupportedException(ParameterNotSupported),
            _ => throw new NotSupportedException(
                "Nested each-comparison as a boolean value is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested each-equality as a boolean value is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested each-and as a boolean value is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested each-or as a boolean value is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested each-not as a boolean value is not supported."
            )
        );
    }

    private static Expression BuildNumberArrayValuePerRow(
        NumberArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetDoubleValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                (double?)scalar.Value.FirstOrDefault(),
                typeof(double?)
            ),
            arithmetic => BuildEachArithmeticPerRow(arithmetic, row),
            diff => BuildEachDateDiffDaysPerRow(diff, row),
            diff => BuildEachDateTimeDiffSecondsPerRow(diff, row),
            diff => BuildEachTimeDiffSecondsPerRow(diff, row)
        );
    }

    private static Expression BuildStringArrayValuePerRow(
        StringArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetTextValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                scalar.Value.FirstOrDefault(),
                typeof(string)
            )
        );
    }

    private static Expression BuildDateArrayValuePerRow(
        DateArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetDateOnlyValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                (DateOnly?)scalar.Value.FirstOrDefault(),
                typeof(DateOnly?)
            ),
            addDays => BuildEachDateAddDaysPerRow(addDays, row)
        );
    }

    private static Expression BuildTimeArrayValuePerRow(
        TimeArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetTimeOnlyValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                (TimeOnly?)scalar.Value.FirstOrDefault(),
                typeof(TimeOnly?)
            ),
            addSeconds => BuildEachTimeAddSecondsPerRow(addSeconds, row)
        );
    }

    private static Expression BuildDateTimeArrayValuePerRow(
        DateTimeArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetDateTimeValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                (DateTime?)scalar.Value.FirstOrDefault(),
                typeof(DateTime?)
            ),
            addSeconds => BuildEachDateTimeAddSecondsPerRow(addSeconds, row)
        );
    }

    private static Expression BuildUuidArrayValuePerRow(
        UuidArrayReturning returning,
        ParameterExpression row
    )
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            field =>
                FieldValue(GetGuidValueMethod, row, field.Entity, field.Field),
            scalar => Expression.Constant(
                (Guid?)scalar.Value.FirstOrDefault(),
                typeof(Guid?)
            )
        );
    }

    // ===== EachArithmetic per row =====

    private static Expression BuildEachArithmeticPerRow(
        EachArithmetic arithmetic,
        ParameterExpression row
    )
    {
        return arithmetic.Match(
            add =>
                add.Values
                    .Select(v => BuildNumberPerRow(v, row))
                    .Aggregate(NullableDoubleAdd),
            sub =>
                sub.Values
                    .Select(v => BuildNumberPerRow(v, row))
                    .Aggregate(NullableDoubleSubtract),
            mul =>
                mul.Values
                    .Select(v => BuildNumberPerRow(v, row))
                    .Aggregate(NullableDoubleMultiply),
            div =>
                div.Values
                    .Select(v => BuildNumberPerRow(v, row))
                    .Aggregate(NullableDoubleDivide)
        );
    }

    private static readonly MethodInfo AddNullableDoubleMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(AddDoubles),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo SubtractNullableDoubleMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(SubtractDoubles),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo MultiplyNullableDoubleMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(MultiplyDoubles),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo DivideNullableDoubleMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(DivideDoubles),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    internal static double? AddDoubles(double? a, double? b)
    {
        return a.HasValue && b.HasValue ? a.Value + b.Value : null;
    }

    internal static double? SubtractDoubles(double? a, double? b)
    {
        return a.HasValue && b.HasValue ? a.Value - b.Value : null;
    }

    internal static double? MultiplyDoubles(double? a, double? b)
    {
        return a.HasValue && b.HasValue ? a.Value * b.Value : null;
    }

    internal static double? DivideDoubles(double? a, double? b)
    {
        return !a.HasValue || !b.HasValue
            ? null
            : b.Value == 0
            ? throw new DivideByZeroException(
                "eachDivide by zero: division by zero is a defined failure, "
                    + "matching SQL division-by-zero semantics."
            )
            : a.Value / b.Value;
    }

    private static Expression NullableDoubleAdd(Expression a, Expression b)
    {
        return Expression.Call(AddNullableDoubleMethod, a, b);
    }

    private static Expression NullableDoubleSubtract(Expression a, Expression b)
    {
        return Expression.Call(SubtractNullableDoubleMethod, a, b);
    }

    private static Expression NullableDoubleMultiply(Expression a, Expression b)
    {
        return Expression.Call(MultiplyNullableDoubleMethod, a, b);
    }

    private static Expression NullableDoubleDivide(Expression a, Expression b)
    {
        return Expression.Call(DivideNullableDoubleMethod, a, b);
    }

    // ===== Date/Time/DateTime per-row arithmetic =====

    private static readonly MethodInfo AddDaysMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(AddDays),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo DiffDaysMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(DiffDays),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo AddSecondsToTimeMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(AddSecondsToTime),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo DiffSecondsTimeMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(DiffSecondsTime),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo AddSecondsToDateTimeMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(AddSecondsToDateTime),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static readonly MethodInfo DiffSecondsDateTimeMethod =
        typeof(WhereExpressionBuilder).GetMethod(
            nameof(DiffSecondsDateTime),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    internal static DateOnly? AddDays(DateOnly? d, double? n)
    {
        return d.HasValue && n.HasValue ? d.Value.AddDays((int)n.Value) : null;
    }

    internal static double? DiffDays(DateOnly? l, DateOnly? r)
    {
        return l.HasValue && r.HasValue
            ? (l.Value.DayNumber - r.Value.DayNumber)
            : null;
    }

    internal static TimeOnly? AddSecondsToTime(TimeOnly? t, double? n)
    {
        return t.HasValue && n.HasValue
            ? t.Value.Add(TimeSpan.FromSeconds(n.Value))
            : null;
    }

    internal static double? DiffSecondsTime(TimeOnly? l, TimeOnly? r)
    {
        return l.HasValue && r.HasValue
            ? (l.Value - r.Value).TotalSeconds
            : null;
    }

    internal static DateTime? AddSecondsToDateTime(DateTime? d, double? n)
    {
        return d.HasValue && n.HasValue
            ? d.Value.AddSeconds(n.Value)
            : null;
    }

    internal static double? DiffSecondsDateTime(DateTime? l, DateTime? r)
    {
        return l.HasValue && r.HasValue ? (l.Value - r.Value).TotalSeconds : null;
    }

    private static Expression BuildEachDateAddDaysPerRow(
        EachDateAddDays op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            AddDaysMethod,
            BuildDatePerRow(op.Left, row),
            BuildNumberPerRow(op.Right, row)
        );
    }

    private static Expression BuildEachDateDiffDaysPerRow(
        EachDateDiffDays op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            DiffDaysMethod,
            BuildDatePerRow(op.Left, row),
            BuildDatePerRow(op.Right, row)
        );
    }

    private static Expression BuildEachTimeAddSecondsPerRow(
        EachTimeAddSeconds op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            AddSecondsToTimeMethod,
            BuildTimePerRow(op.Left, row),
            BuildNumberPerRow(op.Right, row)
        );
    }

    private static Expression BuildEachTimeDiffSecondsPerRow(
        EachTimeDiffSeconds op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            DiffSecondsTimeMethod,
            BuildTimePerRow(op.Left, row),
            BuildTimePerRow(op.Right, row)
        );
    }

    private static Expression BuildEachDateTimeAddSecondsPerRow(
        EachDateTimeAddSeconds op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            AddSecondsToDateTimeMethod,
            BuildDateTimePerRow(op.Left, row),
            BuildNumberPerRow(op.Right, row)
        );
    }

    private static Expression BuildEachDateTimeDiffSecondsPerRow(
        EachDateTimeDiffSeconds op,
        ParameterExpression row
    )
    {
        return Expression.Call(
            DiffSecondsDateTimeMethod,
            BuildDateTimePerRow(op.Left, row),
            BuildDateTimePerRow(op.Right, row)
        );
    }

    // ===== Comparison handling =====

    private static Expression BuildComparison(
        Comparison comparison
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
        if (left.Type == typeof(string) || right.Type == typeof(string))
        {
            Expression compareExpr = Expression.Call(
                StringCompareOrdinalMethod,
                left,
                right
            );
            Expression zero = Expression.Constant(0);
            return op switch
            {
                ComparisonOperator.GreaterThan => Expression.GreaterThan(
                    compareExpr,
                    zero
                ),
                ComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(
                    compareExpr,
                    zero
                ),
                ComparisonOperator.LessThan => Expression.LessThan(compareExpr, zero),
                ComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(
                    compareExpr,
                    zero
                ),
                _ => throw new NotSupportedException(
                    $"Unknown comparison operator: {op}"
                ),
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

    // ===== Scalar-only returnings =====

    private static Expression BuildBoolReturningAsExpr(BooleanReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((bool?)scalar.Value, typeof(bool?)),
            _ => throw new NotSupportedException(
                "Nested equality in scalar boolean context is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested boolean operator in scalar boolean context is not supported."
            ),
            _ => throw new NotSupportedException(
                "Nested comparison in scalar boolean context is not supported."
            )
        );
    }

    private static Expression BuildNumberReturningAsExpr(NumberReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((double?)scalar.Value, typeof(double?)),
            _ => LiteralArithmeticEvaluator.TryEvaluate(returning, out double literal)
                ? Expression.Constant((double?)literal, typeof(double?))
                : throw new NotSupportedException(
                    "Single-value Arithmetic outside per-row context is not "
                        + "supported unless every operand is a literal constant."
                ),
            _ => throw new NotSupportedException(AggregateNotSupported),
            _ => throw new NotSupportedException(AggregateNotSupported)
        );
    }

    private static Expression BuildStringReturningAsExpr(StringReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant(scalar.Value, typeof(string)),
            _ => throw new NotSupportedException(AggregateNotSupported)
        );
    }

    private static Expression BuildDateReturningAsExpr(DateReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((DateOnly?)scalar.Value, typeof(DateOnly?)),
            _ => throw new NotSupportedException(AggregateNotSupported)
        );
    }

    private static Expression BuildDateTimeReturningAsExpr(DateTimeReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((DateTime?)scalar.Value, typeof(DateTime?)),
            _ => throw new NotSupportedException(AggregateNotSupported)
        );
    }

    private static Expression BuildTimeReturningAsExpr(TimeReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((TimeOnly?)scalar.Value, typeof(TimeOnly?)),
            _ => throw new NotSupportedException(AggregateNotSupported)
        );
    }

    private static Expression BuildUuidReturningAsExpr(UuidReturning returning)
    {
        return returning.Match(
            _ => throw new NotSupportedException(ParameterNotSupported),
            scalar => Expression.Constant((Guid?)scalar.Value, typeof(Guid?))
        );
    }
}
