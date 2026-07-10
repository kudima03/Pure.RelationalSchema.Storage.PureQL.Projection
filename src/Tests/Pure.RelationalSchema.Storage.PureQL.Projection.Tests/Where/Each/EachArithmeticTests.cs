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

// Per-row arithmetic (eachAdd/Subtract/Multiply/Divide) computed against a
// field and then compared/equated so the result forms a row predicate. Each
// arithmetic node always yields a per-row vector, even over scalar operands.
[Trait("Clause", "Where")]
[Trait("Feature", "EachArithmetic")]
public sealed class EachArithmeticTests
{
    [Fact]
    public void EachAddShiftsFieldBeforeComparison()
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
                                        new NumberReturning(new NumberScalar(10)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(110))
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
            db.OrderRows.Count(order => order.OrderTotal + 10 > 110),
            result.Count
        );
    }

    [Fact]
    public void EachSubtractShiftsFieldBeforeEquality()
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
                                        new NumberReturning(new NumberScalar(0.5)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(100))
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
            db.OrderRows.Count(order => order.OrderTotal - 0.5 == 100),
            result.Count
        );
    }

    [Fact]
    public void EachMultiplyScalesFieldBeforeComparison()
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
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(400))
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
            db.OrderRows.Count(order => order.OrderTotal * 2 > 400),
            result.Count
        );
    }

    [Fact]
    public void EachDivideScalesFieldBeforeComparison()
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
                        EachComparisonOperator.EachLessThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachDivide(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(40))
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
            db.OrderRows.Count(order => order.OrderTotal / 2 < 40),
            result.Count
        );
    }
}
