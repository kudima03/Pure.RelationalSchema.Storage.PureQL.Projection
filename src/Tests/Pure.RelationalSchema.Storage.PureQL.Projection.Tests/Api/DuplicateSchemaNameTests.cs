using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// PureQLProjection accepts an open IEnumerable<IStoredSchemaDataSet>, and
// nothing forbids two datasets from sharing a schema name (a host that
// concatenates datasets from several sources produces exactly this shape).
// Entity resolution must find a "schema.table" pair wherever it lives, but
// today only the first dataset with a matching schema name is consulted
// (issue #84).
[Trait("Clause", "From")]
[Trait("Feature", "EntityResolution")]
public sealed class DuplicateSchemaNameTests
{
    private const string SchemaName = "dup";
    private const string AlphaValue = "alpha-value";
    private const string BetaValue = "beta-value";

    private static IStoredSchemaDataSet SingleTableDataset(
        string tableName,
        string columnName,
        string cellText
    )
    {
        IColumn column = new Column.Column(
            new String(columnName),
            new StringColumnType()
        );

        ITable table = new Table.Table(new String(tableName), [column], []);

        IRow row = new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                [column],
                c => c,
                _ => new Cell(new String(cellText)),
                c => new ColumnHash(c)
            )
        );

        ISchema schema = new Schema.Schema(new String(SchemaName), [table], []);

        IReadOnlyDictionary<ITable, IStoredTableDataSet> datasetsByTable =
            new Collections.Generic.Dictionary<
                IStoredTableDataSet,
                ITable,
                IStoredTableDataSet
            >(
                [new SampleTableDataset(table, [row])],
                dataset => dataset.TableSchema,
                dataset => dataset,
                t => new TableHash(t)
            );

        return new StoredSchemaDataset(schema, datasetsByTable);
    }

    [Fact(
        Skip = "Issue #84: entity resolution consults only the first schema "
            + "dataset with a matching name, so a from table that lives in a "
            + "later same-named dataset throws InvalidOperationException "
            + "instead of resolving."
    )]
    [Trait("Status", "KnownGap")]
    public void FromTableInALaterSameNamedSchemaDatasetResolves()
    {
        IStoredSchemaDataSet first = SingleTableDataset(
            "alpha",
            "alpha_name",
            AlphaValue
        );
        IStoredSchemaDataSet second = SingleTableDataset(
            "beta",
            "beta_name",
            BetaValue
        );

        Query query = new Query(
            new FromExpression($"{SchemaName}.beta"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField($"{SchemaName}.beta", "beta_name")
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection([first, second], query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(BetaValue, result.Row(0)["beta_name"]);
    }

    [Fact(
        Skip = "Issue #84: join resolution consults only the first schema "
            + "dataset with a matching name, so a join table that lives in a "
            + "later same-named dataset throws InvalidOperationException "
            + "instead of resolving."
    )]
    [Trait("Status", "KnownGap")]
    public void JoinTableInALaterSameNamedSchemaDatasetResolves()
    {
        IStoredSchemaDataSet first = SingleTableDataset(
            "alpha",
            "alpha_name",
            AlphaValue
        );
        IStoredSchemaDataSet second = SingleTableDataset(
            "beta",
            "beta_name",
            BetaValue
        );

        Query query = new Query(
            new FromExpression($"{SchemaName}.alpha"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField($"{SchemaName}.beta", "beta_name")
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    $"{SchemaName}.beta",
                    new BooleanReturning(new BooleanScalar(true))
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection([first, second], query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(BetaValue, result.Row(0)["beta_name"]);
    }
}
