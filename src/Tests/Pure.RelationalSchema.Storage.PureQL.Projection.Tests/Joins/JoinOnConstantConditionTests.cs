using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join's on clause may be a single boolean-returning expression; constant
// conditions pin the degenerate join shapes: true yields the full cross
// product, false yields no matches (empty for INNER, fully padded for the
// outer types).
[Trait("Clause", "Join")]
[Trait("Feature", "ConstantCondition")]
public sealed class JoinOnConstantConditionTests
{
    private static Query UsersJoinedToProducts(JoinType joinType, bool condition)
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
            [
                new Join(
                    joinType,
                    SampleDatabase.Products.Entity,
                    new BooleanReturning(new BooleanScalar(condition))
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void InnerJoinOnConstantTrueProducesTheCrossProduct()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                UsersJoinedToProducts(JoinType.Inner, condition: true)
            )
        );

        Assert.Equal(db.UserRows.Count * db.ProductRows.Count, result.Count);
    }

    [Fact]
    public void InnerJoinOnConstantFalseReturnsEmpty()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                UsersJoinedToProducts(JoinType.Inner, condition: false)
            )
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void LeftJoinOnConstantFalsePadsEveryLeftRowOnce()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                UsersJoinedToProducts(JoinType.Left, condition: false)
            )
        );

        Assert.Equal(db.UserRows.Count, result.Count);

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FullJoinOnConstantFalseKeepsEverySideUnmatched()
    {
        SampleDatabase db = new SampleDatabase();

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                UsersJoinedToProducts(JoinType.Full, condition: false)
            )
        );

        Assert.Equal(db.UserRows.Count + db.ProductRows.Count, result.Count);
    }
}
