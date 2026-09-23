using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON condition is an inequality (each-comparison) rather than an
// equi-key: every left/right pair whose columns satisfy the comparison is kept.
[Trait("Clause", "Join")]
[Trait("Feature", "NonEquiJoin")]
public sealed class NonEquiJoinTests
{
    [Fact]
    public void InnerJoinOnPriceLessThanTotalKeepsEveryQualifyingPair()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new InnerJoinOnPriceLessThanTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Sum(order =>
            productRows.Count(product => product.ProductPrice < order.OrderTotal)
        );

        Assert.Equal(expected, result.Count);
    }
}
