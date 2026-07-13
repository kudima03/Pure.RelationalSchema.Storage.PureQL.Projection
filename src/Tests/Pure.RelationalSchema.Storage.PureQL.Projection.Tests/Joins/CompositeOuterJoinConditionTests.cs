using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Composite per-row on conditions (and / or / not around equalities and
// comparisons) on inner and outer joins. Expected row sets are computed
// pair-by-pair from the ground-truth records.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeCondition")]
public sealed class CompositeOuterJoinConditionTests
{
    private static BooleanArrayReturning UserKeyMatch()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachUuidEquality(
                    new UuidArrayReturning(
                        new UuidField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Id
                        )
                    ),
                    new UuidArrayReturning(
                        new UuidField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.UserId
                        )
                    )
                )
            )
        );
    }

    private static BooleanArrayReturning TotalGreaterThan(double threshold)
    {
        return new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static Query UsersJoinedToOrders(
        JoinType joinType,
        BooleanArrayReturning onCondition
    )
    {
        return new Query(
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
            [new Join(joinType, SampleDatabase.Orders.Entity, onCondition)],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void LeftJoinOnKeyAndThresholdPadsUsersWithoutQualifyingOrders()
    {
        SampleDatabase db = new SampleDatabase();

        const double threshold = 100;

        Query query = UsersJoinedToOrders(
            JoinType.Left,
            new BooleanArrayReturning(
                new EachAndOperator([UserKeyMatch(), TotalGreaterThan(threshold)])
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedCount = db.UserRows.Sum(user =>
            Math.Max(
                1,
                db.OrderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
                )
            )
        );

        Assert.Equal(expectedCount, result.Count);

        foreach (UserRow user in db.UserRows)
        {
            int expectedAppearances = Math.Max(
                1,
                db.OrderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
                )
            );

            Assert.Equal(
                expectedAppearances,
                result
                    .Column(SampleDatabase.Users.Name)
                    .Count(name => name == user.UserName)
            );
        }
    }

    [Fact]
    public void InnerJoinOnDisjunctiveConditionKeepsEitherMatch()
    {
        SampleDatabase db = new SampleDatabase();

        const double markerTotal = 200;

        Query query = UsersJoinedToOrders(
            JoinType.Inner,
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        UserKeyMatch(),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachNumberEquality(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(
                                        new NumberScalar(markerTotal)
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedCount = db.UserRows.Sum(user =>
            db.OrderRows.Count(order =>
                order.OrderUserId == user.UserId
                || order.OrderTotal == markerTotal
            )
        );

        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void InnerJoinOnNegatedKeyEqualityKeepsOnlyNonMatchingPairs()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = UsersJoinedToOrders(
            JoinType.Inner,
            new BooleanArrayReturning(new EachNotOperator(UserKeyMatch()))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedCount = db.UserRows.Sum(user =>
            db.OrderRows.Count(order => order.OrderUserId != user.UserId)
        );

        Assert.Equal(expectedCount, result.Count);
    }
}
