using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY combined with ORDER BY on the grouping key: one row per distinct key,
// emitted in the requested order.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByOrderBy")]
public sealed class GroupByOrderByTests
{
    [Fact]
    public void GroupByStatusOrderedByStatusAscYieldsKeysInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupByStatusOrderedByStatusAscQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        string?[] actual = [.. result.Column(new OrderStatusColumn().Name.TextValue)];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByAgeOrderedByAgeDescYieldsKeysInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByAgeOrderedByAgeDescQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge)
                .Distinct()
                .OrderByDescending(v => v),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
