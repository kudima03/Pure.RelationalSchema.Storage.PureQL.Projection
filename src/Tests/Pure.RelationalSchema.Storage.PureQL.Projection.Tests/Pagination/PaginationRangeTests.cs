using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// Pagination carries skip/take as Int64, but they are applied through an
// unchecked cast to Int32, so values beyond int.MaxValue wrap and silently
// produce the wrong window (issue #85).
[Trait("Clause", "Pagination")]
[Trait("Feature", "Range")]
public sealed class PaginationRangeTests
{
    private static Query AllUserNames(ModelPagination pagination)
    {
        return new Query(
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
            orderBy: null,
            pagination
        );
    }

    [Fact(
        Skip = "Issue #85: take = long.MaxValue wraps to -1 through the "
            + "unchecked int cast, so a query asking for everything returns "
            + "an empty result."
    )]
    [Trait("Status", "KnownGap")]
    public void TakeBeyondIntMaxReturnsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                AllUserNames(new ModelPagination(0, long.MaxValue))
            )
        );

        Assert.Equal(db.UserRows.Count, result.Count);
    }

    [Fact(
        Skip = "Issue #85: skip = long.MaxValue wraps to -1 through the "
            + "unchecked int cast, making the skip a no-op, so rows that "
            + "should all be skipped are returned."
    )]
    [Trait("Status", "KnownGap")]
    public void SkipBeyondIntMaxReturnsNoRows()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                AllUserNames(new ModelPagination(long.MaxValue, 1))
            )
        );

        Assert.Equal(0, result.Count);
    }
}
