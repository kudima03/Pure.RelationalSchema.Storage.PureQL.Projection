using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row temporal arithmetic: date add-days / diff-days (unit: days) and
// time / datetime add-seconds / diff-seconds (unit: seconds). Each result is
// compared or equated to form a row predicate.
[Trait("Clause", "Where")]
[Trait("Feature", "EachDateTimeMath")]
public sealed class EachDateTimeMathTests
{
    [Fact]
    public void EachDateAddDaysShiftsDateBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly expectedAfterShift = new DateOnly(2024, 6, 2);

        Query query = new EachDateAddDaysInEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn.AddDays(1) == expectedAfterShift),
            result.Count
        );
    }

    [Fact]
    public void EachDateDiffDaysComputesDayGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly origin = new DateOnly(2024, 6, 1);

        Query query = new EachDateDiffDaysQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                order.PlacedOn.DayNumber - origin.DayNumber > 2
            ),
            result.Count
        );
    }

    [Fact]
    public void EachTimeAddSecondsShiftsTimeBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly expectedAfterShift = new TimeOnly(10, 0, 0);

        Query query = new EachTimeAddSecondsInEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user =>
                user.ShiftStart.Add(TimeSpan.FromSeconds(3600)) == expectedAfterShift
            ),
            result.Count
        );
    }

    [Fact]
    public void EachTimeDiffSecondsComputesSecondGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new EachTimeDiffSecondsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user =>
                (user.ShiftStart - origin).TotalSeconds > 3600
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeAddSecondsShiftsInstantBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime expectedAfterShift = new DateTime(2024, 6, 1, 9, 30, 0);

        Query query = new EachDateTimeAddSecondsInEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user =>
                user.LastLogin.AddSeconds(3600) == expectedAfterShift
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeDiffSecondsComputesSecondGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime origin = new DateTime(2024, 6, 2, 0, 0, 0);

        Query query = new EachDateTimeDiffSecondsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user =>
                (user.LastLogin - origin).TotalSeconds > 0
            ),
            result.Count
        );
    }
}
