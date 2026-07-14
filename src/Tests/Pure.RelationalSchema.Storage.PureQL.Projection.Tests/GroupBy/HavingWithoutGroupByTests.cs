using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// HAVING with no groupBy is schema-valid (SQL-style implicit whole-set
// group). It is honoured today only when the select list contains an
// aggregate (which engages group mode); with plain field selects the clause
// is silently dropped (issue #83).
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingWithoutGroupByTests
{
    private static BooleanReturning UserCountComparedTo(
        ComparisonOperator comparisonOperator,
        double threshold
    )
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    comparisonOperator,
                    new NumberReturning(
                        new Count(
                            new ArrayReturning(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
                                    )
                                )
                            )
                        )
                    ),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static SelectExpression CountOfUserIds(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(
                new NumberReturning(
                    new Count(
                        new ArrayReturning(
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Id
                                )
                            )
                        )
                    )
                )
            ),
            alias
        );
    }

    [Fact(
        Skip = "Issue #83: with no groupBy and no aggregate select the query "
            + "never enters group mode, so HAVING is silently dropped and a "
            + "constant-false condition still returns every row."
    )]
    [Trait("Status", "KnownGap")]
    public void HavingWithoutGroupByFiltersTheImplicitWholeSetGroup()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            new BooleanReturning(new BooleanScalar(false)),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void WholeSetHavingKeepsTheSingleGroupWhenSatisfied()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [CountOfUserIds("userCount")],
            where: null,
            join: null,
            groupBy: null,
            UserCountComparedTo(
                ComparisonOperator.GreaterThanOrEqual,
                db.UserRows.Count
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.UserRows.Count, result.Row(0).Double("userCount"));
    }

    [Fact]
    public void WholeSetHavingRemovesTheSingleGroupWhenUnsatisfied()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [CountOfUserIds("userCount")],
            where: null,
            join: null,
            groupBy: null,
            UserCountComparedTo(
                ComparisonOperator.GreaterThan,
                db.UserRows.Count
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
