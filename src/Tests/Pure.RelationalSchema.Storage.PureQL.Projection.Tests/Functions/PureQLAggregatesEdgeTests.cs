namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// Edge cases of PureQLAggregates by the specification's "Integer and decimal
// arithmetic" and "Aggregate evaluation": a result "is rounded half away from
// zero to the most decimal places that fit", once, from exact values (never
// half to even, as System.Decimal rounds), and "a result of magnitude 10^28
// or more is an error", also below the largest System.Decimal. A column is
// written as text, values separated by spaces, and a pair as first:second; a
// regression pair is y:x.
public sealed class PureQLAggregatesEdgeTests
{
    private const string Large = "9000000000000000000000000000";

    private const string Widest = "9999999999999999999999999999";

    [Theory]
    [InlineData("1 0.0000000000000000000000000005", "1.000000000000000000000000001")]
    [InlineData(
        "-1 -0.0000000000000000000000000005",
        "-1.000000000000000000000000001"
    )]
    [InlineData("1 0.0000000000000000000000000025", "1.000000000000000000000000003")]
    [InlineData("0.1000000000000000000000000001 1", "1.1")]
    [InlineData(
        "0.5000000000000000000000000005 0.5 null",
        "1.000000000000000000000000001"
    )]
    public void ASumExactlyHalfwayRoundsAwayFromZero(string values, string expected)
    {
        decimal? sum = PureQLAggregates.Sum(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(sum));
    }

    [Theory]
    [InlineData("1 0.0000000000000000000000000001", "0.5000000000000000000000000001")]
    [InlineData(
        "-1 -0.0000000000000000000000000001",
        "-0.5000000000000000000000000001"
    )]
    [InlineData(Widest + " 9999999999999999999999999998", Widest)]
    [InlineData("-" + Widest + " -9999999999999999999999999998", "-" + Widest)]
    [InlineData("0 2.000000000000000000000000001", "1.000000000000000000000000001")]
    public void AMeanExactlyHalfwayRoundsAwayFromZero(string values, string expected)
    {
        decimal? average = PureQLAggregates.Average(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(average));
    }

    [Theory]
    [InlineData("0 2.000000000000000000000000001", "1.000000000000000000000000001")]
    [InlineData("0 9999999999999999999999999997", "4999999999999999999999999999")]
    [InlineData("-9999999999999999999999999997 0", "-4999999999999999999999999999")]
    [InlineData("0 " + Widest, "5000000000000000000000000000")]
    [InlineData("-" + Widest + " null " + Widest, "0")]
    public void AMedianExactlyHalfwayRoundsAwayFromZero(string values, string expected)
    {
        decimal? median = PureQLAggregates.Median(AggregateVectors.Decimals(values));

        Assert.Equal(expected, AggregateVectors.Text(median));
    }

