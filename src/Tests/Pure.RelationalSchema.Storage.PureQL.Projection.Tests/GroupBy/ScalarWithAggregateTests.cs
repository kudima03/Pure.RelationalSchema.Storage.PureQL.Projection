using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Scalar select expressions are legal alongside aggregates in group mode
// (SELECT 'all' AS scope, COUNT(id) FROM t): the constant repeats on every
// group's output row, for whole-set groups, per-key groups and groups
// surviving HAVING alike.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "ScalarWithAggregate")]
public sealed class ScalarWithAggregateTests
{
    [Fact]
    public void ScalarAlongsideWholeSetCountProjectsSingleRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("all"))
                    ),
                    "scope"
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
                    "order_count"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal("all", result.Row(0)["scope"]);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("order_count"));
    }

    [Fact]
    public void ScalarRepeatsOnEveryGroupRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(1))
                    ),
                    "version"
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
                    "status_total"
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

        Dictionary<string, double> expectedTotals = db.OrderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(order => order.OrderTotal)
            );

        Assert.Equal(expectedTotals.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(1, row.Double("version")));
        Assert.Equal(
            expectedTotals.Values.OrderBy(total => total),
            result.Rows
                .Select(row => row.Double("status_total") ?? double.NaN)
                .OrderBy(total => total)
        );
    }

    [Fact]
    public void ScalarGroupKeyFieldAndAggregateMixInOneQuery()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("2024-06"))
                    ),
                    "period"
                ),
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
                    "status_total"
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

        Dictionary<string, double> expectedTotals = db.OrderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(order => order.OrderTotal)
            );

        Assert.Equal(expectedTotals.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Assert.Equal("2024-06", row["period"]);
                string status = row[SampleDatabase.Orders.Status] ?? string.Empty;
                Assert.Equal(
                    expectedTotals[status],
                    row.Double("status_total")
                );
            }
        );
    }

    [Fact]
    public void BooleanAndUuidScalarsProjectInGroupMode()
    {
        SampleDatabase db = new SampleDatabase();

        Guid marker = new Guid("9b2b1f6e-3c86-4c50-8f6a-2f6d1a8f2c11");

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new BooleanReturning(new BooleanScalar(true))
                    ),
                    "flag"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new UuidReturning(new UuidScalar(marker))
                    ),
                    "marker"
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
                    "order_count"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(true, result.Row(0).Bool("flag"));
        Assert.Equal(marker, result.Row(0).Uuid("marker"));
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("order_count"));
    }

    [Fact]
    public void ScalarProjectsOnGroupsSurvivingHaving()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("repeat-buyer"))
                    ),
                    "tag"
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
                    "order_count"
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
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
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
                        ),
                        new NumberReturning(new NumberScalar(1))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedGroups = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() > 1);

        Assert.Equal(expectedGroups, result.Count);
        Assert.All(result.Rows, row => Assert.Equal("repeat-buyer", row["tag"]));
    }
}
