using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
            "audit.logins",
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                "audit.logins",
                                "login_user_id"
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
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
                                            "audit.logins",
                                            "login_at"
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
                                            "audit.logins",
                                            "login_id"
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
                        "schema_with_foreign_keys.users",
                        "user_id"
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (DateTime, double)> expected = loginRows
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
            row => row.Uuid("user_id")!.Value,
            row =>
                (
                    row.DateTime("lastLoginAt")!.Value,
                    row.Double("loginCount")!.Value
                )
        );

        Assert.Equal(expected, actual);
    }
}
