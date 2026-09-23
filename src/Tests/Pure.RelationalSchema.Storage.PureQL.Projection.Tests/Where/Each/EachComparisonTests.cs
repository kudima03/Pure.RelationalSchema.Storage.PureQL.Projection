using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row `each` range comparisons (>, >=, <, <=) across the comparable value
// types (number, string, date, datetime, time). Boolean and uuid have no
// comparison operator in PureQL, so they are intentionally absent.
[Trait("Clause", "Where")]
[Trait("Feature", "EachComparison")]
public sealed class EachComparisonTests
{
    [Fact]
    public void EachNumberGreaterThanFiltersRowsAboveThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count(order => order.OrderTotal > 100), result.Count);
    }

    [Fact]
    public void EachNumberGreaterThanZeroKeepsEveryPositiveRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberGreaterThanZeroQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every sample total is positive, so nothing may be filtered out
        // (issue #90's live symptom was zero rows for exactly this shape).
        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void EachNumberGreaterThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberGreaterThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderTotal >= 100.50),
            result.Count
        );
    }

    [Fact]
    public void EachNumberLessThanFiltersRowsBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberLessThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count(order => order.OrderTotal < 100), result.Count);
    }

    [Fact]
    public void EachNumberLessThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberLessThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderTotal <= 75.25),
            result.Count
        );
    }

    [Fact]
    public void EachStringGreaterThanUsesOrdinalOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachStringGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                string.CompareOrdinal(order.OrderStatus, "pending") > 0
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateGreaterThanFiltersLaterDates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new EachDateGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn > threshold),
            result.Count
        );
    }

    [Fact]
    public void EachTimeGreaterThanFiltersLaterTimes()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new EachTimeGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart > threshold),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeGreaterThanFiltersLaterInstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime threshold = new DateTime(2024, 6, 2, 9, 15, 0);

        Query query = new EachDateTimeGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.LastLogin > threshold),
            result.Count
        );
    }
}
