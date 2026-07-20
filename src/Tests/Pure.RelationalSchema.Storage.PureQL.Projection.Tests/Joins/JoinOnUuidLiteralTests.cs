using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON condition compares a column to a uuid literal (the shape of
// issue #92: ON orders.user_id = '0000...'). The each-equality must actually
// gate the merged rows: a never-matching literal empties an inner join and
// pads a left join, a matching literal keeps only the satisfying left rows
// (crossed with every right row, since the condition does not constrain the
// right side).
[Trait("Clause", "Join")]
[Trait("Feature", "JoinOnUuidLiteral")]
public sealed class JoinOnUuidLiteralTests
{
    [Fact]
    public void InnerJoinOnNeverMatchingUuidLiteralReturnsNoRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    ),
                    "hours"
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
                                new UuidReturning(new UuidScalar(Guid.Empty))
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

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void InnerJoinOnMatchingUuidLiteralKeepsOnlySatisfyingLeftRows()
    {
        SampleDatabase db = new SampleDatabase();

        Guid annId = db.UserRows[0].UserId;

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    ),
                    "hours"
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
                                new UuidReturning(new UuidScalar(annId))
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

        // The condition constrains only the left side, so each satisfying
        // order pairs with every user row.
        int expected =
            db.OrderRows.Count(order => order.OrderUserId == annId)
            * db.UserRows.Count;

        Assert.Equal(expected, result.Count);

        double[] expectedTotals =
        [
            .. db.OrderRows
                .Where(order => order.OrderUserId == annId)
                .Select(order => order.OrderTotal)
                .OrderBy(total => total),
        ];

        Assert.Equal(
            expectedTotals,
            result.Rows
                .Select(row => row.Double("hours") ?? double.NaN)
                .Distinct()
                .OrderBy(total => total)
        );
    }

    [Fact]
    public void LeftJoinOnNeverMatchingUuidLiteralPadsEveryLeftRowOnce()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    ),
                    "hours"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    ),
                    "customer"
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Left,
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
                                new UuidReturning(new UuidScalar(Guid.Empty))
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

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(string.Empty, row["customer"]));
    }
}
