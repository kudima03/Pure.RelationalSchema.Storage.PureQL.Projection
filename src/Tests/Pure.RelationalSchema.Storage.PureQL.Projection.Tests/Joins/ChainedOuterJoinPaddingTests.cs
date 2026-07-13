using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Two chained LEFT joins: padding must propagate. A row padded by the first
// join carries empty cells for the middle table, so the second join's key
// equality can never match it and it must be padded again, surviving to the
// final result exactly once.
[Trait("Clause", "Join")]
[Trait("Feature", "ChainedOuterJoin")]
public sealed class ChainedOuterJoinPaddingTests
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

    private static Join OrdersToItemsLeftJoin()
    {
        return new Join(
            JoinType.Left,
            SampleDatabase.OrderItems.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void SecondLeftJoinPadsRowsAlreadyPaddedByTheFirst()
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
            [UsersToOrdersLeftJoin(), OrdersToItemsLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedCount = db.UserRows.Sum(user =>
        {
            List<OrderRow> orders =
            [
                .. db.OrderRows.Where(order =>
                    order.OrderUserId == user.UserId
                ),
            ];

            return orders.Count == 0
                ? 1
                : orders.Sum(order =>
                    Math.Max(
                        1,
                        db.OrderItemRows.Count(item =>
                            item.ItemOrderId == order.OrderId
                        )
                    )
                );
        });

        Assert.Equal(expectedCount, result.Count);

        foreach (UserRow user in db.UserRows)
        {
            List<OrderRow> orders =
            [
                .. db.OrderRows.Where(order =>
                    order.OrderUserId == user.UserId
                ),
            ];

            int expectedAppearances = orders.Count == 0
                ? 1
                : orders.Sum(order =>
                    Math.Max(
                        1,
                        db.OrderItemRows.Count(item =>
                            item.ItemOrderId == order.OrderId
                        )
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
}
