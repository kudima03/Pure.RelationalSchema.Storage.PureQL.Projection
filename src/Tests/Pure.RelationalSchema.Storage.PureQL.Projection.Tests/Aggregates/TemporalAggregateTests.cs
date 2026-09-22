using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Temporal aggregates: min / max / avg for date, datetime and time.
// Min/max fold each group (avg over temporal values stays unsupported: it
// needs a rounding rule - see Semantics/README.md).
[Trait("Clause", "Aggregate")]
[Trait("Feature", "TemporalAggregate")]
public sealed class TemporalAggregateTests
{
    [Fact]
    public void MaxPlacedOnPerUserProjectsGroupLatestDate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MaxDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_on"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_placed_on"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
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

        DateOnly[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.PlacedOn))
                .OrderBy(value => value),
        ];

        DateOnly[] actual =
        [
            .. result.Rows.Select(row => row.Date("max_placed_on")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinPlacedAtPerUserProjectsGroupEarliestInstant()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateTimeReturning(
                            new DateTimeAggregate(
                                new MinDateTime(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            "schema_with_foreign_keys.orders",
                                            "placed_at"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "min_placed_at"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
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

        DateTime[] expected =
        [
            .. orderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Min(order => order.PlacedAt))
                .OrderBy(value => value),
        ];

        DateTime[] actual =
        [
            .. result.Rows.Select(row => row.DateTime("min_placed_at")!.Value)
                .OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MaxShiftStartOverAllUsersProjectsSingleLatestTime()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new TimeReturning(
                            new TimeAggregate(
                                new MaxTime(
                                    new TimeArrayReturning(
                                        new TimeField(
                                            "schema_with_foreign_keys.users",
                                            "shift_start"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_shift_start"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            userRows.Max(user => user.ShiftStart),
            result.Row(0).Time("max_shift_start")
        );
    }
}
