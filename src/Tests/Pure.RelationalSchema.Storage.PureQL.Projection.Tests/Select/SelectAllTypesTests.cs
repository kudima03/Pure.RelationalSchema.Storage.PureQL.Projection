using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Projecting all seven typed columns of a table in a single query: every column
// is present and each cell round-trips to its ground-truth typed value.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAllTypes")]
public sealed class SelectAllTypesTests
{
    [Fact]
    public void SelectAllUserColumnsProjectsEveryTypedColumn()
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
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        )
                    )
                ),
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
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                "schema_with_foreign_keys.users",
                                "user_active"
                            )
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

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.Contains("user_id", result.ColumnNames);
        Assert.Contains("user_name", result.ColumnNames);
        Assert.Contains("user_age", result.ColumnNames);
        Assert.Contains("user_active", result.ColumnNames);
        Assert.Contains("signup_date", result.ColumnNames);
        Assert.Contains("last_login", result.ColumnNames);
        Assert.Contains("shift_start", result.ColumnNames);

        UserRecord first = userRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.UserId, row.Uuid("user_id"));
        Assert.Equal(first.UserName, row["user_name"]);
        Assert.Equal(first.UserAge, row.Double("user_age"));
        Assert.Equal(first.UserActive, row.Bool("user_active"));
        Assert.Equal(first.SignupDate, row.Date("signup_date"));
        Assert.Equal(first.LastLogin, row.DateTime("last_login"));
        Assert.Equal(first.ShiftStart, row.Time("shift_start"));
    }
}
