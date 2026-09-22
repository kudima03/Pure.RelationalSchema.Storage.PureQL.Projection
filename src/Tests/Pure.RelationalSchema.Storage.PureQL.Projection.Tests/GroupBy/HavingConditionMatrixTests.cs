using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using StringComparison = PureQL.CSharp.Model.Comparisons.StringComparison;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING comparison matrix (issue #141): all 4 ComparisonOperator values over
// Number, Date, DateTime, Time and String aggregates (plus Number
// aggregate-vs-aggregate), each type covering a threshold that keeps some,
// all or none of the groups across its 4 operators. Orders are grouped by
// user for Number/Date/DateTime/String; Users are grouped by Active for
// Time. Expected surviving group keys are computed independently from the
// ground-truth record lists per SQL HAVING semantics.
[Trait("Clause", "Having")]
public sealed class HavingConditionMatrixTests
{
    private static NumberArrayReturning Totals()
    {
        return new NumberArrayReturning(
            new NumberField("schema_with_foreign_keys.orders", "order_total")
        );
    }

    private static NumberReturning SumTotal()
    {
        return new NumberReturning(new NumberAggregate(new SumNumber(Totals())));
    }

    private static NumberReturning MaxTotal()
    {
        return new NumberReturning(new NumberAggregate(new MaxNumber(Totals())));
    }

    private static NumberReturning MinTotal()
    {
        return new NumberReturning(new NumberAggregate(new MinNumber(Totals())));
    }

    private static DateArrayReturning PlacedOns()
    {
        return new DateArrayReturning(
            new DateField("schema_with_foreign_keys.orders", "placed_on")
        );
    }

    private static DateReturning MaxPlacedOn()
    {
        return new DateReturning(new DateAggregate(new MaxDate(PlacedOns())));
    }

    private static DateTimeArrayReturning PlacedAts()
    {
        return new DateTimeArrayReturning(
            new DateTimeField(
                "schema_with_foreign_keys.orders",
                "placed_at"
            )
        );
    }

    private static DateTimeReturning MaxPlacedAt()
    {
        return new DateTimeReturning(
            new DateTimeAggregate(new MaxDateTime(PlacedAts()))
        );
    }

    private static StringArrayReturning Statuses()
    {
        return new StringArrayReturning(
            new StringField("schema_with_foreign_keys.orders", "order_status")
        );
    }

    private static StringReturning MinStatus()
    {
        return new StringReturning(new StringAggregate(new MinString(Statuses())));
    }

    private static TimeArrayReturning ShiftStarts()
    {
        return new TimeArrayReturning(
            new TimeField("schema_with_foreign_keys.users", "shift_start")
        );
    }

    private static TimeReturning MaxShiftStart()
    {
        return new TimeReturning(new TimeAggregate(new MaxTime(ShiftStarts())));
    }

    private static Query OrdersGroupedByUser(BooleanReturning having)
    {
        return new Query(
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
            having,
            orderBy: null,
            pagination: null
        );
    }

    private static Query UsersGroupedByActive(BooleanReturning having)
    {
        return new Query(
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
            having,
            orderBy: null,
            pagination: null
        );
    }

    // ---- Number: sum(Orders.Total) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        SumTotal(),
                        new NumberReturning(new NumberScalar(150))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        SumTotal(),
                        new NumberReturning(new NumberScalar(100.50))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThan,
                        SumTotal(),
                        new NumberReturning(new NumberScalar(50))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThanOrEqual,
                        SumTotal(),
                        new NumberReturning(new NumberScalar(150.50))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) <= 150.50)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Number: max(Orders.Total) <op> min(Orders.Total) (aggregate-vs-aggregate) ----

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanMinTotalKeepsMultiOrderGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        MaxTotal(),
                        MinTotal()
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    > group.Min(order => order.OrderTotal)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanOrEqualMinTotalKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MaxTotal(),
                        MinTotal()
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanMinTotalKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThan,
                        MaxTotal(),
                        MinTotal()
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanOrEqualMinTotalKeepsSingleOrderGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThanOrEqual,
                        MaxTotal(),
                        MinTotal()
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    <= group.Min(order => order.OrderTotal)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Date: max(Orders.PlacedOn) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.GreaterThan,
                        MaxPlacedOn(),
                        new DateReturning(new DateScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 1);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MaxPlacedOn(),
                        new DateReturning(new DateScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        DateOnly threshold = new DateOnly(2024, 6, 1);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.LessThan,
                        MaxPlacedOn(),
                        new DateReturning(new DateScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.LessThanOrEqual,
                        MaxPlacedOn(),
                        new DateReturning(new DateScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- DateTime: max(Orders.PlacedAt) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.GreaterThan,
                        MaxPlacedAt(),
                        new DateTimeReturning(new DateTimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 1, 0, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MaxPlacedAt(),
                        new DateTimeReturning(new DateTimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        DateTime threshold = new DateTime(2024, 6, 1, 0, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.LessThan,
                        MaxPlacedAt(),
                        new DateTimeReturning(new DateTimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.LessThanOrEqual,
                        MaxPlacedAt(),
                        new DateTimeReturning(new DateTimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Time: max(Users.ShiftStart) <op> constant, grouped by Active ----

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.GreaterThan,
                        MaxShiftStart(),
                        new TimeReturning(new TimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool("user_active")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < userRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MaxShiftStart(),
                        new TimeReturning(new TimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = userRows.Select(user => user.UserActive).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        TimeOnly threshold = new TimeOnly(8, 0, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.LessThan,
                        MaxShiftStart(),
                        new TimeReturning(new TimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.LessThanOrEqual,
                        MaxShiftStart(),
                        new TimeReturning(new TimeScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool("user_active")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < userRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- String: min(Orders.Status) <op> constant (ordinal) ----

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new StringComparison(
                        ComparisonOperator.GreaterThan,
                        MinStatus(),
                        new StringReturning(new StringScalar("pending"))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) > 0
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new StringComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MinStatus(),
                        new StringReturning(new StringScalar("cancelled"))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new StringComparison(
                        ComparisonOperator.LessThan,
                        MinStatus(),
                        new StringReturning(new StringScalar("cancelled"))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new StringComparison(
                        ComparisonOperator.LessThanOrEqual,
                        MinStatus(),
                        new StringReturning(new StringScalar("pending"))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) <= 0
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }
}
