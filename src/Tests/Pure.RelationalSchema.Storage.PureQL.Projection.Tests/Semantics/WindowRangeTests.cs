using System.Globalization;
using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Aggregates over a range of values: a row of the partition is in the window
// when its sort key k lies within the distances of the current row's key c,
// c - preceding <= k <= c + following for an ascending key, both bounds
// included, k = c on an absent side, and with a descending key preceding
// reaches towards greater values; "all" is every row on that side. The
// current row and its peers are always inside. Days and seconds are added
// as dateAddDays and datetimeAddSeconds add them, months as dateAddMonths
// and datetimeAddMonths do, the day clamped to the end of the month. A row
// with a null key has the rows with a null key as its window and is in the
// window of no other row; a bound outside the type is not an error. A
// distance parameter is read once: 0 leaves the rows with the same key, and
// a distance below 0 is rejected. Each row's v is a power of two, so a sum
// of v names the rows of the window; every column is read in the order of id.
public sealed class WindowRangeTests
{
    // group x: 1 (1), 2 (2, 3), 5 (4), 7 (6), null (5, 8); group y: 2 (7), 3 (9).
    private static readonly IStoredSchemaDataSet Points = new InlineTable(
        "t",
        "points",
        ("id", new LongColumnType()),
        ("grp", new StringColumnType()),
        ("k", new LongColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "x", "1", "1"],
        ["2", "x", "2", "2"],
        ["3", "x", "2", "4"],
        ["4", "x", "5", "8"],
        ["5", "x", "", "16"],
        ["6", "x", "7", "32"],
        ["7", "y", "2", "64"],
        ["8", "x", "", "128"],
        ["9", "y", "3", "256"]
    );

    private static readonly IStoredSchemaDataSet Limits = new InlineTable(
        "t",
        "limits",
        ("id", new LongColumnType()),
        ("k", new LongColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "-9223372036854775808", "1"],
        ["2", "-9223372036854775807", "2"],
        ["3", "0", "4"],
        ["4", "9223372036854775806", "8"],
        ["5", "9223372036854775807", "16"]
    );

    private static readonly IStoredSchemaDataSet Prices = new InlineTable(
        "t",
        "prices",
        ("id", new LongColumnType()),
        ("k", new DoubleColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "1", "1"],
        ["2", "1.5", "2"],
        ["3", "1.50", "4"],
        ["4", "2", "8"],
        ["5", "2.25", "16"],
        ["6", "4", "32"],
        ["7", "", "64"]
    );

    private static readonly IStoredSchemaDataSet Extremes = new InlineTable(
        "t",
        "extremes",
        ("id", new LongColumnType()),
        ("k", new DoubleColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "-9999999999999999999999999999", "1"],
        ["2", "0", "2"],
        ["3", "9999999999999999999999999999", "4"]
    );

    private static readonly IStoredSchemaDataSet Days = new InlineTable(
        "t",
        "days",
        ("id", new LongColumnType()),
        ("k", new DateColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "2024-01-31", "1"],
        ["2", "2024-02-28", "2"],
        ["3", "2024-02-29", "4"],
        ["4", "2024-03-01", "8"],
        ["5", "2024-03-30", "16"],
        ["6", "2024-03-31", "32"],
        ["7", "2024-03-31", "64"],
        ["8", "", "128"],
        ["9", "2024-04-30", "256"]
    );

    private static readonly IStoredSchemaDataSet Eras = new InlineTable(
        "t",
        "eras",
        ("id", new LongColumnType()),
        ("k", new DateColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "0001-01-01", "1"],
        ["2", "0001-01-03", "2"],
        ["3", "9999-12-29", "4"],
        ["4", "9999-12-31", "8"]
    );

    private static readonly IStoredSchemaDataSet Events = new InlineTable(
        "t",
        "events",
        ("id", new LongColumnType()),
        ("k", new DateTimeColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "2024-01-31T10:00:00Z", "1"],
        ["2", "2024-01-31T10:00:00.5Z", "2"],
        ["3", "2024-01-31T10:59:59Z", "4"],
        ["4", "2024-01-31T11:00:00Z", "8"],
        ["5", "2024-02-29T10:00:00Z", "16"],
        ["6", "2024-02-29T10:00:00Z", "32"],
        ["7", "", "64"],
        ["8", "2024-03-31T10:00:00Z", "128"]
    );

    private static readonly IStoredSchemaDataSet Ages = new InlineTable(
        "t",
        "ages",
        ("id", new LongColumnType()),
        ("k", new DateTimeColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "0001-01-01T00:00:01Z", "1"],
        ["2", "0001-01-01T00:00:03Z", "2"],
        ["3", "9999-12-31T23:59:56Z", "4"],
        ["4", "9999-12-31T23:59:58Z", "8"]
    );

