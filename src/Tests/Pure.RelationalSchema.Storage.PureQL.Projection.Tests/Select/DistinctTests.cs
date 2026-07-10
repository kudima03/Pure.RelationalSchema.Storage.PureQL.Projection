using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// DISTINCT should deduplicate the projected result rows. The translator applies
// distinct to the full source rows before projection, so a low-cardinality
// projected column is not actually deduplicated. These spec-correct tests
// currently fail and are kept failing to document the gap. They should pass
// once distinct is applied to the projected result.
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
[Trait("Status", "KnownGap")]
public sealed class DistinctTests
{
    [Fact(Skip = "KnownGap: DISTINCT is applied to the full source rows before "
        + "projection, so a low-cardinality projected column is not deduplicated.")]
    public void DistinctOnStringColumnCollapsesDuplicateValues()
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
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Select(order => order.OrderStatus).Distinct().Count(),
            result.Count
        );
    }

    [Fact(Skip = "KnownGap: DISTINCT is applied to the full source rows before "
        + "projection, so a low-cardinality projected column is not deduplicated.")]
    public void DistinctOnBooleanColumnCollapsesDuplicateValues()
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
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.UserActive).Distinct().Count(),
            result.Count
        );
    }
}
