using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Combined;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// A group-mode select list may mix the grouping key with one or more
// aggregates in a single row: each output row pairs the group's key value
// with its folded aggregate value(s), not just the aggregate alone.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "MixedProjection")]
public sealed class MixedProjectionTests
{
    [Fact]
    public void GroupKeyAndSumProjectTogetherPerGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupKeyAndSumQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("userTotal")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupKeyAndCountProjectTogetherPerGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupKeyAndCountQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expected = orderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[new OrderStatusColumn().Name.TextValue]!,
            row => row.Double("statusCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MultipleAggregatesOfDifferentTypesProjectInOneRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new MultipleAggregatesOfDifferentTypesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (double Count, double Sum, DateOnly Min, string Max)> expected =
            orderRows
                .GroupBy(order => order.OrderUserId)
                .ToDictionary(
                    group => group.Key,
                    group => (
                        Count: (double)group.Count(),
                        Sum: group.Sum(order => order.OrderTotal),
                        Min: group.Min(order => order.PlacedOn),
                        Max: group.Select(order => order.OrderStatus)
                            .Max(StringComparer.Ordinal)!
                    )
                );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value;
                (double Count, double Sum, DateOnly Min, string Max) expectedGroup =
                    expected[userId];
                Assert.Equal(expectedGroup.Count, row.Double("orderCount"));
                Assert.Equal(expectedGroup.Sum, row.Double("totalSum"));
                Assert.Equal(expectedGroup.Min, row.Date("earliestPlacedOn"));
                Assert.Equal(expectedGroup.Max, row["maxStatus"]);
            }
        );
    }

    [Fact]
    public void TwoNumericAggregatesOverDifferentColumnsProjectIndependently()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new TwoNumericAggregatesOverDifferentColumnsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (double Sum, double Avg)> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, order.OrderTotal, user.UserAge)
            )
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (
                    Sum: group.Sum(row => row.OrderTotal),
                    Avg: group.Average(row => row.UserAge)
                )
            );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid(new UserIdColumn().Name.TextValue)!.Value;
                (double Sum, double Avg) expectedGroup = expected[userId];
                Assert.Equal(expectedGroup.Sum, row.Double("orderTotalSum"));
                Assert.Equal(expectedGroup.Avg, row.Double("avgAge"));
            }
        );
    }

    [Fact]
    public void AggregateColumnsFollowAliasesInMixedProjection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new AggregateColumnsWithAliasesInMixedProjectionQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(["buyer", "purchases", "spend"], result.ColumnNames);
    }
}
