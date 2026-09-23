using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row `each equal` filtering, one case per PureQL value type. The each*
// family is the spec mechanism for referencing a field in a row predicate
// (scalar equality operands are scalar-only), so these exercise the real
// column-filtering path. Every query is constructed explicitly and inline.
[Trait("Clause", "Where")]
[Trait("Feature", "EachEquality")]
public sealed class EachEqualityTests
{
    [Fact]
    public void EachStringEqualityKeepsOnlyRowsWhoseFieldEqualsTheScalar()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachStringEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderStatus == "shipped"),
            result.Count
        );
        Assert.All(
            result.Column(new OrderStatusColumn().Name.TextValue),
            status => Assert.Equal("shipped", status)
        );
    }

    [Fact]
    public void EachStringEqualityWithNoMatchesReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachStringEqualityWithNoMatchQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void EachNumberEqualityFiltersByDoubleField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNumberEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Where(user => user.UserAge == 30)
                .Select(user => user.UserName)
                .OrderBy(name => name)
                .ToArray(),
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void EachBooleanEqualityFiltersByBoolField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachBooleanEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.UserActive), result.Count);
    }

    [Fact]
    public void EachDateEqualityFiltersByDateField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateOnly target = new DateOnly(2020, 1, 15);

        Query query = new EachDateEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.SignupDate == target), result.Count);
    }

    [Fact]
    public void EachTimeEqualityFiltersByTimeField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly target = new TimeOnly(9, 0, 0);

        Query query = new EachTimeEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.ShiftStart == target), result.Count);
    }

    [Fact]
    public void EachDateTimeEqualityFiltersByDateTimeField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime target = new DateTime(2024, 6, 1, 8, 30, 0);

        Query query = new EachDateTimeEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.LastLogin == target), result.Count);
    }

    [Fact]
    public void EachUuidEqualityFiltersByUuidField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachUuidEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal("Bob", result.Row(0)[new UserNameColumn().Name.TextValue]);
    }
}
