using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// orderBy field resolution intentionally differs by query mode
// (RowsFromDatasets.Build): without groupBy it runs pre-projection and
// needs the source column name (OrderByExpansionTests.
// OrderByAliasedSelectColumnStillOrdersByUnderlyingField pins this); with
// groupBy/aggregates it runs post-projection and needs the select alias
// (OrderByExpansionTests.OrderByAggregateResultOrdersEmittedGroupsByItsValue
// pins that). Both rules stay unchanged here (issue #135) - only the
// failure a caller hits by using the wrong name for the current mode is
// pinned as a KeyNotFoundException whose message names the field/entity and
// spells out which name is expected in which mode, instead of the bare
// "Row has no column named 'x'." message that gives no hint why.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByFieldResolutionError")]
public sealed class OrderByFieldResolutionErrorTests
{
    [Fact]
    public void OrderingByAliasWithoutGroupByThrowsWithRuleInMessage()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        )
                    ),
                    "years"
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Users.Entity, "years")
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(db.Datasets, query))
        );

        Assert.Contains("years", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "source column",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "select alias",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void OrderingByOriginalFieldNameInGroupByThrowsWithRuleInMessage()
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
                    ),
                    "status"
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(db.Datasets, query))
        );

        Assert.Contains(
            SampleDatabase.Orders.Total,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "source column",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "select alias",
            exception.Message,
            StringComparison.Ordinal
        );
    }
}
