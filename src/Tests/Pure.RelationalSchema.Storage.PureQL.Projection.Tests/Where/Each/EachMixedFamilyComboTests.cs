using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachDateTimeArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Issue #155: new combinations of existing each* operators - arithmetic
// feeding comparison feeding boolean-ops, temporal add/diff feeding
// comparison, mixed equality+comparison trees across value types, and
// operand-shape variety (field / broadcast literal array / nested each
// expression) within one predicate. Single-table combos only; combos that
// span a join live in EachMixedFamilyJoinedComboTests. Every expectation is
// derived independently in LINQ over the ground-truth lists under SQL
// result-set semantics, per the issue's overriding principle.
[Trait("Clause", "Where")]
[Trait("Feature", "EachMixedFamilyCombo")]
public sealed class EachMixedFamilyComboTests
{
    private static SelectExpression OrderIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                )
            )
        );
    }

    private static SelectExpression UserIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                )
            )
        );
    }

    private static Guid[] OrderedUuids(ProjectionResult result, string column)
    {
        return [.. result.Rows.Select(row => row.Uuid(column)!.Value).OrderBy(id => id)];
    }

    // ===== Category 1: arithmetic feeding comparison feeding boolean-ops =====

    // eachAnd(eachGreaterThan(eachAdd(age, 5), 30), eachLessThan(eachMultiply(age, 2), 100))
    [Fact]
    public void EachAndOfShiftedAgeAboveThresholdAndDoubledAgeBelowThresholdKeepsBoth()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Users.Entity,
                                                            SampleDatabase.Users.Age
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(5)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(30))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachMultiply(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Users.Entity,
                                                            SampleDatabase.Users.Age
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(2)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(100))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u => u.UserAge + 5 > 30 && u.UserAge * 2 < 100)
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // eachOr(eachGreaterThan(eachSubtract(age, 10), 20),
    //        eachLessThanOrEqual(eachDivide(age, 2), 13))
    [Fact]
    public void EachOrOfLoweredAgeAboveThresholdAndHalvedAgeAtMostThresholdKeepsEither()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachSubtract(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Users.Entity,
                                                            SampleDatabase.Users.Age
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(10)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(20))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThanOrEqual,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachDivide(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Users.Entity,
                                                            SampleDatabase.Users.Age
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(2)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(13))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u => u.UserAge - 10 > 20 || u.UserAge / 2 <= 13)
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // eachNot(eachGreaterThan(eachMultiply(age, 3), 120)) - bare each-not at the
    // top of the tree, no further boolean composition.
    [Fact]
    public void EachNotOfTripledAgeAboveThresholdExcludesHighArithmeticRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachNumberComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new NumberArrayReturning(
                                    new EachArithmetic(
                                        new EachMultiply(
                                            [
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Users.Entity,
                                                        SampleDatabase.Users.Age
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(3)),
                                            ]
                                        )
                                    )
                                ),
                                new NumberReturning(new NumberScalar(120))
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
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u => !(u.UserAge * 3 > 120))
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // eachAnd(eachLessThan(eachDivide(total, 2), 100),
    //         eachGreaterThan(eachAdd(total, 20), 90))
    [Fact]
    public void EachAndOfDividedTotalBelowThresholdAndAddedTotalAboveThresholdOverOrders()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachDivide(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(2)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(100))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(20)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(90))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o => o.OrderTotal / 2 < 100 && o.OrderTotal + 20 > 90)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // eachOr(eachNot(eachGreaterThan(eachAdd(total, 20), 150)),
    //        eachLessThan(eachSubtract(total, 30), 175))
    [Fact]
    public void EachOrOfNotAddedTotalAboveThresholdAndSubtractedTotalBelowThreshold()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachNotOperator(
                                new BooleanArrayReturning(
                                    new EachComparison(
                                        new EachNumberComparison(
                                            EachComparisonOperator.EachGreaterThan,
                                            new NumberArrayReturning(
                                                new EachArithmetic(
                                                    new EachAdd(
                                                        [
                                                            new NumberArrayReturning(
                                                                new NumberField(
                                                                    SampleDatabase
                                                                        .Orders
                                                                        .Entity,
                                                                    SampleDatabase
                                                                        .Orders
                                                                        .Total
                                                                )
                                                            ),
                                                            new NumberReturning(
                                                                new NumberScalar(20)
                                                            ),
                                                        ]
                                                    )
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(150))
                                        )
                                    )
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachSubtract(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(30)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(175))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o => !(o.OrderTotal + 20 > 150) || o.OrderTotal - 30 < 175)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // ===== Category 2: temporal add/diff feeding comparison feeding boolean-ops =====

    // eachOr(eachGreaterThan(eachDateDiffDays(placed_on, origin), 2),
    //        eachLessThan(total, 60))
    [Fact]
    public void EachOrOfDateDiffDaysAboveThresholdAndTotalBelowThresholdOverOrders()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly origin = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachDateDiffDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new DateReturning(new DateScalar(origin))
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(2))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(60))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                    o.PlacedOn.DayNumber - origin.DayNumber > 2 || o.OrderTotal < 60
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // eachAnd(eachEquality(eachDateAddDays(placed_on, 1) == target),
    //         eachEquality(status == "shipped"))
    [Fact]
    public void EachAndOfDateAddDaysEqualsTargetAndStatusEqualsShippedOverOrders()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly target = new DateOnly(2024, 6, 2);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachDateEquality(
                                    new DateArrayReturning(
                                        new EachDateAddDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(1))
                                        )
                                    ),
                                    new DateReturning(new DateScalar(target))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                    o.PlacedOn.AddDays(1) == target && o.OrderStatus == "shipped"
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // eachAnd(eachGreaterThanOrEqual(eachTimeAddSeconds(shift_start, 1800), 9:30),
    //         eachLessThan(eachTimeDiffSeconds(shift_start, 8:00), 7200))
    [Fact]
    public void EachAndOfTimeAddSecondsAtLeastThresholdAndTimeDiffSecondsBelowThreshold()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly shiftedThreshold = new TimeOnly(9, 30, 0);
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachTimeComparison(
                                    EachComparisonOperator.EachGreaterThanOrEqual,
                                    new TimeArrayReturning(
                                        new EachTimeAddSeconds(
                                            new TimeArrayReturning(
                                                new TimeField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.ShiftStart
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(1800))
                                        )
                                    ),
                                    new TimeReturning(new TimeScalar(shiftedThreshold))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachTimeDiffSeconds(
                                            new TimeArrayReturning(
                                                new TimeField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.ShiftStart
                                                )
                                            ),
                                            new TimeReturning(new TimeScalar(origin))
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(7200))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u =>
                    u.ShiftStart.Add(TimeSpan.FromSeconds(1800)) >= shiftedThreshold
                    && (u.ShiftStart - origin).TotalSeconds < 7200
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // eachOr(eachEquality(eachDateTimeAddSeconds(last_login, 3600) == target),
    //        eachGreaterThan(eachDateTimeDiffSeconds(last_login, origin), 0))
    [Fact]
    public void EachOrOfDateTimeAddSecondsEqualsTargetAndDateTimeDiffSecondsAboveZero()
    {
        SampleDatabase db = new SampleDatabase();
        DateTime target = new DateTime(2024, 6, 1, 9, 30, 0);
        DateTime origin = new DateTime(2024, 6, 2, 0, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachDateTimeEquality(
                                    new DateTimeArrayReturning(
                                        new EachDateTimeAddSeconds(
                                            new DateTimeArrayReturning(
                                                new DateTimeField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.LastLogin
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(3600))
                                        )
                                    ),
                                    new DateTimeReturning(new DateTimeScalar(target))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachDateTimeDiffSeconds(
                                            new DateTimeArrayReturning(
                                                new DateTimeField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.LastLogin
                                                )
                                            ),
                                            new DateTimeReturning(
                                                new DateTimeScalar(origin)
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(0))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u =>
                    u.LastLogin.AddSeconds(3600) == target
                    || (u.LastLogin - origin).TotalSeconds > 0
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // ===== Category 3: mixed equality + comparison, 3-5 levels, 2+ types =====

    // eachAnd(eachAnd(active == true, age >= 28), signup_date < 2021-01-01)
    // mixes boolean, number and date leaves in a 3-level tree.
    [Fact]
    public void ThreeLevelTreeMixingBooleanNumberAndDateFiltersUsersByAllThree()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2021, 1, 1);

        BooleanArrayReturning activeTrue = new BooleanArrayReturning(
            new EachEquality(
                new EachBooleanEquality(
                    new BooleanArrayReturning(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    new BooleanReturning(new BooleanScalar(true))
                )
            )
        );
        BooleanArrayReturning ageAtLeast28 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    new NumberReturning(new NumberScalar(28))
                )
            )
        );
        BooleanArrayReturning signupBeforeThreshold = new BooleanArrayReturning(
            new EachComparison(
                new EachDateComparison(
                    EachComparisonOperator.EachLessThan,
                    new DateArrayReturning(
                        new DateField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.SignupDate
                        )
                    ),
                    new DateReturning(new DateScalar(threshold))
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachAndOperator([activeTrue, ageAtLeast28])
                        ),
                        signupBeforeThreshold,
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u =>
                    u.UserActive && u.UserAge >= 28 && u.SignupDate < threshold
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // 5-level tree mixing number/boolean/date leaves, AND-rooted:
    //   eachAnd(
    //     eachOr(eachNot(eachAnd(age > 26, active == true)), age >= 30),
    //     eachOr(eachNot(active == true), eachAnd(signup < 2021-01-01, active == false))
    //   )
    [Fact]
    public void FiveLevelTreeMixingNumberBooleanAndDateAcrossOrAndNotFiltersUsers()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2021, 1, 1);

        BooleanArrayReturning ageAbove26 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    new NumberReturning(new NumberScalar(26))
                )
            )
        );
        BooleanArrayReturning activeTrue = new BooleanArrayReturning(
            new EachEquality(
                new EachBooleanEquality(
                    new BooleanArrayReturning(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    new BooleanReturning(new BooleanScalar(true))
                )
            )
        );
        BooleanArrayReturning ageAtLeast30 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    new NumberReturning(new NumberScalar(30))
                )
            )
        );
        BooleanArrayReturning signupBeforeThreshold = new BooleanArrayReturning(
            new EachComparison(
                new EachDateComparison(
                    EachComparisonOperator.EachLessThan,
                    new DateArrayReturning(
                        new DateField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.SignupDate
                        )
                    ),
                    new DateReturning(new DateScalar(threshold))
                )
            )
        );
        BooleanArrayReturning activeFalse = new BooleanArrayReturning(
            new EachEquality(
                new EachBooleanEquality(
                    new BooleanArrayReturning(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    new BooleanReturning(new BooleanScalar(false))
                )
            )
        );

        BooleanArrayReturning leftBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(
                        new EachNotOperator(
                            new BooleanArrayReturning(
                                new EachAndOperator([ageAbove26, activeTrue])
                            )
                        )
                    ),
                    ageAtLeast30,
                ]
            )
        );
        BooleanArrayReturning rightBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(new EachNotOperator(activeTrue)),
                    new BooleanArrayReturning(
                        new EachAndOperator([signupBeforeThreshold, activeFalse])
                    ),
                ]
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator([leftBranch, rightBranch])
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.UserRows
                .Where(u =>
                {
                    bool a = u.UserAge > 26;
                    bool b = u.UserActive;
                    bool c = u.UserAge >= 30;
                    bool d = u.UserActive;
                    bool e = u.SignupDate < threshold;
                    bool f = !u.UserActive;
                    bool left = !(a && b) || c;
                    bool right = !d || (e && f);
                    return left && right;
                })
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Users.Id));
    }

    // eachAnd(eachAnd(order_user_id == Cara.id, placed_on >= 2024-06-05),
    //         status == "shipped")
    // 3-level tree mixing uuid equality, date comparison and string equality.
    [Fact]
    public void ThreeLevelTreeMixingUuidDateAndStringFiltersOrdersByAllThree()
    {
        SampleDatabase db = new SampleDatabase();
        Guid caraId = db.UserRows.Single(u => u.UserName == "Cara").UserId;
        DateOnly threshold = new DateOnly(2024, 6, 5);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachAndOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachUuidEquality(
                                                new UuidArrayReturning(
                                                    new UuidField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.UserId
                                                    )
                                                ),
                                                new UuidReturning(
                                                    new UuidScalar(caraId)
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachDateComparison(
                                                EachComparisonOperator
                                                    .EachGreaterThanOrEqual,
                                                new DateArrayReturning(
                                                    new DateField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.PlacedOn
                                                    )
                                                ),
                                                new DateReturning(
                                                    new DateScalar(threshold)
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                    o.OrderUserId == caraId
                    && o.PlacedOn >= threshold
                    && o.OrderStatus == "shipped"
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // ===== Category 5: operand-shape variety (field / literal / nested each) =====

    // eachAnd(total > [90, -1] (broadcast 90),
    //         eachSubtract(total, 200) < 0)  -- field, literal array, nested each.
    [Fact]
    public void EachAndCombinesFieldLiteralArrayAndNestedArithmeticOperands()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberArrayReturning(
                                        new NumberArrayScalar([90, -1])
                                    )
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachSubtract(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(200)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(0))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // The literal array's second element (-1) is never used - only its
        // first element (90) broadcasts to every row (see
        // EachBroadcastAndLiteralTests).
        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o => o.OrderTotal > 90 && o.OrderTotal - 200 < 0)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // eachOr(["shipped", junk...] (broadcast) == status,
    //        eachDateAddDays(placed_on, 30) > 2024-07-04)  -- literal array,
    // field, and nested date arithmetic.
    [Fact]
    public void EachOrCombinesLiteralStringArrayFieldAndNestedDateArithmeticOperands()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2024, 7, 4);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringArrayScalar(
                                            [
                                                "shipped",
                                                "zzz",
                                                "zzz",
                                                "zzz",
                                                "zzz",
                                                "zzz",
                                            ]
                                        )
                                    ),
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachDateComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new DateArrayReturning(
                                        new EachDateAddDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(30))
                                        )
                                    ),
                                    new DateReturning(new DateScalar(threshold))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                    o.OrderStatus == "shipped" || o.PlacedOn.AddDays(30) > threshold
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }

    // ===== Crossover: numeric arithmetic + temporal diff under one AND =====

    // eachAnd(eachGreaterThan(eachAdd(total, 10), 100),
    //         eachLessThan(eachDateDiffDays(placed_on, origin), 4))
    [Fact]
    public void EachAndOfArithmeticComparisonAndDateDiffComparisonOverOrders()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly origin = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(10)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(100))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new EachDateDiffDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new DateReturning(new DateScalar(origin))
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(4))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                    o.OrderTotal + 10 > 100
                    && o.PlacedOn.DayNumber - origin.DayNumber < 4
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, SampleDatabase.Orders.Id));
    }
}
