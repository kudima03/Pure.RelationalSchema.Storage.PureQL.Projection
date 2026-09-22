using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Every other suite filters a populated table down to nothing; none of them
// projects over a source table that holds no rows at all. That shape comes
// from the centralized Pure.RelationalSchema.Storage.Samples catalogue:
// SchemaDataSetWithoutRows is the schema "schema_without_foreign_keys" whose
// tables all have a real column list and an empty row set. The derived output
// schema still has to follow the select expressions even though nothing is
// projected through it.
[Trait("Clause", "Select")]
[Trait("Feature", "EmptySource")]
public sealed class EmptySourceTableTests
{
    private const string Entity = "schema_without_foreign_keys.table_without_indexes";
    private const string IdField = "id";
    private const string NameField = "name";

    private static Query TwoColumnQuery()
    {
        return new Query(
            new FromExpression(Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(new UuidField(Entity, IdField))
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(new StringField(Entity, NameField))
                    )
                ),
            ]
        );
    }

    [Fact]
    public void ProjectionOverATableWithoutRowsYieldsNoRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection([dataset], TwoColumnQuery())
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void TableSchemaStillFollowsTheSelectExpressionsWithoutRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        PureQLProjection projection = new PureQLProjection(
            [dataset],
            TwoColumnQuery()
        );

        Assert.Equal(
            [IdField, NameField],
            projection.TableSchema.Columns.Select(column => column.Name.TextValue)
        );
    }

    [Fact]
    public async Task AsyncEnumerationOverATableWithoutRowsYieldsNoRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        PureQLProjection projection = new PureQLProjection(
            [dataset],
            TwoColumnQuery()
        );

        List<IRow> rows = [];

        await foreach (IRow row in projection)
        {
            rows.Add(row);
        }

        Assert.Empty(rows);
    }
}
