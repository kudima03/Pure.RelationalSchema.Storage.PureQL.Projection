namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The rules and the examples of the specification's Aggregates, Aggregate
// evaluation and Ordering and equality sections. A column is written as text,
// values separated by spaces, and a pair as first:second; a regression pair
// is y:x.
public sealed class PureQLAggregatesTests
{
    // The pairs (1, 2), (2, 4), (3, 5), (4, 9) of the specification.
    private const string Pairs = "1:2 2:4 3:5 4:9";

    // The same pairs for a regression, with x as x and y as y: y comes first.
    private const string Line = "2:1 4:2 5:3 9:4";

    private const string Large = "9000000000000000000000000000";

    [Theory]
    [InlineData("9223372036854775807 1 -1")]
    [InlineData("1 9223372036854775807 -1")]
    [InlineData("-1 1 9223372036854775807")]
    [InlineData("1 -1 9223372036854775807")]
    public void SumOfIntegersIsTheSameWhateverTheOrderOfTheRows(string values)
    {
        long? sum = PureQLAggregates.Sum(AggregateVectors.Integers(values));

        Assert.Equal(long.MaxValue, sum);
    }

    [Theory]
    [InlineData("9223372036854775807 1")]
    [InlineData("-9223372036854775808 -1")]
    [InlineData("9223372036854775807 9223372036854775807 9223372036854775807")]
    public void SumOfIntegersBeyondSixtyFourBitsIsAnError(string values)
    {
        long?[] column = AggregateVectors.Integers(values);

        _ = Assert.Throws<OverflowException>(() => PureQLAggregates.Sum(column));
    }

