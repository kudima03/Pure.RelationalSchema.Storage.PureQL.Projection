using System.Globalization;
using Pure.Primitives.Number;
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
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The statistical aggregates, countDistinct, concatAll, first / last and
// any / all as the specification defines them under "Aggregate evaluation",
// in a grouped query (over the group and over: all), a total query (always
// nullable, a row also over no rows) and a plain query over all rows: the
// median and the percentiles, the mode, the variances and standard
// deviations (the sample forms null over one row), the statistics of two
// columns over the pairs of non-null values, the least-squares line, means of
// dates, times and datetimes rounded to the day or the microsecond with a mean
// exactly halfway going to the later value, and results computed from exact
// values and rounded once. The README's examples are used verbatim. Every
// grouped column is read in the order of its key g.
public sealed class StatisticsTests
{
    // a: the README's values 1, 2, 3, 4; b: one row; c: 0, 0, 1.
    private static readonly IStoredSchemaDataSet Values = new InlineTable(
        "t",
        "values",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("v", new DoubleColumnType()),
        ("n", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "a", "1", "1", "a"],
        ["2", "a", "2", "2", "B"],
        ["3", "a", "3", "3", "a"],
        ["4", "a", "4", "4", "B"],
        ["5", "b", "7", "7", "x"],
        ["6", "c", "0", "6", "b"],
        ["7", "c", "0", "5", "b"],
        ["8", "c", "1", "6", "c"]
    );

    // a: the README's pairs (1, 2), (2, 4), (3, 5), (4, 9); b: a flat line;
    // c: two pairs with one x; d: one pair; n: the pairs of a and two rows
    // that are not pairs.
    private static readonly IStoredSchemaDataSet Points = new InlineTable(
        "t",
        "points",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("x", new DoubleColumnType()),
        ("y", new DoubleColumnType())
    ).With(
        ["1", "a", "1", "2"],
        ["2", "a", "2", "4"],
        ["3", "a", "3", "5"],
        ["4", "a", "4", "9"],
        ["5", "b", "1", "3"],
        ["6", "b", "2", "3"],
        ["7", "b", "3", "3"],
        ["8", "c", "1", "2"],
        ["9", "c", "1", "5"],
        ["10", "d", "2", "6"],
        ["11", "n", "1", "2"],
        ["12", "n", "2", "4"],
        ["13", "n", "3", "5"],
        ["14", "n", "4", "9"],
        ["15", "n", "5", ""],
        ["16", "n", "", "7"]
    );

    // half: two values one day or one microsecond apart; pre: before 1970,
    // and times far apart in the day; spread: four values; third: a mean a
    // third or two thirds of the way to the next day or microsecond.
    private static readonly IStoredSchemaDataSet Moments = new InlineTable(
        "t",
        "moments",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("d", new DateColumnType()),
        ("t", new TimeColumnType()),
        ("dt", new DateTimeColumnType())
    ).With(
        ["1", "half", "2024-01-01", "00:00:00", "2024-01-01T00:00:00Z"],
        ["2", "half", "2024-01-02", "00:00:00.000001", "2024-01-01T00:00:00.000001Z"],
        ["3", "pre", "1969-12-30", "23:00:00", "1969-12-31T23:59:59.999999Z"],
        ["4", "pre", "1969-12-31", "01:00:00", "1970-01-01T00:00:00Z"],
        ["5", "spread", "2023-12-31", "00:00:00", "2024-01-01T00:00:00Z"],
        ["6", "spread", "2024-01-01", "00:00:01", "2024-01-01T00:00:01Z"],
        ["7", "spread", "2024-01-04", "00:00:02", "2024-01-01T00:00:02Z"],
        ["8", "spread", "2024-01-10", "00:00:04", "2024-01-01T00:00:04Z"],
        ["9", "third", "2024-01-01", "10:00:00", "2024-01-01T00:00:00Z"],
        ["10", "third", "2024-01-01", "10:00:00.000001", "2024-01-01T00:00:00Z"],
        ["11", "third", "2024-01-02", "10:00:00.000001", "2024-01-01T00:00:00.000001Z"]
    );

    // p: 1 and 1.0 are one value, strings that differ in case are two, row 3
    // holds no value; q: no values at all.
    private static readonly IStoredSchemaDataSet Tags = new InlineTable(
        "t",
        "tags",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("d", new DoubleColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "p", "1", "Red"],
        ["2", "p", "1.0", "red"],
        ["3", "p", "", ""],
        ["4", "p", "2", "red"],
        ["5", "q", "", ""],
        ["6", "q", "", ""]
    );

    // Values whose exact sums and means need more digits than a decimal has.
    private static readonly IStoredSchemaDataSet Amounts = new InlineTable(
        "t",
        "amounts",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("a", new DoubleColumnType())
    ).With(
        ["1", "close", "0"],
        ["2", "close", "0.00000000000001"],
        ["3", "huge", "9000000000000000000000000000"],
        ["4", "huge", "9000000000000000000000000000"],
        ["5", "huge", "-9000000000000000000000000000"],
        ["6", "mid", "0.0000000000000000000000000001"],
        ["7", "mid", "0.0000000000000000000000000002"],
        ["8", "third", "1"],
        ["9", "third", "1"],
        ["10", "third", "2"],
        ["11", "tiny", "1"],
        ["12", "tiny", "0.0000000000000000000000000004"],
        ["13", "tiny", "0.0000000000000000000000000004"]
    );

    private static readonly FieldInteger ValueId = new("t.values", "id");

    private static readonly FieldDecimal V = new("t.values", "v");

    private static readonly FieldInteger N = new("t.values", "n");

    private static readonly FieldString S = new("t.values", "s");

    private static readonly LessThanDecimalRow Small = new(V, new LiteralInteger(4));

    private static readonly FieldInteger PointId = new("t.points", "id");

    private static readonly FieldDecimalNullable X = new("t.points", "x");