    private static readonly DistanceDateDays OneDay = new(1L);

    private static readonly DistanceDateMonths OneMonth = new(1L);

    private const decimal AlmostThreeQuarters = 0.7499999999999999999999999999m;

    [Fact]
    public void IntegerPrecedingReachesThatFarBelowTheCurrentKeyBothBoundsIncluded()
    {
        Assert.Equal(
            ["1", "71", "71", "8", "144", "32", "71", "144", "326"],
            IntegerSums(new RangeInteger(preceding: 1L))
        );
    }

    [Fact]
    public void IntegerFollowingReachesThatFarAboveTheCurrentKey()
    {
        Assert.Equal(
            ["327", "326", "326", "40", "144", "32", "326", "144", "264"],
            IntegerSums(new RangeInteger(following: 2L))
        );
    }

    [Fact]
    public void IntegerRangeWithBothSides()
    {
        Assert.Equal(
            ["327", "327", "327", "40", "144", "32", "327", "144", "334"],
            IntegerSums(new RangeInteger(preceding: 1L, following: 2L))
        );
    }

    [Fact]
    public void AllPrecedingIsARunningTotalThatIncludesThePeers()
    {
        Assert.Equal(
            ["1", "71", "71", "335", "144", "367", "71", "144", "327"],
            IntegerSums(new RangeInteger(preceding: new All()))
        );
    }

    [Fact]
    public void AllFollowingIsEveryRowFromTheCurrentKeyOn()
    {
        Assert.Equal(
            ["367", "366", "366", "40", "144", "32", "366", "144", "296"],
            IntegerSums(new RangeInteger(following: new All()))
        );
    }

    [Fact]
    public void AllOnOneSideWithADistanceOnTheOther()
    {
        Assert.Equal(
            ["71", "327", "327", "335", "144", "367", "327", "144", "327"],
            IntegerSums(new RangeInteger(preceding: new All(), following: 1L))
        );
    }

    [Fact]
    public void DescendingPrecedingReachesTowardsGreaterValues()
    {
        Assert.Equal(
            ["71", "326", "326", "8", "144", "32", "326", "144", "256"],
            IntegerSums(new RangeInteger(preceding: 1L), SortDirection.Desc)
        );
    }

    [Fact]
    public void DescendingFollowingReachesTowardsLesserValues()
    {
        Assert.Equal(
            ["1", "71", "71", "264", "144", "40", "71", "144", "327"],
            IntegerSums(new RangeInteger(following: 2L), SortDirection.Desc)
        );
    }

    [Fact]
    public void DescendingAllPrecedingIsEveryRowOfAGreaterOrEqualKey()
    {
        Assert.Equal(
            ["367", "366", "366", "40", "144", "32", "366", "144", "296"],
            IntegerSums(new RangeInteger(preceding: new All()), SortDirection.Desc)
        );
    }

    [Fact]
    public void RangeStaysInsideThePartition()
    {
        Assert.Equal(
            ["1", "7", "7", "8", "144", "32", "64", "144", "320"],
            IntegerSums(new RangeInteger(preceding: 1L), partitioned: true)
        );
    }

    [Fact]
    public void NullKeyRowIsInTheWindowOfNoOtherRowEvenWithAllOnASide()
    {
        Assert.Equal(
            ["5", "5", "5", "5", "2", "5", "2", "2", "2"],
            Values(
                Points,
                Count(
                    IntegerWindow(
                        new RangeInteger(preceding: new All(), following: 100L),
                        partitioned: true
                    )
                ),
                "points"
            )
        );
    }

    [Fact]
    public void NullKeyRowIsInTheWindowOfNoOtherRowDescendingEither()
    {
        Assert.Equal(
            ["5", "5", "5", "5", "2", "5", "2", "2", "2"],
            Values(
                Points,
                Count(
                    IntegerWindow(
                        new RangeInteger(preceding: 100L, following: new All()),
                        SortDirection.Desc,
                        partitioned: true
                    )
                ),
                "points"
            )
        );
    }

