using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Aggregates evaluated over an outer join: unmatched left rows carry empty
// cells for the joined side, and SQL count/sum semantics skip those absent
// values, so aggregates over a joined column see only the matched rows.
[Trait("Clause", "Join")]
[Trait("Feature", "OuterJoinAggregation")]
public sealed class OuterJoinAggregationTests
{
    private static Join UsersToOrdersLeftJoin()
    {
        return new Join(
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
        );
    }

    [Fact]
    public void CountOverJoinedColumnAfterLeftJoinCountsOnlyMatchedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "orderCount"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // Every order matches a user; the padded row of the orderless user
        // must not contribute to the count.
        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("orderCount"));
    }

    [Fact]
    public void LeftJoinGroupByUserCountsZeroForUnmatchedUsers()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "orderCount"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Id
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.UserRows.ToDictionary(
            user => user.UserId,
            user => (double)
                db.OrderRows.Count(order => order.OrderUserId == user.UserId)
        );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row => row.Double("orderCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SumOverJoinedColumnAfterLeftJoinIgnoresPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
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
                    "totalSum"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            db.OrderRows.Sum(order => order.OrderTotal),
            result.Row(0).Double("totalSum")
        );
    }
}
