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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionTests
{
    [Fact]
    public void SelectSingleColumn()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IColumn columnToSelect = tableToSelect.Columns.First();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

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
                                    columnToSelect.Name.TextValue
                                )
                            )
                        )
                    ),
                ]
            )
        );

        Assert.True(
            result.All(x =>
                new ColumnHash(x.Cells.Keys.Single()).SequenceEqual(
                    new ColumnHash(columnToSelect)
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsOnSingleColumn()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IColumn columnToSelect = tableToSelect.Columns.First();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

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
                                    columnToSelect.Name.TextValue
                                )
                            )
                        )
                    ),
                ]
            )
        );

        Assert.True(
            new DeterminedHash(
                result.AsEnumerable().Select(x => new RowHash(x))
            ).SequenceEqual(
                new DeterminedHash(
                    dataset[tableToSelect]
                        .AsEnumerable()
                        .Select(x => new Row(
                            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                [columnToSelect],
                                c => columnToSelect,
                                c => x.Cells[c],
                                c => new ColumnHash(c)
                            )
                        ))
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectMultipleColumns()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [dataset],
            new Query(
                new FromExpression(
                    $"{schemaName}.{tableName}",
                    $"{schemaName}.{tableName}"
                ),
                columnsToSelect.Select(x => new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField($"{schemaName}.{tableName}", x.Name.TextValue)
                        )
                    )
                ))
            )
        );

        Assert.True(
            result.All(x =>
                new DeterminedHash(
                    x.Cells.Keys.Select(c => new ColumnHash(c))
                ).SequenceEqual(
                    new DeterminedHash(columnsToSelect.Select(c => new ColumnHash(c)))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsOnMultipleColumns()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [dataset],
            new Query(
                new FromExpression(
                    $"{schemaName}.{tableName}",
                    $"{schemaName}.{tableName}"
                ),
                columnsToSelect.Select(x => new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField($"{schemaName}.{tableName}", x.Name.TextValue)
                        )
                    )
                ))
            )
        );

        Assert.True(
            new DeterminedHash(
                result.AsEnumerable().Select(x => new RowHash(x))
            ).SequenceEqual(
                new DeterminedHash(
                    dataset[tableToSelect]
                        .AsEnumerable()
                        .Select(x => new Row(
                            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                columnsToSelect,
                                c => c,
                                c => x.Cells[c],
                                c => new ColumnHash(c)
                            )
                        ))
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsOnMultipleColumnsWithFilter()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns.Take(2);

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const string valueToFilter = "test5";

        IStoredTableDataSet result = new PureQLProjection(
            [dataset],
            new Query(
                new FromExpression(
                    $"{schemaName}.{tableName}",
                    $"{schemaName}.{tableName}"
                ),
                columnsToSelect.Select(x => new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField($"{schemaName}.{tableName}", x.Name.TextValue)
                        )
                    )
                )),
                where: new BooleanReturning(
                    new Equality(
                        new ArrayEquality(
                            new StringArrayEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        $"{schemaName}.{tableName}",
                                        columnsToSelect.First().Name.TextValue
                                    )
                                ),
                                new StringArrayReturning(
                                    new StringArrayScalar([valueToFilter])
                                )
                            )
                        )
                    )
                ),
                join: null,
                groupBy: null,
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
                    dataset[tableToSelect]
                        .AsEnumerable()
                        .Where(x =>
                            x.Cells[columnsToSelect.First()].Value.TextValue
                            == valueToFilter
                        )
                        .Select(x => new Row(
                            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                columnsToSelect,
                                c => c,
                                c => x.Cells[c],
                                c => new ColumnHash(c)
                            )
                        ))
                        .Select(x => new RowHash(x))
                )
            )
        );
    }
}
