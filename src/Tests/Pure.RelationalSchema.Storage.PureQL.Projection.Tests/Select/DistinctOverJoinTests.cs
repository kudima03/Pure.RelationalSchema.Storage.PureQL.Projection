using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// SELECT DISTINCT deduplicates the projected rows after a join fans the
// base rows out, so duplicates introduced by the join collapse back to the
// distinct projected value set.
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctOverJoinTests
{
    [Fact]
    public void DistinctCollapsesJoinFanOutDuplicates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOverUsersToOrdersJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user =>
                    orderRows.Any(order => order.OrderUserId == user.UserId)
                )
                .Select(user => user.UserName)
                .OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void DistinctOnJoinedColumnCollapsesToItsDistinctValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOnJoinedColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status),
        ];

        Assert.Equal(
            expected,
            result
                .Column(new OrderStatusColumn().Name.TextValue)
                .OrderBy(status => status)
                .ToArray()
        );
    }
}
