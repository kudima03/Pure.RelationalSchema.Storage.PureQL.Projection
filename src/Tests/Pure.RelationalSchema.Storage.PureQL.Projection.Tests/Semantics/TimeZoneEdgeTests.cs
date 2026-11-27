using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The edges of "Time zones" that TimeZoneTests leaves out: offsets that are
// not whole minutes (Africa/Monrovia until 1972, local mean time), clocks
// that change by half an hour (Australia/Lord_Howe) or at an offset of 45
// minutes (Pacific/Chatham), clocks set back and forward at midnight
// (America/Santiago), a zone that skipped a quarter of an hour
// (Asia/Kathmandu in 1986) or a whole day (Pacific/Apia), the southern
// hemisphere, the ends of the range of instants in the fixed offsets, and
// months moved over a repeated hour. Every expected value comes from the
// rules of the IANA database as Python's zoneinfo and zdump read them
// (unchanged between releases 2026e and 2026f), and the rules of the
// specification: the local time is the instant shifted by the offset, and a
// local date and time gives "the first instant at which the local date and
// time are the given ones or later".
public sealed class TimeZoneEdgeTests
{
    private const string Monrovia = "Africa/Monrovia";

    private const string LordHowe = "Australia/Lord_Howe";

    private const string Chatham = "Pacific/Chatham";

    private const string Santiago = "America/Santiago";

    private const string Apia = "Pacific/Apia";

    private const string NewYork = "America/New_York";

    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    // "an offset from UTC is a whole number of seconds" (Time zones,
    // Operators): Africa/Monrovia was 0:44:30 behind UTC until 1972-01-07,
    // so 1970-01-01T00:00:00Z reads 23:15:30 on 1969-12-31 there.
    [Theory]
    [InlineData("asDate", "1969-12-31")]
    [InlineData("hour", "23")]
    [InlineData("day", "31")]
    public void OffsetOfSecondsReadsTheLocalDayAndHour(string reading, string expected)
    {
        Assert.Equal(
            expected,
            Value(Reading(reading, Instant("1970-01-01T00:00:00Z"), Monrovia))
        );
    }

    [Theory(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds (Africa/Monrovia "
            + "-0:44:30 until 1972; LMT); the library reads offsets through "
            + "TimeZoneInfo, which rounds them to whole minutes"
    )]
    [InlineData("asTime", "23:15:30")]
    [InlineData("minute", "15")]
    [InlineData("second", "30")]
    [InlineData("startOfMinute", "1969-12-31T23:59:30Z")]
    [InlineData("startOfHour", "1969-12-31T23:44:30Z")]
    [InlineData("startOfDay", "1969-12-31T00:44:30Z")]
    [InlineData("startOfMonth", "1969-12-01T00:44:30Z")]
    public void OffsetOfSecondsShiftsTheLocalTimeBySeconds(
        string reading,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(Reading(reading, Instant("1970-01-01T00:00:00Z"), Monrovia))
        );
    }