    [Theory]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("-9223372036854775808 9223372036854775807 null", -1L)]
    [InlineData("5 null 7", 12L)]
    public void SumOfIntegersSkipsNullValues(string values, long expected)
    {
        Assert.Equal(expected, PureQLAggregates.Sum(AggregateVectors.Integers(values)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("null null null")]
    public void SumOfNoValuesIsZero(string values)
    {
        Assert.Equal(0L, PureQLAggregates.Sum(AggregateVectors.Integers(values)));
        Assert.Equal(0m, PureQLAggregates.Sum(AggregateVectors.Decimals(values)));
    }

    [Theory]
    [InlineData(Large + " " + Large + " -" + Large)]
    [InlineData("-" + Large + " " + Large + " " + Large)]
    [InlineData(Large + " -" + Large + " " + Large)]
    public void SumOfDecimalsIsNotCheckedAtAPartialSum(string values)
    {
        decimal? sum = PureQLAggregates.Sum(AggregateVectors.Decimals(values));

        Assert.Equal(Large, AggregateVectors.Text(sum));
    }

    [Theory]
    [InlineData("9999999999999999999999999999 1")]
    [InlineData("-9999999999999999999999999999 -0.5 -0.5")]
    [InlineData(Large + " " + Large)]
    public void SumOfDecimalsOfMagnitudeTenToTheTwentyEighthIsAnError(string values)
    {
        decimal?[] column = AggregateVectors.Decimals(values);

        _ = Assert.Throws<OverflowException>(() => PureQLAggregates.Sum(column));
    }

    [Theory]
    [InlineData("0.1 0.2", "0.3")]
    [InlineData("1.50 1.50", "3")]
    [InlineData("1 null 0.0000000000000000000000000001", "1")]
    [InlineData(
        "0.1 null 0.0000000000000000000000000001",
        "0.1000000000000000000000000001"
    )]
    [InlineData(
        "9999999999999999999999999999 -0.0000000000000000000000000001 null",
        "9999999999999999999999999999"
    )]
    [InlineData("-0.0", "0")]
    public void SumOfDecimalsIsExactAndRoundedOnce(string values, string expected)
    {
        decimal? sum = PureQLAggregates.Sum(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(sum));
    }

    [Fact]
    public void AverageOfTwoValuesWhoseSumIsBeyondTheRangeIsTheirMean()
    {
        decimal? average = PureQLAggregates.Average(
            AggregateVectors.Decimals(Large + " " + Large)
        );

        Assert.Equal(Large, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData("1 null 3", "2")]
    [InlineData("1 2", "1.5")]
    [InlineData("1 0 0", "0.3333333333333333333333333333")]
    [InlineData("2 0 0", "0.6666666666666666666666666667")]
    [InlineData("-2 0 0", "-0.6666666666666666666666666667")]
    [InlineData("0.0000000000000000000000000001 0", "0.0000000000000000000000000001")]
    [InlineData("-0.0000000000000000000000000001 0", "-0.0000000000000000000000000001")]
    [InlineData("7", "7")]
    public void AverageIsTheSumDividedAsDivideDivides(string values, string expected)
    {
        decimal? average = PureQLAggregates.Average(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("null null")]
    public void WithNoValuesEveryAggregateOfOneColumnButSumIsNull(string values)
    {
        decimal?[] column = AggregateVectors.Decimals(values);

        Assert.Null(PureQLAggregates.Average(column));
        Assert.Null(PureQLAggregates.Median(column));
        Assert.Null(PureQLAggregates.PercentileContinuous(column, 0.9m));
        Assert.Null(PureQLAggregates.PercentileDiscrete(column, 0.9m));
        Assert.Null(PureQLAggregates.Mode(column));
        Assert.Null(PureQLAggregates.VariancePopulation(column));
        Assert.Null(PureQLAggregates.VarianceSample(column));
        Assert.Null(PureQLAggregates.StandardDeviationPopulation(column));
        Assert.Null(PureQLAggregates.StandardDeviationSample(column));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("null null")]
    public void WithNoValuesEveryTemporalAggregateIsNull(string values)
    {
        DateOnly?[] dates = AggregateVectors.Dates(values);
        TimeOnly?[] times = AggregateVectors.Times(values);
        DateTime?[] instants = AggregateVectors.Instants(values);

        Assert.Null(PureQLAggregates.Average(dates));
        Assert.Null(PureQLAggregates.Average(times));
        Assert.Null(PureQLAggregates.Average(instants));
        Assert.Null(PureQLAggregates.Median(dates));
        Assert.Null(PureQLAggregates.Median(times));
        Assert.Null(PureQLAggregates.Median(instants));
        Assert.Null(PureQLAggregates.PercentileContinuous(dates, 0.9m));
        Assert.Null(PureQLAggregates.PercentileContinuous(times, 0.9m));
        Assert.Null(PureQLAggregates.PercentileContinuous(instants, 0.9m));
        Assert.Null(PureQLAggregates.PercentileDiscrete(dates, 0.9m));
        Assert.Null(PureQLAggregates.Mode(times));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null:null")]
    [InlineData("1:null null:2")]
    public void WithNoPairsEveryStatisticOfTwoColumnsIsNull(string pairs)
    {
        (decimal?, decimal?)[] rows = AggregateVectors.Pairs(pairs);

        Assert.Null(PureQLAggregates.CovariancePopulation(rows));
        Assert.Null(PureQLAggregates.CovarianceSample(rows));
        Assert.Null(PureQLAggregates.Correlation(rows));
        Assert.Null(PureQLAggregates.RegressionSlope(rows));
        Assert.Null(PureQLAggregates.RegressionIntercept(rows));
        Assert.Null(PureQLAggregates.RegressionRSquared(rows));
    }

    [Theory]
    [InlineData("1969-12-30 1969-12-31", "1969-12-31")]
    [InlineData("2024-01-01 2024-01-02", "2024-01-02")]
    [InlineData("0001-01-01 0001-01-02", "0001-01-02")]
    [InlineData("9999-12-30 9999-12-31", "9999-12-31")]
    [InlineData("2024-01-02 null 2024-01-01", "2024-01-02")]
    public void AverageOfDatesExactlyHalfwayBetweenTwoDaysIsTheLaterDay(
        string values,
        string expected
    )
    {
        DateOnly? average = PureQLAggregates.Average(AggregateVectors.Dates(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData("2024-01-01 2024-01-01 2024-01-02", "2024-01-01")]
    [InlineData("2024-01-01 2024-01-02 2024-01-02", "2024-01-02")]
    [InlineData("2024-02-28 2024-03-01", "2024-02-29")]
    [InlineData("2024-03-05", "2024-03-05")]
    [InlineData("0001-01-01 9999-12-31", "5000-07-02")]
    public void AverageOfDatesIsTheMeanDayRoundedToTheNearestWholeDay(
        string values,
        string expected
    )
    {
        DateOnly? average = PureQLAggregates.Average(AggregateVectors.Dates(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData("23:00:00.0000000 01:00:00.0000000", "12:00:00.0000000")]
    [InlineData("23:59:59.9999990 00:00:00.0000000 null", "12:00:00.0000000")]
    [InlineData("00:00:00.0000000 00:00:00.0000010", "00:00:00.0000010")]
    [InlineData("00:00:00.0000000 00:00:00.0000000 00:00:00.0000010", "00:00:00.0000000")]
    [InlineData("00:00:00.0000000 00:00:00.0000010 00:00:00.0000010", "00:00:00.0000010")]
    [InlineData("18:30:00.5000000", "18:30:00.5000000")]
    public void AverageOfTimesIsTheMeanSinceMidnightWithoutWrapping(
        string values,
        string expected
    )
    {
        TimeOnly? average = PureQLAggregates.Average(AggregateVectors.Times(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData(
        "1969-12-31T23:59:59.9999990 1970-01-01T00:00:00.0000000",
        "1970-01-01T00:00:00.0000000Z"
    )]
    [InlineData(
        "2024-01-01T00:00:00.0000000 2024-01-03T00:00:00.0000000 null",
        "2024-01-02T00:00:00.0000000Z"
    )]
    [InlineData(
        "2024-01-01T00:00:00.0000000 2024-01-01T00:00:00.0000000 "
            + "2024-01-01T00:00:00.0000010",
        "2024-01-01T00:00:00.0000000Z"
    )]
    [InlineData(
        "0001-01-01T00:00:00.0000000 0001-01-01T00:00:00.0000010",
        "0001-01-01T00:00:00.0000010Z"
    )]
    [InlineData(
        "9999-12-31T23:59:59.9999990 9999-12-31T23:59:59.9999980",
        "9999-12-31T23:59:59.9999990Z"
    )]
    public void AverageOfInstantsIsTheMeanInstantInUtc(string values, string expected)
    {
        DateTime? average = PureQLAggregates.Average(AggregateVectors.Instants(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Fact]
    public void AnInstantIsReadAsUtcWhateverItsKindSays()
    {
        long ticks = new DateTime(2024, 5, 6, 7, 8, 9).Ticks;
        DateTime?[] instants =
        [
            new DateTime(ticks, DateTimeKind.Local),
            new DateTime(ticks + 20, DateTimeKind.Unspecified),
            new DateTime(ticks + 40, DateTimeKind.Utc),
        ];
        DateTime expected = new DateTime(ticks + 20, DateTimeKind.Utc);
        DateTime?[] results =
        [
            PureQLAggregates.Average(instants),
            PureQLAggregates.Median(instants),
            PureQLAggregates.PercentileContinuous(instants, 0.5m),
            PureQLAggregates.PercentileDiscrete(instants, 0.5m),
        ];

        Assert.All(
            results,
            instant =>
            {
                Assert.Equal(expected, instant);
                Assert.Equal(DateTimeKind.Utc, instant.GetValueOrDefault().Kind);
            }
        );
    }

    [Theory]
    [InlineData("00:00:00.0000004", "00:00:00.0000000")]
    [InlineData("00:00:00.0000005", "00:00:00.0000010")]
    [InlineData("00:00:00.0000015", "00:00:00.0000020")]
    [InlineData("00:00:00.0000004 00:00:00.0000005", "00:00:00.0000000")]
    [InlineData("00:00:00.0000005 00:00:00.0000006", "00:00:00.0000010")]
    [InlineData("23:59:59.9999994", "23:59:59.9999990")]
    public void ATimeThatCarriesTicksIsAveragedExactlyThenRoundedToTheMicrosecond(
        string values,
        string expected
    )
    {
        TimeOnly?[] times = AggregateVectors.Times(values);
        DateTime?[] instants =
        [
            .. times.Select(time =>
                time is TimeOnly value ? new DateTime(2024, 1, 1) + value.ToTimeSpan()
                : (DateTime?)null
            ),
        ];

        Assert.Equal(expected, AggregateVectors.Text(PureQLAggregates.Average(times)));
        Assert.Equal(expected, AggregateVectors.Text(PureQLAggregates.Median(times)));
        Assert.Equal(
            "2024-01-01T" + expected + "Z",
            AggregateVectors.Text(PureQLAggregates.Average(instants))
        );
        Assert.Equal(
            "2024-01-01T" + expected + "Z",
            AggregateVectors.Text(PureQLAggregates.Median(instants))
        );
    }

    [Fact]
    public void ATimeThatRoundsToTheEndOfTheDayIsAnError()
    {
        TimeOnly?[] times = AggregateVectors.Times("23:59:59.9999995");

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.Average(times)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.Median(times)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.PercentileContinuous(times, 1m)
        );
    }

    [Fact]
    public void AnInstantThatRoundsBeyondTheLastYearIsAnError()
    {
        DateTime?[] instants = [DateTime.MaxValue];

        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.Average(instants)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.Median(instants)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => PureQLAggregates.PercentileContinuous(instants, 0m)
        );
    }

    [Theory]
    [InlineData("1 2 3 4", "2.5")]
    [InlineData("4 null 1 3 2", "2.5")]
    [InlineData("3 1 2", "2")]
    [InlineData("5", "5")]
    [InlineData("1 2", "1.5")]
    [InlineData("1.0 1.00", "1")]
    [InlineData(Large + " " + Large, Large)]
    [InlineData("0 0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [InlineData("0 -0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    public void MedianIsTheMiddleValueOrTheMeanOfTheTwoMiddleOnes(
        string values,
        string expected
    )
    {
        decimal? median = PureQLAggregates.Median(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(median));
    }

    [Theory]
    [InlineData("2024-01-01 2024-01-04", "2024-01-03")]
    [InlineData("2024-01-04 null 2024-01-01", "2024-01-03")]
    [InlineData("2024-01-01 2024-01-02", "2024-01-02")]
    [InlineData("2024-01-09 2024-01-01 2024-01-02", "2024-01-02")]
    [InlineData("1969-12-30 1969-12-31", "1969-12-31")]
    public void MedianOfDatesInterpolatesInDaysAndHalfwayIsTheLaterDay(
        string values,
        string expected
    )
    {
        DateOnly? median = PureQLAggregates.Median(AggregateVectors.Dates(values));

        Assert.Equal(expected, AggregateVectors.Text(median));
    }

    [Theory]
    [InlineData("23:00:00.0000000 01:00:00.0000000", "12:00:00.0000000")]
    [InlineData("00:00:00.0000010 00:00:00.0000000", "00:00:00.0000010")]
    [InlineData("10:00:00.0000000 12:00:00.0000000 23:00:00.0000000", "12:00:00.0000000")]
    public void MedianOfTimesInterpolatesToTheMicrosecond(string values, string expected)
    {
        TimeOnly? median = PureQLAggregates.Median(AggregateVectors.Times(values));

        Assert.Equal(expected, AggregateVectors.Text(median));
    }

    [Theory]
    [InlineData(
        "2024-01-01T00:00:00.0000000 2024-01-01T00:00:00.0000010",
        "2024-01-01T00:00:00.0000010Z"
    )]
    [InlineData(
        "2024-01-03T12:00:00.0000000 2024-01-01T00:00:00.0000000 null",
        "2024-01-02T06:00:00.0000000Z"
    )]
    [InlineData("1969-12-31T23:59:59.9999990", "1969-12-31T23:59:59.9999990Z")]
    public void MedianOfInstantsInterpolatesToTheMicrosecond(
        string values,
        string expected
    )
    {
        DateTime? median = PureQLAggregates.Median(AggregateVectors.Instants(values));

        Assert.Equal(expected, AggregateVectors.Text(median));
    }

    [Theory]
    [InlineData("1 2 3 4", "0.9", "3.7")]
    [InlineData("1 2 3 4", "0", "1")]
    [InlineData("1 2 3 4", "1", "4")]
    [InlineData("1 2 3 4", "0.5", "2.5")]
    [InlineData("4 3 null 2 1", "0.9", "3.7")]
    [InlineData("1 2 3 4", "1.0", "4")]
    [InlineData("1 2 3 4", "0.50", "2.5")]
    [InlineData("1 2 3 4", "-0.0", "1")]
    [InlineData("1 2 3 4", "0.3333333333333333333333333333", "2")]
    [InlineData("1 2 3 4", "0.6666666666666666666666666667", "3")]
    [InlineData(
        "0 1",
        "0.3333333333333333333333333333",
        "0.3333333333333333333333333333"
    )]
    [InlineData(
        "0 3",
        "0.3333333333333333333333333333",
        "0.9999999999999999999999999999"
    )]
    [InlineData("1 2", "0.0000000000000000000000000001", "1")]
    [InlineData("7", "0.37", "7")]
    public void PercentileContinuousInterpolatesBetweenTheTwoValuesNextToThePosition(
        string values,
        string fraction,
        string expected
    )
    {
        decimal? percentile = PureQLAggregates.PercentileContinuous(
            AggregateVectors.Decimals(values),
            AggregateVectors.Decimal(fraction)
        );

        Assert.Equal(expected, AggregateVectors.Text(percentile));
    }

    [Theory]
    [InlineData("0", "2024-01-01")]
    [InlineData("0.1", "2024-01-01")]
    [InlineData("0.5", "2024-01-03")]
    [InlineData("0.16", "2024-01-01")]
    [InlineData("0.17", "2024-01-02")]
    [InlineData("0.9", "2024-01-04")]
    [InlineData("1", "2024-01-04")]
    public void PercentileContinuousOfDatesIsRoundedToTheNearestWholeDay(
        string fraction,
        string expected
    )
    {
        DateOnly? percentile = PureQLAggregates.PercentileContinuous(
            AggregateVectors.Dates("2024-01-04 2024-01-01"),
            AggregateVectors.Decimal(fraction)
        );

        Assert.Equal(expected, AggregateVectors.Text(percentile));
    }

    [Theory]
    [InlineData("0", "00:00:00.0000000")]
    [InlineData("0.25", "00:00:00.0000010")]
    [InlineData("0.24", "00:00:00.0000000")]
    [InlineData("0.75", "00:00:00.0000020")]
    [InlineData("0.74", "00:00:00.0000010")]
    [InlineData("1", "00:00:00.0000020")]
    public void PercentileContinuousOfTimesAndInstantsIsRoundedToTheMicrosecond(
        string fraction,
        string expected
    )
    {
        decimal share = AggregateVectors.Decimal(fraction);
        TimeOnly? time = PureQLAggregates.PercentileContinuous(
            AggregateVectors.Times("00:00:00.0000020 00:00:00.0000000"),
            share
        );
        DateTime? instant = PureQLAggregates.PercentileContinuous(
            AggregateVectors.Instants(
                "1970-01-01T00:00:00.0000020 1970-01-01T00:00:00.0000000"
            ),
            share
        );

        Assert.Equal(expected, AggregateVectors.Text(time));
        Assert.Equal("1970-01-01T" + expected + "Z", AggregateVectors.Text(instant));
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    [InlineData("2")]
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("1.0000000000000000000000000001")]
    public void AFractionOutsideZeroToOneIsAnError(string fraction)
    {
        decimal share = AggregateVectors.Decimal(fraction);
        Action[] percentiles =
        [
            () => PureQLAggregates.PercentileContinuous(Array.Empty<decimal?>(), share),
            () => PureQLAggregates.PercentileContinuous(Array.Empty<DateOnly?>(), share),
            () => PureQLAggregates.PercentileContinuous(Array.Empty<TimeOnly?>(), share),
            () => PureQLAggregates.PercentileContinuous(Array.Empty<DateTime?>(), share),
            () => PureQLAggregates.PercentileDiscrete(Array.Empty<decimal?>(), share),
            () => PureQLAggregates.PercentileDiscrete(["a", "b"], share),
        ];

        Assert.All(
            percentiles,
            percentile => Assert.Throws<ArgumentOutOfRangeException>(percentile)
        );
    }

    [Theory]
    [InlineData("1 2 3 4", "0.5", "2")]
    [InlineData("1 2 3 4", "0.9", "4")]
    [InlineData("1 2 3 4", "0", "1")]
    [InlineData("1 2 3 4", "-0.0", "1")]
    [InlineData("1 2 3 4", "1", "4")]
    [InlineData("1 2 3 4", "0.25", "1")]
    [InlineData("1 2 3 4", "0.26", "2")]
    [InlineData("1 2 3 4", "0.75", "3")]
    [InlineData("1 2 3 4", "0.7500000000000000000000000001", "4")]
    [InlineData("1 2 3 4", "0.0000000000000000000000000001", "1")]
    [InlineData("4 null 2 null 3 1", "0.5", "2")]
    [InlineData("2.50", "0.3", "2.5")]
    [InlineData("1.0 1 3", "0.5", "1")]
    public void PercentileDiscreteIsTheValueNumberCeilingOfFractionTimesCount(
        string values,
        string fraction,
        string expected
    )
    {
        decimal? percentile = PureQLAggregates.PercentileDiscrete(
            AggregateVectors.Decimals(values),
            AggregateVectors.Decimal(fraction)
        );

        Assert.Equal(expected, AggregateVectors.Text(percentile));
    }

    [Fact]
    public void PercentileDiscreteOfIntegersKeepsTheirType()
    {
        long?[] values = [5, null, -2, 9, long.MinValue];

        Assert.Equal(long.MinValue, PureQLAggregates.PercentileDiscrete(values, 0m));
        Assert.Equal(-2L, PureQLAggregates.PercentileDiscrete(values, 0.5m));
        Assert.Equal(9L, PureQLAggregates.PercentileDiscrete(values, 1m));
        Assert.Null(PureQLAggregates.PercentileDiscrete<long?>([null], 1m));
    }

    [Fact]
    public void StringsAreOrderedByCodePointNotByUtf16CodeUnit()
    {
        string?[] values = ["\uFFFF", null, "\U00010000", "a", "B", "ab"];

        Assert.Equal("B", PureQLAggregates.PercentileDiscrete(values, 0m));
        Assert.Equal("a", PureQLAggregates.PercentileDiscrete(values, 0.4m));
        Assert.Equal("ab", PureQLAggregates.PercentileDiscrete(values, 0.6m));
        Assert.Equal("\uFFFF", PureQLAggregates.PercentileDiscrete(values, 0.8m));
        Assert.Equal("\U00010000", PureQLAggregates.PercentileDiscrete(values, 1m));
        Assert.Equal("\uFFFF", PureQLAggregates.Mode(["\U00010000", "\uFFFF"]));
        Assert.Null(PureQLAggregates.PercentileDiscrete<string?>([null], 1m));
        Assert.Null(PureQLAggregates.Mode<string>([]));
    }

    [Fact]
    public void FalseComesBeforeTrue()
    {
        bool?[] values = [true, false, null, true];

        Assert.Equal(false, PureQLAggregates.PercentileDiscrete(values, 0m));
        Assert.Equal(false, PureQLAggregates.PercentileDiscrete(values, 0.33m));
        Assert.Equal(true, PureQLAggregates.PercentileDiscrete(values, 0.34m));
        Assert.Equal(true, PureQLAggregates.Mode(values));
        Assert.Equal(false, PureQLAggregates.Mode<bool?>([true, false]));
        Assert.Null(PureQLAggregates.Mode<bool?>([null]));
    }

    [Fact]
    public void UuidsAreOrderedByTheirHexadecimalDigits()
    {
        string[] texts =
        [
            "80000000-0000-0000-0000-000000000000",
            "00000000-0000-0000-0000-000000000002",
            "7fffffff-ffff-ffff-ffff-ffffffffffff",
            "00000001-0000-0000-0000-000000000000",
            "00000000-0000-0000-0100-000000000000",
            "01000000-0000-0000-0000-000000000000",
            "00000000-0001-0000-0000-000000000000",
            "00000000-0000-0000-0000-000000000100",
            "00000000-0000-0001-0000-000000000000",
            "ffffffff-0000-0000-0000-000000000000",
        ];
        Guid?[] values = [.. texts.Select(text => (Guid?)Guid.Parse(text)), null];
        string[] ordered = [.. texts.Order(StringComparer.Ordinal)];

        for (int number = 1; number <= ordered.Length; number++)
        {
            Guid? value = PureQLAggregates.PercentileDiscrete(
                values,
                new decimal(number, 0, 0, false, 1)
            );

            Assert.Equal(ordered[number - 1], value.GetValueOrDefault().ToString());
        }

        Assert.Equal(
            ordered[0],
            PureQLAggregates.Mode(values).GetValueOrDefault().ToString()
        );
        Assert.Null(PureQLAggregates.Mode<Guid?>([null]));
    }

    [Fact]
    public void DatesTimesAndInstantsAreOrderedChronologically()
    {
        DateOnly?[] dates = AggregateVectors.Dates(
            "2024-03-01 1999-12-31 null 2024-02-29"
        );
        TimeOnly?[] times = AggregateVectors.Times(
            "12:00:00.0000000 00:00:00.0000001 23:59:59.9999999"
        );
        DateTime?[] instants = AggregateVectors.Instants(
            "2024-01-01T00:00:00.0000000 1969-12-31T23:59:59.9999999"
        );

        Assert.Equal(
            "2024-02-29",
            AggregateVectors.Text(PureQLAggregates.PercentileDiscrete(dates, 0.5m))
        );
        Assert.Equal("1999-12-31", AggregateVectors.Text(PureQLAggregates.Mode(dates)));
        Assert.Equal(
            "00:00:00.0000001",
            AggregateVectors.Text(PureQLAggregates.PercentileDiscrete(times, 0.1m))
        );
        Assert.Equal(
            "23:59:59.9999999",
            AggregateVectors.Text(PureQLAggregates.PercentileDiscrete(times, 1m))
        );
        Assert.Equal(
            "1969-12-31T23:59:59.9999999Z",
            AggregateVectors.Text(PureQLAggregates.PercentileDiscrete(instants, 0.5m))
        );
        Assert.Equal(
            "1969-12-31T23:59:59.9999999Z",
            AggregateVectors.Text(PureQLAggregates.Mode(instants))
        );
    }

    [Theory]
    [InlineData("1 2 2 3", "2")]
    [InlineData("3 1 3 1 2", "1")]
    [InlineData("3 null null null 3 1", "3")]
    [InlineData("5 4 -6", "-6")]
    [InlineData("1.0 2 1 2.5 2 1.00", "1")]
    [InlineData("2.50 2.5 7", "2.5")]
    [InlineData(
        "10000000000000000000000000.00 3 10000000000000000000000000",
        "10000000000000000000000000"
    )]
    [InlineData("-0.0 0 1", "0")]
    public void ModeIsTheMostFrequentValueAndAmongSeveralTheLeast(
        string values,
        string expected
    )
    {
        decimal? mode = PureQLAggregates.Mode(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(mode));
    }

    [Fact]
    public void ModeComparesStringsByTheirCodePoints()
    {
        Assert.Equal("a", PureQLAggregates.Mode(["A", "a", null, "a"]));
        Assert.Equal("A", PureQLAggregates.Mode(["a", "A"]));
        Assert.Equal("e\u0301", PureQLAggregates.Mode(["\u00E9", "e\u0301"]));
        Assert.Equal(string.Empty, PureQLAggregates.Mode(["b", "", ""]));
    }

    [Fact]
    public void ModeOfEveryOtherTypeComparesByValue()
    {
        long?[] integers = [7, null, -7, 7, -7, 3];
        DateOnly?[] dates = AggregateVectors.Dates("2024-01-02 2024-01-01 2024-01-02");
        TimeOnly?[] times = AggregateVectors.Times(
            "10:00:00.0000001 10:00:00.0000000 10:00:00.0000001"
        );
        long ticks = new DateTime(2024, 5, 6).Ticks;
        DateTime?[] instants =
        [
            new DateTime(ticks, DateTimeKind.Utc),
            new DateTime(ticks + 1, DateTimeKind.Local),
            new DateTime(ticks + 1, DateTimeKind.Unspecified),
        ];
        DateTime? instant = PureQLAggregates.Mode(instants);

        Assert.Equal(-7L, PureQLAggregates.Mode(integers));
        Assert.Equal("2024-01-02", AggregateVectors.Text(PureQLAggregates.Mode(dates)));
        Assert.Equal(
            "10:00:00.0000001",
            AggregateVectors.Text(PureQLAggregates.Mode(times))
        );
        Assert.Equal(ticks + 1, instant.GetValueOrDefault().Ticks);
        Assert.Equal(DateTimeKind.Utc, instant.GetValueOrDefault().Kind);
    }

    [Fact]
    public void AValueOfAnotherTypeHasNoOrder()
    {
        _ = Assert.Throws<NotSupportedException>(
            () => PureQLAggregates.Mode<int?>([1])
        );
        _ = Assert.Throws<NotSupportedException>(
            () => PureQLAggregates.Mode<long>([1])
        );
        _ = Assert.Throws<NotSupportedException>(
            () => PureQLAggregates.PercentileDiscrete<object>(["a"], 0.5m)
        );
    }

    [Fact]
    public void TheStatisticsOfTheValuesOneToFourAreThoseOfTheSpecification()
    {
        decimal?[] values = AggregateVectors.Decimals("1 2 3 4");

        Assert.Equal(1.25m, PureQLAggregates.VariancePopulation(values));
        Assert.Equal(
            1.666666666666666666666666667m,
            PureQLAggregates.VarianceSample(values)
        );
        Assert.Equal(
            1.118033988749894848204586834m,
            PureQLAggregates.StandardDeviationPopulation(values)
        );
        Assert.Equal(
            1.290994448735805628393088467m,
            PureQLAggregates.StandardDeviationSample(values)
        );
    }

    [Theory]
    [InlineData("7")]
    [InlineData("null 7 null")]
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("9999999999999999999999999999")]
    public void OneValueHasAPopulationVarianceOfZeroAndNoSampleVariance(string values)
    {
        decimal?[] column = AggregateVectors.Decimals(values);

        Assert.Equal(0m, PureQLAggregates.VariancePopulation(column));
        Assert.Equal(0m, PureQLAggregates.StandardDeviationPopulation(column));
        Assert.Null(PureQLAggregates.VarianceSample(column));
        Assert.Null(PureQLAggregates.StandardDeviationSample(column));
    }

    [Fact]
    public void AVarianceIsComputedFromSquaresThatNeedMoreDigitsThanADecimalHolds()
    {
        decimal?[] values = AggregateVectors.Decimals(
            "9999999999999999999999999999 null 9999999999999999999999999997"
        );

        Assert.Equal(1m, PureQLAggregates.VariancePopulation(values));
        Assert.Equal(2m, PureQLAggregates.VarianceSample(values));
        Assert.Equal(1m, PureQLAggregates.StandardDeviationPopulation(values));
        Assert.Equal(
            1.414213562373095048801688724m,
            PureQLAggregates.StandardDeviationSample(values)
        );
    }

    [Fact]
    public void AStandardDeviationIsTheRootOfTheExactVarianceNotOfItsRoundedValue()
    {
        decimal?[] values = AggregateVectors.Decimals("0 0 5");

        // The root of the rounded variance, 8.333333333333333333333333333,
        // would end in 902.
        Assert.Equal(
            8.333333333333333333333333333m,
            PureQLAggregates.VarianceSample(values)
        );
        Assert.Equal(
            2.886751345948128822545743903m,
            PureQLAggregates.StandardDeviationSample(values)
        );
    }

    [Fact]
    public void OnlyTheResultOfAStatisticIsCheckedAgainstTheRangeOfADecimal()
    {
        decimal?[] values = AggregateVectors.Decimals(Large + " -" + Large);

        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.VariancePopulation(values)
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.VarianceSample(values)
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.StandardDeviationSample(values)
        );
        Assert.Equal(
            Large,
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(values))
        );
        Assert.Equal(0m, PureQLAggregates.Average(values));
    }

    [Fact]
    public void TheStatisticsOfThePairsAreThoseOfTheSpecification()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(Pairs);

        Assert.Equal(2.75m, PureQLAggregates.CovariancePopulation(pairs));
        Assert.Equal(
            3.666666666666666666666666667m,
            PureQLAggregates.CovarianceSample(pairs)
        );
        Assert.Equal(
            0.9647638212377321361920414832m,
            PureQLAggregates.Correlation(pairs)
        );
    }

    [Fact]
    public void APairCountsOnlyWhenBothOfItsValuesAreNotNull()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(
            "null:1000 1:2 2:4 1000:null 3:5 null:null 4:9"
        );
        (decimal?, decimal?)[] line = AggregateVectors.Pairs(
            "1000:null 2:1 4:2 null:1000 5:3 null:null 9:4"
        );

        Assert.Equal(2.75m, PureQLAggregates.CovariancePopulation(pairs));
        Assert.Equal(
            3.666666666666666666666666667m,
            PureQLAggregates.CovarianceSample(pairs)
        );
        Assert.Equal(
            0.9647638212377321361920414832m,
            PureQLAggregates.Correlation(pairs)
        );
        Assert.Equal(2.2m, PureQLAggregates.RegressionSlope(line));
        Assert.Equal(-0.5m, PureQLAggregates.RegressionIntercept(line));
        Assert.Equal(
            0.9307692307692307692307692308m,
            PureQLAggregates.RegressionRSquared(line)
        );
    }

    [Theory]
    [InlineData("1 2 3 4")]
    [InlineData("1.5 2.25 -7 0.001")]
    [InlineData("5")]
    [InlineData("0.0000000000000000000000000001 9999999999999 -3 null")]
    public void CovariancePopulationOfAColumnWithItselfIsItsVariancePopulation(
        string values
    )
    {
        decimal?[] column = AggregateVectors.Decimals(values);
        (decimal?, decimal?)[] pairs = [.. column.Select(value => (value, value))];

        Assert.Equal(
            PureQLAggregates.VariancePopulation(column),
            PureQLAggregates.CovariancePopulation(pairs)
        );
        Assert.Equal(
            PureQLAggregates.VarianceSample(column),
            PureQLAggregates.CovarianceSample(pairs)
        );
    }

    [Theory]
    [InlineData("3:4")]
    [InlineData("3:4 null:5 6:null")]
    public void OnePairHasACovariancePopulationOfZeroAndNothingElse(string pairs)
    {
        (decimal?, decimal?)[] rows = AggregateVectors.Pairs(pairs);

        Assert.Equal(0m, PureQLAggregates.CovariancePopulation(rows));
        Assert.Null(PureQLAggregates.CovarianceSample(rows));
        Assert.Null(PureQLAggregates.Correlation(rows));
        Assert.Null(PureQLAggregates.RegressionSlope(rows));
        Assert.Null(PureQLAggregates.RegressionIntercept(rows));
        Assert.Null(PureQLAggregates.RegressionRSquared(rows));
    }

    [Theory]
    [InlineData("1:3 2:3 3:3")]
    [InlineData("3:1 3:2 3:3")]
    [InlineData("3:3 3.0:3.00")]
    public void AColumnOfOneValueHasNoCorrelation(string pairs)
    {
        (decimal?, decimal?)[] rows = AggregateVectors.Pairs(pairs);

        Assert.Null(PureQLAggregates.Correlation(rows));
        Assert.Equal(0m, PureQLAggregates.CovariancePopulation(rows));
        Assert.Equal(0m, PureQLAggregates.CovarianceSample(rows));
    }

    [Theory]
    [InlineData("0.1:1.3 0.2:1.6 0.7:3.1 -5.5:-15.5", "1")]
    [InlineData("0.1:-1.3 0.2:-1.6 0.7:-3.1 -5.5:15.5", "-1")]
    [InlineData("1:3 2:6 3:9.0000000000000000000000000001", "1")]
    [InlineData("1:-3 2:-6 3:-9.0000000000000000000000000001", "-1")]
    [InlineData("1:1 2:-1 3:0 4:-1 5:1", "0")]
    [InlineData("2:1 4:2 5:3 9:4", "0.9647638212377321361920414832")]
    [InlineData("1:9 2:5 3:4 4:2", "-0.9647638212377321361920414832")]
    public void CorrelationIsRoundedOnceAndNeverFallsOutsideMinusOneToOne(
        string pairs,
        string expected
    )
    {
        decimal? correlation = PureQLAggregates.Correlation(
            AggregateVectors.Pairs(pairs)
        );

        Assert.Equal(expected, AggregateVectors.Text(correlation));
    }

    [Fact]
    public void ACorrelationThatRoundsToZeroHasNoSign()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(
            "0:-1 -0.0000000000000000000000000001:1 " + Large + ":0 -" + Large + ":0"
        );
        decimal correlation = PureQLAggregates.Correlation(pairs).GetValueOrDefault(1m);

        Assert.Equal(0m, correlation);
        Assert.False(decimal.IsNegative(correlation));
        Assert.Equal(0m, PureQLAggregates.RegressionRSquared(pairs));
    }

    [Fact]
    public void TheLineOfThePairsIsThatOfTheSpecification()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(Line);
        decimal slope = PureQLAggregates.RegressionSlope(pairs).GetValueOrDefault();
        decimal intercept = PureQLAggregates
            .RegressionIntercept(pairs)
            .GetValueOrDefault();

        Assert.Equal(2.2m, slope);
        Assert.Equal(-0.5m, intercept);
        Assert.Equal(
            0.9307692307692307692307692308m,
            PureQLAggregates.RegressionRSquared(pairs)
        );
        Assert.Equal(8.3m, intercept + (slope * 4m));
    }

    [Fact]
    public void TheFirstValueOfARegressionPairIsYAndTheSecondIsX()
    {
        (decimal?, decimal?)[] swapped = AggregateVectors.Pairs(Pairs);

        Assert.Equal(
            0.4230769230769230769230769231m,
            PureQLAggregates.RegressionSlope(swapped)
        );
        Assert.Equal(
            0.3846153846153846153846153846m,
            PureQLAggregates.RegressionIntercept(swapped)
        );
        Assert.Equal(
            0.9307692307692307692307692308m,
            PureQLAggregates.RegressionRSquared(swapped)
        );
    }

    [Fact]
    public void AFlatLineFitsExactlyAndHasNoCorrelation()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs("3:1 3:2 3:3");

        Assert.Equal(0m, PureQLAggregates.RegressionSlope(pairs));
        Assert.Equal(3m, PureQLAggregates.RegressionIntercept(pairs));
        Assert.Equal(1m, PureQLAggregates.RegressionRSquared(pairs));
        Assert.Null(PureQLAggregates.Correlation(pairs));
    }

    [Theory]
    [InlineData("2:1 5:1")]
    [InlineData("2:1 5:1.0 null:3 7:1.00")]
    [InlineData("4:4 4:4")]
    public void NoLineIsDefinedWhenXHasOneValueInEveryPair(string pairs)
    {
        (decimal?, decimal?)[] rows = AggregateVectors.Pairs(pairs);

        Assert.Null(PureQLAggregates.RegressionSlope(rows));
        Assert.Null(PureQLAggregates.RegressionIntercept(rows));
        Assert.Null(PureQLAggregates.RegressionRSquared(rows));
    }

    [Fact]
    public void TheInterceptIsNotComputedFromTheRoundedSlope()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs("0:1 0:2 1:4");

        // The mean of y minus the rounded slope times the mean of x would be
        // -0.4999999999999999999999999998.
        Assert.Equal(
            0.3571428571428571428571428571m,
            PureQLAggregates.RegressionSlope(pairs)
        );
        Assert.Equal(-0.5m, PureQLAggregates.RegressionIntercept(pairs));
    }

    [Fact]
    public void RegressionRSquaredIsNotTheSquareOfTheRoundedCorrelation()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs("0:1 1:2 1:4");

        // The square of the rounded correlation would end in 715.
        Assert.Equal(
            0.7559289460184544544290330725m,
            PureQLAggregates.Correlation(pairs)
        );
        Assert.Equal(
            0.5714285714285714285714285714m,
            PureQLAggregates.RegressionRSquared(pairs)
        );
    }

    [Fact]
    public void AResultIsTheSameInAnyOrderOfTheRows()
    {
        string[] values =
        [
            "9999999999999999999999999999",
            "-0.0000000000000000000000000007",
            "1.50",
            "null",
            "-9999999999999999999999999999",
            "1.5",
            "0.3333333333333333333333333333",
            "123456789.123456789",
        ];

        string[] results =
        [
            .. Enumerable
                .Range(0, values.Length)
                .SelectMany(shift => new string[][]
                {
                    [.. values.Skip(shift), .. values.Take(shift)],
                    [.. values.Skip(shift).Reverse(), .. values.Take(shift)],
                })
                .Select(order => Everything(string.Join(' ', order)))
                .Distinct(),
        ];

        _ = Assert.Single(results);
    }

    [Fact]
    public void EveryAggregateReadsItsValuesOnce()
    {
        decimal?[] numbers = AggregateVectors.Decimals("3 1 null 2 2");
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(Pairs);
        DateOnly?[] dates = AggregateVectors.Dates("2024-01-04 2024-01-01");
        TimeOnly?[] times = AggregateVectors.Times("23:00:00.0000000 01:00:00.0000000");
        DateTime?[] instants = AggregateVectors.Instants("2024-01-04T00:00:00.0000000");

        ReadOnce(AggregateVectors.Integers("1 2"), rows => PureQLAggregates.Sum(rows));
        ReadOnce(numbers, rows => PureQLAggregates.Sum(rows));
        ReadOnce(numbers, rows => PureQLAggregates.Average(rows));
        ReadOnce(numbers, rows => PureQLAggregates.Median(rows));
        ReadOnce(numbers, rows => PureQLAggregates.PercentileContinuous(rows, 0.9m));
        ReadOnce(numbers, rows => PureQLAggregates.PercentileDiscrete(rows, 0.9m));
        ReadOnce(numbers, rows => PureQLAggregates.Mode(rows));
        ReadOnce(numbers, rows => PureQLAggregates.VariancePopulation(rows));
        ReadOnce(numbers, rows => PureQLAggregates.VarianceSample(rows));
        ReadOnce(numbers, rows => PureQLAggregates.StandardDeviationPopulation(rows));
        ReadOnce(numbers, rows => PureQLAggregates.StandardDeviationSample(rows));
        ReadOnce(pairs, rows => PureQLAggregates.CovariancePopulation(rows));
        ReadOnce(pairs, rows => PureQLAggregates.CovarianceSample(rows));
        ReadOnce(pairs, rows => PureQLAggregates.Correlation(rows));
        ReadOnce(pairs, rows => PureQLAggregates.RegressionSlope(rows));
        ReadOnce(pairs, rows => PureQLAggregates.RegressionIntercept(rows));
        ReadOnce(pairs, rows => PureQLAggregates.RegressionRSquared(rows));
        ReadOnce(dates, rows => PureQLAggregates.Average(rows));
        ReadOnce(dates, rows => PureQLAggregates.Median(rows));
        ReadOnce(dates, rows => PureQLAggregates.PercentileContinuous(rows, 0.9m));
        ReadOnce(times, rows => PureQLAggregates.Average(rows));
        ReadOnce(times, rows => PureQLAggregates.Median(rows));
        ReadOnce(times, rows => PureQLAggregates.PercentileContinuous(rows, 0.9m));
        ReadOnce(instants, rows => PureQLAggregates.Average(rows));
        ReadOnce(instants, rows => PureQLAggregates.Median(rows));
        ReadOnce(instants, rows => PureQLAggregates.PercentileContinuous(rows, 0.9m));
    }

    [Fact]
    public void AMissingColumnIsAnArgumentError()
    {
        IEnumerable<long?> integers = null!;
        IEnumerable<decimal?> numbers = null!;
        IEnumerable<(decimal?, decimal?)> pairs = null!;
        IEnumerable<DateOnly?> dates = null!;
        IEnumerable<TimeOnly?> times = null!;
        IEnumerable<DateTime?> instants = null!;
        Action[] aggregates =
        [
            () => PureQLAggregates.Sum(integers),
            () => PureQLAggregates.Sum(numbers),
            () => PureQLAggregates.Average(numbers),
            () => PureQLAggregates.Average(dates),
            () => PureQLAggregates.Average(times),
            () => PureQLAggregates.Average(instants),
            () => PureQLAggregates.Median(numbers),
            () => PureQLAggregates.Median(dates),
            () => PureQLAggregates.Median(times),
            () => PureQLAggregates.Median(instants),
            () => PureQLAggregates.PercentileContinuous(numbers, 0.5m),
            () => PureQLAggregates.PercentileContinuous(dates, 0.5m),
            () => PureQLAggregates.PercentileContinuous(times, 0.5m),
            () => PureQLAggregates.PercentileContinuous(instants, 0.5m),
            () => PureQLAggregates.PercentileDiscrete(numbers, 0.5m),
            () => PureQLAggregates.Mode(numbers),
            () => PureQLAggregates.VariancePopulation(numbers),
            () => PureQLAggregates.VarianceSample(numbers),
            () => PureQLAggregates.StandardDeviationPopulation(numbers),
            () => PureQLAggregates.StandardDeviationSample(numbers),
            () => PureQLAggregates.CovariancePopulation(pairs),
            () => PureQLAggregates.CovarianceSample(pairs),
            () => PureQLAggregates.Correlation(pairs),
            () => PureQLAggregates.RegressionSlope(pairs),
            () => PureQLAggregates.RegressionIntercept(pairs),
            () => PureQLAggregates.RegressionRSquared(pairs),
        ];

        Assert.All(
            aggregates,
            aggregate => Assert.Throws<ArgumentNullException>(aggregate)
        );
    }

    [Fact]
    public void ALargeColumnIsAggregatedExactly()
    {
        const int count = 200_000;

        // The values 0.01, 0.02, ... 2000.00, from the last to the first.
        decimal?[] numbers =
        [
            .. Enumerable
                .Range(1, count)
                .Reverse()
                .Select(number => (decimal?)new decimal(number, 0, 0, false, 2)),
        ];
        long?[] integers =
        [
            .. Enumerable.Range(0, count).Select(number => (long?)(number % 1000)),
        ];
        (decimal?, decimal?)[] pairs = [.. numbers.Select(number => (number, number))];

        Assert.Equal(200_001_000m, PureQLAggregates.Sum(numbers));
        Assert.Equal(1000.005m, PureQLAggregates.Average(numbers));
        Assert.Equal(1000.005m, PureQLAggregates.Median(numbers));
        Assert.Equal(1800.001m, PureQLAggregates.PercentileContinuous(numbers, 0.9m));
        Assert.Equal(1998m, PureQLAggregates.PercentileDiscrete(numbers, 0.999m));
        Assert.Equal(333333.333325m, PureQLAggregates.VariancePopulation(numbers));
        Assert.Equal(
            577.350269182408886144233353m,
            PureQLAggregates.StandardDeviationPopulation(numbers)
        );
        Assert.Equal(333333.333325m, PureQLAggregates.CovariancePopulation(pairs));
        Assert.Equal(1m, PureQLAggregates.Correlation(pairs));
        Assert.Equal(1m, PureQLAggregates.RegressionSlope(pairs));
        Assert.Equal(0m, PureQLAggregates.RegressionIntercept(pairs));
        Assert.Equal(99_900_000L, PureQLAggregates.Sum(integers));
        Assert.Equal(0L, PureQLAggregates.Mode(integers));
        Assert.Equal(999L, PureQLAggregates.PercentileDiscrete(integers, 1m));
    }

    // Every aggregate of a column of decimals, and of the column paired with
    // itself shifted by one row, as one text.
    private static string Everything(string values)
    {
        decimal?[] column = AggregateVectors.Decimals(values);
        (decimal?, decimal?)[] pairs =
        [
            .. column.Select(value => (value, value is null ? value : value / 3m)),
        ];
        Func<decimal?>[] aggregates =
        [
            () => PureQLAggregates.Sum(column),
            () => PureQLAggregates.Average(column),
            () => PureQLAggregates.Median(column),
            () => PureQLAggregates.PercentileContinuous(column, 0.37m),
            () => PureQLAggregates.PercentileDiscrete(column, 0.37m),
            () => PureQLAggregates.Mode(column),
            () => PureQLAggregates.VariancePopulation(column),
            () => PureQLAggregates.StandardDeviationSample(column),
            () => PureQLAggregates.CovariancePopulation(pairs),
            () => PureQLAggregates.Correlation(pairs),
            () => PureQLAggregates.RegressionSlope(pairs),
            () => PureQLAggregates.RegressionIntercept(pairs),
            () => PureQLAggregates.RegressionRSquared(pairs),
        ];

        return string.Join(
            ' ',
            aggregates.Select(AggregateVectors.Text)
        );
    }

    // Runs an aggregate over values that count how often they are read.
    private static void ReadOnce<T>(
        IEnumerable<T> values,
        Func<IEnumerable<T>, object?> aggregate
    )
    {
        int reads = 0;

        IEnumerable<T> Counted()
        {
            reads++;

            foreach (T value in values)
            {
                yield return value;
            }
        }

        _ = aggregate(Counted());

        Assert.Equal(1, reads);
    }
}
