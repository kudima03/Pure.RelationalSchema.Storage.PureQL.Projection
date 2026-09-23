using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A WHERE predicate that references a column from the joined (right) table,
// proving the filter sees the merged row's columns from both sides.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinWhereOnRightColumn")]
public sealed class JoinWhereOnRightColumnTests
{
    [Fact]
    public void InnerJoinThenWhereOnUserAgeFiltersByTheRightTable()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinThenWhereOnUserAgeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order =>
            userRows.Single(user => user.UserId == order.OrderUserId).UserAge > 30
        );

        Assert.Equal(expected, result.Count);
    }
}
