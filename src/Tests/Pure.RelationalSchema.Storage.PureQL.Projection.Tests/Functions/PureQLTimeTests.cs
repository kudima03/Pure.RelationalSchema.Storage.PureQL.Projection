namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The calendar functions of "Dates and times" and "Time zones" in the
// specification: every example and table row there, and the edges of each
// rule.
public sealed class PureQLTimeTests
{
    private const string Moscow = "Europe/Moscow";
    private const string NewYork = "America/New_York";
    private const string Kathmandu = "Asia/Kathmandu";

    [Theory]
    [InlineData("2024-01-01", 1)]
    [InlineData("2024-02-29", 1)]
    [InlineData("2024-03-31", 1)]
    [InlineData("2024-04-01", 2)]
    [InlineData("2024-05-15", 2)]
    [InlineData("2024-06-30", 2)]
    [InlineData("2024-07-01", 3)]
    [InlineData("2024-08-15", 3)]
    [InlineData("2024-09-30", 3)]
    [InlineData("2024-10-01", 4)]
    [InlineData("2024-11-15", 4)]
    [InlineData("2024-12-31", 4)]
    public void QuarterCountsFromJanuaryToMarchAsOne(string date, long quarter)
    {
        Assert.Equal(quarter, PureQLTime.Quarter(Date(date)));
    }

    [Theory]
    [InlineData("2024-12-30", 1)]
    [InlineData("2024-12-31", 2)]
    [InlineData("2025-01-01", 3)]
    [InlineData("2025-01-02", 4)]
    [InlineData("2025-01-03", 5)]
    [InlineData("2025-01-04", 6)]
    [InlineData("2024-03-31", 7)]
    [InlineData("0001-01-01", 1)]
    [InlineData("9999-12-31", 5)]
    public void DayOfWeekIsOneOnMondayAndSevenOnSunday(string date, long day)
    {
        Assert.Equal(day, PureQLTime.DayOfWeek(Date(date)));
    }

    [Theory]
    [InlineData("2024-12-30", 1, 2025)]
    [InlineData("2021-01-01", 53, 2020)]
    [InlineData("2020-12-31", 53, 2020)]
    [InlineData("2021-01-03", 53, 2020)]
    [InlineData("2021-01-04", 1, 2021)]
    [InlineData("2024-01-01", 1, 2024)]
    [InlineData("2024-08-15", 33, 2024)]
    [InlineData("2024-12-29", 52, 2024)]
    [InlineData("2026-01-01", 1, 2026)]
    [InlineData("2023-01-01", 52, 2022)]
    [InlineData("0001-01-01", 1, 1)]
    [InlineData("9999-12-31", 52, 9999)]
    public void WeekGoesWithWeekYearAndWeekOneHasTheFirstThursday(
        string date,
        long week,
        long weekYear
    )
    {
        Assert.Equal(week, PureQLTime.Week(Date(date)));
        Assert.Equal(weekYear, PureQLTime.WeekYear(Date(date)));
    }

    [Theory]
    [InlineData("18:30:15.25", 250000)]
    [InlineData("18:30:15", 0)]
    [InlineData("00:00:00.000001", 1)]
    [InlineData("23:59:59.999999", 999999)]
    public void MicrosecondIsTheFractionOfTheSecond(string time, long microsecond)
    {
        Assert.Equal(microsecond, PureQLTime.Microsecond(Time(time)));
    }

    [Fact]
    public void MicrosecondCutsADigitBelowAMicrosecond()
    {
        Assert.Equal(250000, PureQLTime.Microsecond(Time("18:30:15.25").Add(Tick(9))));
    }

    [Fact]
    public void StartOfAPeriodOfADateIsItsFirstDay()
    {
        DateOnly date = Date("2024-08-15");

        Assert.Equal(Date("2024-01-01"), PureQLTime.StartOfYear(date));
        Assert.Equal(Date("2024-07-01"), PureQLTime.StartOfQuarter(date));
        Assert.Equal(Date("2024-08-01"), PureQLTime.StartOfMonth(date));
        Assert.Equal(Date("2024-08-12"), PureQLTime.StartOfWeek(date));
    }

    [Theory]
    [InlineData("2024-01-31", "2024-01-01")]
    [InlineData("2024-02-29", "2024-01-01")]
    [InlineData("2024-03-31", "2024-01-01")]
    [InlineData("2024-04-01", "2024-04-01")]
    [InlineData("2024-05-31", "2024-04-01")]
    [InlineData("2024-06-30", "2024-04-01")]
    [InlineData("2024-07-01", "2024-07-01")]
    [InlineData("2024-09-30", "2024-07-01")]
    [InlineData("2024-10-01", "2024-10-01")]
    [InlineData("2024-11-30", "2024-10-01")]
    [InlineData("2024-12-31", "2024-10-01")]
    public void StartOfQuarterIsTheFirstOfJanuaryAprilJulyOrOctober(
        string date,
        string start
    )
    {
        Assert.Equal(Date(start), PureQLTime.StartOfQuarter(Date(date)));
    }

