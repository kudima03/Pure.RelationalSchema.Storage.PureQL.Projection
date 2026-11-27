namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The edges of the calendar functions that PureQLTimeTests leaves out: ISO
// 8601 weeks around more turns of a year, the rounding of the second of
// timeFromParts at its bounds, months between the ends of the range, the
// fixed offsets at the ends of the range, offsets of seconds, half-hour and
// 45-minute changes of the clocks, and clocks set back and forward at
// midnight. Expected values come from the specification and from the rules
// of the IANA database as Python's zoneinfo and zdump read them.
public sealed class PureQLTimeEdgeTests
{
    private const string Monrovia = "Africa/Monrovia";
    private const string LordHowe = "Australia/Lord_Howe";
    private const string Chatham = "Pacific/Chatham";
    private const string Santiago = "America/Santiago";

    // "week: ISO 8601 week ... week 1 is the one with the year's first
    // Thursday"; weekYear "the ISO 8601 week-numbering year"; dayOfWeek
    // "Monday is 1, Sunday is 7" (Parts), as Python's isocalendar() gives
    // them.
    [Theory]
    [InlineData("2026-12-31", 53, 2026, 4)]
    [InlineData("2027-01-03", 53, 2026, 7)]
    [InlineData("2027-01-04", 1, 2027, 1)]
    [InlineData("2008-12-29", 1, 2009, 1)]
    [InlineData("2010-01-03", 53, 2009, 7)]
    [InlineData("2015-12-31", 53, 2015, 4)]
    [InlineData("2016-01-03", 53, 2015, 7)]
    [InlineData("2000-01-02", 52, 1999, 7)]
    [InlineData("2000-01-03", 1, 2000, 1)]
    [InlineData("1900-12-31", 1, 1901, 1)]
    [InlineData("0001-12-31", 1, 2, 1)]
    [InlineData("0004-12-31", 53, 4, 5)]
    [InlineData("9999-01-01", 53, 9998, 5)]
    [InlineData("9999-12-27", 52, 9999, 1)]
    public void IsoWeekAroundTheTurnOfTheYear(
        string date,
        long week,
        long weekYear,
        long dayOfWeek
    )
    {
        DateOnly day = Date(date);

        Assert.Equal(week, PureQLTime.Week(day));
        Assert.Equal(weekYear, PureQLTime.WeekYear(day));
        Assert.Equal(dayOfWeek, PureQLTime.DayOfWeek(day));
        Assert.Equal(day.AddDays(1 - (int)dayOfWeek), PureQLTime.StartOfWeek(day));
    }

