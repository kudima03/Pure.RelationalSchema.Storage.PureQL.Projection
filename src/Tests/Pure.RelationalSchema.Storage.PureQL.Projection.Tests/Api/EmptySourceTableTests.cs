using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Samples.Queries.Select;

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
    [Fact]
    public void ProjectionOverATableWithoutRowsYieldsNoRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                [dataset],
                new SelectIdAndNameFromTableWithoutIndexesQuery().Value
            )
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void TableSchemaStillFollowsTheSelectExpressionsWithoutRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        PureQLProjection projection = new PureQLProjection(
            [dataset],
            new SelectIdAndNameFromTableWithoutIndexesQuery().Value
        );

        Assert.Equal(
            [new IdColumn().Name.TextValue, new NameColumn().Name.TextValue],
            projection.TableSchema.Columns.Select(column => column.Name.TextValue)
        );
    }

    [Fact]
    public async Task AsyncEnumerationOverATableWithoutRowsYieldsNoRows()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithoutRows();

        PureQLProjection projection = new PureQLProjection(
            [dataset],
            new SelectIdAndNameFromTableWithoutIndexesQuery().Value
        );

        List<IRow> rows = [];

        await foreach (IRow row in projection)
        {
            rows.Add(row);
        }

        Assert.Empty(rows);
    }
}
