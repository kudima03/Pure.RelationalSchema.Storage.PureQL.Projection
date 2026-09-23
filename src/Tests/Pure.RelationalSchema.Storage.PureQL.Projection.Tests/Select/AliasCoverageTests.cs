using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Aliases can cover every select item in a query, and an alias may collide
// with a different source column's name without the projected value being
// confused with that other column.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAlias")]
public sealed class AliasCoverageTests
{
    [Fact]
    public void AliasesOnEverySelectItemRenameAllColumns()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new AliasesOnEverySelectItemQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(["id", "state", "amount"], result.ColumnNames);
        Assert.DoesNotContain(new OrderIdColumn().Name.TextValue, result.ColumnNames);
        Assert.DoesNotContain(new OrderStatusColumn().Name.TextValue, result.ColumnNames);
        Assert.DoesNotContain(new OrderTotalColumn().Name.TextValue, result.ColumnNames);
    }

    [Fact]
    public void AliasEqualToAnotherFieldNameShadowsInProjection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new AliasEqualToAnotherFieldNameQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal([new OrderTotalColumn().Name.TextValue], result.ColumnNames);

        string?[] expected = [.. orderRows.Select(order => order.OrderStatus)];
        string?[] actual = [.. result.Column(new OrderTotalColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }
}
