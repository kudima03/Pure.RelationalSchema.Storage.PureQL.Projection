using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY combined with ORDER BY on the grouping key: one row per distinct key,
// emitted in the requested order.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByOrderBy")]
public sealed class GroupByOrderByTests
{
    [Fact]
    public void GroupByStatusOrderedByStatusAscYieldsKeysInOrder()
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
            join: null,
            [new Field(new StringField("schema_with_foreign_keys.orders", "order_status"))],
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
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByAgeOrderedByAgeDescYieldsKeysInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [new Field(new NumberField("schema_with_foreign_keys.users", "user_age"))],
            having: null,
            [
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

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge)
                .Distinct()
                .OrderByDescending(v => v),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("user_age")!.Value),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
