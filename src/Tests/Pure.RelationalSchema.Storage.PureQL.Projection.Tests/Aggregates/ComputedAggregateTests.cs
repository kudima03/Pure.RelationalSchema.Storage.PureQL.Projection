using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// An aggregate's argument may be any array-returning expression, including
// per-row (each*) computed values: sum/average/count fold the computed
// per-row results, and min/max fold per-row temporal diffs, not stored
// columns directly.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "ComputedAggregate")]
public sealed class ComputedAggregateTests
{
    private static Join ItemsToProductsJoin()
    {
        return new Join(
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
        );
    }

    private static Join UsersToOrdersJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Orders.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning QtyTimesPrice()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachMultiply(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.Qty
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Price
                            )
                        ),
                    ]
                )
            )
        );
    }

    private static double PriceOf(SampleDatabase db, Guid productId)
    {
        return db.ProductRows
            .Single(product => product.ProductId == productId)
            .ProductPrice;
    }

    [Fact]
    public void SumOfEachMultiplyProjectsPerGroupRevenue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(new SumNumber(QtyTimesPrice()))
                        )
                    ),
                    "revenue"
                ),
            ],
            where: null,
            [ItemsToProductsJoin()],
            [
                new Field(
                    new UuidField(
                        SampleDatabase.OrderItems.Entity,
                        SampleDatabase.OrderItems.OrderId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.ItemQty * PriceOf(db, item.ItemProductId))
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.OrderItems.OrderId)!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachMultiplyProjectsPerGroupMean()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(new AverageNumber(QtyTimesPrice()))
                        )
                    ),
                    "meanLineValue"
                ),
            ],
            where: null,
            [ItemsToProductsJoin()],
            [
                new Field(
                    new UuidField(
                        SampleDatabase.OrderItems.Entity,
                        SampleDatabase.OrderItems.OrderId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderItemRows
            .GroupBy(item => item.ItemOrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(item =>
                    item.ItemQty * PriceOf(db, item.ItemProductId)
                )
            );

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.OrderItems.OrderId)!.Value,
            row => row.Double("meanLineValue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxOfEachDateDiffDaysProjectsPerGroupSpan()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MaxNumber(
                                    new NumberArrayReturning(
                                        new EachDateDiffDays(
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.PlacedOn
                                                )
                                            ),
                                            new DateArrayReturning(
                                                new DateField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.SignupDate
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxSpanDays"
                ),
            ],
            where: null,
            [UsersToOrdersJoin()],
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Id
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderRows
            .Join(
                db.UserRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, Span: (double)(order.PlacedOn.DayNumber - user.SignupDate.DayNumber))
            )
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Span));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row => row.Double("maxSpanDays")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinOfEachTimeDiffSecondsProjectsPerGroupValue()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly origin = new TimeOnly(8, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new MinNumber(
                                    new NumberArrayReturning(
                                        new EachTimeDiffSeconds(
                                            new TimeArrayReturning(
                                                new TimeField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.ShiftStart
                                                )
                                            ),
                                            new TimeReturning(new TimeScalar(origin))
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "minShiftGapSeconds"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Active
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<bool, double> expected = db.UserRows
            .GroupBy(user => user.UserActive)
            .ToDictionary(
                group => group.Key,
                group => group.Min(user => (user.ShiftStart - origin).TotalSeconds)
            );

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(SampleDatabase.Users.Active)!.Value,
            row => row.Double("minShiftGapSeconds")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfEachArithmeticCountsGroupRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(1)
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "rowCount"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("rowCount")!.Value
        );

        Assert.Equal(expected, actual);
    }
}
