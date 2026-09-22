using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// `count` over every column type (Boolean, Date, DateTime, Number, Time;
// String/Uuid are already covered in CountTests.cs), both grouped and over
// the whole set, plus the SQL NULL-exclusion semantics of count(column):
// NULLs are dropped, not counted as present rows.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "Count")]
public sealed class CountByColumnTypeTests
{
    [Fact]
    public void CountOfBooleanColumnGroupedByStockStatusProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.products"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            "schema_with_foreign_keys.products",
                                            "product_in_stock"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(
                        "schema_with_foreign_keys.products",
                        "product_in_stock"
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

        double[] expected =
        [
            .. productRows.GroupBy(product => product.ProductInStock)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfBooleanColumnOverAllProductsProjectsWholeSetRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.products"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            "schema_with_foreign_keys.products",
                                            "product_in_stock"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(productRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfDateColumnPerUserProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateArrayReturning(
                                        new DateField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_on"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
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

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfDateColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateArrayReturning(
                                        new DateField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_on"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfDateTimeColumnPerUserProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_at"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
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

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfDateTimeColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_at"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfNumberColumnPerUserProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
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

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfNumberColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfTimeColumnGroupedByActiveStatusProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new TimeArrayReturning(
                                        new TimeField(
                                            "schema_with_foreign_keys.users",
                                            "shift_start"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField("schema_with_foreign_keys.users", "user_active")
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.GroupBy(user => user.UserActive)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfTimeColumnOverAllUsersProjectsWholeSetRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new TimeArrayReturning(
                                        new TimeField(
                                            "schema_with_foreign_keys.users",
                                            "shift_start"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(userRows.Count, result.Row(0).Double("n"));
    }

    // SQL semantics: count(column) counts non-NULL values, not row presence.
    // Users.Score is NULL for Bob and Dan (issue #103 fixture), so the
    // expected count is 4, not the full row count of 6.
    [Fact]
    public void CountOfNullableScoreColumnExcludesNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.users",
                                            "user_score"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            userRows.Count(user => user.UserScore is not null),
            result.Row(0).Double("n")
        );
    }

    // Grouped cross-check of the same NULL-exclusion behaviour: Bob (Active =
    // false, Score = null) and Dan (Active = true, Score = null) each drop
    // out of their group's count, leaving Active=true at 3 (Ann, Cara, Fay)
    // and Active=false at 1 (Eve).
    [Fact]
    public void CountOfNullableScoreColumnGroupedByActiveExcludesNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.users",
                                            "user_score"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField("schema_with_foreign_keys.users", "user_active")
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.GroupBy(user => user.UserActive)
                .Select(group => (double)group.Count(user => user.UserScore is not null))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
