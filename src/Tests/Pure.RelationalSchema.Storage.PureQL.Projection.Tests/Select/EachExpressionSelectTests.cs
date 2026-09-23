using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// A bare each* (per-row array-returning) expression has no defined result
// when projected directly: it must be folded by an aggregate first (see
// AggregateOverPerRowArithmeticTests). Selecting it unwrapped is a known
// execution gap (CLAUDE.md: "computed select columns") and must fail fast
// with NotSupportedException, with or without groupBy, rather than crash on
// an internal OneOf type mismatch (issue #134).
[Trait("Clause", "Select")]
[Trait("Feature", "EachExpressionProjection")]
public sealed class EachExpressionSelectTests
{
    [Fact]
    public void BareEachMultiplyInSelectWithoutGroupByFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new BareEachMultiplyInSelectWithoutGroupByQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query).ToList()
        );
    }

    [Fact]
    public void BareEachSubtractInGroupBySelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new BareEachSubtractInGroupBySelectQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query).ToList()
        );
    }
}