    [Theory]
    [InlineData("2025-01-01", "2024-12-30")]
    [InlineData("2024-12-30", "2024-12-30")]
    [InlineData("2025-01-05", "2024-12-30")]
    [InlineData("2025-01-06", "2025-01-06")]
    [InlineData("0001-01-01", "0001-01-01")]
    [InlineData("0001-01-07", "0001-01-01")]
    [InlineData("9999-12-31", "9999-12-27")]
    public void StartOfWeekIsTheMondayOnOrBeforeTheDay(string date, string monday)
    {
        Assert.Equal(Date(monday), PureQLTime.StartOfWeek(Date(date)));
    }

    [Fact]
    public void StartOfAPeriodOfATimeSetsTheRestToZero()
    {
        TimeOnly time = Time("18:30:15.25");

        Assert.Equal(Time("18:00:00"), PureQLTime.StartOfHour(time));
        Assert.Equal(Time("18:30:00"), PureQLTime.StartOfMinute(time));
        Assert.Equal(Time("18:30:15"), PureQLTime.StartOfSecond(time));
    }

    [Fact]
    public void StartOfAPeriodOfAnInstantIsTruncatedInUtc()
    {
        DateTime instant = Instant("2024-08-15T18:30:15.25Z");

        Same("2024-01-01T00:00:00Z", PureQLTime.StartOfYear(instant));
        Same("2024-07-01T00:00:00Z", PureQLTime.StartOfQuarter(instant));
        Same("2024-08-01T00:00:00Z", PureQLTime.StartOfMonth(instant));
        Same("2024-08-12T00:00:00Z", PureQLTime.StartOfWeek(instant));
        Same("2024-08-15T00:00:00Z", PureQLTime.StartOfDay(instant));
        Same("2024-08-15T18:00:00Z", PureQLTime.StartOfHour(instant));
        Same("2024-08-15T18:30:00Z", PureQLTime.StartOfMinute(instant));
        Same("2024-08-15T18:30:15Z", PureQLTime.StartOfSecond(instant));
    }

    [Fact]
    public void StartOfAPeriodReadsAnyInstantAsUtcAndCutsItsLastDigit()
    {
        DateTime stored = new(
            Instant("2024-08-15T18:30:15.25Z").Ticks + 9,
            DateTimeKind.Local
        );

        Same("2024-01-01T00:00:00Z", PureQLTime.StartOfYear(stored));
        Same("2024-07-01T00:00:00Z", PureQLTime.StartOfQuarter(stored));
        Same("2024-08-01T00:00:00Z", PureQLTime.StartOfMonth(stored));
        Same("2024-08-12T00:00:00Z", PureQLTime.StartOfWeek(stored));
        Same("2024-08-15T00:00:00Z", PureQLTime.StartOfDay(stored));
        Same("2024-08-15T18:00:00Z", PureQLTime.StartOfHour(stored));
        Same("2024-08-15T18:30:00Z", PureQLTime.StartOfMinute(stored));
        Same("2024-08-15T18:30:15Z", PureQLTime.StartOfSecond(stored));
    }

    [Theory]
    [InlineData(Moscow, "2024-04-01", "01:30:00")]
    [InlineData(NewYork, "2024-03-31", "18:30:00")]
    [InlineData(Kathmandu, "2024-04-01", "04:15:00")]
    [InlineData("UTC", "2024-03-31", "22:30:00")]
    public void LocalTimeIsTheInstantShiftedByTheOffsetOfTheZone(
        string zone,
        string date,
        string time
    )
    {
        DateTime instant = Instant("2024-03-31T22:30:00Z");

        Assert.Equal(Date(date), PureQLTime.LocalDate(instant, zone));
        Assert.Equal(Time(time), PureQLTime.LocalTime(instant, zone));
    }

    [Fact]
    public void PartsOfALocalDateAreThoseOfTheLocalDay()
    {
        DateTime instant = Instant("2024-03-31T22:30:00Z");
        DateOnly moscow = PureQLTime.LocalDate(instant, Moscow);
        DateOnly utc = DateOnly.FromDateTime(instant);

        Assert.Equal(4, moscow.Month);
        Assert.Equal(1, moscow.Day);
        Assert.Equal(1, PureQLTime.DayOfWeek(moscow));
        Assert.Equal(1, PureQLTime.LocalTime(instant, Moscow).Hour);
        Assert.Equal(3, utc.Month);
        Assert.Equal(31, utc.Day);
        Assert.Equal(7, PureQLTime.DayOfWeek(utc));
        Assert.Equal(15, PureQLTime.LocalTime(instant, Kathmandu).Minute);
    }

    [Fact]
    public void TheOffsetOfAZoneChangesWithDaylightSavingTime()
    {
        Assert.Equal(
            Time("08:00:00"),
            PureQLTime.LocalTime(Instant("2024-07-01T12:00:00Z"), NewYork)
        );
        Assert.Equal(
            Time("07:00:00"),
            PureQLTime.LocalTime(Instant("2024-01-01T12:00:00Z"), NewYork)
        );
    }

