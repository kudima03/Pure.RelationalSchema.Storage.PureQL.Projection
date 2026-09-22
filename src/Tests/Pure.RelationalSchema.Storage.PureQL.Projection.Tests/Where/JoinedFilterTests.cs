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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where;

// WHERE evaluated over merged join rows: predicates may mix columns of both
// tables, reference a joined boolean field directly, or negate a condition
// on the joined side. Expected sets are computed pair-by-pair from the
// ground-truth records.
[Trait("Clause", "Where")]
[Trait("Feature", "JoinedFilter")]
public sealed class JoinedFilterTests
{
    private static Join OrdersToUsersJoin()
    {
        return new Join(
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
        );
    }

    private static Query OrdersWithUsers(BooleanArrayReturning where)
    {
        return new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
                            )
                        )
                    )
                ),
            ],
            where,
            [OrdersToUsersJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    private static UserRecord UserOf(
        IReadOnlyList<UserRecord> userRows,
        OrderRecord order
    )
    {
        return userRows.Single(user => user.UserId == order.OrderUserId);
    }

    [Fact]
    public void WhereConjunctionAcrossBothTablesFiltersMergedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const double ageThreshold = 30;
        const double totalThreshold = 100;

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThanOrEqual,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.users",
                                            "user_age"
                                        )
                                    ),
                                    new NumberReturning(
                                        new NumberScalar(ageThreshold)
                                    )
                                )
                            )
                        ),
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
                                    new NumberReturning(
                                        new NumberScalar(totalThreshold)
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order =>
            UserOf(userRows, order).UserAge >= ageThreshold
            && order.OrderTotal > totalThreshold
        );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereOnJoinedBooleanFieldKeepsRowsWhereItIsTrue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new BooleanField(
                    "schema_with_foreign_keys.users",
                    "user_active"
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order => UserOf(userRows, order).UserActive);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void WhereNegationOnJoinedColumnExcludesItsRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const string excludedName = "Ann";

        Query query = OrdersWithUsers(
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachStringEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        "schema_with_foreign_keys.users",
                                        "user_name"
                                    )
                                ),
                                new StringReturning(
                                    new StringScalar(excludedName)
                                )
                            )
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Count(order =>
            UserOf(userRows, order).UserName != excludedName
        );

        Assert.Equal(expected, result.Count);
    }
}
