using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row boolean composition: eachAnd / eachOr over per-row conditions, and
// eachNot (PureQL has no eachNotEqual; negation is expressed as not(eachEqual)).
[Trait("Clause", "Where")]
[Trait("Feature", "EachBooleanOps")]
public sealed class EachBooleanOpsTests
{
    [Fact]
    public void EachAndKeepsRowsSatisfyingEveryCondition()
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
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
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
                                            "schema_with_foreign_keys.orders",
                                            "order_status"
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                order.OrderTotal > 100 && order.OrderStatus == "shipped"
            ),
            result.Count
        );
    }

    [Fact]
    public void EachOrKeepsRowsSatisfyingAnyCondition()
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
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            "schema_with_foreign_keys.orders",
                                            "order_status"
                                        )
                                    ),
                                    new StringReturning(new StringScalar("cancelled"))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThanOrEqual,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(300))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                order.OrderStatus == "cancelled" || order.OrderTotal >= 300
            ),
            result.Count
        );
    }

    [Fact]
    public void EachNotNegatesAPerRowCondition()
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
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachStringEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        "schema_with_foreign_keys.orders",
                                        "order_status"
                                    )
                                ),
                                new StringReturning(new StringScalar("shipped"))
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderStatus != "shipped"),
            result.Count
        );
    }
}
