using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join composed with later clauses (group by, and order by + pagination) to
// exercise the pipeline ordering over merged rows.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinWithClauses")]
public sealed class JoinWithClausesTests
{
    [Fact]
    public void InnerJoinThenGroupByStatusYieldsDistinctStatuses()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinThenGroupByStatusQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        string?[] actual =
        [
            .. result.Column(new OrderStatusColumn().Name.TextValue).OrderBy(s => s),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InnerJoinThenOrderByTotalWithPaginationReturnsWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinThenOrderByTotalWithPaginationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderTotal)
                .Skip(1)
                .Take(3)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue)),
        ];

        Assert.Equal(expected, actual);
    }
}