    private static readonly FieldDecimalNullable Y = new("t.points", "y");

    private static readonly FieldDate Day = new("t.moments", "d");

    private static readonly FieldTime Clock = new("t.moments", "t");

    private static readonly FieldDatetime Instant = new("t.moments", "dt");

    private static readonly FieldInteger TagId = new("t.tags", "id");

    private static readonly FieldDecimalNullable Tag = new("t.tags", "d");

    private static readonly FieldStringNullable Label = new("t.tags", "s");

    private static readonly FieldDecimal Amount = new("t.amounts", "a");

    [Fact]
    public void MedianIsTheMiddleValueOrTheMeanOfTheTwoMiddleOnes()
    {
        Assert.Equal(["2.5", "7", "0"], Grouped(Values, new MedianDecimalGroup(V)));
    }

    [Theory]
    [InlineData("0.9", "3.7", "0.8")]
    [InlineData("0", "1", "0")]
    [InlineData("1", "4", "1")]
    [InlineData("0.5", "2.5", "0")]
    [InlineData("0.25", "1.75", "0")]
    public void PercentileContinuousInterpolatesAtTheFractionOfTheValues(
        string fraction,
        string a,
        string c
    )
    {
        Assert.Equal(
            [a, "7", c],
            Grouped(
                Values,
                new PercentileContinuousDecimalGroup(
                    V,
                    new LiteralDecimal(Parse(fraction))
                )
            )
        );
    }

    [Theory]
    [InlineData("0.5", "2", "6")]
    [InlineData("0.9", "4", "6")]
    [InlineData("0", "1", "5")]
    [InlineData("0.25", "1", "5")]
    [InlineData("0.26", "2", "5")]
    [InlineData("1", "4", "6")]
    public void PercentileDiscreteIsAValueOfTheRowsAndKeepsItsType(
        string fraction,
        string a,
        string c
    )
    {
        Assert.Equal(
            [a, "7", c],
            Grouped(
                Values,
                new PercentileDiscreteIntegerGroup(N, new LiteralDecimal(Parse(fraction)))
            )
        );
    }

    [Fact]
    public void PercentileDiscreteReadsItsFractionFromAParameter()
    {
        Assert.Equal(
            ["4", "7", "6"],
            Grouped(
                Values,
                new PercentileDiscreteIntegerGroup(N, new ParamDecimal("fraction")),
                new Dictionary<string, IParameter>
                {
                    ["fraction"] = new DecimalParameter(new Decimal(0.9m)),
                }
            )
        );
    }

