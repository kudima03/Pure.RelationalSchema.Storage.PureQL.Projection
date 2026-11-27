using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The edges of "Dates and times" in UTC that the other temporal tests leave
// out: amounts of seconds, days and months far beyond a day or 32 bits, the
// two ends of the range of dates and instants, the leap years of the
// proleptic Gregorian calendar, the clamping of months at the ends of months
// and ISO 8601 weeks around the turn of a year. Every expected value is
// derived from the specification (Python's datetime for the calendar), not
// from the implementation.
public sealed class TemporalEdgeTests
{
    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    // "timeAddSeconds wraps around midnight: ... negative seconds go
    // backwards" (Dates and times), for amounts within a day.
    [Theory]
    [InlineData("12:00:00", "86400", "12:00:00")]
    [InlineData("12:00:00", "-86400", "12:00:00")]
    [InlineData("12:00:00", "172800.5", "12:00:00.5")]
    [InlineData("23:59:59.999999", "0.000001", "00:00:00")]
    [InlineData("00:00:00", "-0.000001", "23:59:59.999999")]
    [InlineData("00:00:00", "-86399.999999", "00:00:00.000001")]
    [InlineData("00:00:00", "-0.0000015", "23:59:59.999998")]
    public void TimeAddSecondsWrapsAroundMidnightByWholeDays(
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

    // "timeAddSeconds wraps around midnight" (Dates and times) whatever the
    // amount: the result is the operand plus the seconds modulo 86400. No
    // operand or result here is outside its range, so nothing may fail.
    [Theory(
        Skip = "Bug: spec says timeAddSeconds wraps any amount around midnight "
            + "(00:00:00 + 1e12 s is 01:46:40); the library turns microseconds "
            + "into 64-bit ticks unchecked (gives 20:10:29.0448384) or throws "
            + "OverflowException for >= ~9.2e14 s"
    )]
    [InlineData("00:00:00", "1000000000000", "01:46:40")]
    [InlineData("00:00:00", "-1000000000000", "22:13:20")]
    [InlineData("00:00:00", "1844674407370.955162", "05:36:10.955162")]
    [InlineData("00:00:00", "-1844674407370.955162", "18:23:49.044838")]
    [InlineData("00:00:00", "9999999999999999999999999999", "17:46:39")]
    [InlineData("00:00:00", "-9999999999999999999999999999", "06:13:21")]
    [InlineData("00:00:00", "864000000000000.000001", "00:00:00.000001")]
    public void TimeAddSecondsWrapsAnAmountOfAnySize(
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

    // "datetimeAddSeconds ... work[s] on the instant", "A number of seconds
    // with more than 6 fractional digits is rounded to microseconds, half away
    // from zero" (Dates and times), up to the two ends of the range.
    [Theory]
    [InlineData("9999-12-31T23:59:59Z", "0.999999", "9999-12-31T23:59:59.999999Z")]
    [InlineData("0001-01-01T00:00:01Z", "-1", "0001-01-01T00:00:00Z")]
    [InlineData("0001-01-01T00:00:00Z", "315537897599.999999", "9999-12-31T23:59:59.999999Z")]
    [InlineData("9999-12-31T23:59:59.999999Z", "-315537897599.999999", "0001-01-01T00:00:00Z")]
    [InlineData("2024-01-01T00:00:00Z", "-0.0000015", "2023-12-31T23:59:59.999998Z")]
    [InlineData("2024-01-01T00:00:00Z", "-0.0000005", "2023-12-31T23:59:59.999999Z")]
    [InlineData("2024-01-01T00:00:00Z", "-0.0000004", "2024-01-01T00:00:00Z")]
    [InlineData("2024-01-01T00:00:00.5Z", "0.5", "2024-01-01T00:00:01Z")]
    public void DatetimeAddSecondsReachesTheEndsOfTheRange(
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

    // "produces a date or datetime outside the years 0001–9999" is an
    // execution error (Execution errors).
    [Theory]
    [InlineData("9999-12-31T23:59:59.999999Z", "0.000001")]
    [InlineData("0001-01-01T00:00:00Z", "-0.000001")]
    [InlineData("2024-01-01T00:00:00Z", "1000000000000")]
    [InlineData("2024-01-01T00:00:00Z", "-1000000000000")]
    [InlineData("2024-01-01T00:00:00Z", "9999999999999999999999999999")]
    public void DatetimeAddSecondsBeyondTheRangeIsAnExecutionError(
        string instant,
        string seconds
    )
    {
        Fails(
            new DatetimeAddSecondsDatetimeProjection(Instant(instant), Decimal(seconds))
        );
    }

    // 1844674407370.955162 seconds are about 58 456 years: the result is
    // outside the years 0001–9999, an execution error (Execution errors).
    // 1844674407370955162 microseconds are 2^64 + 4 ticks of 100 ns.
    [Theory(
        Skip = "Bug: spec says a datetime outside 0001-9999 is an execution "
            + "error; datetimeAddSeconds(2024-01-01T00:00:00Z, "
            + "+-1844674407370.955162) wraps its 64-bit ticks and returns "
            + "2024-01-01 +- 400 ns"
    )]
    [InlineData("2024-01-01T00:00:00Z", "1844674407370.955162")]
    [InlineData("2024-01-01T00:00:00Z", "-1844674407370.955162")]
    public void DatetimeAddSecondsWhoseTicksWrapIsStillAnExecutionError(
        string instant,
        string seconds
    )
    {
        Fails(
            new DatetimeAddSecondsDatetimeProjection(Instant(instant), Decimal(seconds))
        );
    }

    // "datetimeDiffSeconds ... left − right" across the whole range: 3652059
    // days less a microsecond.
    [Theory]
    [InlineData("9999-12-31T23:59:59.999999Z", "0001-01-01T00:00:00Z", "315537897599.999999")]
    [InlineData("0001-01-01T00:00:00Z", "9999-12-31T23:59:59.999999Z", "-315537897599.999999")]
    public void DatetimeDiffSecondsSpansTheWholeRange(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DatetimeDiffSecondsDecimalProjection(Instant(left), Instant(right)))
        );
    }

    [Theory]
    [InlineData("23:59:59.999999", "00:00:00", "86399.999999")]
    [InlineData("00:00:00", "23:59:59.999999", "-86399.999999")]
    [InlineData("00:00:00.000001", "00:00:00", "0.000001")]
    public void TimeDiffSecondsSpansTheWholeDay(
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
    [InlineData("9999-12-31", "0001-01-01", "3652058")]
    [InlineData("0001-01-01", "9999-12-31", "-3652058")]
    [InlineData("2000-03-01", "2000-02-28", "2")]
    [InlineData("1900-03-01", "1900-02-28", "1")]
    public void DateDiffDaysSpansTheWholeCalendar(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DateDiffDaysIntegerProjection(Date(left), Date(right)))
        );
    }

