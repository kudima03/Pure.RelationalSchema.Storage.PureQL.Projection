using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Composite per-row on conditions (and / or / not around equalities and
// comparisons) on inner and outer joins. Expected row sets are computed
// pair-by-pair from the ground-truth records.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeCondition")]
public sealed class CompositeOuterJoinConditionTests
{
    [Fact]
    public void LeftJoinOnKeyAndThresholdPadsUsersWithoutQualifyingOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        const double threshold = 100;

        Query query = new LeftJoinOnKeyAndThresholdQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            Math.Max(
                1,
                orderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
                )
            )
        );

        Assert.Equal(expectedCount, result.Count);

        foreach (UserRecord user in userRows)
        {
            int expectedAppearances = Math.Max(
                1,
                orderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
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

    [Fact]
    public void InnerJoinOnDisjunctiveConditionKeepsEitherMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        const double markerTotal = 200;

        Query query = new InnerJoinOnDisjunctiveConditionQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            orderRows.Count(order =>
                order.OrderUserId == user.UserId
                || order.OrderTotal == markerTotal
            )
        );

        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void InnerJoinOnNegatedKeyEqualityKeepsOnlyNonMatchingPairs()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new InnerJoinOnNegatedKeyEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            orderRows.Count(order => order.OrderUserId != user.UserId)
        );

        Assert.Equal(expectedCount, result.Count);
    }
}
