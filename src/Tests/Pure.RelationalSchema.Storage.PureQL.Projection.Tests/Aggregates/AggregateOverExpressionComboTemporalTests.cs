using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

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
    // ===== E: numeric aggregates over each-temporal-diff =====

    [Fact]
    public void SumOfEachDateDiffDaysGroupedByOrderUserIdComputesTotalSpanDays()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumOfEachDateDiffDaysGroupedByOrderUserIdQuery().Value;

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
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
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

        Query query = new AverageOfEachDateDiffDaysGroupedByOrderStatusQuery().Value;

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
            row => row[new OrderStatusColumn().Name.TextValue]!,
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

        Query query = new MinAndMaxOfEachDateDiffDaysGroupedByUserActiveQuery().Value;

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
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("minSpanDays")!.Value
        );

        Dictionary<bool, double> actualMax = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
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

        Query query = new CountOfEachDateDiffDaysGroupedByUserAgeQuery().Value;

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
            row => row.Double(new UserAgeColumn().Name.TextValue)!.Value,
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

        Query query = new SumOfEachTimeDiffSecondsWholeSetQuery().Value;

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

        Query query =
            new AverageOfEachDateTimeDiffSecondsGroupedByOrderUserIdQuery().Value;

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
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
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

        Query query = new MaxOfEachDateAddDaysGroupedByUserActiveQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, DateOnly> expected = userRows
            .GroupBy(user => user.UserActive)
            .ToDictionary(g => g.Key, g => g.Max(user => user.SignupDate.AddDays(30)));

        Dictionary<bool, DateOnly> actual = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
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

        Query query = new MinOfEachDateAddDaysGroupedByOrderStatusQuery().Value;

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
            row => row[new OrderStatusColumn().Name.TextValue]!,
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

        Query query = new MaxOfEachTimeAddSecondsWholeSetQuery().Value;

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

        Query query = new MinOfEachDateTimeAddSecondsGroupedByOrderUserIdQuery().Value;

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
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
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

        Query query = new HavingMaxOfEachDateDiffDaysComparisonQuery().Value;

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
            .. result.Rows.Select(row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value),
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

        Query query = new AverageOfEachDateAddDaysQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }
}
