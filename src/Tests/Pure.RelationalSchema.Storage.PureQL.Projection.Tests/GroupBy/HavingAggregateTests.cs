using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING beyond a bare count comparison: aggregate comparisons over sum,
// average, min/max (string and date), boolean composites, equality over an
// aggregate, and the always-true/always-false boundary conditions. Orders
// are grouped by their user; expected groups are computed from the
// ground-truth records.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingAggregateTests
{
    [Fact]
    public void HavingSumGreaterThanKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingSumGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAverageLessThanOrEqualKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingAverageLessThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Average(order => order.OrderTotal) <= 100.50)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMinStringComparisonKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStringComparisonQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) >= 0
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMaxDateComparisonKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 4);

        Query query = new HavingMaxDateComparisonQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) < threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAndOfTwoAggregateComparisonsKeepsIntersection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingAndOfCountAndSumComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 && group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsKeepsUnion()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingOrOfCountAndSumComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 || group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingNotInvertsAggregateComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingNotWithProjectedCountQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() <= 1)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingComparingTwoAggregatesOfSameGroupKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingComparingTwoAggregatesOfSameGroupQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    > group.Average(order => order.OrderTotal)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Count < orderRows.Select(o => o.OrderUserId).Distinct().Count());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingEqualityOverCountKeepsExactMatches()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingEqualityOverCountQuery().Value;

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
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingRejectingEveryGroupReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingRejectingEveryGroupQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void HavingAcceptingEveryGroupKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingAcceptingEveryGroupQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
