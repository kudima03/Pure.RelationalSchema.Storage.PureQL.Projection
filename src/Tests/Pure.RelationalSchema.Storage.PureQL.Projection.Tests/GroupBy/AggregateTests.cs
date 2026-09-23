using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Aggregate projections (sum/count/... over each group): one result row per
// group, holding the folded aggregate value.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Aggregate")]
public sealed class AggregateTests
{
    [Fact]
    public void SumAggregateProjectsPerGroupTotal()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new SumAggregateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }

    [Fact]
    public void CountAggregateProjectsPerGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new CountAggregateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
