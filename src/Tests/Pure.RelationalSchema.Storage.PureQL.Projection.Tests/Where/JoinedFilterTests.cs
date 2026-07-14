using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where;

// WHERE evaluated over merged join rows: predicates may mix columns of both
// tables, reference a joined boolean field directly, or negate a condition
// on the joined side. Expected sets are computed pair-by-pair from the
// ground-truth records.
[Trait("Clause", "Where")]
[Trait("Feature", "JoinedFilter")]
public sealed class JoinedFilterTests
{
    private static Join OrdersToUsersJoin()
    {
        return new Join(
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
        );
    }

    private static Query OrdersWithUsers(BooleanArrayReturning where)
    {
        return new Query(
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
            ],
            where,
            [OrdersToUsersJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    private static UserRow UserOf(SampleDatabase db, OrderRow order)
    {
        return db.UserRows.Single(user => user.UserId == order.OrderUserId);
    }

    [Fact]
    public void WhereConjunctionAcrossBothTablesFiltersMergedRows()
    {
        SampleDatabase db = new SampleDatabase();

        const double ageThreshold = 30;
        const double totalThreshold = 100;

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThanOrEqual,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Age
                                        )
                                    ),
                                    new NumberReturning(
                                        new NumberScalar(ageThreshold)
                                    )
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(
                                        new NumberScalar(totalThreshold)
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

        int expected = db.OrderRows.Count(order =>
            UserOf(db, order).UserAge >= ageThreshold
            && order.OrderTotal > totalThreshold
        );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereOnJoinedBooleanFieldKeepsRowsWhereItIsTrue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new BooleanField(
                    SampleDatabase.Users.Entity,
                    SampleDatabase.Users.Active
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Count(order => UserOf(db, order).UserActive);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereNegationOnJoinedColumnExcludesItsRows()
    {
        SampleDatabase db = new SampleDatabase();

        const string excludedName = "Ann";

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachStringEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Name
                                    )
                                ),
                                new StringReturning(
                                    new StringScalar(excludedName)
                                )
                            )
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Count(order =>
            UserOf(db, order).UserName != excludedName
        );

        Assert.Equal(expected, result.Count);
    }
}
