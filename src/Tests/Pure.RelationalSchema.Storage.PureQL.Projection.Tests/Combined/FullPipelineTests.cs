using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Every clause at once, in pipeline order: JOIN -> WHERE -> ORDER BY ->
// GROUP BY -> HAVING -> projection -> pagination. The expected window is
// computed step-by-step from the ground-truth records.
[Trait("Clause", "Combined")]
[Trait("Feature", "FullPipeline")]
public sealed class FullPipelineTests
{
    [Fact]
    public void JoinWhereGroupByHavingOrderByPaginationCompose()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
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
                    "orderCount"
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.users",
                                "user_active"
                            )
                        ),
                        new BooleanReturning(new BooleanScalar(true))
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
            [
                new Field(
                    new StringField(
                        "schema_with_foreign_keys.orders",
                        "order_status"
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
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
                        ),
                        new NumberReturning(new NumberScalar(1))
                    )
                )
            ),
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            "schema_with_foreign_keys.orders",
                            "order_status"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected =
        [
            .. orderRows
                .Where(order =>
                    userRows.Single(user =>
                        user.UserId == order.OrderUserId
                    ).UserActive
                )
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, (double)group.Count()))
                .Skip(1)
                .Take(2),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row["order_status"]!,
                    row.Double("orderCount")!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
