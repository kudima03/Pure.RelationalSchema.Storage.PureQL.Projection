using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachDateTimeArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row temporal arithmetic: date add-days / diff-days (unit: days) and
// time / datetime add-seconds / diff-seconds (unit: seconds). Each result is
// compared or equated to form a row predicate.
[Trait("Clause", "Where")]
[Trait("Feature", "EachDateTimeMath")]
public sealed class EachDateTimeMathTests
{
    [Fact]
    public void EachDateAddDaysShiftsDateBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly expectedAfterShift = new DateOnly(2024, 6, 2);

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
                    new EachDateEquality(
                        new DateArrayReturning(
                            new EachDateAddDays(
                                new DateArrayReturning(
                                    new DateField(
                                        "schema_with_foreign_keys.orders",
                                        "placed_on"
                                    )
                                ),
                                new NumberReturning(new NumberScalar(1))
                            )
                        ),
                        new DateReturning(new DateScalar(expectedAfterShift))
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
            orderRows.Count(order => order.PlacedOn.AddDays(1) == expectedAfterShift),
            result.Count
        );
    }

    [Fact]
    public void EachDateDiffDaysComputesDayGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly origin = new DateOnly(2024, 6, 1);

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
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachDateDiffDays(
                                new DateArrayReturning(
                                    new DateField(
                                        "schema_with_foreign_keys.orders",
                                        "placed_on"
                                    )
                                ),
                                new DateReturning(new DateScalar(origin))
                            )
                        ),
                        new NumberReturning(new NumberScalar(2))
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
            orderRows.Count(order =>
                order.PlacedOn.DayNumber - origin.DayNumber > 2
            ),
            result.Count
        );
    }

    [Fact]
    public void EachTimeAddSecondsShiftsTimeBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly expectedAfterShift = new TimeOnly(10, 0, 0);

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
                            new EachTimeAddSeconds(
                                new TimeArrayReturning(
                                    new TimeField(
                                        "schema_with_foreign_keys.users",
                                        "shift_start"
                                    )
                                ),
                                new NumberReturning(new NumberScalar(3600))
                            )
                        ),
                        new TimeReturning(new TimeScalar(expectedAfterShift))
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
            userRows.Count(user =>
                user.ShiftStart.Add(TimeSpan.FromSeconds(3600)) == expectedAfterShift
            ),
            result.Count
        );
    }

    [Fact]
    public void EachTimeDiffSecondsComputesSecondGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly origin = new TimeOnly(8, 0, 0);

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
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachTimeDiffSeconds(
                                new TimeArrayReturning(
                                    new TimeField(
                                        "schema_with_foreign_keys.users",
                                        "shift_start"
                                    )
                                ),
                                new TimeReturning(new TimeScalar(origin))
                            )
                        ),
                        new NumberReturning(new NumberScalar(3600))
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
            userRows.Count(user =>
                (user.ShiftStart - origin).TotalSeconds > 3600
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeAddSecondsShiftsInstantBeforeEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime expectedAfterShift = new DateTime(2024, 6, 1, 9, 30, 0);

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
                            new EachDateTimeAddSeconds(
                                new DateTimeArrayReturning(
                                    new DateTimeField(
                                        "schema_with_foreign_keys.users",
                                        "last_login"
                                    )
                                ),
                                new NumberReturning(new NumberScalar(3600))
                            )
                        ),
                        new DateTimeReturning(new DateTimeScalar(expectedAfterShift))
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
            userRows.Count(user =>
                user.LastLogin.AddSeconds(3600) == expectedAfterShift
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeDiffSecondsComputesSecondGapBeforeComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime origin = new DateTime(2024, 6, 2, 0, 0, 0);

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
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachDateTimeDiffSeconds(
                                new DateTimeArrayReturning(
                                    new DateTimeField(
                                        "schema_with_foreign_keys.users",
                                        "last_login"
                                    )
                                ),
                                new DateTimeReturning(new DateTimeScalar(origin))
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
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
            userRows.Count(user =>
                (user.LastLogin - origin).TotalSeconds > 0
            ),
            result.Count
        );
    }
}
