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
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Rounds out the GroupBy suite with pipeline combinations not already covered
// by GroupByOrderByTests, CrossEntityGroupByTests, HavingCompositeTests or
// Combined/AggregatePipelineTests: ordering + pagination over the
// group-projected rows, a GROUP BY key sourced from a joined (QualifiedColumn)
// table, a NULL-valued group key produced by an unmatched LEFT JOIN row, an
// empty group set that still carries a HAVING clause, and HAVING conditions
// nested 3+ levels deep.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByPipeline")]
public sealed class GroupByPipelineTests
{
    private static Join OrdersToUsersInnerJoin()
    {
        return new Join(
            JoinType.Inner,
            new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
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
                                ).TextValue,
                                new UserIdColumn().Name.TextValue
                            )
                        )
                    )
                )
            )
        );
    }

    private static Join UsersToOrdersLeftJoin()
    {
        return new Join(
            JoinType.Left,
            new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
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
    }

    private static NumberReturning OrderCount()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderIdColumn().Name.TextValue
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning Totals()
    {
        return new NumberArrayReturning(
            new NumberField(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue,
                new OrderTotalColumn().Name.TextValue
            )
        );
    }

    private static NumberReturning MaxTotal()
    {
        return new NumberReturning(new NumberAggregate(new MaxNumber(Totals())));
    }

    private static NumberReturning MinTotal()
    {
        return new NumberReturning(new NumberAggregate(new MinNumber(Totals())));
    }

    private static BooleanReturning CountGreaterThan(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning MaxTotalAtLeast(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
                    MaxTotal(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning MinTotalAtLeast(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
                    MinTotal(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static Query OrdersGroupedByUser(BooleanReturning having)
    {
        return new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
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
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
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
            ],
            having,
            orderBy: null,
            pagination: null
        );
    }

    // GROUP BY the age key with no HAVING, ordered by the key itself
    // descending, then a pagination window taken over the group-projected
    // rows: exact ordered window is asserted (not just membership).
    [Fact]
    public void GroupByAgeDescOrderThenPaginateReturnsExactOrderedWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
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
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NumberField(new JoinedString(
                        new DotString(),
                        [
                            new RelationalSchemaWithForeignKeys().Name,
                            new UsersTable().Name,
                        ]
                    ).TextValue, new UserAgeColumn().Name.TextValue)
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
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
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(0, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge)
                .Distinct()
                .OrderByDescending(age => age)
                .Take(2),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    // Skipping past every distinct key once ordered leaves an empty, but
    // still valid, paginated window.
    [Fact]
    public void GroupByOrderByThenPaginateSkippingPastAllGroupsReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
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
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NumberField(new JoinedString(
                        new DotString(),
                        [
                            new RelationalSchemaWithForeignKeys().Name,
                            new UsersTable().Name,
                        ]
                    ).TextValue, new UserAgeColumn().Name.TextValue)
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
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
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(50, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // GROUP BY key sourced from the joined side (Users.Active is tagged as a
    // QualifiedColumn because Users is the joined table here, not the FROM
    // table), aggregating base-table (Orders) values per group.
    [Fact]
    public void GroupByJoinedBooleanKeyAggregatesBaseTableTotalsPerSide()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserActiveColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberAggregate(new SumNumber(Totals())))
                    ),
                    "totalByActive"
                ),
            ],
            where: null,
            [OrdersToUsersInnerJoin()],
            [
                new Field(
                    new BooleanField(
                        new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new UsersTable().Name,
                            ]
                        ).TextValue,
                        new UserActiveColumn().Name.TextValue
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, double> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserActive, order.OrderTotal)
            )
            .GroupBy(pair => pair.UserActive)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.OrderTotal)
            );

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("totalByActive")!.Value
        );

        Assert.Equal(expected, actual);
    }

    // LEFT JOIN Orders onto Users: unmatched users (no orders) expose the
    // joined Orders.Total column as a NULL cell (CellValueExtractor parses
    // the padded empty cell back to a real null for a numeric field).
    // Grouping by that joined key collapses every unmatched row into a
    // single NULL-keyed group, distinct from the matched, per-total groups.
    [Fact]
    public void LeftJoinGroupByJoinedTotalKeyPlacesUnmatchedUsersInOwnNullGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
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
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
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
                                    )
                                )
                            )
                        )
                    ),
                    "ageSum"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            [
                new Field(
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
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> matchedUserIds =
        [
            .. orderRows.Select(order => order.OrderUserId),
        ];

        int distinctTotals = orderRows
            .Select(order => order.OrderTotal)
            .Distinct()
            .Count();

        double unmatchedAgeSum = userRows
            .Where(user => !matchedUserIds.Contains(user.UserId))
            .Sum(user => user.UserAge);

        // One group per distinct matched total, plus exactly one group for
        // every unmatched (NULL-total) user.
        Assert.Equal(distinctTotals + 1, result.Count);

        double? nullGroupAgeSum = result.Rows
            .Where(row => row.Double(new OrderTotalColumn().Name.TextValue) is null)
            .Select(row => row.Double("ageSum"))
            .SingleOrDefault();

        Assert.Equal(unmatchedAgeSum, nullGroupAgeSum);
    }

    // WHERE eliminates every row before GROUP BY runs, and a HAVING clause
    // is still present: the whole-set/grouped pipeline must short-circuit to
    // zero groups rather than evaluating HAVING against an empty group.
    [Fact]
    public void WhereEliminatesAllRowsGroupByWithHavingStillReturnsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        ),
                        new StringReturning(new StringScalar("no-such-status"))
                    )
                )
            ),
            join: null,
            [
                new Field(
                    new StringField(
                        new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new OrdersTable().Name,
                            ]
                        ).TextValue,
                        new OrderStatusColumn().Name.TextValue
                    )
                ),
            ],
            CountGreaterThan(0),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // HAVING nested 4 levels deep: NOT( OR( AND(countGt, maxAtLeast),
    // NOT(minAtLeast) ) ).
    [Fact]
    public void HavingFourLevelNestedNotOrAndNotFiltersGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        BooleanReturning innerAnd = new BooleanReturning(
            new BooleanOperator(
                new AndOperator([CountGreaterThan(1), MaxTotalAtLeast(200)])
            )
        );

        BooleanReturning innerNot = new BooleanReturning(
            new BooleanOperator(new NotOperator(MinTotalAtLeast(100)))
        );

        BooleanReturning or = new BooleanReturning(
            new BooleanOperator(new OrOperator([innerAnd, innerNot]))
        );

        BooleanReturning having = new BooleanReturning(
            new BooleanOperator(new NotOperator(or))
        );

        Query query = OrdersGroupedByUser(having);

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                !(
                    (group.Count() > 1 && group.Max(order => order.OrderTotal) >= 200)
                    || !(group.Min(order => order.OrderTotal) >= 100)
                )
            );

        Assert.Equal(expected, result.Count);
    }

    // HAVING nested 3 levels deep with a different shape: AND( OR(a, NOT(b)),
    // OR(NOT(c), d) ).
    [Fact]
    public void HavingThreeLevelNestedAndOfTwoOrClausesFiltersGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        BooleanReturning leftOr = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        CountGreaterThan(2),
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(MaxTotalAtLeast(300)))
                        ),
                    ]
                )
            )
        );

        BooleanReturning rightOr = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(CountGreaterThan(0)))
                        ),
                        MinTotalAtLeast(50),
                    ]
                )
            )
        );

        BooleanReturning having = new BooleanReturning(
            new BooleanOperator(new AndOperator([leftOr, rightOr]))
        );

        Query query = OrdersGroupedByUser(having);

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                (group.Count() > 2 || !(group.Max(order => order.OrderTotal) >= 300))
                && (!group.Any() || group.Min(order => order.OrderTotal) >= 50)
            );

        Assert.Equal(expected, result.Count);
    }
}
