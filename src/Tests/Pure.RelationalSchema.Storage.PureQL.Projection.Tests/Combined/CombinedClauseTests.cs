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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// End-to-end queries that combine several clauses, checking they compose into
// one correct result set.
[Trait("Clause", "Combined")]
[Trait("Feature", "CombinedClauses")]
public sealed class CombinedClauseTests
{
    [Fact]
    public void WhereThenOrderByThenPaginateReturnsCorrectWindow()
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
                        new NumberReturning(new NumberScalar(50))
                    )
                )
            ),
            join: null,
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
            new global::PureQL.CSharp.Model.Pagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 50)
                .OrderBy(order => order.OrderTotal)
                .Skip(1)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double("order_total")),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WhereThenGroupByYieldsGroupsOfTheFilteredRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
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
                                new StringReturning(new StringScalar("cancelled"))
                            )
                        )
                    )
                )
            ),
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

        int expected = orderRows
            .Where(order => order.OrderStatus != "cancelled")
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void JoinThenWhereThenOrderByThenPaginateReturnsCorrectWindow()
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
                        new NumberReturning(new NumberScalar(75))
                    )
                )
            ),
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
                    SortDirection.Desc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(0, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 75)
                .OrderByDescending(order => order.OrderTotal)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double("order_total")),
        ];

        Assert.Equal(expected, actual);
    }
}
