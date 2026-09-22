using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
            "schema_with_foreign_keys.orders",
            UsersOrdersCondition()
        );
    }

    private static Join UsersToOrdersLeftJoin()
    {
        return new Join(JoinType.Left, "schema_with_foreign_keys.orders", UsersOrdersCondition());
    }

    private static Join OrdersToUsersRightJoin()
    {
        return new Join(
            JoinType.Right,
            "schema_with_foreign_keys.users",
            OrdersUsersCondition()
        );
    }

    private static Join OrdersToUsersFullJoin()
    {
        return new Join(JoinType.Full, "schema_with_foreign_keys.users", OrdersUsersCondition());
    }

    private static BooleanArrayReturning UsersOrdersCondition()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachUuidEquality(
                    new UuidArrayReturning(
                        new UuidField("schema_with_foreign_keys.users", "user_id")
                    ),
                    new UuidArrayReturning(
                        new UuidField(
                            "schema_with_foreign_keys.orders",
                            "order_user_id"
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
                            "schema_with_foreign_keys.orders",
                            "order_user_id"
                        )
                    ),
                    new UuidArrayReturning(
                        new UuidField("schema_with_foreign_keys.users", "user_id")
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
                            "schema_with_foreign_keys.users",
                            "user_active"
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
                                    "schema_with_foreign_keys.orders",
                                    "order_id"
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
                                        "schema_with_foreign_keys.orders",
                                        "order_total"
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
                    new UuidField("schema_with_foreign_keys.users", "user_id")
                ),
            ],
            CountAtLeastOne(),
            [
                new OrderByItem(
                    new Field(
                        new NumberField("schema_with_foreign_keys.users", "orderCount")
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
    private static double[] ExpectedDistinctOrderCountsSkippingFirst(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. userRows
                .Where(user => user.UserActive)
                .Select(user =>
                    (double)orderRows.Count(order => order.OrderUserId == user.UserId)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = FullPipelineQuery(
            new FromExpression("schema_with_foreign_keys.users"),
            UsersToOrdersInnerJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = FullPipelineQuery(
            new FromExpression("schema_with_foreign_keys.users"),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = FullPipelineQuery(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = FullPipelineQuery(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Join usersToLoginsLeftJoin = new Join(
            JoinType.Left,
            "audit.logins",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "audit.logins",
                                "login_user_id"
                            )
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            "audit.logins",
                                            "login_id"
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
                    new UuidField("schema_with_foreign_keys.users", "user_id")
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
                                            "audit.logins",
                                            "login_id"
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
                        new NumberField("schema_with_foreign_keys.users", "loginCount")
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(0, 1),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows
                .Select(user =>
                    (double)loginRows.Count(login => login.LoginUserId == user.UserId)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Join usersToLoginsInnerJoin = new Join(
            JoinType.Inner,
            "audit.logins",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "audit.logins",
                                "login_user_id"
                            )
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            "audit.logins",
                                            "login_id"
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
                    new UuidField("schema_with_foreign_keys.users", "user_id")
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField("schema_with_foreign_keys.users", "loginCount")
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(0, 5),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows
                .Select(user =>
                    (double)loginRows.Count(login => login.LoginUserId == user.UserId)
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
