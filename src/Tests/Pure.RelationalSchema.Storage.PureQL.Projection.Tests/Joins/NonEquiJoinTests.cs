using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON condition is an inequality (each-comparison) rather than an
// equi-key: every left/right pair whose columns satisfy the comparison is kept.
[Trait("Clause", "Join")]
[Trait("Feature", "NonEquiJoin")]
public sealed class NonEquiJoinTests
{
    [Fact]
    public void InnerJoinOnPriceLessThanTotalKeepsEveryQualifyingPair()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Name
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Products.Entity,
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachNumberComparison(
                                EachComparisonOperator.EachLessThan,
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Products.Entity,
                                        SampleDatabase.Products.Price
                                    )
                                ),
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.Total
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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows.Sum(order =>
            db.ProductRows.Count(product => product.ProductPrice < order.OrderTotal)
        );

        Assert.Equal(expected, result.Count);
    }
}
