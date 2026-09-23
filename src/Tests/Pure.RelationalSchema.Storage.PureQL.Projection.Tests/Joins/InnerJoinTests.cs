using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// INNER JOIN on an equi-key expressed as a per-row each-equality over the two
// tables' uuid columns. Only matched pairs survive; columns from both sides
// are available (their names are globally unique so there is no ambiguity).
[Trait("Clause", "Join")]
[Trait("Feature", "InnerJoin")]
public sealed class InnerJoinTests
{
    [Fact]
    public void InnerJoinPairsEachOrderWithItsUser()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new InnerJoinOrderWithUserNameQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (Guid, string?)[] expected =
        [
            .. orderRows
                .Select(order =>
                    (
                        order.OrderId,
                        (string?)
                            userRows.Single(user =>
                                user.UserId == order.OrderUserId
                            ).UserName
                    )
                )
                .OrderBy(pair => pair.OrderId),
        ];

        (Guid, string?)[] actual =
        [
            .. result
                .Rows.Select(row =>
                    (
                        row.Uuid(new OrderIdColumn().Name.TextValue)!.Value,
                        row[new UserNameColumn().Name.TextValue]
                    )
                )
                .OrderBy(pair => pair.Value),
        ];

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InnerJoinProducesNoUnmatchedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new InnerJoinOrdersToUsersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every order references an existing user, so an inner join neither
        // drops nor duplicates order rows.
        Assert.Equal(orderRows.Count, result.Count);
    }
}
