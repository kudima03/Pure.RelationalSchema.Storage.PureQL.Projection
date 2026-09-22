using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING equality matrix (issue #141): SingleValueEquality over every
// comparable type (Number, Date, DateTime, Time, String), a boolean
// composite equality between two aggregate comparisons, and the Uuid
// fail-fast case. Orders are grouped by user for Number/Date/DateTime/
// String; Users are grouped by Active for Time. Expected surviving group
// keys are computed independently from the ground-truth record lists per
// SQL HAVING semantics.
[Trait("Clause", "Having")]
public sealed class HavingEqualityMatrixTests
{
    private static NumberReturning OrderCount()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            "schema_with_foreign_keys.orders",
                            "order_id"
                        )
                    )
                )
            )
        );
    }

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

    private static BooleanReturning CountGreaterThanOne()
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(1))
                )
            )
        );
    }

    private static BooleanReturning SumGreaterThan(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    SumTotal(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning MaxTotalGreaterThanOrEqual(double threshold)
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

    // ---- Number: count(Orders.Id) == constant ----

    [Fact]
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsMatchingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new NumberEquality(
                            OrderCount(),
                            new NumberReturning(new NumberScalar(2))
                        )
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
                .Where(group => group.Count() == 2)
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
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new NumberEquality(
                            OrderCount(),
                            new NumberReturning(new NumberScalar(5))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            5,
            orderRows.GroupBy(order => order.OrderUserId).Select(group => group.Count())
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Boolean composite: (count > k) == (aggregate > m) ----

    [Fact]
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndSumComparisonsKeepsMatchingTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new BooleanEquality(CountGreaterThanOne(), SumGreaterThan(150))
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
                    (group.Count() > 1) == (group.Sum(order => order.OrderTotal) > 150)
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
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndMaxComparisonsKeepsMatchingTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new BooleanEquality(
                            CountGreaterThanOne(),
                            MaxTotalGreaterThanOrEqual(300)
                        )
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
                    (group.Count() > 1)
                    == (group.Max(order => order.OrderTotal) >= 300)
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

    // ---- Date: max(Orders.PlacedOn) == constant ----

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 2);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new DateEquality(
                            MaxPlacedOn(),
                            new DateReturning(new DateScalar(threshold))
                        )
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
                .Where(group => group.Max(order => order.PlacedOn) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 1, 1);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new DateEquality(
                            MaxPlacedOn(),
                            new DateReturning(new DateScalar(threshold))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.PlacedOn))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- DateTime: max(Orders.PlacedAt) == constant ----

    [Fact]
    [Trait("Feature", "HavingDateTimeEquality")]
    public void HavingMaxPlacedAtEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 5, 14, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new DateTimeEquality(
                            MaxPlacedAt(),
                            new DateTimeReturning(new DateTimeScalar(threshold))
                        )
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
                .Where(group => group.Max(order => order.PlacedAt) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeEquality")]
    public void HavingMaxPlacedAtEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 1, 1, 0, 0, 0);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new DateTimeEquality(
                            MaxPlacedAt(),
                            new DateTimeReturning(new DateTimeScalar(threshold))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.PlacedAt))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Time: max(Users.ShiftStart) == constant, grouped by Active ----

    [Fact]
    [Trait("Feature", "HavingTimeEquality")]
    public void HavingMaxShiftStartEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(11, 30, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new TimeEquality(
                            MaxShiftStart(),
                            new TimeReturning(new TimeScalar(threshold))
                        )
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
                .Where(group => group.Max(user => user.ShiftStart) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool("user_active")!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeEquality")]
    public void HavingMaxShiftStartEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(0, 0, 0);

        Query query = UsersGroupedByActive(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new TimeEquality(
                            MaxShiftStart(),
                            new TimeReturning(new TimeScalar(threshold))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            userRows
                .GroupBy(user => user.UserActive)
                .Select(group => group.Max(user => user.ShiftStart))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- String: min(Orders.Status) == constant (ordinal) ----

    [Fact]
    [Trait("Feature", "HavingStringEquality")]
    public void HavingMinStatusEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new StringEquality(
                            MinStatus(),
                            new StringReturning(new StringScalar("cancelled"))
                        )
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
                    string.Equals(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "cancelled",
                        System.StringComparison.Ordinal
                    )
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingStringEquality")]
    public void HavingMinStatusEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new StringEquality(
                            MinStatus(),
                            new StringReturning(new StringScalar("unknown"))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            "unknown",
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group =>
                    group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal)
                )
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Uuid: no aggregate arm exists on UuidReturning (only Parameter and
    // Scalar - see PureQL.CSharp.Model.Returnings.UuidReturning), so "an
    // aggregate over a uuid column" cannot even be constructed; there is no
    // MinUuid/MaxUuid in the model. The closest HAVING-shaped probe is a
    // UuidParameter operand, which AggregateEvaluator.BuildUuid rejects with
    // its parameter-not-supported fail-fast (matching every other
    // parameterised HAVING/WHERE operand - see Parameters/ParameterTests.cs).
    // This is intentional, spec-correct fail-fast behaviour, not a gap.

    [Fact]
    [Trait("Feature", "HavingUuidEquality")]
    public void HavingUuidParameterEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new UuidEquality(
                            new UuidReturning(new UuidParameter("id")),
                            new UuidReturning(new UuidScalar(Guid.Empty))
                        )
                    )
                )
            )
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
