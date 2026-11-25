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

// Calendar months in UTC, as the specification defines them under "Dates and
// times": dateAddMonths and datetimeAddMonths move the month and the year,
// keep the day and clamp it to the end of a shorter month, so adding months
// is neither reversible nor additive, and a year is 12 months; dateDiffMonths
// and datetimeDiffMonths count the whole months from right to left as the
// inverse of adding them, and swapping the operands only changes the sign.
public sealed class MonthsTests
{
    // Days at and near the ends of months, in a leap year and a common one.
    // Row 6 has no date and row 7 no number of months.
    private static readonly IStoredSchemaDataSet Days = new InlineTable(
        "t",
        "days",
        ("id", new LongColumnType()),
        ("d", new DateColumnType()),
        ("dt", new DateTimeColumnType()),
        ("n", new LongColumnType())
    ).With(
        ["1", "2024-01-31", "2024-01-31T18:30:15.25Z", "1"],
        ["2", "2024-02-29", "2024-02-29T23:59:59.999999Z", "12"],
        ["3", "2023-12-31", "2023-12-31T00:00:00Z", "-10"],
        ["4", "2024-03-30", "2024-03-30T12:00:00Z", "-1"],
        ["5", "2024-08-15", "2024-08-15T18:30:15.25Z", "0"],
        ["6", "", "", "1"],
        ["7", "2024-01-31", "2024-01-31T18:30:15.25Z", ""]
    );

    private static readonly FieldInteger Id = new("t.days", "id");

    private static readonly FieldDateNullable D = new("t.days", "d");

    private static readonly FieldDatetimeNullable Dt = new("t.days", "dt");

    private static readonly FieldIntegerNullable N = new("t.days", "n");

