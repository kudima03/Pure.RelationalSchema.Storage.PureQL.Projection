using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachDateTimeArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Matrix of aggregate (numeric sum/avg/count, temporal min/max) over
// temporal each-expression arguments (eachDateDiffDays/eachTimeDiffSeconds/
// eachDateTimeDiffSeconds and eachDateAddDays/eachTimeAddSeconds/
// eachDateTimeAddSeconds), across group-key types (uuid/string/bool/number)
// and whole-set, plus HAVING and the temporal-average fail-fast. Numeric
// each-arithmetic combos live in AggregateOverExpressionComboTests.cs. Every
// expected value is computed independently in LINQ over the ground-truth
// record lists; none of the fields used here (signup_date, placed_on,
// placed_at, shift_start) carry NULLs in the fixture, so every diff/add is
// always defined.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "AggregateOverExpressionCombo")]
public sealed class AggregateOverExpressionComboTemporalTests
{
    private static Join OrdersToUsersJoin()
    {
        return new Join(
            JoinType.Inner,
            "schema_with_foreign_keys.users",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        )
                    )
                )
            )
        );
    }

    // ===== each-temporal-diff arguments (return NumberArrayReturning) =====

    private static NumberArrayReturning PlacedOnMinusSignupDate()
    {
        return new NumberArrayReturning(
            new EachDateDiffDays(
                new DateArrayReturning(
                    new DateField("schema_with_foreign_keys.orders", "placed_on")
                ),
                new DateArrayReturning(
                    new DateField("schema_with_foreign_keys.users", "signup_date")
                )
            )
        );
    }

    private static NumberArrayReturning ShiftStartMinusOrigin(TimeOnly origin)
    {
        return new NumberArrayReturning(
            new EachTimeDiffSeconds(
                new TimeArrayReturning(
                    new TimeField("schema_with_foreign_keys.users", "shift_start")
                ),
                new TimeReturning(new TimeScalar(origin))
            )
        );
    }

    private static NumberArrayReturning PlacedAtMinusLastLogin()
    {
        return new NumberArrayReturning(
            new EachDateTimeDiffSeconds(
                new DateTimeArrayReturning(
                    new DateTimeField("schema_with_foreign_keys.orders", "placed_at")
                ),
                new DateTimeArrayReturning(
                    new DateTimeField("schema_with_foreign_keys.users", "last_login")
                )
            )
        );
    }

    // ===== each-temporal-add arguments =====

    private static DateArrayReturning SignupDatePlusDays(double days)
    {
        return new DateArrayReturning(
            new EachDateAddDays(
                new DateArrayReturning(
                    new DateField("schema_with_foreign_keys.users", "signup_date")
                ),
                new NumberReturning(new NumberScalar(days))
            )
        );
    }

    private static TimeArrayReturning ShiftStartPlusSeconds(double seconds)
    {
        return new TimeArrayReturning(
            new EachTimeAddSeconds(
                new TimeArrayReturning(
                    new TimeField("schema_with_foreign_keys.users", "shift_start")
                ),
                new NumberReturning(new NumberScalar(seconds))
            )
        );
    }

    private static DateTimeArrayReturning PlacedAtPlusSeconds(double seconds)
    {
        return new DateTimeArrayReturning(
            new EachDateTimeAddSeconds(
                new DateTimeArrayReturning(
                    new DateTimeField("schema_with_foreign_keys.orders", "placed_at")
                ),
                new NumberReturning(new NumberScalar(seconds))
            )
        );
    }

    // ===== select / group-by builders =====

    private static SelectExpression NumberAggregateSelect(
        NumberAggregate aggregate,
        string alias
    )
    {
        return new SelectExpression(
            new SingleValueReturning(new NumberReturning(aggregate)),
            alias
        );
    }

    private static SelectExpression DateAggregateSelect(DateAggregate aggregate, string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(new DateReturning(aggregate)),
            alias
        );
    }

    private static SelectExpression TimeAggregateSelect(TimeAggregate aggregate, string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(new TimeReturning(aggregate)),
            alias
        );
    }

    private static SelectExpression DateTimeAggregateSelect(
        DateTimeAggregate aggregate,
        string alias
    )
    {
        return new SelectExpression(
            new SingleValueReturning(new DateTimeReturning(aggregate)),
            alias
        );
    }

    private static NumberReturning MaxOf(NumberArrayReturning argument)
    {
        return new NumberReturning(new NumberAggregate(new MaxNumber(argument)));
    }

    private static SelectExpression CountSelect(NumberArrayReturning argument, string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(
                new NumberReturning(new Count(new ArrayReturning(argument)))
            ),
            alias
        );
    }

    private static SelectExpression UuidGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new UuidArrayReturning(new UuidField(entity, field)))
        );
    }

    private static SelectExpression StringGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new StringArrayReturning(new StringField(entity, field)))
        );
    }

    private static SelectExpression BoolGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new BooleanArrayReturning(new BooleanField(entity, field)))
        );
    }

    private static SelectExpression NumberGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new NumberArrayReturning(new NumberField(entity, field)))
        );
    }

    private static Field UuidGroupKeyField(string entity, string field)
    {
        return new Field(new UuidField(entity, field));
    }

    private static Field StringGroupKeyField(string entity, string field)
    {
        return new Field(new StringField(entity, field));
    }

    private static Field BoolGroupKeyField(string entity, string field)
    {
        return new Field(new BooleanField(entity, field));
    }

    // ===== E: numeric aggregates over each-temporal-diff =====

    [Fact]
    public void SumOfEachDateDiffDaysGroupedByOrderUserIdComputesTotalSpanDays()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                UuidGroupKeySelect("schema_with_foreign_keys.orders", "order_user_id"),
                NumberAggregateSelect(
                    new NumberAggregate(new SumNumber(PlacedOnMinusSignupDate())),
                    "totalSpanDays"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [UuidGroupKeyField("schema_with_foreign_keys.orders", "order_user_id")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new
            {
                order.OrderUserId,
                Span = (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber),
            }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Span));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("order_user_id")!.Value,
            row => row.Double("totalSpanDays")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachDateDiffDaysGroupedByOrderStatusComputesMeanSpanDays()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                StringGroupKeySelect("schema_with_foreign_keys.orders", "order_status"),
                NumberAggregateSelect(
                    new NumberAggregate(new AverageNumber(PlacedOnMinusSignupDate())),
                    "meanSpanDays"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [StringGroupKeyField("schema_with_foreign_keys.orders", "order_status")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new
            {
                order.OrderStatus,
                Span = (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber),
            }
        )
            .GroupBy(x => x.OrderStatus)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Span));

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row["order_status"]!,
            row => row.Double("meanSpanDays")!.Value
        );

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinAndMaxOfEachDateDiffDaysGroupedByUserActiveBoundSpanDays()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                BoolGroupKeySelect("schema_with_foreign_keys.users", "user_active"),
                NumberAggregateSelect(
                    new NumberAggregate(new MinNumber(PlacedOnMinusSignupDate())),
                    "minSpanDays"
                ),
                NumberAggregateSelect(
                    new NumberAggregate(new MaxNumber(PlacedOnMinusSignupDate())),
                    "maxSpanDays"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [BoolGroupKeyField("schema_with_foreign_keys.users", "user_active")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        IReadOnlyList<(bool Active, double Span)> spans =
        [
            .. from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select (
                user.UserActive,
                Span: (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber)
            ),
        ];

        Dictionary<bool, double> expectedMin = spans
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Span));

        Dictionary<bool, double> expectedMax = spans
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Span));

        Dictionary<bool, double> actualMin = result.Rows.ToDictionary(
            row => row.Bool("user_active")!.Value,
            row => row.Double("minSpanDays")!.Value
        );

        Dictionary<bool, double> actualMax = result.Rows.ToDictionary(
            row => row.Bool("user_active")!.Value,
            row => row.Double("maxSpanDays")!.Value
        );

        Assert.Equal(expectedMin, actualMin);
        Assert.Equal(expectedMax, actualMax);
    }

    [Fact]
    public void CountOfEachDateDiffDaysGroupedByUserAgeCountsDefinedSpans()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                NumberGroupKeySelect("schema_with_foreign_keys.users", "user_age"),
                CountSelect(PlacedOnMinusSignupDate(), "spanCount"),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [new Field(new NumberField("schema_with_foreign_keys.users", "user_age"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<double, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select user.UserAge
        )
            .GroupBy(age => age)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        Dictionary<double, double> actual = result.Rows.ToDictionary(
            row => row.Double("user_age")!.Value,
            row => row.Double("spanCount")!.Value
        );

        // Ann and Cara share age 30: their four combined orders land in one
        // group, so this pins count-over-each on a merged multi-user group.
        Assert.Equal(4, expected[30]);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SumOfEachTimeDiffSecondsWholeSetComputesTotalShiftGap()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                NumberAggregateSelect(
                    new NumberAggregate(new SumNumber(ShiftStartMinusOrigin(origin))),
                    "totalGapSeconds"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expected = userRows.Sum(user => (user.ShiftStart - origin).TotalSeconds);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("totalGapSeconds"));
    }

    [Fact]
    public void AverageOfEachDateTimeDiffSecondsGroupedByOrderUserIdComputesMeanGapSeconds()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                UuidGroupKeySelect("schema_with_foreign_keys.orders", "order_user_id"),
                NumberAggregateSelect(
                    new NumberAggregate(new AverageNumber(PlacedAtMinusLastLogin())),
                    "meanGapSeconds"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [UuidGroupKeyField("schema_with_foreign_keys.orders", "order_user_id")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new
            {
                order.OrderUserId,
                Gap = (order.PlacedAt - user.LastLogin).TotalSeconds,
            }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Gap));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("order_user_id")!.Value,
            row => row.Double("meanGapSeconds")!.Value
        );

        Assert.Equal(expected, actual);
    }

    // ===== F: temporal min/max over each-temporal-add =====

    [Fact]
    public void MaxOfEachDateAddDaysGroupedByUserActiveFindsLatestProjectedDate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                BoolGroupKeySelect("schema_with_foreign_keys.users", "user_active"),
                DateAggregateSelect(
                    new DateAggregate(new MaxDate(SignupDatePlusDays(30))),
                    "latestProjectedDate"
                ),
            ],
            where: null,
            join: null,
            [BoolGroupKeyField("schema_with_foreign_keys.users", "user_active")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, DateOnly> expected = userRows
            .GroupBy(user => user.UserActive)
            .ToDictionary(g => g.Key, g => g.Max(user => user.SignupDate.AddDays(30)));

        Dictionary<bool, DateOnly> actual = result.Rows.ToDictionary(
            row => row.Bool("user_active")!.Value,
            row => row.Date("latestProjectedDate")!.Value
        );

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfEachDateAddDaysGroupedByOrderStatusFindsEarliestProjectedDate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                StringGroupKeySelect("schema_with_foreign_keys.orders", "order_status"),
                DateAggregateSelect(
                    new DateAggregate(new MinDate(SignupDatePlusDays(30))),
                    "earliestProjectedDate"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [StringGroupKeyField("schema_with_foreign_keys.orders", "order_status")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, DateOnly> expected = (
            from order in orderRows
            join user in userRows on order.OrderUserId equals user.UserId
            select new { order.OrderStatus, Projected = user.SignupDate.AddDays(30) }
        )
            .GroupBy(x => x.OrderStatus)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Projected));

        Dictionary<string, DateOnly> actual = result.Rows.ToDictionary(
            row => row["order_status"]!,
            row => row.Date("earliestProjectedDate")!.Value
        );

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfEachTimeAddSecondsWholeSetFindsLatestProjectedTime()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                TimeAggregateSelect(
                    new TimeAggregate(new MaxTime(ShiftStartPlusSeconds(3600))),
                    "latestProjectedTime"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        TimeOnly expected = userRows.Max(user =>
            user.ShiftStart.Add(TimeSpan.FromSeconds(3600))
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Time("latestProjectedTime"));
    }

    [Fact]
    public void MinOfEachDateTimeAddSecondsGroupedByOrderUserIdFindsEarliestProjectedInstant()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                UuidGroupKeySelect("schema_with_foreign_keys.orders", "order_user_id"),
                DateTimeAggregateSelect(
                    new DateTimeAggregate(new MinDateTime(PlacedAtPlusSeconds(1800))),
                    "earliestProjectedInstant"
                ),
            ],
            where: null,
            join: null,
            [UuidGroupKeyField("schema_with_foreign_keys.orders", "order_user_id")],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, DateTime> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Min(order => order.PlacedAt.AddSeconds(1800))
            );

        Dictionary<Guid, DateTime> actual = result.Rows.ToDictionary(
            row => row.Uuid("order_user_id")!.Value,
            row => row.DateTime("earliestProjectedInstant")!.Value
        );

        Assert.Equal(expected, actual);
    }

    // ===== G: HAVING on a temporal-diff aggregate =====

    [Fact]
    public void HavingMaxOfEachDateDiffDaysComparisonKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                UuidGroupKeySelect("schema_with_foreign_keys.orders", "order_user_id"),
                NumberAggregateSelect(
                    new NumberAggregate(new MaxNumber(PlacedOnMinusSignupDate())),
                    "maxSpanDays"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [UuidGroupKeyField("schema_with_foreign_keys.orders", "order_user_id")],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        MaxOf(PlacedOnMinusSignupDate()),
                        new NumberReturning(new NumberScalar(1500))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. (
                from order in orderRows
                join user in userRows on order.OrderUserId equals user.UserId
                select new
                {
                    order.OrderUserId,
                    Span = (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber),
                }
            )
                .GroupBy(x => x.OrderUserId)
                .Where(g => g.Max(x => x.Span) > 1500)
                .Select(g => g.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid("order_user_id")!.Value),
        ];

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }

    // ===== H: temporal average is unimplemented (fail-fast, not a
    // KnownGap - CLAUDE.md documents this as an intentional execution gap
    // with undefined rounding semantics) =====

    [Fact]
    public void AverageOfEachDateAddDaysThrowsNotSupportedForTemporalAverage()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                DateAggregateSelect(
                    new DateAggregate(new AverageDate(SignupDatePlusDays(30))),
                    "meanProjectedDate"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }
}
