using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// A group-mode select list may mix the grouping key with one or more
// aggregates in a single row: each output row pairs the group's key value
// with its folded aggregate value(s), not just the aggregate alone.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "MixedProjection")]
public sealed class MixedProjectionTests
{
    private static Join UsersToOrdersJoin()
    {
        return new Join(
            JoinType.Inner,
            "schema_with_foreign_keys.orders",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void GroupKeyAndSumProjectTogetherPerGroup()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "userTotal"
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

        Dictionary<Guid, double> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid("order_user_id")!.Value,
            row => row.Double("userTotal")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupKeyAndCountProjectTogetherPerGroup()
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
                    "statusCount"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new StringField(
                        "schema_with_foreign_keys.orders",
                        "order_status"
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

        Dictionary<string, double> expected = orderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row["order_status"]!,
            row => row.Double("statusCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MultipleAggregatesOfDifferentTypesProjectInOneRow()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "totalSum"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MinDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_on"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "earliestPlacedOn"
                ),
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
                    "maxStatus"
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

        Dictionary<Guid, (double Count, double Sum, DateOnly Min, string Max)> expected =
            orderRows
                .GroupBy(order => order.OrderUserId)
                .ToDictionary(
                    group => group.Key,
                    group => (
                        Count: (double)group.Count(),
                        Sum: group.Sum(order => order.OrderTotal),
                        Min: group.Min(order => order.PlacedOn),
                        Max: group.Select(order => order.OrderStatus)
                            .Max(StringComparer.Ordinal)!
                    )
                );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid("order_user_id")!.Value;
                (double Count, double Sum, DateOnly Min, string Max) expectedGroup =
                    expected[userId];
                Assert.Equal(expectedGroup.Count, row.Double("orderCount"));
                Assert.Equal(expectedGroup.Sum, row.Double("totalSum"));
                Assert.Equal(expectedGroup.Min, row.Date("earliestPlacedOn"));
                Assert.Equal(expectedGroup.Max, row["maxStatus"]);
            }
        );
    }

    [Fact]
    public void TwoNumericAggregatesOverDifferentColumnsProjectIndependently()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "orderTotalSum"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new AverageNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.users",
                                            "user_age"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "avgAge"
                ),
            ],
            where: null,
            [UsersToOrdersJoin()],
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.users",
                        "user_id"
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

        Dictionary<Guid, (double Sum, double Avg)> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, order.OrderTotal, user.UserAge)
            )
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (
                    Sum: group.Sum(row => row.OrderTotal),
                    Avg: group.Average(row => row.UserAge)
                )
            );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid("user_id")!.Value;
                (double Sum, double Avg) expectedGroup = expected[userId];
                Assert.Equal(expectedGroup.Sum, row.Double("orderTotalSum"));
                Assert.Equal(expectedGroup.Avg, row.Double("avgAge"));
            }
        );
    }

    [Fact]
    public void AggregateColumnsFollowAliasesInMixedProjection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
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
                    ),
                    "buyer"
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
                    "purchases"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "spend"
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

        Assert.Equal(["buyer", "purchases", "spend"], result.ColumnNames);
    }
}
