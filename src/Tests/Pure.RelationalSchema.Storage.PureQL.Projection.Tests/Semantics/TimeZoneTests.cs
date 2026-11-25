using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Time zones, as the specification defines them under "Time zones": the
// operators that read a calendar or a clock from a datetime take an optional
// zone and read the local date and time; startOf… with a zone is the first
// instant of the local period; asDatetime of a date and datetimeFromParts go
// from a local date and time to the first instant at which the clocks read it
// or later; datetimeAddMonths and datetimeDiffMonths work in the local
// calendar. A zone is a string operand, a literal, a parameter or a value of
// the row, and the operator is lifted over it. A parameter that is not a zone
// name fails when the projection is built, a zone computed for a row when the
// row is read, and UTC is accepted and reads the instant as no zone does.
public sealed class TimeZoneTests
{
    private const string Moscow = "Europe/Moscow";

    private const string NewYork = "America/New_York";

    private const string Kathmandu = "Asia/Kathmandu";

    // The instant every reading operator is given: 00:15:15.25 on Monday
    // 2024-01-01 in Asia/Kathmandu, 5:45 ahead of UTC, and 18:30:15.25 on
    // Sunday 2023-12-31 in UTC.
    private const string Moment = "2023-12-31T18:30:15.25Z";

    // Rows read each with its own zone: row 2 has no zone, row 3 no instant
    // and row 4 the zone UTC.
    private static readonly IStoredSchemaDataSet Visits = new InlineTable(
        "t",
        "visits",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("zone", new StringColumnType())
    ).With(
        ["1", Moment, Kathmandu],
        ["2", Moment, ""],
        ["3", "", Kathmandu],
        ["4", Moment, "UTC"]
    );

    // One instant and a local midnight in several zones.
    private static readonly IStoredSchemaDataSet Stores = new InlineTable(
        "t",
        "stores",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("zone", new StringColumnType())
    ).With(
        ["1", "2024-03-31T22:30:00Z", Moscow],
        ["2", "2024-03-31T22:30:00Z", NewYork],
        ["3", "2024-03-31T22:30:00Z", Kathmandu],
        ["4", "2024-03-31T22:30:00Z", ""],
        ["5", "2023-12-31T21:30:15.25Z", Moscow],
        ["6", "2024-01-01T04:59:59.999999Z", NewYork],
        ["7", "2024-11-03T05:30:00Z", NewYork]
    );

    // A local day and time of day in several zones: 02:30 on 2024-03-10
    // never occurs in America/New_York.
    private static readonly IStoredSchemaDataSet Days = new InlineTable(
        "t",
        "days",
        ("id", new LongColumnType()),
        ("day", new DateColumnType()),
        ("clock", new TimeColumnType()),
        ("zone", new StringColumnType())
    ).With(
        ["1", "2024-03-10", "02:30:00", NewYork],
        ["2", "2024-03-10", "02:30:00", Moscow],
        ["3", "2024-03-10", "02:30:00", ""],
        ["4", "", "", Moscow],
        ["5", "2024-03-10", "02:30:00", "UTC"]
    );

    private static readonly IStoredSchemaDataSet Renewals = new InlineTable(
        "t",
        "renewals",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("zone", new StringColumnType()),
        ("n", new LongColumnType())
    ).With(
        ["1", "2024-02-15T17:00:00Z", NewYork, "1"],
        ["2", "2024-03-30T22:00:00Z", Moscow, "1"],
        ["3", "2024-02-10T07:30:00Z", NewYork, "1"],
        ["4", "2024-02-15T17:00:00Z", "", "1"],
        ["5", "2024-02-15T17:00:00Z", "UTC", "1"],
        ["6", "", NewYork, "1"],
        ["7", "2024-02-15T17:00:00Z", NewYork, ""]
    );

