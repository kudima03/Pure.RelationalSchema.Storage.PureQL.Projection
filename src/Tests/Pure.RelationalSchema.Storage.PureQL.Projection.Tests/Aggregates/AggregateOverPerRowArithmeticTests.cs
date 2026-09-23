using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// An aggregate's argument may be any array-returning expression, including
// per-row arithmetic over fields of both joined tables: sum(qty * price)
// folds the computed per-row products, not a stored column.
[Trait("Clause", "Select")]
[Trait("Feature", "AggregateOverArithmetic")]
public sealed class AggregateOverPerRowArithmeticTests
{
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

        Query query = new SumOfQuantityTimesPriceGroupedByOrderQuery().Value;

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
            row => row.Uuid(new ItemOrderIdColumn().Name.TextValue)!.Value,
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

        Query query = new WholeSetSumOfQuantityTimesPriceQuery().Value;

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
