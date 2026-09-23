using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

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
    private static Guid[] OrderedUuids(ProjectionResult result, string column)
    {
        return [.. result.Rows.Select(row => row.Uuid(column)!.Value).OrderBy(id => id)];
    }

    // ===== Category 1: arithmetic feeding comparison feeding boolean-ops =====

    // eachAnd(eachGreaterThan(eachAdd(age, 5), 30), eachLessThan(eachMultiply(age, 2), 100))
    [Fact]
    public void EachAndOfShiftedAgeAboveThresholdAndDoubledAgeBelowThresholdKeepsBoth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query =
            new EachAndOfShiftedAgeAboveThresholdAndDoubledAgeBelowThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u => u.UserAge + 5 > 30 && u.UserAge * 2 < 100)
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // eachOr(eachGreaterThan(eachSubtract(age, 10), 20),
    //        eachLessThanOrEqual(eachDivide(age, 2), 13))
    [Fact]
    public void EachOrOfLoweredAgeAboveThresholdAndHalvedAgeAtMostThresholdKeepsEither()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query =
            new EachOrOfLoweredAgeAboveThresholdAndHalvedAgeAtMostThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u => u.UserAge - 10 > 20 || u.UserAge / 2 <= 13)
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // eachNot(eachGreaterThan(eachMultiply(age, 3), 120)) - bare each-not at the
    // top of the tree, no further boolean composition.
    [Fact]
    public void EachNotOfTripledAgeAboveThresholdExcludesHighArithmeticRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNotOfTripledAgeAboveThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u => !(u.UserAge * 3 > 120))
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // eachAnd(eachLessThan(eachDivide(total, 2), 100),
    //         eachGreaterThan(eachAdd(total, 20), 90))
    [Fact]
    public void EachAndOfDividedTotalBelowThresholdAndAddedTotalAboveThresholdOverOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachAndOfDividedTotalBelowThresholdAndAddedTotalAboveThresholdOverOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o => o.OrderTotal / 2 < 100 && o.OrderTotal + 20 > 90)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // eachOr(eachNot(eachGreaterThan(eachAdd(total, 20), 150)),
    //        eachLessThan(eachSubtract(total, 30), 175))
    [Fact]
    public void EachOrOfNotAddedTotalAboveThresholdAndSubtractedTotalBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachOrOfNotAddedTotalAboveThresholdAndSubtractedTotalBelowThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o => !(o.OrderTotal + 20 > 150) || o.OrderTotal - 30 < 175)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // ===== Category 2: temporal add/diff feeding comparison feeding boolean-ops =====

    // eachOr(eachGreaterThan(eachDateDiffDays(placed_on, origin), 2),
    //        eachLessThan(total, 60))
    [Fact]
    public void EachOrOfDateDiffDaysAboveThresholdAndTotalBelowThresholdOverOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly origin = new DateOnly(2024, 6, 1);

        Query query =
            new EachOrOfDateDiffDaysAboveThresholdAndTotalBelowThresholdOverOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                    o.PlacedOn.DayNumber - origin.DayNumber > 2 || o.OrderTotal < 60
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // eachAnd(eachEquality(eachDateAddDays(placed_on, 1) == target),
    //         eachEquality(status == "shipped"))
    [Fact]
    public void EachAndOfDateAddDaysEqualsTargetAndStatusEqualsShippedOverOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly target = new DateOnly(2024, 6, 2);

        Query query =
            new EachAndOfDateAddDaysEqualsTargetAndStatusEqualsShippedOverOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                    o.PlacedOn.AddDays(1) == target && o.OrderStatus == "shipped"
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // eachAnd(eachGreaterThanOrEqual(eachTimeAddSeconds(shift_start, 1800), 9:30),
    //         eachLessThan(eachTimeDiffSeconds(shift_start, 8:00), 7200))
    [Fact]
    public void EachAndOfTimeAddSecondsAtLeastThresholdAndTimeDiffSecondsBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly shiftedThreshold = new TimeOnly(9, 30, 0);
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query =
            new EachAndOfTimeAddSecondsAtLeastThresholdAndTimeDiffSecondsBelowThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u =>
                    u.ShiftStart.Add(TimeSpan.FromSeconds(1800)) >= shiftedThreshold
                    && (u.ShiftStart - origin).TotalSeconds < 7200
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // eachOr(eachEquality(eachDateTimeAddSeconds(last_login, 3600) == target),
    //        eachGreaterThan(eachDateTimeDiffSeconds(last_login, origin), 0))
    [Fact]
    public void EachOrOfDateTimeAddSecondsEqualsTargetAndDateTimeDiffSecondsAboveZero()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime target = new DateTime(2024, 6, 1, 9, 30, 0);
        DateTime origin = new DateTime(2024, 6, 2, 0, 0, 0);

        Query query =
            new EachOrOfDateTimeAddSecondsEqualsTargetAndDateTimeDiffSecondsAboveZeroQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u =>
                    u.LastLogin.AddSeconds(3600) == target
                    || (u.LastLogin - origin).TotalSeconds > 0
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // ===== Category 3: mixed equality + comparison, 3-5 levels, 2+ types =====

    // eachAnd(eachAnd(active == true, age >= 28), signup_date < 2021-01-01)
    // mixes boolean, number and date leaves in a 3-level tree.
    [Fact]
    public void ThreeLevelTreeMixingBooleanNumberAndDateFiltersUsersByAllThree()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateOnly threshold = new DateOnly(2021, 1, 1);

        Query query = new ThreeLevelTreeMixingBooleanNumberAndDateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
                .Where(u =>
                    u.UserActive && u.UserAge >= 28 && u.SignupDate < threshold
                )
                .Select(u => u.UserId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // 5-level tree mixing number/boolean/date leaves, AND-rooted:
    //   eachAnd(
    //     eachOr(eachNot(eachAnd(age > 26, active == true)), age >= 30),
    //     eachOr(eachNot(active == true), eachAnd(signup < 2021-01-01, active == false))
    //   )
    [Fact]
    public void FiveLevelTreeMixingNumberBooleanAndDateAcrossOrAndNotFiltersUsers()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateOnly threshold = new DateOnly(2021, 1, 1);

        Query query =
            new FiveLevelTreeMixingNumberBooleanAndDateAcrossOrAndNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. userRows
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
        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new UserIdColumn().Name.TextValue));
    }

    // eachAnd(eachAnd(order_user_id == Cara.id, placed_on >= 2024-06-05),
    //         status == "shipped")
    // 3-level tree mixing uuid equality, date comparison and string equality.
    [Fact]
    public void ThreeLevelTreeMixingUuidDateAndStringFiltersOrdersByAllThree()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Guid caraId = userRows.Single(u => u.UserName == "Cara").UserId;
        DateOnly threshold = new DateOnly(2024, 6, 5);

        Query query = new ThreeLevelTreeMixingUuidDateAndStringQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                    o.OrderUserId == caraId
                    && o.PlacedOn >= threshold
                    && o.OrderStatus == "shipped"
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // ===== Category 5: operand-shape variety (field / literal / nested each) =====

    // eachAnd(total > [90, -1] (broadcast 90),
    //         eachSubtract(total, 200) < 0)  -- field, literal array, nested each.
    [Fact]
    public void EachAndCombinesFieldLiteralArrayAndNestedArithmeticOperands()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachAndOfFieldLiteralArrayAndNestedArithmeticOperandsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // The literal array's second element (-1) is never used - only its
        // first element (90) broadcasts to every row (see
        // EachBroadcastAndLiteralTests).
        Guid[] expected =
        [
            .. orderRows
                .Where(o => o.OrderTotal > 90 && o.OrderTotal - 200 < 0)
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // eachOr(["shipped", junk...] (broadcast) == status,
    //        eachDateAddDays(placed_on, 30) > 2024-07-04)  -- literal array,
    // field, and nested date arithmetic.
    [Fact]
    public void EachOrCombinesLiteralStringArrayFieldAndNestedDateArithmeticOperands()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 7, 4);

        Query query =
            new EachOrOfLiteralStringArrayFieldAndNestedDateArithmeticOperandsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                    o.OrderStatus == "shipped" || o.PlacedOn.AddDays(30) > threshold
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }

    // ===== Crossover: numeric arithmetic + temporal diff under one AND =====

    // eachAnd(eachGreaterThan(eachAdd(total, 10), 100),
    //         eachLessThan(eachDateDiffDays(placed_on, origin), 4))
    [Fact]
    public void EachAndOfArithmeticComparisonAndDateDiffComparisonOverOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly origin = new DateOnly(2024, 6, 1);

        Query query =
            new EachAndOfArithmeticComparisonAndDateDiffComparisonOverOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                    o.OrderTotal + 10 > 100
                    && o.PlacedOn.DayNumber - origin.DayNumber < 4
                )
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, OrderedUuids(result, new OrderIdColumn().Name.TextValue));
    }
}
