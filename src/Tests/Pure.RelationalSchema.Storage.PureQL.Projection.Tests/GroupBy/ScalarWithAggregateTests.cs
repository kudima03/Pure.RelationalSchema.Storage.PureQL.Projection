using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Scalar select expressions are legal alongside aggregates in group mode
// (SELECT 'all' AS scope, COUNT(id) FROM t): the constant repeats on every
// group's output row, for whole-set groups, per-key groups and groups
// surviving HAVING alike.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "ScalarWithAggregate")]
public sealed class ScalarWithAggregateTests
{
    [Fact]
    public void ScalarAlongsideWholeSetCountProjectsSingleRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new ScalarAlongsideWholeSetCountQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal("all", result.Row(0)["scope"]);
        Assert.Equal(orderRows.Count, result.Row(0).Double("order_count"));
    }

    [Fact]
    public void ScalarRepeatsOnEveryGroupRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new ScalarInGroupModeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expectedTotals = orderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(order => order.OrderTotal)
            );

        Assert.Equal(expectedTotals.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(1, row.Double("version")));
        Assert.Equal(
            expectedTotals.Values.OrderBy(total => total),
            result.Rows
                .Select(row => row.Double("status_total") ?? double.NaN)
                .OrderBy(total => total)
        );
    }

    [Fact]
    public void ScalarGroupKeyFieldAndAggregateMixInOneQuery()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new ScalarGroupKeyFieldAndAggregateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expectedTotals = orderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(order => order.OrderTotal)
            );

        Assert.Equal(expectedTotals.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Assert.Equal("2024-06", row["period"]);
                string status = row[new OrderStatusColumn().Name.TextValue] ?? string.Empty;
                Assert.Equal(
                    expectedTotals[status],
                    row.Double("status_total")
                );
            }
        );
    }

    [Fact]
    public void BooleanAndUuidScalarsProjectInGroupMode()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Guid marker = new Guid("9b2b1f6e-3c86-4c50-8f6a-2f6d1a8f2c11");

        Query query = new BooleanAndUuidScalarsInGroupModeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(true, result.Row(0).Bool("flag"));
        Assert.Equal(marker, result.Row(0).Uuid("marker"));
        Assert.Equal(orderRows.Count, result.Row(0).Double("order_count"));
    }

    [Fact]
    public void ScalarProjectsOnGroupsSurvivingHaving()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new ScalarWithHavingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() > 1);

        Assert.Equal(expectedGroups, result.Count);
        Assert.All(result.Rows, row => Assert.Equal("repeat-buyer", row["tag"]));
    }
}
