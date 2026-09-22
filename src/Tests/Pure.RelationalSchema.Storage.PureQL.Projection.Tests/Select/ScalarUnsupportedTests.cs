using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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

// Scalar constants, and single-value Arithmetic whose operands are all
// literal constants, are the only supported non-aggregate
// SingleValueReturning select projections. Parameters (no binding API),
// Arithmetic containing a parameter or aggregate operand, and boolean
// composites still have no defined result through this entry point, so the
// translator fails fast with NotSupportedException instead of silently
// producing wrong cells. These tests pin that explicit-failure contract.
[Trait("Clause", "Select")]
[Trait("Feature", "ScalarProjection")]
public sealed class ScalarUnsupportedTests
{
    [Fact]
    public void NumberParameterInSelectFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
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
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void SingleValueArithmeticWithParameterOperandInSelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Arithmetic(
                                new Add(
                                    [
                                        new NumberReturning(new NumberScalar(1)),
                                        new NumberReturning(
                                            new NumberParameter("bonus")
                                        ),
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
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void BooleanCompositeInSelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
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
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void ParameterAlongsideAggregateFailsFastInGroupMode()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
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
                                            "schema_with_foreign_keys.orders",
                                            "order_id"
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
            new PureQLProjection(datasets, query)
        );
    }
}
