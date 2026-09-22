using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// The each* right operand is OneOf<XReturning, XArrayReturning>. The other
// tests broadcast a scalar (XReturning); these use a second field
// (XArrayReturning), so the operator zips two per-row columns element-wise.
[Trait("Clause", "Where")]
[Trait("Feature", "EachArrayOperand")]
public sealed class EachArrayOperandTests
{
    [Fact]
    public void EachEqualityOfANumberFieldWithItselfKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
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

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void EachGreaterThanOfANumberFieldWithItselfRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
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
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
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

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void EachEqualityOfTwoDistinctUuidFieldsRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderIdColumn().Name.TextValue
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderUserIdColumn().Name.TextValue
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
            orderRows.Count(order => order.OrderId == order.OrderUserId),
            result.Count
        );
    }

    [Fact]
    public void EachNotOfTwoDistinctUuidFieldsKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                        new OrderIdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                        new OrderUserIdColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderId != order.OrderUserId),
            result.Count
        );
    }
}
