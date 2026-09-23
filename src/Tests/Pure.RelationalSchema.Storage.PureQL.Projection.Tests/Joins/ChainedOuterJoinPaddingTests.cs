using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Two chained LEFT joins: padding must propagate. A row padded by the first
// join carries empty cells for the middle table, so the second join's key
// equality can never match it and it must be padded again, surviving to the
// final result exactly once.
[Trait("Clause", "Join")]
[Trait("Feature", "ChainedOuterJoin")]
public sealed class ChainedOuterJoinPaddingTests
{
    [Fact]
    public void SecondLeftJoinPadsRowsAlreadyPaddedByTheFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        Query query = new ChainedLeftJoinsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
        {
            List<OrderRecord> orders =
            [
                .. orderRows.Where(order =>
                    order.OrderUserId == user.UserId
                ),
            ];

            return orders.Count == 0
                ? 1
                : orders.Sum(order =>
                    Math.Max(
                        1,
                        orderItemRows.Count(item =>
                            item.ItemOrderId == order.OrderId
                        )
                    )
                );
        });

        Assert.Equal(expectedCount, result.Count);

        foreach (UserRecord user in userRows)
        {
            List<OrderRecord> orders =
            [
                .. orderRows.Where(order =>
                    order.OrderUserId == user.UserId
                ),
            ];

            int expectedAppearances = orders.Count == 0
                ? 1
                : orders.Sum(order =>
                    Math.Max(
                        1,
                        orderItemRows.Count(item =>
                            item.ItemOrderId == order.OrderId
                        )
                    )
                );

            Assert.Equal(
                expectedAppearances,
                result
                    .Column(new UserNameColumn().Name.TextValue)
                    .Count(name => name == user.UserName)
            );
        }
    }
}