    // Orders around the two ends of 2024-03-10 in America/New_York, which
    // starts at 05:00 UTC and ends at 04:00 UTC: the clocks are set forward.
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("total", new LongColumnType())
    ).With(
        ["1", "2024-03-10T04:59:59Z", "10"],
        ["2", "2024-03-10T05:00:00Z", "20"],
        ["3", "2024-03-11T03:59:59Z", "30"],
        ["4", "2024-03-11T04:00:00Z", "40"]
    );

    // Every operator that reads a datetime in a zone, with its value for
    // Moment in Asia/Kathmandu and in UTC.
    public static TheoryData<string, string, string> Readings =>
        new()
        {
            { "asDate", "2024-01-01", "2023-12-31" },
            { "asTime", "00:15:15.25", "18:30:15.25" },
            { "year", "2024", "2023" },
            { "quarter", "1", "4" },
            { "month", "1", "12" },
            { "day", "1", "31" },
            { "dayOfWeek", "1", "7" },
            { "dayOfYear", "1", "365" },
            { "week", "1", "52" },
            { "weekYear", "2024", "2023" },
            { "hour", "0", "18" },
            { "minute", "15", "30" },
            { "second", "15", "15" },
            { "startOfYear", "2023-12-31T18:15:00Z", "2023-01-01T00:00:00Z" },
            { "startOfQuarter", "2023-12-31T18:15:00Z", "2023-10-01T00:00:00Z" },
            { "startOfMonth", "2023-12-31T18:15:00Z", "2023-12-01T00:00:00Z" },
            { "startOfWeek", "2023-12-31T18:15:00Z", "2023-12-25T00:00:00Z" },
            { "startOfDay", "2023-12-31T18:15:00Z", "2023-12-31T00:00:00Z" },
            { "startOfHour", "2023-12-31T18:15:00Z", "2023-12-31T18:00:00Z" },
            { "startOfMinute", "2023-12-31T18:30:00Z", "2023-12-31T18:30:00Z" },
        };

    // The readings of Readings, by name.
    public static TheoryData<string> ReadingNames
    {
        get
        {
            TheoryData<string> names = [];

            foreach (object[] row in Readings)
            {
                names.Add((string)row[0]);
            }

            return names;
        }
    }

    // The operators that go from a local date and time to an instant or
    // count months in a local calendar, with their value in America/New_York
    // and in UTC over the operands of Building.
    public static TheoryData<string, string, string> Buildings =>
        new()
        {
            { "asDatetime", "2024-03-10T05:00:00Z", "2024-03-10T00:00:00Z" },
            { "datetimeFromParts", "2024-03-10T07:00:00Z", "2024-03-10T02:30:00Z" },
            { "datetimeAddMonths", "2024-03-15T16:00:00Z", "2024-03-15T17:00:00Z" },
            { "datetimeDiffMonths", "1", "0" },
        };

    [Theory]
    [MemberData(nameof(Readings))]
    public void LiteralZoneReadsTheLocalDateAndTime(
        string reading,
        string local,
        string utc
    )
    {
        Assert.Equal(
            local,
            Value(Reading(reading, Instant(Moment), new LiteralString(Kathmandu)))
        );
        Assert.Equal(utc, Value(Reading(reading, Instant(Moment), null)));
    }

    [Theory]
    [MemberData(nameof(Readings))]
    public void ParameterZoneReadsTheLocalDateAndTime(
        string reading,
        string local,
        string utc
    )
    {
        ExprProjection expression = Reading(
            reading,
            Instant(Moment),
            new ParamString("zone")
        );

        Assert.Equal(local, Value(expression, Zone(Kathmandu)));
        Assert.Equal(utc, Value(expression, Zone("UTC")));
    }

    [Theory]
    [MemberData(nameof(Readings))]
    public void ZoneOfTheRowReadsEachRowInItsOwnZoneAndANullZoneGivesNull(
        string reading,
        string local,
        string utc
    )
    {
        Assert.Equal(
            [local, "", "", utc],
            Values(
                Visits,
                "t.visits",
                NullableReading(
                    reading,
                    new FieldDatetimeNullable("t.visits", "at"),
                    new FieldStringNullable("t.visits", "zone")
                )
            )
        );
    }

    [Theory]
    [MemberData(nameof(ReadingNames))]
    public void NullZoneParameterGivesNull(string reading)
    {
        ExprProjection expression = NullableReading(
            reading,
            Instant(Moment),
            new ParamStringNullable("zone")
        );

        Assert.Equal(
            "",
            Value(
                expression,
                new Dictionary<string, IParameter>
                {
                    ["zone"] = new NullableStringParameter(),
                }
            )
        );
        Assert.NotEqual("", Value(expression, Zone(Kathmandu)));
    }

    [Theory]
    [MemberData(nameof(ReadingNames))]
    public void ZoneParameterThatIsNotAZoneNameFailsWhenTheProjectionIsBuilt(string reading)
    {
        ExprProjection expression = Reading(
            reading,
            Instant(Moment),
            new ParamString("zone")
        );

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(Visits, "t.visits", expression, Zone("Europe/Atlantis"))
        );
    }

    [Theory]
    [MemberData(nameof(ReadingNames))]
    public void ZoneOfARowThatIsNotAZoneNameFailsWhenTheRowIsRead(string reading)
    {
        IStoredSchemaDataSet places = Zones(Kathmandu, "Europe/Atlantis");
        PureQLProjection projection = Selecting(
            places,
            "t.zones",
            NullableReading(
                reading,
                Instant(Moment),
                new FieldStringNullable("t.zones", "zone")
            )
        );

        _ = Assert.Throws<TimeZoneNotFoundException>(() => projection.ToList());
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void LiteralZoneGivesTheInstantOfALocalDateAndTime(
        string building,
        string local,
        string utc
    )
    {
        Assert.Equal(local, Value(Building(building, new LiteralString(NewYork))));
        Assert.Equal(utc, Value(Building(building, null)));
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void ParameterZoneGivesTheInstantOfALocalDateAndTime(
        string building,
        string local,
        string utc
    )
    {
        ExprProjection expression = Building(building, new ParamString("zone"));

        Assert.Equal(local, Value(expression, Zone(NewYork)));
        Assert.Equal(utc, Value(expression, Zone("UTC")));
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void ZoneOfTheRowGivesTheInstantInThatZoneAndANullZoneGivesNull(
        string building,
        string local,
        string utc
    )
    {
        Assert.Equal(
            [local, "", utc],
            Values(
                Zones(NewYork, "", "UTC"),
                "t.zones",
                NullableBuilding(building, new FieldStringNullable("t.zones", "zone"))
            )
        );
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void NullZoneParameterGivesNoInstant(string building, string local, string utc)
    {
        ExprProjection expression = NullableBuilding(
            building,
            new ParamStringNullable("zone")
        );

        Assert.Equal(
            "",
            Value(
                expression,
                new Dictionary<string, IParameter>
                {
                    ["zone"] = new NullableStringParameter(),
                }
            )
        );
        Assert.Equal(local, Value(expression, Zone(NewYork)));
        Assert.NotEqual(local, utc);
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void ZoneParameterOfABuildingThatIsNotAZoneNameFailsWhenBuilt(
        string building,
        string local,
        string utc
    )
    {
        ExprProjection expression = Building(building, new ParamString("zone"));

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(Visits, "t.visits", expression, Zone("Europe/Atlantis"))
        );
        Assert.NotEqual(local, utc);
    }

    [Theory]
    [MemberData(nameof(Buildings))]
    public void ZoneOfARowOfABuildingThatIsNotAZoneNameFailsWhenTheRowIsRead(
        string building,
        string local,
        string utc
    )
    {
        PureQLProjection projection = Selecting(
            Zones(NewYork, "Europe/Atlantis"),
            "t.zones",
            NullableBuilding(building, new FieldStringNullable("t.zones", "zone"))
        );

        _ = Assert.Throws<TimeZoneNotFoundException>(() => projection.ToList());
        Assert.NotEqual(local, utc);
    }

    [Theory]
    [InlineData("Europe/Atlantis")]
    [InlineData("")]
    [InlineData("Moscow time")]
    public void TextThatIsNotAZoneNameIsRejectedAsAParameterAndAsAValueOfARow(
        string zone
    )
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                Visits,
                "t.visits",
                new AsDateDatetimeDateProjection(Instant(Moment), new ParamString("zone")),
                Zone(zone)
            )
        );

        PureQLProjection projection = Selecting(
            Zones(zone),
            "t.zones",
            new AsDateDatetimeDateProjection(
                Instant(Moment),
                new FieldString("t.zones", "zone")
            )
        );

        _ = Assert.Throws<TimeZoneNotFoundException>(() => projection.ToList());
    }

    [Fact]
    public void ZoneParameterIsCheckedWhereverItStandsAndWhateverTheRows()
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            new PureQLProjection(
                [Zones()],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.zones"),
                    where: new EqualDecimalRow(
                        new HourDatetimeIntegerRow(
                            Instant(Moment),
                            new ParamString("zone")
                        ),
                        new LiteralInteger(0)
                    ),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.zones", "id")),
                    ]
                ),
                Zone("Europe/Atlantis")
            )
        );
    }

    [Fact]
    public void ZoneOfARowThatIsNotReadDoesNotFailTheQuery()
    {
        FieldString zone = new("t.zones", "zone");

        Assert.Equal(
            ["2024-01-01", ""],
            Values(
                Zones(Kathmandu, "Europe/Atlantis"),
                "t.zones",
                new IfDateNullableProjection(
                    then: new LiteralDateNullable(),
                    @else: new AsDateDatetimeDateProjection(Instant(Moment), zone),
                    condition: new EqualStringProjection(
                        zone,
                        new LiteralString("Europe/Atlantis")
                    )
                )
            )
        );
    }

    [Fact]
    public void ZoneComputedForARowIsAZone()
    {
        IStoredSchemaDataSet cities = Zones("Berlin", "Moscow");

        Assert.Equal(
            ["02:30:00", "03:30:00"],
            Values(
                cities,
                "t.zones",
                new AsTimeDatetimeTimeProjection(
                    Instant("2024-10-27T00:30:00Z"),
                    new ConcatStringProjection([
                        new LiteralString("Europe/"),
                        new FieldString("t.zones", "zone"),
                    ])
                )
            )
        );
    }

    [Theory]
    [InlineData(null, "2024-03-31T22:30:00Z", "2024-03-31", "22:30:00")]
    [InlineData(Moscow, "2024-03-31T22:30:00Z", "2024-04-01", "01:30:00")]
    [InlineData(NewYork, "2024-03-31T22:30:00Z", "2024-03-31", "18:30:00")]
    [InlineData(Kathmandu, "2024-03-31T22:30:00Z", "2024-04-01", "04:15:00")]
    [InlineData(NewYork, "2024-01-31T22:30:00Z", "2024-01-31", "17:30:00")]
    [InlineData(null, "2024-01-31T23:30:00Z", "2024-01-31", "23:30:00")]
    [InlineData("Asia/Tokyo", "2024-01-31T23:30:00Z", "2024-02-01", "08:30:00")]
    [InlineData("Europe/Berlin", "2024-10-27T00:30:00Z", "2024-10-27", "02:30:00")]
    [InlineData("Europe/Berlin", "2024-10-27T01:30:00Z", "2024-10-27", "02:30:00")]
    [InlineData("Africa/Cairo", "2024-04-25T22:00:00Z", "2024-04-26", "01:00:00")]
    [InlineData("Pacific/Apia", "2011-12-30T10:00:00Z", "2011-12-31", "00:00:00")]
    public void AsDateAndAsTimeGiveTheLocalDayAndTimeOfDay(
        string? zone,
        string instant,
        string day,
        string time
    )
    {
        StringProjection? name = zone is null ? null : (StringProjection?)new LiteralString(zone);

        Assert.Equal(
            day,
            Value(new AsDateDatetimeDateProjection(Instant(instant), name))
        );
        Assert.Equal(
            time,
            Value(new AsTimeDatetimeTimeProjection(Instant(instant), name))
        );
    }

    [Theory]
    [InlineData("Etc/GMT-3", "2024-04-01", "01:30:00")]
    [InlineData("Etc/GMT+12", "2024-03-31", "10:30:00")]
    [InlineData("Etc/GMT+1", "2024-03-31", "21:30:00")]
    [InlineData("Etc/GMT-1", "2024-03-31", "23:30:00")]
    [InlineData("Etc/GMT-2", "2024-04-01", "00:30:00")]
    [InlineData("Etc/GMT-14", "2024-04-01", "12:30:00")]
    public void FixedOffsetHasItsSignReversed(string zone, string day, string time)
    {
        LiteralDatetime instant = Instant("2024-03-31T22:30:00Z");

        Assert.Equal(
            day,
            Value(new AsDateDatetimeDateProjection(instant, new LiteralString(zone)))
        );
        Assert.Equal(
            time,
            Value(new AsTimeDatetimeTimeProjection(instant, new LiteralString(zone)))
        );
    }

    [Theory]
    [InlineData("month", Moscow, "4")]
    [InlineData("day", Moscow, "1")]
    [InlineData("dayOfWeek", Moscow, "1")]
    [InlineData("hour", Moscow, "1")]
    [InlineData("month", null, "3")]
    [InlineData("day", null, "31")]
    [InlineData("dayOfWeek", null, "7")]
    [InlineData("hour", null, "22")]
    [InlineData("minute", Kathmandu, "15")]
    [InlineData("minute", null, "30")]
    [InlineData("hour", NewYork, "18")]
    [InlineData("quarter", Moscow, "2")]
    [InlineData("quarter", NewYork, "1")]
    [InlineData("dayOfYear", Kathmandu, "92")]
    [InlineData("dayOfYear", NewYork, "91")]
    [InlineData("week", Moscow, "14")]
    [InlineData("week", NewYork, "13")]
    public void PartOfAnInstantIsThePartOfItsLocalDateAndTime(
        string part,
        string? zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                Reading(
                    part,
                    Instant("2024-03-31T22:30:00Z"),
                    zone is null ? null : (StringProjection?)new LiteralString(zone)
                )
            )
        );
    }

    [Theory]
    [InlineData("year", "2024", "2024", "2024", "", "2024", "2023", "2024")]
    [InlineData("quarter", "2", "1", "2", "", "1", "4", "4")]
    [InlineData("month", "4", "3", "4", "", "1", "12", "11")]
    [InlineData("day", "1", "31", "1", "", "1", "31", "3")]
    [InlineData("dayOfWeek", "1", "7", "1", "", "1", "7", "7")]
    [InlineData("dayOfYear", "92", "91", "92", "", "1", "365", "308")]
    [InlineData("week", "14", "13", "14", "", "1", "52", "44")]
    [InlineData("weekYear", "2024", "2024", "2024", "", "2024", "2023", "2024")]
    public void CalendarPartWithAZoneIsThePartOfAsDateWithThatZone(
        string part,
        params string[] expected
    )
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        FieldStringNullable zone = new("t.stores", "zone");

        Assert.Equal(
            expected,
            Values(Stores, "t.stores", NullableReading(part, at, zone))
        );
        Assert.Equal(
            expected,
            Values(
                Stores,
                "t.stores",
                DatePart(part, new AsDateDatetimeDateNullableProjection(at, zone))
            )
        );
    }

    [Theory]
    [InlineData("hour", "1", "18", "4", "", "0", "23", "1")]
    [InlineData("minute", "30", "30", "15", "", "30", "59", "30")]
    [InlineData("second", "0", "0", "0", "", "15", "59", "0")]
    public void ClockPartWithAZoneIsThePartOfAsTimeWithThatZone(
        string part,
        params string[] expected
    )
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        FieldStringNullable zone = new("t.stores", "zone");

        Assert.Equal(
            expected,
            Values(Stores, "t.stores", NullableReading(part, at, zone))
        );
        Assert.Equal(
            expected,
            Values(
                Stores,
                "t.stores",
                TimePart(part, new AsTimeDatetimeTimeNullableProjection(at, zone))
            )
        );
    }

    [Theory]
    [InlineData("startOfYear", Moscow, "2023-12-31T21:00:00Z")]
    [InlineData("startOfQuarter", Moscow, "2024-06-30T21:00:00Z")]
    [InlineData("startOfMonth", Moscow, "2024-07-31T21:00:00Z")]
    [InlineData("startOfWeek", Moscow, "2024-08-11T21:00:00Z")]
    [InlineData("startOfDay", Moscow, "2024-08-14T21:00:00Z")]
    [InlineData("startOfHour", Moscow, "2024-08-15T18:00:00Z")]
    [InlineData("startOfMinute", Moscow, "2024-08-15T18:30:00Z")]
    [InlineData("startOfYear", Kathmandu, "2023-12-31T18:15:00Z")]
    [InlineData("startOfQuarter", Kathmandu, "2024-06-30T18:15:00Z")]
    [InlineData("startOfMonth", Kathmandu, "2024-07-31T18:15:00Z")]
    [InlineData("startOfWeek", Kathmandu, "2024-08-11T18:15:00Z")]
    [InlineData("startOfDay", Kathmandu, "2024-08-15T18:15:00Z")]
    [InlineData("startOfHour", Kathmandu, "2024-08-15T18:15:00Z")]
    [InlineData("startOfMinute", Kathmandu, "2024-08-15T18:30:00Z")]
    public void StartOfALocalPeriodIsItsFirstInstant(
        string period,
        string zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                Reading(
                    period,
                    Instant("2024-08-15T18:30:15.25Z"),
                    new LiteralString(zone)
                )
            )
        );
    }

    [Theory]
    [InlineData(NewYork, "2024-03-10T05:00:00Z", "2024-03-10T05:00:00Z")]
    [InlineData(NewYork, "2024-03-10T12:00:00Z", "2024-03-10T05:00:00Z")]
    [InlineData(NewYork, "2024-03-11T03:59:59.999999Z", "2024-03-10T05:00:00Z")]
    [InlineData(NewYork, "2024-03-11T04:00:00Z", "2024-03-11T04:00:00Z")]
    [InlineData(NewYork, "2024-03-10T04:59:59.999999Z", "2024-03-09T05:00:00Z")]
    [InlineData(NewYork, "2024-11-03T04:00:00Z", "2024-11-03T04:00:00Z")]
    [InlineData(NewYork, "2024-11-04T04:59:59Z", "2024-11-03T04:00:00Z")]
    [InlineData(NewYork, "2024-11-04T05:00:00Z", "2024-11-04T05:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-25T22:00:00Z", "2024-04-25T22:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-26T10:00:00Z", "2024-04-25T22:00:00Z")]
    public void LocalDayIsNotAlways24HoursLong(
        string zone,
        string instant,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new StartOfDayDatetimeProjection(
                    Instant(instant),
                    new LiteralString(zone)
                )
            )
        );
    }

    [Theory]
    [InlineData("2024-03-09", "86400")]
    [InlineData("2024-03-10", "82800")]
    [InlineData("2024-03-11", "86400")]
    [InlineData("2024-11-03", "90000")]
    public void RowsOfALocalDayAreThoseBetweenItsStartAndTheStartOfTheNextDay(
        string day,
        string seconds
    )
    {
        LiteralString zone = new(NewYork);

        Assert.Equal(
            seconds,
            Value(
                new DatetimeDiffSecondsDecimalProjection(
                    new AsDatetimeDateDatetimeProjection(
                        new DateAddDaysDateProjection(Date(day), new LiteralInteger(1)),
                        zone
                    ),
                    new AsDatetimeDateDatetimeProjection(Date(day), zone)
                )
            )
        );
    }

    [Theory]
    [InlineData("2024-10-27T00:30:00Z")]
    [InlineData("2024-10-27T01:30:00Z")]
    [InlineData("2024-10-27T00:00:00Z")]
    [InlineData("2024-10-27T01:59:59.999999Z")]
    public void BothPassesOfARepeatedLocalHourAreTheSameLocalHour(string instant)
    {
        Assert.Equal(
            "2024-10-27T00:00:00Z",
            Value(
                new StartOfHourDatetimeProjection(
                    Instant(instant),
                    new LiteralString("Europe/Berlin")
                )
            )
        );
    }

    [Fact]
    public void StartOfAnHourNextToARepeatedOneIsItsOwn()
    {
        LiteralString berlin = new("Europe/Berlin");

        Assert.Equal(
            "2024-10-26T23:00:00Z",
            Value(
                new StartOfHourDatetimeProjection(
                    Instant("2024-10-26T23:59:59Z"),
                    berlin
                )
            )
        );
        Assert.Equal(
            "2024-10-27T02:00:00Z",
            Value(
                new StartOfHourDatetimeProjection(
                    Instant("2024-10-27T02:30:00Z"),
                    berlin
                )
            )
        );
    }

    [Fact]
    public void LocalDayOfTheStartOfALocalDayIsThatDay()
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        FieldStringNullable zone = new("t.stores", "zone");

        Assert.Equal(
            [
                "2024-04-01",
                "2024-03-31",
                "2024-04-01",
                "",
                "2024-01-01",
                "2023-12-31",
                "2024-11-03",
            ],
            Values(
                Stores,
                "t.stores",
                new AsDateDatetimeDateNullableProjection(
                    new StartOfDayDatetimeNullableProjection(at, zone),
                    zone
                )
            )
        );
    }

    [Fact]
    public void AsDatetimeOfTheLocalDayIsTheStartOfThatLocalDay()
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        FieldStringNullable zone = new("t.stores", "zone");
        IReadOnlyList<string> starts =
        [
            "2024-03-31T21:00:00Z",
            "2024-03-31T04:00:00Z",
            "2024-03-31T18:15:00Z",
            "",
            "2023-12-31T21:00:00Z",
            "2023-12-31T05:00:00Z",
            "2024-11-03T04:00:00Z",
        ];

        Assert.Equal(
            starts,
            Values(
                Stores,
                "t.stores",
                new StartOfDayDatetimeNullableProjection(at, zone)
            )
        );
        Assert.Equal(
            starts,
            Values(
                Stores,
                "t.stores",
                new AsDatetimeDateDatetimeNullableProjection(
                    new AsDateDatetimeDateNullableProjection(at, zone),
                    zone
                )
            )
        );
    }

    [Theory]
    [InlineData(NewYork, "2024-01-31", "18:30:00", "2024-01-31T23:30:00Z")]
    [InlineData(NewYork, "2024-11-03", "01:30:00", "2024-11-03T05:30:00Z")]
    [InlineData(NewYork, "2024-03-10", "02:30:00", "2024-03-10T07:00:00Z")]
    [InlineData(NewYork, "2024-03-10", "01:59:59.999999", "2024-03-10T06:59:59.999999Z")]
    [InlineData(NewYork, "2024-03-10", "02:00:00", "2024-03-10T07:00:00Z")]
    [InlineData(NewYork, "2024-03-10", "02:59:59.999999", "2024-03-10T07:00:00Z")]
    [InlineData(NewYork, "2024-03-10", "03:00:00", "2024-03-10T07:00:00Z")]
    [InlineData(NewYork, "2024-03-10", "03:00:00.000001", "2024-03-10T07:00:00.000001Z")]
    [InlineData(NewYork, "2024-11-03", "00:59:59.999999", "2024-11-03T04:59:59.999999Z")]
    [InlineData(NewYork, "2024-11-03", "01:00:00", "2024-11-03T05:00:00Z")]
    [InlineData(NewYork, "2024-11-03", "01:59:59.999999", "2024-11-03T05:59:59.999999Z")]
    [InlineData(NewYork, "2024-11-03", "02:00:00", "2024-11-03T07:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-26", "00:00:00", "2024-04-25T22:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-26", "00:30:00", "2024-04-25T22:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-26", "01:00:00", "2024-04-25T22:00:00Z")]
    [InlineData("Pacific/Apia", "2011-12-30", "12:00:00", "2011-12-30T10:00:00Z")]
    [InlineData(Moscow, "2024-08-15", "21:30:15.25", "2024-08-15T18:30:15.25Z")]
    [InlineData(Kathmandu, "2024-08-16", "00:15:15.25", "2024-08-15T18:30:15.25Z")]
    public void DatetimeFromPartsIsTheFirstInstantAtWhichTheClocksReadTheTimeOrLater(
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

    [Theory]
    [InlineData(NewYork, "2024-03-10", "2024-03-10T05:00:00Z")]
    [InlineData(NewYork, "2024-03-11", "2024-03-11T04:00:00Z")]
    [InlineData(NewYork, "2024-11-03", "2024-11-03T04:00:00Z")]
    [InlineData(NewYork, "2024-11-04", "2024-11-04T05:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-26", "2024-04-25T22:00:00Z")]
    [InlineData("Pacific/Apia", "2011-12-30", "2011-12-30T10:00:00Z")]
    [InlineData("Pacific/Apia", "2011-12-31", "2011-12-30T10:00:00Z")]
    [InlineData("Asia/Tokyo", "2024-02-01", "2024-01-31T15:00:00Z")]
    [InlineData(Moscow, "2024-08-15", "2024-08-14T21:00:00Z")]
    [InlineData(Kathmandu, "2024-08-16", "2024-08-15T18:15:00Z")]
    public void AsDatetimeOfADateWithAZoneIsTheFirstInstantOfThatLocalDay(
        string zone,
        string date,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new AsDatetimeDateDatetimeProjection(Date(date), new LiteralString(zone))
            )
        );
        Assert.Equal(
            expected,
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    Date(date),
                    Time("00:00:00"),
                    new LiteralString(zone)
                )
            )
        );
    }

    [Fact]
    public void LocalDateAndTimeOfAnInstantGiveTheInstantAgain()
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        FieldStringNullable zone = new("t.stores", "zone");

        Assert.Equal(
            [
                "2024-03-31T22:30:00Z",
                "2024-03-31T22:30:00Z",
                "2024-03-31T22:30:00Z",
                "",
                "2023-12-31T21:30:15.25Z",
                "2024-01-01T04:59:59.999999Z",
                "2024-11-03T05:30:00Z",
            ],
            Values(
                Stores,
                "t.stores",
                new DatetimeFromPartsDatetimeNullableProjection(
                    new AsDateDatetimeDateNullableProjection(at, zone),
                    new AsTimeDatetimeTimeNullableProjection(at, zone),
                    zone
                )
            )
        );
    }

    [Fact]
    public void SecondPassOfARepeatedTimeGivesTheInstantOfTheFirstPass()
    {
        LiteralDatetime second = Instant("2024-11-03T06:30:00Z");
        LiteralString zone = new(NewYork);

        Assert.Equal(
            "2024-11-03T05:30:00Z",
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    new AsDateDatetimeDateProjection(second, zone),
                    new AsTimeDatetimeTimeProjection(second, zone),
                    zone
                )
            )
        );
    }

    [Fact]
    public void SameLocalTimeOnTheNextDayIsBuiltFromPartsNotBy86400Seconds()
    {
        LiteralDatetime noon = Instant("2024-03-09T17:00:00Z");
        LiteralString zone = new(NewYork);

        Assert.Equal(
            "2024-03-10T16:00:00Z",
            Value(
                new DatetimeFromPartsDatetimeProjection(
                    new DateAddDaysDateProjection(
                        new AsDateDatetimeDateProjection(noon, zone),
                        new LiteralInteger(1)
                    ),
                    new AsTimeDatetimeTimeProjection(noon, zone),
                    zone
                )
            )
        );
        Assert.Equal(
            "13:00:00",
            Value(
                new AsTimeDatetimeTimeProjection(
                    new DatetimeAddSecondsDatetimeProjection(
                        noon,
                        new LiteralInteger(86400)
                    ),
                    zone
                )
            )
        );
    }

    [Fact]
    public void LocalMidnightBeforeTheFirstInstantIsAnExecutionError()
    {
        PureQLProjection projection = Selecting(
            Visits,
            "t.visits",
            new AsDatetimeDateDatetimeProjection(
                Date("0001-01-01"),
                new LiteralString(Moscow)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void LocalTimeAfterTheLastInstantIsAnExecutionError()
    {
        PureQLProjection projection = Selecting(
            Visits,
            "t.visits",
            new DatetimeFromPartsDatetimeProjection(
                Date("9999-12-31"),
                Time("23:59:59"),
                new LiteralString(NewYork)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Theory]
    [InlineData("9999-12-31T23:00:00Z", "Asia/Tokyo")]
    [InlineData("0001-01-01T00:00:00Z", NewYork)]
    public void LocalDateOutsideTheYears0001To9999IsAnExecutionError(
        string instant,
        string zone
    )
    {
        PureQLProjection projection = Selecting(
            Visits,
            "t.visits",
            new AsDateDatetimeDateProjection(Instant(instant), new LiteralString(zone))
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Theory]
    [InlineData("2024-02-15T17:00:00Z", 1, null, "2024-03-15T17:00:00Z")]
    [InlineData("2024-02-15T17:00:00Z", 1, NewYork, "2024-03-15T16:00:00Z")]
    [InlineData("2024-03-30T22:00:00Z", 1, null, "2024-04-30T22:00:00Z")]
    [InlineData("2024-03-30T22:00:00Z", 1, Moscow, "2024-04-29T22:00:00Z")]
    [InlineData("2024-02-10T07:30:00Z", 1, NewYork, "2024-03-10T07:00:00Z")]
    [InlineData("2024-02-10T07:30:00Z", 1, null, "2024-03-10T07:30:00Z")]
    [InlineData("2024-03-15T16:00:00Z", -1, NewYork, "2024-02-15T17:00:00Z")]
    [InlineData("2024-08-15T18:30:15.25Z", 12, Kathmandu, "2025-08-15T18:30:15.25Z")]
    public void DatetimeAddMonthsWithAZoneMovesTheLocalDateAndKeepsTheLocalTime(
        string instant,
        long months,
        string? zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeAddMonthsDatetimeProjection(
                    Instant(instant),
                    new LiteralInteger(months),
                    zone is null ? null : (StringProjection?)new LiteralString(zone)
                )
            )
        );
    }

    [Fact]
    public void DatetimeAddMonthsWithTheZoneOfTheRowIsLiftedOverEveryOperand()
    {
        FieldDatetimeNullable at = new("t.renewals", "at");
        FieldStringNullable zone = new("t.renewals", "zone");
        FieldIntegerNullable months = new("t.renewals", "n");
        IReadOnlyList<string> expected =
        [
            "2024-03-15T16:00:00Z",
            "2024-04-29T22:00:00Z",
            "2024-03-10T07:00:00Z",
            "",
            "2024-03-15T17:00:00Z",
            "",
            "",
        ];

        Assert.Equal(
            expected,
            Values(
                Renewals,
                "t.renewals",
                new DatetimeAddMonthsDatetimeNullableProjection(at, months, zone)
            )
        );
        Assert.Equal(
            expected,
            Values(
                Renewals,
                "t.renewals",
                new DatetimeFromPartsDatetimeNullableProjection(
                    new DateAddMonthsDateNullableProjection(
                        new AsDateDatetimeDateNullableProjection(at, zone),
                        months
                    ),
                    new AsTimeDatetimeTimeNullableProjection(at, zone),
                    zone
                )
            )
        );
    }

    [Theory]
    [InlineData("2024-03-15T16:30:00Z", "2024-02-15T17:00:00Z", null, "0")]
    [InlineData("2024-03-15T16:30:00Z", "2024-02-15T17:00:00Z", NewYork, "1")]
    [InlineData("2024-03-15T16:00:00Z", "2024-02-15T17:00:00Z", NewYork, "1")]
    [InlineData("2024-03-15T15:59:59Z", "2024-02-15T17:00:00Z", NewYork, "0")]
    [InlineData("2024-02-15T17:00:00Z", "2024-03-15T16:00:00Z", NewYork, "-1")]
    [InlineData("2024-02-15T17:00:00Z", "2024-03-15T15:59:59Z", NewYork, "0")]
    [InlineData("2024-04-29T22:00:00Z", "2024-03-30T22:00:00Z", Moscow, "1")]
    [InlineData("2024-04-29T22:00:00Z", "2024-03-30T22:00:00Z", null, "0")]
    [InlineData("2024-03-10T07:00:00Z", "2024-02-10T07:30:00Z", NewYork, "1")]
    [InlineData("2024-03-10T06:59:59Z", "2024-02-10T07:30:00Z", NewYork, "0")]
    public void DatetimeDiffMonthsWithAZoneCountsMonthsOfTheLocalCalendar(
        string left,
        string right,
        string? zone,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new DatetimeDiffMonthsIntegerProjection(
                    Instant(left),
                    Instant(right),
                    zone is null ? null : (StringProjection?)new LiteralString(zone)
                )
            )
        );
    }

    [Fact]
    public void DatetimeDiffMonthsWithTheZoneOfTheRowIsLiftedOverEveryOperand()
    {
        FieldDatetimeNullable at = new("t.renewals", "at");
        FieldStringNullable zone = new("t.renewals", "zone");

        Assert.Equal(
            ["1", "0", "1", "", "0", "", "1"],
            Values(
                Renewals,
                "t.renewals",
                new DatetimeDiffMonthsIntegerNullableProjection(
                    Instant("2024-03-15T16:30:00Z"),
                    at,
                    zone
                )
            )
        );
    }

    [Fact]
    public void WholeMonthsBetweenTwoLocalDaysAreDateDiffMonthsOfAsDateWithTheZone()
    {
        LiteralString zone = new(Moscow);

        Assert.Equal(
            "1",
            Value(
                new DateDiffMonthsIntegerProjection(
                    new AsDateDatetimeDateProjection(
                        Instant("2024-04-29T21:00:00Z"),
                        zone
                    ),
                    new AsDateDatetimeDateProjection(
                        Instant("2024-03-30T22:00:00Z"),
                        zone
                    )
                )
            )
        );
    }

    [Fact]
    public void AsDatetimeAndDatetimeFromPartsWithTheZoneOfTheRowAreLifted()
    {
        FieldDateNullable day = new("t.days", "day");
        FieldTimeNullable clock = new("t.days", "clock");
        FieldStringNullable zone = new("t.days", "zone");

        Assert.Equal(
            [
                "2024-03-10T05:00:00Z",
                "2024-03-09T21:00:00Z",
                "",
                "",
                "2024-03-10T00:00:00Z",
            ],
            Values(
                Days,
                "t.days",
                new AsDatetimeDateDatetimeNullableProjection(day, zone)
            )
        );
        Assert.Equal(
            [
                "2024-03-10T07:00:00Z",
                "2024-03-09T23:30:00Z",
                "",
                "",
                "2024-03-10T02:30:00Z",
            ],
            Values(
                Days,
                "t.days",
                new DatetimeFromPartsDatetimeNullableProjection(day, clock, zone)
            )
        );
    }

    [Fact]
    public void LocalTimeFiltersRowsInWhere()
    {
        PureQLProjection projection = new(
            [Stores],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.stores"),
                where: new LessThanTimeRow(
                    new AsTimeDatetimeTimeNullableRow(
                        new FieldDatetimeNullable("t.stores", "at"),
                        new FieldStringNullable("t.stores", "zone")
                    ),
                    Time("18:00:00")
                ),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.stores", "id")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["1", "3", "5", "7"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void RevenueByLocalBusinessDayGroupsByAsDateWithAZone()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                groupBy:
                [
                    new GroupKey(
                        "day",
                        new AsDateDatetimeDateRow(
                            new FieldDatetime("t.orders", "at"),
                            new ParamString("zone")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemGroup("day", new KeyDate("day")),
                    new SelectItemGroup(
                        "revenue",
                        new SumIntegerGroup(new FieldInteger("t.orders", "total"))
                    ),
                    new SelectItemGroup(
                        "start",
                        new AsDatetimeDateDatetimeGroup(
                            new KeyDate("day"),
                            new ParamString("zone")
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("day")]
            ),
            Zone(NewYork)
        );

        Assert.Equal(
            ["2024-03-09", "2024-03-10", "2024-03-11"],
            ResultColumn.Texts(projection, "day")
        );
        Assert.Equal(["10", "50", "40"], ResultColumn.Texts(projection, "revenue"));
        Assert.Equal(
            ["2024-03-09T05:00:00Z", "2024-03-10T05:00:00Z", "2024-03-11T04:00:00Z"],
            ResultColumn.Texts(projection, "start")
        );
    }

    [Fact]
    public void GroupingByTheStartOfALocalDayGroupsByTheLocalDay()
    {
        LiteralString zone = new(NewYork);
        PureQLProjection projection = new(
            [Orders],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                groupBy:
                [
                    new GroupKey(
                        "start",
                        new StartOfDayDatetimeRow(
                            new FieldDatetime("t.orders", "at"),
                            zone
                        )
                    ),
                ],
                select:
                [
                    new SelectItemGroup("start", new KeyDatetime("start")),
                    new SelectItemGroup("orders", new CountGroup()),
                    new SelectItemGroup(
                        "day",
                        new AsDateDatetimeDateGroup(new KeyDatetime("start"), zone)
                    ),
                    new SelectItemGroup(
                        "last",
                        new HourDatetimeIntegerGroup(
                            new MaxDatetimeGroup(new FieldDatetime("t.orders", "at")),
                            zone
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("start")]
            )
        );

        Assert.Equal(
            ["2024-03-09T05:00:00Z", "2024-03-10T05:00:00Z", "2024-03-11T04:00:00Z"],
            ResultColumn.Texts(projection, "start")
        );
        Assert.Equal(["1", "2", "1"], ResultColumn.Texts(projection, "orders"));
        Assert.Equal(
            ["2024-03-09", "2024-03-10", "2024-03-11"],
            ResultColumn.Texts(projection, "day")
        );
        Assert.Equal(["23", "23", "0"], ResultColumn.Texts(projection, "last"));
    }

    [Fact]
    public void LocalDateAndTimeOfAnAggregateAreReadInATotalQuery()
    {
        FieldDatetimeNullable at = new("t.stores", "at");
        PureQLProjection projection = new(
            [Stores],
            new MainTotalQuery(
                from: new FromEntity("t.stores"),
                aggregate:
                [
                    new SelectItemTotal(
                        "day",
                        new AsDateDatetimeDateNullableTotal(
                            new MaxDatetimeNullableTotal(at),
                            new LiteralString(NewYork)
                        )
                    ),
                    new SelectItemTotal(
                        "hour",
                        new HourDatetimeIntegerNullableTotal(
                            new MinDatetimeNullableTotal(at),
                            new ParamString("zone")
                        )
                    ),
                    new SelectItemTotal(
                        "month",
                        new StartOfMonthDatetimeNullableTotal(
                            new MinDatetimeNullableTotal(at),
                            new ParamString("zone")
                        )
                    ),
                ]
            ),
            Zone(Moscow)
        );

        Assert.Equal(["2024-11-03"], ResultColumn.Texts(projection, "day"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "hour"));
        Assert.Equal(["2023-12-31T21:00:00Z"], ResultColumn.Texts(projection, "month"));
    }

    private static ExprProjection Reading(
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
            "quarter" => new QuarterDatetimeIntegerProjection(at, zone),
            "month" => new MonthDatetimeIntegerProjection(at, zone),
            "day" => new DayDatetimeIntegerProjection(at, zone),
            "dayOfWeek" => new DayOfWeekDatetimeIntegerProjection(at, zone),
            "dayOfYear" => new DayOfYearDatetimeIntegerProjection(at, zone),
            "week" => new WeekDatetimeIntegerProjection(at, zone),
            "weekYear" => new WeekYearDatetimeIntegerProjection(at, zone),
            "hour" => new HourDatetimeIntegerProjection(at, zone),
            "minute" => new MinuteDatetimeIntegerProjection(at, zone),
            "second" => new SecondDatetimeIntegerProjection(at, zone),
            "startOfYear" => new StartOfYearDatetimeProjection(at, zone),
            "startOfQuarter" => new StartOfQuarterDatetimeProjection(at, zone),
            "startOfMonth" => new StartOfMonthDatetimeProjection(at, zone),
            "startOfWeek" => new StartOfWeekDatetimeProjection(at, zone),
            "startOfDay" => new StartOfDayDatetimeProjection(at, zone),
            "startOfHour" => new StartOfHourDatetimeProjection(at, zone),
            "startOfMinute" => new StartOfMinuteDatetimeProjection(at, zone),
            _ => throw new ArgumentException(reading),
        };
    }

    private static ExprProjection NullableReading(
        string reading,
        DatetimeNullableProjection at,
        StringNullableProjection zone
    )
    {
        return reading switch
        {
            "asDate" => new AsDateDatetimeDateNullableProjection(at, zone),
            "asTime" => new AsTimeDatetimeTimeNullableProjection(at, zone),
            "year" => new YearDatetimeIntegerNullableProjection(at, zone),
            "quarter" => new QuarterDatetimeIntegerNullableProjection(at, zone),
            "month" => new MonthDatetimeIntegerNullableProjection(at, zone),
            "day" => new DayDatetimeIntegerNullableProjection(at, zone),
            "dayOfWeek" => new DayOfWeekDatetimeIntegerNullableProjection(at, zone),
            "dayOfYear" => new DayOfYearDatetimeIntegerNullableProjection(at, zone),
            "week" => new WeekDatetimeIntegerNullableProjection(at, zone),
            "weekYear" => new WeekYearDatetimeIntegerNullableProjection(at, zone),
            "hour" => new HourDatetimeIntegerNullableProjection(at, zone),
            "minute" => new MinuteDatetimeIntegerNullableProjection(at, zone),
            "second" => new SecondDatetimeIntegerNullableProjection(at, zone),
            "startOfYear" => new StartOfYearDatetimeNullableProjection(at, zone),
            "startOfQuarter" => new StartOfQuarterDatetimeNullableProjection(at, zone),
            "startOfMonth" => new StartOfMonthDatetimeNullableProjection(at, zone),
            "startOfWeek" => new StartOfWeekDatetimeNullableProjection(at, zone),
            "startOfDay" => new StartOfDayDatetimeNullableProjection(at, zone),
            "startOfHour" => new StartOfHourDatetimeNullableProjection(at, zone),
            "startOfMinute" => new StartOfMinuteDatetimeNullableProjection(at, zone),
            _ => throw new ArgumentException(reading),
        };
    }

    // The operators that take a local date and time, over the operands of
    // the specification's examples for America/New_York: the day 2024-03-10,
    // on which 02:30 never occurs, and noon of 2024-02-15, one month before
    // noon of 2024-03-15, which is an hour earlier in UTC.
    private static ExprProjection Building(string building, StringProjection? zone)
    {
        return building switch
        {
            "asDatetime" => new AsDatetimeDateDatetimeProjection(
                Date("2024-03-10"),
                zone
            ),
            "datetimeFromParts" => new DatetimeFromPartsDatetimeProjection(
                Date("2024-03-10"),
                Time("02:30:00"),
                zone
            ),
            "datetimeAddMonths" => new DatetimeAddMonthsDatetimeProjection(
                Instant("2024-02-15T17:00:00Z"),
                new LiteralInteger(1),
                zone
            ),
            "datetimeDiffMonths" => new DatetimeDiffMonthsIntegerProjection(
                Instant("2024-03-15T16:30:00Z"),
                Instant("2024-02-15T17:00:00Z"),
                zone
            ),
            _ => throw new ArgumentException(building),
        };
    }

    private static ExprProjection NullableBuilding(
        string building,
        StringNullableProjection zone
    )
    {
        return building switch
        {
            "asDatetime" => new AsDatetimeDateDatetimeNullableProjection(
                Date("2024-03-10"),
                zone
            ),
            "datetimeFromParts" => new DatetimeFromPartsDatetimeNullableProjection(
                Date("2024-03-10"),
                Time("02:30:00"),
                zone
            ),
            "datetimeAddMonths" => new DatetimeAddMonthsDatetimeNullableProjection(
                Instant("2024-02-15T17:00:00Z"),
                new LiteralInteger(1),
                zone
            ),
            "datetimeDiffMonths" => new DatetimeDiffMonthsIntegerNullableProjection(
                Instant("2024-03-15T16:30:00Z"),
                Instant("2024-02-15T17:00:00Z"),
                zone
            ),
            _ => throw new ArgumentException(building),
        };
    }

    private static ExprProjection DatePart(
        string part,
        DeclaresDateNullableProjection operand
    )
    {
        return part switch
        {
            "year" => new YearDateIntegerNullableProjection(operand),
            "quarter" => new QuarterDateIntegerNullableProjection(operand),
            "month" => new MonthDateIntegerNullableProjection(operand),
            "day" => new DayDateIntegerNullableProjection(operand),
            "dayOfWeek" => new DayOfWeekDateIntegerNullableProjection(operand),
            "dayOfYear" => new DayOfYearDateIntegerNullableProjection(operand),
            "week" => new WeekDateIntegerNullableProjection(operand),
            "weekYear" => new WeekYearDateIntegerNullableProjection(operand),
            _ => throw new ArgumentException(part),
        };
    }

    private static ExprProjection TimePart(
        string part,
        DeclaresTimeNullableProjection operand
    )
    {
        return part switch
        {
            "hour" => new HourTimeIntegerNullableProjection(operand),
            "minute" => new MinuteTimeIntegerNullableProjection(operand),
            "second" => new SecondTimeIntegerNullableProjection(operand),
            _ => throw new ArgumentException(part),
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

    // A table of zone names, one row each in this order; empty text is no
    // zone.
    private static IStoredSchemaDataSet Zones(params string[] zones)
    {
        return new InlineTable(
            "t",
            "zones",
            ("id", new LongColumnType()),
            ("zone", new StringColumnType())
        ).With([
            .. zones.Select(
                (zone, index) =>
                    new[] { index.ToString(CultureInfo.InvariantCulture), zone }
            ),
        ]);
    }

    private static Dictionary<string, IParameter> Zone(string zone)
    {
        return new Dictionary<string, IParameter>
        {
            ["zone"] = new StringParameter(new String(zone)),
        };
    }

    // The query that selects one expression for every row of a table, in the
    // order of its id.
    private static PureQLProjection Selecting(
        IStoredSchemaDataSet table,
        string entity,
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return new PureQLProjection(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(entity),
                select: [new SelectItemProjection("value", expression)],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger(entity, "id")),
                ]
            ),
            parameters ?? []
        );
    }

    private static IReadOnlyList<string> Values(
        IStoredSchemaDataSet table,
        string entity,
        ExprProjection expression
    )
    {
        return ResultColumn.Texts(Selecting(table, entity, expression), "value");
    }

    // The value of an expression that reads no row.
    private static string Value(
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return ResultColumn.Texts(
            Selecting(Visits, "t.visits", expression, parameters),
            "value"
        )[0];
    }
}
