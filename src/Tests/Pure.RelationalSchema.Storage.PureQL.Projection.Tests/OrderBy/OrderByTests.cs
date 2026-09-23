using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.OrderBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// ORDER BY across each value type, ascending and descending, plus a stable
// multi-key sort. Expected sequences are produced by the equivalent stable
// LINQ ordering over the ground-truth records.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderBy")]
public sealed class OrderByTests
{
    [Fact]
    public void OrderByNumberAscendingSortsRowsLowToHigh()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new OrderByNumberAscendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByNumberDescendingSortsRowsHighToLow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new OrderByNumberDescendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderByDescending(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByStringAscendingSortsRowsAlphabetically()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByStringAscendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column(new UserNameColumn().Name.TextValue).ToArray()
        );
    }

    [Fact]
    public void OrderByDateDescendingSortsRowsLatestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByDateDescendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderByDescending(user => user.SignupDate)
                .Select(user => (DateOnly?)user.SignupDate)
                .ToArray(),
            [.. result.Rows.Select(row => row.Date(new SignupDateColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByDateTimeAscendingSortsRowsEarliestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByDateTimeAscendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.LastLogin)
                .Select(user => (DateTime?)user.LastLogin)
                .ToArray(),
            [.. result.Rows.Select(row => row.DateTime(new LastLoginColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByTimeAscendingSortsRowsEarliestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByTimeAscendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.ShiftStart)
                .Select(user => (TimeOnly?)user.ShiftStart)
                .ToArray(),
            [.. result.Rows.Select(row => row.Time(new ShiftStartColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByUuidAscendingMatchesGuidComparerOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByUuidAscendingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserId)
                .Select(user => (Guid?)user.UserId)
                .ToArray(),
            [.. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void OrderByTwoKeysAppliesStableSecondaryOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByTwoKeysQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserAge)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column(new UserNameColumn().Name.TextValue).ToArray()
        );
    }
}
