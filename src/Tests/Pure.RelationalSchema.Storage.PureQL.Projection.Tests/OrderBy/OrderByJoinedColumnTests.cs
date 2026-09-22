using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// ORDER BY over a joined (entity-qualified) column: the primary key comes
// from the joined table, the secondary key from the base table, so ordering
// must resolve both sides of the merged row.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "JoinedColumn")]
public sealed class OrderByJoinedColumnTests
{
    [Fact]
    public void OrderByJoinedNameThenBaseTotalDescOrdersMergedRows()
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
                        new StringField(
                            "schema_with_foreign_keys.users",
                            "user_name"
                        )
                    ),
                    SortDirection.Asc
                ),
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
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string, double)[] expected =
        [
            .. orderRows
                .Select(order =>
                    (
                        Name: userRows.Single(user =>
                            user.UserId == order.OrderUserId
                        ).UserName,
                        Total: order.OrderTotal
                    )
                )
                .OrderBy(pair => pair.Name, StringComparer.Ordinal)
                .ThenByDescending(pair => pair.Total),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row["user_name"]!,
                    row.Double("order_total")!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
