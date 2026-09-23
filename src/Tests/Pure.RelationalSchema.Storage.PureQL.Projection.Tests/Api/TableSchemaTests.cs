using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// PureQLProjection.TableSchema is the derived output schema. Its columns follow
// the select expressions: one column per expression, named by the select alias
// and typed by the expression's value type.
[Trait("Clause", "Select")]
[Trait("Feature", "TableSchema")]
public sealed class TableSchemaTests
{
    [Fact]
    public void TableSchemaColumnsFollowTheAliasedSelectExpressions()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new AliasedOrderColumnsQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["oid", "state", "amount"],
            [.. columns.Select(column => column.Name.TextValue)]
        );
        Assert.Equal(
            ["uuid", "string", "double"],
            [.. columns.Select(column => column.Type.Name.TextValue)]
        );
    }

    [Fact]
    public void TableSchemaColumnForAggregateFollowsAliasAndType()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new AliasedAggregatesOfEveryTypeQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["totalSum", "maxStatus", "maxPlacedOn", "minPlacedAt", "maxShiftStart"],
            [.. columns.Select(column => column.Name.TextValue)]
        );
        Assert.Equal(
            ["double", "string", "date", "datetime", "time"],
            [.. columns.Select(column => column.Type.Name.TextValue)]
        );
    }

    [Fact]
    public void TableSchemaWithoutAliasesFallsBackToFieldNames()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new UnaliasedOrderColumnsQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            [new OrderIdColumn().Name.TextValue, new OrderStatusColumn().Name.TextValue],
            [.. columns.Select(column => column.Name.TextValue)]
        );

        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [.. columns.Select(column => column.Name.TextValue)],
            result.ColumnNames
        );
    }
}
