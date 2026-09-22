using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// Multi-key ORDER BY where the keys sort in opposite directions (asc then desc).
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByMixedDirection")]
public sealed class OrderByMixedDirectionTests
{
    [Fact]
    public void OrderByStatusAscThenTotalDescOrdersWithinEachStatus()
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
            join: null,
            groupBy: null,
            having: null,
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

        (string?, double?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.OrderTotal)
                .Select(order => ((string?)order.OrderStatus, (double?)order.OrderTotal)),
        ];

        (string?, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row["order_status"],
                    row.Double("order_total")
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByActiveAscThenAgeDescOrdersWithinEachFlag()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new BooleanField(
                            "schema_with_foreign_keys.users",
                            "user_active"
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.users",
                            "user_age"
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

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive)
                .ThenByDescending(user => user.UserAge)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column("user_name")];

        Assert.Equal(expected, actual);
    }
}
