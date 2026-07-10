using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON is a composite condition: an each-equality on the key AND an
// each-comparison filter, combined with eachAnd.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeJoinCondition")]
public sealed class CompositeJoinConditionTests
{
    [Fact]
    public void InnerJoinOnKeyAndQuantityKeepsMatchingHighQuantityItems()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.Qty
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.OrderItems.Entity,
                    new BooleanArrayReturning(
                        new EachAndOperator(
                            [
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachUuidEquality(
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    SampleDatabase.OrderItems.Entity,
                                                    SampleDatabase.OrderItems.OrderId
                                                )
                                            ),
                                            new UuidArrayReturning(
                                                new UuidField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Id
                                                )
                                            )
                                        )
                                    )
                                ),
                                new BooleanArrayReturning(
                                    new EachComparison(
                                        new EachNumberComparison(
                                            EachComparisonOperator.EachGreaterThan,
                                            new NumberArrayReturning(
                                                new NumberField(
                                                    SampleDatabase.OrderItems.Entity,
                                                    SampleDatabase.OrderItems.Qty
                                                )
                                            ),
                                            new NumberReturning(new NumberScalar(1))
                                        )
                                    )
                                ),
                            ]
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

        int expected = (
            from order in db.OrderRows
            from item in db.OrderItemRows
            where item.ItemOrderId == order.OrderId && item.ItemQty > 1
            select 1
        ).Count();

        Assert.Equal(expected, result.Count);
    }
}
