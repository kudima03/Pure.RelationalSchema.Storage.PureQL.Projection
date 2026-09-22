using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row each* predicates whose right operand is a *literal array* (an
// X ArrayScalar), for the five element types not already covered by
// EachArrayOperandTests/EachBroadcastAndLiteralTests (Number, String): Bool,
// Date, DateTime, Time, Uuid. WhereExpressionBuilder.Build<Type>ArrayValuePerRow
// resolves a literal array operand with `.FirstOrDefault()` - every row
// evaluation uses only the literal's first element, broadcast to every row
// regardless of the literal's declared length (ratified behaviour, see
// Semantics/README.md "Literal-array each* operand vs. row count"). Every
// query here is constructed explicitly and inline, and expected surviving
// rows are computed independently from the ground-truth record lists.
[Trait("Clause", "Where")]
[Trait("Feature", "EachLiteralOperand")]
public sealed class EachLiteralOperandTypeMatrixTests
{
    private static SelectExpression ProductNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField("schema_with_foreign_keys.products", "product_name")
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

    // ===== Boolean: Products.InStock (Widget=true, Gadget=false, =====
    // ===== Gizmo=true, Deluxe=true)                                =====

    [Fact]
    public void EachEqualBooleanLiteralArrayKeepsRowsMatchingTheFirstElement()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.products"),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.products",
                                "product_in_stock"
                            )
                        ),
                        new BooleanArrayReturning(new BooleanArrayScalar([true]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            productRows.Count(product => product.ProductInStock),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 5-element literal ([false, true, true, true, true]) broadcasts only
    // its first element (false) to every row, regardless of its own length
    // (5) or the table's row count (4): every row is checked against
    // `InStock == false`, keeping only Gadget. If the trailing `true`
    // elements had any effect, more rows would survive.
    [Fact]
    public void EachEqualBooleanLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.products"),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.products",
                                "product_in_stock"
                            )
                        ),
                        new BooleanArrayReturning(
                            new BooleanArrayScalar([false, true, true, true, true])
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            productRows.Count(product => !product.ProductInStock),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Gadget", Assert.Single(result.Column("product_name")));
    }

    // eachNot wraps a literal-operand eachEqual, showing the broadcast first
    // element (true) still applies under negation: keeps rows whose InStock
    // is NOT true, i.e. only Gadget.
    [Fact]
    public void EachNotOfBooleanLiteralArrayEqualityKeepsRowsNotMatchingFirstElement()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.products"),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachBooleanEquality(
                                new BooleanArrayReturning(
                                    new BooleanField(
                                        "schema_with_foreign_keys.products",
                                        "product_in_stock"
                                    )
                                ),
                                new BooleanArrayReturning(new BooleanArrayScalar([true]))
                            )
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            productRows.Count(product => !product.ProductInStock),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Gadget", Assert.Single(result.Column("product_name")));
    }

    // ===== Uuid: Orders.Id (each order id is distinct) =====

    [Fact]
    public void EachEqualUuidLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Guid target = orderRows.Single(order => order.OrderTotal == 200.00).OrderId;

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField("schema_with_foreign_keys.orders", "order_id")
                        ),
                        new UuidArrayReturning(new UuidArrayScalar([target]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderId == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal(200.00, result.Row(0).Double("order_total"));
    }

    // A 3-element literal whose first element is the target order id and
    // whose remaining two point at other, non-matching orders still filters
    // purely on the first element, keeping only the target's row.
    [Fact]
    public void EachEqualUuidLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Guid target = orderRows.Single(order => order.OrderTotal == 200.00).OrderId;
        Guid decoyOne = orderRows.Single(order => order.OrderTotal == 50.00).OrderId;
        Guid decoyTwo = orderRows.Single(order => order.OrderTotal == 75.25).OrderId;

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField("schema_with_foreign_keys.orders", "order_id")
                        ),
                        new UuidArrayReturning(
                            new UuidArrayScalar([target, decoyOne, decoyTwo])
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderId == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal(200.00, result.Row(0).Double("order_total"));
    }

    // ===== Date: Orders.PlacedOn (2024-06-01 .. 2024-06-06, one per row) =====

    [Fact]
    public void EachEqualDateLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly target = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.orders",
                                "placed_on"
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([target]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    // A 3-element literal whose first element is 2024-06-01 and whose
    // remaining two (2024-06-02, 2024-06-03) each match a different order
    // still filters purely on the first element, keeping only the
    // 2024-06-01 order.
    [Fact]
    public void EachEqualDateLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly target = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.orders",
                                "placed_on"
                            )
                        ),
                        new DateArrayReturning(
                            new DateArrayScalar(
                                [
                                    target,
                                    new DateOnly(2024, 6, 2),
                                    new DateOnly(2024, 6, 3),
                                ]
                            )
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void EachGreaterThanDateLiteralArrayFiltersLaterDates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.orders",
                                "placed_on"
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([threshold]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn > threshold),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 2-element literal ([2024-06-03, 2024-06-01]) broadcasts only its
    // first element under `eachLessThan` too: every row is checked against
    // `PlacedOn < 2024-06-03`. If the trailing 2024-06-01 element had any
    // effect (e.g. as a second per-row-zipped value), the surviving count
    // would differ (comparing against 2024-06-01 keeps zero rows).
    [Fact]
    public void EachLessThanDateLiteralArrayBroadcastsFirstElementUnderComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachLessThan,
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.orders",
                                "placed_on"
                            )
                        ),
                        new DateArrayReturning(
                            new DateArrayScalar([threshold, new DateOnly(2024, 6, 1)])
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn < threshold),
            result.Count
        );
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EachEqualDateLiteralArrayWithNoMatchesReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        DateOnly target = new DateOnly(2099, 1, 1);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.orders",
                                "placed_on"
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([target]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // ===== DateTime: Orders.PlacedAt =====
    // ===== (2024-06-01T10:00 .. 2024-06-06T15:00, one per row) =====

    [Fact]
    public void EachEqualDateTimeLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime target = new DateTime(2024, 6, 1, 10, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateTimeEquality(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.orders",
                                "placed_at"
                            )
                        ),
                        new DateTimeArrayReturning(new DateTimeArrayScalar([target]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedAt == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    // A 2-element literal whose first element is the target instant and
    // whose second element (2024-06-02T11:00) matches a different order
    // still filters purely on the first element.
    [Fact]
    public void EachEqualDateTimeLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime target = new DateTime(2024, 6, 1, 10, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateTimeEquality(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.orders",
                                "placed_at"
                            )
                        ),
                        new DateTimeArrayReturning(
                            new DateTimeArrayScalar(
                                [target, new DateTime(2024, 6, 2, 11, 0, 0)]
                            )
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedAt == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void EachGreaterThanDateTimeLiteralArrayFiltersLaterInstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateTimeComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.orders",
                                "placed_at"
                            )
                        ),
                        new DateTimeArrayReturning(new DateTimeArrayScalar([threshold]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedAt > threshold),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // ===== Time: Users.ShiftStart =====
    // ===== (Ann 09:00, Bob 10:00, Cara 09:00, Dan 11:30, Eve 08:00, =====
    // ===== Fay 09:00)                                               =====

    [Fact]
    public void EachEqualTimeLiteralArrayKeepsRowsMatchingTheFirstElement()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly target = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachTimeEquality(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([target]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart == target),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 3-element literal whose first element is 09:00 and whose remaining
    // two (11:30, 08:00) each match a different, non-09:00 user still
    // filters purely on the first element, keeping only the three 09:00
    // users.
    [Fact]
    public void EachEqualTimeLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly target = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachTimeEquality(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        ),
                        new TimeArrayReturning(
                            new TimeArrayScalar(
                                [target, new TimeOnly(11, 30, 0), new TimeOnly(8, 0, 0)]
                            )
                        )
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart == target),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void EachLessThanTimeLiteralArrayFiltersEarlierTimes()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachLessThan,
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([threshold]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart < threshold),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Eve", Assert.Single(result.Column("user_name")));
    }

    [Fact]
    public void EachGreaterThanOrEqualTimeLiteralArrayIncludesTheThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachGreaterThanOrEqual,
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([threshold]))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart >= threshold),
            result.Count
        );
        Assert.Equal(5, result.Count);
    }
}
