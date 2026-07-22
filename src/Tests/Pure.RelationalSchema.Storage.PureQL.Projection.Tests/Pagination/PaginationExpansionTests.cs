using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            SampleDatabase.Products.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.ProductId
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Id
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            join: null,
            [
                new Field(
                    new StringField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
                    )
                ),
            ],
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] distinctGroups =
        [
            .. db.OrderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal),
        ];

        string[] expected = [.. distinctGroups.Skip(1).Take(1)];
        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        // The window addresses the 3 grouped rows, not the 6 source orders.
        Assert.True(distinctGroups.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterDistinctOnMultiColumnTuplesWindowsDeduplicatedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
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
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 2),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (double Age, bool Active)[] expected =
        [
            .. db.UserRows
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
                    row.Double(SampleDatabase.Users.Age)!.Value,
                    row.Bool(SampleDatabase.Users.Active)!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterJoinWindowsTheFullyJoinedAndFilteredRowSet()
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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.Qty
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
                            SampleDatabase.OrderItems.Entity,
                            SampleDatabase.OrderItems.Qty
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderItemRows
                .Where(item => item.ItemQty > 1)
                .OrderBy(item => item.ItemQty)
                .Select(item => item.ItemQty)
                .Skip(1)
                .Take(1),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row =>
                row.Double(SampleDatabase.OrderItems.Qty)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationWindowIsStableAndDeterministicAcrossRepeatedRunsWithTies()
    {
        SampleDatabase db = new SampleDatabase();

        // Orders 101 and 106 tie on Total (100.50), so a stable sort must keep
        // them in their original relative (insertion) order across runs.
        Assert.Equal(
            db.OrderRows[0].OrderTotal,
            db.OrderRows.Single(order => order.OrderId == Id(101)).OrderTotal
        );

        Query BuildQuery()
        {
            return new Query(
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
                join: null,
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
                new ModelPagination(2, 2)
            );
        }

        ProjectionResult firstRun = new ProjectionResult(
            new PureQLProjection(db.Datasets, BuildQuery())
        );
        ProjectionResult secondRun = new ProjectionResult(
            new PureQLProjection(db.Datasets, BuildQuery())
        );

        Guid[] expected = [Id(101), Id(106)];

        Guid[] firstRunIds =
        [
            .. firstRun.Rows.Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value),
        ];
        Guid[] secondRunIds =
        [
            .. secondRun.Rows.Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value),
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
        SampleDatabase db = new SampleDatabase();

        // Pagination does not validate skip >= 0 at construction. RowsFromDatasets
        // clamps skip into [0, int.MaxValue] before calling Skip, so a negative
        // skip behaves exactly like skip = 0 rather than throwing or wrapping.
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
            join: null,
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
            new ModelPagination(-5, 3)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double?[] expected =
        [
            .. db.OrderRows.OrderBy(order => order.OrderTotal)
                .Take(3)
                .Select(order => (double?)order.OrderTotal),
        ];

        Assert.Equal(
            expected,
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void NonPositiveTakeIsClampedToZeroYieldingAnEmptyPage()
    {
        SampleDatabase db = new SampleDatabase();

        // Pagination does not validate take >= 1 at construction. A take of
        // zero or a negative value clamps to 0, so Take(0) yields an empty
        // page rather than throwing or returning every remaining row.
        Query zeroTakeQuery = new Query(
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            new ModelPagination(0, 0)
        );

        Query negativeTakeQuery = new Query(
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            new ModelPagination(3, -2)
        );

        ProjectionResult zeroTakeResult = new ProjectionResult(
            new PureQLProjection(db.Datasets, zeroTakeQuery)
        );
        ProjectionResult negativeTakeResult = new ProjectionResult(
            new PureQLProjection(db.Datasets, negativeTakeQuery)
        );

        Assert.Equal(0, zeroTakeResult.Count);
        Assert.Equal(0, negativeTakeResult.Count);
    }

    private static Guid Id(int seed)
    {
        return new Guid(seed, 0, 0, new byte[8]);
    }
}
