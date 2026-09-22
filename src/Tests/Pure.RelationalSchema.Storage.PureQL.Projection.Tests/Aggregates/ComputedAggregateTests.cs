using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// An aggregate's argument may be any array-returning expression, including
// per-row (each*) computed values: sum/average/count fold the computed
// per-row results, and min/max fold per-row temporal diffs, not stored
// columns directly.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "ComputedAggregate")]
public sealed class ComputedAggregateTests
{
    private static Join ItemsToProductsJoin()
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

    private static Join UsersToOrdersJoin()
    {
        return new Join(
            JoinType.Inner,
            "schema_with_foreign_keys.orders",
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
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
                            )
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning QtyTimesPrice()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachMultiply(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.order_items",
                                "item_qty"
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.products",
                                "product_price"
                            )
                        ),
                    ]
                )
            )
        );
    }

    private static double PriceOf(
        IReadOnlyList<ProductRecord> productRows,
        Guid productId
    )
    {
        return productRows
            .Single(product => product.ProductId == productId)
            .ProductPrice;
    }

    [Fact]
    public void SumOfEachMultiplyProjectsPerGroupRevenue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.order_items"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.order_items",
                                "item_order_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(new SumNumber(QtyTimesPrice()))
                        )
                    ),
                    "revenue"
                ),
            ],
            where: null,
            [ItemsToProductsJoin()],
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.order_items",
                        "item_order_id"
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

        Dictionary<Guid, double> expected = orderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.ItemQty * PriceOf(productRows, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("item_order_id")!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachMultiplyProjectsPerGroupMean()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.order_items"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.order_items",
                                "item_order_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(new AverageNumber(QtyTimesPrice()))
                        )
                    ),
                    "meanLineValue"
                ),
            ],
            where: null,
            [ItemsToProductsJoin()],
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.order_items",
                        "item_order_id"
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

        Dictionary<Guid, double> expected = orderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(item =>
                    item.ItemQty * PriceOf(productRows, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("item_order_id")!.Value,
            row => row.Double("meanLineValue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfEachDateDiffDaysProjectsPerGroupSpan()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MaxNumber(
                                    new NumberArrayReturning(
                                        new EachDateDiffDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    "schema_with_foreign_keys.orders",
                                                    "placed_on"
                                                )
                                            ),
                                            new DateArrayReturning(
                                                new DateField(
                                                    "schema_with_foreign_keys.users",
                                                    "signup_date"
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxSpanDays"
                ),
            ],
            where: null,
            [UsersToOrdersJoin()],
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.users",
                        "user_id"
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

        Dictionary<Guid, double> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, Span: (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber))
            )
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Span));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("user_id")!.Value,
            row => row.Double("maxSpanDays")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfEachTimeDiffSecondsProjectsPerGroupValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MinNumber(
                                    new NumberArrayReturning(
                                        new EachTimeDiffSeconds(
                                            new TimeArrayReturning(
                                                new TimeField(
                                                    "schema_with_foreign_keys.users",
                                                    "shift_start"
                                                )
                                            ),
                                            new TimeReturning(new TimeScalar(origin))
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "minShiftGapSeconds"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(
                        "schema_with_foreign_keys.users",
                        "user_active"
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

        Dictionary<bool, double> expected = userRows
            .GroupBy(user => user.UserActive)
            .ToDictionary(
                group => group.Key,
                group => group.Min(user => (user.ShiftStart - origin).TotalSeconds)
            );

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool("user_active")!.Value,
            row => row.Double("minShiftGapSeconds")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfEachArithmeticCountsGroupRows()
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
                                "order_user_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            "schema_with_foreign_keys.orders",
                                                            "order_total"
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(1)
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "rowCount"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
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

        Dictionary<Guid, double> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("order_user_id")!.Value,
            row => row.Double("rowCount")!.Value
        );

        Assert.Equal(expected, actual);
    }
}
