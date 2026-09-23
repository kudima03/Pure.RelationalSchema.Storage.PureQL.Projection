using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Parameters;

// PureQL parameters are placeholders bound to values at execution time. The
// PureQLProjection public API exposes no surface to supply parameter values, so
// there is no defined result for a parameterised query through this entry
// point; the translator fails fast with NotSupportedException instead of
// silently mis-binding. These tests pin that explicit-failure contract. When a
// binding API is added, they should be replaced with value-binding assertions.
[Trait("Clause", "Where")]
[Trait("Feature", "Parameter")]
public sealed class ParameterTests
{
    [Fact]
    public void StringParameterInEachEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new StringParameterInEachEqualityQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void NumberParameterInEachEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new NumberParameterInEachEqualityQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
