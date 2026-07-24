using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Matrix: each JoinType (Inner/Left/Right/Full) layered with WHERE and with
// GROUP BY + HAVING on top of the joined rows. Expectations are computed
// independently with GroupJoin/DefaultIfEmpty so outer-join padded rows are
// modelled the way SQL would (NULL on the unmatched side; a numeric/uuid
// comparison against NULL is unknown and drops the row from WHERE/HAVING).
[Trait("Clause", "Join")]
[Trait("Feature", "JoinPipelineCombo")]
public sealed class JoinPipelineWhereHavingTests
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

    private static BooleanArrayReturning TotalAtLeast100Each()
    {
        return new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(100))
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

    // Every order with total >= 100, independent of unmatched-row padding:
    // Ann/101, Bob/103, Cara/105, Dan/106 survive; Ann/102 (50) and
    // Cara/104 (75.25) do not.
    private static string[] ExpectedNamesWithTotalAtLeast100(SampleDatabase db)
    {
        return
        [
            .. db.OrderRows
                .Where(order => order.OrderTotal >= 100)
                .Select(order =>
                    db.UserRows.Single(user => user.UserId == order.OrderUserId).UserName
                )
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void InnerJoinThenEachWhereOnJoinedTotalKeepsOnlyQualifyingOrders()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            TotalAtLeast100Each(),
            [UsersToOrdersInnerJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(db);

        string?[] actual =
        [
            .. result.Column(SampleDatabase.Users.Name).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            TotalAtLeast100Each(),
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // The padded rows for Eve/Fay carry a NULL total; NULL >= 100 is
        // unknown in SQL, so WHERE drops them exactly like every real row
        // that fails the threshold.
        string[] expected = ExpectedNamesWithTotalAtLeast100(db);

        string?[] actual =
        [
            .. result.Column(SampleDatabase.Users.Name).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [UserNameSelect()],
            TotalAtLeast100Each(),
            [OrdersToUsersRightJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(db);

        string?[] actual =
        [
            .. result.Column(SampleDatabase.Users.Name).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [UserNameSelect()],
            TotalAtLeast100Each(),
            [OrdersToUsersFullJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(db);

        string?[] actual =
        [
            .. result.Column(SampleDatabase.Users.Name).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    private static SelectExpression OrderCountSelect(string alias)
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
            alias
        );
    }

    private static SelectExpression OrderTotalSumSelect(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(
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
                )
            ),
            alias
        );
    }

    private static Field UsersIdField()
    {
        return new Field(
            new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
        );
    }

    // Extends OuterJoinAggregationTests (LEFT JOIN only) to RIGHT JOIN: an
    // unmatched right-side user still forms its own group with a real key
    // and a zero/NULL aggregate, exercising JoinApplicator.RightJoin's own
    // padding branch rather than LeftJoin's.
    [Fact]
    public void RightJoinGroupByUserCountsZeroAndSumsNullForUnmatchedUsers()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                OrderCountSelect("orderCount"),
                OrderTotalSumSelect("totalSum"),
            ],
            where: null,
            [OrdersToUsersRightJoin()],
            [UsersIdField()],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, (double Count, double? Sum)> expected = db.UserRows.ToDictionary(
            user => user.UserId,
            user =>
            {
                List<OrderRow> matched =
                [
                    .. db.OrderRows.Where(order => order.OrderUserId == user.UserId),
                ];
                return (
                    (double)matched.Count,
                    matched.Count == 0
                        ? (double?)null
                        : matched.Sum(order => order.OrderTotal)
                );
            }
        );

        Dictionary<Guid, (double Count, double? Sum)> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row => (row.Double("orderCount")!.Value, row.Double("totalSum"))
        );

        Assert.Equal(expected, actual);
    }

    // Same as above but for FULL JOIN, exercising JoinApplicator.FullJoin's
    // right-unmatched padding branch.
    [Fact]
    public void FullJoinGroupByUserCountsZeroAndSumsNullForUnmatchedUsers()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                OrderCountSelect("orderCount"),
                OrderTotalSumSelect("totalSum"),
            ],
            where: null,
            [OrdersToUsersFullJoin()],
            [UsersIdField()],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, (double Count, double? Sum)> expected = db.UserRows.ToDictionary(
            user => user.UserId,
            user =>
            {
                List<OrderRow> matched =
                [
                    .. db.OrderRows.Where(order => order.OrderUserId == user.UserId),
                ];
                return (
                    (double)matched.Count,
                    matched.Count == 0
                        ? (double?)null
                        : matched.Sum(order => order.OrderTotal)
                );
            }
        );

        Dictionary<Guid, (double Count, double? Sum)> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row => (row.Double("orderCount")!.Value, row.Double("totalSum"))
        );

        Assert.Equal(expected, actual);
    }

    // GROUP BY + HAVING(sum) on top of the join: Ann/Bob/Cara's sums clear
    // the 150 bar, Dan's (100.50) does not, and unmatched users' NULL sum
    // is unknown against ">= 150" so they are excluded exactly like Dan.
    private static HashSet<Guid> ExpectedUsersWithTotalAtLeast150(SampleDatabase db)
    {
        return
        [
            .. db.UserRows
                .Where(user =>
                    db.OrderRows.Any(order => order.OrderUserId == user.UserId)
                    && db.OrderRows
                        .Where(order => order.OrderUserId == user.UserId)
                        .Sum(order => order.OrderTotal)
                        >= 150
                )
                .Select(user => user.UserId),
        ];
    }

    private static BooleanReturning SumAtLeast150()
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
                    new NumberReturning(new NumberScalar(150))
                )
            )
        );
    }

    private static Query GroupedByUserWithHaving(FromExpression from, Join join)
    {
        return new Query(
            from,
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                OrderTotalSumSelect("totalSum"),
            ],
            where: null,
            [join],
            [UsersIdField()],
            SumAtLeast150(),
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void InnerJoinGroupByHavingSumFiltersGroupsBelowThreshold()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = GroupedByUserWithHaving(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersInnerJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(db);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = GroupedByUserWithHaving(
            new FromExpression(SampleDatabase.Users.Entity),
            UsersToOrdersLeftJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(db);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = GroupedByUserWithHaving(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersRightJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(db);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = GroupedByUserWithHaving(
            new FromExpression(SampleDatabase.Orders.Entity),
            OrdersToUsersFullJoin()
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(db);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id)!.Value),
        ];

        Assert.Equal(expected, actual);
    }
}
