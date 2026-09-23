using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Numeric aggregates (sum / avg / min / max) over a group or the whole set.
// With groupBy they fold each group; without it they fold the whole set into
// a single result row.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "NumericAggregate")]
public sealed class NumericAggregateTests
{
    [Fact]
    public void AverageOfTotalPerUserProjectsGroupMean()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new AverageOfTotalPerUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Average(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("avg_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfTotalPerUserProjectsGroupMinimum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MinOfTotalPerUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Min(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("min_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfTotalPerUserProjectsGroupMaximum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new MaxOfTotalPerUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("max_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SumOfAllTotalsProjectsSingleWholeSetValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumOfAllTotalsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            orderRows.Sum(order => order.OrderTotal),
            result.Row(0).Double("sum_total")
        );
    }
}
