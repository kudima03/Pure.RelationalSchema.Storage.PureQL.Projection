using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Pagination is always the last pipeline stage (see RowsFromDatasets.Build), so
// "pagination after X" really means "paginate a query whose pipeline includes
// X". These tests exercise pagination windows over rows that GROUP BY, DISTINCT
// and JOIN have already reshaped, plus tie-stability under ORDER BY and the
// skip/take boundary cases not already covered by PaginationTests. Pagination
// after DISTINCT (single-column, ordered) and a plain inner-join pagination
// window are already covered by DistinctInteractionTests.DistinctAppliesBefore
// Pagination and JoinWithClausesTests.InnerJoinThenOrderByTotalWithPagination
// ReturnsWindow, so this file adds complementary, non-duplicate scenarios
// instead of repeating them.
[Trait("Clause", "Pagination")]
[Trait("Feature", "Pagination")]
public sealed class PaginationExpansionTests
{
    private static Join OrderItemsToProductsJoin()
    {
        return new Join(
            JoinType.Inner,
            "schema_with_foreign_keys.products",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.order_items",
                                "item_product_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.products",
                                "product_id"
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void PaginationAfterGroupByWindowsGroupProjectedRowsNotSourceRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
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
            join: null,
            [
                new Field(
                    new StringField(
                        "schema_with_foreign_keys.orders",
                        "order_status"
                    )
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            "schema_with_foreign_keys.orders",
                            "order_status"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] distinctGroups =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal),
        ];

        string[] expected = [.. distinctGroups.Skip(1).Take(1)];
        string?[] actual = [.. result.Column("order_status")];

        // The window addresses the 3 grouped rows, not the 6 source orders.
        Assert.True(distinctGroups.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterDistinctOnMultiColumnTuplesWindowsDeduplicatedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.users",
                                "user_active"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.users",
                            "user_age"
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new BooleanField(
                            "schema_with_foreign_keys.users",
                            "user_active"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 2),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (double Age, bool Active)[] expected =
        [
            .. userRows
                .OrderBy(user => user.UserAge)
                .ThenBy(user => user.UserActive)
                .Select(user => (user.UserAge, user.UserActive))
                .Distinct()
                .Skip(1)
                .Take(2),
        ];

        (double Age, bool Active)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row.Double("user_age")!.Value,
                    row.Bool("user_active")!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterJoinWindowsTheFullyJoinedAndFilteredRowSet()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.order_items",
                                "item_qty"
                            )
                        ),
                        new NumberReturning(new NumberScalar(1))
                    )
                )
            ),
            [OrderItemsToProductsJoin()],
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.order_items",
                            "item_qty"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderItemRows
                .Where(item => item.ItemQty > 1)
                .OrderBy(item => item.ItemQty)
                .Select(item => item.ItemQty)
                .Skip(1)
                .Take(1),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row =>
                row.Double("item_qty")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationWindowIsStableAndDeterministicAcrossRepeatedRunsWithTies()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // Orders 101 and 106 tie on Total (100.50), so a stable sort must keep
        // them in their original relative (insertion) order across runs.
        Assert.Equal(
            orderRows[0].OrderTotal,
            orderRows.Single(order => order.OrderId == Id(101)).OrderTotal
        );

        Query BuildQuery()
        {
            return new Query(
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
                join: null,
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
                new ModelPagination(2, 2)
            );
        }

        ProjectionResult firstRun = new ProjectionResult(
            new PureQLProjection(datasets, BuildQuery())
        );
        ProjectionResult secondRun = new ProjectionResult(
            new PureQLProjection(datasets, BuildQuery())
        );

        Guid[] expected = [Id(101), Id(106)];

        Guid[] firstRunIds =
        [
            .. firstRun.Rows.Select(row => row.Uuid("order_id")!.Value),
        ];
        Guid[] secondRunIds =
        [
            .. secondRun.Rows.Select(row => row.Uuid("order_id")!.Value),
        ];

        Assert.Equal(expected, firstRunIds);
        Assert.Equal(expected, secondRunIds);
        Assert.Equal(firstRunIds, secondRunIds);
    }

    // TakeBeyondEndReturnsAllRemainingRows and SkipBeyondEndReturnsNoRows in
    // PaginationTests.cs already cover skip=0/take-beyond-the-set full
    // passthrough and skip-beyond-the-set empty pages; not duplicated here.

    [Fact]
    public void NegativeSkipIsClampedToZeroInsteadOfThrowingOrWrapping()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // Pagination does not validate skip >= 0 at construction. RowsFromDatasets
        // clamps skip into [0, int.MaxValue] before calling Skip, so a negative
        // skip behaves exactly like skip = 0 rather than throwing or wrapping.
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
            join: null,
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
            new ModelPagination(-5, 3)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderTotal)
                .Take(3)
                .Select(order => (double?)order.OrderTotal),
        ];

        Assert.Equal(
            expected,
            [.. result.Rows.Select(row => row.Double("order_total"))]
        );
    }

    [Fact]
    public void NonPositiveTakeIsClampedToZeroYieldingAnEmptyPage()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        // Pagination does not validate take >= 1 at construction. A take of
        // zero or a negative value clamps to 0, so Take(0) yields an empty
        // page rather than throwing or returning every remaining row.
        Query zeroTakeQuery = new Query(
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            new ModelPagination(0, 0)
        );

        Query negativeTakeQuery = new Query(
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            new ModelPagination(3, -2)
        );

        ProjectionResult zeroTakeResult = new ProjectionResult(
            new PureQLProjection(datasets, zeroTakeQuery)
        );
        ProjectionResult negativeTakeResult = new ProjectionResult(
            new PureQLProjection(datasets, negativeTakeQuery)
        );

        Assert.Equal(0, zeroTakeResult.Count);
        Assert.Equal(0, negativeTakeResult.Count);
    }

    private static Guid Id(int seed)
    {
        return new Guid(seed, 0, 0, new byte[8]);
    }
}
