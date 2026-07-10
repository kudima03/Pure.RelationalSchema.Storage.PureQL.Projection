using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A chain of two joins: orders -> order_items -> products. Columns from all
// three tables are available on the merged rows (names are globally unique).
[Trait("Clause", "Join")]
[Trait("Feature", "MultiJoin")]
public sealed class MultiJoinTests
{
    [Fact]
    public void ChainedInnerJoinsEnrichEachItemWithOrderAndProduct()
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
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Name
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.OrderItems.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.OrderItems.Entity,
                                        SampleDatabase.OrderItems.OrderId
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.Id
                                    )
                                )
                            )
                        )
                    )
                ),
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Products.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.OrderItems.Entity,
                                        SampleDatabase.OrderItems.ProductId
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Products.Entity,
                                        SampleDatabase.Products.Id
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
            new PureQLProjection(db.Datasets, query)
        );

        (string?, string?)[] expected =
        [
            .. (
                from item in db.OrderItemRows
                join order in db.OrderRows on item.ItemOrderId equals order.OrderId
                join product in db.ProductRows
                    on item.ItemProductId equals product.ProductId
                select ((string?)order.OrderStatus, (string?)product.ProductName)
            ).OrderBy(pair => pair.Item1).ThenBy(pair => pair.Item2),
        ];

        (string?, string?)[] actual =
        [
            .. result
                .Rows.Select(row =>
                    (
                        row[SampleDatabase.Orders.Status],
                        row[SampleDatabase.Products.Name]
                    )
                )
                .OrderBy(pair => pair.Item1)
                .ThenBy(pair => pair.Item2),
        ];

        Assert.Equal(expected, actual);
    }
}