    [Theory]
    [InlineData(0, "1,70,70,8,144,32,70,144,256")]
    [InlineData(1, "1,71,71,8,144,32,71,144,326")]
    [InlineData(2, "1,71,71,264,144,40,71,144,327")]
    [InlineData(6, "1,71,71,335,144,367,71,144,327")]
    [InlineData(long.MaxValue, "1,71,71,335,144,367,71,144,327")]
    public void IntegerPrecedingReadsItsDistanceFromAParameter(
        long distance,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            IntegerSums(
                new RangeInteger(preceding: new ParamInteger("distance")),
                parameters: Integer(distance)
            )
        );
    }

    [Theory]
    [InlineData(0, "1,70,70,8,144,32,70,144,256")]
    [InlineData(2, "327,326,326,40,144,32,326,144,264")]
    [InlineData(long.MaxValue, "367,366,366,40,144,32,366,144,296")]
    public void IntegerFollowingReadsItsDistanceFromAParameter(
        long distance,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            IntegerSums(
                new RangeInteger(following: new ParamInteger("distance")),
                parameters: Integer(distance)
            )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void IntegerRangeRejectsADistanceParameterBelowZero(long distance)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            IntegerSums(
                new RangeInteger(preceding: new ParamInteger("distance")),
                parameters: Integer(distance)
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            IntegerSums(
                new RangeInteger(
                    preceding: 1L,
                    following: new ParamInteger("distance")
                ),
                parameters: Integer(distance)
            )
        );
    }

    [Fact]
    public void IntegerBoundBelowSixtyFourBitsReachesTheStartOfThePartition()
    {
        Assert.Equal(
            ["1", "3", "4", "8", "24"],
            Sums(
                Limits,
                "limits",
                new WindowWithoutFrameWithRangeInteger(
                    [new OrderItemIntegerRow(Key("limits"))],
                    new RangeInteger(preceding: 5L)
                )
            )
        );
    }

    [Fact]
    public void IntegerBoundAboveSixtyFourBitsReachesTheEndOfThePartition()
    {
        Assert.Equal(
            ["3", "2", "4", "24", "16"],
            Sums(
                Limits,
                "limits",
                new WindowWithoutFrameWithRangeInteger(
                    [new OrderItemIntegerRow(Key("limits"))],
                    new RangeInteger(following: 5L)
                )
            )
        );
    }

    [Fact]
    public void IntegerDistanceWiderThanSixtyFourBitsOfKeysIsEveryRowOnThatSide()
    {
        Assert.Equal(
            ["1", "3", "6", "12", "28"],
            Sums(
                Limits,
                "limits",
                new WindowWithoutFrameWithRangeInteger(
                    [new OrderItemIntegerRow(Key("limits"))],
                    new RangeInteger(preceding: new ParamInteger("distance"))
                ),
                Integer(long.MaxValue)
            )
        );
    }

    [Fact]
    public void DecimalRangeTakesAWholeNumber()
    {
        Assert.Equal(
            ["1", "7", "7", "15", "30", "32", "64"],
            DecimalSums(new RangeDecimal(preceding: 1L))
        );
    }

    [Fact]
    public void DecimalRangeTakesADecimalLiteralAndPeersOfAnotherScaleAreInside()
    {
        Assert.Equal(
            ["1", "7", "7", "14", "24", "32", "64"],
            DecimalSums(new RangeDecimal(preceding: new LiteralDecimal(0.5m)))
        );
    }

    [Fact]
    public void DecimalFollowingReachesAboveTheCurrentKey()
    {
        Assert.Equal(
            ["1", "6", "6", "24", "16", "32", "64"],
            DecimalSums(new RangeDecimal(following: new LiteralDecimal(0.25m)))
        );
    }

    [Fact]
    public void DecimalRangeWithBothSides()
    {
        Assert.Equal(
            ["1", "7", "7", "30", "24", "32", "64"],
            DecimalSums(
                new RangeDecimal(
                    preceding: new LiteralDecimal(0.5m),
                    following: new LiteralDecimal(0.25m)
                )
            )
        );
    }

    [Fact]
    public void DecimalAllPrecedingIsARunningTotalThatIncludesThePeers()
    {
        Assert.Equal(
            ["1", "7", "7", "15", "31", "63", "64"],
            DecimalSums(new RangeDecimal(preceding: new All()))
        );
    }

    [Fact]
    public void DecimalDescendingPrecedingReachesTowardsGreaterValues()
    {
        Assert.Equal(
            ["7", "14", "14", "24", "16", "32", "64"],
            DecimalSums(
                new RangeDecimal(preceding: new LiteralDecimal(0.5m)),
                SortDirection.Desc
            )
        );
    }

    // A distance is never computed into a bound (Window evaluation): a key
    // just beyond a distance of 28 significant digits is outside the window,
    // although the bound key -/+ distance needs 29 digits, which rounding to
    // 28 would move onto that key (#293). 2.25 - 0.7499999999999999999999999999
    // is 1.5000000000000000000000000001, so the rows of 1.5 are outside;
    // 1.5 + 0.7499999999999999999999999999 is 2.2499999999999999999999999999,
    // so the row of 2.25 is outside.
    [Fact(
        Skip = "Bug #293: a decimal range bound key -/+ distance is rounded to 28 "
            + "digits onto the next key, which the window then takes in"
    )]
    public void DecimalPrecedingJustBelowAKeyLeavesThatKeyOutside()
    {
        Assert.Equal(
            ["1", "7", "7", "14", "24", "32", "64"],
            DecimalSums(new RangeDecimal(preceding: new LiteralDecimal(AlmostThreeQuarters)))
        );
    }

    [Fact(
        Skip = "Bug #293: a decimal range bound key -/+ distance is rounded to 28 "
            + "digits onto the next key, which the window then takes in"
    )]
    public void DecimalFollowingJustBelowAKeyLeavesThatKeyOutside()
    {
        Assert.Equal(
            ["7", "14", "14", "24", "16", "32", "64"],
            DecimalSums(new RangeDecimal(following: new LiteralDecimal(AlmostThreeQuarters)))
        );
    }

    [Fact(
        Skip = "Bug #293: a decimal range bound key -/+ distance is rounded to 28 "
            + "digits onto the next key, which the window then takes in"
    )]
    public void DecimalDescendingPrecedingJustBelowAKeyLeavesThatKeyOutside()
    {
        Assert.Equal(
            ["7", "14", "14", "24", "16", "32", "64"],
            DecimalSums(
                new RangeDecimal(preceding: new LiteralDecimal(AlmostThreeQuarters)),
                SortDirection.Desc
            )
        );
    }

    [Fact(
        Skip = "Bug #293: a decimal range bound key -/+ distance is rounded to 28 "
            + "digits onto the next key, which the window then takes in"
    )]
    public void DecimalParameterDistanceJustBelowAKeyLeavesThatKeyOutside()
    {
        Assert.Equal(
            ["1", "7", "7", "14", "24", "32", "64"],
            DecimalSums(
                new RangeDecimal(preceding: new ParamDecimal("distance")),
                parameters: Exact("0.7499999999999999999999999999")
            )
        );
    }

    [Theory]
    [InlineData("0", "1,6,6,8,16,32,64")]
    [InlineData("0.25", "1,6,6,8,24,32,64")]
    [InlineData("0.5", "1,7,7,14,24,32,64")]
    [InlineData("0.75", "1,7,7,14,30,32,64")]
    [InlineData("9999999999999999999999999999", "1,7,7,15,31,63,64")]
    public void DecimalRangeReadsItsDistanceFromADecimalParameter(
        string distance,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            DecimalSums(
                new RangeDecimal(preceding: new ParamDecimal("distance")),
                parameters: Exact(distance)
            )
        );
    }

    [Fact]
    public void DecimalParameterDistanceMayBeBoundToAnInteger()
    {
        Assert.Equal(
            ["1", "7", "7", "15", "30", "32", "64"],
            DecimalSums(
                new RangeDecimal(preceding: new ParamDecimal("distance")),
                parameters: Integer(1)
            )
        );
    }

    [Theory]
    [InlineData(0, "1,6,6,8,16,32,64")]
    [InlineData(1, "15,30,30,24,16,32,64")]
    [InlineData(2, "31,30,30,56,48,32,64")]
    public void DecimalRangeReadsItsDistanceFromAnIntegerParameter(
        long distance,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            DecimalSums(
                new RangeDecimal(following: new ParamInteger("distance")),
                parameters: Integer(distance)
            )
        );
    }

    [Theory]
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("-0.5")]
    [InlineData("-1")]
    public void DecimalRangeRejectsADecimalDistanceParameterBelowZero(string distance)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DecimalSums(
                new RangeDecimal(preceding: new ParamDecimal("distance")),
                parameters: Exact(distance)
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            DecimalSums(
                new RangeDecimal(following: new ParamDecimal("distance")),
                parameters: Exact(distance)
            )
        );
    }

    [Fact]
    public void DecimalRangeRejectsAnIntegerDistanceParameterBelowZero()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DecimalSums(
                new RangeDecimal(preceding: new ParamInteger("distance")),
                parameters: Integer(-1)
            )
        );
    }

    [Fact]
    public void DecimalBoundOutsideTheTypeIsNotAnError()
    {
        Assert.Equal(
            ["3", "7", "6"],
            Sums(
                Extremes,
                "extremes",
                new WindowWithoutFrameWithRangeDecimal(
                    [new OrderItemDecimalRow(new FieldDecimal("t.extremes", "k"))],
                    new RangeDecimal(
                        preceding: new LiteralDecimal(9999999999999999999999999999m),
                        following: new LiteralDecimal(9999999999999999999999999999m)
                    )
                )
            )
        );
    }

    [Fact]
    public void DaysPrecedingReachThatManyDaysBeforeTheCurrentDate()
    {
        Assert.Equal(
            ["1", "2", "6", "12", "16", "112", "112", "128", "256"],
            DateSums(new RangeDate(preceding: OneDay))
        );
    }

    [Fact]
    public void TwentyNineDaysPrecedingIsAWindowOfThirtyDays()
    {
        Assert.Equal(
            ["1", "3", "7", "14", "24", "112", "112", "128", "256"],
            DateSums(new RangeDate(preceding: new DistanceDateDays(29L)))
        );
    }

    [Fact]
    public void DaysFollowingReachThatManyDaysAfterTheCurrentDate()
    {
        Assert.Equal(
            ["1", "6", "12", "8", "112", "96", "96", "128", "256"],
            DateSums(new RangeDate(following: OneDay))
        );
    }

    [Fact]
    public void MonthsPrecedingStartAtTheCurrentDateMinusTheMonthsClampedToTheMonthEnd()
    {
        Assert.Equal(
            ["1", "3", "7", "14", "28", "124", "124", "128", "368"],
            DateSums(new RangeDate(preceding: OneMonth))
        );
    }

    [Fact]
    public void TwoMonthsPrecedingStartTwoMonthsBeforeTheCurrentDate()
    {
        Assert.Equal(
            ["1", "3", "7", "15", "31", "127", "127", "128", "380"],
            DateSums(new RangeDate(preceding: new DistanceDateMonths(2L)))
        );
    }

    [Fact]
    public void MonthsFollowingEndAtTheCurrentDatePlusTheMonthsClampedToTheMonthEnd()
    {
        Assert.Equal(
            ["7", "14", "12", "120", "368", "352", "352", "128", "256"],
            DateSums(new RangeDate(following: OneMonth))
        );
    }

    [Fact]
    public void DateRangeMixesMonthsOnOneSideAndDaysOnTheOther()
    {
        Assert.Equal(
            ["1", "7", "15", "14", "124", "124", "124", "128", "368"],
            DateSums(new RangeDate(preceding: OneMonth, following: OneDay))
        );
    }

    [Fact]
    public void DateAllPrecedingIsARunningTotalThatIncludesThePeers()
    {
        Assert.Equal(
            ["1", "3", "7", "15", "31", "127", "127", "128", "383"],
            DateSums(new RangeDate(preceding: new All()))
        );
    }

    [Fact]
    public void DateAllFollowingWithDaysPreceding()
    {
        Assert.Equal(
            ["383", "382", "382", "380", "368", "368", "368", "128", "256"],
            DateSums(new RangeDate(preceding: OneDay, following: new All()))
        );
    }

    [Fact]
    public void DescendingDaysPrecedingReachTowardsLaterDates()
    {
        Assert.Equal(
            ["1", "6", "12", "8", "112", "96", "96", "128", "256"],
            DateSums(new RangeDate(preceding: OneDay), SortDirection.Desc)
        );
    }

    [Fact]
    public void DescendingMonthsPrecedingEndAtTheCurrentDatePlusTheMonths()
    {
        Assert.Equal(
            ["7", "14", "12", "120", "368", "352", "352", "128", "256"],
            DateSums(new RangeDate(preceding: OneMonth), SortDirection.Desc)
        );
    }

    [Fact]
    public void DescendingMonthsFollowingStartAtTheCurrentDateMinusTheMonths()
    {
        Assert.Equal(
            ["1", "3", "7", "14", "28", "124", "124", "128", "368"],
            DateSums(new RangeDate(following: OneMonth), SortDirection.Desc)
        );
    }

    [Theory]
    [InlineData(0, "1,2,4,8,16,96,96,128,256")]
    [InlineData(1, "1,2,6,12,16,112,112,128,256")]
    [InlineData(29, "1,3,7,14,24,112,112,128,256")]
    [InlineData(long.MaxValue, "1,3,7,15,31,127,127,128,383")]
    public void DaysReadTheirNumberFromAParameter(long days, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            DateSums(
                new RangeDate(
                    preceding: new DistanceDateDays(new ParamInteger("distance"))
                ),
                parameters: Integer(days)
            )
        );
    }

    [Theory]
    [InlineData(0, "1,2,4,8,16,96,96,128,256")]
    [InlineData(1, "7,14,12,120,368,352,352,128,256")]
    [InlineData(long.MaxValue, "383,382,380,376,368,352,352,128,256")]
    public void MonthsReadTheirNumberFromAParameter(long months, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            DateSums(
                new RangeDate(
                    following: new DistanceDateMonths(new ParamInteger("distance"))
                ),
                parameters: Integer(months)
            )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void DateRangeRejectsADistanceParameterBelowZero(long distance)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DateSums(
                new RangeDate(
                    preceding: new DistanceDateDays(new ParamInteger("distance"))
                ),
                parameters: Integer(distance)
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            DateSums(
                new RangeDate(
                    following: new DistanceDateMonths(new ParamInteger("distance"))
                ),
                parameters: Integer(distance)
            )
        );
    }

    [Fact]
    public void DateBoundBeforeTheFirstDateReachesTheStartOfThePartition()
    {
        Assert.Equal(
            ["1", "3", "4", "12"],
            EraSums(new RangeDate(preceding: new DistanceDateDays(5L)))
        );
        Assert.Equal(["1", "3", "4", "12"], EraSums(new RangeDate(preceding: OneMonth)));
    }

    [Fact]
    public void DateBoundAfterTheLastDateReachesTheEndOfThePartition()
    {
        Assert.Equal(
            ["3", "2", "12", "8"],
            EraSums(new RangeDate(following: new DistanceDateDays(5L)))
        );
        Assert.Equal(["3", "2", "12", "8"], EraSums(new RangeDate(following: OneMonth)));
    }

    [Fact]
    public void SecondsPrecedingReachThatManySecondsBeforeTheCurrentInstant()
    {
        Assert.Equal(
            ["1", "3", "7", "15", "48", "48", "64", "128"],
            DatetimeSums(new RangeDatetime(preceding: new DistanceDatetimeSeconds(3600L)))
        );
    }

    [Fact]
    public void SecondsPrecedingLeaveOutTheRowOneSecondTooEarly()
    {
        Assert.Equal(
            ["1", "3", "7", "12", "48", "48", "64", "128"],
            DatetimeSums(new RangeDatetime(preceding: new DistanceDatetimeSeconds(3599L)))
        );
    }

    [Fact]
    public void SecondsFollowingReachThatManySecondsAfterTheCurrentInstant()
    {
        Assert.Equal(
            ["3", "2", "12", "8", "48", "48", "64", "128"],
            DatetimeSums(new RangeDatetime(following: new DistanceDatetimeSeconds(1L)))
        );
    }

    [Theory]
    [InlineData("0.5", "1,3,4,8,48,48,64,128")]
    [InlineData("0.499999", "1,2,4,8,48,48,64,128")]
    [InlineData("3599.5", "1,3,7,14,48,48,64,128")]
    public void SecondsTakeADecimalLiteral(string seconds, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            DatetimeSums(
                new RangeDatetime(
                    preceding: new DistanceDatetimeSeconds(
                        new LiteralDecimal(
                            decimal.Parse(seconds, CultureInfo.InvariantCulture)
                        )
                    )
                )
            )
        );
    }

    [Theory]
    [InlineData(0, "1,2,4,8,48,48,64,128")]
    [InlineData(3600, "1,3,7,15,48,48,64,128")]
    [InlineData(long.MaxValue, "1,3,7,15,63,63,64,191")]
    public void SecondsReadTheirNumberFromAnIntegerParameter(
        long seconds,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            DatetimeSums(
                new RangeDatetime(
                    preceding: new DistanceDatetimeSeconds(new ParamInteger("distance"))
                ),
                parameters: Integer(seconds)
            )
        );
    }

    [Theory]
    [InlineData("0", "1,2,4,8,48,48,64,128")]
    [InlineData("0.5", "3,2,4,8,48,48,64,128")]
    [InlineData("3599.5", "7,14,12,8,48,48,64,128")]
    [InlineData("9999999999999999999999999999", "191,190,188,184,176,176,64,128")]
    public void SecondsReadTheirNumberFromADecimalParameter(
        string seconds,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            DatetimeSums(
                new RangeDatetime(
                    following: new DistanceDatetimeSeconds(new ParamDecimal("distance"))
                ),
                parameters: Exact(seconds)
            )
        );
    }

    [Fact]
    public void DatetimeMonthsPrecedingKeepTheTimeOfDayAndClampTheDay()
    {
        Assert.Equal(
            ["1", "3", "7", "15", "63", "63", "64", "176"],
            DatetimeSums(new RangeDatetime(preceding: new DistanceDatetimeMonths(1L)))
        );
    }

    [Fact]
    public void DatetimeMonthsFollowingEndAtTheSameTimeOfDayAMonthLater()
    {
        Assert.Equal(
            ["63", "62", "60", "56", "48", "48", "64", "128"],
            DatetimeSums(new RangeDatetime(following: new DistanceDatetimeMonths(1L)))
        );
    }

    [Theory]
    [InlineData(0, "1,2,4,8,48,48,64,128")]
    [InlineData(1, "1,3,7,15,63,63,64,176")]
    [InlineData(2, "1,3,7,15,63,63,64,191")]
    [InlineData(long.MaxValue, "1,3,7,15,63,63,64,191")]
    public void DatetimeMonthsReadTheirNumberFromAParameter(long months, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            DatetimeSums(
                new RangeDatetime(
                    preceding: new DistanceDatetimeMonths(new ParamInteger("distance"))
                ),
                parameters: Integer(months)
            )
        );
    }

    [Fact]
    public void DatetimeAllFollowingIsEveryRowFromTheCurrentInstantOn()
    {
        Assert.Equal(
            ["191", "190", "188", "184", "176", "176", "64", "128"],
            DatetimeSums(new RangeDatetime(following: new All()))
        );
    }

    [Fact]
    public void DescendingSecondsPrecedingReachTowardsLaterInstants()
    {
        Assert.Equal(
            ["3", "2", "12", "8", "48", "48", "64", "128"],
            DatetimeSums(
                new RangeDatetime(preceding: new DistanceDatetimeSeconds(1L)),
                SortDirection.Desc
            )
        );
    }

    [Fact]
    public void DatetimeRangeRejectsADistanceParameterBelowZero()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DatetimeSums(
                new RangeDatetime(
                    preceding: new DistanceDatetimeSeconds(new ParamInteger("distance"))
                ),
                parameters: Integer(-1)
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            DatetimeSums(
                new RangeDatetime(
                    preceding: new DistanceDatetimeSeconds(new ParamDecimal("distance"))
                ),
                parameters: Exact("-0.000001")
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            DatetimeSums(
                new RangeDatetime(
                    following: new DistanceDatetimeMonths(new ParamInteger("distance"))
                ),
                parameters: Integer(-1)
            )
        );
    }

    [Fact]
    public void SecondsBoundBeforeTheFirstInstantReachesTheStartOfThePartition()
    {
        Assert.Equal(
            ["1", "3", "4", "12"],
            AgeSums(new RangeDatetime(preceding: new DistanceDatetimeSeconds(5L)))
        );
    }

    [Fact]
    public void SecondsBoundAfterTheLastInstantReachesTheEndOfThePartition()
    {
        Assert.Equal(
            ["3", "2", "12", "8"],
            AgeSums(new RangeDatetime(following: new DistanceDatetimeSeconds(5L)))
        );
    }

    [Fact]
    public void DatetimeMonthsBoundBeforeTheFirstInstantReachesTheStartOfThePartition()
    {
        Assert.Equal(
            ["1", "3", "4", "12"],
            AgeSums(new RangeDatetime(preceding: new DistanceDatetimeMonths(1L)))
        );
    }

    [Fact]
    public void DatetimeMonthsBoundAfterTheLastInstantReachesTheEndOfThePartition()
    {
        Assert.Equal(
            ["3", "2", "12", "8"],
            AgeSums(new RangeDatetime(following: new DistanceDatetimeMonths(1L)))
        );
    }

    [Fact]
    public void CountOverARangeIsTheNumberOfRowsWithinTheDistances()
    {
        Assert.Equal(
            ["1", "4", "4", "1", "2", "1", "4", "2", "4"],
            Values(Points, Count(IntegerWindow(new RangeInteger(preceding: 1L))), "points")
        );
    }

    [Fact]
    public void PredicateFiltersTheRowsOfTheRangeAndDoesNotMoveIt()
    {
        Assert.Equal(
            ["0", "64", "64", "0", "0", "0", "64", "0", "320"],
            Values(
                Points,
                new SumIntegerProjection(
                    new FieldInteger("t.points", "v"),
                    predicate: new EqualStringRow(
                        new FieldString("t.points", "grp"),
                        new LiteralString("y")
                    ),
                    over: IntegerWindow(new RangeInteger(preceding: 1L))
                ),
                "points"
            )
        );
    }

    [Fact]
    public void AverageOverARangeOfANonNullSelectorIsNeverNull()
    {
        Assert.Equal(
            ["1", "17.75", "17.75", "8", "72", "32", "17.75", "72", "81.5"],
            Values(
                Points,
                new AverageDecimalProjection(
                    new FieldInteger("t.points", "v"),
                    over: IntegerWindow(new RangeInteger(preceding: 1L))
                ),
                "points"
            )
        );
    }

    [Fact]
    public void MinAndMaxOverARangeReadTheRowsWithinTheDistances()
    {
        Assert.Equal(
            ["1", "1", "1", "8", "16", "32", "1", "16", "2"],
            Values(
                Points,
                new MinIntegerProjection(
                    new FieldInteger("t.points", "v"),
                    over: IntegerWindow(new RangeInteger(preceding: 1L))
                ),
                "points"
            )
        );
        Assert.Equal(
            ["64", "256", "256", "8", "128", "32", "256", "128", "256"],
            Values(
                Points,
                new MaxIntegerProjection(
                    new FieldInteger("t.points", "v"),
                    over: IntegerWindow(new RangeInteger(following: 1L))
                ),
                "points"
            )
        );
    }

    [Fact]
    public void FirstOverARangeSortsItsRowsByItsOwnOrder()
    {
        Assert.Equal(
            ["1", "7", "7", "4", "8", "6", "7", "8", "9"],
            Values(
                Points,
                new FirstIntegerProjection(
                    new FieldInteger("t.points", "id"),
                    [
                        new OrderItemRow(
                            new FieldInteger("t.points", "id"),
                            SortDirection.Desc
                        ),
                    ],
                    over: IntegerWindow(new RangeInteger(preceding: 1L))
                ),
                "points"
            )
        );
    }

    [Fact]
    public void RangeMeasuresTheRowsLeftByWhere()
    {
        Assert.Equal(
            ["1", "7", "7", "8", "144", "32", "144"],
            Values(
                Points,
                new SumIntegerProjection(
                    new FieldInteger("t.points", "v"),
                    over: IntegerWindow(new RangeInteger(preceding: 1L))
                ),
                "points",
                where: new EqualStringRow(
                    new FieldString("t.points", "grp"),
                    new LiteralString("x")
                )
            )
        );
    }

    private static FieldIntegerNullable Key(string table)
    {
        return new FieldIntegerNullable($"t.{table}", "k");
    }

    private static CountProjection Count(Window over)
    {
        return new CountProjection(over: over);
    }

    private static WindowWithoutFrameWithRangeInteger IntegerWindow(
        RangeInteger range,
        SortDirection direction = SortDirection.Asc,
        bool partitioned = false
    )
    {
        return new WindowWithoutFrameWithRangeInteger(
            [new OrderItemIntegerRow(Key("points"), direction)],
            range,
            partitioned ? [new FieldString("t.points", "grp")] : null
        );
    }

    private static IReadOnlyList<string> IntegerSums(
        RangeInteger range,
        SortDirection direction = SortDirection.Asc,
        bool partitioned = false,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Sums(
            Points,
            "points",
            IntegerWindow(range, direction, partitioned),
            parameters
        );
    }

    private static IReadOnlyList<string> DecimalSums(
        RangeDecimal range,
        SortDirection direction = SortDirection.Asc,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Sums(
            Prices,
            "prices",
            new WindowWithoutFrameWithRangeDecimal(
                [
                    new OrderItemDecimalRow(
                        new FieldDecimalNullable("t.prices", "k"),
                        direction
                    ),
                ],
                range
            ),
            parameters
        );
    }

    private static IReadOnlyList<string> DateSums(
        RangeDate range,
        SortDirection direction = SortDirection.Asc,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Sums(
            Days,
            "days",
            new WindowWithoutFrameWithRangeDate(
                [new OrderItemDateRow(new FieldDateNullable("t.days", "k"), direction)],
                range
            ),
            parameters
        );
    }

    private static IReadOnlyList<string> EraSums(RangeDate range)
    {
        return Sums(
            Eras,
            "eras",
            new WindowWithoutFrameWithRangeDate(
                [new OrderItemDateRow(new FieldDate("t.eras", "k"))],
                range
            )
        );
    }

    private static IReadOnlyList<string> DatetimeSums(
        RangeDatetime range,
        SortDirection direction = SortDirection.Asc,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Sums(
            Events,
            "events",
            new WindowWithoutFrameWithRangeDatetime(
                [
                    new OrderItemDatetimeRow(
                        new FieldDatetimeNullable("t.events", "k"),
                        direction
                    ),
                ],
                range
            ),
            parameters
        );
    }

    private static IReadOnlyList<string> AgeSums(RangeDatetime range)
    {
        return Sums(
            Ages,
            "ages",
            new WindowWithoutFrameWithRangeDatetime(
                [new OrderItemDatetimeRow(new FieldDatetime("t.ages", "k"))],
                range
            )
        );
    }

    private static Dictionary<string, IParameter> Integer(long distance)
    {
        return new Dictionary<string, IParameter>
        {
            ["distance"] = new IntegerParameter(new Long(distance)),
        };
    }

    private static Dictionary<string, IParameter> Exact(string distance)
    {
        return new Dictionary<string, IParameter>
        {
            ["distance"] = new DecimalParameter(
                new Decimal(decimal.Parse(distance, CultureInfo.InvariantCulture))
            ),
        };
    }

    private static IReadOnlyList<string> Sums(
        IStoredSchemaDataSet table,
        string name,
        Window over,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Values(
            table,
            new SumIntegerProjection(new FieldInteger($"t.{name}", "v"), over: over),
            name,
            parameters
        );
    }

    private static IReadOnlyList<string> Values(
        IStoredSchemaDataSet table,
        ExprProjection expression,
        string name,
        Dictionary<string, IParameter>? parameters = null,
        BooleanRow? where = null
    )
    {
        PureQLProjection projection = new(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity($"t.{name}"),
                where: where,
                select: [new SelectItemProjection("v", expression)],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldInteger($"t.{name}", "id")
                    ),
                ]
            ),
            parameters ?? []
        );

        return ResultColumn.Texts(projection, "v");
    }
}
