using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Aggregate projections (sum/count/... over each group). The translator does
// not yet project aggregate select expressions (it raises
// NotSupportedException), so these spec-correct tests currently fail and are
// kept failing to document the gap. They should pass once aggregate projection
// is implemented.
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Aggregate")]
[Trait("Status", "KnownGap")]
public sealed class AggregateTests
{
    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void SumAggregateProjectsPerGroupTotal()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
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
                    "group_total"
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

        int expectedGroups = db.OrderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }

    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void CountAggregateProjectsPerGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
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
                    "group_count"
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

        int expectedGroups = db.OrderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