    // The value at position fraction x (n - 1), "interpolated linearly
    // between the two values next to that position", from exact values.
    [Theory]
    [InlineData(
        "0 " + Widest,
        "0.9999999999999999999999999999",
        "9999999999999999999999999998"
    )]
    [InlineData(
        "-" + Widest + " " + Widest,
        "0.9999999999999999999999999999",
        "9999999999999999999999999997"
    )]
    [InlineData(
        "-" + Widest + " " + Widest,
        "0.0000000000000000000000000001",
        "-9999999999999999999999999997"
    )]
    [InlineData("0 " + Widest, "0.1", "999999999999999999999999999.9")]
    [InlineData("1 2 3", "0.9999999999999999999999999999", "3")]
    public void PercentileContinuousBetweenWideValuesIsExactAndRoundedOnce(
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

    // "the value number ceiling(fraction x n)": over 1 ... 7 the fractions
    // next to 1/7 fall on either side of 1.
    [Theory]
    [InlineData("0.1428571428571428571428571428", 1L)]
    [InlineData("0.1428571428571428571428571429", 2L)]
    [InlineData("0.8571428571428571428571428571", 6L)]
    [InlineData("0.8571428571428571428571428572", 7L)]
    [InlineData("0.9999999999999999999999999999", 7L)]
    public void PercentileDiscreteRoundsItsRankUpFromTheExactProduct(
        string fraction,
        long expected
    )
    {
        long?[] values = [7, 3, null, 1, 5, 2, 6, 4];

        Assert.Equal(
            expected,
            PureQLAggregates.PercentileDiscrete(
                values,
                AggregateVectors.Decimal(fraction)
            )
        );
    }

    // "A standard deviation is the square root of the exact variance", and
    // the root is rounded as every decimal is: half away from zero.
    [Fact]
    public void AStandardDeviationExactlyHalfwayRoundsAwayFromZero()
    {
        decimal?[] values = AggregateVectors.Decimals("0 2.000000000000000000000000001");
        decimal?[] tiny = AggregateVectors.Decimals("0 0.0000000000000000000000000001");

        Assert.Equal(
            "1.000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(values))
        );
        Assert.Equal(
            "1.000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.VariancePopulation(values))
        );
        Assert.Equal(
            "2.000000000000000000000000002",
            AggregateVectors.Text(PureQLAggregates.VarianceSample(values))
        );
        Assert.Equal(
            "1.414213562373095048801688725",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationSample(values))
        );
        Assert.Equal(
            "0.0000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(tiny))
        );
        Assert.Equal(0m, PureQLAggregates.VariancePopulation(tiny));
    }

