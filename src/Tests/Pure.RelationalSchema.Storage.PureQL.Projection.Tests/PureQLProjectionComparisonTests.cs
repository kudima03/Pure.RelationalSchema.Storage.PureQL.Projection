using Pure.HashCodes;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.HashCodes;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using ModelStringComparison = PureQL.CSharp.Model.Comparisons.StringComparison;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionComparisonTests
{
    [Fact]
    public void SelectAllRowsWhenNumberComparisonIsAlwaysTrue()
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
                where: new BooleanReturning(
                    new Comparison(
                        new NumberComparison(
                            ComparisonOperator.GreaterThan,
                            new NumberReturning(new NumberScalar(5.0)),
                            new NumberReturning(new NumberScalar(3.0))
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
    public void SelectNoRowsWhenNumberComparisonIsAlwaysFalse()
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
                where: new BooleanReturning(
                    new Comparison(
                        new NumberComparison(
                            ComparisonOperator.GreaterThan,
                            new NumberReturning(new NumberScalar(1.0)),
                            new NumberReturning(new NumberScalar(3.0))
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
                    Enumerable.Empty<IRow>().Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectAllRowsWhenStringComparisonIsAlwaysTrue()
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
                where: new BooleanReturning(
                    new Comparison(
                        new ModelStringComparison(
                            ComparisonOperator.GreaterThan,
                            new StringReturning(new StringScalar("z")),
                            new StringReturning(new StringScalar("a"))
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
