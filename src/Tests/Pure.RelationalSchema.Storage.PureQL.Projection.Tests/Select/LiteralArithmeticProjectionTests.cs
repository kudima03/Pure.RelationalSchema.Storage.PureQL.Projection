using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// A single-value Arithmetic whose operands are all literal constants
// (NumberScalar, or nested Arithmetic composed entirely of such) evaluates
// once, exactly like a plain scalar constant (ScalarCell), and repeats on
// every output row. Parameters, aggregates, and Count remain unsupported
// operands (ScalarUnsupportedTests) - this is strictly the literal-only
// case.
[Trait("Clause", "Select")]
[Trait("Feature", "LiteralArithmeticProjection")]
public sealed class LiteralArithmeticProjectionTests
{
    [Fact]
    public void NestedArithmeticOfLiteralsProjectsFoldedConstant()
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
                                new Multiply(
                                    [
                                        new NumberReturning(
                                            new Arithmetic(
                                                new Add(
                                                    [
                                                        new NumberReturning(
                                                            new NumberScalar(1)
                                                        ),
                                                        new NumberReturning(
                                                            new NumberScalar(2)
                                                        ),
                                                    ]
                                                )
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(3)),
                                    ]
                                )
                            )
                        )
                    ),
                    "result"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(["result"], result.ColumnNames);
        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, row => Assert.Equal(9, row.Double("result")));
    }

    [Fact]
    public void LiteralArithmeticSubtractAndDivideFoldLeftToRight()
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
                                new Divide(
                                    [
                                        new NumberReturning(
                                            new Arithmetic(
                                                new Subtract(
                                                    [
                                                        new NumberReturning(
                                                            new NumberScalar(10)
                                                        ),
                                                        new NumberReturning(
                                                            new NumberScalar(4)
                                                        ),
                                                    ]
                                                )
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        )
                    ),
                    "result"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.All(result.Rows, row => Assert.Equal(3, row.Double("result")));
    }

    [Fact]
    public void LiteralArithmeticDivideByZeroFailsFast()
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
                                new Divide(
                                    [
                                        new NumberReturning(new NumberScalar(1)),
                                        new NumberReturning(new NumberScalar(0)),
                                    ]
                                )
                            )
                        )
                    ),
                    "result"
                ),
            ]
        );

        _ = Assert.Throws<DivideByZeroException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
