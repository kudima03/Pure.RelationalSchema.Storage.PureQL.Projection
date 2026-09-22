using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

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

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.products",
                                "product_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.order_items",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.order_items",
                                        "item_order_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_id"
                                    )
                                )
                            )
                        )
                    )
                ),
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.products",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.order_items",
                                        "item_product_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.products",
                                        "product_id"
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

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
                        row["order_status"],
                        row["product_name"]
                    )
                )
                .OrderBy(pair => pair.Item1)
                .ThenBy(pair => pair.Item2),
        ];

        Assert.Equal(expected, actual);
    }
}
