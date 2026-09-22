using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using String = Pure.Primitives.String.String;

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
            new FromExpression(
                new JoinedString(
                    new String("."),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserIdColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new SignupDateColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new LastLoginColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new ShiftStartColumn().Name.TextValue
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
            Guid userId = row.Uuid(new UserIdColumn().Name.TextValue)!.Value;
            UserRecord expected = userRows.Single(user => user.UserId == userId);

            Assert.Equal(expected.SignupDate, row.Date(new SignupDateColumn().Name.TextValue));
            Assert.Equal(expected.LastLogin, row.DateTime(new LastLoginColumn().Name.TextValue));
            Assert.Equal(expected.ShiftStart, row.Time(new ShiftStartColumn().Name.TextValue));
        }
    }

    [Fact]
    public void NullableColumnSurvivesAsNullForExactlyTheRecordsWithoutAValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new String("."),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserIdColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserScoreColumn().Name.TextValue
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
                .Where(row => row.Double(new UserScoreColumn().Name.TextValue) is null)
                .Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
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
            new FromExpression(
                new JoinedString(
                    new String("."),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserAgeColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NumberField(
                        new JoinedString(
                            new String("."),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new UsersTable().Name,
                            ]
                        ).TextValue,
                        new UserAgeColumn().Name.TextValue
                    )
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
            result.Column(new UserAgeColumn().Name.TextValue)
                .Select(age => double.Parse(age!))
                .OrderBy(age => age)
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
            new FromExpression(
                new JoinedString(
                    new String("."),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new String("."),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    new JoinedString(
                        new String("."),
                        [new AuditRelationalSchema().Name, new LoginsTable().Name]
                    ).TextValue,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
                                            new String("."),
                                            [
                                                new RelationalSchemaWithForeignKeys().Name,
                                                new UsersTable().Name,
                                            ]
                                        ).TextValue,
                                        new UserIdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
                                            new String("."),
                                            [
                                                new AuditRelationalSchema().Name,
                                                new LoginsTable().Name,
                                            ]
                                        ).TextValue,
                                        new LoginUserIdColumn().Name.TextValue
                                    )
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
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }
}
