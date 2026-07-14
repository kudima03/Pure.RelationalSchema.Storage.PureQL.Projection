using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Aggregates folding a column of a table joined in from another schema:
// per-user login statistics from audit.logins joined onto shop.users, with
// a temporal max and a count over the joined side.
[Trait("Clause", "Select")]
[Trait("Feature", "CrossSchemaAggregate")]
public sealed class CrossSchemaJoinAggregateTests
{
    private static Join UsersToLoginsJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Logins.Entity,
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
                                SampleDatabase.Logins.Entity,
                                SampleDatabase.Logins.UserId
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void PerUserMaxAndCountOverCrossSchemaLoginsFoldTheJoinedRows()
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
                        new DateTimeReturning(
                            new DateTimeAggregate(
                                new MaxDateTime(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.At
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "lastLoginAt"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "loginCount"
                ),
            ],
            where: null,
            [UsersToLoginsJoin()],
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

        Dictionary<Guid, (DateTime, double)> expected = db.LoginRows
            .GroupBy(login => login.LoginUserId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (
                        group.Max(login => login.LoginAt),
                        (double)group.Count()
                    )
            );

        Dictionary<Guid, (DateTime, double)> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row =>
                (
                    row.DateTime("lastLoginAt")!.Value,
                    row.Double("loginCount")!.Value
                )
        );

        Assert.Equal(expected, actual);
    }
}
