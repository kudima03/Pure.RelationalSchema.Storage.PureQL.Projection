using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Dates, times and datetimes: a datetime is an instant, read as UTC when
// stored without an offset and written in UTC; timeAddSeconds wraps around
// midnight; the *Diff operators are left - right; averages round to the
// unit of their type, half away from zero.
public sealed class TemporalTests
{
    private static readonly IStoredSchemaDataSet Events = new InlineTable(
        "t",
        "events",
        ("at", new DateTimeColumnType()),
        ("on", new DateColumnType()),
        ("clock", new TimeColumnType())
    ).With(
        ["2024-01-01T03:00:00+03:00", "2024-01-01", "00:30:00"],
        ["2024-01-01T00:00:01", "2024-01-02", "23:59:59"]
    );

    [Fact]
    public void DatetimeStoredWithAnOffsetIsReadAsItsInstantAndWrittenInUtc()
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDatetime(
                                    "at",
                                    new DatetimeProjection(
                                        new FieldDatetime("t.events", "at")
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(
            ["2024-01-01T00:00:00", "2024-01-01T00:00:01"],
            ResultColumn.Texts(projection, "at")
        );
    }

    [Theory]
    [InlineData(-3600, "23:30:00", "22:59:59")]
    [InlineData(0.5, "00:30:00.5", "23:59:59.5")]
    [InlineData(1, "00:30:01", "00:00:00")]
    public void TimeAddSecondsWrapsAroundMidnight(
        double seconds,
        string first,
        string second
    )
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionTime(
                                    "shifted",
                                    new TimeProjection(
                                        new TimeAddSecondsTimeProjection(
                                            new TimeProjection(
                                                new FieldTime("t.events", "clock")
                                            ),
                                            new DecimalProjection(
                                                new LiteralAsDecimal(
                                                    new LiteralDecimal((decimal)seconds)
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal([first, second], ResultColumn.Texts(projection, "shifted"));
    }

    [Fact]
    public void TimeDiffSecondsIsNegativeWhenLeftIsEarlier()
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "seconds",
                                    new DecimalProjection(
                                        new DifferenceDecimalProjection(
                                            new TimeDiffSecondsDecimalProjection(
                                                new TimeProjection(
                                                    new FieldTime("t.events", "clock")
                                                ),
                                                new TimeProjection(
                                                    new LiteralTime(new TimeOnly(12, 0))
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["-41400", "43199"], ResultColumn.Texts(projection, "seconds"));
    }

    [Fact]
    public void DatetimeArithmeticWorksOnInstants()
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "elapsed",
                                    new DecimalProjection(
                                        new DifferenceDecimalProjection(
                                            new DatetimeDiffSecondsDecimalProjection(
                                                new DatetimeProjection(
                                                    new FieldDatetime("t.events", "at")
                                                ),
                                                new DatetimeProjection(
                                                    new LiteralDatetime(
                                                        new DateTimeOffset(
                                                            2024,
                                                            1,
                                                            1,
                                                            2,
                                                            0,
                                                            0,
                                                            TimeSpan.FromHours(2)
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDatetime(
                                    "later",
                                    new DatetimeProjection(
                                        new DatetimeAddSecondsDatetimeProjection(
                                            new DatetimeProjection(
                                                new FieldDatetime("t.events", "at")
                                            ),
                                            new DecimalProjection(
                                                new LiteralAsDecimal(
                                                    new LiteralDecimal(1.5m)
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["0", "1"], ResultColumn.Texts(projection, "elapsed"));
        Assert.Equal(
            ["2024-01-01T00:00:01.5", "2024-01-01T00:00:02.5"],
            ResultColumn.Texts(projection, "later")
        );
    }

    [Fact]
    public void DateOutsideTheCalendarFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDate(
                                    "due",
                                    new DateProjection(
                                        new DateAddDaysDateProjection(
                                            new DateProjection(
                                                new FieldDate("t.events", "on")
                                            ),
                                            new IntegerProjection(
                                                new LiteralInteger(3_000_000)
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void TemporalAveragesRoundHalfAwayFromZeroToTheirUnit()
    {
        PureQLProjection projection = new(
            [Events],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.events")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDateNullable(
                                    "mean_day",
                                    new DateNullableProjection(
                                        new AggregateDateProjection(
                                            new AverageDateNullableProjection(
                                                new DateNullableRow(
                                                    new FieldAsDateNullable(
                                                        new FieldDate("t.events", "on")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionTimeNullable(
                                    "mean_clock",
                                    new TimeNullableProjection(
                                        new AggregateTimeProjection(
                                            new AverageTimeNullableProjection(
                                                new TimeNullableRow(
                                                    new FieldAsTimeNullable(
                                                        new FieldTime("t.events", "clock")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDatetimeNullable(
                                    "mean_at",
                                    new DatetimeNullableProjection(
                                        new AggregateDatetimeProjection(
                                            new AverageDatetimeNullableProjection(
                                                new DatetimeNullableRow(
                                                    new FieldAsDatetimeNullable(
                                                        new FieldDatetime("t.events", "at")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["2024-01-02"], ResultColumn.Texts(projection, "mean_day"));
        Assert.Equal(["12:14:59.5"], ResultColumn.Texts(projection, "mean_clock"));
        Assert.Equal(["2024-01-01T00:00:00.5"], ResultColumn.Texts(projection, "mean_at"));
    }
}
