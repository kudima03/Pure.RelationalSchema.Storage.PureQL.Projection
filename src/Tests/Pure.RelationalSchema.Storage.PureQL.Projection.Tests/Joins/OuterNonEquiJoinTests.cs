using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A LEFT join whose non-equi ON condition matches nothing: every left row is
// preserved exactly once (selecting only left-side columns is well-defined).
[Trait("Clause", "Join")]
[Trait("Feature", "OuterNonEquiJoin")]
public sealed class OuterNonEquiJoinTests
{
    [Fact]
    public void LeftJoinWithNonMatchingInequalityPreservesEveryLeftRow()
    {
        SampleDatabase db = new SampleDatabase();

        // user_age (25..42) is never greater than order_total (50..300), so no
        // order matches any user and every user survives the left join once.
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
            [
                new Join(
                    JoinType.Left,
                    SampleDatabase.Orders.Entity,
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachNumberComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Age
                                    )
                                ),
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.Total
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
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        string?[] actual =
        [
            .. result.Column(SampleDatabase.Users.Name).OrderBy(name => name),
        ];

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Equal(expected, actual);
    }
}
