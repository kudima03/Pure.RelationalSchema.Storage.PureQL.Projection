using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Dates, times and datetimes: a datetime is an instant, read as UTC when
// stored without an offset and written in UTC; timeAddSeconds wraps around
// midnight; the *Diff operators are left - right; seconds and averages round
// half away from zero to the unit of their type, the microsecond for times
// and datetimes.
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection("at", new FieldDatetime("t.events", "at")),
                ]
            )
        );

        Assert.Equal(
            ["2024-01-01T00:00:00Z", "2024-01-01T00:00:01Z"],
            ResultColumn.Texts(projection, "at")
        );
    }

    [Theory]
    [InlineData("-3600", "23:30:00", "22:59:59")]
    [InlineData("0.5", "00:30:00.5", "23:59:59.5")]
    [InlineData("1", "00:30:01", "00:00:00")]
    [InlineData("0.0000005", "00:30:00.000001", "23:59:59.000001")]
    [InlineData("-0.0000005", "00:29:59.999999", "23:59:58.999999")]
    [InlineData("0.0000004", "00:30:00", "23:59:59")]
    public void TimeAddSecondsWrapsAroundMidnight(
        string seconds,
        string first,
        string second
    )
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "shifted",
                        new TimeAddSecondsTimeProjection(
                            new FieldTime("t.events", "clock"),
                            new LiteralDecimal(
                                decimal.Parse(seconds, CultureInfo.InvariantCulture)
                            )
                        )
                    ),
                ]
            )
        );

        Assert.Equal([first, second], ResultColumn.Texts(projection, "shifted"));
    }

    [Fact]
    public void TimeDiffSecondsIsNegativeWhenLeftIsEarlier()
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "seconds",
                        new TimeDiffSecondsDecimalProjection(
                            new FieldTime("t.events", "clock"),
                            new LiteralTime(new TimeOnly(12, 0))
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["-41400", "43199"], ResultColumn.Texts(projection, "seconds"));
    }

    [Fact]
    public void DatetimeArithmeticWorksOnInstants()
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "elapsed",
                        new DatetimeDiffSecondsDecimalProjection(
                            new FieldDatetime("t.events", "at"),
                            new LiteralDatetime(
                                new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "later",
                        new DatetimeAddSecondsDatetimeProjection(
                            new FieldDatetime("t.events", "at"),
                            new LiteralDecimal(1.5m)
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["0", "1"], ResultColumn.Texts(projection, "elapsed"));
        Assert.Equal(
            ["2024-01-01T00:00:01.5Z", "2024-01-01T00:00:02.5Z"],
            ResultColumn.Texts(projection, "later")
        );
    }

    [Theory]
    [InlineData("2026-01-31T15:30:00Z")]
    [InlineData("2026-01-31T15:30:00.5Z")]
    [InlineData("2026-01-31T15:30:00.123456Z")]
    public void DatetimeLiteralIsWrittenAsItsLiteralText(string literal)
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "at",
                        new LiteralDatetime(
                            DateTimeOffset.Parse(literal, CultureInfo.InvariantCulture)
                        )
                    ),
                ]
            )
        );

        Assert.Equal([literal, literal], ResultColumn.Texts(projection, "at"));
    }

    [Fact]
    public void DateOutsideTheCalendarFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "due",
                        new DateAddDaysDateProjection(
                            new FieldDate("t.events", "on"),
                            new LiteralInteger(3_000_000)
                        )
                    ),
                ]
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void TemporalAveragesRoundHalfAwayFromZeroToTheirUnit()
    {
        PureQLProjection projection = new(
            [Events],
            new MainTotalQuery(
                from: new FromEntity("t.events"),
                aggregate:
                [
                    new SelectItemTotal(
                        "mean_day",
                        new AverageDateNullableTotal(new FieldDate("t.events", "on"))
                    ),
                    new SelectItemTotal(
                        "mean_clock",
                        new AverageTimeNullableTotal(new FieldTime("t.events", "clock"))
                    ),
                    new SelectItemTotal(
                        "mean_at",
                        new AverageDatetimeNullableTotal(
                            new FieldDatetime("t.events", "at")
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["2024-01-02"], ResultColumn.Texts(projection, "mean_day"));
        Assert.Equal(["12:14:59.5"], ResultColumn.Texts(projection, "mean_clock"));
        Assert.Equal(
            ["2024-01-01T00:00:00.5Z"],
            ResultColumn.Texts(projection, "mean_at")
        );
    }

    [Fact]
    public void TimeAndDatetimeAveragesRoundToTheMicrosecond()
    {
        PureQLProjection projection = new(
            [
                new InlineTable(
                    "t",
                    "ticks",
                    ("at", new DateTimeColumnType()),
                    ("clock", new TimeColumnType())
                ).With(
                    ["2024-01-01T00:00:00", "00:00:00"],
                    ["2024-01-01T00:00:00.000001", "00:00:00.000001"]
                ),
            ],
            new MainTotalQuery(
                from: new FromEntity("t.ticks"),
                aggregate:
                [
                    new SelectItemTotal(
                        "mean_clock",
                        new AverageTimeNullableTotal(new FieldTime("t.ticks", "clock"))
                    ),
                    new SelectItemTotal(
                        "mean_at",
                        new AverageDatetimeNullableTotal(
                            new FieldDatetime("t.ticks", "at")
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["00:00:00.000001"], ResultColumn.Texts(projection, "mean_clock"));
        Assert.Equal(
            ["2024-01-01T00:00:00.000001Z"],
            ResultColumn.Texts(projection, "mean_at")
        );
    }
}
