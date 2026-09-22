using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// SELECT DISTINCT deduplicates the projected rows after a join fans the
// base rows out, so duplicates introduced by the join collapse back to the
// distinct projected value set.
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctOverJoinTests
{
    private static Join UsersToOrdersInnerJoin()
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

    private static Query DistinctColumnThroughJoin(SelectExpression select)
    {
        return new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [select],
            where: null,
            [UsersToOrdersInnerJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );
    }

    [Fact]
    public void DistinctCollapsesJoinFanOutDuplicates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = DistinctColumnThroughJoin(
            new SelectExpression(
                new ArrayReturning(
                    new StringArrayReturning(
                        new StringField(
                            "schema_with_foreign_keys.users",
                            "user_name"
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user =>
                    orderRows.Any(order => order.OrderUserId == user.UserId)
                )
                .Select(user => user.UserName)
                .OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column("user_name").OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void DistinctOnJoinedColumnCollapsesToItsDistinctValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = DistinctColumnThroughJoin(
            new SelectExpression(
                new ArrayReturning(
                    new StringArrayReturning(
                        new StringField(
                            "schema_with_foreign_keys.orders",
                            "order_status"
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status),
        ];

        Assert.Equal(
            expected,
            result
                .Column("order_status")
                .OrderBy(status => status)
                .ToArray()
        );
    }
}
