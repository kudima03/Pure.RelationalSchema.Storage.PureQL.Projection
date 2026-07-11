using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Deeply nested per-row boolean trees (eachAnd / eachOr / eachNot composed
// several levels deep), exercising the recursive predicate builder.
[Trait("Clause", "Where")]
[Trait("Feature", "EachNestedBoolean")]
public sealed class EachNestedBooleanTests
{
    [Fact]
    public void AndOfOrAndNotFiltersByTheCombinedPredicate()
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
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachGreaterThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(100))
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachStringEquality(
                                                new StringArrayReturning(
                                                    new StringField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Status
                                                    )
                                                ),
                                                new StringReturning(
                                                    new StringScalar("pending")
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachNotOperator(
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachStringEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Status
                                                )
                                            ),
                                            new StringReturning(
                                                new StringScalar("cancelled")
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
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
            db.OrderRows.Count(order =>
                (order.OrderTotal > 100 || order.OrderStatus == "pending")
                && order.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    [Fact]
    public void OrOfTwoAndBranchesFiltersByEitherCombination()
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
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachAndOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachStringEquality(
                                                new StringArrayReturning(
                                                    new StringField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Status
                                                    )
                                                ),
                                                new StringReturning(
                                                    new StringScalar("shipped")
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachGreaterThanOrEqual,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(200))
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachAndOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachStringEquality(
                                                new StringArrayReturning(
                                                    new StringField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Status
                                                    )
                                                ),
                                                new StringReturning(
                                                    new StringScalar("pending")
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachLessThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(100))
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                    ]
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
            db.OrderRows.Count(order =>
                (order.OrderStatus == "shipped" && order.OrderTotal >= 200)
                || (order.OrderStatus == "pending" && order.OrderTotal < 100)
            ),
            result.Count
        );
    }

    [Fact]
    public void DoubleNegationIsEquivalentToTheInnerCondition()
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
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachNotOperator(
                            new BooleanArrayReturning(
                                new EachEquality(
                                    new EachStringEquality(
                                        new StringArrayReturning(
                                            new StringField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Status
                                            )
                                        ),
                                        new StringReturning(new StringScalar("shipped"))
                                    )
                                )
                            )
                        )
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
            db.OrderRows.Count(order => order.OrderStatus == "shipped"),
            result.Count
        );
    }
}