    [Fact]
    public void AnInstantIsReadInTokyoOnTheNextDay()
    {
        DateTime instant = Instant("2024-01-31T23:30:00Z");

        Assert.Equal(Date("2024-02-01"), PureQLTime.LocalDate(instant, "Asia/Tokyo"));
        Assert.Equal(Time("08:30:00"), PureQLTime.LocalTime(instant, "Asia/Tokyo"));
    }

    [Fact]
    public void AFixedOffsetHasItsSignReversed()
    {
        DateTime instant = Instant("2024-01-31T23:30:00Z");

        Assert.Equal(Time("02:30:00"), PureQLTime.LocalTime(instant, "Etc/GMT-3"));
        Assert.Equal(Date("2024-02-01"), PureQLTime.LocalDate(instant, "Etc/GMT-3"));
        Assert.Equal(Time("11:30:00"), PureQLTime.LocalTime(instant, "Etc/GMT+12"));
        Assert.Equal(Time("13:30:00"), PureQLTime.LocalTime(instant, "Etc/GMT-14"));
    }

    [Fact]
    public void LocalTimeKeepsTheFractionOfTheSecondToTheLastDigit()
    {
        DateTime instant = Instant("2024-03-31T22:30:00.123456Z").AddTicks(7);

        Assert.Equal(
            Time("04:15:00.123456").Add(Tick(7)),
            PureQLTime.LocalTime(instant, Kathmandu)
        );
    }

