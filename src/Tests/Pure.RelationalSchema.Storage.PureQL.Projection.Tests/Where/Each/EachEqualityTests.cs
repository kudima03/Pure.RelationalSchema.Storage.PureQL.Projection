using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row `each equal` filtering, one case per PureQL value type. The each*
// family is the spec mechanism for referencing a field in a row predicate
// (scalar equality operands are scalar-only), so these exercise the real
// column-filtering path. Every query is constructed explicitly and inline.
[Trait("Clause", "Where")]
[Trait("Feature", "EachEquality")]
public sealed class EachEqualityTests
{
    [Fact]
    public void EachStringEqualityKeepsOnlyRowsWhoseFieldEqualsTheScalar()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        ),
                        new StringReturning(new StringScalar("shipped"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderStatus == "shipped"),
            result.Count
        );
        Assert.All(
            result.Column("order_status"),
            status => Assert.Equal("shipped", status)
        );
    }

    [Fact]
    public void EachStringEqualityWithNoMatchesReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        ),
                        new StringReturning(new StringScalar("no-such-status"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void EachNumberEqualityFiltersByDoubleField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        ),
                        new NumberReturning(new NumberScalar(30))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Where(user => user.UserAge == 30)
                .Select(user => user.UserName)
                .OrderBy(name => name)
                .ToArray(),
            result.Column("user_name").OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void EachBooleanEqualityFiltersByBoolField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.users",
                                "user_active"
                            )
                        ),
                        new BooleanReturning(new BooleanScalar(true))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.UserActive), result.Count);
    }

    [Fact]
    public void EachDateEqualityFiltersByDateField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateOnly target = new DateOnly(2020, 1, 15);

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.users",
                                "signup_date"
                            )
                        ),
                        new DateReturning(new DateScalar(target))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.SignupDate == target), result.Count);
    }

    [Fact]
    public void EachTimeEqualityFiltersByTimeField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly target = new TimeOnly(9, 0, 0);

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachTimeEquality(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        ),
                        new TimeReturning(new TimeScalar(target))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.ShiftStart == target), result.Count);
    }

    [Fact]
    public void EachDateTimeEqualityFiltersByDateTimeField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime target = new DateTime(2024, 6, 1, 8, 30, 0);

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateTimeEquality(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.users",
                                "last_login"
                            )
                        ),
                        new DateTimeReturning(new DateTimeScalar(target))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.LastLogin == target), result.Count);
    }

    [Fact]
    public void EachUuidEqualityFiltersByUuidField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Guid target = userRows.Single(user => user.UserName == "Bob").UserId;

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        ),
                        new UuidReturning(new UuidScalar(target))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal("Bob", result.Row(0)["user_name"]);
    }
}