    [Theory]
    [InlineData("2024-01-31", 1, "2024-02-29")]
    [InlineData("2023-01-31", 1, "2023-02-28")]
    [InlineData("2024-01-31", 2, "2024-03-31")]
    [InlineData("2024-01-31", 3, "2024-04-30")]
    [InlineData("2024-02-29", 12, "2025-02-28")]
    [InlineData("2024-02-29", 48, "2028-02-29")]
    [InlineData("2024-02-29", -1, "2024-01-29")]
    [InlineData("2024-03-31", -1, "2024-02-29")]
    [InlineData("2024-01-31", -2, "2023-11-30")]
    [InlineData("2024-01-15", 1, "2024-02-15")]
    [InlineData("2024-11-30", 3, "2025-02-28")]
    [InlineData("2024-08-15", 0, "2024-08-15")]
    [InlineData("2024-08-15", -8, "2023-12-15")]
    public void DateAddMonthsKeepsTheDayAndClampsItToTheEndOfAShorterMonth(
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

    [Fact]
    public void AddingAMonthAndTakingItBackIsNotTheSameDay()
    {
        Assert.Equal(
            "2024-01-29",
            Value(
                new DateAddMonthsDateProjection(
                    new DateAddMonthsDateProjection(
                        Date("2024-01-31"),
                        new LiteralInteger(1)
                    ),
                    new LiteralInteger(-1)
                )
            )
        );
    }

    [Fact]
    public void AddingOneMonthTwiceIsNotAddingTwoMonths()
    {
        Assert.Equal(
            "2024-03-29",
            Value(
                new DateAddMonthsDateProjection(
                    new DateAddMonthsDateProjection(
                        Date("2024-01-31"),
                        new LiteralInteger(1)
                    ),
                    new LiteralInteger(1)
                )
            )
        );
        Assert.Equal(
            "2024-03-31",
            Value(
                new DateAddMonthsDateProjection(Date("2024-01-31"), new LiteralInteger(2))
            )
        );
    }

    [Fact]
    public void YearsAreAddedAsMonthsMultipliedBy12()
    {
        Assert.Equal(
            "2026-02-28",
            Value(
                new DateAddMonthsDateProjection(
                    Date("2024-02-29"),
                    new MultiplyIntegerProjection([
                        new LiteralInteger(2),
                        new LiteralInteger(12),
                    ])
                )
            )
        );
    }

    [Theory]
    [InlineData("9999-12-31", 1)]
    [InlineData("9999-01-31", 12)]
    [InlineData("0001-01-31", -1)]
    [InlineData("2024-01-31", -24277)]
    [InlineData("2024-01-31", 95712)]
    public void DateAddMonthsOutsideTheYears0001To9999IsAnExecutionError(
        string date,
        long months
    )
    {
        PureQLProjection projection = Selecting(
            new DateAddMonthsDateProjection(Date(date), new LiteralInteger(months))
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Theory]
    [InlineData("0001-01-31", -24276, "2024-01-31")]
    [InlineData("9999-12-31", 95711, "2024-01-31")]
    public void DateAddMonthsReachesTheEndsOfTheRangeOfDates(
        string expected,
        long months,
        string date
    )
    {
        Assert.Equal(
            expected,
            Value(new DateAddMonthsDateProjection(Date(date), new LiteralInteger(months)))
        );
    }

    [Fact]
    public void DateAddMonthsIsLiftedOverTheDateAndTheMonths()
    {
        Assert.Equal(
            [
                "2024-02-29",
                "2025-02-28",
                "2023-02-28",
                "2024-02-29",
                "2024-08-15",
                "",
                "",
            ],
            Values(new DateAddMonthsDateNullableProjection(D, N))
        );
    }

    [Theory]
    [InlineData("2024-01-31T18:30:15.25Z", 1, "2024-02-29T18:30:15.25Z")]
    [InlineData("2024-02-15T17:00:00Z", 1, "2024-03-15T17:00:00Z")]
    [InlineData("2024-03-30T22:00:00Z", 1, "2024-04-30T22:00:00Z")]
    [InlineData("2024-03-31T23:59:59.999999Z", -1, "2024-02-29T23:59:59.999999Z")]
    [InlineData("2024-02-29T00:00:00Z", 12, "2025-02-28T00:00:00Z")]
    [InlineData("2023-12-31T12:00:00Z", 2, "2024-02-29T12:00:00Z")]
    public void DatetimeAddMonthsMovesTheUtcDateAndKeepsTheTimeOfDay(
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

    [Theory]
    [InlineData("9999-12-31T00:00:00Z", 1)]
    [InlineData("0001-01-01T00:00:00Z", -1)]
    public void DatetimeAddMonthsOutsideTheYears0001To9999IsAnExecutionError(
        string instant,
        long months
    )
    {
        PureQLProjection projection = Selecting(
            new DatetimeAddMonthsDatetimeProjection(
                Instant(instant),
                new LiteralInteger(months)
            )
        );

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => projection.ToList());
    }

    [Fact]
    public void DatetimeAddMonthsIsLiftedOverTheInstantAndTheMonths()
    {
        Assert.Equal(
            [
                "2024-02-29T18:30:15.25Z",
                "2025-02-28T23:59:59.999999Z",
                "2023-02-28T00:00:00Z",
                "2024-02-29T12:00:00Z",
                "2024-08-15T18:30:15.25Z",
                "",
                "",
            ],
            Values(new DatetimeAddMonthsDatetimeNullableProjection(Dt, N))
        );
    }

    [Theory]
    [InlineData("2024-02-14", "2024-01-15", "0")]
    [InlineData("2024-02-15", "2024-01-15", "1")]
    [InlineData("2023-02-28", "2023-01-31", "1")]
    [InlineData("2024-02-28", "2024-01-31", "0")]
    [InlineData("2024-02-29", "2024-01-31", "1")]
    [InlineData("2025-02-28", "2024-02-29", "12")]
    [InlineData("2025-02-27", "2024-02-29", "11")]
    [InlineData("2024-08-15", "2024-08-15", "0")]
    [InlineData("2024-08-15", "2014-08-15", "120")]
    [InlineData("2024-01-15", "2024-02-14", "0")]
    [InlineData("2024-01-15", "2024-02-15", "-1")]
    [InlineData("2023-01-31", "2023-02-28", "-1")]
    [InlineData("2024-01-31", "2024-02-28", "0")]
    [InlineData("2024-01-31", "2024-02-29", "-1")]
    [InlineData("2024-02-29", "2025-02-28", "-12")]
    [InlineData("9999-12-31", "0001-01-01", "119987")]
    [InlineData("0001-01-01", "9999-12-31", "-119987")]
    public void DateDiffMonthsCountsWholeMonthsFromRightToLeft(
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

    [Theory]
    [InlineData(-25)]
    [InlineData(-13)]
    [InlineData(-12)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(25)]
    public void DateDiffMonthsIsTheInverseOfDateAddMonths(long months)
    {
        string expected = months.ToString(CultureInfo.InvariantCulture);

        Assert.Equal(
            [expected, expected, expected, expected, expected, "", expected],
            Values(
                new DateDiffMonthsIntegerNullableProjection(
                    new DateAddMonthsDateNullableProjection(
                        D,
                        new LiteralInteger(months)
                    ),
                    D
                )
            )
        );
    }

    [Theory]
    [InlineData("2025-02-27", "0")]
    [InlineData("2025-02-28", "1")]
    [InlineData("2028-02-28", "3")]
    [InlineData("2028-02-29", "4")]
    public void WholeYearsBetweenTwoDatesAreTheMonthsDividedBy12(
        string day,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new IntegerDivideIntegerProjection(
                    new DateDiffMonthsIntegerProjection(Date(day), Date("2024-02-29")),
                    new LiteralInteger(12)
                )
            )
        );
    }

    [Fact]
    public void DateDiffMonthsIsLiftedOverBothDates()
    {
        Assert.Equal(
            ["0", "1", "-1", "1", "6", "", "0"],
            Values(new DateDiffMonthsIntegerNullableProjection(D, Date("2024-01-31")))
        );
        Assert.Equal(
            ["0", "-1", "1", "-1", "-6", "", "0"],
            Values(new DateDiffMonthsIntegerNullableProjection(Date("2024-01-31"), D))
        );
    }

    [Theory]
    [InlineData("2024-02-15T11:59:59Z", "2024-01-15T12:00:00Z", "0")]
    [InlineData("2024-02-15T11:59:59.999999Z", "2024-01-15T12:00:00Z", "0")]
    [InlineData("2024-02-15T12:00:00Z", "2024-01-15T12:00:00Z", "1")]
    [InlineData("2024-03-15T16:30:00Z", "2024-02-15T17:00:00Z", "0")]
    [InlineData("2024-03-15T17:00:00Z", "2024-02-15T17:00:00Z", "1")]
    [InlineData("2024-01-15T12:00:00Z", "2024-02-15T11:59:59Z", "0")]
    [InlineData("2024-01-15T12:00:00Z", "2024-02-15T12:00:00Z", "-1")]
    [InlineData("2023-02-28T18:00:00Z", "2023-01-31T18:00:00Z", "1")]
    [InlineData("2023-02-28T17:59:59Z", "2023-01-31T18:00:00Z", "0")]
    public void DatetimeDiffMonthsCountsTheTimeOfDayInUtc(
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

    [Theory]
    [InlineData(-13)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    public void DatetimeDiffMonthsIsTheInverseOfDatetimeAddMonths(long months)
    {
        string expected = months.ToString(CultureInfo.InvariantCulture);

        Assert.Equal(
            [expected, expected, expected, expected, expected, "", expected],
            Values(
                new DatetimeDiffMonthsIntegerNullableProjection(
                    new DatetimeAddMonthsDatetimeNullableProjection(
                        Dt,
                        new LiteralInteger(months)
                    ),
                    Dt
                )
            )
        );
    }

    [Fact]
    public void DatetimeDiffMonthsIsLiftedOverBothInstants()
    {
        Assert.Equal(
            ["0", "1", "-1", "1", "6", "", "0"],
            Values(
                new DatetimeDiffMonthsIntegerNullableProjection(
                    Dt,
                    Instant("2024-01-31T18:30:15.25Z")
                )
            )
        );
        Assert.Equal(
            ["0", "-1", "1", "-1", "-6", "", "0"],
            Values(
                new DatetimeDiffMonthsIntegerNullableProjection(
                    Instant("2024-01-31T18:30:15.25Z"),
                    Dt
                )
            )
        );
    }

    [Fact]
    public void MonthsFilterRowsInWhere()
    {
        PureQLProjection projection = new(
            [Days],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.days"),
                where: new AndRow([
                    new GreaterThanOrEqualDecimalRow(
                        new DateDiffMonthsIntegerNullableRow(D, Date("2024-01-31")),
                        new LiteralInteger(1)
                    ),
                    new LessThanDatetimeRow(
                        new DatetimeAddMonthsDatetimeNullableRow(
                            Dt,
                            new LiteralInteger(-6)
                        ),
                        Instant("2024-01-01T00:00:00Z")
                    ),
                ]),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["2", "4"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void MonthsFromAFixedDayAreAGroupKeyAndMoveADateInAGroup()
    {
        PureQLProjection projection = new(
            [Days],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.days"),
                where: new NotEqualDateRow(D, new LiteralDateNullable()),
                groupBy:
                [
                    new GroupKey(
                        "age",
                        new DateDiffMonthsIntegerNullableRow(D, Date("2024-01-31"))
                    ),
                ],
                select:
                [
                    new SelectItemGroup("age", new KeyIntegerNullable("age")),
                    new SelectItemGroup("rows", new CountGroup()),
                    new SelectItemGroup(
                        "moved",
                        new DateAddMonthsDateNullableGroup(
                            new MinDateNullableGroup(D),
                            new KeyIntegerNullable("age")
                        )
                    ),
                    new SelectItemGroup(
                        "span",
                        new DatetimeDiffMonthsIntegerNullableGroup(
                            new MaxDatetimeNullableGroup(Dt, over: AggregateOver.All),
                            new MinDatetimeNullableGroup(Dt)
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("age")]
            )
        );

        Assert.Equal(["-1", "0", "1", "6"], ResultColumn.Texts(projection, "age"));
        Assert.Equal(["1", "2", "2", "1"], ResultColumn.Texts(projection, "rows"));
        Assert.Equal(
            ["2023-11-30", "2024-01-31", "2024-03-29", "2025-02-15"],
            ResultColumn.Texts(projection, "moved")
        );
        Assert.Equal(["7", "6", "5", "0"], ResultColumn.Texts(projection, "span"));
    }

    [Fact]
    public void MonthsBetweenAggregatesAreCountedInATotalQuery()
    {
        PureQLProjection projection = new(
            [Days],
            new MainTotalQuery(
                from: new FromEntity("t.days"),
                aggregate:
                [
                    new SelectItemTotal(
                        "span",
                        new DateDiffMonthsIntegerNullableTotal(
                            new MaxDateNullableTotal(D),
                            new MinDateNullableTotal(D)
                        )
                    ),
                    new SelectItemTotal(
                        "back",
                        new DatetimeDiffMonthsIntegerNullableTotal(
                            new MinDatetimeNullableTotal(Dt),
                            new MaxDatetimeNullableTotal(Dt)
                        )
                    ),
                    new SelectItemTotal(
                        "later",
                        new DateAddMonthsDateNullableTotal(
                            new MinDateNullableTotal(D),
                            new CountTotal()
                        )
                    ),
                    new SelectItemTotal(
                        "earlier",
                        new DatetimeAddMonthsDatetimeNullableTotal(
                            new MaxDatetimeNullableTotal(Dt),
                            new LiteralInteger(-6)
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["7"], ResultColumn.Texts(projection, "span"));
        Assert.Equal(["-7"], ResultColumn.Texts(projection, "back"));
        Assert.Equal(["2024-07-31"], ResultColumn.Texts(projection, "later"));
        Assert.Equal(
            ["2024-02-15T18:30:15.25Z"],
            ResultColumn.Texts(projection, "earlier")
        );
    }

    private static LiteralDate Date(string text)
    {
        return new LiteralDate(
            DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        );
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

    // The query that selects one expression for every row, in the order of id.
    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return new PureQLProjection(
            [Days],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.days"),
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
