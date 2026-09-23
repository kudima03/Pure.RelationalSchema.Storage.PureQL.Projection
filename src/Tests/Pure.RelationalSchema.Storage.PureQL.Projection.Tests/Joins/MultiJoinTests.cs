using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A chain of two joins: orders -> order_items -> products. Columns from all
// three tables are available on the merged rows (names are globally unique).
[Trait("Clause", "Join")]
[Trait("Feature", "MultiJoin")]
public sealed class MultiJoinTests
{
    [Fact]
    public void ChainedInnerJoinsEnrichEachItemWithOrderAndProduct()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new ChainedInnerJoinsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, string?)[] expected =
        [
            .. (
                from item in orderItemRows
                join order in orderRows on item.ItemOrderId equals order.OrderId
                join product in productRows
                    on item.ItemProductId equals product.ProductId
                select ((string?)order.OrderStatus, (string?)product.ProductName)
            ).OrderBy(pair => pair.Item1).ThenBy(pair => pair.Item2),
        ];

        (string?, string?)[] actual =
        [
            .. result
                .Rows.Select(row =>
                    (
                        row[new OrderStatusColumn().Name.TextValue],
                        row[new ProductNameColumn().Name.TextValue]
                    )
                )
                .OrderBy(pair => pair.Item1)
                .ThenBy(pair => pair.Item2),
        ];

        Assert.Equal(expected, actual);
    }
}
