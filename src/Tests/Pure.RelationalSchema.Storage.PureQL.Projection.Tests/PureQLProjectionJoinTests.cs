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
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionJoinTests
{
    [Fact]
    public void SelectCorrectRowsWithInnerJoin()
    {
        ISchema leftSchema = new FakeSchema();

        ISchema rightSchema = new FakeSchema();

        IStoredSchemaDataSet leftDataset = new FakeStoredSchemaDataset(leftSchema);

        IStoredSchemaDataSet rightDataset = new FakeStoredSchemaDataset(rightSchema);

        ITable leftTable = leftSchema.Tables.First();

        ITable rightTable = rightSchema.Tables.First();

        IColumn leftCol = leftTable.Columns.First();

        IColumn rightCol = rightTable.Columns.First();

        string leftSchemaName = leftSchema.Name.TextValue;

        string leftTableName = leftTable.Name.TextValue;

        string rightSchemaName = rightSchema.Name.TextValue;

        string rightTableName = rightTable.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [leftDataset, rightDataset],
            new Query(
                new FromExpression(
                    $"{leftSchemaName}.{leftTableName}",
                    $"{leftSchemaName}.{leftTableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{leftSchemaName}.{leftTableName}",
                                    leftCol.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Inner,
                        $"{rightSchemaName}.{rightTableName}",
                        new BooleanReturning(
                            new Equality(
                                new ArrayEquality(
                                    new StringArrayEquality(
                                        new StringArrayReturning(
                                            new StringField(
                                                $"{leftSchemaName}.{leftTableName}",
                                                leftCol.Name.TextValue
                                            )
                                        ),
                                        new StringArrayReturning(
                                            new StringField(
                                                $"{rightSchemaName}.{rightTableName}",
                                                rightCol.Name.TextValue
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                ],
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
                    leftDataset[leftTable]
                        .AsEnumerable()
                        .SelectMany(l =>
                            rightDataset[rightTable]
                                .AsEnumerable()
                                .Where(r =>
                                    r.Cells
                                        .First(
                                            kvp =>
                                                kvp.Key.Name.TextValue == rightCol.Name.TextValue
                                        )
                                        .Value.Value.TextValue
                                    == l.Cells
                                        .First(
                                            kvp =>
                                                kvp.Key.Name.TextValue == leftCol.Name.TextValue
                                        )
                                        .Value.Value.TextValue
                                )
                                .Select(_ =>
                                    new Row(
                                        new Collections.Generic.Dictionary<
                                            IColumn,
                                            IColumn,
                                            ICell
                                        >(
                                            [leftCol],
                                            c => c,
                                            c => l.Cells[c],
                                            c => new ColumnHash(c)
                                        )
                                    )
                                )
                        )
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsWithLeftJoinWhenNoRightRowMatches()
    {
        ISchema leftSchema = new FakeSchema();

        ISchema rightSchema = new FakeSchema();

        IStoredSchemaDataSet leftDataset = new FakeStoredSchemaDataset(leftSchema);

        IStoredSchemaDataSet rightDataset = new FakeStoredSchemaDataset(rightSchema);

        ITable leftTable = leftSchema.Tables.First();

        ITable rightTable = rightSchema.Tables.First();

        IColumn leftCol = leftTable.Columns.First();

        string leftSchemaName = leftSchema.Name.TextValue;

        string leftTableName = leftTable.Name.TextValue;

        string rightSchemaName = rightSchema.Name.TextValue;

        string rightTableName = rightTable.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [leftDataset, rightDataset],
            new Query(
                new FromExpression(
                    $"{leftSchemaName}.{leftTableName}",
                    $"{leftSchemaName}.{leftTableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{leftSchemaName}.{leftTableName}",
                                    leftCol.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Left,
                        $"{rightSchemaName}.{rightTableName}",
                        new BooleanReturning(new BooleanScalar(false))
                    ),
                ],
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
                    leftDataset[leftTable]
                        .AsEnumerable()
                        .Select(l =>
                            new Row(
                                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                    [leftCol],
                                    c => c,
                                    c => l.Cells[c],
                                    c => new ColumnHash(c)
                                )
                            )
                        )
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsWithRightJoinWhenNoLeftRowMatches()
    {
        ISchema leftSchema = new FakeSchema();

        ISchema rightSchema = new FakeSchema();

        IStoredSchemaDataSet leftDataset = new FakeStoredSchemaDataset(leftSchema);

        IStoredSchemaDataSet rightDataset = new FakeStoredSchemaDataset(rightSchema);

        ITable leftTable = leftSchema.Tables.First();

        ITable rightTable = rightSchema.Tables.First();

        IColumn rightCol = rightTable.Columns.First();

        string leftSchemaName = leftSchema.Name.TextValue;

        string leftTableName = leftTable.Name.TextValue;

        string rightSchemaName = rightSchema.Name.TextValue;

        string rightTableName = rightTable.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [leftDataset, rightDataset],
            new Query(
                new FromExpression(
                    $"{leftSchemaName}.{leftTableName}",
                    $"{leftSchemaName}.{leftTableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{rightSchemaName}.{rightTableName}",
                                    rightCol.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Right,
                        $"{rightSchemaName}.{rightTableName}",
                        new BooleanReturning(new BooleanScalar(false))
                    ),
                ],
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
                    rightDataset[rightTable]
                        .AsEnumerable()
                        .Select(r =>
                            new Row(
                                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                                    [rightCol],
                                    c => c,
                                    c => r.Cells[c],
                                    c => new ColumnHash(c)
                                )
                            )
                        )
                        .Select(x => new RowHash(x))
                )
            )
        );
    }

    [Fact]
    public void SelectCorrectRowsWithFullJoin()
    {
        ISchema leftSchema = new FakeSchema();

        ISchema rightSchema = new FakeSchema();

        IStoredSchemaDataSet leftDataset = new FakeStoredSchemaDataset(leftSchema);

        IStoredSchemaDataSet rightDataset = new FakeStoredSchemaDataset(rightSchema);

        ITable leftTable = leftSchema.Tables.First();

        ITable rightTable = rightSchema.Tables.First();

        IColumn leftCol = leftTable.Columns.First();

        IColumn rightCol = rightTable.Columns.First();

        string leftSchemaName = leftSchema.Name.TextValue;

        string leftTableName = leftTable.Name.TextValue;

        string rightSchemaName = rightSchema.Name.TextValue;

        string rightTableName = rightTable.Name.TextValue;

        IStoredTableDataSet result = new PureQLProjection(
            [leftDataset, rightDataset],
            new Query(
                new FromExpression(
                    $"{leftSchemaName}.{leftTableName}",
                    $"{leftSchemaName}.{leftTableName}"
                ),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    $"{leftSchemaName}.{leftTableName}",
                                    leftCol.Name.TextValue
                                )
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Full,
                        $"{rightSchemaName}.{rightTableName}",
                        new BooleanReturning(
                            new Equality(
                                new ArrayEquality(
                                    new StringArrayEquality(
                                        new StringArrayReturning(
                                            new StringField(
                                                $"{leftSchemaName}.{leftTableName}",
                                                leftCol.Name.TextValue
                                            )
                                        ),
                                        new StringArrayReturning(
                                            new StringField(
                                                $"{rightSchemaName}.{rightTableName}",
                                                rightCol.Name.TextValue
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                ],
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
                    leftDataset[leftTable]
                        .AsEnumerable()
                        .SelectMany(l =>
                            rightDataset[rightTable]
                                .AsEnumerable()
                                .Where(r =>
                                    r.Cells
                                        .First(
                                            kvp =>
                                                kvp.Key.Name.TextValue == rightCol.Name.TextValue
                                        )
                                        .Value.Value.TextValue
                                    == l.Cells
                                        .First(
                                            kvp =>
                                                kvp.Key.Name.TextValue == leftCol.Name.TextValue
                                        )
                                        .Value.Value.TextValue
                                )
                                .Select(_ =>
                                    new Row(
                                        new Collections.Generic.Dictionary<
                                            IColumn,
                                            IColumn,
                                            ICell
                                        >(
                                            [leftCol],
                                            c => c,
                                            c => l.Cells[c],
                                            c => new ColumnHash(c)
                                        )
                                    )
                                )
                        )
                        .Select(x => new RowHash(x))
                )
            )
        );
    }
}
