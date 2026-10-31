using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// A date, time or datetime parameter bound to date math, such as now-10d (the
// current and offset parameters of
// Pure.RelationalSchema.Storage.PureQL.Projection.Parameters write it), has its
// value at the instant a run starts, read from the projection's clock
// once per run: the UTC instant truncated to the microsecond, moved by
// calendar months (years and months together, the day clamped), then days,
// hours, minutes and seconds; a date is its UTC date, a time its UTC time of
// day, wrapping around midnight.
public sealed class DateMathParameterTests
{
    private const string Thursday = "2026-10-29T08:30:15.1234567Z";

    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    private static readonly InlineTable Orders = new(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("placed_on", new DateColumnType())
    );

    private static readonly string[][] OrderRows =
    [
        ["1", "2026-10-01"],
        ["2", "2026-10-20"],
        ["3", "2026-10-28"],
    ];

    [Theory]
    [InlineData(Thursday, "now", "2026-10-29")]
    [InlineData(Thursday, "now-10d", "2026-10-19")]
    [InlineData(Thursday, "now+3d", "2026-11-01")]
    [InlineData(Thursday, "now-1M", "2026-09-29")]
    [InlineData(Thursday, "now-1M-5d", "2026-09-24")]
    [InlineData(Thursday, "now+1y", "2027-10-29")]
    [InlineData(Thursday, "now-1y-1M-1d", "2025-09-28")]
    [InlineData(Thursday, "now-0d", "2026-10-29")]
    [InlineData("2026-03-31T12:00:00Z", "now-1M", "2026-02-28")]
    [InlineData("2024-02-29T12:00:00Z", "now+1y", "2025-02-28")]
    [InlineData("2024-01-31T12:00:00Z", "now+1y+1M", "2025-02-28")]
    [InlineData("2024-02-29T12:00:00Z", "now+1y+1M", "2025-03-29")]
    [InlineData("2026-10-29T23:30:00-02:00", "now", "2026-10-30")]
    public void DateIsTheUtcDateOfNowMovedByTheParts(
        string now,
        string dateMath,
        string date
    )
    {
        PureQLProjection projection = Selecting(
            FixedClock.At(now),
            new TextParameter("date", dateMath),
            new SelectItemProjectionDate("v", new DateProjection(new ParamDate("v")))
        );

        Assert.Equal([date], ResultColumn.Texts(projection, "v"));
    }

    [Theory]
    [InlineData("now", "08:30:15.123456")]
    [InlineData("now-30m", "08:00:15.123456")]
    [InlineData("now-1h-30m", "07:00:15.123456")]
    [InlineData("now-9h", "23:30:15.123456")]
    [InlineData("now+16h", "00:30:15.123456")]
    [InlineData("now-25h", "07:30:15.123456")]
    [InlineData("now+1441m", "08:31:15.123456")]
    [InlineData("now-86401s", "08:30:14.123456")]
    [InlineData("now+1h+29m+45s", "10:00:00.123456")]
    public void TimeIsTheUtcTimeOfDayOfNowWrappingAroundMidnight(
        string dateMath,
        string time
    )
    {
        PureQLProjection projection = Selecting(
            FixedClock.At(Thursday),
            new TextParameter("time", dateMath),
            new SelectItemProjectionTime("v", new TimeProjection(new ParamTime("v")))
        );

        Assert.Equal([time], ResultColumn.Texts(projection, "v"));
    }

    [Theory]
    [InlineData("now", "2026-10-29T08:30:15.123456")]
    [InlineData("now-7d", "2026-10-22T08:30:15.123456")]
    [InlineData("now-2h", "2026-10-29T06:30:15.123456")]
    [InlineData("now+90s", "2026-10-29T08:31:45.123456")]
    [InlineData("now-1M-1d-1h-1m-1s", "2026-09-28T07:29:14.123456")]
    [InlineData("now-1y", "2025-10-29T08:30:15.123456")]
    [InlineData("now-9h", "2026-10-28T23:30:15.123456")]
    public void DatetimeIsTheUtcInstantOfNowMovedByTheParts(
        string dateMath,
        string datetime
    )
    {
        PureQLProjection projection = Selecting(
            FixedClock.At(Thursday),
            new TextParameter("datetime", dateMath),
            new SelectItemProjectionDatetime(
                "v",
                new DatetimeProjection(new ParamDatetime("v"))
            )
        );

        Assert.Equal([datetime], ResultColumn.Texts(projection, "v"));
    }

    [Fact]
    public void EveryRunHasTheValueAtTheInstantItStarts()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = Selecting(
            clock,
            new TextParameter("date", "now-10d"),
            new SelectItemProjectionDate("v", new DateProjection(new ParamDate("v")))
        );

