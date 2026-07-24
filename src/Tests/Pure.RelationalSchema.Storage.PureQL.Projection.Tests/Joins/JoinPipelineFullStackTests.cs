using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Every clause at once, layered on each JoinType in turn: JOIN -> WHERE ->
// GROUP BY -> HAVING -> ORDER BY -> DISTINCT -> pagination. Extends
// Combined/FullPipelineTests.cs (INNER JOIN only) to LEFT/RIGHT/FULL, and
// adds a cross-schema variant against audit.logins for breadth.
//
// Selecting only the aggregate (no group key in the output) makes DISTINCT
// do real work here: Ann and Cara both place 2 orders, so their projected
// group rows (orderCount = 2) collapse into a single distinct row once
// DISTINCT runs, alongside Dan's (orderCount = 1). Fay (active, but no
// orders) forms its own zero-count group, which HAVING then drops - so the
// outer-join-only "empty group" case is exercised here too.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinPipelineCombo")]
public sealed class JoinPipelineFullStackTests
{
    private static Join UsersToOrdersInnerJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Orders.Entity,
            UsersOrdersCondition()
        );
    }

    private static Join UsersToOrdersLeftJoin()
    {
        return new Join(JoinType.Left, SampleDatabase.Orders.Entity, UsersOrdersCondition());
    }

    private static Join OrdersToUsersRightJoin()
    {
        return new Join(
            JoinType.Right,
            SampleDatabase.Users.Entity,
            OrdersUsersCondition()
        );
    }

    private static Join OrdersToUsersFullJoin()
    {
        return new Join(JoinType.Full, SampleDatabase.Users.Entity, OrdersUsersCondition());
    }

    private static BooleanArrayReturning UsersOrdersCondition()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachUuidEquality(
                    new UuidArrayReturning(
                        new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
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

    private static BooleanArrayReturning OrdersUsersCondition()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachUuidEquality(
                    new UuidArrayReturning(
                        new UuidField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.UserId
                        )
                    ),
                    new UuidArrayReturning(
                        new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                    )
                )
            )
        );
    }

    private static BooleanArrayReturning UserIsActiveEach()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachBooleanEquality(
                    new BooleanArrayReturning(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    new BooleanReturning(new BooleanScalar(true))
                )
            )
        );
    }

    private static SelectExpression OrderCountSelect()
    {
        return new SelectExpression(
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
        );
    }

    private static BooleanReturning CountAtLeastOne()
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
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
                    ),
                    new NumberReturning(new NumberScalar(0))
                )
            )
        );
    }

    // sum(total) >= 0 is only ever true when the group actually has a
    // matched order (an empty/NULL group folds sum to NULL, and NULL >= 0
    // is unknown), so this HAVING clause is a clean, aggregate-driven way
    // to require "at least one order" without a bare count comparison.
    private static Query FullPipelineQuery(FromExpression from, Join join)
    {
        return new Query(
            from,
            [OrderCountSelect()],
            UserIsActiveEach(),
            [join],
            [
                new Field(
                    new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                ),
            ],
            CountAtLeastOne(),
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Users.Entity, "orderCount")
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(1, 5),
            distinct: true
        );
    }

    // Active users are Ann, Cara, Dan, Fay (Bob and Eve are inactive and
    // dropped by WHERE). Ann and Cara each place 2 orders, Dan places 1,
    // and Fay places none - Fay's zero-order group is dropped by HAVING.
    // Distinct group counts, sorted desc, are therefore [2, 1]; skipping
    // the first (2) and taking up to 5 leaves exactly [1].
    private static double[] ExpectedDistinctOrderCountsSkippingFirst(SampleDatabase db)
    {
        return
        [
            .. db.UserRows
                .Where(user => user.UserActive)
                .Select(user =>
                    (double)db.OrderRows.Count(order => order.OrderUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Skip(1)
                .Take(5),
        ];
    }

    [Fact]
    public void InnerJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersInnerJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(db);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(db);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(db);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(db);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    // Cross-schema breadth: shop.users <-> audit.logins. Ann has 2 logins,
    // Bob and Eve have 1 each, Cara/Dan/Fay have none. WHERE keeps every
    // row (bare true), GROUP BY the user, HAVING requires at least one
    // login, ORDER BY the count desc, DISTINCT collapses Bob's and Eve's
    // tied count of 1 into a single row alongside Ann's 2, and pagination
    // takes just the top entry.
    [Fact]
    public void CrossSchemaLeftJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Join usersToLoginsLeftJoin = new Join(
            JoinType.Left,
            SampleDatabase.Logins.Entity,
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
                                SampleDatabase.Logins.Entity,
                                SampleDatabase.Logins.UserId
                            )
                        )
                    )
                )
            )
        );

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
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "loginCount"
                ),
            ],
            where: null,
            [usersToLoginsLeftJoin],
            [
                new Field(
                    new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.Id
                                        )
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(1))
                    )
                )
            ),
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Users.Entity, "loginCount")
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(0, 1),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.UserRows
                .Select(user =>
                    (double)db.LoginRows.Count(login => login.LoginUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Take(1),
        ];

        double[] actual = [.. result.Rows.Select(row => row.Double("loginCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    // Cross-schema variant with an INNER JOIN: every emitted group already
    // has at least one login by construction, so HAVING is a pass-through
    // and the interesting behaviour is DISTINCT collapsing Bob's and Eve's
    // tied login count after ORDER BY + pagination select the full set.
    [Fact]
    public void CrossSchemaInnerJoinFullPipelineComposesEveryClauseInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Join usersToLoginsInnerJoin = new Join(
            JoinType.Inner,
            SampleDatabase.Logins.Entity,
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
                                SampleDatabase.Logins.Entity,
                                SampleDatabase.Logins.UserId
                            )
                        )
                    )
                )
            )
        );

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
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "loginCount"
                ),
            ],
            where: null,
            [usersToLoginsInnerJoin],
            [
                new Field(
                    new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Users.Entity, "loginCount")
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(0, 5),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.UserRows
                .Select(user =>
                    (double)db.LoginRows.Count(login => login.LoginUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Take(5),
        ];

        double[] actual = [.. result.Rows.Select(row => row.Double("loginCount")!.Value)];

        Assert.Equal(expected, actual);
    }
}