    [Fact]
    public void ALocalDateBeforeTheFirstYearIsOutOfRange()
    {
        DateTime first = Instant("0001-01-01T00:00:00Z");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.LocalDate(first, NewYork)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.LocalTime(first, NewYork)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.StartOfHour(first, NewYork)
        );
        Assert.Equal(Date("0001-01-01"), PureQLTime.LocalDate(first, Moscow));
    }

    [Fact]
    public void ALocalDateAfterTheLastYearIsOutOfRange()
    {
        DateTime last = Instant("9999-12-31T23:59:59.999999Z");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.LocalDate(last, Moscow)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.AddMonths(last, 0, Moscow)
        );
        Assert.Equal(Date("9999-12-31"), PureQLTime.LocalDate(last, NewYork));
    }

    [Fact]
    public void StartOfALocalPeriodInMoscowIsItsFirstInstant()
    {
        DateTime instant = Instant("2024-08-15T18:30:15.25Z");

        Assert.Equal(Time("21:30:15.25"), PureQLTime.LocalTime(instant, Moscow));
        Same("2023-12-31T21:00:00Z", PureQLTime.StartOfYear(instant, Moscow));
        Same("2024-06-30T21:00:00Z", PureQLTime.StartOfQuarter(instant, Moscow));
        Same("2024-07-31T21:00:00Z", PureQLTime.StartOfMonth(instant, Moscow));
        Same("2024-08-11T21:00:00Z", PureQLTime.StartOfWeek(instant, Moscow));
        Same("2024-08-14T21:00:00Z", PureQLTime.StartOfDay(instant, Moscow));
        Same("2024-08-15T18:00:00Z", PureQLTime.StartOfHour(instant, Moscow));
        Same("2024-08-15T18:30:00Z", PureQLTime.StartOfMinute(instant, Moscow));
    }

    [Fact]
    public void StartOfALocalPeriodInKathmanduFollowsItsQuarterHourOffset()
    {
        DateTime instant = Instant("2024-08-15T18:30:15.25Z");

        Assert.Equal(Date("2024-08-16"), PureQLTime.LocalDate(instant, Kathmandu));
        Assert.Equal(Time("00:15:15.25"), PureQLTime.LocalTime(instant, Kathmandu));
        Same("2023-12-31T18:15:00Z", PureQLTime.StartOfYear(instant, Kathmandu));
        Same("2024-06-30T18:15:00Z", PureQLTime.StartOfQuarter(instant, Kathmandu));
        Same("2024-07-31T18:15:00Z", PureQLTime.StartOfMonth(instant, Kathmandu));
        Same("2024-08-11T18:15:00Z", PureQLTime.StartOfWeek(instant, Kathmandu));
        Same("2024-08-15T18:15:00Z", PureQLTime.StartOfDay(instant, Kathmandu));
        Same("2024-08-15T18:15:00Z", PureQLTime.StartOfHour(instant, Kathmandu));
        Same("2024-08-15T18:30:00Z", PureQLTime.StartOfMinute(instant, Kathmandu));
    }

    [Fact]
    public void StartOfAPeriodInUtcAsAZoneIsTheStartWithoutAZone()
    {
        DateTime instant = Instant("2024-08-15T18:30:15.25Z");

        Assert.Equal(
            PureQLTime.StartOfYear(instant),
            PureQLTime.StartOfYear(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfQuarter(instant),
            PureQLTime.StartOfQuarter(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfMonth(instant),
            PureQLTime.StartOfMonth(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfWeek(instant),
            PureQLTime.StartOfWeek(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfDay(instant),
            PureQLTime.StartOfDay(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfHour(instant),
            PureQLTime.StartOfHour(instant, "UTC")
        );
        Assert.Equal(
            PureQLTime.StartOfMinute(instant),
            PureQLTime.StartOfMinute(instant, "UTC")
        );
    }

    [Fact]
    public void ALocalDayIsNotAlwaysTwentyFourHoursLong()
    {
        DateTime spring = PureQLTime.StartOfDay(Instant("2024-03-10T12:00:00Z"), NewYork);
        DateTime next = PureQLTime.StartOfDay(Instant("2024-03-11T12:00:00Z"), NewYork);
        DateTime autumn = PureQLTime.StartOfDay(Instant("2024-11-03T12:00:00Z"), NewYork);
        DateTime after = PureQLTime.StartOfDay(Instant("2024-11-04T12:00:00Z"), NewYork);

        Same("2024-03-10T05:00:00Z", spring);
        Same("2024-03-11T04:00:00Z", next);
        Assert.Equal(TimeSpan.FromHours(23), next - spring);
        Assert.Equal(TimeSpan.FromHours(25), after - autumn);
    }

    [Fact]
    public void ADayStartsAtTheFirstTimeThatExistsWhereClocksGoForwardAtMidnight()
    {
        DateTime start = PureQLTime.StartOfDay(
            Instant("2024-04-26T10:00:00Z"),
            "Africa/Cairo"
        );

        Same("2024-04-25T22:00:00Z", start);
        Assert.Equal(Time("01:00:00"), PureQLTime.LocalTime(start, "Africa/Cairo"));
        Assert.Equal(Date("2024-04-26"), PureQLTime.LocalDate(start, "Africa/Cairo"));
    }

    [Fact]
    public void BothPassesOfARepeatedHourAreTheSameLocalHour()
    {
        DateTime first = Instant("2024-10-27T00:30:00Z");
        DateTime second = Instant("2024-10-27T01:30:00Z");

        Assert.Equal(Time("02:30:00"), PureQLTime.LocalTime(first, "Europe/Berlin"));
        Assert.Equal(Time("02:30:00"), PureQLTime.LocalTime(second, "Europe/Berlin"));
        Same("2024-10-27T00:00:00Z", PureQLTime.StartOfHour(first, "Europe/Berlin"));
        Same("2024-10-27T00:00:00Z", PureQLTime.StartOfHour(second, "Europe/Berlin"));
        Same("2024-10-27T00:30:00Z", PureQLTime.StartOfMinute(second, "Europe/Berlin"));
    }

    [Theory]
    [InlineData("2024-01-31", "18:30:00", "2024-01-31T23:30:00Z")]
    [InlineData("2024-11-03", "01:30:00", "2024-11-03T05:30:00Z")]
    [InlineData("2024-03-10", "02:30:00", "2024-03-10T07:00:00Z")]
    [InlineData("2024-11-03", "00:59:59.999999", "2024-11-03T04:59:59.999999Z")]
    [InlineData("2024-11-03", "01:00:00", "2024-11-03T05:00:00Z")]
    [InlineData("2024-11-03", "01:59:59.999999", "2024-11-03T05:59:59.999999Z")]
    [InlineData("2024-11-03", "02:00:00", "2024-11-03T07:00:00Z")]
    [InlineData("2024-03-10", "01:59:59.999999", "2024-03-10T06:59:59.999999Z")]
    [InlineData("2024-03-10", "02:00:00", "2024-03-10T07:00:00Z")]
    [InlineData("2024-03-10", "02:59:59.999999", "2024-03-10T07:00:00Z")]
    [InlineData("2024-03-10", "03:00:00", "2024-03-10T07:00:00Z")]
    [InlineData("2024-03-10", "03:00:00.000001", "2024-03-10T07:00:00.000001Z")]
    public void ALocalTimeIsTheFirstInstantAtWhichTheClocksReadItOrLater(
        string date,
        string time,
        string instant
    )
    {
        Same(instant, PureQLTime.DatetimeFromParts(Date(date), Time(time), NewYork));
    }

    [Fact]
    public void ALocalDayThatAZoneSkippedGivesTheFirstInstantAfterIt()
    {
        DateTime instant = PureQLTime.DatetimeFromParts(
            Date("2011-12-30"),
            TimeOnly.MinValue,
            "Pacific/Apia"
        );

        Same("2011-12-30T10:00:00Z", instant);
        Assert.Equal(Date("2011-12-31"), PureQLTime.LocalDate(instant, "Pacific/Apia"));
        Assert.Equal(TimeOnly.MinValue, PureQLTime.LocalTime(instant, "Pacific/Apia"));
        Same(
            "2011-12-30T10:00:00Z",
            PureQLTime.DatetimeFromParts(
                Date("2011-12-30"),
                Time("23:59:59.999999"),
                "Pacific/Apia"
            )
        );
        Same(
            "2011-12-30T09:59:59Z",
            PureQLTime.DatetimeFromParts(
                Date("2011-12-29"),
                Time("23:59:59"),
                "Pacific/Apia"
            )
        );
    }

    [Fact]
    public void ALocalTimeInUtcAsAZoneIsTheSameInstant()
    {
        Same(
            "2024-01-31T18:30:00Z",
            PureQLTime.DatetimeFromParts(Date("2024-01-31"), Time("18:30:00"), "UTC")
        );
        Same(
            "0001-01-01T00:00:00Z",
            PureQLTime.DatetimeFromParts(Date("0001-01-01"), TimeOnly.MinValue, "UTC")
        );
        Assert.Equal(
            DateTime.MaxValue.Ticks,
            PureQLTime
                .DatetimeFromParts(Date("9999-12-31"), TimeOnly.MaxValue, "UTC")
                .Ticks
        );
    }

    [Fact]
    public void ALocalTimeKeepsTheFractionOfTheSecondToTheLastDigit()
    {
        DateTime instant = PureQLTime.DatetimeFromParts(
            Date("2024-01-31"),
            Time("18:30:00.123456").Add(Tick(7)),
            NewYork
        );

        Assert.Equal(Instant("2024-01-31T23:30:00.123456Z").AddTicks(7), instant);
        Assert.Equal(DateTimeKind.Utc, instant.Kind);
    }

    [Fact]
    public void TheLocalDateAndTimeOfAnInstantGiveTheInstantAgain()
    {
        foreach (string zone in new[] { NewYork, Moscow, Kathmandu, "Pacific/Apia" })
        {
            DateTime instant = Instant("2024-03-09T00:00:00Z");

            for (int i = 0; i < 400; i++)
            {
                Assert.Equal(
                    instant,
                    PureQLTime.DatetimeFromParts(
                        PureQLTime.LocalDate(instant, zone),
                        PureQLTime.LocalTime(instant, zone),
                        zone
                    )
                );
                instant = instant.AddMinutes(13);
            }
        }
    }

    [Fact]
    public void TheSecondPassOfARepeatedTimeGivesTheInstantOfTheFirstPass()
    {
        DateTime second = Instant("2024-11-03T06:30:00Z");

        Assert.Equal(Time("01:30:00"), PureQLTime.LocalTime(second, NewYork));
        Same(
            "2024-11-03T05:30:00Z",
            PureQLTime.DatetimeFromParts(
                PureQLTime.LocalDate(second, NewYork),
                PureQLTime.LocalTime(second, NewYork),
                NewYork
            )
        );
    }

    [Fact]
    public void TheRowsOfALocalDayAreThoseFromItsStartToTheStartOfTheNext()
    {
        DateOnly day = Date("2024-03-10");
        DateTime start = PureQLTime.DatetimeFromParts(day, TimeOnly.MinValue, NewYork);
        DateTime end = PureQLTime.DatetimeFromParts(
            day.AddDays(1),
            TimeOnly.MinValue,
            NewYork
        );

        Assert.Equal(day, PureQLTime.LocalDate(start, NewYork));
        Assert.Equal(day, PureQLTime.LocalDate(end.AddTicks(-10), NewYork));
        Assert.Equal(day.AddDays(-1), PureQLTime.LocalDate(start.AddTicks(-10), NewYork));
        Assert.Equal(day.AddDays(1), PureQLTime.LocalDate(end, NewYork));
    }

    [Fact]
    public void MidnightOfTheLocalDayOfAnInstantIsItsStartOfDay()
    {
        foreach (string zone in new[] { NewYork, Moscow, "Africa/Cairo", "Pacific/Apia" })
        {
            foreach (string day in new[] { "2024-04-26", "2011-12-31", "2024-03-10" })
            {
                DateTime instant = Instant(day + "T10:00:00Z");

                Assert.Equal(
                    PureQLTime.StartOfDay(instant, zone),
                    PureQLTime.DatetimeFromParts(
                        PureQLTime.LocalDate(instant, zone),
                        TimeOnly.MinValue,
                        zone
                    )
                );
            }
        }
    }

    [Fact]
    public void TheSameLocalTimeOnTheNextDayIsBuiltFromPartsNotFromSeconds()
    {
        DateTime instant = Instant("2024-03-09T17:00:00Z");
        DateTime next = PureQLTime.DatetimeFromParts(
            PureQLTime.LocalDate(instant, NewYork).AddDays(1),
            PureQLTime.LocalTime(instant, NewYork),
            NewYork
        );

        Same("2024-03-10T16:00:00Z", next);
        Assert.Equal(Time("12:00:00"), PureQLTime.LocalTime(next, NewYork));
        Assert.Equal(
            Time("13:00:00"),
            PureQLTime.LocalTime(instant.AddSeconds(86400), NewYork)
        );
    }

    [Fact]
    public void AnInstantBeforeTheFirstOneIsOutOfRange()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.DatetimeFromParts(Date("0001-01-01"), TimeOnly.MinValue, Moscow)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.StartOfYear(Instant("0001-06-01T00:00:00Z"), Moscow)
        );
        Assert.Equal(
            DateTimeKind.Utc,
            PureQLTime
                .DatetimeFromParts(Date("0001-01-01"), TimeOnly.MinValue, NewYork)
                .Kind
        );
    }

    [Fact]
    public void AnInstantAfterTheLastOneIsOutOfRange()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.DatetimeFromParts(Date("9999-12-31"), Time("23:00:00"), NewYork)
        );
        Assert.Equal(
            9999,
            PureQLTime
                .DatetimeFromParts(Date("9999-12-31"), Time("23:00:00"), Moscow)
                .Year
        );
    }

    [Theory]
    [InlineData(18, 30, "15.25", "18:30:15.25")]
    [InlineData(0, 0, "0", "00:00:00")]
    [InlineData(23, 59, "59.999999", "23:59:59.999999")]
    [InlineData(1, 2, "3.0000005", "01:02:03.000001")]
    [InlineData(1, 2, "3.0000015", "01:02:03.000002")]
    [InlineData(1, 2, "3.0000004999999999999999999", "01:02:03")]
    [InlineData(1, 2, "59.9999994999999999999999999", "01:02:59.999999")]
    [InlineData(1, 2, "0.0000000000000000000000000001", "01:02:00")]
    public void TimeFromPartsRoundsTheSecondHalfAwayFromZeroToMicroseconds(
        long hour,
        long minute,
        string second,
        string time
    )
    {
        Assert.Equal(
            Time(time),
            PureQLTime.TimeFromParts(hour, minute, PureQLConversions.AsDecimal(second))
        );
    }

    [Theory]
    [InlineData(24, 0, "0")]
    [InlineData(-1, 0, "0")]
    [InlineData(long.MaxValue, 0, "0")]
    [InlineData(0, 60, "0")]
    [InlineData(0, -1, "0")]
    [InlineData(0, long.MinValue, "0")]
    [InlineData(0, 0, "60")]
    [InlineData(0, 0, "59.9999995")]
    [InlineData(0, 0, "-1")]
    [InlineData(0, 0, "-0.0000001")]
    [InlineData(0, 0, "-0.000001")]
    [InlineData(0, 0, "9999999999999999999999999999")]
    public void TimeFromPartsFailsOnAPartOutsideItsRange(
        long hour,
        long minute,
        string second
    )
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.TimeFromParts(hour, minute, PureQLConversions.AsDecimal(second))
        );
    }

    [Theory]
    [InlineData("2024-02-14", "2024-01-15", 0)]
    [InlineData("2024-02-15", "2024-01-15", 1)]
    [InlineData("2023-02-28", "2023-01-31", 1)]
    [InlineData("2023-02-27", "2023-01-31", 0)]
    [InlineData("2024-02-28", "2024-01-31", 0)]
    [InlineData("2024-02-29", "2024-01-31", 1)]
    [InlineData("2025-02-28", "2024-02-29", 12)]
    [InlineData("2025-02-27", "2024-02-29", 11)]
    [InlineData("2028-02-28", "2024-02-29", 47)]
    [InlineData("2028-02-29", "2024-02-29", 48)]
    [InlineData("2024-01-15", "2024-01-15", 0)]
    [InlineData("2024-01-15", "2024-02-14", 0)]
    [InlineData("2024-01-15", "2024-02-15", -1)]
    [InlineData("2023-01-31", "2023-02-28", -1)]
    [InlineData("2024-01-31", "2024-02-28", 0)]
    [InlineData("2024-01-29", "2024-02-29", -1)]
    [InlineData("9999-12-31", "0001-01-01", 119987)]
    [InlineData("0001-01-01", "9999-12-31", -119987)]
    public void DiffMonthsCountsWholeMonthsFromRightToLeft(
        string left,
        string right,
        long months
    )
    {
        Assert.Equal(months, PureQLTime.DiffMonths(Date(left), Date(right)));
    }

    [Fact]
    public void DiffMonthsIsTheInverseOfAddingMonthsWithClamping()
    {
        DateOnly date = Date("2023-01-01");

        for (int day = 0; day < 800; day++)
        {
            for (int months = -40; months <= 40; months++)
            {
                Assert.Equal(
                    months,
                    PureQLTime.DiffMonths(date.AddMonths(months), date)
                );
            }

            date = date.AddDays(1);
        }
    }

    [Fact]
    public void SwappingTheOperandsOfDiffMonthsOnlyChangesTheSign()
    {
        DateOnly left = Date("2023-01-01");

        for (int i = 0; i < 500; i++)
        {
            DateOnly right = Date("2024-02-29").AddDays((i * 37 % 900) - 450);

            Assert.Equal(
                -PureQLTime.DiffMonths(right, left),
                PureQLTime.DiffMonths(left, right)
            );
            left = left.AddDays(3);
        }
    }

    [Theory]
    [InlineData("2024-02-15T11:59:59Z", "2024-01-15T12:00:00Z", 0)]
    [InlineData("2024-02-15T11:59:59.999999Z", "2024-01-15T12:00:00Z", 0)]
    [InlineData("2024-02-15T12:00:00Z", "2024-01-15T12:00:00Z", 1)]
    [InlineData("2024-01-15T12:00:00Z", "2024-02-15T11:59:59Z", 0)]
    [InlineData("2024-01-15T12:00:00Z", "2024-02-15T12:00:00Z", -1)]
    [InlineData("2024-02-29T12:00:00Z", "2024-01-31T12:00:00Z", 1)]
    [InlineData("2024-02-29T11:59:59Z", "2024-01-31T12:00:00Z", 0)]
    [InlineData("2024-03-15T16:30:00Z", "2024-02-15T17:00:00Z", 0)]
    public void DiffMonthsOfInstantsCountsTheTimeOfDayInUtc(
        string left,
        string right,
        long months
    )
    {
        Assert.Equal(months, PureQLTime.DiffMonths(Instant(left), Instant(right)));
        Assert.Equal(months, PureQLTime.DiffMonths(Instant(left), Instant(right), "UTC"));
    }

    [Theory]
    [InlineData("2024-03-15T16:30:00Z", "2024-02-15T17:00:00Z", 1)]
    [InlineData("2024-03-15T16:00:00Z", "2024-02-15T17:00:00Z", 1)]
    [InlineData("2024-03-15T15:59:59Z", "2024-02-15T17:00:00Z", 0)]
    [InlineData("2024-02-15T17:00:00Z", "2024-03-15T16:30:00Z", -1)]
    [InlineData("2024-02-15T17:00:00Z", "2024-03-15T15:59:59Z", 0)]
    [InlineData("2024-02-15T17:00:00Z", "2024-02-15T17:00:00Z", 0)]
    [InlineData("2024-11-03T05:10:00Z", "2024-10-03T05:30:00Z", 0)]
    [InlineData("2024-11-03T05:30:00Z", "2024-10-03T05:30:00Z", 1)]
    [InlineData("2024-11-03T06:10:00Z", "2024-10-03T05:30:00Z", 1)]
    [InlineData("2024-11-03T06:10:00Z", "2024-11-03T05:30:00Z", 0)]
    [InlineData("2024-03-10T07:00:00Z", "2024-02-10T07:30:00Z", 1)]
    [InlineData("2024-03-10T06:59:59Z", "2024-02-10T07:30:00Z", 0)]
    [InlineData("9999-12-15T12:00:00Z", "9999-11-10T12:00:00Z", 1)]
    [InlineData("9999-12-15T12:00:00Z", "0001-01-10T12:00:00Z", 119987)]
    public void DiffMonthsInAZoneCountsMonthsOfTheLocalCalendar(
        string left,
        string right,
        long months
    )
    {
        Assert.Equal(
            months,
            PureQLTime.DiffMonths(Instant(left), Instant(right), NewYork)
        );
    }

    [Theory]
    [InlineData("2024-02-15T17:00:00Z", 1, NewYork, "2024-03-15T16:00:00Z")]
    [InlineData("2024-03-30T22:00:00Z", 1, Moscow, "2024-04-29T22:00:00Z")]
    [InlineData("2024-02-10T07:30:00Z", 1, NewYork, "2024-03-10T07:00:00Z")]
    [InlineData("2024-03-15T16:00:00Z", -1, NewYork, "2024-02-15T17:00:00Z")]
    [InlineData("2024-02-15T17:00:00Z", 0, NewYork, "2024-02-15T17:00:00Z")]
    [InlineData("2024-01-31T12:00:00Z", 1, "UTC", "2024-02-29T12:00:00Z")]
    [InlineData("2024-02-29T12:00:00Z", 12, "UTC", "2025-02-28T12:00:00Z")]
    [InlineData("2024-10-03T05:30:00Z", 1, NewYork, "2024-11-03T05:30:00Z")]
    [InlineData("0001-01-01T12:00:00Z", 119987, "Etc/GMT-3", "9999-12-01T12:00:00Z")]
    [InlineData("9999-12-01T12:00:00Z", -119987, "Etc/GMT-3", "0001-01-01T12:00:00Z")]
    public void AddMonthsInAZoneMovesTheLocalDateAndKeepsTheLocalTimeOfDay(
        string operand,
        long months,
        string zone,
        string result
    )
    {
        Same(result, PureQLTime.AddMonths(Instant(operand), months, zone));
    }

    [Fact]
    public void AddMonthsInUtcMovesTheInstantWhereAZoneKeepsTheLocalTime()
    {
        Assert.Equal(
            Time("13:00:00"),
            PureQLTime.LocalTime(Instant("2024-02-15T17:00:00Z").AddMonths(1), NewYork)
        );
        Assert.Equal(
            Time("12:00:00"),
            PureQLTime.LocalTime(
                PureQLTime.AddMonths(Instant("2024-02-15T17:00:00Z"), 1, NewYork),
                NewYork
            )
        );
    }

    [Theory]
    [InlineData("2024-02-15T17:00:00Z", 95713)]
    [InlineData("2024-02-15T17:00:00Z", -24290)]
    [InlineData("2024-02-15T17:00:00Z", 120000)]
    [InlineData("2024-02-15T17:00:00Z", 4294967297)]
    [InlineData("2024-02-15T17:00:00Z", long.MaxValue)]
    [InlineData("2024-02-15T17:00:00Z", long.MinValue)]
    [InlineData("0001-01-01T12:00:00Z", -1)]
    [InlineData("9999-12-01T12:00:00Z", 1)]
    public void AddMonthsFailsOutsideTheYearsOfADate(string operand, long months)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.AddMonths(Instant(operand), months, Moscow)
        );
    }

    [Fact]
    public void AddMonthsFailsWhenTheInstantIsOutsideTheYearsOfADatetime()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLTime.AddMonths(Instant("0001-01-31T22:00:00Z"), -1, "Etc/GMT-3")
        );
    }

    [Fact]
    public void DiffMonthsInAZoneIsTheInverseOfAddMonthsInThatZone()
    {
        foreach (string zone in new[] { NewYork, Moscow, "Australia/Lord_Howe", "UTC" })
        {
            DateTime instant = Instant("2023-01-01T00:20:00Z");

            for (int i = 0; i < 300; i++)
            {
                for (int months = -14; months <= 14; months += 7)
                {
                    DateTime moved = PureQLTime.AddMonths(instant, months, zone);

                    Assert.Equal(months, PureQLTime.DiffMonths(moved, instant, zone));
                }

                instant = instant.AddHours(61);
            }
        }
    }

    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kolkata")]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("Africa/Abidjan")]
    [InlineData("Pacific/Wallis")]
    [InlineData("Etc/GMT+12")]
    [InlineData("Etc/GMT-14")]
    [InlineData("UTC")]
    public void IsZoneAcceptsANameOfTheListAndUtc(string name)
    {
        Assert.True(PureQLTime.IsZone(name));
        Assert.Equal(
            DateTimeKind.Utc,
            PureQLTime.StartOfDay(Instant("2024-08-15T18:30:15.25Z"), name).Kind
        );
    }

    [Theory]
    [InlineData("Europe/Atlantis")]
    [InlineData("US/Eastern")]
    [InlineData("Asia/Calcutta")]
    [InlineData("America/Argentina/ComodRivadavia")]
    [InlineData("Europe/Kiev")]
    [InlineData("MSK")]
    [InlineData("EST")]
    [InlineData("EST5EDT")]
    [InlineData("+03:00")]
    [InlineData("")]
    [InlineData("europe/moscow")]
    [InlineData("EUROPE/MOSCOW")]
    [InlineData(" Europe/Moscow")]
    [InlineData("Europe/Moscow ")]
    [InlineData("Europe/Moscow\n")]
    [InlineData("utc")]
    [InlineData("Etc/UTC")]
    [InlineData("GMT")]
    [InlineData("Etc/GMT")]
    [InlineData("Etc/GMT+13")]
    [InlineData("Z")]
    public void AnyOtherTextIsNotAZone(string name)
    {
        Assert.False(PureQLTime.IsZone(name));

        DateTime instant = Instant("2024-08-15T18:30:15.25Z");
        DateOnly day = Date("2024-08-15");
        Func<object>[] functions =
        [
            () => PureQLTime.StartOfYear(instant, name),
            () => PureQLTime.StartOfQuarter(instant, name),
            () => PureQLTime.StartOfMonth(instant, name),
            () => PureQLTime.StartOfWeek(instant, name),
            () => PureQLTime.StartOfDay(instant, name),
            () => PureQLTime.StartOfHour(instant, name),
            () => PureQLTime.StartOfMinute(instant, name),
            () => PureQLTime.LocalDate(instant, name),
            () => PureQLTime.LocalTime(instant, name),
            () => PureQLTime.DatetimeFromParts(day, TimeOnly.MinValue, name),
            () => PureQLTime.DiffMonths(instant, instant, name),
            () => PureQLTime.AddMonths(instant, 1, name),
        ];

        foreach (Func<object> function in functions)
        {
            TimeZoneNotFoundException error = Assert.Throws<TimeZoneNotFoundException>(
                function
            );

            Assert.Contains($"'{name}'", error.Message, StringComparison.Ordinal);
        }
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

    private static TimeSpan Tick(long ticks)
    {
        return TimeSpan.FromTicks(ticks);
    }
}
