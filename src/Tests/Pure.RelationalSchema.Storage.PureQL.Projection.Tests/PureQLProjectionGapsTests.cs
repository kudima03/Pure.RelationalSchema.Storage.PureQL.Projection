using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Helpers;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelEquality = PureQL.CSharp.Model.Equality;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

// Tests for the documented execution gaps in EXECUTION_GAPS.md.
// Each gap is verified to either throw NotSupportedException loudly
// (so callers can detect it) or to be skipped with a reference to the
// gap entry that tracks the work needed to enable the test.
public sealed record PureQLProjectionGapsTests
{
    [Fact]
    public void StringParameterInWhereThrowsNotSupported()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(
                [s.Dataset],
                new Query(
                    new FromExpression(s.Entity, s.Entity),
                    [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                    where: new BooleanReturning(
                        new ModelEquality(
                            new SingleValueEquality(
                                new StringEquality(
                                    new StringReturning(new StringParameter("p")),
                                    new StringReturning(new StringScalar("test1"))
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
            )
        );
    }

    [Fact]
    public void NumberAggregateInsideHavingThrowsNotSupported()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(
                [s.Dataset],
                new Query(
                    new FromExpression(s.Entity, s.Entity),
                    [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                    where: null,
                    join: null,
                    groupBy:
                    [new Field(new StringField(s.Entity, s.First.Name.TextValue))],
                    having: new BooleanReturning(
                        new Comparison(
                            new NumberComparison(
                                ComparisonOperator.GreaterThan,
                                new NumberReturning(
                                    new NumberAggregate(
                                        new SumNumber(
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    s.Entity,
                                                    s.First.Name.TextValue
                                                )
                                            )
                                        )
                                    )
                                ),
                                new NumberReturning(new NumberScalar(0))
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null
                )
            )
        );
    }

    [Fact]
    public void CountInsideHavingThrowsNotSupported()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(
                [s.Dataset],
                new Query(
                    new FromExpression(s.Entity, s.Entity),
                    [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                    where: null,
                    join: null,
                    groupBy:
                    [new Field(new StringField(s.Entity, s.First.Name.TextValue))],
                    having: new BooleanReturning(
                        new Comparison(
                            new NumberComparison(
                                ComparisonOperator.GreaterThan,
                                new NumberReturning(
                                    new Count(
                                        new ArrayReturning(
                                            new StringArrayReturning(
                                                new StringField(
                                                    s.Entity,
                                                    s.First.Name.TextValue
                                                )
                                            )
                                        )
                                    )
                                ),
                                new NumberReturning(new NumberScalar(1))
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null
                )
            )
        );
    }

    [Fact]
    public void SingleValueArithmeticInsideWhereThrowsNotSupported()
    {
        TestEnvironment s = QueryHelpers.NewEnv();

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(
                [s.Dataset],
                new Query(
                    new FromExpression(s.Entity, s.Entity),
                    [QueryHelpers.SelectStringField(s.Entity, s.First.Name.TextValue)],
                    where: new BooleanReturning(
                        new ModelEquality(
                            new SingleValueEquality(
                                new NumberEquality(
                                    new NumberReturning(
                                        new Arithmetic(
                                            new Add(
                                                [
                                                    new NumberReturning(
                                                        new NumberScalar(1)
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(2)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(3))
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
            )
        );
    }

#pragma warning disable xUnit1004 // Pending execution gaps — see EXECUTION_GAPS.md
    [Fact(Skip = "Computed-column select projections are tracked as Gap 4 "
        + "in EXECUTION_GAPS.md (sample 17_each_arithmetic_select.json).")]
    public void ComputedSelectExpressionShouldProjectAsSyntheticColumn()
    {
        Assert.Fail("Pending Gap 4");
    }

    [Fact(Skip = "Real aggregate folding (sum/min/max/avg/count) inside "
        + "groupBy projection is tracked as Gap 3 in EXECUTION_GAPS.md "
        + "(samples 06_count_aggregate.json, 07_group_by.json).")]
    public void AggregateProjectionShouldFoldGroupRows()
    {
        Assert.Fail("Pending Gap 3");
    }

    [Fact(Skip = "Parameter binding throughout the executor is tracked "
        + "as Gap 1 in EXECUTION_GAPS.md (samples 10_parameters.json, "
        + "12_complex_query.json).")]
    public void ParameterBoundWhereShouldFilterByBoundValue()
    {
        Assert.Fail("Pending Gap 1");
    }

    [Fact(Skip = "NullField projection is tracked as Gap 5 in "
        + "EXECUTION_GAPS.md; depends on Gap 4 being closed first.")]
    public void NullFieldSelectShouldProduceAllNullColumn()
    {
        Assert.Fail("Pending Gap 5 (blocked by Gap 4)");
    }
#pragma warning restore xUnit1004
}
