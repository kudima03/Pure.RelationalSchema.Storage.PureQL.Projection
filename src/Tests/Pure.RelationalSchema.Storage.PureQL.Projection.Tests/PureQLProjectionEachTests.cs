using OneOf;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionEachTests
{
    private static (
        ISchema schema,
        IStoredSchemaDataSet dataset,
        ITable table,
        IColumn first,
        IColumn second,
        string schemaName,
        string tableName,
        string entity
    ) Setup()
    {
        ISchema schema = new FakeSchema();
        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);
        ITable table = schema.Tables.First();
        IColumn first = table.Columns.First();
        IColumn second = table.Columns.Skip(1).First();
        string schemaName = schema.Name.TextValue;
        string tableName = table.Name.TextValue;
        return (
            schema,
            dataset,
            table,
            first,
            second,
            schemaName,
            tableName,
            $"{schemaName}.{tableName}"
        );
    }

    [Fact]
    public void EachStringEqualityFiltersMatchingRows()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(
                                    new StringScalar("test5")
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

        _ = Assert.Single(result.AsEnumerable());
    }

    [Fact]
    public void EachNotInvertsMatch()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachNotOperator(
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            s.entity,
                                            s.first.Name.TextValue
                                        )
                                    ),
                                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                                        new StringReturning(
                                            new StringScalar("test5")
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

        Assert.Equal(9, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachAndCombinesPredicates()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        BooleanArrayReturning leftPredicate = new(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(s.entity, s.first.Name.TextValue)
                    ),
                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                        new StringReturning(new StringScalar("test5"))
                    )
                )
            )
        );

        BooleanArrayReturning rightPredicate = new(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(s.entity, s.second.Name.TextValue)
                    ),
                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                        new StringReturning(new StringScalar("test5"))
                    )
                )
            )
        );

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachAndOperator([leftPredicate, rightPredicate])
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null
            )
        );

        _ = Assert.Single(result.AsEnumerable());
    }

    [Fact]
    public void EachOrUnionsMatches()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        BooleanArrayReturning leftPredicate = new(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(s.entity, s.first.Name.TextValue)
                    ),
                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                        new StringReturning(new StringScalar("test1"))
                    )
                )
            )
        );

        BooleanArrayReturning rightPredicate = new(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(s.entity, s.first.Name.TextValue)
                    ),
                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                        new StringReturning(new StringScalar("test2"))
                    )
                )
            )
        );

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachOrOperator([leftPredicate, rightPredicate])
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null
            )
        );

        Assert.Equal(2, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachStringComparisonGreaterThanOrdersByText()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachGreaterThan,
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(
                                    new StringScalar("test7")
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

        Assert.Equal(2, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachWithEmptyDatasetReturnsEmpty()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(
                                    new StringScalar("does-not-exist")
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

        Assert.Empty(result.AsEnumerable());
    }

    [Fact]
    public void EachFieldToFieldEqualityReturnsAllRows()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT1(
                                new StringArrayReturning(
                                    new StringField(s.entity, s.second.Name.TextValue)
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

        Assert.Equal(10, result.AsEnumerable().Count());
    }

    [Fact]
    public void OrderByDescendingReversesOrder()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    new OrderByItem(
                        new Field(
                            new StringField(s.entity, s.first.Name.TextValue)
                        ),
                        SortDirection.Desc
                    ),
                ],
                pagination: null
            )
        );

        IEnumerable<string?> expected = s
            .dataset[s.table]
            .AsEnumerable()
            .Select(r => r.Cells[s.first].Value.TextValue)
            .OrderByDescending(x => x, StringComparer.Ordinal);

        Assert.Equal(
            expected,
            result.AsEnumerable().Select(r => r.Cells[s.first].Value.TextValue)
        );
    }

    [Fact]
    public void DistinctRemovesDuplicateRows()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(
                                    new StringScalar("test5")
                                )
                            )
                        )
                    )
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null,
                distinct: true
            )
        );

        _ = Assert.Single(result.AsEnumerable());
    }

    [Fact]
    public void DistinctIsIdentityWhenAllRowsAlreadyDistinct()
    {
        (ISchema schema, IStoredSchemaDataSet dataset, ITable table, IColumn first, IColumn second, string schemaName, string tableName, string entity) s = Setup();

        IStoredTableDataSet result = new PureQLProjection(
            [s.dataset],
            new Query(
                new FromExpression(s.entity, s.entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.entity, s.first.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null,
                distinct: true
            )
        );

        Assert.Equal(10, result.AsEnumerable().Count());
    }

}