        Assert.Equal(["2026-10-19"], ResultColumn.Texts(projection, "v"));
        clock.Now = clock.Now.AddDays(3);
        Assert.Equal(["2026-10-22"], ResultColumn.Texts(projection, "v"));
    }

    [Fact]
    public void RunReadsTheClockOnceForAllItsDateMath()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = Selecting(
            clock,
            new Dictionary<string, IParameter>
            {
                ["from"] = new TextParameter("date", "now-7d"),
                ["to"] = new TextParameter("date", "now"),
                ["at"] = new TextParameter("time", "now-1h"),
                ["fixed"] = new TextParameter("date", "2026-10-01"),
            },
            new SelectItemProjectionDate(
                "from",
                new DateProjection(new ParamDate("from"))
            ),
            new SelectItemProjectionDate("to", new DateProjection(new ParamDate("to"))),
            new SelectItemProjectionTime("at", new TimeProjection(new ParamTime("at"))),
            new SelectItemProjectionDate(
                "fixed",
                new DateProjection(new ParamDate("fixed"))
            )
        );
        int built = clock.Reads;

        IRow row = Assert.Single(projection.ToList());

        Assert.Equal(built + 1, clock.Reads);
        Assert.Equal(["2026-10-22"], ResultColumn.Texts([row], "from"));
        Assert.Equal(["2026-10-29"], ResultColumn.Texts([row], "to"));
        Assert.Equal(["07:30:15.123456"], ResultColumn.Texts([row], "at"));
        Assert.Equal(["2026-10-01"], ResultColumn.Texts([row], "fixed"));
    }

    [Fact]
    public void RunWithoutDateMathDoesNotReadTheClock()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = Selecting(
            clock,
            new TextParameter("date", "2026-10-01"),
            new SelectItemProjectionDate("v", new DateProjection(new ParamDate("v")))
        );

        Assert.Equal(["2026-10-01"], ResultColumn.Texts(projection, "v"));
        Assert.Equal(0, clock.Reads);
    }

    [Fact]
    public void TreeHoldsDateMathAsACapturedVariable()
    {
        PureQLProjection projection = PlacedSince(
            [Orders.With(OrderRows)],
            FixedClock.At(Thursday)
        );

        Assert.Contains(
            "@since",
            TreeText.Of(projection.Expression),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ProviderIsGivenTheValueAtTheInstantEachRunStarts()
    {
        RecordingProvider provider = new();
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = PlacedSince(
            [ProvidedTable.Schema(Orders.With(OrderRows), provider)],
            clock
        );

        Assert.Equal(["2", "3"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        clock.Now = clock.Now.AddDays(5);
        Assert.Equal(["3"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(2, provider.Queries.Count);
    }

    [Fact]
    public void InMemoryRunHasTheValueAtTheInstantItStarts()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = PlacedSince([Orders.With(OrderRows)], clock);

        Assert.Equal(["2", "3"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        clock.Now = clock.Now.AddDays(5);
        Assert.Equal(["3"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void QueryComposedOverTheProjectionHasTheValueAtTheInstantItRuns()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = PlacedSince([Orders.With(OrderRows)], clock);

        Assert.Equal(2, projection.Count());
        clock.Now = clock.Now.AddDays(5);
        Assert.Equal(1, projection.Count());
    }

    [Fact]
    public void DateMathBindsANullableParameter()
    {
        PureQLProjection projection = Selecting(
            FixedClock.At(Thursday),
            new TextParameter("date", "now-1d"),
            new SelectItemProjectionDateNullable(
                "v",
                new ExactDateNullableProjection(new ParamDateNullable("v"))
            )
        );

        Assert.Equal(["2026-10-28"], ResultColumn.Texts(projection, "v"));
    }

    [Fact]
    public void StringParameterStartingWithNowIsItsText()
    {
        PureQLProjection projection = Selecting(
            FixedClock.At(Thursday),
            new TextParameter("string", "now-1d"),
            new SelectItemProjectionString(
                "v",
                new StringProjection(new ParamString("v"))
            )
        );

        Assert.Equal(["now-1d"], ResultColumn.Texts(projection, "v"));
    }

    [Fact]
    public void ProjectionWithoutAClockUsesTheSystemClock()
    {
        DateOnly before = DateOnly.FromDateTime(DateTime.UtcNow);
        PureQLProjection projection = new(
            [One],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.one")),
                    [
                        Item(
                            new SelectItemProjectionDate(
                                "v",
                                new DateProjection(new ParamDate("v"))
                            )
                        ),
                    ]
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["v"] = new TextParameter("date", "now"),
            }
        );

        DateOnly today = DateOnly.Parse(ResultColumn.Texts(projection, "v")[0], null);

        Assert.InRange(today, before, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private static PureQLProjection Selecting(
        TimeProvider clock,
        IParameter value,
        object column
    )
    {
        return Selecting(
            clock,
            new Dictionary<string, IParameter> { ["v"] = value },
            column
        );
    }

    private static PureQLProjection Selecting(
        TimeProvider clock,
        IReadOnlyDictionary<string, IParameter> values,
        params object[] columns
    )
    {
        return new PureQLProjection(
            [One],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.one")),
                    [.. columns.Select(Item)]
                )
            ),
            values,
            clock
        );
    }

    // The ids of the orders placed since now-10d.
    private static PureQLProjection PlacedSince(
        IEnumerable<IStoredSchemaDataSet> datasets,
        TimeProvider clock
    )
    {
        return new PureQLProjection(
            datasets,
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.orders", "o")),
                    [
                        Item(
                            new SelectItemProjectionInteger(
                                "id",
                                new IntegerProjection(new FieldInteger("o", "id"))
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanOrEqualRow(
                                new GreaterThanOrEqualDateRow(
                                    new DateNullableRow(
                                        new DateRow(new FieldDate("o", "placed_on"))
                                    ),
                                    new DateNullableRow(
                                        new DateRow(new ParamDate("since"))
                                    )
                                )
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["since"] = new TextParameter("date", "now-10d"),
            },
            clock
        );
    }

    private static SelectItemProjection Item(object column)
    {
        return column switch
        {
            SelectItemProjectionInteger item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionString item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDate item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionTime item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDatetime item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDateNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            _ => throw new ArgumentException($"{column} is not a select item."),
        };
    }
}
