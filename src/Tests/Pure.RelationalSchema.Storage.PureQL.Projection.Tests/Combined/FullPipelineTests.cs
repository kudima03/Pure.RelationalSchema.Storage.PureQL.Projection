using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Combined;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Every clause at once, in pipeline order: JOIN -> WHERE -> ORDER BY ->
// GROUP BY -> HAVING -> projection -> pagination. The expected window is
// computed step-by-step from the ground-truth records.
[Trait("Clause", "Combined")]
[Trait("Feature", "FullPipeline")]
public sealed class FullPipelineTests
{
    [Fact]
    public void JoinWhereGroupByHavingOrderByPaginationCompose()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new JoinWhereGroupByHavingOrderByPaginationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected =
        [
            .. orderRows
                .Where(order =>
                    userRows.Single(user =>
                        user.UserId == order.OrderUserId
                    ).UserActive
                )
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, (double)group.Count()))
                .Skip(1)
                .Take(2),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue]!,
                    row.Double("orderCount")!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
