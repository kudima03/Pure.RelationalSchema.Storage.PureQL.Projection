using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING with no groupBy is schema-valid (SQL-style implicit whole-set
// group). It is honoured today only when the select list contains an
// aggregate (which engages group mode); with plain field selects the clause
// is silently dropped (issue #83).
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingWithoutGroupByTests
{
    [Fact]
    public void HavingWithoutGroupByFiltersTheImplicitWholeSetGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingWithoutGroupByQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void WholeSetHavingKeepsTheSingleGroupWhenSatisfied()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new WholeSetHavingGreaterThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(userRows.Count, result.Row(0).Double("userCount"));
    }

    [Fact]
    public void WholeSetHavingRemovesTheSingleGroupWhenUnsatisfied()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new WholeSetHavingGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
