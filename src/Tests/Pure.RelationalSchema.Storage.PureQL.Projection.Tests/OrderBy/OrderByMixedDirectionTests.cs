using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.OrderBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// Multi-key ORDER BY where the keys sort in opposite directions (asc then desc).
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByMixedDirection")]
public sealed class OrderByMixedDirectionTests
{
    [Fact]
    public void OrderByStatusAscThenTotalDescOrdersWithinEachStatus()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new OrderByStatusAscThenTotalDescQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, double?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.OrderTotal)
                .Select(order => ((string?)order.OrderStatus, (double?)order.OrderTotal)),
        ];

        (string?, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.Double(new OrderTotalColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByActiveAscThenAgeDescOrdersWithinEachFlag()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByActiveAscThenAgeDescQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive)
                .ThenByDescending(user => user.UserAge)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }
}
