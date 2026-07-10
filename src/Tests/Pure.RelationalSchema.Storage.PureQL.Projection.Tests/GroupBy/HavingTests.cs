using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING filters groups by an aggregate over each group's rows. The translator
// does not yet support aggregates in HAVING (it raises NotSupportedException),
// so these spec-correct tests currently fail. They are kept failing on purpose
// to document the gap - do not weaken them; they should pass once aggregate
// evaluation is implemented.
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
[Trait("Status", "KnownGap")]
public sealed class HavingTests
{
    [Fact(Skip = "KnownGap: aggregates in HAVING raise NotSupportedException. "
        + "Enable once aggregate evaluation over groups is implemented.")]
    public void HavingCountGreaterThanKeepsOnlyGroupsAboveThreshold()
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

        int expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() > 1);

        Assert.Equal(expected, result.Count);
    }
}