    // 2.25 x 10^28 and 4.5 x 10^28 fit a System.Decimal but are beyond the
    // range of a PureQL decimal, whose magnitude is below 10^28.
    [Fact]
    public void AVarianceOfMagnitudeTenToTheTwentyEighthIsAnError()
    {
        decimal?[] values = AggregateVectors.Decimals("-150000000000000 150000000000000");
        (decimal?, decimal?)[] pairs = [.. values.Select(value => (value, value))];

        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.VariancePopulation(values)
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.VarianceSample(values)
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.CovariancePopulation(pairs)
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.CovarianceSample(pairs)
        );
        Assert.Equal(
            "150000000000000",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(values))
        );
        Assert.Equal(
            "212132034355964.2573202533086",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationSample(values))
        );
    }

    [Fact]
    public void ASumOfMagnitudeTenToTheTwentyEighthIsAnErrorBelowTheLargestDecimal()
    {
        decimal?[] values = AggregateVectors.Decimals(
            "5000000000000000000000000000 5000000000000000000000000000"
        );

        _ = Assert.Throws<OverflowException>(() => PureQLAggregates.Sum(values));
        Assert.Equal(
            "5000000000000000000000000000",
            AggregateVectors.Text(PureQLAggregates.Average(values))
        );
    }

    [Fact]
    public void AVarianceJustBelowTenToTheTwentyEighthIsExact()
    {
        decimal?[] values = AggregateVectors.Decimals("-99999999999999 99999999999999");

        Assert.Equal(
            "9999999999999800000000000001",
            AggregateVectors.Text(PureQLAggregates.VariancePopulation(values))
        );
        Assert.Equal(
            "99999999999999",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(values))
        );
    }

    [Fact]
    public void AStandardDeviationOfTheWidestValuesIsExactAlthoughItsVarianceIsNot()
    {
        decimal?[] values = AggregateVectors.Decimals("0 " + Widest);

        // The population deviation is 4999999999999999999999999999.5.
        Assert.Equal(
            "5000000000000000000000000000",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationPopulation(values))
        );
        Assert.Equal(
            "7071067811865475244008443620",
            AggregateVectors.Text(PureQLAggregates.StandardDeviationSample(values))
        );
        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.VarianceSample(values)
        );
    }

    // A covariance of exactly 0.00000000000000000000000000005, with 29
    // decimal places, rounds to 0.0000000000000000000000000001.
    [Fact]
    public void ACovarianceExactlyHalfwayRoundsAwayFromZero()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(
            "0:0 1:0.0000000000000000000000000002"
        );
        (decimal?, decimal?)[] line = AggregateVectors.Pairs(
            "0:0 0.0000000000000000000000000002:1"
        );

        Assert.Equal(
            "0.0000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.CovariancePopulation(pairs))
        );
        Assert.Equal(
            "0.0000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.CovarianceSample(pairs))
        );
        Assert.Equal(1m, PureQLAggregates.Correlation(pairs));
        Assert.Equal(
            "0.0000000000000000000000000002",
            AggregateVectors.Text(PureQLAggregates.RegressionSlope(line))
        );
        Assert.Equal(0m, PureQLAggregates.RegressionIntercept(line));
        Assert.Equal(1m, PureQLAggregates.RegressionRSquared(line));
    }

    // The statistics of two columns are exact ratios of exact sums: the
    // products of these pairs need about 56 digits, yet the correlation, the
    // slope and the share of variance are small numbers.
    [Fact]
    public void PairStatisticsOfHugeValuesAreExactRatios()
    {
        (decimal?, decimal?)[] same = AggregateVectors.Pairs(
            Large + ":" + Large + " -" + Large + ":-" + Large
        );
        (decimal?, decimal?)[] line = AggregateVectors.Pairs(
            "1:" + Large + " -1:-" + Large
        );

        _ = Assert.Throws<OverflowException>(
            () => PureQLAggregates.CovariancePopulation(same)
        );
        Assert.Equal(1m, PureQLAggregates.Correlation(same));
        Assert.Equal(1m, PureQLAggregates.RegressionSlope(same));
        Assert.Equal(0m, PureQLAggregates.RegressionIntercept(same));
        Assert.Equal(1m, PureQLAggregates.RegressionRSquared(same));
        Assert.Equal(
            Large,
            AggregateVectors.Text(PureQLAggregates.CovariancePopulation(line))
        );
        Assert.Equal(
            "0.0000000000000000000000000001",
            AggregateVectors.Text(PureQLAggregates.RegressionSlope(line))
        );
        Assert.Equal(0m, PureQLAggregates.RegressionIntercept(line));
        Assert.Equal(1m, PureQLAggregates.RegressionRSquared(line));
        Assert.Equal(1m, PureQLAggregates.Correlation(line));
    }

    [Theory]
    [InlineData("-9223372036854775808 -1 1", long.MinValue)]
    [InlineData(
        "9223372036854775807 9223372036854775807 -9223372036854775808 "
            + "-9223372036854775808 1",
        -1L
    )]
    [InlineData("-1 -9223372036854775808 9223372036854775807 null 1", -1L)]
    public void AnIntegerSumIsExactWhereverItsPartialSumsGo(string values, long expected)
    {
        Assert.Equal(expected, PureQLAggregates.Sum(AggregateVectors.Integers(values)));
    }

    [Fact]
    public void ModeOfIntegersBreaksATieAtTheExtremes()
    {
        long?[] values =
        [
            long.MaxValue,
            long.MinValue,
            null,
            long.MaxValue,
            long.MinValue,
            null,
            null,
        ];

        Assert.Equal(long.MinValue, PureQLAggregates.Mode(values));
        Assert.Equal(long.MinValue, PureQLAggregates.PercentileDiscrete(values, 0.5m));
        Assert.Equal(long.MaxValue, PureQLAggregates.PercentileDiscrete(values, 0.51m));
    }

    // Statistics of one value: a population variance and covariance of 0,
    // no sample statistic, no correlation and no line, for any value.
    [Fact]
    public void OneWidestPairHasACovariancePopulationOfZero()
    {
        (decimal?, decimal?)[] pairs = AggregateVectors.Pairs(
            Widest + ":-" + Widest + " null:1"
        );

        Assert.Equal(0m, PureQLAggregates.CovariancePopulation(pairs));
        Assert.Null(PureQLAggregates.CovarianceSample(pairs));
        Assert.Null(PureQLAggregates.Correlation(pairs));
        Assert.Null(PureQLAggregates.RegressionSlope(pairs));
    }
}
