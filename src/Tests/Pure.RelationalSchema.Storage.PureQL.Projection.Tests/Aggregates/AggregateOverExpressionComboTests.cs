using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Matrix of aggregate (count / numeric sum-avg-min-max) over each-arithmetic
// (eachAdd/eachSubtract/eachMultiply/eachDivide) arguments, across varied
// group-key types (uuid/string/bool/number) and whole-set, plus HAVING on
// the computed aggregate. Temporal each-expression combos live in
// AggregateOverExpressionComboTemporalTests.cs. Every expected value is
// computed independently in LINQ over the ground-truth record lists under
// SQL aggregate rules: aggregates ignore NULL inputs, COUNT ignores NULL
// results the same as any other aggregate, and an all-NULL/empty group folds
// to NULL (COUNT folds to 0).
//
// eachDivide by zero: WhereExpressionBuilder.DivideDoubles raises
// DivideByZeroException for a zero divisor (matching SQL division-by-zero
// semantics), not a silent NULL - pinned for WHERE by
// Errors/NegativePathTests.EachDivideByZeroFailsFast. This suite pins the
// identical fail-fast behaviour when the same expression is folded by an
// aggregate instead of filtered by WHERE.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "AggregateOverExpressionCombo")]
public sealed class AggregateOverExpressionComboTests
{
    // ===== SQL NULL-aware folds over a nullable-double sequence =====

    private static double? SqlSum(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Sum();
    }

    private static double SqlCount(IEnumerable<double?> values)
    {
        return values.Count(v => v.HasValue);
    }

    private static double? SqlMin(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Min();
    }

    private static double? SqlMax(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Max();
    }

    // ===== A: eachMultiply(item_qty, product_price), grouped by uuid =====

