using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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

    private static Join UsersToOrdersInnerJoin()
    {
        return new Join(
            JoinType.Inner,
            "schema_with_foreign_keys.orders",
            UsersOrdersCondition()
        );
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

    private static SelectExpression UserNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField("schema_with_foreign_keys.users", "user_name")
                )
            )
        );
    }

    private static SelectExpression OrderTotalSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new NumberArrayReturning(
                    new NumberField("schema_with_foreign_keys.orders", "order_total")
                )
            )
        );
    }

    private static OrderByItem OrderByTotalAsc()
    {
        return new OrderByItem(
            new Field(
                new NumberField("schema_with_foreign_keys.orders", "order_total")
            ),
            SortDirection.Asc
        );
    }

    private static OrderByItem OrderByTotalDesc()
    {
        return new OrderByItem(
            new Field(
                new NumberField("schema_with_foreign_keys.orders", "order_total")
            ),
            SortDirection.Desc
        );
    }

    private static OrderByItem OrderByNameAsc()
    {
        return new OrderByItem(
            new Field(
                new StringField("schema_with_foreign_keys.users", "user_name")
            ),
            SortDirection.Asc
        );
    }

    // (name, total) pairs for every user, LEFT-JOIN style: matched users
    // appear once per order, unmatched users appear once with a NULL total.
    // Ties on total are broken by name so the expected sequence is fully
    // deterministic without depending on the translator's tie-break/stable-
    // sort behaviour matching this computation by coincidence.
    private static List<(string Name, double? Total)> LeftOuterPairs(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. from user in userRows
                join order in orderRows on user.UserId equals order.OrderUserId into g
                from order in g.DefaultIfEmpty()
                select (user.UserName, order?.OrderTotal),
        ];
    }

    [Fact]
    public void LeftJoinOrderByJoinedTotalAscendingSortsNullsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalAsc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double?)[] expected =
        [
            .. LeftOuterPairs(userRows, orderRows)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenBy(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total"))
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinOrderByJoinedTotalAscendingSortsNullsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [OrdersToUsersRightJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalAsc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double?)[] expected =
        [
            .. LeftOuterPairs(userRows, orderRows)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenBy(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total"))
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinOrderByJoinedTotalDescendingStillSortsNullsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [UserNameSelect(), OrderTotalSelect()],
            where: null,
            [OrdersToUsersFullJoin()],
            groupBy: null,
            having: null,
            [OrderByTotalDesc(), OrderByNameAsc()],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // NULLS-last is unconditional: even under a descending primary
        // sort, the unmatched rows (NULL total) still land at the end.
        (string, double?)[] expected =
        [
            .. LeftOuterPairs(userRows, orderRows)
                .OrderBy(pair => pair.Total.HasValue ? 0 : 1)
                .ThenByDescending(pair => pair.Total)
                .ThenBy(pair => pair.Name, StringComparer.Ordinal),
        ];

        (string, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total"))
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                UserNameSelect(),
                new SelectExpression(
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
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            [
                new Field(
                    new StringField(
                        "schema_with_foreign_keys.users",
                        "user_name"
                    )
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField("schema_with_foreign_keys.orders", "orderCount")
                    ),
                    SortDirection.Desc
                ),
                OrderByNameAsc(),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected =
        [
            .. userRows
                .Select(user =>
                    (
                        user.UserName,
                        (double)orderRows.Count(order => order.OrderUserId == user.UserId)
                    )
                )
                .OrderByDescending(pair => pair.Item2)
                .ThenBy(pair => pair.UserName, StringComparer.Ordinal),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("orderCount")!.Value)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = DistinctUserNames(
            new FromExpression("schema_with_foreign_keys.users"),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column("user_name").OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void RightJoinDistinctOnUserNameKeepsEveryUserIncludingUnmatched()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = DistinctUserNames(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column("user_name").OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FullJoinDistinctOnUserNameKeepsEveryUserIncludingUnmatched()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = DistinctUserNames(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).Distinct().OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column("user_name").OrderBy(name => name).ToArray()
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
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
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Append(string.Empty)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal),
        ];

        Assert.Equal(
            expected,
            result
                .Column("order_status")
                .OrderBy(status => status, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ORDER BY + pagination windowing over the joined rows. The window
    // (skip 2, take 3) lands entirely on matched rows for every join type
    // in this fixture (the NULL-total padded rows sort last, past the
    // window), so all four join types share the same expected slice.
    private static (string Name, double Total)[] ExpectedInnerWindow(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. (
                from user in userRows
                join order in orderRows on user.UserId equals order.OrderUserId
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = WindowedQuery(
            new FromExpression("schema_with_foreign_keys.users"),
            UsersToOrdersInnerJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(userRows, orderRows);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinOrderByThenPaginationWindowsJoinedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = WindowedQuery(
            new FromExpression("schema_with_foreign_keys.users"),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(userRows, orderRows);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinOrderByThenPaginationWindowsJoinedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = WindowedQuery(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(userRows, orderRows);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinOrderByThenPaginationWindowsJoinedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = WindowedQuery(
            new FromExpression("schema_with_foreign_keys.orders"),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected = ExpectedInnerWindow(userRows, orderRows);

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (row["user_name"]!, row.Double("order_total")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
