using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join composed with later clauses (group by, and order by + pagination) to
// exercise the pipeline ordering over merged rows.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinWithClauses")]
public sealed class JoinWithClausesTests
{
    [Fact]
    public void InnerJoinThenGroupByStatusYieldsDistinctStatuses()
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
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            [new Field(new StringField("schema_with_foreign_keys.orders", "order_status"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        string?[] actual =
        [
            .. result.Column("order_status").OrderBy(s => s),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InnerJoinThenOrderByTotalWithPaginationReturnsWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.orders",
                            "order_total"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(1, 3)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderTotal)
                .Skip(1)
                .Take(3)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double("order_total")),
        ];

        Assert.Equal(expected, actual);
    }
}
