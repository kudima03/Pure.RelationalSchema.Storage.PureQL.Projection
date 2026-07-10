using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// INNER JOIN on an equi-key expressed as a per-row each-equality over the two
// tables' uuid columns. Only matched pairs survive; columns from both sides
// are available (their names are globally unique so there is no ambiguity).
[Trait("Clause", "Join")]
[Trait("Feature", "InnerJoin")]
public sealed class InnerJoinTests
{
    [Fact]
    public void InnerJoinPairsEachOrderWithItsUser()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
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
                    JoinType.Inner,
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

        (Guid, string?)[] expected =
        [
            .. db.OrderRows
                .Select(order =>
                    (
                        order.OrderId,
                        (string?)
                            db.UserRows.Single(user =>
                                user.UserId == order.OrderUserId
                            ).UserName
                    )
                )
                .OrderBy(pair => pair.OrderId),
        ];

        (Guid, string?)[] actual =
        [
            .. result
                .Rows.Select(row =>
                    (
                        row.Uuid(SampleDatabase.Orders.Id)!.Value,
                        row[SampleDatabase.Users.Name]
                    )
                )
                .OrderBy(pair => pair.Value),
        ];

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InnerJoinProducesNoUnmatchedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
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

        // Every order references an existing user, so an inner join neither
        // drops nor duplicates order rows.
        Assert.Equal(db.OrderRows.Count, result.Count);
    }
}
