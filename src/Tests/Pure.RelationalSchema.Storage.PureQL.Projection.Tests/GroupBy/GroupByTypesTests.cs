using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY on the remaining key types (number, date, datetime, time),
// complementing GroupByTests (string / bool / uuid / composite). Each projects
// its grouping key and yields one row per distinct value.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByTypes")]
public sealed class GroupByTypesTests
{
    [Fact]
    public void GroupByNumberKeyYieldsOneRowPerDistinctValue()
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
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [new Field(new NumberField("schema_with_foreign_keys.users", "user_age"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge).Distinct().OrderBy(v => v),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("user_age")!.Value)
                .OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByDateKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
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
            ],
            where: null,
            join: null,
            [new Field(new DateField("schema_with_foreign_keys.users", "signup_date"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.SignupDate).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByDateTimeKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
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
            ],
            where: null,
            join: null,
            [new Field(new DateTimeField("schema_with_foreign_keys.users", "last_login"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.LastLogin).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByTimeKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
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
            ],
            where: null,
            join: null,
            [new Field(new TimeField("schema_with_foreign_keys.users", "shift_start"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.ShiftStart).Distinct().Count(),
            result.Count
        );
    }
}
