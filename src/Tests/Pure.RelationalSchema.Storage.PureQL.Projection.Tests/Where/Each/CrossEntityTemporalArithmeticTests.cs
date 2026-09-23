using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row temporal arithmetic whose operands come from both sides of a
// join: eachDateDiffDays(order date, user signup date) computes a per-row
// day gap across the merged row, then feeds a numeric comparison.
[Trait("Clause", "Where")]
[Trait("Feature", "EachDateArithmetic")]
public sealed class CrossEntityTemporalArithmeticTests
{
    [Fact]
    public void EachDateDiffDaysAcrossJoinedTablesFiltersByTheGap()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const double thresholdDays = 1200;

        Query query = new EachDateDiffDaysAcrossJoinedTablesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(order =>
                {
                    UserRecord user = userRows.Single(candidate =>
                        candidate.UserId == order.OrderUserId
                    );

                    int gap = order.PlacedOn.DayNumber
                        - user.SignupDate.DayNumber;

                    return gap > thresholdDays;
                })
                .Select(order => order.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }
}
