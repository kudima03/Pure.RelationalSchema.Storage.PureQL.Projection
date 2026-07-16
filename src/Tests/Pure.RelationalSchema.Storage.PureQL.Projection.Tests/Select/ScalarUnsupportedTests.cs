using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Scalar constants are the only supported non-aggregate SingleValueReturning
// select projections. Parameters (no binding API), single-value arithmetic
// and boolean composites still have no defined result through this entry
// point, so the translator fails fast with NotSupportedException instead of
// silently producing wrong cells. These tests pin that explicit-failure
// contract.
[Trait("Clause", "Select")]
[Trait("Feature", "ScalarProjection")]
public sealed class ScalarUnsupportedTests
{
    [Fact]
    public void NumberParameterInSelectFailsFastWithoutBinding()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberParameter("limit"))
                    ),
                    "limit"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(db.Datasets, query)
        );
    }

    [Fact]
    public void SingleValueArithmeticInSelectFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Arithmetic(
                                new Add(
                                    [
                                        new NumberReturning(new NumberScalar(1)),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        )
                    ),
                    "sum"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(db.Datasets, query)
        );
    }

    [Fact]
    public void BooleanCompositeInSelectFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new BooleanReturning(
                            new Comparison(
                                new NumberComparison(
                                    ComparisonOperator.GreaterThan,
                                    new NumberReturning(new NumberScalar(2)),
                                    new NumberReturning(new NumberScalar(1))
                                )
                            )
                        )
                    ),
                    "flag"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(db.Datasets, query)
        );
    }

    [Fact]
    public void ParameterAlongsideAggregateFailsFastInGroupMode()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringParameter("scope"))
                    ),
                    "scope"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "order_count"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(db.Datasets, query)
        );
    }
}
