using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Combined;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// End-to-end queries that combine several clauses, checking they compose into
// one correct result set.
[Trait("Clause", "Combined")]
[Trait("Feature", "CombinedClauses")]
public sealed class CombinedClauseTests
{
    [Fact]
    public void WhereThenOrderByThenPaginateReturnsCorrectWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WhereThenOrderByThenPaginateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 50)
                .OrderBy(order => order.OrderTotal)
                .Skip(1)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue)),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WhereThenGroupByYieldsGroupsOfTheFilteredRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new UsersWithNonCancelledOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .Where(order => order.OrderStatus != "cancelled")
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void JoinThenWhereThenOrderByThenPaginateReturnsCorrectWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new JoinThenWhereThenOrderByThenPaginateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 75)
                .OrderByDescending(order => order.OrderTotal)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue)),
        ];

        Assert.Equal(expected, actual);
    }
}
