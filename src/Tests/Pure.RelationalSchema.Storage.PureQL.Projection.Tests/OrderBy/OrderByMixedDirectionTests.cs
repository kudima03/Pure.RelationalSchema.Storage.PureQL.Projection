using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
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
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
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

        (string?, double?)[] expected =
        [
            .. db.OrderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.OrderTotal)
                .Select(order => ((string?)order.OrderStatus, (double?)order.OrderTotal)),
        ];

        (string?, double?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[SampleDatabase.Orders.Status],
                    row.Double(SampleDatabase.Orders.Total)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByActiveAscThenAgeDescOrdersWithinEachFlag()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
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

        string[] expected =
        [
            .. db.UserRows.OrderBy(user => user.UserActive)
                .ThenByDescending(user => user.UserAge)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name)];

        Assert.Equal(expected, actual);
    }
}
