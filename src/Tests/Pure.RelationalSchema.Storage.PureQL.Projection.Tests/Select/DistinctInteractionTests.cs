using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// DISTINCT interacting with the rest of the pipeline: multi-column tuple
// dedup, join fan-out collapse, ordering/pagination interplay, aliasing, and
// the remaining typed columns of the seven-type matrix (date/number/uuid/
// time/datetime; string and bool are covered by DistinctTests).
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctInteractionTests
{
    [Fact]
    public void DistinctOnMultiColumnProjectionDeduplicatesTuples()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DistinctOnMultiColumnProjectionQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (double, bool)[] expected =
        [
            .. userRows
                .Select(user => (user.UserAge, user.UserActive))
                .Distinct()
                .OrderBy(pair => pair.UserAge)
                .ThenBy(pair => pair.UserActive),
        ];

        (double, bool)[] actual =
        [
            .. result.Rows
                .Select(row =>
                    (
                        row.Double(new UserAgeColumn().Name.TextValue)!.Value,
                        row.Bool(new UserActiveColumn().Name.TextValue)!.Value
                    )
                )
                .OrderBy(pair => pair.Item1)
                .ThenBy(pair => pair.Item2),
        ];

        Assert.True(expected.Length < userRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctCollapsesJoinFanOutDuplicates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new DistinctOverItemsToProductsJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderItemRows
                .Select(item => item.ItemOrderId)
                .Distinct()
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Column(new ItemOrderIdColumn().Name.TextValue)
                .Select(text => Guid.Parse(text!))
                .OrderBy(id => id),
        ];

        Assert.True(expected.Length < orderItemRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctPreservesFirstOccurrenceOrderAfterOrderBy()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctAfterOrderByQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .OrderByDescending(status => status, StringComparer.Ordinal)
                .Distinct(),
        ];

        string?[] actual = [.. result.Column(new OrderStatusColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctAppliesBeforePagination()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctWithPaginationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal)
                .Take(2),
        ];

        string?[] actual = [.. result.Column(new OrderStatusColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctOnAliasedColumnDeduplicatesProjectedValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOnAliasedColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Contains("state", result.ColumnNames);
        Assert.Equal(
            orderRows.Select(order => order.OrderStatus).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void DistinctOnDateColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DistinctOnDateColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinct = userRows
            .Select(user => user.SignupDate)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < userRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnNumberColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOnNumberColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinct = orderRows
            .Select(order => order.OrderTotal)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < orderRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnUuidColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOnUuidColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinct = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < orderRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnTimeColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DistinctOnTimeColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinct = userRows
            .Select(user => user.ShiftStart)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < userRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnDateTimeColumnCollapsesDuplicateValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DistinctOnDateTimeColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinct = userRows
            .Select(user => user.LastLogin)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < userRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }
}
