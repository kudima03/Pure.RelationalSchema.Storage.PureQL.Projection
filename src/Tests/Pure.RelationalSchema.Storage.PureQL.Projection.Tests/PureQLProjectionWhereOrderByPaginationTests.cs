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
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQlProjectionWhereOrderByPaginationTests
{
    [Fact]
    public void SelectCorrectRowsWithAndFilter()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

        IColumn lastColumn = columnsToSelect.Last();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const string firstColumnValue = "test3";

        const string secondColumnValue = "test3";

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
                    new BooleanOperator(
                        new AndOperator([
                            new BooleanReturning(
                                new Equality(
                                    new ArrayEquality(
                                        new StringArrayEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    $"{schemaName}.{tableName}",
                                                    firstColumn.Name.TextValue
                                                )
                                            ),
                                            new StringArrayReturning(
                                                new StringArrayScalar([firstColumnValue])
                                            )
                                        )
                                    )
                                )
                            ),
                            new BooleanReturning(
                                new Equality(
                                    new ArrayEquality(
                                        new StringArrayEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    $"{schemaName}.{tableName}",
                                                    lastColumn.Name.TextValue
                                                )
                                            ),
                                            new StringArrayReturning(
                                                new StringArrayScalar([secondColumnValue])
                                            )
                                        )
                                    )
                                )
                            ),
                        ])
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
                            x.Cells[firstColumn].Value.TextValue == firstColumnValue
                            && x.Cells[lastColumn].Value.TextValue == secondColumnValue
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

    [Fact]
    public void SelectCorrectRowsWithOrFilter()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const string firstValue = "test3";

        const string secondValue = "test5";

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
                    new BooleanOperator(
                        new OrOperator([
                            new BooleanReturning(
                                new Equality(
                                    new ArrayEquality(
                                        new StringArrayEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    $"{schemaName}.{tableName}",
                                                    firstColumn.Name.TextValue
                                                )
                                            ),
                                            new StringArrayReturning(
                                                new StringArrayScalar([firstValue])
                                            )
                                        )
                                    )
                                )
                            ),
                            new BooleanReturning(
                                new Equality(
                                    new ArrayEquality(
                                        new StringArrayEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    $"{schemaName}.{tableName}",
                                                    firstColumn.Name.TextValue
                                                )
                                            ),
                                            new StringArrayReturning(
                                                new StringArrayScalar([secondValue])
                                            )
                                        )
                                    )
                                )
                            ),
                        ])
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
                            x.Cells[firstColumn].Value.TextValue
                                is firstValue
                                    or secondValue
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

    [Fact]
    public void SelectCorrectRowsWithNotFilter()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const string valueToExclude = "test5";

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
                    new BooleanOperator(
                        new NotOperator(
                            new BooleanReturning(
                                new Equality(
                                    new ArrayEquality(
                                        new StringArrayEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    $"{schemaName}.{tableName}",
                                                    firstColumn.Name.TextValue
                                                )
                                            ),
                                            new StringArrayReturning(
                                                new StringArrayScalar([valueToExclude])
                                            )
                                        )
                                    )
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
                            x.Cells[firstColumn].Value.TextValue != valueToExclude
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

    [Fact]
    public void SelectAllRowsWithConstantTrueFilter()
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
                )),
                where: new BooleanReturning(new BooleanScalar(true)),
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
    public void SelectNoRowsWithConstantFalseFilter()
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
                )),
                where: new BooleanReturning(new BooleanScalar(false)),
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
            ).SequenceEqual(new DeterminedHash(Enumerable.Empty<RowHash>()))
        );
    }

    [Fact]
    public void SelectCorrectRowsOrderedByFirstColumn()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

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
                )),
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    new Field(
                        new StringField(
                            $"{schemaName}.{tableName}",
                            firstColumn.Name.TextValue
                        )
                    ),
                ],
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
                        .OrderBy(x => x.Cells[firstColumn].Value.TextValue)
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
    public void SelectCorrectRowsOrderedByMultipleColumns()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

        IColumn lastColumn = columnsToSelect.Last();

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
                )),
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy: columnsToSelect.Select(x => new Field(
                    new StringField($"{schemaName}.{tableName}", x.Name.TextValue)
                )),
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
                        .OrderBy(x => x.Cells[firstColumn].Value.TextValue)
                        .ThenBy(x => x.Cells[lastColumn].Value.TextValue)
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
    public void SelectCorrectRowsWithPagination()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        const int skip = 2;

        const int take = 3;

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
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: new Pagination(skip, take)
            )
        );

        Assert.True(
            new DeterminedHash(
                result.AsEnumerable().Select(x => new RowHash(x))
            ).SequenceEqual(
                new DeterminedHash(
                    dataset[tableToSelect]
                        .AsEnumerable()
                        .Skip(skip)
                        .Take(take)
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
    public void SelectCorrectRowsWithFilterAndOrderByAndPagination()
    {
        ISchema schema = new FakeSchema();

        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);

        ITable tableToSelect = schema.Tables.First();

        IEnumerable<IColumn> columnsToSelect = tableToSelect.Columns;

        IColumn firstColumn = columnsToSelect.First();

        string schemaName = schema.Name.TextValue;

        string tableName = tableToSelect.Name.TextValue;

        IEnumerable<string> valuesToFilter =
        [
            "test1",
            "test2",
            "test3",
            "test4",
            "test5",
        ];

        const int skip = 1;

        const int take = 2;

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
                                        firstColumn.Name.TextValue
                                    )
                                ),
                                new StringArrayReturning(
                                    new StringArrayScalar(valuesToFilter)
                                )
                            )
                        )
                    )
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    new Field(
                        new StringField(
                            $"{schemaName}.{tableName}",
                            firstColumn.Name.TextValue
                        )
                    ),
                ],
                pagination: new Pagination(skip, take)
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
                            valuesToFilter.Contains(x.Cells[firstColumn].Value.TextValue)
                        )
                        .OrderBy(x => x.Cells[firstColumn].Value.TextValue)
                        .Skip(skip)
                        .Take(take)
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