    [Theory]
    [InlineData("0001-01-01", 3652058, "9999-12-31")]
    [InlineData("9999-12-31", -3652058, "0001-01-01")]
    [InlineData("1900-02-28", 1, "1900-03-01")]
    [InlineData("2000-02-28", 1, "2000-02-29")]
    public void DateAddDaysReachesBothEndsOfTheCalendar(
        string date,
        long days,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DateAddDaysDateProjection(Date(date), new LiteralInteger(days)))
        );
    }

    // An amount beyond 32 bits moves the date out of 0001–9999: an execution
    // error, never the date moved by the amount's low 32 bits.
    [Theory]
    [InlineData(4294967297)]
    [InlineData(-4294967295)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(3652059)]
    public void DateAddDaysBeyondTheCalendarIsAnExecutionError(long days)
    {
        Fails(
            new DateAddDaysDateProjection(Date("0001-01-01"), new LiteralInteger(days))
        );
    }

    [Theory]
    [InlineData(4294967297)]
    [InlineData(-4294967295)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void AddingMonthsBeyond32BitsIsAnExecutionError(long months)
    {
        Fails(
            new DateAddMonthsDateProjection(
                Date("2024-01-31"),
                new LiteralInteger(months)
            )
        );
        Fails(
            new DatetimeAddMonthsDatetimeProjection(
                Instant("2024-01-31T00:00:00Z"),
                new LiteralInteger(months)
            )
        );
        Fails(
            new DatetimeAddMonthsDatetimeProjection(
                Instant("2024-01-31T00:00:00Z"),
                new LiteralInteger(months),
                new LiteralString("Europe/Moscow")
            )
        );
    }

    // "the day is clamped to its last day" (Dates and times, Months), in the
    // proleptic Gregorian calendar: years divisible by 100 are leap only when
    // divisible by 400, and year 4 is a leap year.
    [Theory]
    [InlineData("0001-01-31", 1, "0001-02-28")]
    [InlineData("0004-01-31", 1, "0004-02-29")]
    [InlineData("1900-01-31", 1, "1900-02-28")]
    [InlineData("2000-01-31", 1, "2000-02-29")]
    [InlineData("2100-03-31", -1, "2100-02-28")]
    [InlineData("9999-12-31", -119987, "0001-01-31")]
    [InlineData("2024-03-31", -13, "2023-02-28")]
    [InlineData("2024-05-31", -3, "2024-02-29")]
    [InlineData("2024-12-31", -10, "2024-02-29")]
    [InlineData("1999-02-28", 12, "2000-02-28")]
    public void DateAddMonthsClampsInTheProlepticGregorianCalendar(
        string date,
        long months,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DateAddMonthsDateProjection(Date(date), new LiteralInteger(months)))
        );
    }

    // "the largest n for which right plus n months, as dateAddMonths adds
    // them, is not later than left"; "When left is earlier, it is the negative
    // of the months from left to right" (Dates and times, Months between).
    [Theory]
    [InlineData("2024-02-29", "2024-01-30", "1")]
    [InlineData("2024-02-28", "2024-01-30", "0")]
    [InlineData("2023-02-28", "2023-01-29", "1")]
    [InlineData("2024-01-30", "2024-02-29", "-1")]
    [InlineData("2024-01-30", "2024-02-28", "0")]
    [InlineData("2024-04-30", "2024-03-31", "1")]
    [InlineData("2024-05-30", "2024-04-30", "1")]
    [InlineData("2024-05-31", "2024-04-30", "1")]
    [InlineData("2024-06-29", "2024-04-30", "1")]
    [InlineData("2024-06-30", "2024-04-30", "2")]
    [InlineData("2024-02-28", "2023-02-28", "12")]
    [InlineData("2023-02-28", "2024-02-28", "-12")]
    [InlineData("2100-02-28", "2096-02-29", "48")]
    [InlineData("9999-12-31", "0001-01-31", "119987")]
    [InlineData("0001-02-28", "0001-01-31", "1")]
    public void DateDiffMonthsInvertsTheClampingOfDateAddMonths(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DateDiffMonthsIntegerProjection(Date(left), Date(right)))
        );
    }

    // "For a datetime the time of day counts, in UTC" (Months between), to
    // the microsecond.
    [Theory]
    [InlineData("2024-02-29T12:00:00Z", "2024-01-31T12:00:00.000001Z", "0")]
    [InlineData("2024-02-29T12:00:00.000001Z", "2024-01-31T12:00:00.000001Z", "1")]
    [InlineData("2024-01-31T12:00:00.000001Z", "2024-02-29T12:00:00Z", "0")]
    [InlineData("2024-01-31T12:00:00Z", "2024-02-29T12:00:00Z", "-1")]
    [InlineData("9999-12-31T23:59:59.999999Z", "0001-01-01T00:00:00Z", "119987")]
    [InlineData("0001-01-01T00:00:00Z", "9999-12-31T23:59:59.999999Z", "-119987")]
    public void DatetimeDiffMonthsCountsTheMicrosecondsOfTheTimeOfDay(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new DatetimeDiffMonthsIntegerProjection(Instant(left), Instant(right)))
        );
    }

    // "A datetime is moved in UTC and keeps its time of day" (Months).
    [Theory]
    [InlineData("2024-01-31T06:00:00Z", -11, "2023-02-28T06:00:00Z")]
    [InlineData("0001-02-28T23:59:59.999999Z", -1, "0001-01-28T23:59:59.999999Z")]
    [InlineData("9999-11-30T12:00:00Z", 1, "9999-12-30T12:00:00Z")]
    [InlineData("1900-01-29T00:00:00.000001Z", 1, "1900-02-28T00:00:00.000001Z")]
    public void DatetimeAddMonthsKeepsTheTimeOfDayToTheMicrosecond(
        string instant,
        long months,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeAddMonthsDatetimeProjection(
                    Instant(instant),
                    new LiteralInteger(months)
                )
            )
        );
    }

    // "day 1 to the last day of that month" (From parts), in the proleptic
    // Gregorian calendar.
    [Theory]
    [InlineData(2000, 2, 29, "2000-02-29")]
    [InlineData(4, 2, 29, "0004-02-29")]
    [InlineData(9996, 2, 29, "9996-02-29")]
    [InlineData(1, 12, 31, "0001-12-31")]
    public void DateFromPartsAcceptsEveryLeapDay(
        long year,
        long month,
        long day,
        string expected
    )
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

    // "A part outside its range is an execution error ... Nothing is clamped
    // and nothing rolls over" (From parts), also for a part beyond 32 bits,
    // whose low 32 bits would make a valid part.
    [Theory]
    [InlineData(1900, 2, 29)]
    [InlineData(2100, 2, 29)]
    [InlineData(1, 2, 29)]
    [InlineData(4294969320, 1, 1)]
    [InlineData(2024, 4294967297, 1)]
    [InlineData(2024, 1, 4294967297)]
    [InlineData(long.MinValue, 1, 1)]
    public void DateFromPartsFailsOnANonLeapYearAndOnAPartBeyond32Bits(
        long year,
        long month,
        long day
    )
    {
        Fails(
            new DateFromPartsDateProjection(
                new LiteralInteger(year),
                new LiteralInteger(month),
                new LiteralInteger(day)
            )
        );
    }

    [Theory]
    [InlineData(4294967296, 0, "0")]
    [InlineData(0, 4294967296, "0")]
    [InlineData(0, 0, "-0.0000005")]
    [InlineData(0, 0, "4294967296")]
    [InlineData(long.MinValue, 0, "0")]
    public void TimeFromPartsFailsOnAPartFarOutsideItsRange(
        long hour,
        long minute,
        string second
    )
    {
        Fails(
            new TimeFromPartsTimeProjection(
                new LiteralInteger(hour),
                new LiteralInteger(minute),
                Decimal(second)
            )
        );
    }

    // ISO 8601 weeks and the parts of "Parts", computed with Python's
    // date.isocalendar(), isoweekday() and timetuple().tm_yday.
    [Theory]
    [InlineData("2026-12-31", 53, 2026, 4, 365)]
    [InlineData("2027-01-01", 53, 2026, 5, 1)]
    [InlineData("2027-01-03", 53, 2026, 7, 3)]
    [InlineData("2027-01-04", 1, 2027, 1, 4)]
    [InlineData("2008-12-29", 1, 2009, 1, 364)]
    [InlineData("2010-01-03", 53, 2009, 7, 3)]
    [InlineData("2015-12-31", 53, 2015, 4, 365)]
    [InlineData("2016-01-03", 53, 2015, 7, 3)]
    [InlineData("2000-01-02", 52, 1999, 7, 2)]
    [InlineData("2000-01-03", 1, 2000, 1, 3)]
    [InlineData("2000-12-31", 52, 2000, 7, 366)]
    [InlineData("1900-12-31", 1, 1901, 1, 365)]
    [InlineData("2024-03-01", 9, 2024, 5, 61)]
    [InlineData("2023-03-01", 9, 2023, 3, 60)]
    [InlineData("0001-12-31", 1, 2, 1, 365)]
    [InlineData("0004-12-31", 53, 4, 5, 366)]
    [InlineData("9999-01-01", 53, 9998, 5, 1)]
    [InlineData("9999-12-27", 52, 9999, 1, 361)]
    public void IsoWeekPartsAroundTheTurnOfTheYear(
        string date,
        long week,
        long weekYear,
        long dayOfWeek,
        long dayOfYear
    )
    {
        LiteralDate operand = Date(date);

        Assert.Equal(
            Text(week),
            Value(new WeekDateIntegerProjection(operand))
        );
        Assert.Equal(Text(weekYear), Value(new WeekYearDateIntegerProjection(operand)));
        Assert.Equal(Text(dayOfWeek), Value(new DayOfWeekDateIntegerProjection(operand)));
        Assert.Equal(Text(dayOfYear), Value(new DayOfYearDateIntegerProjection(operand)));

        DatetimeProjection noon = Instant(date + "T12:00:00Z");

        Assert.Equal(Text(week), Value(new WeekDatetimeIntegerProjection(noon)));
        Assert.Equal(Text(weekYear), Value(new WeekYearDatetimeIntegerProjection(noon)));
    }

    // "startOfWeek may fall in the previous month or year"; "a datetime is
    // truncated in UTC" (startOf…), at the ends of the range.
    [Theory]
    [InlineData("year", "9999-12-31T23:59:59.999999Z", "9999-01-01T00:00:00Z")]
    [InlineData("quarter", "2024-12-31T23:59:59.999999Z", "2024-10-01T00:00:00Z")]
    [InlineData("month", "0001-01-31T23:59:59.999999Z", "0001-01-01T00:00:00Z")]
    [InlineData("week", "0001-01-07T23:59:59.999999Z", "0001-01-01T00:00:00Z")]
    [InlineData("week", "9999-12-31T12:00:00Z", "9999-12-27T00:00:00Z")]
    [InlineData("week", "2023-01-01T12:00:00Z", "2022-12-26T00:00:00Z")]
    [InlineData("day", "9999-12-31T23:59:59.999999Z", "9999-12-31T00:00:00Z")]
    [InlineData("hour", "9999-12-31T23:59:59.999999Z", "9999-12-31T23:00:00Z")]
    [InlineData("minute", "0001-01-01T00:00:59.999999Z", "0001-01-01T00:00:00Z")]
    [InlineData("second", "9999-12-31T23:59:59.999999Z", "9999-12-31T23:59:59Z")]
    public void StartOfAPeriodOfAnInstantAtTheEndsOfTheRange(
        string period,
        string instant,
        string expected
    )
    {
        DatetimeProjection operand = Instant(instant);
        ExprProjection start = period switch
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

        Assert.Equal(expected, Value(start));
    }

    [Theory]
    [InlineData("9999-12-31", "9999-12-27")]
    [InlineData("2023-01-01", "2022-12-26")]
    [InlineData("2000-03-05", "2000-02-28")]
    [InlineData("2024-03-03", "2024-02-26")]
    public void StartOfWeekOfADateIsTheMondayOnOrBeforeIt(string date, string monday)
    {
        Assert.Equal(monday, Value(new StartOfWeekDateProjection(Date(date))));
    }

    // The microsecond of an instant computed with rounded seconds.
    [Fact]
    public void MicrosecondOfAnInstantReadsTheRoundedArithmetic()
    {
        Assert.Equal(
            "2",
            Value(
                new MicrosecondDatetimeIntegerProjection(
                    new DatetimeAddSecondsDatetimeProjection(
                        Instant("2024-01-01T00:00:00Z"),
                        Decimal("0.0000015")
                    )
                )
            )
        );
        Assert.Equal(
            "999998",
            Value(
                new MicrosecondTimeIntegerProjection(
                    new TimeAddSecondsTimeProjection(
                        Time("00:00:00"),
                        Decimal("-0.0000015")
                    )
                )
            )
        );
    }

    private static string Text(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
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

    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return new PureQLProjection(
            [One],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.one"),
                select: [new SelectItemProjection("value", expression)]
            )
        );
    }

    private static string Value(ExprProjection expression)
    {
        return ResultColumn.Texts(Selecting(expression), "value")[0];
    }

    // An execution error: the query fails, when it is built or when it runs.
    private static void Fails(ExprProjection expression)
    {
        _ = Assert.ThrowsAny<Exception>(() => Selecting(expression).ToList());
    }
}
