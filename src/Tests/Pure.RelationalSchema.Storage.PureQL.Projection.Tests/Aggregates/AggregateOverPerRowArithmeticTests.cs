using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// An aggregate's argument may be any array-returning expression, including
// per-row arithmetic over fields of both joined tables: sum(qty * price)
// folds the computed per-row products, not a stored column.
[Trait("Clause", "Select")]
[Trait("Feature", "AggregateOverArithmetic")]
public sealed class AggregateOverPerRowArithmeticTests
{
    private static Join ItemsToProductsJoin()
    {
        return new Join(
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
        );
    }

    private static SelectExpression SumOfQuantityTimesPrice(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(
                new NumberReturning(
                    new NumberAggregate(
                        new SumNumber(
                            new NumberArrayReturning(
                                new EachArithmetic(
                                    new EachMultiply(
                                        [
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    "schema_with_foreign_keys.order_items",
                                                    "item_qty"
                                                )
                                            ),
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    "schema_with_foreign_keys.products",
                                                    "product_price"
                                                )
                                            ),
                                        ]
                                    )
                                )
                            )
                        )
                    )
                )
            ),
            alias
        );
    }

    private static double PriceOf(
        IReadOnlyList<ProductRecord> productRows,
        Guid productId
    )
    {
        return productRows
            .Single(product => product.ProductId == productId)
            .ProductPrice;
    }

    [Fact]
    public void SumOfQuantityTimesPriceGroupedByOrderComputesRevenue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.order_items"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.order_items",
                                "item_order_id"
                            )
                        )
                    )
                ),
                SumOfQuantityTimesPrice("revenue"),
            ],
            where: null,
            [ItemsToProductsJoin()],
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.order_items",
                        "item_order_id"
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.ItemQty * PriceOf(productRows, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("item_order_id")!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetSumOfQuantityTimesPriceComputesTotalRevenue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.order_items"),
            [SumOfQuantityTimesPrice("revenue")],
            where: null,
            [ItemsToProductsJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expected = orderItemRows.Sum(item =>
            item.ItemQty * PriceOf(productRows, item.ItemProductId)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("revenue"));
    }
}
