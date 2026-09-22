using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Permanent guard for MIGRATION-PLAN.md's evidence table: pins the
// Pure.RelationalSchema.Storage.Samples package's cell-text contract, which is
// the one thing a future package bump could silently break for this
// translator (InvariantCellText's date/datetime/time/NULL rendering must stay
// TryParse-with-InvariantCulture-round-trippable; SchemaDataSetWithForeignKeys
// + AuditSchemaDataSet must keep enough row variety for real joins/groups).
[Trait("Feature", "PackageFixtureContract")]
public sealed class PackageFixtureRoundTripTests
{
    [Fact]
    public void TemporalColumnsRoundTripThroughStoredCellText()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("schema_with_foreign_keys.users", "user_id")
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.users",
                                "signup_date"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.users",
                                "last_login"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        Assert.Equal(userRows.Count, result.Count);

        foreach (ResultRow row in result.Rows)
        {
            Guid userId = row.Uuid("user_id")!.Value;
            UserRecord expected = userRows.Single(user => user.UserId == userId);

            Assert.Equal(expected.SignupDate, row.Date("signup_date"));
            Assert.Equal(expected.LastLogin, row.DateTime("last_login"));
            Assert.Equal(expected.ShiftStart, row.Time("shift_start"));
        }
    }

    [Fact]
    public void NullableColumnSurvivesAsNullForExactlyTheRecordsWithoutAValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("schema_with_foreign_keys.users", "user_id")
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_score"
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        Guid[] expectedNullIds =
        [
            .. userRows.Where(user => user.UserScore is null).Select(user => user.UserId),
        ];
        Guid[] actualNullIds =
        [
            .. result.Rows
                .Where(row => row.Double("user_score") is null)
                .Select(row => row.Uuid("user_id")!.Value),
        ];

        Assert.Equal(2, expectedNullIds.Length);
        Assert.Equal(expectedNullIds.OrderBy(id => id), actualNullIds.OrderBy(id => id));
    }

    [Fact]
    public void GroupByUserAgeYieldsTheRealDistinctAgeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField("schema_with_foreign_keys.users", "user_age")
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NumberField("schema_with_foreign_keys.users", "user_age")
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        double[] expectedAges =
        [
            .. userRows.Select(user => user.UserAge).Distinct().OrderBy(age => age),
        ];

        Assert.True(expectedAges.Length < userRows.Count);
        Assert.Equal(expectedAges.Length, result.Count);
        Assert.Equal(
            expectedAges,
            result.Column("user_age").Select(age => double.Parse(age!)).OrderBy(age => age)
        );
    }

    [Fact]
    public void CrossSchemaJoinFromUsersToAuditLoginsMatchesGroundTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
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
                                    new UuidField("audit.logins", "login_user_id")
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        string[] expected =
        [
            .. (
                from user in userRows
                join login in loginRows on user.UserId equals login.LoginUserId
                select user.UserName
            ).OrderBy(name => name),
        ];

        Assert.Equal(4, loginRows.Count);
        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, result.Column("user_name").OrderBy(name => name).ToArray());
    }
}
