using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Aggregates evaluated over an outer join: unmatched left rows carry empty
// cells for the joined side, and SQL count/sum semantics skip those absent
// values, so aggregates over a joined column see only the matched rows.
[Trait("Clause", "Join")]
[Trait("Feature", "OuterJoinAggregation")]
public sealed class OuterJoinAggregationTests
{
    [Fact]
    public void CountOverJoinedColumnAfterLeftJoinCountsOnlyMatchedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new CountOverJoinedColumnAfterLeftJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every order matches a user; the padded row of the orderless user
        // must not contribute to the count.
        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("orderCount"));
    }

    [Fact]
    public void LeftJoinGroupByUserCountsZeroForUnmatchedUsers()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinGroupByUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = userRows.ToDictionary(
            user => user.UserId,
            user => (double)
                orderRows.Count(order => order.OrderUserId == user.UserId)
        );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row => row.Double("orderCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SumOverJoinedColumnAfterLeftJoinIgnoresPaddedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumOverJoinedColumnAfterLeftJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            orderRows.Sum(order => order.OrderTotal),
            result.Row(0).Double("totalSum")
        );
    }
}
