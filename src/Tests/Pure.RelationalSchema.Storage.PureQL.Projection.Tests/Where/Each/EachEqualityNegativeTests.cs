using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Negative path: each equality against a value that no row has yields an empty
// result, one case per non-boolean value type.
[Trait("Clause", "Where")]
[Trait("Feature", "EachEqualityNegative")]
public sealed class EachEqualityNegativeTests
{
    [Fact]
    public void EachNumberEqualityWithAbsentValueReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachNumberEqualityWithAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void EachDateEqualityWithAbsentValueReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachDateEqualityWithAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void EachUuidEqualityWithAbsentValueReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachUuidEqualityWithAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
