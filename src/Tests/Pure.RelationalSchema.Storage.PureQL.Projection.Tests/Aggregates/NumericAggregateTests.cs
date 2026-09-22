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
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Numeric aggregates (sum / avg / min / max) over a group or the whole set.
// With groupBy they fold each group; without it they fold the whole set into
// a single result row.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "NumericAggregate")]
public sealed class NumericAggregateTests
{
    [Fact]
    public void AverageOfTotalPerUserProjectsGroupMean()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new AverageNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderTotalColumn().Name.TextValue
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "avg_total"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                        new OrderUserIdColumn().Name.TextValue
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Average(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("avg_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfTotalPerUserProjectsGroupMinimum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MinNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderTotalColumn().Name.TextValue
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "min_total"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                        new OrderUserIdColumn().Name.TextValue
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Min(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("min_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfTotalPerUserProjectsGroupMaximum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MaxNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderTotalColumn().Name.TextValue
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_total"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                        new OrderUserIdColumn().Name.TextValue
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.OrderTotal))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("max_total")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SumOfAllTotalsProjectsSingleWholeSetValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderTotalColumn().Name.TextValue
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "sum_total"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            orderRows.Sum(order => order.OrderTotal),
            result.Row(0).Double("sum_total")
        );
    }
}
