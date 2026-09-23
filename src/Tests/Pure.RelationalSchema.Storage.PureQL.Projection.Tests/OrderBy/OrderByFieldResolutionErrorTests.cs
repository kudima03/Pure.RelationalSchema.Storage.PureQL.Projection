using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.OrderBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// orderBy field resolution intentionally differs by query mode
// (RowsFromDatasets.Build): without groupBy it runs pre-projection and
// needs the source column name (OrderByExpansionTests.
// OrderByAliasedSelectColumnStillOrdersByUnderlyingField pins this); with
// groupBy/aggregates it runs post-projection and needs the select alias
// (OrderByExpansionTests.OrderByAggregateResultOrdersEmittedGroupsByItsValue
// pins that). Both rules stay unchanged here (issue #135) - only the
// failure a caller hits by using the wrong name for the current mode is
// pinned as a KeyNotFoundException whose message names the field/entity and
// spells out which name is expected in which mode, instead of the bare
// "Row has no column named 'x'." message that gives no hint why.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByFieldResolutionError")]
public sealed class OrderByFieldResolutionErrorTests
{
    [Fact]
    public void OrderingByAliasWithoutGroupByThrowsWithRuleInMessage()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new OrderByAliasWithoutGroupByQuery().Value;

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(datasets, query))
        );

        Assert.Contains("years", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "source column",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "select alias",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void OrderingByOriginalFieldNameInGroupByThrowsWithRuleInMessage()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new OrderByOriginalFieldNameInGroupByQuery().Value;

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(datasets, query))
        );

        Assert.Contains(
            new OrderTotalColumn().Name.TextValue,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "source column",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "select alias",
            exception.Message,
            StringComparison.Ordinal
        );
    }
}
