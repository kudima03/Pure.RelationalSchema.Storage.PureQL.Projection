using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// String aggregates are limited to min / max (there is no string sum/avg).
// They fold each group's values using ordinal comparison.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "StringAggregate")]
public sealed class StringAggregateTests
{
    [Fact]
    public void MinStatusPerUserProjectsGroupMinimum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MinString(
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
                    "min_status"
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

        string?[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Min(order => order.OrderStatus))
                .OrderBy(value => value, StringComparer.Ordinal),
        ];

        string?[] actual =
        [
            .. result.Column("min_status").OrderBy(v => v, StringComparer.Ordinal),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxStatusPerUserProjectsGroupMaximum()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MaxString(
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
                    "max_status"
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

        string?[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.OrderStatus))
                .OrderBy(value => value, StringComparer.Ordinal),
        ];

        string?[] actual =
        [
            .. result.Column("max_status").OrderBy(v => v, StringComparer.Ordinal),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
