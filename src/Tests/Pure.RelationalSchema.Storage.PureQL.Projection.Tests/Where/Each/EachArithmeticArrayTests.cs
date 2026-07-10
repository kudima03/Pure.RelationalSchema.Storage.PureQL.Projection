using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row arithmetic where both operands are fields (array operands), so the
// operator combines two columns element-wise before the comparison/equality.
[Trait("Clause", "Where")]
[Trait("Feature", "EachArithmeticArray")]
public sealed class EachArithmeticArrayTests
{
    [Fact]
    public void EachAddOfAFieldToItselfDoublesItBeforeComparison()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachAdd(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(200))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(order => order.OrderTotal + order.OrderTotal > 200),
            result.Count
        );
    }

    [Fact]
    public void EachSubtractOfAFieldFromItselfIsZeroForEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachSubtract(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    [Fact]
    public void EachMultiplyOfAFieldByItselfSquaresItBeforeComparison()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachMultiply(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(10000))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(order => order.OrderTotal * order.OrderTotal > 10000),
            result.Count
        );
    }
}
