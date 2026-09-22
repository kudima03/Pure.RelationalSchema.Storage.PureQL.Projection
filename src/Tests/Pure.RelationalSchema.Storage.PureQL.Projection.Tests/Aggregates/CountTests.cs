using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// `count` counts the rows in each group, regardless of the counted column's
// type.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "Count")]
public sealed class CountTests
{
    [Fact]
    public void CountOfUuidColumnPerUserProjectsGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
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
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
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
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfStringColumnPerUserProjectsSameGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new StringArrayReturning(
                                        new StringField(
                                            "schema_with_foreign_keys.orders",
                                            "order_status"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
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
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
