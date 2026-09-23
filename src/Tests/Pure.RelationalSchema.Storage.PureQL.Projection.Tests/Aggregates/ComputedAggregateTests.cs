using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// An aggregate's argument may be any array-returning expression, including
// per-row (each*) computed values: sum/average/count fold the computed
// per-row results, and min/max fold per-row temporal diffs, not stored
// columns directly.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "ComputedAggregate")]
public sealed class ComputedAggregateTests
{
    private static double PriceOf(
        IReadOnlyList<ProductRecord> productRows,
        Guid productId
    )
    {
        return productRows
            .Single(product => product.ProductId == productId)
            .ProductPrice;
    }

    [Fact]
    public void SumOfEachMultiplyProjectsPerGroupRevenue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new SumOfQuantityTimesPriceGroupedByOrderQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.ItemQty * PriceOf(productRows, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new ItemOrderIdColumn().Name.TextValue)!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachMultiplyProjectsPerGroupMean()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new AverageOfEachMultiplyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(item =>
                    item.ItemQty * PriceOf(productRows, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new ItemOrderIdColumn().Name.TextValue)!.Value,
            row => row.Double("meanLineValue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfEachDateDiffDaysProjectsPerGroupSpan()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MaxOfEachDateDiffDaysQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, Span: (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber))
            )
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Span));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row => row.Double("maxSpanDays")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfEachTimeDiffSecondsProjectsPerGroupValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new MinOfEachTimeDiffSecondsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, double> expected = userRows
            .GroupBy(user => user.UserActive)
            .ToDictionary(
                group => group.Key,
                group => group.Min(user => (user.ShiftStart - origin).TotalSeconds)
            );

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("minShiftGapSeconds")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfEachArithmeticCountsGroupRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new CountOfEachArithmeticQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("rowCount")!.Value
        );

        Assert.Equal(expected, actual);
    }
}
