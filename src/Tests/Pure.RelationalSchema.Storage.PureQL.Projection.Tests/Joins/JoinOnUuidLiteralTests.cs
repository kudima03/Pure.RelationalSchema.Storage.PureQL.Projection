using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON condition compares a column to a uuid literal (the shape of
// issue #92: ON orders.user_id = '0000...'). The each-equality must actually
// gate the merged rows: a never-matching literal empties an inner join and
// pads a left join, a matching literal keeps only the satisfying left rows
// (crossed with every right row, since the condition does not constrain the
// right side).
[Trait("Clause", "Join")]
[Trait("Feature", "JoinOnUuidLiteral")]
public sealed class JoinOnUuidLiteralTests
{
    [Fact]
    public void InnerJoinOnNeverMatchingUuidLiteralReturnsNoRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new InnerJoinOnNeverMatchingUuidLiteralQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void InnerJoinOnMatchingUuidLiteralKeepsOnlySatisfyingLeftRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Guid annId = userRows[0].UserId;

        Query query = new InnerJoinOnMatchingUuidLiteralQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // The condition constrains only the left side, so each satisfying
        // order pairs with every user row.
        int expected =
            orderRows.Count(order => order.OrderUserId == annId)
            * userRows.Count;

        Assert.Equal(expected, result.Count);

        double[] expectedTotals =
        [
            .. orderRows
                .Where(order => order.OrderUserId == annId)
                .Select(order => order.OrderTotal)
                .OrderBy(total => total),
        ];

        Assert.Equal(
            expectedTotals,
            result.Rows
                .Select(row => row.Double("hours") ?? double.NaN)
                .Distinct()
                .OrderBy(total => total)
        );
    }

    [Fact]
    public void LeftJoinOnNeverMatchingUuidLiteralPadsEveryLeftRowOnce()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinOnNeverMatchingUuidLiteralQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(string.Empty, row["customer"]));
    }
}
