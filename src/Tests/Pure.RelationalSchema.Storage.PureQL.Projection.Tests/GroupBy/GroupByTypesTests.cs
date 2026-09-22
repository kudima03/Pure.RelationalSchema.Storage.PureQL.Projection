using Pure.Primitives.String;
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
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
                                    new DotString(),
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
            [new Field(new NumberField(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue, new UserAgeColumn().Name.TextValue))],
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
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value)
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
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                new JoinedString(
                                    new DotString(),
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
            ],
            where: null,
            join: null,
            [new Field(new DateField(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue, new SignupDateColumn().Name.TextValue))],
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
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                new JoinedString(
                                    new DotString(),
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
            ],
            where: null,
            join: null,
            [new Field(new DateTimeField(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue, new LastLoginColumn().Name.TextValue))],
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
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                new JoinedString(
                                    new DotString(),
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
            ],
            where: null,
            join: null,
            [new Field(new TimeField(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue, new ShiftStartColumn().Name.TextValue))],
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
