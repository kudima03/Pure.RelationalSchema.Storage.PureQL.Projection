using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
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
            new DateField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.PlacedOn)
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
                SampleDatabase.Orders.Entity,
                SampleDatabase.Orders.PlacedAt
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
            new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)
        );
    }

    private static StringReturning MinStatus()
    {
        return new StringReturning(new StringAggregate(new MinString(Statuses())));
    }

    private static TimeArrayReturning ShiftStarts()
    {
        return new TimeArrayReturning(
            new TimeField(SampleDatabase.Users.Entity, SampleDatabase.Users.ShiftStart)
        );
    }

    private static TimeReturning MaxShiftStart()
    {
        return new TimeReturning(new TimeAggregate(new MaxTime(ShiftStarts())));
    }

    private static Query OrdersGroupedByUser(BooleanReturning having)
    {
        return new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
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
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
            [
                new Field(
                    new BooleanField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Active
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
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumGreaterThanOrEqualConstantKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanConstantKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanOrEqualConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) <= 150.50)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Number: max(Orders.Total) <op> min(Orders.Total) (aggregate-vs-aggregate) ----

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanMinTotalKeepsMultiOrderGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanOrEqualMinTotalKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanMinTotalKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanOrEqualMinTotalKeepsSingleOrderGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Date: max(Orders.PlacedOn) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanOrEqualConstantKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanConstantKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanOrEqualConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- DateTime: max(Orders.PlacedAt) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanOrEqualConstantKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanConstantKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanOrEqualConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Time: max(Users.ShiftStart) <op> constant, grouped by Active ----

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<bool> expected =
        [
            .. db.UserRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(SampleDatabase.Users.Active)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < db.UserRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanOrEqualConstantKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.UserRows.Select(user => user.UserActive).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanConstantKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanOrEqualConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<bool> expected =
        [
            .. db.UserRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(SampleDatabase.Users.Active)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < db.UserRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- String: min(Orders.Status) <op> constant (ordinal) ----

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanOrEqualConstantKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanConstantKeepsNoGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanOrEqualConstantKeepsSomeGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < db.OrderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }
}
