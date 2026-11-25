using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The clocks of every time zone of the specification's list, against Python
// zoneinfo. TimeZoneVectors.txt holds, around changes of the clocks of each
// zone from 1970 to 2023 and at ordinary instants, the local date and time of
// an instant, the start of its local day and hour, and the instant of a local
// date and time by the rule "the first instant at which the local date and
// time are the given ones or later", each computed by a reference that knows
// every change of the zone. It is written by
// tools/generate_time_zone_vectors.py and assumes the rules of release 2026e
// of the IANA time zone database for those years.
public sealed class ZoneClockTests
{
    private const long Tick = 1234567;

    private static readonly IReadOnlyList<(
        string Zone,
        char Kind,
        long[] Seconds
    )> Vectors = Read();

    [Fact]
    public void EveryZoneOfTheListHasVectorsAndIsAccepted()
    {
        string[] zones = [.. Vectors.Select(vector => vector.Zone).Distinct()];

        Assert.Equal(444, zones.Length);
        Assert.All(zones, zone => Assert.True(PureQLTime.IsZone(zone)));
        Assert.True(Vectors.Count > 8000);
    }

    [Fact]
    public void TheLocalDateAndTimeOfAnInstantAreThoseOfTheDatabase()
    {
        List<string> wrong = [];

        foreach ((string zone, char _, long[] seconds) in Of('T'))
        {
            DateTime instant = Moment(seconds[0]).AddTicks(Tick);
            DateTime local = Moment(seconds[1]).AddTicks(Tick);

            if (
                PureQLTime.LocalDate(instant, zone) != DateOnly.FromDateTime(local)
                || PureQLTime.LocalTime(instant, zone) != TimeOnly.FromDateTime(local)
            )
            {
                wrong.Add($"{zone} {seconds[0]}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void TheStartOfALocalDayIsItsFirstInstant()
    {
        List<string> wrong = [];

        foreach ((string zone, char _, long[] seconds) in Of('T'))
        {
            DateTime start = PureQLTime.StartOfDay(Moment(seconds[0]), zone);

            if (start != Moment(seconds[2]) || start.Kind != DateTimeKind.Utc)
            {
                wrong.Add($"{zone} {seconds[0]} {start:O}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void TheStartOfALocalHourIsItsFirstInstant()
    {
        List<string> wrong = [];

        foreach ((string zone, char _, long[] seconds) in Of('T'))
        {
            DateTime start = PureQLTime.StartOfHour(Moment(seconds[0]), zone);

            if (start != Moment(seconds[3]))
            {
                wrong.Add($"{zone} {seconds[0]} {start:O}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void TheInstantOfALocalTimeIsTheFirstAtWhichTheClocksReadItOrLater()
    {
        List<string> wrong = [];

        foreach ((string zone, char _, long[] seconds) in Of('F'))
        {
            DateTime local = Moment(seconds[0]);
            DateTime instant = PureQLTime.DatetimeFromParts(
                DateOnly.FromDateTime(local),
                TimeOnly.FromDateTime(local),
                zone
            );

            if (instant != Moment(seconds[1]) || instant.Kind != DateTimeKind.Utc)
            {
                wrong.Add($"{zone} {seconds[0]} {instant:O}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void AnInstantReachedAtALocalTimeReadsThatTimeOrALaterOne()
    {
        List<string> wrong = [];

        foreach ((string zone, char _, long[] seconds) in Of('F'))
        {
            DateTime local = Moment(seconds[0]).AddTicks(Tick);
            DateTime instant = PureQLTime.DatetimeFromParts(
                DateOnly.FromDateTime(local),
                TimeOnly.FromDateTime(local),
                zone
            );
            DateTime read = PureQLTime
                .LocalDate(instant, zone)
                .ToDateTime(PureQLTime.LocalTime(instant, zone));
            DateTime before = instant.AddTicks(-1);
            DateTime earlier = PureQLTime
                .LocalDate(before, zone)
                .ToDateTime(PureQLTime.LocalTime(before, zone));

            if (read < local || earlier >= local)
            {
                wrong.Add($"{zone} {seconds[0]} {instant:O}");
            }
        }

        Assert.Empty(wrong);
    }

    [Theory]
    [InlineData("America/New_York", "2024-03-09T00:00:00Z")]
    [InlineData("America/New_York", "2024-11-02T00:00:00Z")]
    [InlineData("Europe/Berlin", "2024-10-26T00:00:00Z")]
    [InlineData("Africa/Cairo", "2024-04-25T00:00:00Z")]
    [InlineData("Australia/Lord_Howe", "2023-03-31T00:00:00Z")]
    [InlineData("Australia/Lord_Howe", "2023-09-29T00:00:00Z")]
    [InlineData("Pacific/Apia", "2011-12-28T00:00:00Z")]
    [InlineData("Pacific/Kiritimati", "1994-12-29T00:00:00Z")]
    [InlineData("Pacific/Kwajalein", "1993-08-19T00:00:00Z")]
    [InlineData("Asia/Kathmandu", "1985-12-30T00:00:00Z")]
    [InlineData("America/Caracas", "2007-12-08T00:00:00Z")]
    [InlineData("Asia/Pyongyang", "2015-08-13T00:00:00Z")]
    [InlineData("America/Sao_Paulo", "2018-02-16T00:00:00Z")]
    [InlineData("Asia/Beirut", "2023-10-27T00:00:00Z")]
    [InlineData("Antarctica/Troll", "2023-03-24T00:00:00Z")]
    [InlineData("UTC", "2024-03-09T00:00:00Z")]
    public void ALaterLocalTimeNeverGivesAnEarlierInstant(string zone, string from)
    {
        DateTime local = PureQLConversions.AsDatetime(from);
        DateTime last = PureQLTime.DatetimeFromParts(
            DateOnly.FromDateTime(local),
            TimeOnly.FromDateTime(local),
            zone
        );
        bool changed = false;

        for (int i = 0; i < 1500; i++)
        {
            local = local.AddTicks((TimeSpan.TicksPerMinute * 3) + 7);

            DateTime instant = PureQLTime.DatetimeFromParts(
                DateOnly.FromDateTime(local),
                TimeOnly.FromDateTime(local),
                zone
            );

            Assert.True(instant >= last, $"{zone} {local:O}");
            changed |= instant - last != TimeSpan.FromTicks(1800000007);
            last = instant;
        }

        // Every sweep but the one in UTC crosses a change of the clocks.
        Assert.Equal(zone != "UTC", changed);
    }

    private static IEnumerable<(string Zone, char Kind, long[] Seconds)> Of(char kind)
    {
        return Vectors.Where(vector => vector.Kind == kind);
    }

    private static DateTime Moment(long seconds)
    {
        return new DateTime(
            DateTime.UnixEpoch.Ticks + (seconds * TimeSpan.TicksPerSecond),
            DateTimeKind.Utc
        );
    }

    private static List<(string Zone, char Kind, long[] Seconds)> Read()
    {
        Type type = typeof(ZoneClockTests);
        string name = type
            .Assembly.GetManifestResourceNames()
            .Single(resource =>
                resource.EndsWith("TimeZoneVectors.txt", StringComparison.Ordinal)
            );
        using Stream stream = type.Assembly.GetManifestResourceStream(name)!;
        using StreamReader reader = new(stream);
        List<(string Zone, char Kind, long[] Seconds)> vectors = [];
        string zone = string.Empty;

        while (reader.ReadLine() is string line)
        {
            if (line.StartsWith('Z'))
            {
                zone = line[2..];
            }
            else if (!line.StartsWith('#'))
            {
                vectors.Add(
                    (
                        zone,
                        line[0],
                        [
                            .. line[2..]
                                .Split(' ')
                                .Select(number =>
                                    long.Parse(number, CultureInfo.InvariantCulture)
                                ),
                        ]
                    )
                );
            }
        }

        return vectors;
    }
}