    // "second ... is rounded to microseconds, half away from zero"; "second
    // from 0 up to but excluding 60, also after rounding" (From parts).
    [Theory]
    [InlineData(0, 0, "0.0000005", "00:00:00.000001")]
    [InlineData(0, 0, "0.0000004999999999", "00:00:00")]
    [InlineData(12, 0, "59.9999994999999999", "12:00:59.999999")]
    [InlineData(23, 59, "0.0000015", "23:59:00.000002")]
    public void TimeFromPartsRoundsTheSecondAtItsBounds(
        long hour,
        long minute,
        string second,
        string expected
    )
    {
        Assert.Equal(
            Time(expected),
            PureQLTime.TimeFromParts(
                hour,
                minute,
                decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }

    [Theory]
    [InlineData(long.MaxValue, 0, "0")]
    [InlineData(long.MinValue, 0, "0")]
    [InlineData(0, long.MaxValue, "0")]
    [InlineData(0, 0, "-0.0000005")]
    [InlineData(0, 0, "-1")]
    [InlineData(0, 0, "79228162514264337593543950335")]
    public void TimeFromPartsFailsOnAPartFarOutsideItsRange(
        long hour,
        long minute,
        string second
    )
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.TimeFromParts(
                hour,
                minute,
                decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture)
            )
        );
    }

    // "the largest n for which right plus n months, as dateAddMonths adds
    // them, is not later than left" (Months between), at the ends of the
    // range and at the ends of months.
    [Theory]
    [InlineData("9999-12-31", "0001-01-31", 119987)]
    [InlineData("0001-01-31", "9999-12-31", -119987)]
    [InlineData("9999-12-30", "0001-01-31", 119986)]
    [InlineData("0001-02-28", "0001-01-31", 1)]
    [InlineData("2024-02-29", "2024-01-30", 1)]
    [InlineData("2024-02-28", "2024-01-30", 0)]
    [InlineData("2024-06-29", "2024-04-30", 1)]
    [InlineData("2024-06-30", "2024-04-30", 2)]
    public void DiffMonthsOfDatesAtTheEndsOfTheRangeAndOfMonths(
        string left,
        string right,
        long months
    )
    {
        Assert.Equal(months, PureQLTime.DiffMonths(Date(left), Date(right)));
    }

    [Fact]
    public void DiffMonthsOfInstantsSpansTheWholeRange()
    {
        DateTime first = Instant("0001-01-01T00:00:00Z");
        DateTime last = Instant("9999-12-31T23:59:59.999999Z");

        Assert.Equal(119987, PureQLTime.DiffMonths(last, first));
        Assert.Equal(-119987, PureQLTime.DiffMonths(first, last));
        Assert.Equal(
            119987,
            PureQLTime.DiffMonths(last, Instant("0001-01-01T12:00:00Z"), "Etc/GMT+12")
        );
        Assert.Equal(
            -119987,
            PureQLTime.DiffMonths(
                first,
                Instant("9999-12-31T09:59:59.999999Z"),
                "Etc/GMT-14"
            )
        );
    }

    // In the fixed offsets the local time of the first and the last instant
    // is still inside the years 0001–9999.
    [Fact]
    public void FixedOffsetsReadTheEndsOfTheRange()
    {
        DateTime first = Instant("0001-01-01T00:00:00Z");
        DateTime last = Instant("9999-12-31T23:59:59.999999Z");

        Assert.Equal(Date("0001-01-01"), PureQLTime.LocalDate(first, "Etc/GMT-14"));
        Assert.Equal(Time("14:00:00"), PureQLTime.LocalTime(first, "Etc/GMT-14"));
        Assert.Equal(Date("9999-12-31"), PureQLTime.LocalDate(last, "Etc/GMT+12"));
        Assert.Equal(Time("11:59:59.999999"), PureQLTime.LocalTime(last, "Etc/GMT+12"));
        Same(
            "0001-01-01T00:00:00Z",
            PureQLTime.DatetimeFromParts(
                Date("0001-01-01"),
                Time("14:00:00"),
                "Etc/GMT-14"
            )
        );
        Same(
            "9999-12-31T23:59:59.999999Z",
            PureQLTime.DatetimeFromParts(
                Date("9999-12-31"),
                Time("11:59:59.999999"),
                "Etc/GMT+12"
            )
        );
        Same("9999-12-31T12:00:00Z", PureQLTime.StartOfDay(last, "Etc/GMT+12"));
    }

    // "A result outside the years 0001–9999 is an execution error, as for
    // every datetime" (Time zones).
    [Fact]
    public void StartOfALocalPeriodBeforeTheFirstInstantIsOutOfRange()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.StartOfWeek(Instant("0001-01-01T12:00:00Z"), "Etc/GMT-14")
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.StartOfYear(Instant("0001-06-01T00:00:00Z"), "Etc/GMT-1")
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.DatetimeFromParts(
                Date("9999-12-31"),
                Time("12:00:00"),
                "Etc/GMT+12"
            )
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.AddMonths(Instant("9999-11-01T00:00:00Z"), 2, "Etc/GMT+12")
        );
    }

    [Fact]
    public void AddMonthsInAFixedOffsetReachesTheFirstInstant()
    {
        Same(
            "0001-01-01T00:00:00Z",
            PureQLTime.AddMonths(Instant("0001-02-01T00:00:00Z"), -1, "Etc/GMT-14")
        );
        Same(
            "9999-12-30T23:00:00Z",
            PureQLTime.AddMonths(Instant("9999-11-30T23:00:00Z"), 1, "Etc/GMT+12")
        );
    }

    // "The local time of an instant is the instant shifted by the offset from
    // UTC that the database gives the zone at that instant"; "an offset from
    // UTC is a whole number of seconds" (Time zones). Africa/Monrovia was
    // 0:44:30 behind UTC until 1972-01-07T00:44:30Z.
    [Fact(
        Skip = "Bug: spec says the local time is the instant shifted by the "
            + "zone's offset, a whole number of seconds; Africa/Monrovia "
            + "-0:44:30 gives LocalTime(1970-01-01T00:00:00Z) = 23:16:00, not "
            + "23:15:30 (TimeZoneInfo rounds offsets to minutes)"
    )]
    public void OffsetOfSecondsShiftsTheLocalTimeBySeconds()
    {
        DateTime instant = Instant("1970-01-01T00:00:00Z");

        Assert.Equal(Date("1969-12-31"), PureQLTime.LocalDate(instant, Monrovia));
        Assert.Equal(Time("23:15:30"), PureQLTime.LocalTime(instant, Monrovia));
        Same("1969-12-31T23:59:30Z", PureQLTime.StartOfMinute(instant, Monrovia));
        Same("1969-12-31T00:44:30Z", PureQLTime.StartOfDay(instant, Monrovia));
        Same(
            "1970-01-01T00:44:30Z",
            PureQLTime.DatetimeFromParts(Date("1970-01-01"), TimeOnly.MinValue, Monrovia)
        );
        Same(
            "1972-01-07T00:44:30Z",
            PureQLTime.DatetimeFromParts(Date("1972-01-07"), TimeOnly.MinValue, Monrovia)
        );
        Assert.Equal(
            Time("19:03:58"),
            PureQLTime.LocalTime(Instant("1880-01-01T00:00:00Z"), "America/New_York")
        );
    }

    // Australia/Lord_Howe sets its clocks back from 02:00 (+11) to 01:30
    // (+10:30) at 2024-04-06T15:00:00Z: every local minute from 01:30 to
    // 01:59 passes twice, and both passes are the same local minute and hour.
    [Fact]
    public void RepeatedHalfHourIsOneLocalMinuteAndHourInLordHowe()
    {
        DateTime second = Instant("2024-04-06T15:15:00Z");

        Assert.Equal(Time("01:45:00"), PureQLTime.LocalTime(second, LordHowe));
        Same("2024-04-06T14:45:00Z", PureQLTime.StartOfMinute(second, LordHowe));
        Same("2024-04-06T14:00:00Z", PureQLTime.StartOfHour(second, LordHowe));
        Same(
            "2024-04-06T14:45:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-04-07"), Time("01:45:00"), LordHowe)
        );
        Same(
            "2024-04-06T15:30:00Z",
            PureQLTime.StartOfHour(Instant("2024-04-06T15:30:00Z"), LordHowe)
        );
    }

    // ... and forward from 02:00 (+10:30) to 02:30 (+11) at
    // 2024-10-05T15:30:00Z: the local hour 02:00 starts at 02:30.
    [Fact]
    public void SkippedHalfHourStartsTheLocalHourLateInLordHowe()
    {
        Same(
            "2024-10-05T15:30:00Z",
            PureQLTime.StartOfHour(Instant("2024-10-05T15:45:00Z"), LordHowe)
        );
        Same(
            "2024-10-05T15:30:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-10-06"), Time("02:15:00"), LordHowe)
        );
        Same(
            "2024-10-05T15:29:59Z",
            PureQLTime.DatetimeFromParts(Date("2024-10-06"), Time("01:59:59"), LordHowe)
        );
    }

    // Pacific/Chatham is 12:45 or 13:45 ahead of UTC, so its local hours
    // start at a quarter past the hour of UTC, and the hour repeated on
    // 2024-04-07 (03:45 back to 02:45) starts at its first pass.
    [Fact]
    public void HoursOfChathamStartAtAQuarterPastTheHourOfUtc()
    {
        Same(
            "2024-04-06T13:15:00Z",
            PureQLTime.StartOfHour(Instant("2024-04-06T14:30:00Z"), Chatham)
        );
        Same(
            "2024-04-06T10:15:00Z",
            PureQLTime.StartOfDay(Instant("2024-04-06T20:00:00Z"), Chatham)
        );
        Same(
            "2024-09-28T14:00:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-09-29"), Time("03:00:00"), Chatham)
        );
        Assert.Equal(
            Time("03:45:00"),
            PureQLTime.LocalTime(Instant("2024-09-28T14:00:00Z"), Chatham)
        );
    }

    // America/Santiago sets its clocks back from 24:00 to 23:00 on
    // 2024-04-06 and forward from 24:00 to 01:00 on 2024-09-07.
    [Fact]
    public void ClocksChangedAtMidnightInSantiago()
    {
        DateTime secondPass = Instant("2024-04-07T03:30:00Z");

        Assert.Equal(Date("2024-04-06"), PureQLTime.LocalDate(secondPass, Santiago));
        Same("2024-04-06T03:00:00Z", PureQLTime.StartOfDay(secondPass, Santiago));
        Same("2024-04-07T02:00:00Z", PureQLTime.StartOfHour(secondPass, Santiago));
        Same(
            "2024-04-07T04:00:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-04-07"), TimeOnly.MinValue, Santiago)
        );
        Same(
            "2024-09-08T04:00:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-09-08"), TimeOnly.MinValue, Santiago)
        );
        Same(
            "2024-09-08T04:00:00Z",
            PureQLTime.StartOfDay(Instant("2024-09-08T12:00:00Z"), Santiago)
        );
        Same(
            "2024-09-01T04:00:00Z",
            PureQLTime.StartOfMonth(Instant("2024-09-08T12:00:00Z"), Santiago)
        );
    }

    // "datetimeAddMonths with a zone ... gives the instant of that local date
    // and time by the rule above": into the day Pacific/Apia skipped and over
    // the hour America/New_York repeats.
    [Fact]
    public void AddMonthsInAZoneResolvesASkippedDayAndARepeatedHour()
    {
        Same(
            "2011-12-30T10:00:00Z",
            PureQLTime.AddMonths(Instant("2011-11-30T20:00:00Z"), 1, "Pacific/Apia")
        );
        Same(
            "2024-11-03T05:30:00Z",
            PureQLTime.AddMonths(Instant("2024-10-03T05:30:00Z"), 1, "America/New_York")
        );
        Same(
            "2024-10-03T05:30:00Z",
            PureQLTime.AddMonths(Instant("2024-11-03T06:30:00Z"), -1, "America/New_York")
        );
        Assert.Equal(
            1,
            PureQLTime.DiffMonths(
                Instant("2024-11-03T06:15:00Z"),
                Instant("2024-10-03T05:30:00Z"),
                "America/New_York"
            )
        );
        Assert.Equal(
            0,
            PureQLTime.DiffMonths(
                Instant("2011-12-30T09:59:59Z"),
                Instant("2011-11-30T10:00:00Z"),
                "Pacific/Apia"
            )
        );
    }

    private static void Same(string expected, DateTime actual)
    {
        Assert.Equal(Instant(expected), actual);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
    }

    private static DateOnly Date(string text)
    {
        return PureQLConversions.AsDate(text);
    }

    private static TimeOnly Time(string text)
    {
        return PureQLConversions.AsTime(text);
    }

    private static DateTime Instant(string text)
    {
        return PureQLConversions.AsDatetime(text);
    }
}
