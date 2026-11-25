using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Dates and times in UTC, as the specification defines them under "Dates and
// times": an operator that adds names the amount by its unit (days, seconds)
// and is lifted over it; the calendar parts of a date and of a datetime, the
// clock parts of a time and of a datetime, with ISO 8601 weeks; startOf… of
// every type it takes, in the type of its operand; dateFromParts,
// timeFromParts and datetimeFromParts, the inverse of the parts, where a part
// outside its range is an execution error and nothing is clamped.
public sealed class DatePartsTests
{
    // Row 1 holds every value, row 2 no date, time or datetime, row 3 no
    // number: a lifted operator is null in rows 2 and 3.
    private static readonly IStoredSchemaDataSet Moments = new InlineTable(
        "t",
        "moments",
        ("id", new LongColumnType()),
        ("d", new DateColumnType()),
        ("t", new TimeColumnType()),
        ("dt", new DateTimeColumnType()),
        ("n", new LongColumnType()),
        ("s", new DoubleColumnType())
    ).With(
        ["1", "2024-08-15", "18:30:15.25", "2024-08-15T18:30:15.25Z", "2", "1.5"],
        ["2", "", "", "", "2", "1.5"],
        ["3", "2024-08-15", "18:30:15.25", "2024-08-15T18:30:15.25Z", "", ""]
    );

    private static readonly FieldInteger Id = new("t.moments", "id");

    private static readonly FieldDateNullable D = new("t.moments", "d");

    private static readonly FieldTimeNullable T = new("t.moments", "t");

    private static readonly FieldDatetimeNullable Dt = new("t.moments", "dt");

    private static readonly FieldIntegerNullable N = new("t.moments", "n");

    private static readonly FieldDecimalNullable S = new("t.moments", "s");

    [Theory]
    [InlineData("2024-02-28", 2, "2024-03-01")]
    [InlineData("2023-02-28", 1, "2023-03-01")]
    [InlineData("2024-03-01", -1, "2024-02-29")]
    [InlineData("2024-12-31", 1, "2025-01-01")]
    [InlineData("2024-08-15", 0, "2024-08-15")]
    public void DateAddDaysAddsWholeCalendarDays(string date, long days, string expected)
    {
        Assert.Equal(
            expected,
            Value(new DateAddDaysDateProjection(Date(date), new LiteralInteger(days)))
        );
    }

    [Fact]
    public void DateAddDaysIsLiftedOverTheDateAndTheDays()
    {
        Assert.Equal(
            ["2024-08-17", "", ""],
            Values(new DateAddDaysDateNullableProjection(D, N))
        );
    }

