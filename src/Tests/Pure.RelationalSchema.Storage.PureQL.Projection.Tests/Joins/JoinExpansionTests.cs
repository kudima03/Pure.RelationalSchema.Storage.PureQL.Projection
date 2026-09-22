using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Rounds out issue #99 (part of the #72 roadmap): composite/non-equi ON
// variety not already covered by CompositeJoinConditionTests/NonEquiJoinTests,
// one more cross-schema pairing beyond CrossSchemaJoinTests' single direction
// pair, and JOIN composed individually with each downstream clause.
//
// KnownGap items from the issue are intentionally not duplicated here:
//   - Outer-join NULL extension is no longer a gap: it is implemented
//     (JoinApplicator.Pad) and already asserted by
//     JoinedTableColumnProjectionTests.LeftJoinUnmatchedRowsExposeJoinedColumnsAsNullCells.
//   - Self-joins are already pinned as a real fail-fast assertion in
//     SelfJoinTests.JoinOnSameEntityAsFromFailsFast, per the #109 defect.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeEqualityAndFieldComparisonJoinCondition")]
public sealed class CompositeEqualityAndFieldComparisonJoinConditionTests
{
    [Fact]
    public void InnerJoinOnKeyEqualityAndQtyAtMostOrderTotalKeepsEveryMatchingItem()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.order_items"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.order_items",
                                "item_qty"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.orders",
                    new BooleanArrayReturning(
                        new EachAndOperator(
                            [
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachUuidEquality(
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    "schema_with_foreign_keys.order_items",
                                                    "item_order_id"
                                                )
                                            ),
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    "schema_with_foreign_keys.orders",
                                                    "order_id"
                                                )
                                            )
                                        )
                                    )
                                ),
                                new BooleanArrayReturning(
                                    new EachComparison(
                                        new EachNumberComparison(
                                            EachComparisonOperator.EachLessThanOrEqual,
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    "schema_with_foreign_keys.order_items",
                                                    "item_qty"
                                                )
                                            ),
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    "schema_with_foreign_keys.orders",
                                                    "order_total"
                                                )
                                            )
                                        )
                                    )
                                ),
                            ]
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
            new PureQLProjection(datasets, query)
        );

        int expected = (
            from item in orderItemRows
            join order in orderRows on item.ItemOrderId equals order.OrderId
            where item.ItemQty <= order.OrderTotal
            select 1
        ).Count();

        Assert.Equal(expected, result.Count);
    }
}

// A join whose ON condition is an each*-comparison (not a plain field
// equality) between two datetime columns spanning schemas: shop.users and
// audit.logins.
[Trait("Clause", "Join")]
[Trait("Feature", "EachDateTimeComparisonCrossSchemaJoinCondition")]
public sealed class EachDateTimeComparisonCrossSchemaJoinConditionTests
{
    [Fact]
    public void InnerJoinOnLastLoginAfterLoginAtKeepsQualifyingPairsAcrossSchemas()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "audit.logins",
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachDateTimeComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new DateTimeArrayReturning(
                                    new DateTimeField(
                                        "schema_with_foreign_keys.users",
                                        "last_login"
                                    )
                                ),
                                new DateTimeArrayReturning(
                                    new DateTimeField("audit.logins", "login_at")
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
            new PureQLProjection(datasets, query)
        );

        int expected = (
            from user in userRows
            from login in loginRows
            where user.LastLogin > login.LoginAt
            select 1
        ).Count();

        Assert.Equal(expected, result.Count);
    }
}

// A second cross-schema pairing (shop.users LEFT JOIN audit.logins) beyond
// CrossSchemaJoinTests' inner-join pair, exercising the well-defined parts
// of an outer join (row counts and preserved-side values) across the schema
// boundary.
[Trait("Clause", "Join")]
[Trait("Feature", "CrossSchemaOuterJoin")]
public sealed class CrossSchemaOuterJoinTests
{
    [Fact]
    public void LeftJoinFromUsersToLoginsKeepsUsersWithNoLogins()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
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
                                    new UuidField("audit.logins", "login_user_id")
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
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            Math.Max(1, loginRows.Count(login => login.LoginUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        // Cara and Dan have no logins and must each still appear exactly once.
        Assert.Equal(
            1,
            result.Column("user_name").Count(name => name == "Cara")
        );
        Assert.Equal(
            1,
            result.Column("user_name").Count(name => name == "Dan")
        );
        // Ann has two logins and must appear once per matched login.
        Assert.Equal(
            2,
            result.Column("user_name").Count(name => name == "Ann")
        );
    }
}

// JOIN composed with ORDER BY alone (no pagination), pinning that ordering
// applies to the merged row set produced by the join.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinThenOrderBy")]
public sealed class JoinThenOrderByTests
{
    [Fact]
    public void InnerJoinThenOrderByTotalDescendingSortsMergedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.orders",
                            "order_total"
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows
                .OrderByDescending(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double("order_total")),
        ];

        Assert.Equal(expected, actual);
    }
}

// JOIN composed with pagination alone (no ORDER BY). Every order matches
// exactly one user (one-to-one FK), so the inner join's nested-loop
// enumeration (JoinApplicator.InnerJoin: left.SelectMany(right.Select))
// deterministically preserves the source Orders order, making the window
// pinned here a stable, correct expectation rather than an assumption about
// unordered SQL semantics.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinThenPagination")]
public sealed class JoinThenPaginationTests
{
    [Fact]
    public void InnerJoinThenPaginationAloneReturnsTheDeterministicWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
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
            new global::PureQL.CSharp.Model.Pagination(2, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid?[] expected =
        [
            .. orderRows.Skip(2).Take(2).Select(order => (Guid?)order.OrderId),
        ];

        Guid?[] actual =
        [
            .. result.Rows.Select(row => row.Uuid("order_id")),
        ];

        Assert.Equal(expected, actual);
    }
}

// A bridge toward full composed coverage: JOIN followed by two more clauses
// (WHERE then ORDER BY) applied together, over the merged row set.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinWhereOrderByBridge")]
public sealed class JoinWhereOrderByBridgeTests
{
    [Fact]
    public void InnerJoinThenWhereThenOrderByComposesAllThreeClauses()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        ),
                        new NumberReturning(new NumberScalar(50))
                    )
                )
            ),
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.orders",
                            "order_total"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows
                .Where(order => order.OrderTotal > 50)
                .OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double("order_total")),
        ];

        Assert.Equal(expected, actual);
    }
}
