using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
                    new UuidField(
                        new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new OrdersTable().Name,
                            ]
                        ).TextValue, new OrderIdColumn().Name.TextValue)
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
                        new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new UsersTable().Name,
                            ]
                        ).TextValue,
                        new UserNameColumn().Name.TextValue
                    )
                )
            )
        );
    }

    private static Join InnerJoinOrdersToUsers()
    {
        return new Join(
            JoinType.Inner,
            new JoinedString(
                new DotString(),
                [
                    new RelationalSchemaWithForeignKeys().Name,
                    new UsersTable().Name,
                ]
            ).TextValue,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderUserIdColumn().Name.TextValue
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue, new UserIdColumn().Name.TextValue)
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
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserAgeColumn().Name.TextValue
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new OrdersTable().Name,
                    ]
                ).TextValue),
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return o.OrderTotal + user.UserAge > 120;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }

    // eachAnd(eachGreaterThan(eachSubtract(user.age, order.total), -100),
    //         status == "shipped") - cross-entity arithmetic AND a plain,
    // same-side (order) string equality.
    [Fact]
    public void EachAndOfCrossEntityArithmeticComparisonAndOwnSideStringEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new OrdersTable().Name,
                    ]
                ).TextValue),
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
                                                            new JoinedString(
                                                                new DotString(),
                                                                [
                                                                    new RelationalSchemaWithForeignKeys()
                                                                        .Name,
                                                                    new UsersTable().Name,
                                                                ]
                                                            ).TextValue,
                                                            new UserAgeColumn().Name.TextValue
                                                        )
                                                    ),
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            new JoinedString(
                                                                new DotString(),
                                                                [
                                                                    new RelationalSchemaWithForeignKeys()
                                                                        .Name,
                                                                    new OrdersTable()
                                                                        .Name,
                                                                ]
                                                            ).TextValue,
                                                            new OrderTotalColumn().Name.TextValue
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
                                            new JoinedString(
                                                new DotString(),
                                                [
                                                    new RelationalSchemaWithForeignKeys()
                                                        .Name,
                                                    new OrdersTable().Name,
                                                ]
                                            ).TextValue,
                                            new OrderStatusColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserAge - o.OrderTotal > -100 && o.OrderStatus == "shipped";
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }

    // eachOr(user.active == false,
    //        eachGreaterThan(eachDateDiffDays(order.placed_on, user.signup_date), 1500))
    [Fact]
    public void EachOrOfJoinedBooleanEqualityAndCrossEntityDateDiffComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new OrdersTable().Name,
                    ]
                ).TextValue),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachBooleanEquality(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            new JoinedString(
                                                new DotString(),
                                                [
                                                    new RelationalSchemaWithForeignKeys()
                                                        .Name,
                                                    new UsersTable().Name,
                                                ]
                                            ).TextValue,
                                            new UserActiveColumn().Name.TextValue
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
                                                    new JoinedString(
                                                        new DotString(),
                                                        [
                                                            new RelationalSchemaWithForeignKeys()
                                                                .Name,
                                                            new OrdersTable().Name,
                                                        ]
                                                    ).TextValue,
                                                    new PlacedOnColumn().Name.TextValue
                                                )
                                            ),
                                            new DateArrayReturning(
                                                new DateField(
                                                    new JoinedString(
                                                        new DotString(),
                                                        [
                                                            new RelationalSchemaWithForeignKeys()
                                                                .Name,
                                                            new UsersTable().Name,
                                                        ]
                                                    ).TextValue,
                                                    new SignupDateColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    int gap = o.PlacedOn.DayNumber - user.SignupDate.DayNumber;
                    return !user.UserActive || gap > 1500;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new OrdersTable().Name,
                    ]
                ).TextValue),
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
                                                        new JoinedString(
                                                            new DotString(),
                                                            [
                                                                new RelationalSchemaWithForeignKeys()
                                                                    .Name,
                                                                new UsersTable().Name,
                                                            ]
                                                        ).TextValue,
                                                        new UserActiveColumn().Name.TextValue
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
                                                    new JoinedString(
                                                        new DotString(),
                                                        [
                                                            new RelationalSchemaWithForeignKeys()
                                                                .Name,
                                                            new OrdersTable().Name,
                                                        ]
                                                    ).TextValue,
                                                    new OrderStatusColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
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
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Join usersToOrders = new Join(
            JoinType.Left,
            new JoinedString(
                new DotString(),
                [
                    new RelationalSchemaWithForeignKeys().Name,
                    new OrdersTable().Name,
                ]
            ).TextValue,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserIdColumn().Name.TextValue
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderUserIdColumn().Name.TextValue
                            )
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new UsersTable().Name,
                    ]
                ).TextValue),
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
                                                new JoinedString(
                                                    new DotString(),
                                                    [
                                                        new RelationalSchemaWithForeignKeys()
                                                            .Name,
                                                        new UsersTable().Name,
                                                    ]
                                                ).TextValue,
                                                new UserAgeColumn().Name.TextValue
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                new JoinedString(
                                                    new DotString(),
                                                    [
                                                        new RelationalSchemaWithForeignKeys()
                                                            .Name,
                                                        new OrdersTable().Name,
                                                    ]
                                                ).TextValue,
                                                new OrderTotalColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .GroupJoin(
                    orderRows,
                    user => user.UserId,
                    order => order.OrderUserId,
                    (user, orders) => (user, orders)
                )
                .SelectMany(t => t.orders.DefaultIfEmpty(), (t, order) => (t.user, order))
                .Where(t => t.user.UserAge + (t.order?.OrderTotal) > 120)
                .Select(t => t.user.UserName)
                .OrderBy(name => name),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue).OrderBy(n => n)];

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [
                        new RelationalSchemaWithForeignKeys().Name,
                        new OrdersTable().Name,
                    ]
                ).TextValue),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachBooleanEquality(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            new JoinedString(
                                                new DotString(),
                                                [
                                                    new RelationalSchemaWithForeignKeys()
                                                        .Name,
                                                    new UsersTable().Name,
                                                ]
                                            ).TextValue,
                                            new UserActiveColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserActive && o.OrderTotal + user.UserAge > 100;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }
}
