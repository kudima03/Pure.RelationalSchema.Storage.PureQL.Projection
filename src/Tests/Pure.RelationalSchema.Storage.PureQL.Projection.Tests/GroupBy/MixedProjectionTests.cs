using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// A group-mode select list may mix the grouping key with one or more
// aggregates in a single row: each output row pairs the group's key value
// with its folded aggregate value(s), not just the aggregate alone.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "MixedProjection")]
public sealed class MixedProjectionTests
{
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

    [Fact]
    public void GroupKeyAndSumProjectTogetherPerGroup()
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
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "userTotal"
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
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("userTotal")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupKeyAndCountProjectTogetherPerGroup()
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
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "statusCount"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new StringField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
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

        Dictionary<string, double> expected = db.OrderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[SampleDatabase.Orders.Status]!,
            row => row.Double("statusCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MultipleAggregatesOfDifferentTypesProjectInOneRow()
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
                    "orderCount"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "totalSum"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MinDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "earliestPlacedOn"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MaxString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxStatus"
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

        Dictionary<Guid, (double Count, double Sum, DateOnly Min, string Max)> expected =
            db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .ToDictionary(
                    group => group.Key,
                    group => (
                        Count: (double)group.Count(),
                        Sum: group.Sum(order => order.OrderTotal),
                        Min: group.Min(order => order.PlacedOn),
                        Max: group.Select(order => order.OrderStatus)
                            .Max(StringComparer.Ordinal)!
                    )
                );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid(SampleDatabase.Orders.UserId)!.Value;
                (double Count, double Sum, DateOnly Min, string Max) expectedGroup =
                    expected[userId];
                Assert.Equal(expectedGroup.Count, row.Double("orderCount"));
                Assert.Equal(expectedGroup.Sum, row.Double("totalSum"));
                Assert.Equal(expectedGroup.Min, row.Date("earliestPlacedOn"));
                Assert.Equal(expectedGroup.Max, row["maxStatus"]);
            }
        );
    }

    [Fact]
    public void TwoNumericAggregatesOverDifferentColumnsProjectIndependently()
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
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "orderTotalSum"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new AverageNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Age
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "avgAge"
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

        Dictionary<Guid, (double Sum, double Avg)> expected = db.OrderRows
            .Join(
                db.UserRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserId, order.OrderTotal, user.UserAge)
            )
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (
                    Sum: group.Sum(row => row.OrderTotal),
                    Avg: group.Average(row => row.UserAge)
                )
            );

        Assert.Equal(expected.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Guid userId = row.Uuid(SampleDatabase.Users.Id)!.Value;
                (double Sum, double Avg) expectedGroup = expected[userId];
                Assert.Equal(expectedGroup.Sum, row.Double("orderTotalSum"));
                Assert.Equal(expectedGroup.Avg, row.Double("avgAge"));
            }
        );
    }

    [Fact]
    public void AggregateColumnsFollowAliasesInMixedProjection()
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
                    ),
                    "buyer"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "purchases"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "spend"
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

        Assert.Equal(["buyer", "purchases", "spend"], result.ColumnNames);
    }
}
