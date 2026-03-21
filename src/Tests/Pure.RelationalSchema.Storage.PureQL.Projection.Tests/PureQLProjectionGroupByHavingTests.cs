using Pure.HashCodes;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.HashCodes;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using Query = PureQL.CSharp.Model.Query;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionGroupByHavingTests
{
    [Fact]
    public void SelectFirstRowPerGroupWhenGroupingBySingleColumn()
    {
        ISchema schema = new FakeSchema();

        ITable tableToSelect = schema.Tables.First();

        IColumn col1 = tableToSelect.Columns.First();

        IColumn col2 = tableToSelect.Columns.Last();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        IRow[] rows =
        [
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String("A"))
                            : new Cell(new String("x")),
                    c => new ColumnHash(c)
                )
            ),
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String("A"))
                            : new Cell(new String("y")),
                    c => new ColumnHash(c)
                )
            ),
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String("B"))
                            : new Cell(new String("z")),
                    c => new ColumnHash(c)
                )
            ),
        ];

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(
            schema,
            [new FakeStoredTableDataset(tableToSelect, rows.AsQueryable())]
        );

        IStoredTableDataSet result = new PureQLProjection(
            [dataset],
            new Query(
                new FromExpression(
                    $"{schemaName}.{tableName}",
                    $"{schemaName}.{tableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{schemaName}.{tableName}",
                                    col1.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join: null,
                groupBy:
                [
                    new Field(
                        new StringField($"{schemaName}.{tableName}", col1.Name.TextValue)
                    ),
                ],
                having: null,
                orderBy: null,
                pagination: null
            )
        );

        Assert.True(
            new DeterminedHash(
                result.AsEnumerable().Select(x => new RowHash(x))
            ).SequenceEqual(
                new DeterminedHash(
                    rows.GroupBy(r =>
                            r.Cells.First(kvp =>
                                kvp.Key.Name.TextValue == col1.Name.TextValue
                            ).Value.Value.TextValue
                        )
                        .Select(g => new Row(
                            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                [col1],
                                c => c,
                                c => g.First().Cells[c],
                                c => new ColumnHash(c)
                            )
                        ))
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectGroupsWhenHavingFilterIsApplied()
    {
        ISchema schema = new FakeSchema();

        ITable tableToSelect = schema.Tables.First();

        IColumn col1 = tableToSelect.Columns.First();

        IColumn col2 = tableToSelect.Columns.Last();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const string passingGroupValue = "A";

        const string passingFirstRowSecondColValue = "x";

        IRow[] rows =
        [
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String(passingGroupValue))
                            : new Cell(new String(passingFirstRowSecondColValue)),
                    c => new ColumnHash(c)
                )
            ),
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String(passingGroupValue))
                            : new Cell(new String("y")),
                    c => new ColumnHash(c)
                )
            ),
            new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    [col1, col2],
                    c => c,
                    c =>
                        c.Name.TextValue == col1.Name.TextValue
                            ? new Cell(new String("B"))
                            : new Cell(new String("z")),
                    c => new ColumnHash(c)
                )
            ),
        ];

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(
            schema,
            [new FakeStoredTableDataset(tableToSelect, rows.AsQueryable())]
        );

        IStoredTableDataSet result = new PureQLProjection(
            [dataset],
            new Query(
                new FromExpression(
                    $"{schemaName}.{tableName}",
                    $"{schemaName}.{tableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{schemaName}.{tableName}",
                                    col1.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join: null,
                groupBy:
                [
                    new Field(
                        new StringField($"{schemaName}.{tableName}", col1.Name.TextValue)
                    ),
                ],
                having: new BooleanReturning(
                    new Equality(
                        new ArrayEquality(
                            new StringArrayEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        $"{schemaName}.{tableName}",
                                        col2.Name.TextValue
                                    )
                                ),
                                new StringArrayReturning(
                                    new StringArrayScalar([passingFirstRowSecondColValue])
                                )
                            )
                        )
                    )
                ),
                orderBy: null,
                pagination: null
            )
        );

        Assert.True(
            new DeterminedHash(
                result.AsEnumerable().Select(x => new RowHash(x))
            ).SequenceEqual(
                new DeterminedHash(
                    rows.GroupBy(r =>
                            r.Cells.First(kvp =>
                                kvp.Key.Name.TextValue == col1.Name.TextValue
                            ).Value.Value.TextValue
                        )
                        .Where(g =>
                            g.First()
                                .Cells.First(kvp =>
                                    kvp.Key.Name.TextValue == col2.Name.TextValue
                                )
                                .Value.Value.TextValue == passingFirstRowSecondColValue
                        )
                        .Select(g => new Row(
                            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                [col1],
                                c => c,
                                c => g.First().Cells[c],
                                c => new ColumnHash(c)
                            )
                        ))
                        .Select(x => new RowHash(x))
                )
            )
        );
    }
}
