using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.OrderBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// ORDER BY over a joined (entity-qualified) column: the primary key comes
// from the joined table, the secondary key from the base table, so ordering
// must resolve both sides of the merged row.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "JoinedColumn")]
public sealed class OrderByJoinedColumnTests
{
    [Fact]
    public void OrderByJoinedNameThenBaseTotalDescOrdersMergedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new OrderByJoinedNameThenBaseTotalDescQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected =
        [
            .. orderRows
                .Select(order =>
                    (
                        Name: userRows.Single(user =>
                            user.UserId == order.OrderUserId
                        ).UserName,
                        Total: order.OrderTotal
                    )
                )
                .OrderBy(pair => pair.Name, StringComparer.Ordinal)
                .ThenByDescending(pair => pair.Total),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new UserNameColumn().Name.TextValue]!,
                    row.Double(new OrderTotalColumn().Name.TextValue)!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
