using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// SELECT DISTINCT deduplicates the projected rows after a join fans the
// base rows out, so duplicates introduced by the join collapse back to the
// distinct projected value set.
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctOverJoinTests
{
    private static Join UsersToOrdersInnerJoin()
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

    private static Query DistinctColumnThroughJoin(SelectExpression select)
    {
        return new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [select],
            where: null,
            [UsersToOrdersInnerJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );
    }

    [Fact]
    public void DistinctCollapsesJoinFanOutDuplicates()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = DistinctColumnThroughJoin(
            new SelectExpression(
                new ArrayReturning(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Name
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows
                .Where(user =>
                    db.OrderRows.Any(order => order.OrderUserId == user.UserId)
                )
                .Select(user => user.UserName)
                .OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void DistinctOnJoinedColumnCollapsesToItsDistinctValues()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = DistinctColumnThroughJoin(
            new SelectExpression(
                new ArrayReturning(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status),
        ];

        Assert.Equal(
            expected,
            result
                .Column(SampleDatabase.Orders.Status)
                .OrderBy(status => status)
                .ToArray()
        );
    }
}
