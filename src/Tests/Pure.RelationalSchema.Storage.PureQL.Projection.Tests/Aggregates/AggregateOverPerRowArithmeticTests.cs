using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            SampleDatabase.Products.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.ProductId
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Id
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
                                                    SampleDatabase.OrderItems.Entity,
                                                    SampleDatabase.OrderItems.Qty
                                                )
                                            ),
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    SampleDatabase.Products.Entity,
                                                    SampleDatabase.Products.Price
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

    private static double PriceOf(SampleDatabase db, Guid productId)
    {
        return db.ProductRows
            .Single(product => product.ProductId == productId)
            .ProductPrice;
    }

    [Fact]
    public void SumOfQuantityTimesPriceGroupedByOrderComputesRevenue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
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
                        SampleDatabase.OrderItems.Entity,
                        SampleDatabase.OrderItems.OrderId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.ItemQty * PriceOf(db, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.OrderItems.OrderId)!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetSumOfQuantityTimesPriceComputesTotalRevenue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [SumOfQuantityTimesPrice("revenue")],
            where: null,
            [ItemsToProductsJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expected = db.OrderItemRows.Sum(item =>
            item.ItemQty * PriceOf(db, item.ItemProductId)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("revenue"));
    }
}
