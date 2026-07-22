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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.Qty
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Orders.Entity,
                    new BooleanArrayReturning(
                        new EachAndOperator(
                            [
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachUuidEquality(
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    SampleDatabase.OrderItems.Entity,
                                                    SampleDatabase.OrderItems.OrderId
                                                )
                                            ),
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Id
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
                                                    SampleDatabase.OrderItems.Entity,
                                                    SampleDatabase.OrderItems.Qty
                                                )
                                            ),
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Total
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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = (
            from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
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
                    JoinType.Inner,
                    SampleDatabase.Logins.Entity,
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachDateTimeComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new DateTimeArrayReturning(
                                    new DateTimeField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.LastLogin
                                    )
                                ),
                                new DateTimeArrayReturning(
                                    new DateTimeField(
                                        SampleDatabase.Logins.Entity,
                                        SampleDatabase.Logins.At
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

        int expected = (
            from user in db.UserRows
            from login in db.LoginRows
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
            Math.Max(1, db.LoginRows.Count(login => login.LoginUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        // Cara and Dan have no logins and must each still appear exactly once.
        Assert.Equal(
            1,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Cara")
        );
        Assert.Equal(
            1,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Dan")
        );
        // Ann has two logins and must appear once per matched login.
        Assert.Equal(
            2,
            result.Column(SampleDatabase.Users.Name).Count(name => name == "Ann")
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double?[] expected =
        [
            .. db.OrderRows
                .OrderByDescending(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total)),
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
            new global::PureQL.CSharp.Model.Pagination(2, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid?[] expected =
        [
            .. db.OrderRows.Skip(2).Take(2).Select(order => (Guid?)order.OrderId),
        ];

        Guid?[] actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Orders.Id)),
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
                    )
                ),
            ],
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
                        new NumberReturning(new NumberScalar(50))
                    )
                )
            ),
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double?[] expected =
        [
            .. db.OrderRows
                .Where(order => order.OrderTotal > 50)
                .OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total)),
        ];

        Assert.Equal(expected, actual);
    }
}
