using OneOf;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Helpers;
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

public sealed record PureQLProjectionEachExtendedTests
{
    [Fact]
    public void EachStringComparisonLessThanFiltersBelowThreshold()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachLessThan,
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(new StringScalar("test3"))
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

        // "test0", "test1", "test2" come strictly before "test3"
        Assert.Equal(3, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachStringComparisonGreaterThanOrEqualFiltersInclusively()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachGreaterThanOrEqual,
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(new StringScalar("test7"))
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

        // "test7", "test8", "test9"
        Assert.Equal(3, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachStringComparisonLessThanOrEqualIsInclusive()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachLessThanOrEqual,
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(new StringScalar("test2"))
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

        // "test0", "test1", "test2"
        Assert.Equal(3, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachAndWithSingleConditionIsIdentity()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachAndOperator(
                        [
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test4"
                            ),
                        ]
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
    public void EachAndWithImpossibleConjunctionIsEmpty()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachAndOperator(
                        [
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test1"
                            ),
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test2"
                            ),
                        ]
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
    public void EachOrWithThreeConditionsUnionsAll()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachOrOperator(
                        [
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test1"
                            ),
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test3"
                            ),
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test5"
                            ),
                        ]
                    )
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null
            )
        );

        Assert.Equal(3, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachNotOverEachAndExcludesIntersection()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        BooleanArrayReturning inner = new(
            new EachAndOperator(
                [
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachStringComparison(
                                EachComparisonOperator.EachGreaterThanOrEqual,
                                new StringArrayReturning(
                                    new StringField(s.Entity, s.First.Name.TextValue)
                                ),
                                OneOf<StringReturning, StringArrayReturning>.FromT0(
                                    new StringReturning(new StringScalar("test3"))
                                )
                            )
                        )
                    ),
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachStringComparison(
                                EachComparisonOperator.EachLessThanOrEqual,
                                new StringArrayReturning(
                                    new StringField(s.Entity, s.First.Name.TextValue)
                                ),
                                OneOf<StringReturning, StringArrayReturning>.FromT0(
                                    new StringReturning(new StringScalar("test6"))
                                )
                            )
                        )
                    ),
                ]
            )
        );

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(new EachNotOperator(inner)),
                join: null,
                groupBy: null,
                having: null,
                orderBy: null,
                pagination: null
            )
        );

        // Outside [test3, test6] → test0, test1, test2, test7, test8, test9 = 6 rows
        Assert.Equal(6, result.AsEnumerable().Count());
    }

    [Fact]
    public void EachNotOfEachNotIsIdempotent()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        BooleanArrayReturning innerPredicate = QueryHelpers.EachStringEqualsScalar(
            s.Entity,
            s.First.Name.TextValue,
            "test4"
        );

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachNotOperator(
                        new BooleanArrayReturning(new EachNotOperator(innerPredicate))
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
    public void EachFieldToFieldGreaterThanReturnsEmptyForIdenticalColumns()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachGreaterThan,
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT1(
                                new StringArrayReturning(
                                    new StringField(s.Entity, s.Second.Name.TextValue)
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

        // Both columns hold the same per-row value → no strict-greater rows.
        Assert.Empty(result.AsEnumerable());
    }

    [Fact]
    public void OrderByAscendingExplicitMatchesDefault()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet ascExplicit = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    QueryHelpers.OrderByString(
                        s.Entity,
                        s.First.Name.TextValue,
                        SortDirection.Asc
                    ),
                ],
                pagination: null
            )
        );

        IStoredTableDataSet ascDefault = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [QueryHelpers.OrderByString(s.Entity, s.First.Name.TextValue)],
                pagination: null
            )
        );

        Assert.Equal(
            ascDefault.AsEnumerable().Select(r => r.Cells[s.First].Value.TextValue),
            ascExplicit.AsEnumerable().Select(r => r.Cells[s.First].Value.TextValue)
        );
    }

    [Fact]
    public void OrderByMultipleColumnsWithMixedDirections()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: null,
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    QueryHelpers.OrderByString(
                        s.Entity,
                        s.First.Name.TextValue,
                        SortDirection.Desc
                    ),
                    QueryHelpers.OrderByString(
                        s.Entity,
                        s.Second.Name.TextValue,
                        SortDirection.Asc
                    ),
                ],
                pagination: null
            )
        );

        List<string?> first = [.. result
            .AsEnumerable()
            .Select(r => r.Cells[s.First].Value.TextValue)];

        Assert.Equal(
            first.OrderByDescending(x => x, StringComparer.Ordinal),
            first
        );
    }

    [Fact]
    public void DistinctCombinedWithWhereAndOrderByPreservesUniqueness()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachComparison(
                        new EachStringComparison(
                            EachComparisonOperator.EachGreaterThanOrEqual,
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT0(
                                new StringReturning(new StringScalar("test5"))
                            )
                        )
                    )
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [
                    QueryHelpers.OrderByString(
                        s.Entity,
                        s.First.Name.TextValue,
                        SortDirection.Desc
                    ),
                ],
                pagination: null,
                distinct: true
            )
        );

        List<string?> values = [.. result
            .AsEnumerable()
            .Select(r => r.Cells[s.First].Value.TextValue)];

        Assert.Equal(5, values.Count); // test5..test9
        Assert.Equal(values.Distinct(), values);
        Assert.Equal(
            values.OrderByDescending(x => x, StringComparer.Ordinal),
            values
        );
    }

    [Fact]
    public void EachStringEqualityFieldToFieldFollowedByPagination()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(s.Entity, s.First.Name.TextValue)
                            ),
                            OneOf<StringReturning, StringArrayReturning>.FromT1(
                                new StringArrayReturning(
                                    new StringField(s.Entity, s.Second.Name.TextValue)
                                )
                            )
                        )
                    )
                ),
                join: null,
                groupBy: null,
                having: null,
                orderBy:
                [QueryHelpers.OrderByString(s.Entity, s.First.Name.TextValue)],
                pagination: new Pagination(2, 3)
            )
        );

        List<string?> values = [.. result
            .AsEnumerable()
            .Select(r => r.Cells[s.First].Value.TextValue)];

        Assert.Equal(3, values.Count);
    }

}
