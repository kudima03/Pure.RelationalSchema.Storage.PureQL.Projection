using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where;

// WHERE evaluated over merged join rows: predicates may mix columns of both
// tables, reference a joined boolean field directly, or negate a condition
// on the joined side. Expected sets are computed pair-by-pair from the
// ground-truth records.
[Trait("Clause", "Where")]
[Trait("Feature", "JoinedFilter")]
public sealed class JoinedFilterTests
{
    private static UserRecord UserOf(
        IReadOnlyList<UserRecord> userRows,
        OrderRecord order
    )
    {
        return userRows.Single(user => user.UserId == order.OrderUserId);
    }

    [Fact]
    public void WhereConjunctionAcrossBothTablesFiltersMergedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const double ageThreshold = 30;
        const double totalThreshold = 100;

        Query query = new WhereConjunctionAcrossBothTablesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order =>
            UserOf(userRows, order).UserAge >= ageThreshold
            && order.OrderTotal > totalThreshold
        );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereOnJoinedBooleanFieldKeepsRowsWhereItIsTrue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WhereOnJoinedBooleanFieldQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order => UserOf(userRows, order).UserActive);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereNegationOnJoinedColumnExcludesItsRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const string excludedName = "Ann";

        Query query = new WhereNegationOnJoinedColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order =>
            UserOf(userRows, order).UserName != excludedName
        );

        Assert.Equal(expected, result.Count);
    }
}