    [Theory]
    [InlineData("1972-01-07", "00:44:29", "1972-01-07T00:44:30Z")]
    [InlineData("1972-01-07", "00:44:30", "1972-01-07T00:44:30Z")]
    [InlineData("1972-01-07", "00:44:31", "1972-01-07T00:44:31Z")]
    public void LocalTimeAfterAChangeFromAnOffsetOfSecondsGivesItsInstant(
        string date,
        string time,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date(date),
                    Time(time),
                    new LiteralString(Monrovia)
                )
            )
        );
    }

    [Theory(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds (Africa/Monrovia "
            + "-0:44:30 until 1972; LMT); the library reads offsets through "
            + "TimeZoneInfo, which rounds them to whole minutes"
    )]
    [InlineData("1970-01-01", "00:00:00", "1970-01-01T00:44:30Z")]
    [InlineData("1972-01-06", "23:59:59", "1972-01-07T00:44:29Z")]
    [InlineData("1972-01-07", "00:00:00", "1972-01-07T00:44:30Z")]
    public void LocalTimeInAnOffsetOfSecondsGivesItsInstant(
        string date,
        string time,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date(date),
                    Time(time),
                    new LiteralString(Monrovia)
                )
            )
        );
    }

    [Fact(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds (Africa/Monrovia "
            + "-0:44:30 until 1972; LMT); the library reads offsets through "
            + "TimeZoneInfo, which rounds them to whole minutes"
    )]
    public void LocalDayThatStartedLateInAnOffsetOfSecondsStartsAtItsFirstInstant()
    {
        Assert.Equal(
            "1972-01-07T00:44:30Z",
            Value(
                new AsDatetimeDateDatetimeProjection(
                    Date("1972-01-07"),
                    new LiteralString(Monrovia)
                )
            )
        );
    }

    // Local mean time, the offset of a zone before standard time, is a
    // number of seconds too.
    [Theory(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds (Africa/Monrovia "
            + "-0:44:30 until 1972; LMT); the library reads offsets through "
            + "TimeZoneInfo, which rounds them to whole minutes"
    )]
    [InlineData("1880-01-01T00:00:00Z", NewYork, "1879-12-31", "19:03:58")]
    [InlineData("1900-01-01T00:00:00Z", "Europe/Moscow", "1900-01-01", "02:30:17")]
    [InlineData("1900-01-01T00:00:00Z", "Asia/Kolkata", "1900-01-01", "05:21:10")]
    [InlineData("1900-01-01T00:00:00Z", "Europe/Paris", "1900-01-01", "00:09:21")]
    public void LocalMeanTimeIsAnOffsetOfSeconds(
        string instant,
        string zone,
        string day,
        string time
    )
    {
        Assert.Equal(day, Value(Reading("asDate", Instant(instant), zone)));
        Assert.Equal(time, Value(Reading("asTime", Instant(instant), zone)));
    }

    // Australia/Lord_Howe sets its clocks back from 02:00 to 01:30 on
    // 2024-04-07 (+11 to +10:30) and forward from 02:00 to 02:30 on
    // 2024-10-06.
    [Theory]
    [InlineData("2024-04-06T14:45:00Z", "asTime", "01:45:00")]
    [InlineData("2024-04-06T15:15:00Z", "asTime", "01:45:00")]
    [InlineData("2024-04-06T15:15:00Z", "startOfHour", "2024-04-06T14:00:00Z")]
    [InlineData("2024-04-06T14:45:00Z", "startOfHour", "2024-04-06T14:00:00Z")]
    [InlineData("2024-04-06T15:30:00Z", "startOfHour", "2024-04-06T15:30:00Z")]
    [InlineData("2024-04-06T15:15:00Z", "startOfMinute", "2024-04-06T14:45:00Z")]
    [InlineData("2024-10-05T15:30:00Z", "asTime", "02:30:00")]
    [InlineData("2024-10-05T15:29:59Z", "asTime", "01:59:59")]
    [InlineData("2024-10-05T15:30:00Z", "startOfHour", "2024-10-05T15:30:00Z")]
    [InlineData("2024-10-05T15:45:00Z", "startOfHour", "2024-10-05T15:30:00Z")]
    [InlineData("2024-04-06T20:00:00Z", "startOfDay", "2024-04-06T13:00:00Z")]
    public void HalfHourChangeOfTheClocksInLordHowe(
        string instant,
        string reading,
        string expected
    )
    {
        Assert.Equal(expected, Value(Reading(reading, Instant(instant), LordHowe)));
    }

    [Theory]
    [InlineData(LordHowe, "2024-04-07", "01:45:00", "2024-04-06T14:45:00Z")]
    [InlineData(LordHowe, "2024-04-07", "01:29:59", "2024-04-06T14:29:59Z")]
    [InlineData(LordHowe, "2024-04-07", "02:00:00", "2024-04-06T15:30:00Z")]
    [InlineData(LordHowe, "2024-10-06", "02:15:00", "2024-10-05T15:30:00Z")]
    [InlineData(LordHowe, "2024-10-06", "02:30:00", "2024-10-05T15:30:00Z")]
    [InlineData(Chatham, "2024-04-07", "03:00:00", "2024-04-06T13:15:00Z")]
    [InlineData(Chatham, "2024-09-29", "03:00:00", "2024-09-28T14:00:00Z")]
    [InlineData(Chatham, "2024-09-29", "03:45:00", "2024-09-28T14:00:00Z")]
    [InlineData("Australia/Sydney", "2024-04-07", "02:30:00", "2024-04-06T15:30:00Z")]
    [InlineData("Australia/Sydney", "2024-10-06", "02:30:00", "2024-10-05T16:00:00Z")]
    [InlineData("Europe/Berlin", "2024-03-31", "02:30:00", "2024-03-31T01:00:00Z")]
    [InlineData("Europe/Berlin", "2024-10-27", "02:30:00", "2024-10-27T00:30:00Z")]
    [InlineData(Santiago, "2024-04-06", "23:30:00", "2024-04-07T02:30:00Z")]
    [InlineData(Santiago, "2024-09-08", "00:30:00", "2024-09-08T04:00:00Z")]
    [InlineData("Asia/Kathmandu", "1986-01-01", "00:05:00", "1985-12-31T18:30:00Z")]
    [InlineData("Asia/Kathmandu", "1986-01-01", "00:15:00", "1985-12-31T18:30:00Z")]
    [InlineData("Asia/Kathmandu", "1985-12-31", "23:59:59", "1985-12-31T18:29:59Z")]
    [InlineData("Etc/GMT+5", "2024-03-10", "02:30:00", "2024-03-10T07:30:00Z")]
    public void LocalTimeIsTheFirstInstantThatReadsItOrLater(
        string zone,
        string date,
        string time,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date(date),
                    Time(time),
                    new LiteralString(zone)
                )
            )
        );
    }

    // Pacific/Chatham sets its clocks back from 03:45 to 02:45 on 2024-04-07:
    // 03:15 of the second pass is in the local hour that began at 03:00 of
    // the first.
    [Fact]
    public void StartOfARepeatedHourAt45MinutesIsItsFirstPass()
    {
        Assert.Equal(
            "2024-04-06T13:15:00Z",
            Value(Reading("startOfHour", Instant("2024-04-06T14:30:00Z"), Chatham))
        );
    }

    // America/Santiago sets its clocks back from 24:00 to 23:00 on 2024-04-06
    // and forward from 24:00 to 01:00 on 2024-09-07: "Where clocks are set
    // forward at midnight, the day starts at the first time that exists",
    // and the hour before a midnight set back passes twice.
    [Theory]
    [InlineData("2024-04-07T03:30:00Z", "asDate", "2024-04-06")]
    [InlineData("2024-04-07T03:30:00Z", "asTime", "23:30:00")]
    [InlineData("2024-04-07T03:30:00Z", "startOfDay", "2024-04-06T03:00:00Z")]
    [InlineData("2024-04-07T03:30:00Z", "startOfHour", "2024-04-07T02:00:00Z")]
    [InlineData("2024-04-07T04:00:00Z", "startOfDay", "2024-04-07T04:00:00Z")]
    [InlineData("2024-09-08T12:00:00Z", "startOfDay", "2024-09-08T04:00:00Z")]
    [InlineData("2024-09-08T03:59:59Z", "asDate", "2024-09-07")]
    [InlineData("2024-09-08T04:00:00Z", "asTime", "01:00:00")]
    public void ClocksChangedAtMidnightInSantiago(
        string instant,
        string reading,
        string expected
    )
    {
        Assert.Equal(expected, Value(Reading(reading, Instant(instant), Santiago)));
    }

    // America/Havana sets its clocks back from 01:00 to 00:00 on 2024-11-03,
    // so that midnight occurs twice: the day starts at the first pass, and an
    // instant of the second pass is in the same local day and hour.
    [Theory]
    [InlineData("2024-11-03T05:30:00Z", "asTime", "00:30:00")]
    [InlineData("2024-11-03T05:30:00Z", "startOfDay", "2024-11-03T04:00:00Z")]
    [InlineData("2024-11-03T05:30:00Z", "startOfHour", "2024-11-03T04:00:00Z")]
    [InlineData("2024-11-03T05:30:00Z", "startOfMinute", "2024-11-03T04:30:00Z")]
    [InlineData("2024-11-03T03:59:59Z", "asDate", "2024-11-02")]
    [InlineData("2024-03-10T05:00:00Z", "asTime", "01:00:00")]
    [InlineData("2024-03-10T12:00:00Z", "startOfDay", "2024-03-10T05:00:00Z")]
    public void MidnightThatOccursTwiceStartsTheDayAtItsFirstPass(
        string instant,
        string reading,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(Reading(reading, Instant(instant), "America/Havana"))
        );
    }

    // "The rows of a local day are those from asDatetime(day, zone) up to but
    // excluding asDatetime(dateAddDays(day, 1), zone)": the lengths of local
    // days in seconds.
    [Theory]
    [InlineData(Santiago, "2024-04-06", "90000")]
    [InlineData(Santiago, "2024-09-07", "86400")]
    [InlineData(Santiago, "2024-09-08", "82800")]
    [InlineData(LordHowe, "2024-04-07", "88200")]
    [InlineData(LordHowe, "2024-10-06", "84600")]
    [InlineData(Chatham, "2024-04-07", "90000")]
    [InlineData("Asia/Kathmandu", "1986-01-01", "85500")]
    [InlineData(Apia, "2011-12-29", "86400")]
    [InlineData("America/Havana", "2024-11-02", "86400")]
    [InlineData("America/Havana", "2024-11-03", "90000")]
    [InlineData("America/Havana", "2024-03-10", "82800")]
    public void LocalDayLastsFromItsStartToTheStartOfTheNextDay(
        string zone,
        string day,
        string seconds
    )
    {
        LiteralDate date = Date(day);

        Assert.Equal(
            seconds,
            Value(
                new DatetimeDiffSecondsDecimalProjection(
                    new AsDatetimeDateDatetimeProjection(
                        new DateAddDaysDateProjection(date, new LiteralInteger(1)),
                        new LiteralString(zone)
                    ),
                    new AsDatetimeDateDatetimeProjection(date, new LiteralString(zone))
                )
            )
        );
    }

    // Africa/Monrovia moved from -0:44:30 to UTC at 1972-01-07T00:44:30Z,
    // when its clocks read 1972-01-06 23:59:59 + 1 s: the local day
    // 1972-01-07 lasted 86400 - 2670 seconds.
    [Fact(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds (Africa/Monrovia "
            + "-0:44:30 until 1972; LMT); the library reads offsets through "
            + "TimeZoneInfo, which rounds them to whole minutes"
    )]
    public void LocalDayThatAnOffsetOfSecondsShortenedLastsItsSeconds()
    {
        LiteralDate date = Date("1972-01-07");

        Assert.Equal(
            "83730",
            Value(
                new DatetimeDiffSecondsDecimalProjection(
                    new AsDatetimeDateDatetimeProjection(
                        new DateAddDaysDateProjection(date, new LiteralInteger(1)),
                        new LiteralString(Monrovia)
                    ),
                    new AsDatetimeDateDatetimeProjection(
                        date,
                        new LiteralString(Monrovia)
                    )
                )
            )
        );
    }

    // Pacific/Apia skipped 2011-12-30: 2011-12-29 23:59:59 at -10 is followed
    // by 2011-12-31 00:00:00 at +14.
    [Theory]
    [InlineData("2011-12-30T09:59:59Z", "asDate", "2011-12-29")]
    [InlineData("2011-12-30T10:00:00Z", "asDate", "2011-12-31")]
    [InlineData("2011-12-30T10:00:00Z", "dayOfYear", "365")]
    [InlineData("2011-12-30T10:00:00Z", "dayOfWeek", "6")]
    [InlineData("2011-12-30T12:00:00Z", "startOfMonth", "2011-12-01T10:00:00Z")]
    [InlineData("2011-12-30T12:00:00Z", "startOfWeek", "2011-12-26T10:00:00Z")]
    [InlineData("2011-12-30T12:00:00Z", "startOfDay", "2011-12-30T10:00:00Z")]
    [InlineData("2011-12-30T09:59:59Z", "startOfDay", "2011-12-29T10:00:00Z")]
    public void DayThatApiaSkippedIsNoLocalDay(
        string instant,
        string reading,
        string expected
    )
    {
        Assert.Equal(expected, Value(Reading(reading, Instant(instant), Apia)));
    }

    // "datetimeAddMonths with a zone ... gives the instant of that local date
    // and time by the rule above": into the day Apia skipped, and over the
    // hour New York repeats on 2024-11-03 (01:00 to 02:00, EDT then EST).
    [Theory]
    [InlineData("2011-11-30T10:00:00Z", 1, Apia, "2011-12-30T10:00:00Z")]
    [InlineData("2011-11-30T20:00:00Z", 1, Apia, "2011-12-30T10:00:00Z")]
    [InlineData("2024-10-03T05:30:00Z", 1, NewYork, "2024-11-03T05:30:00Z")]
    [InlineData("2024-11-03T06:30:00Z", 1, NewYork, "2024-12-03T06:30:00Z")]
    [InlineData("2024-11-03T06:30:00Z", -1, NewYork, "2024-10-03T05:30:00Z")]
    [InlineData("2024-11-03T05:30:00Z", -1, NewYork, "2024-10-03T05:30:00Z")]
    [InlineData("2024-03-31T01:30:00Z", 1, LordHowe, "2024-04-30T02:00:00Z")]
    [InlineData("0001-02-01T00:00:00Z", -1, "Etc/GMT-14", "0001-01-01T00:00:00Z")]
    [InlineData("9999-11-30T23:00:00Z", 1, "Etc/GMT+12", "9999-12-30T23:00:00Z")]
    public void DatetimeAddMonthsWithAZoneResolvesTheLocalTimeByTheRule(
        string instant,
        long months,
        string zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeAddMonthsDatetimeProjection(
                    Instant(instant),
                    new LiteralInteger(months),
                    new LiteralString(zone)
                )
            )
        );
    }

    // "datetimeDiffMonths with a zone is its inverse: the whole months from
    // right to left as datetimeAddMonths adds them in that zone".
    [Theory]
    [InlineData("2011-12-30T10:00:00Z", "2011-11-30T10:00:00Z", Apia, "1")]
    [InlineData("2011-12-30T09:59:59Z", "2011-11-30T10:00:00Z", Apia, "0")]
    [InlineData("2024-11-03T06:30:00Z", "2024-10-03T05:30:00Z", NewYork, "1")]
    [InlineData("2024-11-03T06:15:00Z", "2024-10-03T05:30:00Z", NewYork, "1")]
    [InlineData("2024-11-03T05:29:59Z", "2024-10-03T05:30:00Z", NewYork, "0")]
    [InlineData("2024-10-03T05:30:00Z", "2024-11-03T06:15:00Z", NewYork, "-1")]
    [InlineData("9999-12-31T00:00:00Z", "0001-01-01T12:00:00Z", "Etc/GMT-14", "119987")]
    [InlineData("0001-01-01T12:00:00Z", "9999-12-31T00:00:00Z", "Etc/GMT-14", "-119987")]
    public void DatetimeDiffMonthsWithAZoneInvertsDatetimeAddMonths(
        string left,
        string right,
        string zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeDiffMonthsIntegerProjection(
                    Instant(left),
                    Instant(right),
                    new LiteralString(zone)
                )
            )
        );
    }

    // 2024-12-29T23:30:00Z is Monday 2024-12-30 02:30 in Moscow, in ISO week
    // 1 of 2025, and Sunday of week 52 of 2024 in UTC.
    [Theory]
    [InlineData("week", "1", "52")]
    [InlineData("weekYear", "2025", "2024")]
    [InlineData("dayOfWeek", "1", "7")]
    [InlineData("year", "2024", "2024")]
    [InlineData("dayOfYear", "365", "364")]
    [InlineData("startOfWeek", "2024-12-29T21:00:00Z", "2024-12-23T00:00:00Z")]
    public void LocalWeekCrossesTheTurnOfTheYear(string reading, string local, string utc)
    {
        LiteralDatetime instant = Instant("2024-12-29T23:30:00Z");

        Assert.Equal(local, Value(Reading(reading, instant, "Europe/Moscow")));
        Assert.Equal(utc, Value(Reading(reading, instant, null)));
    }

    // A fixed offset has no daylight saving time: Etc/GMT+5 is always five
    // hours behind UTC.
    [Theory]
    [InlineData("2024-07-01T03:00:00Z", "startOfDay", "2024-06-30T05:00:00Z")]
    [InlineData("2024-01-01T03:00:00Z", "startOfYear", "2023-01-01T05:00:00Z")]
    [InlineData("2024-07-01T03:00:00Z", "asTime", "22:00:00")]
    [InlineData("2024-07-01T03:00:00Z", "asDate", "2024-06-30")]
    public void FixedOffsetBehindUtcReadsTheDayBefore(
        string instant,
        string reading,
        string expected
    )
    {
        Assert.Equal(expected, Value(Reading(reading, Instant(instant), "Etc/GMT+5")));
    }

    // "A result outside the years 0001–9999 is an execution error, as for
    // every datetime" (Time zones), for the start of a local period and for
    // months moved in a local calendar.
    [Theory]
    [InlineData("startOfYear", "0001-06-01T00:00:00Z", "Etc/GMT-14")]
    [InlineData("startOfWeek", "0001-01-01T12:00:00Z", "Etc/GMT-14")]
    [InlineData("startOfDay", "0001-01-01T05:00:00Z", "Etc/GMT-1")]
    [InlineData("startOfMonth", "0001-01-15T00:00:00Z", "Etc/GMT-1")]
    public void StartOfALocalPeriodBeforeTheFirstInstantIsAnExecutionError(
        string reading,
        string instant,
        string zone
    )
    {
        Fails(Reading(reading, Instant(instant), zone));
    }

    [Fact]
    public void StartOfALocalDayAtTheLastDayIsInRange()
    {
        Assert.Equal(
            "9999-12-31T12:00:00Z",
            Value(Reading("startOfDay", Instant("9999-12-31T23:00:00Z"), "Etc/GMT+12"))
        );
    }

    [Theory]
    [InlineData("0001-02-01T00:00:00Z", -1, "Etc/GMT+12")]
    [InlineData("9999-12-01T00:00:00Z", 1, "Etc/GMT-14")]
    [InlineData("9999-11-01T00:00:00Z", 2, "Etc/GMT+12")]
    public void MonthsMovedOutOfTheRangeInALocalCalendarAreAnExecutionError(
        string instant,
        long months,
        string zone
    )
    {
        Fails(
            new DatetimeAddMonthsDatetimeProjection(
                Instant(instant),
                new LiteralInteger(months),
                new LiteralString(zone)
            )
        );
    }

    [Theory]
    [InlineData("9999-12-31", "11:59:59.999999", "9999-12-31T23:59:59.999999Z")]
    [InlineData("0001-01-01", "14:00:00", "0001-01-01T00:00:00Z")]
    public void LocalTimeAtTheEndsOfTheRangeGivesAnInstantInRange(
        string date,
        string time,
        string expected
    )
    {
        string zone = date.StartsWith("9999", StringComparison.Ordinal)
            ? "Etc/GMT+12"
            : "Etc/GMT-14";

        Assert.Equal(
            expected,
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date(date),
                    Time(time),
                    new LiteralString(zone)
                )
            )
        );
    }

    [Fact]
    public void LocalTimeAfterTheLastInstantIsAnExecutionError()
    {
        Fails(
            new DatetimeFromPartsDatetimeProjection(
                Date("9999-12-31"),
                Time("12:00:00"),
                new LiteralString("Etc/GMT+12")
            )
        );
    }

    // "a literal zone that is not in the list is rejected"; "computes, for a
    // row, a zone that is not a time zone name" is an execution error: a
    // literal zone that is no zone fails the query.
    [Theory]
    [InlineData("Europe/Atlantis")]
    [InlineData("+03:00")]
    [InlineData("Etc/GMT+13")]
    public void LiteralZoneThatIsNotAZoneNameFailsTheQuery(string zone)
    {
        Fails(
            new AsDateDatetimeDateProjection(
                Instant("2024-01-01T00:00:00Z"),
                new LiteralString(zone)
            )
        );
    }

    // "A zone parameter is bound to a time zone name of that list, or to UTC"
    // (Validation, interpreter): text that names no zone is rejected when the
    // parameter is bound.
    [Theory]
    [InlineData("+03:00")]
    [InlineData("Etc/GMT+13")]
    [InlineData("Etc/GMT-15")]
    [InlineData("UTC+3")]
    public void ZoneParameterThatNamesNoZoneIsRejectedWhenBound(string zone)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                new DatetimeFromPartsDatetimeProjection(
                    Date("2024-01-01"),
                    Time("00:00:00"),
                    new ParamString("zone")
                ),
                new Dictionary<string, IParameter>
                {
                    ["zone"] = new StringParameter(new String(zone)),
                }
            )
        );
    }

    // "so is UTC, which reads the instant as no zone does": also for an
    // instant at the ends of the range, where no shift is possible.
    [Theory]
    [InlineData("asDate", "9999-12-31T23:59:59.999999Z", "9999-12-31")]
    [InlineData("startOfWeek", "0001-01-03T00:00:00Z", "0001-01-01T00:00:00Z")]
    [InlineData("startOfYear", "9999-12-31T23:59:59.999999Z", "9999-01-01T00:00:00Z")]
    public void UtcParameterReadsTheEndsOfTheRangeAsNoZoneDoes(
        string reading,
        string instant,
        string expected
    )
    {
        PureQLProjection projection = Selecting(
            Zoned(reading, Instant(instant), new ParamString("zone")),
            new Dictionary<string, IParameter>
            {
                ["zone"] = new StringParameter(new String("UTC")),
            }
        );

        Assert.Equal(expected, ResultColumn.Texts(projection, "value")[0]);
    }

    private static ExprProjection Reading(
        string reading,
        DatetimeProjection at,
        string? zone
    )
    {
        return zone is null
            ? Zoned(reading, at, null)
            : Zoned(reading, at, new LiteralString(zone));
    }

    private static ExprProjection Zoned(
        string reading,
        DatetimeProjection at,
        StringProjection? zone
    )
    {
        return reading switch
        {
            "asDate" => new AsDateDatetimeDateProjection(at, zone),
            "asTime" => new AsTimeDatetimeTimeProjection(at, zone),
            "year" => new YearDatetimeIntegerProjection(at, zone),
            "day" => new DayDatetimeIntegerProjection(at, zone),
            "dayOfWeek" => new DayOfWeekDatetimeIntegerProjection(at, zone),
            "dayOfYear" => new DayOfYearDatetimeIntegerProjection(at, zone),
            "week" => new WeekDatetimeIntegerProjection(at, zone),
            "weekYear" => new WeekYearDatetimeIntegerProjection(at, zone),
            "hour" => new HourDatetimeIntegerProjection(at, zone),
            "minute" => new MinuteDatetimeIntegerProjection(at, zone),
            "second" => new SecondDatetimeIntegerProjection(at, zone),
            "startOfYear" => new StartOfYearDatetimeProjection(at, zone),
            "startOfMonth" => new StartOfMonthDatetimeProjection(at, zone),
            "startOfWeek" => new StartOfWeekDatetimeProjection(at, zone),
            "startOfDay" => new StartOfDayDatetimeProjection(at, zone),
            "startOfHour" => new StartOfHourDatetimeProjection(at, zone),
            "startOfMinute" => new StartOfMinuteDatetimeProjection(at, zone),
            _ => throw new ArgumentException(reading),
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

    private static PureQLProjection Selecting(
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return new PureQLProjection(
            [One],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.one"),
                select: [new SelectItemProjection("value", expression)]
            ),
            parameters ?? []
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
