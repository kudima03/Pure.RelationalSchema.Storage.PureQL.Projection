using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Temporal aggregates: min / max / avg for date, datetime and time. The
// translator does not yet evaluate aggregate projections, so these spec-correct
// tests are disabled and document the intended SQL behaviour. (avg over temporal
// values additionally needs a rounding rule - see Semantics/README.md.)
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "Aggregate")]
[Trait("Feature", "TemporalAggregate")]
[Trait("Status", "KnownGap")]
public sealed class TemporalAggregateTests
{
    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void MaxPlacedOnPerUserProjectsGroupLatestDate()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MaxDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
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
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        DateOnly[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
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

    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void MinPlacedAtPerUserProjectsGroupEarliestInstant()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateTimeReturning(
                            new DateTimeAggregate(
                                new MinDateTime(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedAt
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
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        DateTime[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
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

    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void MaxShiftStartOverAllUsersProjectsSingleLatestTime()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new TimeReturning(
                            new TimeAggregate(
                                new MaxTime(
                                    new TimeArrayReturning(
                                        new TimeField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.ShiftStart
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
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            db.UserRows.Max(user => user.ShiftStart),
            result.Row(0).Time("max_shift_start")
        );
    }
}
