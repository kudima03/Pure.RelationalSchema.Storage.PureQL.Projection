using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY collapses rows sharing a key to one row per distinct key. These
// tests project the grouping key itself (aggregates over grouped rows are a
// separate, currently-unsupported feature - see AggregateTests). Group order is
// not asserted; the distinct key set is.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupBy")]
public sealed class GroupByTests
{
    [Fact]
    public void GroupByStringKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupByStringKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(
            expected,
            result.Column(new OrderStatusColumn().Name.TextValue).OrderBy(s => s).ToArray()
        );
    }

    [Fact]
    public void GroupByBooleanKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByBooleanKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.UserActive).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByUuidKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupByUuidKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Select(order => order.OrderUserId).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByCompositeKeyYieldsOneRowPerDistinctCombination()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupByCompositeKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Select(order => (order.OrderStatus, order.OrderUserId))
                .Distinct()
                .Count(),
            result.Count
        );
    }
}
