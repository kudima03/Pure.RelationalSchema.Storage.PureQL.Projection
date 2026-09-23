using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Fills out the each-comparison operator x type matrix: the remaining operators
// (LessThan, LessThanOrEqual, GreaterThanOrEqual) for string/date/time/datetime
// (GreaterThan is covered in EachComparisonTests).
[Trait("Clause", "Where")]
[Trait("Feature", "EachComparisonMore")]
public sealed class EachComparisonMoreTests
{
    [Fact]
    public void EachStringLessThanFiltersRowsBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachStringLessThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                string.CompareOrdinal(order.OrderStatus, "pending") < 0
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateLessThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new EachDateLessThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn <= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachTimeGreaterThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = new EachTimeGreaterThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart >= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeLessThanFiltersEarlierInstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime threshold = new DateTime(2024, 6, 2, 9, 15, 0);

        Query query = new EachDateTimeLessThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.LastLogin < threshold),
            result.Count
        );
    }
}
