using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// LEFT / RIGHT / FULL joins. Assertions target the well-defined parts of outer
// joins (row counts and preserved-side values). The behaviour of null-extended
// columns on the unmatched side is spec-ambiguous (see Semantics/README.md) and
// is deliberately not asserted here; every test selects only a column that is
// present in all result rows.
[Trait("Clause", "Join")]
[Trait("Feature", "OuterJoin")]
public sealed class OuterJoinTests
{
    [Fact]
    public void LeftJoinKeepsUsersWithNoOrders()
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
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Left,
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
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            Math.Max(1, orderRows.Count(order => order.OrderUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        // Eve has no orders and must still appear exactly once.
        Assert.Equal(
            1,
            result.Column("user_name").Count(name => name == "Eve")
        );
        // Ann has two orders and must appear once per matched order.
        Assert.Equal(
            2,
            result.Column("user_name").Count(name => name == "Ann")
        );
    }

    [Fact]
    public void RightJoinKeepsUnmatchedUsersOnTheRightSide()
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
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Right,
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
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            Math.Max(1, orderRows.Count(order => order.OrderUserId == user.UserId))
        );

        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(
            1,
            result.Column("user_name").Count(name => name == "Eve")
        );
    }

    [Fact]
    public void FullJoinKeepsUnmatchedRowsFromBothSides()
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
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Full,
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
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every order matches exactly one user (matched merged rows = order
        // count), plus the one user with no orders appears once on the right.
        int expectedCount =
            orderRows.Count
            + userRows.Count(user =>
                !orderRows.Any(order => order.OrderUserId == user.UserId)
            );

        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(
            1,
            result.Column("user_name").Count(name => name == "Eve")
        );
    }
}
