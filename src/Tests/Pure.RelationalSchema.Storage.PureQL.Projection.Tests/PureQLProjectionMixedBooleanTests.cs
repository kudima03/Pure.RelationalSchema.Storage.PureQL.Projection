using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Helpers;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelEquality = PureQL.CSharp.Model.Equality;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionMixedBooleanTests
{
    [Fact]
    public void BooleanScalarTrueAcceptsAllRows()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(new BooleanScalar(true)),
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
    public void BooleanScalarFalseRejectsAllRows()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(new BooleanScalar(false)),
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
    public void AndOperatorOverBooleanArrayConditionsIsDispatched()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(
                    new BooleanOperator(
                        new AndOperator(
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test4"
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
    public void OrOperatorOverBooleanArrayConditionsIsDispatched()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(
                    new BooleanOperator(
                        new OrOperator(
                            QueryHelpers.EachStringEqualsScalar(
                                s.Entity,
                                s.First.Name.TextValue,
                                "test4"
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
    public void NotOperatorInvertsBooleanReturning()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(
                    new BooleanOperator(
                        new NotOperator(new BooleanReturning(new BooleanScalar(false)))
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
    public void SingleValueEqualityOfTwoStringScalarsActsAsConstantPredicate()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        IStoredTableDataSet matchingResult = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(
                    new ModelEquality(
                        new SingleValueEquality(
                            new StringEquality(
                                new StringReturning(new StringScalar("a")),
                                new StringReturning(new StringScalar("a"))
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

        IStoredTableDataSet mismatchedResult = new PureQLProjection(
            [s.Dataset],
            new Query(
                new FromExpression(s.Entity, s.Entity),
                [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                where: new BooleanReturning(
                    new ModelEquality(
                        new SingleValueEquality(
                            new StringEquality(
                                new StringReturning(new StringScalar("a")),
                                new StringReturning(new StringScalar("b"))
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

        Assert.Equal(10, matchingResult.AsEnumerable().Count());
        Assert.Empty(mismatchedResult.AsEnumerable());
    }
}
