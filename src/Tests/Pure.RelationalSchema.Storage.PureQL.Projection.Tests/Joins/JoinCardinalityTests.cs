using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Join cardinality: a one-to-many join fans a left row out once per matching
// right row, and an outer join preserves a typed (non-string) column on the
// unmatched side's own values.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinCardinality")]
public sealed class JoinCardinalityTests
{
    [Fact]
    public void InnerJoinFansEachUserOutOncePerOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new InnerJoinUsersToOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. (
                from user in userRows
                join order in orderRows on user.UserId equals order.OrderUserId
                select user.UserName
            ).OrderBy(name => name),
        ];

        string?[] actual =
        [
            .. result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinPreservesADoubleColumnForEveryUser()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new LeftJoinUsersToOrdersSelectingAgeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.SelectMany(user =>
                    Enumerable.Repeat(
                        user.UserAge,
                        Math.Max(
                            1,
                            orderRows.Count(order => order.OrderUserId == user.UserId)
                        )
                    )
                )
                .OrderBy(age => age),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value)
                .OrderBy(age => age),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