    [Theory]
    [InlineData("9999-12-31", 1)]
    [InlineData("0001-01-01", -1)]
    public void DateOutsideTheYears0001To9999IsAnExecutionError(string date, long days)
    {
        PureQLProjection projection = Selecting(
            new DateAddDaysDateProjection(Date(date), new LiteralInteger(days))
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Theory]
    [InlineData("2024-03-01", "2024-02-28", "2")]
    [InlineData("2024-02-28", "2024-03-01", "-2")]
    public void DateDiffDaysIsLeftMinusRight(string left, string right, string expected)
    {
        Assert.Equal(
            expected,
            Value(new DateDiffDaysIntegerProjection(Date(left), Date(right)))
        );
    }

    [Theory]
    [InlineData("23:00:00", "7200", "01:00:00")]
    [InlineData("00:30:00", "-3600", "23:30:00")]
    [InlineData("18:30:00", "15.25", "18:30:15.25")]
    [InlineData("00:00:00", "0.0000005", "00:00:00.000001")]
    [InlineData("00:00:00", "0.0000004", "00:00:00")]
    [InlineData("00:00:01", "-0.0000005", "00:00:00.999999")]
    public void TimeAddSecondsTakesItsAmountAsSecondsAndWrapsAroundMidnight(
        string time,
        string seconds,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new TimeAddSecondsTimeProjection(Time(time), Decimal(seconds)))
        );
    }

    [Fact]
    public void TimeAddSecondsAcceptsAnIntegerNumberOfSeconds()
    {
        Assert.Equal(
            "01:00:00",
            Value(
                new TimeAddSecondsTimeProjection(Time("23:00:00"), new LiteralInteger(7200))
            )
        );
    }

    [Fact]
    public void TimeAddSecondsIsLiftedOverTheTimeAndTheSeconds()
    {
        Assert.Equal(
            ["18:30:16.75", "", ""],
            Values(new TimeAddSecondsTimeNullableProjection(T, S))
        );
    }

    [Theory]
    [InlineData("01:00:00", "23:00:00", "-79200")]
    [InlineData("18:30:15.25", "18:30:00", "15.25")]
    public void TimeDiffSecondsIsLeftMinusRightWithoutWrapping(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new TimeDiffSecondsDecimalProjection(Time(left), Time(right)))
        );
    }

    [Theory]
    [InlineData("2024-02-28T23:59:59Z", "2", "2024-02-29T00:00:01Z")]
    [InlineData("2024-01-01T00:00:00Z", "-0.5", "2023-12-31T23:59:59.5Z")]
    [InlineData("2024-08-15T18:30:00Z", "86400", "2024-08-16T18:30:00Z")]
    [InlineData("2024-08-15T18:30:00Z", "0.0000015", "2024-08-15T18:30:00.000002Z")]
    public void DatetimeAddSecondsAddsSecondsToTheInstant(
        string instant,
        string seconds,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeAddSecondsDatetimeProjection(
                    Instant(instant),
                    Decimal(seconds)
                )
            )
        );
    }

    [Fact]
    public void HoursAreAddedAsSecondsMultipliedBy3600()
    {
        Assert.Equal(
            "2024-08-16T01:30:00Z",
            Value(
                new DatetimeAddSecondsDatetimeProjection(
                    Instant("2024-08-15T18:30:00Z"),
                    new MultiplyIntegerProjection([
                        new LiteralInteger(7),
                        new LiteralInteger(3600),
                    ])
                )
            )
        );
    }

    [Fact]
    public void DatetimeAddSecondsIsLiftedOverTheInstantAndTheSeconds()
    {
        Assert.Equal(
            ["2024-08-15T18:30:16.75Z", "", ""],
            Values(new DatetimeAddSecondsDatetimeNullableProjection(Dt, S))
        );
    }

    [Theory]
    [InlineData("2024-02-29T00:00:01Z", "2024-02-28T23:59:59Z", "2")]
    [InlineData("2024-08-15T18:30:00Z", "2024-08-15T18:30:15.25Z", "-15.25")]
    public void DatetimeDiffSecondsIsLeftMinusRight(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeDiffSecondsDecimalProjection(Instant(left), Instant(right))
            )
        );
    }

    [Theory]
    [InlineData("year", "2024-12-30", "2024")]
    [InlineData("week", "2024-12-30", "1")]
    [InlineData("weekYear", "2024-12-30", "2025")]
    [InlineData("dayOfWeek", "2024-12-30", "1")]
    [InlineData("year", "2021-01-01", "2021")]
    [InlineData("week", "2021-01-01", "53")]
    [InlineData("weekYear", "2021-01-01", "2020")]
    [InlineData("month", "2024-03-31", "3")]
    [InlineData("day", "2024-03-31", "31")]
    [InlineData("dayOfWeek", "2024-03-31", "7")]
    [InlineData("quarter", "2024-03-31", "1")]
    [InlineData("quarter", "2024-04-01", "2")]
    [InlineData("quarter", "2024-09-30", "3")]
    [InlineData("quarter", "2024-10-01", "4")]
    [InlineData("dayOfYear", "2024-01-01", "1")]
    [InlineData("dayOfYear", "2024-12-31", "366")]
    [InlineData("dayOfYear", "2023-12-31", "365")]
    [InlineData("year", "0001-01-01", "1")]
    [InlineData("year", "9999-12-31", "9999")]
    [InlineData("month", "2024-12-31", "12")]
    [InlineData("day", "2024-02-01", "1")]
    [InlineData("week", "2024-08-15", "33")]
    [InlineData("weekYear", "2024-08-15", "2024")]
    public void CalendarPartOfADate(string part, string date, string expected)
    {
        Assert.Equal(expected, Value(DatePart(part, Date(date))));
    }

    [Theory]
    [InlineData("year", "2024-12-30T23:59:59Z", "2024")]
    [InlineData("week", "2024-12-30T23:59:59Z", "1")]
    [InlineData("weekYear", "2024-12-30T23:59:59Z", "2025")]
    [InlineData("week", "2021-01-01T00:00:00Z", "53")]
    [InlineData("weekYear", "2021-01-01T00:00:00Z", "2020")]
    [InlineData("month", "2024-03-31T22:30:00Z", "3")]
    [InlineData("day", "2024-03-31T22:30:00Z", "31")]
    [InlineData("dayOfWeek", "2024-03-31T22:30:00Z", "7")]
    [InlineData("quarter", "2024-08-15T18:30:15.25Z", "3")]
    [InlineData("dayOfYear", "2024-12-31T23:59:59.999999Z", "366")]
    public void CalendarPartOfADatetimeIsReadInUtc(
        string part,
        string instant,
        string expected
    )
    {
        Assert.Equal(expected, Value(DatetimeCalendarPart(part, Instant(instant))));
    }

    [Theory]
    [InlineData("hour", "18:30:15.25", "18")]
    [InlineData("minute", "18:30:15.25", "30")]
    [InlineData("second", "18:30:15.25", "15")]
    [InlineData("microsecond", "18:30:15.25", "250000")]
    [InlineData("hour", "00:00:00", "0")]
    [InlineData("minute", "00:00:00", "0")]
    [InlineData("second", "00:00:00", "0")]
    [InlineData("microsecond", "00:00:00", "0")]
    [InlineData("hour", "23:59:59.999999", "23")]
    [InlineData("minute", "23:59:59.999999", "59")]
    [InlineData("second", "23:59:59.999999", "59")]
    [InlineData("microsecond", "23:59:59.999999", "999999")]
    public void ClockPartOfATime(string part, string time, string expected)
    {
        Assert.Equal(expected, Value(TimePart(part, Time(time))));
    }

    [Theory]
    [InlineData("hour", "2024-03-31T22:30:00Z", "22")]
    [InlineData("minute", "2024-03-31T22:30:00Z", "30")]
    [InlineData("second", "2024-08-15T18:30:15.25Z", "15")]
    [InlineData("microsecond", "2024-08-15T18:30:15.25Z", "250000")]
    [InlineData("hour", "2024-08-15T00:00:00Z", "0")]
    [InlineData("microsecond", "2024-08-15T23:59:59.999999Z", "999999")]
    public void ClockPartOfADatetimeIsReadInUtc(
        string part,
        string instant,
        string expected
    )
    {
        Assert.Equal(expected, Value(DatetimeClockPart(part, Instant(instant))));
    }

    [Theory]
    [InlineData("year", "2024")]
    [InlineData("quarter", "3")]
    [InlineData("month", "8")]
    [InlineData("day", "15")]
    [InlineData("dayOfWeek", "4")]
    [InlineData("dayOfYear", "228")]
    [InlineData("week", "33")]
    [InlineData("weekYear", "2024")]
    public void CalendarPartOfANullDateOrDatetimeIsNull(string part, string expected)
    {
        Assert.Equal([expected, "", expected], Values(NullableDatePart(part)));
        Assert.Equal([expected, "", expected], Values(NullableDatetimePart(part)));
    }

    [Theory]
    [InlineData("hour", "18")]
    [InlineData("minute", "30")]
    [InlineData("second", "15")]
    [InlineData("microsecond", "250000")]
    public void ClockPartOfANullTimeOrDatetimeIsNull(string part, string expected)
    {
        Assert.Equal([expected, "", expected], Values(NullableTimePart(part)));
        Assert.Equal([expected, "", expected], Values(NullableDatetimePart(part)));
    }

    [Theory]
    [InlineData("year", "2024-01-01T00:00:00Z")]
    [InlineData("quarter", "2024-07-01T00:00:00Z")]
    [InlineData("month", "2024-08-01T00:00:00Z")]
    [InlineData("week", "2024-08-12T00:00:00Z")]
    [InlineData("day", "2024-08-15T00:00:00Z")]
    [InlineData("hour", "2024-08-15T18:00:00Z")]
    [InlineData("minute", "2024-08-15T18:30:00Z")]
    [InlineData("second", "2024-08-15T18:30:15Z")]
    public void StartOfAPeriodOfADatetimeIsTruncatedInUtc(string period, string expected)
    {
        Assert.Equal(
            expected,
            Value(DatetimeStart(period, Instant("2024-08-15T18:30:15.25Z")))
        );
        Assert.Equal([expected, "", expected], Values(NullableDatetimeStart(period)));
    }

    [Theory]
    [InlineData("year", "2024-08-15", "2024-01-01")]
    [InlineData("quarter", "2024-08-15", "2024-07-01")]
    [InlineData("month", "2024-08-15", "2024-08-01")]
    [InlineData("week", "2024-08-15", "2024-08-12")]
    [InlineData("week", "2025-01-01", "2024-12-30")]
    [InlineData("week", "2024-08-12", "2024-08-12")]
    [InlineData("week", "2024-08-18", "2024-08-12")]
    [InlineData("week", "0001-01-01", "0001-01-01")]
    [InlineData("week", "0001-01-07", "0001-01-01")]
    [InlineData("quarter", "2024-03-31", "2024-01-01")]
    [InlineData("quarter", "2024-06-30", "2024-04-01")]
    [InlineData("quarter", "2024-12-31", "2024-10-01")]
    [InlineData("year", "2024-12-31", "2024-01-01")]
    [InlineData("month", "2024-02-29", "2024-02-01")]
    public void StartOfAPeriodOfADateIsADate(string period, string date, string expected)
    {
        Assert.Equal(expected, Value(DateStart(period, Date(date))));
    }

    [Theory]
    [InlineData("year", "2024-01-01")]
    [InlineData("quarter", "2024-07-01")]
    [InlineData("month", "2024-08-01")]
    [InlineData("week", "2024-08-12")]
    public void StartOfAPeriodOfANullDateIsNull(string period, string expected)
    {
        Assert.Equal([expected, "", expected], Values(NullableDateStart(period)));
    }

    [Theory]
    [InlineData("hour", "18:00:00")]
    [InlineData("minute", "18:30:00")]
    [InlineData("second", "18:30:15")]
    public void StartOfAPeriodOfATimeIsATime(string period, string expected)
    {
        Assert.Equal(expected, Value(TimeStart(period, Time("18:30:15.25"))));
        Assert.Equal([expected, "", expected], Values(NullableTimeStart(period)));
    }

    [Theory]
    [InlineData(2024, 2, 29, "2024-02-29")]
    [InlineData(1, 1, 1, "0001-01-01")]
    [InlineData(9999, 12, 31, "9999-12-31")]
    [InlineData(987, 1, 31, "0987-01-31")]
    public void DateFromPartsBuildsTheDate(long year, long month, long day, string expected)
    {
        Assert.Equal(
            expected,
            Value(
                new DateFromPartsDateProjection(
                    new LiteralInteger(year),
                    new LiteralInteger(month),
                    new LiteralInteger(day)
                )
            )
        );
    }

    [Theory]
    [InlineData(2023, 2, 29)]
    [InlineData(2024, 2, 30)]
    [InlineData(2024, 4, 31)]
    [InlineData(2024, 1, 32)]
    [InlineData(2024, 1, 0)]
    [InlineData(2024, 0, 1)]
    [InlineData(2024, 13, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(10000, 1, 1)]
    [InlineData(-1, 1, 1)]
    public void DateFromPartsFailsOnAPartOutsideItsRange(long year, long month, long day)
    {
        PureQLProjection projection = Selecting(
            new DateFromPartsDateProjection(
                new LiteralInteger(year),
                new LiteralInteger(month),
                new LiteralInteger(day)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void DateFromPartsFailsOnAPartOfARowOutsideItsRange()
    {
        PureQLProjection projection = Selecting(
            new DateFromPartsDateNullableProjection(
                new LiteralInteger(2023),
                N,
                new LiteralInteger(30)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void DateFromPartsIsLiftedOverEveryPart()
    {
        LiteralInteger one = new(1);

        Assert.Equal(
            ["0002-01-01", "0002-01-01", ""],
            Values(new DateFromPartsDateNullableProjection(N, one, one))
        );
        Assert.Equal(
            ["2024-02-01", "2024-02-01", ""],
            Values(
                new DateFromPartsDateNullableProjection(new LiteralInteger(2024), N, one)
            )
        );
        Assert.Equal(
            ["2024-01-02", "2024-01-02", ""],
            Values(
                new DateFromPartsDateNullableProjection(new LiteralInteger(2024), one, N)
            )
        );
    }

    [Fact]
    public void DateFromPartsIsTheInverseOfTheParts()
    {
        Assert.Equal(
            ["2024-08-15", "", "2024-08-15"],
            Values(
                new DateFromPartsDateNullableProjection(
                    new YearDateIntegerNullableProjection(D),
                    new MonthDateIntegerNullableProjection(D),
                    new DayDateIntegerNullableProjection(D)
                )
            )
        );
    }

    [Theory]
    [InlineData(18, 30, "15.25", "18:30:15.25")]
    [InlineData(0, 0, "0", "00:00:00")]
    [InlineData(23, 59, "59.999999", "23:59:59.999999")]
    [InlineData(0, 0, "1.0000005", "00:00:01.000001")]
    [InlineData(0, 0, "1.0000004", "00:00:01")]
    [InlineData(23, 59, "59.9999994", "23:59:59.999999")]
    public void TimeFromPartsCarriesTheFractionInItsSecond(
        long hour,
        long minute,
        string second,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new TimeFromPartsTimeProjection(
                    new LiteralInteger(hour),
                    new LiteralInteger(minute),
                    Decimal(second)
                )
            )
        );
    }

    [Fact]
    public void TimeFromPartsAcceptsAnIntegerSecond()
    {
        Assert.Equal(
            "18:30:15",
            Value(
                new TimeFromPartsTimeProjection(
                    new LiteralInteger(18),
                    new LiteralInteger(30),
                    new LiteralInteger(15)
                )
            )
        );
    }

    [Theory]
    [InlineData(24, 0, "0")]
    [InlineData(-1, 0, "0")]
    [InlineData(0, 60, "0")]
    [InlineData(0, -1, "0")]
    [InlineData(0, 0, "60")]
    [InlineData(0, 0, "-1")]
    [InlineData(23, 59, "59.9999995")]
    public void TimeFromPartsFailsOnAPartOutsideItsRange(
        long hour,
        long minute,
        string second
    )
    {
        PureQLProjection projection = Selecting(
            new TimeFromPartsTimeProjection(
                new LiteralInteger(hour),
                new LiteralInteger(minute),
                Decimal(second)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void TimeFromPartsIsLiftedOverEveryPart()
    {
        LiteralInteger one = new(1);

        Assert.Equal(
            ["02:01:01", "02:01:01", ""],
            Values(new TimeFromPartsTimeNullableProjection(N, one, one))
        );
        Assert.Equal(
            ["01:02:01", "01:02:01", ""],
            Values(new TimeFromPartsTimeNullableProjection(one, N, one))
        );
        Assert.Equal(
            ["01:01:01.5", "01:01:01.5", ""],
            Values(new TimeFromPartsTimeNullableProjection(one, one, S))
        );
    }

    [Theory]
    [InlineData("2024-01-31", "18:30:00", "2024-01-31T18:30:00Z")]
    [InlineData("2024-08-15", "18:30:15.25", "2024-08-15T18:30:15.25Z")]
    [InlineData("0001-01-01", "00:00:00", "0001-01-01T00:00:00Z")]
    [InlineData("9999-12-31", "23:59:59.999999", "9999-12-31T23:59:59.999999Z")]
    public void DatetimeFromPartsIsThatDayAndTimeOfDayInUtc(
        string date,
        string time,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DatetimeFromPartsDatetimeProjection(Date(date), Time(time)))
        );
    }

    [Fact]
    public void DatetimeFromPartsWithMidnightIsAsDatetimeOfTheDate()
    {
        Assert.Equal(
            Value(new AsDatetimeDateDatetimeProjection(Date("2024-01-31"))),
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date("2024-01-31"),
                    Time("00:00:00")
                )
            )
        );
    }

    [Fact]
    public void DatetimeFromPartsIsLiftedOverTheDateAndTheTime()
    {
        Assert.Equal(
            ["2024-08-15T12:00:00Z", "", "2024-08-15T12:00:00Z"],
            Values(new DatetimeFromPartsDatetimeNullableProjection(D, Time("12:00:00")))
        );
        Assert.Equal(
            ["2024-01-31T18:30:15.25Z", "", "2024-01-31T18:30:15.25Z"],
            Values(new DatetimeFromPartsDatetimeNullableProjection(Date("2024-01-31"), T))
        );
    }

    [Fact]
    public void DatetimeFromPartsOfTheDateAndTheTimeOfAnInstantIsTheInstant()
    {
        Assert.Equal(
            ["2024-08-15T18:30:15.25Z", "", "2024-08-15T18:30:15.25Z"],
            Values(
                new DatetimeFromPartsDatetimeNullableProjection(
                    new AsDateDatetimeDateNullableProjection(Dt),
                    new AsTimeDatetimeTimeNullableProjection(Dt)
                )
            )
        );
    }

    [Fact]
    public void PartsFilterRowsInWhere()
    {
        PureQLProjection projection = new(
            [Moments],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.moments"),
                where: new AndRow([
                    new EqualDecimalRow(
                        new MonthDateIntegerNullableRow(D),
                        new LiteralInteger(8)
                    ),
                    new EqualDecimalRow(
                        new HourTimeIntegerNullableRow(T),
                        new LiteralInteger(18)
                    ),
                    new EqualTimeRow(
                        new StartOfMinuteTimeNullableRow(T),
                        Time("18:30:00")
                    ),
                ]),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["1", "3"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void StartOfMonthIsAGroupKeyAndItsPartsAreReadInAGroup()
    {
        IStoredSchemaDataSet orders = new InlineTable(
            "t",
            "orders",
            ("id", new LongColumnType()),
            ("day", new DateColumnType())
        ).With(
            ["1", "2024-01-31"],
            ["2", "2024-02-01"],
            ["3", "2024-02-29"],
            ["4", "2024-01-01"],
            ["5", "2023-01-15"]
        );
        PureQLProjection projection = new(
            [orders],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                groupBy:
                [
                    new GroupKey(
                        "month",
                        new StartOfMonthDateRow(new FieldDate("t.orders", "day"))
                    ),
                ],
                select:
                [
                    new SelectItemGroup("month", new KeyDate("month")),
                    new SelectItemGroup(
                        "year",
                        new YearDateIntegerGroup(new KeyDate("month"))
                    ),
                    new SelectItemGroup(
                        "next",
                        new DateAddDaysDateGroup(
                            new KeyDate("month"),
                            new CountGroup()
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("month")]
            )
        );

        Assert.Equal(
            ["2023-01-01", "2024-01-01", "2024-02-01"],
            ResultColumn.Texts(projection, "month")
        );
        Assert.Equal(["2023", "2024", "2024"], ResultColumn.Texts(projection, "year"));
        Assert.Equal(
            ["2023-01-02", "2024-01-03", "2024-02-03"],
            ResultColumn.Texts(projection, "next")
        );
    }

    [Fact]
    public void PartsOfAnAggregateAreReadInATotalQuery()
    {
        PureQLProjection projection = new(
            [Moments],
            new MainTotalQuery(
                from: new FromEntity("t.moments"),
                aggregate:
                [
                    new SelectItemTotal(
                        "week",
                        new WeekDateIntegerNullableTotal(new MaxDateNullableTotal(D))
                    ),
                    new SelectItemTotal(
                        "microsecond",
                        new MicrosecondTimeIntegerNullableTotal(
                            new MaxTimeNullableTotal(T)
                        )
                    ),
                    new SelectItemTotal(
                        "hour",
                        new StartOfHourDatetimeNullableTotal(
                            new MaxDatetimeNullableTotal(Dt)
                        )
                    ),
                    new SelectItemTotal(
                        "built",
                        new DateFromPartsDateNullableTotal(
                            new LiteralInteger(2024),
                            new SumIntegerTotal(N),
                            new CountTotal()
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["33"], ResultColumn.Texts(projection, "week"));
        Assert.Equal(["250000"], ResultColumn.Texts(projection, "microsecond"));
        Assert.Equal(["2024-08-15T18:00:00Z"], ResultColumn.Texts(projection, "hour"));
        Assert.Equal(["2024-04-03"], ResultColumn.Texts(projection, "built"));
    }

    private static ExprProjection DatePart(string part, DateProjection operand)
    {
        return part switch
        {
            "year" => new YearDateIntegerProjection(operand),
            "quarter" => new QuarterDateIntegerProjection(operand),
            "month" => new MonthDateIntegerProjection(operand),
            "day" => new DayDateIntegerProjection(operand),
            "dayOfWeek" => new DayOfWeekDateIntegerProjection(operand),
            "dayOfYear" => new DayOfYearDateIntegerProjection(operand),
            "week" => new WeekDateIntegerProjection(operand),
            "weekYear" => new WeekYearDateIntegerProjection(operand),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection NullableDatePart(string part)
    {
        return part switch
        {
            "year" => new YearDateIntegerNullableProjection(D),
            "quarter" => new QuarterDateIntegerNullableProjection(D),
            "month" => new MonthDateIntegerNullableProjection(D),
            "day" => new DayDateIntegerNullableProjection(D),
            "dayOfWeek" => new DayOfWeekDateIntegerNullableProjection(D),
            "dayOfYear" => new DayOfYearDateIntegerNullableProjection(D),
            "week" => new WeekDateIntegerNullableProjection(D),
            "weekYear" => new WeekYearDateIntegerNullableProjection(D),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection DatetimeCalendarPart(
        string part,
        DatetimeProjection operand
    )
    {
        return part switch
        {
            "year" => new YearDatetimeIntegerProjection(operand),
            "quarter" => new QuarterDatetimeIntegerProjection(operand),
            "month" => new MonthDatetimeIntegerProjection(operand),
            "day" => new DayDatetimeIntegerProjection(operand),
            "dayOfWeek" => new DayOfWeekDatetimeIntegerProjection(operand),
            "dayOfYear" => new DayOfYearDatetimeIntegerProjection(operand),
            "week" => new WeekDatetimeIntegerProjection(operand),
            "weekYear" => new WeekYearDatetimeIntegerProjection(operand),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection DatetimeClockPart(
        string part,
        DatetimeProjection operand
    )
    {
        return part switch
        {
            "hour" => new HourDatetimeIntegerProjection(operand),
            "minute" => new MinuteDatetimeIntegerProjection(operand),
            "second" => new SecondDatetimeIntegerProjection(operand),
            "microsecond" => new MicrosecondDatetimeIntegerProjection(operand),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection NullableDatetimePart(string part)
    {
        return part switch
        {
            "year" => new YearDatetimeIntegerNullableProjection(Dt),
            "quarter" => new QuarterDatetimeIntegerNullableProjection(Dt),
            "month" => new MonthDatetimeIntegerNullableProjection(Dt),
            "day" => new DayDatetimeIntegerNullableProjection(Dt),
            "dayOfWeek" => new DayOfWeekDatetimeIntegerNullableProjection(Dt),
            "dayOfYear" => new DayOfYearDatetimeIntegerNullableProjection(Dt),
            "week" => new WeekDatetimeIntegerNullableProjection(Dt),
            "weekYear" => new WeekYearDatetimeIntegerNullableProjection(Dt),
            "hour" => new HourDatetimeIntegerNullableProjection(Dt),
            "minute" => new MinuteDatetimeIntegerNullableProjection(Dt),
            "second" => new SecondDatetimeIntegerNullableProjection(Dt),
            "microsecond" => new MicrosecondDatetimeIntegerNullableProjection(Dt),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection TimePart(string part, TimeProjection operand)
    {
        return part switch
        {
            "hour" => new HourTimeIntegerProjection(operand),
            "minute" => new MinuteTimeIntegerProjection(operand),
            "second" => new SecondTimeIntegerProjection(operand),
            "microsecond" => new MicrosecondTimeIntegerProjection(operand),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection NullableTimePart(string part)
    {
        return part switch
        {
            "hour" => new HourTimeIntegerNullableProjection(T),
            "minute" => new MinuteTimeIntegerNullableProjection(T),
            "second" => new SecondTimeIntegerNullableProjection(T),
            "microsecond" => new MicrosecondTimeIntegerNullableProjection(T),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection DatetimeStart(string period, DatetimeProjection operand)
    {
        return period switch
        {
            "year" => new StartOfYearDatetimeProjection(operand),
            "quarter" => new StartOfQuarterDatetimeProjection(operand),
            "month" => new StartOfMonthDatetimeProjection(operand),
            "week" => new StartOfWeekDatetimeProjection(operand),
            "day" => new StartOfDayDatetimeProjection(operand),
            "hour" => new StartOfHourDatetimeProjection(operand),
            "minute" => new StartOfMinuteDatetimeProjection(operand),
            "second" => new StartOfSecondDatetimeProjection(operand),
            _ => throw new ArgumentException(period),
        };
    }

    private static ExprProjection NullableDatetimeStart(string period)
    {
        return period switch
        {
            "year" => new StartOfYearDatetimeNullableProjection(Dt),
            "quarter" => new StartOfQuarterDatetimeNullableProjection(Dt),
            "month" => new StartOfMonthDatetimeNullableProjection(Dt),
            "week" => new StartOfWeekDatetimeNullableProjection(Dt),
            "day" => new StartOfDayDatetimeNullableProjection(Dt),
            "hour" => new StartOfHourDatetimeNullableProjection(Dt),
            "minute" => new StartOfMinuteDatetimeNullableProjection(Dt),
            "second" => new StartOfSecondDatetimeNullableProjection(Dt),
            _ => throw new ArgumentException(period),
        };
    }

    private static ExprProjection DateStart(string period, DateProjection operand)
    {
        return period switch
        {
            "year" => new StartOfYearDateProjection(operand),
            "quarter" => new StartOfQuarterDateProjection(operand),
            "month" => new StartOfMonthDateProjection(operand),
            "week" => new StartOfWeekDateProjection(operand),
            _ => throw new ArgumentException(period),
        };
    }

    private static ExprProjection NullableDateStart(string period)
    {
        return period switch
        {
            "year" => new StartOfYearDateNullableProjection(D),
            "quarter" => new StartOfQuarterDateNullableProjection(D),
            "month" => new StartOfMonthDateNullableProjection(D),
            "week" => new StartOfWeekDateNullableProjection(D),
            _ => throw new ArgumentException(period),
        };
    }

    private static ExprProjection TimeStart(string period, TimeProjection operand)
    {
        return period switch
        {
            "hour" => new StartOfHourTimeProjection(operand),
            "minute" => new StartOfMinuteTimeProjection(operand),
            "second" => new StartOfSecondTimeProjection(operand),
            _ => throw new ArgumentException(period),
        };
    }

    private static ExprProjection NullableTimeStart(string period)
    {
        return period switch
        {
            "hour" => new StartOfHourTimeNullableProjection(T),
            "minute" => new StartOfMinuteTimeNullableProjection(T),
            "second" => new StartOfSecondTimeNullableProjection(T),
            _ => throw new ArgumentException(period),
        };
    }

    private static LiteralDate Date(string text)
    {
        return new LiteralDate(
            DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        );
    }

    private static LiteralTime Time(string text)
    {
        return new LiteralTime(TimeOnly.Parse(text, CultureInfo.InvariantCulture));
    }

    private static LiteralDatetime Instant(string text)
    {
        return new LiteralDatetime(
            DateTimeOffset.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal
            )
        );
    }

    private static LiteralDecimal Decimal(string text)
    {
        return new LiteralDecimal(decimal.Parse(text, CultureInfo.InvariantCulture));
    }

    // The query that selects one expression for every row, in the order of id.
    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return new PureQLProjection(
            [Moments],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.moments"),
                select: [new SelectItemProjection("value", expression)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );
    }

    private static IReadOnlyList<string> Values(ExprProjection expression)
    {
        return ResultColumn.Texts(Selecting(expression), "value");
    }

    // The value of an expression that reads no row.
    private static string Value(ExprProjection expression)
    {
        return Values(expression)[0];
    }
}
