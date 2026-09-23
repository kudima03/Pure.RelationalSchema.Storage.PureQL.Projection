using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// DISTINCT deduplicates the projected result rows (not the source rows), so a
// low-cardinality projected column collapses to its distinct values.
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctTests
{
    [Fact]
    public void DistinctOnStringColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOnStringColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Select(order => order.OrderStatus).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void DistinctOnBooleanColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DistinctOnBooleanColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.UserActive).Distinct().Count(),
            result.Count
        );
    }
}
