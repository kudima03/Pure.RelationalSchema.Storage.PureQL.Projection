using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
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
            new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
            ).TextValue,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserIdColumn().Name.TextValue
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderUserIdColumn().Name.TextValue
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
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserNameColumn().Name.TextValue
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
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
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
                .Column(new OrderStatusColumn().Name.TextValue)
                .OrderBy(status => status)
                .ToArray()
        );
    }
}
