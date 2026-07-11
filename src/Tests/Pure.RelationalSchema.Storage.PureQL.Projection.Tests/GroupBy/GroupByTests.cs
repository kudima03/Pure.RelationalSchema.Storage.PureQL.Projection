using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY collapses rows sharing a key to one row per distinct key. These
// tests project the grouping key itself (aggregates over grouped rows are a
// separate, currently-unsupported feature - see AggregateTests). Group order is
// not asserted; the distinct key set is.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupBy")]
public sealed class GroupByTests
{
    [Fact]
    public void GroupByStringKeyYieldsOneRowPerDistinctValue()
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
            ],
            where: null,
            join: null,
            [new Field(new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows.Select(order => order.OrderStatus).Distinct().OrderBy(s => s),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Orders.Status).OrderBy(s => s).ToArray()
        );
    }

    [Fact]
    public void GroupByBooleanKeyYieldsOneRowPerDistinctValue()
    {
        SampleDatabase db = new SampleDatabase();

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
            ],
            where: null,
            join: null,
            [new Field(new BooleanField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.UserActive).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByUuidKeyYieldsOneRowPerDistinctValue()
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
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Select(order => order.OrderUserId).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByCompositeKeyYieldsOneRowPerDistinctCombination()
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
            ],
            where: null,
            join: null,
            [
                new Field(new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)),
                new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Select(order => (order.OrderStatus, order.OrderUserId))
                .Distinct()
                .Count(),
            result.Count
        );
    }
}