    [Fact]
    public void FractionParameterMayBeBoundToAnInteger()
    {
        Assert.Equal(
            ["4", "7", "6"],
            Grouped(
                Values,
                new PercentileDiscreteIntegerGroup(N, new ParamDecimal("fraction")),
                new Dictionary<string, IParameter>
                {
                    ["fraction"] = new IntegerParameter(new Long(1)),
                }
            )
        );
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void FractionParameterOutsideZeroToOneIsAnError(string fraction)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Grouped(
                Values,
                new PercentileContinuousDecimalGroup(V, new ParamDecimal("fraction")),
                new Dictionary<string, IParameter>
                {
                    ["fraction"] = new DecimalParameter(new Decimal(Parse(fraction))),
                }
            )
        );
    }

    [Fact]
    public void PercentileDiscreteOfStringsOrdersThemByCodePoint()
    {
        Assert.Equal(
            ["B", "x", "b"],
            Grouped(
                Values,
                new PercentileDiscreteStringGroup(S, new LiteralDecimal(0.5m))
            )
        );
    }

    [Fact]
    public void ModeIsTheMostFrequentValueAndTheLeastAmongSeveral()
    {
        Assert.Equal(["1", "7", "6"], Grouped(Values, new ModeIntegerGroup(N)));
    }

    [Fact]
    public void ModeOfStringsBreaksATieByCodePoint()
    {
        Assert.Equal(["B", "x", "b"], Grouped(Values, new ModeStringGroup(S)));
    }

    [Fact]
    public void ModeComparesDecimalsAsEqualComparesThem()
    {
        Assert.Equal(["1", ""], Grouped(Tags, new ModeDecimalNullableGroup(Tag)));
    }

    [Fact]
    public void PopulationVarianceAndDeviationOfOneValueAreZero()
    {
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup("variance", new VariancePopulationDecimalGroup(V)),
                new SelectItemGroup(
                    "deviation",
                    new StandardDeviationPopulationDecimalGroup(V)
                ),
            ]
        );

        Assert.Equal(
            ["1.25", "0", "0.2222222222222222222222222222"],
            ResultColumn.Texts(projection, "variance")
        );
        Assert.Equal(
            ["1.118033988749894848204586834", "0", "0.4714045207910316829338962414"],
            ResultColumn.Texts(projection, "deviation")
        );
    }

    [Fact]
    public void SampleVarianceAndDeviationOfOneValueAreNull()
    {
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup("variance", new VarianceSampleGroup(V)),
                new SelectItemGroup("deviation", new StandardDeviationSampleGroup(V)),
            ]
        );

        Assert.Equal(
            ["1.666666666666666666666666667", "", "0.3333333333333333333333333333"],
            ResultColumn.Texts(projection, "variance")
        );
        Assert.Equal(
            ["1.290994448735805628393088467", "", "0.5773502691896257645091487805"],
            ResultColumn.Texts(projection, "deviation")
        );
    }

    [Fact]
    public void StatisticsOverAllAreComputedOverEveryRowForEachGroup()
    {
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup(
                    "median",
                    new MedianDecimalGroup(V, AggregateOver.All)
                ),
                new SelectItemGroup(
                    "p90",
                    new PercentileContinuousDecimalGroup(
                        V,
                        new LiteralDecimal(0.9m),
                        AggregateOver.All
                    )
                ),
                new SelectItemGroup("mode", new ModeIntegerGroup(N, AggregateOver.All)),
                new SelectItemGroup(
                    "variance",
                    new VariancePopulationDecimalGroup(V, AggregateOver.All)
                ),
                new SelectItemGroup(
                    "deviation",
                    new StandardDeviationSampleGroup(V, over: AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(["1.5", "1.5", "1.5"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["4.9", "4.9", "4.9"], ResultColumn.Texts(projection, "p90"));
        Assert.Equal(["6", "6", "6"], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(
            ["4.9375", "4.9375", "4.9375"],
            ResultColumn.Texts(projection, "variance")
        );
        Assert.Equal(
            Enumerable.Repeat("2.375469878330841486897061183", 3),
            ResultColumn.Texts(projection, "deviation")
        );
    }

    [Fact]
    public void MedianAndModeWithAPredicateAreNullForAGroupWithoutItsRows()
    {
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup(
                    "median",
                    new MedianDecimalNullableGroup(V, predicate: Small)
                ),
                new SelectItemGroup("mode", new ModeStringNullableGroup(S, Small)),
                new SelectItemGroup(
                    "p90",
                    new PercentileDiscreteIntegerNullableGroup(
                        N,
                        new LiteralDecimal(0.9m),
                        Small
                    )
                ),
            ]
        );

        Assert.Equal(["2", "", "0"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["a", "", "b"], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(["3", "", "6"], ResultColumn.Texts(projection, "p90"));
    }

    [Fact]
    public void VariancesWithAPredicateReadOnlyTheRowsThatSatisfyIt()
    {
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup(
                    "population",
                    new VariancePopulationDecimalNullableGroup(V, Small)
                ),
                new SelectItemGroup("sample", new VarianceSampleGroup(V, Small)),
                new SelectItemGroup(
                    "deviation",
                    new StandardDeviationPopulationDecimalNullableGroup(V, Small)
                ),
            ]
        );

        Assert.Equal(
            ["0.6666666666666666666666666667", "", "0.2222222222222222222222222222"],
            ResultColumn.Texts(projection, "population")
        );
        Assert.Equal(
            ["1", "", "0.3333333333333333333333333333"],
            ResultColumn.Texts(projection, "sample")
        );
        Assert.Equal(
            ["0.8164965809277260327324280249", "", "0.4714045207910316829338962414"],
            ResultColumn.Texts(projection, "deviation")
        );
    }

    [Fact]
    public void PlainQueryRepeatsStatisticsOverAllRowsOnEveryRow()
    {
        PureQLProjection projection = Plain(
            Values,
            [
                new SelectItemProjection("median", new MedianDecimalProjection(V)),
                new SelectItemProjection(
                    "variance",
                    new VariancePopulationDecimalProjection(V)
                ),
                new SelectItemProjection(
                    "p50",
                    new PercentileDiscreteIntegerProjection(N, new LiteralDecimal(0.5m))
                ),
                new SelectItemProjection("mode", new ModeStringProjection(S)),
                new SelectItemProjection("distinct", new CountDistinctProjection(S)),
            ]
        );

        Assert.Equal(
            Enumerable.Repeat("1.5", 8),
            ResultColumn.Texts(projection, "median")
        );
        Assert.Equal(
            Enumerable.Repeat("4.9375", 8),
            ResultColumn.Texts(projection, "variance")
        );
        Assert.Equal(Enumerable.Repeat("4", 8), ResultColumn.Texts(projection, "p50"));
        Assert.Equal(Enumerable.Repeat("B", 8), ResultColumn.Texts(projection, "mode"));
        Assert.Equal(
            Enumerable.Repeat("5", 8),
            ResultColumn.Texts(projection, "distinct")
        );
    }

    [Fact]
    public void PlainQueryStatisticWithAPredicateIsNullWhenTooFewRowsSatisfyIt()
    {
        PureQLProjection projection = Plain(
            Values,
            [
                new SelectItemProjection(
                    "median",
                    new MedianDecimalNullableProjection(
                        V,
                        new GreaterThanDecimalRow(V, new LiteralInteger(100))
                    )
                ),
                new SelectItemProjection(
                    "sample",
                    new VarianceSampleProjection(
                        V,
                        new EqualDecimalRow(ValueId, new LiteralInteger(5))
                    )
                ),
            ]
        );

        Assert.Equal(Enumerable.Repeat("", 8), ResultColumn.Texts(projection, "median"));
        Assert.Equal(Enumerable.Repeat("", 8), ResultColumn.Texts(projection, "sample"));
    }

    [Fact]
    public void TotalQueryComputesEachStatisticOverAllRowsOnce()
    {
        PureQLProjection projection = Total(
            Values,
            [
                new SelectItemTotal("median", new MedianDecimalNullableTotal(V)),
                new SelectItemTotal(
                    "p25",
                    new PercentileContinuousDecimalNullableTotal(
                        V,
                        new LiteralDecimal(0.25m)
                    )
                ),
                new SelectItemTotal("mode", new ModeStringNullableTotal(S)),
                new SelectItemTotal("deviation", new StandardDeviationSampleTotal(V)),
            ]
        );

        Assert.Equal(["1.5"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["0.75"], ResultColumn.Texts(projection, "p25"));
        Assert.Equal(["B"], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(
            ["2.375469878330841486897061183"],
            ResultColumn.Texts(projection, "deviation")
        );
    }

    [Fact]
    public void TotalQueryOverNoRowsGivesNullForEveryStatisticOfOneColumn()
    {
        PureQLProjection projection = Total(
            Values,
            [
                new SelectItemTotal("median", new MedianDecimalNullableTotal(V)),
                new SelectItemTotal(
                    "continuous",
                    new PercentileContinuousDecimalNullableTotal(
                        V,
                        new LiteralDecimal(0.5m)
                    )
                ),
                new SelectItemTotal(
                    "discrete",
                    new PercentileDiscreteIntegerNullableTotal(
                        N,
                        new LiteralDecimal(0.5m)
                    )
                ),
                new SelectItemTotal("mode", new ModeStringNullableTotal(S)),
                new SelectItemTotal("variance", new VariancePopulationTotal(V)),
                new SelectItemTotal("sample", new VarianceSampleTotal(V)),
                new SelectItemTotal("deviation", new StandardDeviationPopulationTotal(V)),
                new SelectItemTotal(
                    "sample_deviation",
                    new StandardDeviationSampleTotal(V)
                ),
            ],
            new LiteralBoolean(false)
        );

        foreach (
            string column in new[]
            {
                "median",
                "continuous",
                "discrete",
                "mode",
                "variance",
                "sample",
                "deviation",
                "sample_deviation",
            }
        )
        {
            Assert.Equal([""], ResultColumn.Texts(projection, column));
        }
    }

    [Fact]
    public void TotalQueryOverOneRowHasAPopulationButNoSampleVariance()
    {
        PureQLProjection projection = Total(
            Values,
            [
                new SelectItemTotal("variance", new VariancePopulationTotal(V)),
                new SelectItemTotal("sample", new VarianceSampleTotal(V)),
                new SelectItemTotal("deviation", new StandardDeviationPopulationTotal(V)),
                new SelectItemTotal(
                    "sample_deviation",
                    new StandardDeviationSampleTotal(V)
                ),
                new SelectItemTotal("covariance", new CovariancePopulationTotal(V, N)),
                new SelectItemTotal(
                    "sample_covariance",
                    new CovarianceSampleTotal(V, N)
                ),
                new SelectItemTotal("correlation", new CorrelationTotal(V, N)),
            ],
            new EqualDecimalRow(ValueId, new LiteralInteger(5))
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "variance"));
        Assert.Equal([""], ResultColumn.Texts(projection, "sample"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "deviation"));
        Assert.Equal([""], ResultColumn.Texts(projection, "sample_deviation"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "covariance"));
        Assert.Equal([""], ResultColumn.Texts(projection, "sample_covariance"));
        Assert.Equal([""], ResultColumn.Texts(projection, "correlation"));
    }

    [Fact]
    public void CovarianceAndCorrelationReadOnlyThePairsOfNonNullValues()
    {
        PureQLProjection projection = Grouped(
            Points,
            [
                new SelectItemGroup(
                    "population",
                    new CovariancePopulationDecimalNullableGroup(X, Y)
                ),
                new SelectItemGroup("sample", new CovarianceSampleGroup(X, Y)),
                new SelectItemGroup("correlation", new CorrelationGroup(X, Y)),
            ]
        );

        Assert.Equal(
            ["2.75", "0", "0", "0", "2.75"],
            ResultColumn.Texts(projection, "population")
        );
        Assert.Equal(
            [
                "3.666666666666666666666666667",
                "0",
                "0",
                "",
                "3.666666666666666666666666667",
            ],
            ResultColumn.Texts(projection, "sample")
        );
        Assert.Equal(
            [
                "0.9647638212377321361920414832",
                "",
                "",
                "",
                "0.9647638212377321361920414832",
            ],
            ResultColumn.Texts(projection, "correlation")
        );
    }

    [Fact]
    public void RegressionIsTheLeastSquaresLineAndNullWhereNoLineIsDefined()
    {
        PureQLProjection projection = Grouped(
            Points,
            [
                new SelectItemGroup("slope", new RegressionSlopeGroup(Y, X)),
                new SelectItemGroup("intercept", new RegressionInterceptGroup(Y, X)),
                new SelectItemGroup("r2", new RegressionRSquaredGroup(Y, X)),
            ]
        );

        Assert.Equal(
            ["2.2", "0", "", "", "2.2"],
            ResultColumn.Texts(projection, "slope")
        );
        Assert.Equal(
            ["-0.5", "3", "", "", "-0.5"],
            ResultColumn.Texts(projection, "intercept")
        );
        Assert.Equal(
            [
                "0.9307692307692307692307692308",
                "1",
                "",
                "",
                "0.9307692307692307692307692308",
            ],
            ResultColumn.Texts(projection, "r2")
        );
    }

    [Fact]
    public void PairStatisticsWithAPredicateReadOnlyThePairsOfItsRows()
    {
        LessThanOrEqualDecimalRow upTo13 = new(PointId, new LiteralInteger(13));
        PureQLProjection projection = Grouped(
            Points,
            [
                new SelectItemGroup(
                    "population",
                    new CovariancePopulationDecimalNullableGroup(X, Y, upTo13)
                ),
                new SelectItemGroup("sample", new CovarianceSampleGroup(X, Y, upTo13)),
                new SelectItemGroup("correlation", new CorrelationGroup(X, Y, upTo13)),
                new SelectItemGroup("slope", new RegressionSlopeGroup(Y, X, upTo13)),
                new SelectItemGroup(
                    "intercept",
                    new RegressionInterceptGroup(Y, X, upTo13)
                ),
                new SelectItemGroup("r2", new RegressionRSquaredGroup(Y, X, upTo13)),
            ]
        );

        Assert.Equal("1", ResultColumn.Texts(projection, "population")[4]);
        Assert.Equal("1.5", ResultColumn.Texts(projection, "sample")[4]);
        Assert.Equal(
            "0.9819805060619657156974386844",
            ResultColumn.Texts(projection, "correlation")[4]
        );
        Assert.Equal("1.5", ResultColumn.Texts(projection, "slope")[4]);
        Assert.Equal(
            "0.6666666666666666666666666667",
            ResultColumn.Texts(projection, "intercept")[4]
        );
        Assert.Equal(
            "0.9642857142857142857142857143",
            ResultColumn.Texts(projection, "r2")[4]
        );
    }

    [Fact]
    public void PairStatisticsOverAllReadEveryPairOfTheQuery()
    {
        PureQLProjection projection = Grouped(
            Points,
            [
                new SelectItemGroup(
                    "population",
                    new CovariancePopulationDecimalNullableGroup(
                        X,
                        Y,
                        over: AggregateOver.All
                    )
                ),
                new SelectItemGroup(
                    "sample",
                    new CovarianceSampleGroup(X, Y, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "correlation",
                    new CorrelationGroup(X, Y, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "slope",
                    new RegressionSlopeGroup(Y, X, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "intercept",
                    new RegressionInterceptGroup(Y, X, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "r2",
                    new RegressionRSquaredGroup(Y, X, over: AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(
            Enumerable.Repeat("1.867346938775510204081632653", 5),
            ResultColumn.Texts(projection, "population")
        );
        Assert.Equal(
            Enumerable.Repeat("2.010989010989010989010989011", 5),
            ResultColumn.Texts(projection, "sample")
        );
        Assert.Equal(
            Enumerable.Repeat("0.7914739265642150204314452597", 5),
            ResultColumn.Texts(projection, "correlation")
        );
        Assert.Equal(
            Enumerable.Repeat("1.663636363636363636363636364", 5),
            ResultColumn.Texts(projection, "slope")
        );
        Assert.Equal(
            Enumerable.Repeat("0.8636363636363636363636363636", 5),
            ResultColumn.Texts(projection, "intercept")
        );
        Assert.Equal(
            Enumerable.Repeat("0.6264309764309764309764309764", 5),
            ResultColumn.Texts(projection, "r2")
        );
    }

    [Fact]
    public void CovarianceOfAColumnWithItselfIsItsVariance()
    {
        Assert.Equal(
            ["1.25", "0", "0.2222222222222222222222222222"],
            Grouped(Values, new CovariancePopulationDecimalGroup(V, V))
        );
    }

    [Fact]
    public void PairStatisticsOfATotalAndOfAPlainQueryReadEveryPair()
    {
        PureQLProjection total = Total(
            Points,
            [
                new SelectItemTotal("correlation", new CorrelationTotal(X, Y)),
                new SelectItemTotal("r2", new RegressionRSquaredTotal(Y, X)),
            ]
        );
        PureQLProjection plain = Plain(
            Points,
            [
                new SelectItemProjection(
                    "intercept",
                    new RegressionInterceptProjection(Y, X)
                ),
            ]
        );

        Assert.Equal(
            ["0.7914739265642150204314452597"],
            ResultColumn.Texts(total, "correlation")
        );
        Assert.Equal(["0.6264309764309764309764309764"], ResultColumn.Texts(total, "r2"));
        Assert.Equal(
            Enumerable.Repeat("0.8636363636363636363636363636", 16),
            ResultColumn.Texts(plain, "intercept")
        );
    }

    [Fact]
    public void TotalQueryOverNoRowsGivesNullForEveryStatisticOfTwoColumns()
    {
        PureQLProjection projection = Total(
            Points,
            [
                new SelectItemTotal("population", new CovariancePopulationTotal(X, Y)),
                new SelectItemTotal("sample", new CovarianceSampleTotal(X, Y)),
                new SelectItemTotal("correlation", new CorrelationTotal(X, Y)),
                new SelectItemTotal("slope", new RegressionSlopeTotal(Y, X)),
                new SelectItemTotal("intercept", new RegressionInterceptTotal(Y, X)),
                new SelectItemTotal("r2", new RegressionRSquaredTotal(Y, X)),
            ],
            new LiteralBoolean(false)
        );

        foreach (
            string column in new[]
            {
                "population",
                "sample",
                "correlation",
                "slope",
                "intercept",
                "r2",
            }
        )
        {
            Assert.Equal([""], ResultColumn.Texts(projection, column));
        }
    }

    [Fact]
    public void AverageOfDatesIsTheMeanDayAndAMeanHalfwayIsTheLaterDay()
    {
        Assert.Equal(
            ["2024-01-02", "1969-12-31", "2024-01-04", "2024-01-01"],
            Grouped(Moments, new AverageDateGroup(Day))
        );
    }

    [Fact]
    public void AverageOfTimesIsTheMeanOfTheirSecondsWithoutWrapping()
    {
        Assert.Equal(
            ["00:00:00.000001", "12:00:00", "00:00:01.75", "10:00:00.000001"],
            Grouped(Moments, new AverageTimeGroup(Clock))
        );
    }

    [Fact]
    public void AverageOfDatetimesIsTheMeanInstantAndAMeanHalfwayIsTheLaterOne()
    {
        Assert.Equal(
            [
                "2024-01-01T00:00:00.000001Z",
                "1970-01-01T00:00:00Z",
                "2024-01-01T00:00:01.75Z",
                "2024-01-01T00:00:00Z",
            ],
            Grouped(Moments, new AverageDatetimeGroup(Instant))
        );
    }

    [Fact]
    public void MedianOfDatesAveragesTheTwoMiddleDaysAsAverageDoes()
    {
        Assert.Equal(
            ["2024-01-02", "1969-12-31", "2024-01-03", "2024-01-01"],
            Grouped(Moments, new MedianDateGroup(Day))
        );
    }

    [Fact]
    public void MedianOfTimesAndDatetimesRoundsToTheMicrosecond()
    {
        PureQLProjection projection = Grouped(
            Moments,
            [
                new SelectItemGroup("time", new MedianTimeGroup(Clock)),
                new SelectItemGroup("instant", new MedianDatetimeGroup(Instant)),
            ]
        );

        Assert.Equal(
            ["00:00:00.000001", "12:00:00", "00:00:01.5", "10:00:00.000001"],
            ResultColumn.Texts(projection, "time")
        );
        Assert.Equal(
            [
                "2024-01-01T00:00:00.000001Z",
                "1970-01-01T00:00:00Z",
                "2024-01-01T00:00:01.5Z",
                "2024-01-01T00:00:00Z",
            ],
            ResultColumn.Texts(projection, "instant")
        );
    }

    [Theory]
    [InlineData("0.9", "2024-01-02", "1969-12-31", "2024-01-08", "2024-01-02")]
    [InlineData("0.25", "2024-01-01", "1969-12-30", "2024-01-01", "2024-01-01")]
    public void PercentileContinuousOfDatesInterpolatesInDays(
        string fraction,
        string half,
        string pre,
        string spread,
        string third
    )
    {
        Assert.Equal(
            [half, pre, spread, third],
            Grouped(
                Moments,
                new PercentileContinuousDateGroup(
                    Day,
                    new LiteralDecimal(Parse(fraction))
                )
            )
        );
    }

    [Fact]
    public void PercentileContinuousOfTimesAndDatetimesInterpolatesInSeconds()
    {
        PureQLProjection projection = Grouped(
            Moments,
            [
                new SelectItemGroup(
                    "time",
                    new PercentileContinuousTimeGroup(Clock, new LiteralDecimal(0.9m))
                ),
                new SelectItemGroup(
                    "instant",
                    new PercentileContinuousDatetimeGroup(
                        Instant,
                        new LiteralDecimal(0.9m)
                    )
                ),
            ]
        );

        Assert.Equal(
            ["00:00:00.000001", "20:48:00", "00:00:03.4", "10:00:00.000001"],
            ResultColumn.Texts(projection, "time")
        );
        Assert.Equal(
            [
                "2024-01-01T00:00:00.000001Z",
                "1970-01-01T00:00:00Z",
                "2024-01-01T00:00:03.4Z",
                "2024-01-01T00:00:00.000001Z",
            ],
            ResultColumn.Texts(projection, "instant")
        );
    }

    [Fact]
    public void TemporalMeansOverAllAreTheMeansOfEveryRow()
    {
        PureQLProjection projection = Grouped(
            Moments,
            [
                new SelectItemGroup("day", new AverageDateGroup(Day, AggregateOver.All)),
                new SelectItemGroup(
                    "time",
                    new AverageTimeGroup(Clock, AggregateOver.All)
                ),
                new SelectItemGroup(
                    "instant",
                    new AverageDatetimeGroup(Instant, AggregateOver.All)
                ),
                new SelectItemGroup(
                    "median",
                    new MedianDateGroup(Day, AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(
            Enumerable.Repeat("2014-03-09", 4),
            ResultColumn.Texts(projection, "day")
        );
        Assert.Equal(
            Enumerable.Repeat("04:54:33.363637", 4),
            ResultColumn.Texts(projection, "time")
        );
        Assert.Equal(
            Enumerable.Repeat("2014-03-08T00:00:00.636364Z", 4),
            ResultColumn.Texts(projection, "instant")
        );
        Assert.Equal(
            Enumerable.Repeat("2024-01-01", 4),
            ResultColumn.Texts(projection, "median")
        );
    }

    [Fact]
    public void TemporalMeansOfAPlainAndOfATotalQuery()
    {
        PureQLProjection plain = Plain(
            Moments,
            [new SelectItemProjection("day", new AverageDateProjection(Day))]
        );
        PureQLProjection total = Total(
            Moments,
            [
                new SelectItemTotal(
                    "day",
                    new AverageDateNullableTotal(
                        Day,
                        new EqualStringRow(
                            new FieldString("t.moments", "g"),
                            new LiteralString("pre")
                        )
                    )
                ),
                new SelectItemTotal("instant", new MedianDatetimeNullableTotal(Instant)),
            ]
        );

        Assert.Equal(
            Enumerable.Repeat("2014-03-09", 11),
            ResultColumn.Texts(plain, "day")
        );
        Assert.Equal(["1969-12-31"], ResultColumn.Texts(total, "day"));
        Assert.Equal(["2024-01-01T00:00:00Z"], ResultColumn.Texts(total, "instant"));
    }

    [Fact]
    public void TotalQueryOverNoRowsGivesNullForEveryTemporalMean()
    {
        PureQLProjection projection = Total(
            Moments,
            [
                new SelectItemTotal("day", new AverageDateNullableTotal(Day)),
                new SelectItemTotal("time", new AverageTimeNullableTotal(Clock)),
                new SelectItemTotal("instant", new AverageDatetimeNullableTotal(Instant)),
                new SelectItemTotal("median", new MedianTimeNullableTotal(Clock)),
                new SelectItemTotal(
                    "p90",
                    new PercentileContinuousDateNullableTotal(
                        Day,
                        new LiteralDecimal(0.9m)
                    )
                ),
            ],
            new LiteralBoolean(false)
        );

        foreach (string column in new[] { "day", "time", "instant", "median", "p90" })
        {
            Assert.Equal([""], ResultColumn.Texts(projection, column));
        }
    }

    [Fact]
    public void CountDistinctSkipsNullsAndCountsOneAndOnePointZeroOnce()
    {
        Assert.Equal(["2", "0"], Grouped(Tags, new CountDistinctGroup(Tag)));
    }

    [Fact]
    public void CountDistinctTellsApartStringsThatDifferInCase()
    {
        Assert.Equal(["2", "0"], Grouped(Tags, new CountDistinctGroup(Label)));
    }

    [Fact]
    public void CountDistinctWithAPredicateAndOverAll()
    {
        NotEqualDecimalRow notFirst = new(TagId, new LiteralInteger(1));
        PureQLProjection projection = Grouped(
            Tags,
            [
                new SelectItemGroup("numbers", new CountDistinctGroup(Tag, notFirst)),
                new SelectItemGroup("labels", new CountDistinctGroup(Label, notFirst)),
                new SelectItemGroup(
                    "all_labels",
                    new CountDistinctGroup(Label, over: AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(["2", "0"], ResultColumn.Texts(projection, "numbers"));
        Assert.Equal(["1", "0"], ResultColumn.Texts(projection, "labels"));
        Assert.Equal(["2", "2"], ResultColumn.Texts(projection, "all_labels"));
    }

    [Fact]
    public void ConcatAllJoinsTheNonNullValuesInItsOwnOrder()
    {
        Assert.Equal(
            ["red; red; Red", ""],
            Grouped(
                Tags,
                new ConcatAllGroup(
                    Label,
                    new LiteralString("; "),
                    [new OrderItemRow(TagId, SortDirection.Desc)]
                )
            )
        );
    }

    [Fact]
    public void ConcatAllDistinctListsEachValueOnce()
    {
        Assert.Equal(
            ["Red; red", ""],
            Grouped(
                Tags,
                new ConcatAllGroup(
                    Label,
                    new LiteralString("; "),
                    [new OrderItemRow(Label)],
                    distinct: true
                )
            )
        );
    }

    [Fact]
    public void ConcatAllWithAPredicateAndOverAll()
    {
        PureQLProjection projection = Grouped(
            Tags,
            [
                new SelectItemGroup(
                    "late",
                    new ConcatAllGroup(
                        Label,
                        new LiteralString(","),
                        [new OrderItemRow(TagId)],
                        new GreaterThanDecimalRow(TagId, new LiteralInteger(3))
                    )
                ),
                new SelectItemGroup(
                    "all",
                    new ConcatAllGroup(
                        Label,
                        new LiteralString(","),
                        [new OrderItemRow(TagId)],
                        over: AggregateOver.All
                    )
                ),
            ]
        );

        Assert.Equal(["red", ""], ResultColumn.Texts(projection, "late"));
        Assert.Equal(
            ["Red,red,red", "Red,red,red"],
            ResultColumn.Texts(projection, "all")
        );
    }

    [Fact]
    public void CountDistinctAndConcatAllOfAPlainAndOfATotalQuery()
    {
        PureQLProjection plain = Plain(
            Tags,
            [
                new SelectItemProjection("distinct", new CountDistinctProjection(Tag)),
                new SelectItemProjection(
                    "labels",
                    new ConcatAllProjection(
                        Label,
                        new LiteralString(","),
                        [new OrderItemRow(TagId)]
                    )
                ),
            ]
        );
        PureQLProjection total = Total(
            Tags,
            [
                new SelectItemTotal("distinct", new CountDistinctTotal(Label)),
                new SelectItemTotal(
                    "labels",
                    new ConcatAllTotal(
                        Label,
                        new LiteralString(","),
                        [new OrderItemRow(TagId, SortDirection.Desc)],
                        distinct: true
                    )
                ),
            ]
        );

        Assert.Equal(Enumerable.Repeat("2", 6), ResultColumn.Texts(plain, "distinct"));
        Assert.Equal(
            Enumerable.Repeat("Red,red,red", 6),
            ResultColumn.Texts(plain, "labels")
        );
        Assert.Equal(["2"], ResultColumn.Texts(total, "distinct"));
        Assert.Equal(["red,Red"], ResultColumn.Texts(total, "labels"));
    }

    [Fact]
    public void TotalQueryOverNoRowsCountsNoValueAndJoinsTheEmptyString()
    {
        PureQLProjection projection = Total(
            Tags,
            [
                new SelectItemTotal("distinct", new CountDistinctTotal(Label)),
                new SelectItemTotal(
                    "labels",
                    new ConcatAllTotal(
                        Label,
                        new LiteralString(","),
                        [new OrderItemRow(TagId)]
                    )
                ),
                new SelectItemTotal(
                    "first",
                    new FirstStringNullableTotal(Label, [new OrderItemRow(TagId)])
                ),
            ],
            new LiteralBoolean(false)
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal([""], ResultColumn.Texts(projection, "labels"));
        Assert.Equal([""], ResultColumn.Texts(projection, "first"));
    }

    [Fact]
    public void FirstAndLastAreTheValuesOfTheFirstAndLastRowsOfTheirOrder()
    {
        PureQLProjection projection = Grouped(
            Tags,
            [
                new SelectItemGroup(
                    "first",
                    new FirstStringNullableGroup(Label, [new OrderItemRow(TagId)])
                ),
                new SelectItemGroup(
                    "last",
                    new LastStringNullableGroup(Label, [new OrderItemRow(TagId)])
                ),
            ]
        );

        Assert.Equal(["Red", ""], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["red", ""], ResultColumn.Texts(projection, "last"));
    }

    [Fact]
    public void FirstAndLastReturnTheNullValueOfTheRowTheyPick()
    {
        PureQLProjection projection = Grouped(
            Tags,
            [
                new SelectItemGroup(
                    "first",
                    new FirstStringNullableGroup(
                        Label,
                        [new OrderItemRow(Tag), new OrderItemRow(TagId)]
                    )
                ),
                new SelectItemGroup(
                    "last",
                    new LastDecimalNullableGroup(
                        Tag,
                        [
                            new OrderItemRow(Label, SortDirection.Desc),
                            new OrderItemRow(TagId),
                        ]
                    )
                ),
                new SelectItemGroup(
                    "last_ascending",
                    new LastDecimalNullableGroup(
                        Tag,
                        [new OrderItemRow(Label), new OrderItemRow(TagId)]
                    )
                ),
            ]
        );

        Assert.Equal(["", ""], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["", ""], ResultColumn.Texts(projection, "last"));
        Assert.Equal(["2", ""], ResultColumn.Texts(projection, "last_ascending"));
    }

    [Fact]
    public void FirstWithAPredicateAndOverAll()
    {
        PureQLProjection projection = Grouped(
            Tags,
            [
                new SelectItemGroup(
                    "largest",
                    new FirstStringNullableGroup(
                        Label,
                        [
                            new OrderItemRow(Tag, SortDirection.Desc),
                            new OrderItemRow(TagId),
                        ],
                        new NotEqualStringRow(Label, new LiteralStringNullable())
                    )
                ),
                new SelectItemGroup(
                    "overall",
                    new FirstStringNullableGroup(
                        Label,
                        [new OrderItemRow(TagId)],
                        over: AggregateOver.All
                    )
                ),
            ]
        );

        Assert.Equal(["red", ""], ResultColumn.Texts(projection, "largest"));
        Assert.Equal(["Red", "Red"], ResultColumn.Texts(projection, "overall"));
    }

    [Fact]
    public void LastOfATotalQuerySortsNullsFirst()
    {
        Assert.Equal(
            ["4"],
            ResultColumn.Texts(
                Total(
                    Tags,
                    [
                        new SelectItemTotal(
                            "v",
                            new LastIntegerNullableTotal(
                                TagId,
                                [new OrderItemRow(Tag), new OrderItemRow(TagId)]
                            )
                        ),
                    ]
                ),
                "v"
            )
        );
    }

    [Fact]
    public void AnyAndAllOverEachGroupAndOverAll()
    {
        GreaterThanDecimalRow large = new(V, new LiteralInteger(3));
        LessThanDecimalRow below5 = new(V, new LiteralInteger(5));
        PureQLProjection projection = Grouped(
            Values,
            [
                new SelectItemGroup("any", new AnyGroup(large)),
                new SelectItemGroup("all", new AllGroup(below5)),
                new SelectItemGroup(
                    "any_overall",
                    new AnyGroup(
                        new GreaterThanDecimalRow(V, new LiteralInteger(6)),
                        AggregateOver.All
                    )
                ),
                new SelectItemGroup(
                    "all_overall",
                    new AllGroup(below5, AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(["True", "True", "False"], ResultColumn.Texts(projection, "any"));
        Assert.Equal(["True", "False", "True"], ResultColumn.Texts(projection, "all"));
        Assert.Equal(
            ["True", "True", "True"],
            ResultColumn.Texts(projection, "any_overall")
        );
        Assert.Equal(
            ["False", "False", "False"],
            ResultColumn.Texts(projection, "all_overall")
        );
    }

    [Fact]
    public void DecimalSumIsExactAndRoundedOnce()
    {
        Assert.Equal(
            [
                "0.00000000000001",
                "9000000000000000000000000000",
                "0.0000000000000000000000000003",
                "4",
                "1.000000000000000000000000001",
            ],
            Grouped(Amounts, new SumDecimalGroup(Amount))
        );
    }

    [Fact]
    public void AverageIsTheExactSumDividedOnce()
    {
        Assert.Equal(
            [
                "0.000000000000005",
                "3000000000000000000000000000",
                "0.0000000000000000000000000002",
                "1.333333333333333333333333333",
                "0.3333333333333333333333333336",
            ],
            Grouped(Amounts, new AverageDecimalGroup(Amount))
        );
    }

    [Fact]
    public void MedianOfTwoMiddleValuesIsTheirMeanRoundedOnce()
    {
        Assert.Equal(
            [
                "0.000000000000005",
                "9000000000000000000000000000",
                "0.0000000000000000000000000002",
                "1",
                "0.0000000000000000000000000004",
            ],
            Grouped(Amounts, new MedianDecimalGroup(Amount))
        );
    }

    [Fact]
    public void StandardDeviationIsTheRootOfTheExactVariance()
    {
        PureQLProjection projection = Total(
            Amounts,
            [
                new SelectItemTotal("variance", new VariancePopulationTotal(Amount)),
                new SelectItemTotal(
                    "deviation",
                    new StandardDeviationPopulationTotal(Amount)
                ),
                new SelectItemTotal("sample", new VarianceSampleTotal(Amount)),
                new SelectItemTotal(
                    "sample_deviation",
                    new StandardDeviationSampleTotal(Amount)
                ),
            ],
            new EqualStringRow(
                new FieldString("t.amounts", "g"),
                new LiteralString("close")
            )
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "variance"));
        Assert.Equal(["0.000000000000005"], ResultColumn.Texts(projection, "deviation"));
        Assert.Equal(
            ["0.0000000000000000000000000001"],
            ResultColumn.Texts(projection, "sample")
        );
        Assert.Equal(
            ["0.0000000000000070710678118655"],
            ResultColumn.Texts(projection, "sample_deviation")
        );
    }

    [Fact]
    public void VarianceBeyondTheRangeOfADecimalIsAnError()
    {
        PureQLProjection projection = Total(
            Amounts,
            [new SelectItemTotal("variance", new VariancePopulationTotal(Amount))],
            new EqualStringRow(
                new FieldString("t.amounts", "g"),
                new LiteralString("huge")
            )
        );

        _ = Assert.Throws<OverflowException>(() =>
            ResultColumn.Texts(projection, "variance")
        );
    }

    private static decimal Parse(string text)
    {
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string Entity(IStoredSchemaDataSet table)
    {
        return "t." + table.Schema.Tables.Single().Name.TextValue;
    }

    private static IReadOnlyList<string> Grouped(
        IStoredSchemaDataSet table,
        ExprGroup expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return ResultColumn.Texts(
            Grouped(table, [new SelectItemGroup("v", expression)], parameters),
            "v"
        );
    }

    private static PureQLProjection Grouped(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemGroup> select,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        string entity = Entity(table);

        return new PureQLProjection(
            [table],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(entity),
                groupBy: [new GroupKey("g", new FieldString(entity, "g"))],
                select: select,
                orderBy: [new OrderItemGroupExpression(new KeyString("g"))]
            ),
            parameters ?? []
        );
    }

    private static PureQLProjection Total(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemTotal> aggregate,
        BooleanRow? where = null
    )
    {
        return new PureQLProjection(
            [table],
            new MainTotalQuery(
                from: new FromEntity(Entity(table)),
                aggregate: aggregate,
                where: where
            )
        );
    }

    private static PureQLProjection Plain(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemProjection> select
    )
    {
        string entity = Entity(table);

        return new PureQLProjection(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(entity),
                select: select,
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger(entity, "id")),
                ]
            )
        );
    }
}
