using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Issue #155: mixed-family each* combos whose operands are drawn from both
// sides of a join - a base-table column compared against/combined with a
// joined-table column inside an each-arithmetic, then fed into a comparison
// and/or further boolean composition. Every expectation is derived
// independently in LINQ over the ground-truth lists under SQL result-set
// semantics, including the LEFT JOIN null-propagation case (unmatched side
// -> null operand -> comparison false -> row excluded).
[Trait("Clause", "Where")]
[Trait("Feature", "EachMixedFamilyCombo")]
public sealed class EachMixedFamilyJoinedComboTests
{
    private static SelectExpression OrderIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                )
            )
        );
    }

    private static SelectExpression UserNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Name
                    )
                )
            )
        );
    }

    private static Join InnerJoinOrdersToUsers()
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
                            new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning TotalPlusAge()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachAdd(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        ),
                    ]
                )
            )
        );
    }

    // eachGreaterThan(eachAdd(order.total, user.age), 120) - cross-entity
    // arithmetic feeding a comparison, bare at the top of the tree.
    [Fact]
    public void EachGreaterThanOfSummedOrderTotalAndUserAgeAcrossJoinFiltersRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        TotalPlusAge(),
                        new NumberReturning(new NumberScalar(120))
                    )
                )
            ),
            [InnerJoinOrdersToUsers()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    return o.OrderTotal + user.UserAge > 120;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    // eachAnd(eachGreaterThan(eachSubtract(user.age, order.total), -100),
    //         status == "shipped") - cross-entity arithmetic AND a plain,
    // same-side (order) string equality.
    [Fact]
    public void EachAndOfCrossEntityArithmeticComparisonAndOwnSideStringEquality()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachSubtract(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Users.Entity,
                                                            SampleDatabase.Users.Age
                                                        )
                                                    ),
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(-100))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
                                )
                            )
                        ),
                    ]
                )
            ),
            [InnerJoinOrdersToUsers()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserAge - o.OrderTotal > -100 && o.OrderStatus == "shipped";
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    // eachOr(user.active == false,
    //        eachGreaterThan(eachDateDiffDays(order.placed_on, user.signup_date), 1500))
    [Fact]
    public void EachOrOfJoinedBooleanEqualityAndCrossEntityDateDiffComparison()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachBooleanEquality(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Active
                                        )
                                    ),
                                    new BooleanReturning(new BooleanScalar(false))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachDateDiffDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.SignupDate
                                                )
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(1500))
                                )
                            )
                        ),
                    ]
                )
            ),
            [InnerJoinOrdersToUsers()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    int gap = o.PlacedOn.DayNumber - user.SignupDate.DayNumber;
                    return !user.UserActive || gap > 1500;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    // 4-level tree over joined columns:
    //   eachAnd(
    //     eachOr(user.active == false, eachGreaterThan(eachAdd(order.total, user.age), 300)),
    //     eachNot(status == "cancelled")
    //   )
    [Fact]
    public void FourLevelTreeOverJoinedColumnsMixingCrossEntityArithmeticAndBooleanOps()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachBooleanEquality(
                                                new BooleanArrayReturning(
                                                    new BooleanField(
                                                        SampleDatabase.Users.Entity,
                                                        SampleDatabase.Users.Active
                                                    )
                                                ),
                                                new BooleanReturning(
                                                    new BooleanScalar(false)
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachGreaterThan,
                                                TotalPlusAge(),
                                                new NumberReturning(
                                                    new NumberScalar(300)
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachNotOperator(
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachStringEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Status
                                                )
                                            ),
                                            new StringReturning(
                                                new StringScalar("cancelled")
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            ),
            [InnerJoinOrdersToUsers()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    bool orCondition =
                        !user.UserActive || o.OrderTotal + user.UserAge > 300;
                    return orCondition && o.OrderStatus != "cancelled";
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    // FROM shop.users LEFT JOIN shop.orders: eachGreaterThan(eachAdd(user.age,
    // order.total), 120). Eve and Fay place no orders, so their joined Total
    // is NULL; the cross-entity add propagates the NULL and the comparison
    // evaluates false (SQL 3VL), excluding those rows rather than throwing or
    // treating the missing side as zero.
    [Fact]
    public void EachLeftJoinWithCrossEntityArithmeticExcludesUnmatchedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Join usersToOrders = new Join(
            JoinType.Left,
            SampleDatabase.Orders.Entity,
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
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachAdd(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Age
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(120))
                    )
                )
            ),
            [usersToOrders],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows
                .GroupJoin(
                    db.OrderRows,
                    user => user.UserId,
                    order => order.OrderUserId,
                    (user, orders) => (user, orders)
                )
                .SelectMany(t => t.orders.DefaultIfEmpty(), (t, order) => (t.user, order))
                .Where(t => t.user.UserAge + (t.order?.OrderTotal) > 120)
                .Select(t => t.user.UserName)
                .OrderBy(name => name),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name).OrderBy(n => n)];

        Assert.NotEmpty(expected);
        // Eve and Fay place no orders; their NULL total must never
        // participate in a kept row, regardless of the threshold.
        Assert.DoesNotContain("Eve", expected);
        Assert.DoesNotContain("Fay", expected);
        Assert.Equal(expected, actual);
    }

    // eachAnd(user.active == true,
    //         eachGreaterThan(eachAdd(order.total, user.age), [100, junk]
    //         (broadcast)))  -- field, nested cross-entity arithmetic, and a
    // broadcast literal array, all combined over a join.
    [Fact]
    public void ThreeOperandShapesCombineOverJoinedColumnsInOnePredicate()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
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
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    TotalPlusAge(),
                                    new NumberArrayReturning(
                                        new NumberArrayScalar([100, -1])
                                    )
                                )
                            )
                        ),
                    ]
                )
            ),
            [InnerJoinOrdersToUsers()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserActive && o.OrderTotal + user.UserAge > 100;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }
}
