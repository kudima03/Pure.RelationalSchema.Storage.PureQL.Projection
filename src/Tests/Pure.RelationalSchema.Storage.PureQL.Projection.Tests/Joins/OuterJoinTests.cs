using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// LEFT / RIGHT / FULL joins. Assertions target the well-defined parts of outer
// joins (row counts and preserved-side values). The behaviour of null-extended
// columns on the unmatched side is spec-ambiguous (see Semantics/README.md) and
// is deliberately not asserted here; every test selects only a column that is
// present in all result rows.
[Trait("Clause", "Join")]
[Trait("Feature", "OuterJoin")]
public sealed class OuterJoinTests
{
    [Fact]
    public void LeftJoinKeepsUsersWithNoOrders()
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
            [
                new Join(
                    JoinType.Left,
                    SampleDatabase.Orders.Entity,
                    new BooleanArrayReturning(
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

        int expectedCount = db.UserRows.Sum(user =>
            Math.Max(1, db.OrderRows.Count(order => order.OrderUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        // Eve has no orders and must still appear exactly once.
        Assert.Equal(
            1,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Eve")
        );
        // Ann has two orders and must appear once per matched order.
        Assert.Equal(
            2,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Ann")
        );
    }

    [Fact]
    public void RightJoinKeepsUnmatchedUsersOnTheRightSide()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
                    JoinType.Right,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.UserId
                                    )
                                ),
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
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedCount = db.UserRows.Sum(user =>
            Math.Max(1, db.OrderRows.Count(order => order.OrderUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(
            1,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Eve")
        );
    }

    [Fact]
    public void FullJoinKeepsUnmatchedRowsFromBothSides()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
                    JoinType.Full,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.UserId
                                    )
                                ),
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
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // Every order matches exactly one user (matched merged rows = order
        // count), plus the one user with no orders appears once on the right.
        int expectedCount =
            db.OrderRows.Count
            + db.UserRows.Count(user =>
                !db.OrderRows.Any(order => order.OrderUserId == user.UserId)
            );

        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(
            1,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Eve")
        );
    }
}
