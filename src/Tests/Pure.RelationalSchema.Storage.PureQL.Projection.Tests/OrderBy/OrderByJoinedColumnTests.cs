using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.UserId
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
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
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Name
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected =
        [
            .. db.OrderRows
                .Select(order =>
                    (
                        Name: db.UserRows.Single(user =>
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
                    row[SampleDatabase.Users.Name]!,
                    row.Double(SampleDatabase.Orders.Total)!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
