using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Id
                        )
                    )
                )
            )
        );
    }

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

    // ---- Number: count(Orders.Id) == constant ----

    [Fact]
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsMatchingGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() == 2)
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
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.DoesNotContain(
            5,
            db.OrderRows.GroupBy(order => order.OrderUserId).Select(group => group.Count())
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Boolean composite: (count > k) == (aggregate > m) ----

    [Fact]
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndSumComparisonsKeepsMatchingTruth()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    (group.Count() > 1) == (group.Sum(order => order.OrderTotal) > 150)
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
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndMaxComparisonsKeepsMatchingTruth()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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

    // ---- Date: max(Orders.PlacedOn) == constant ----

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsMatchingGroup()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            db.OrderRows
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
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeEquality")]
    public void HavingMaxPlacedAtEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            db.OrderRows
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
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<bool> expected =
        [
            .. db.UserRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(SampleDatabase.Users.Active)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeEquality")]
    public void HavingMaxShiftStartEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            db.UserRows
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
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingStringEquality")]
    public void HavingMinStatusEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.DoesNotContain(
            "unknown",
            db.OrderRows
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
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );
    }
}