    [Fact]
    public void SumOfEachMultiplyGroupedByOrderUserIdComputesRevenuePerUser()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new SumOfEachMultiplyGroupedByOrderUserIdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            join product in productRows on item.ItemProductId equals product.ProductId
            select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachMultiplyGroupedByOrderUserIdComputesMeanLineValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new AverageOfEachMultiplyGroupedByOrderUserIdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            join product in productRows on item.ItemProductId equals product.ProductId
            select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("meanLineValue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinAndMaxOfEachMultiplyGroupedByOrderUserIdBoundLineValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new MinAndMaxOfEachMultiplyGroupedByOrderUserIdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IReadOnlyList<(Guid UserId, double Value)> lineValues =
        [
            .. from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            join product in productRows on item.ItemProductId equals product.ProductId
            select (order.OrderUserId, Value: item.ItemQty * product.ProductPrice),
        ];

        Dictionary<Guid, double> expectedMin = lineValues
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Value));

        Dictionary<Guid, double> expectedMax = lineValues
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Value));

        Dictionary<Guid, double> actualMin = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("minLineValue")!.Value
        );

        Dictionary<Guid, double> actualMax = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("maxLineValue")!.Value
        );

        Assert.Equal(expectedMin, actualMin);
        Assert.Equal(expectedMax, actualMax);
    }

    [Fact]
    public void CountOfEachMultiplyGroupedByOrderUserIdCountsLineItems()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new CountOfEachMultiplyGroupedByOrderUserIdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            join product in productRows on item.ItemProductId equals product.ProductId
            select order.OrderUserId
        )
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("lineCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetSumOfEachMultiplyComputesTotalRevenue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new WholeSetSumOfEachMultiplyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expected = (
            from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            join product in productRows on item.ItemProductId equals product.ProductId
            select item.ItemQty * product.ProductPrice
        ).Sum();

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("revenue"));
    }

    // ===== B: eachAdd/eachSubtract(order_total, user_age), grouped by
    // bool/number/string =====

    [Fact]
    public void SumOfEachAddGroupedByUserActiveComputesTotalPlusAge()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumOfEachAddGroupedByUserActiveQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new { user.UserActive, Value = order.OrderTotal + user.UserAge }
        )
            .GroupBy(x => x.UserActive)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("totalPlusAge")!.Value
        );

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachAddGroupedByUserAgeComputesMeanTotalPlusAge()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new AverageOfEachAddGroupedByUserAgeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<double, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new { user.UserAge, Value = order.OrderTotal + user.UserAge }
        )
            .GroupBy(x => x.UserAge)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value));

        Dictionary<double, double> actual = result.Rows.ToDictionary(
            row => row.Double(new UserAgeColumn().Name.TextValue)!.Value,
            row => row.Double("meanTotalPlusAge")!.Value
        );

        // Ann and Cara share age 30, so this group merges two users' orders -
        // a real multi-user group, not merely one row per group.
        Assert.Contains(expected, pair => pair.Key == 30);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinAndMaxOfEachSubtractGroupedByUserActiveBoundDifference()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MinAndMaxOfEachSubtractGroupedByUserActiveQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IReadOnlyList<(bool Active, double Value)> diffs =
        [
            .. from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select (user.UserActive, Value: order.OrderTotal - user.UserAge),
        ];

        Dictionary<bool, double> expectedMin = diffs
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Value));

        Dictionary<bool, double> expectedMax = diffs
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Value));

        Dictionary<bool, double> actualMin = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("minDiff")!.Value
        );

        Dictionary<bool, double> actualMax = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("maxDiff")!.Value
        );

        Assert.Equal(expectedMin, actualMin);
        Assert.Equal(expectedMax, actualMax);
    }

    [Fact]
    public void CountOfEachSubtractGroupedByOrderStatusCountsRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new CountOfEachSubtractGroupedByOrderStatusQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expected = orderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[new OrderStatusColumn().Name.TextValue]!,
            row => row.Double("diffCount")!.Value
        );

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetAverageOfEachSubtractComputesOverallMeanDifference()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WholeSetAverageOfEachSubtractQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select order.OrderTotal - user.UserAge
        ).Average();

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("meanDiff"));
    }

    // ===== C: eachDivide(order_total, user_score) - NULL exclusion and
    // divide-by-zero fail-fast =====

    [Fact]
    public void SumAndCountOfEachDivideGroupedByOrderUserIdExcludeNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumAndCountOfEachDivideGroupedByOrderUserIdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IReadOnlyList<(Guid UserId, double? Ratio)> ratios =
        [
            .. from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select (
                order.OrderUserId,
                Ratio: user.UserScore.HasValue
                    ? order.OrderTotal / user.UserScore.Value
                    : (double?)null
            ),
        ];

        Dictionary<Guid, double?> expectedSum = ratios
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => SqlSum(g.Select(x => x.Ratio)));

        Dictionary<Guid, double> expectedCount = ratios
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => SqlCount(g.Select(x => x.Ratio)));

        Dictionary<Guid, double?> actualSum = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("sumRatio")
        );

        Dictionary<Guid, double> actualCount = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("ratioCount")!.Value
        );

        // Bob and Dan each have exactly one order and a NULL score, so their
        // group folds to an all-NULL sum (SQL SUM over no defined rows) and
        // a zero count - not merely "smaller", but genuinely empty.
        Assert.Contains(expectedSum, pair => pair.Value is null);
        Assert.Contains(expectedCount, pair => pair.Value == 0);
        Assert.Equal(expectedSum, actualSum);
        Assert.Equal(expectedCount, actualCount);
    }

    [Fact]
    public void MinOfEachDivideWholeSetExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MinOfEachDivideWholeSetQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IEnumerable<double?> ratios =
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select user.UserScore.HasValue
                ? order.OrderTotal / user.UserScore.Value
                : (double?)null;

        double? expected = SqlMin(ratios);

        _ = Assert.NotNull(expected);
        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("minRatio"));
    }

    [Fact]
    public void MaxOfEachDivideWholeSetExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MaxOfEachDivideWholeSetQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IEnumerable<double?> ratios =
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select user.UserScore.HasValue
                ? order.OrderTotal / user.UserScore.Value
                : (double?)null;

        double? expected = SqlMax(ratios);

        _ = Assert.NotNull(expected);
        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("maxRatio"));
    }

    [Fact]
    public void AggregateOverEachDivideByZeroDenominatorThrowsDivideByZeroException()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new AggregateOverEachDivideByZeroDenominatorQuery().Value;

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ===== D: HAVING on the computed aggregate =====

    [Fact]
    public void HavingSumOfEachMultiplyGreaterThanKeepsQualifyingOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new HavingSumOfEachMultiplyGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. (
                from item in orderItemRows
                join order in orderRows on item.ItemOrderId equals order.OrderId
                join product in productRows
                    on item.ItemProductId equals product.ProductId
                select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
            )
                .GroupBy(x => x.OrderUserId)
                .Where(g => g.Sum(x => x.Value) > 25)
                .Select(g => g.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAverageOfEachSubtractLessThanOrEqualKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingAverageOfEachSubtractLessThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. (
                from order in orderRows
                join user in userRows on order.OrderUserId equals user.UserId
                select new { user.UserActive, Value = order.OrderTotal - user.UserAge }
            )
                .GroupBy(x => x.UserActive)
                .Where(g => g.Average(x => x.Value) <= 100)
                .Select(g => g.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingCountOfEachDivideEqualToZeroKeepsOnlyAllNullGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingCountOfEachDivideEqualToZeroQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. (
                from order in orderRows
                join user in userRows on order.OrderUserId equals user.UserId
                select (
                    order.OrderUserId,
                    Ratio: user.UserScore.HasValue
                        ? order.OrderTotal / user.UserScore.Value
                        : (double?)null
                )
            )
                .GroupBy(x => x.OrderUserId)
                .Where(g => SqlCount(g.Select(x => x.Ratio)) == 0)
                .Select(g => g.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }
}
