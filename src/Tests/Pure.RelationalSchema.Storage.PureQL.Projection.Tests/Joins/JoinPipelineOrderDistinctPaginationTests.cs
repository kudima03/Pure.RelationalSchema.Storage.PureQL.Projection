using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Matrix: each JoinType layered with ORDER BY (including the ratified
// NULLS-last rule for the outer-joined side), DISTINCT (collapsing join
// fan-out, including padded rows), and ORDER BY + pagination windowing.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinPipelineCombo")]
public sealed class JoinPipelineOrderDistinctPaginationTests
{
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

    private static Join UsersToOrdersInnerJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Orders.Entity,
            UsersOrdersCondition()
        );
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

    private static SelectExpression UserNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField(SampleDatabase.Users.Entity, SampleDatabase.Users.Name)
                )
            )
        );
    }

    private static SelectExpression OrderTotalSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new NumberArrayReturning(
                    new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
                )
            )
        );
    }

    private static OrderByItem OrderByTotalAsc()
    {
        return new OrderByItem(
            new Field(
                new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
            ),
            SortDirection.Asc
        );
    }

    private static OrderByItem OrderByTotalDesc()
    {
        return new OrderByItem(
            new Field(
                new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
            ),
            SortDirection.Desc
        );
    }

    private static OrderByItem OrderByNameAsc()
    {
        return new OrderByItem(
            new Field(
                new StringField(SampleDatabase.Users.Entity, SampleDatabase.Users.Name)
            ),
            SortDirection.Asc
        );
    }

    // (name, total) pairs for every user, LEFT-JOIN style: matched users
    // appear once per order, unmatched users appear once with a NULL total.
    // Ties on total are broken by name so the expected sequence is fully
    // deterministic without depending on the translator's tie-break/stable-
    // sort behaviour matching this computation by coincidence.
    private static List<(string Name, double? Total)> LeftOuterPairs(SampleDatabase db)
    {
        return
        [
            .. from user in db.UserRows
                join order in db.OrderRows on user.UserId equals order.OrderUserId into g
                from order in g.DefaultIfEmpty()
                select (user.UserName, order?.OrderTotal),
        ];
    }

    [Fact]
    public void LeftJoinOrderByJoinedTotalAscendingSortsNullsLast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalAsc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double?)[] expected =
        [
            .. LeftOuterPairs(db)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenBy(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total))
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinOrderByJoinedTotalAscendingSortsNullsLast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [OrdersToUsersRightJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalAsc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double?)[] expected =
        [
            .. LeftOuterPairs(db)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenBy(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total))
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinOrderByJoinedTotalDescendingStillSortsNullsLast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [OrdersToUsersFullJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalDesc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // NULLS-last is unconditional: even under a descending primary
        // sort, the unmatched rows (NULL total) still land at the end.
        (string, double?)[] expected =
        [
            .. LeftOuterPairs(db)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenByDescending(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total))
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // ORDER BY the aggregate alias in group mode, on top of a LEFT JOIN:
    // proves the alias sort runs against the post-aggregation row (per
    // RowsFromDatasets.Build) even when the underlying rows came from an
    // outer join.
    [Fact]
    public void LeftJoinGroupByOrderByAggregateAliasDescendingOrdersGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                UserNameSelect(),
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
                    new StringField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Name
                    )
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Orders.Entity, "orderCount")
                    ),
                    SortDirection.Desc
                ),
                OrderByNameAsc(),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected =
        [
            .. db.UserRows
                .Select(user =>
                    (
                        user.UserName,
                        (double)db.OrderRows.Count(order => order.OrderUserId == user.UserId)
                    )
                )
                .OrderByDescending(pair => pair.Item2)
                .ThenBy(pair => pair.UserName, StringComparer.Ordinal),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double("orderCount")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // SELECT DISTINCT users.name through an outer join: unlike an INNER
    // JOIN (Select/DistinctOverJoinTests), the padded rows for unmatched
    // users still carry their own real name, so DISTINCT must keep every
    // user, not just the ones with at least one order.
    private static Query DistinctUserNames(FromExpression from, Join join)
    {
        return new Query(
            from,
            [UserNameSelect()],
            where: null,
            [join],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );
    }

    [Fact]
    public void LeftJoinDistinctOnUserNameKeepsEveryUserIncludingUnmatched()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = DistinctUserNames(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void RightJoinDistinctOnUserNameKeepsEveryUserIncludingUnmatched()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = DistinctUserNames(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FullJoinDistinctOnUserNameKeepsEveryUserIncludingUnmatched()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = DistinctUserNames(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    // DISTINCT on the padded (joined) side itself: the ratified rule pads
    // a missing string cell with "" (Semantics/README.md), so the distinct
    // value set for orders.status through a LEFT JOIN gains an extra ""
    // entry on top of the three real statuses, for the two unmatched users.
    // Unaffected by issue #167 (padded strings now reading as NULL through
    // CellValueExtractor): DistinctApplicator computes its dedup key from
    // the projected row's raw ICell.TextValue directly, not through
    // CellValueExtractor, so the padded cell still surfaces here as a
    // literal "" - the same display convention every other NULL group/
    // dedup key already uses (see Semantics/README.md's "Outer-join null
    // extension" row). #167 changed the *computational* reading of a
    // padded string (aggregates, count, WHERE, ORDER BY, GROUP BY key),
    // not this display-layer text.
    [Fact]
    public void LeftJoinDistinctOnJoinedStatusIncludesEmptyStringForPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
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
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .Select(order => order.OrderStatus)
                .Append(string.Empty)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal),
        ];

        Assert.Equal(
            expected,
            result
                .Column(SampleDatabase.Orders.Status)
                .OrderBy(status => status, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ORDER BY + pagination windowing over the joined rows. The window
    // (skip 2, take 3) lands entirely on matched rows for every join type
    // in this fixture (the NULL-total padded rows sort last, past the
    // window), so all four join types share the same expected slice.
    private static (string Name, double Total)[] ExpectedInnerWindow(SampleDatabase db)
    {
        return
        [
            .. (
                from user in db.UserRows
                join order in db.OrderRows on user.UserId equals order.OrderUserId
                select (user.UserName, order.OrderTotal)
            )
                .OrderBy(pair => pair.OrderTotal)
                .ThenBy(pair => pair.UserName, StringComparer.Ordinal)
                .Skip(2)
                .Take(3),
        ];
    }

    private static Query WindowedQuery(FromExpression from, Join join)
    {
        return new Query(
            from,
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [join],
            groupBy: null,
            having: null,
            [OrderByTotalAsc(), OrderByNameAsc()],
            new ModelPagination(2, 3)
        );
    }

    [Fact]
    public void InnerJoinOrderByThenPaginationWindowsJoinedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = WindowedQuery(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersInnerJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(db);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total)!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinOrderByThenPaginationWindowsJoinedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = WindowedQuery(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(db);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total)!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinOrderByThenPaginationWindowsJoinedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = WindowedQuery(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(db);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total)!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinOrderByThenPaginationWindowsJoinedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = WindowedQuery(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(db);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Users.Name]!, row.Double(SampleDatabase.Orders.Total)!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
