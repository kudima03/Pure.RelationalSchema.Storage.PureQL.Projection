using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Scalar;

// Single-value range comparison over two constant operands, one per comparable
// value type. A true constant comparison keeps every row; a false one removes
// all rows.
[Trait("Clause", "Where")]
[Trait("Feature", "ScalarComparison")]
public sealed class ScalarComparisonTests
{
    [Fact]
    public void ScalarNumberGreaterThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(new NumberScalar(2)),
                        new NumberReturning(new NumberScalar(1))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarNumberLessThanFalseConstantRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThan,
                        new NumberReturning(new NumberScalar(2)),
                        new NumberReturning(new NumberScalar(1))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void ScalarStringGreaterThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new global::PureQL.CSharp.Model.Comparisons.StringComparison(
                        ComparisonOperator.GreaterThan,
                        new StringReturning(new StringScalar("b")),
                        new StringReturning(new StringScalar("a"))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarDateGreaterThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.GreaterThan,
                        new DateReturning(new DateScalar(new DateOnly(2024, 1, 2))),
                        new DateReturning(new DateScalar(new DateOnly(2024, 1, 1)))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarTimeGreaterThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.GreaterThan,
                        new TimeReturning(new TimeScalar(new TimeOnly(10, 0, 0))),
                        new TimeReturning(new TimeScalar(new TimeOnly(9, 0, 0)))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarDateTimeGreaterThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.GreaterThan,
                        new DateTimeReturning(
                            new DateTimeScalar(new DateTime(2024, 1, 1, 13, 0, 0))
                        ),
                        new DateTimeReturning(
                            new DateTimeScalar(new DateTime(2024, 1, 1, 12, 0, 0))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarNumberLessThanOrEqualTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThanOrEqual,
                        new NumberReturning(new NumberScalar(1)),
                        new NumberReturning(new NumberScalar(2))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarNumberGreaterThanOrEqualFalseConstantRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        new NumberReturning(new NumberScalar(1)),
                        new NumberReturning(new NumberScalar(2))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void ScalarDateLessThanTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.LessThan,
                        new DateReturning(new DateScalar(new DateOnly(2024, 1, 1))),
                        new DateReturning(new DateScalar(new DateOnly(2024, 1, 2)))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarTimeLessThanOrEqualTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        TimeOnly value = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new TimeComparison(
                        ComparisonOperator.LessThanOrEqual,
                        new TimeReturning(new TimeScalar(value)),
                        new TimeReturning(new TimeScalar(value))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarDateTimeGreaterThanOrEqualTrueConstantKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime value = new DateTime(2024, 1, 1, 12, 0, 0);

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new DateTimeComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        new DateTimeReturning(new DateTimeScalar(value)),
                        new DateTimeReturning(new DateTimeScalar(value))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void ScalarStringLessThanFalseConstantRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new global::PureQL.CSharp.Model.Comparisons.StringComparison(
                        ComparisonOperator.LessThan,
                        new StringReturning(new StringScalar("b")),
                        new StringReturning(new StringScalar("a"))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
