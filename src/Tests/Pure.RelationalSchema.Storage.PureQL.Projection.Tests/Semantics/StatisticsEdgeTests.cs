using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Edge cases of the statistical aggregates of "Aggregate evaluation", run as
// queries: the sample statistics of two rows and the deviations of values
// too small for their variance, medians and percentiles rounded half away
// from zero, percentiles of integers, of every type and at the exact edges
// of their fraction, fraction parameters at and just beyond 0 and 1, the
// tie rule of mode for every type, the statistics of two columns of a
// constant, a falling and a fractional line, a variance beyond the range of
// a decimal although it fits a System.Decimal, statistics over inline rows,
// and temporal percentiles exactly halfway. Every grouped column is read in
// the order of its key g.
public sealed class StatisticsEdgeTests
{
    private const string Max = "9223372036854775807";

    private const string Wide = "9999999999999999999999999997";

    private const string Tenth = "0.0000000000000000000000000001";

    private const string Uuid7 = "7fffffff-ffff-ffff-ffff-ffffffffffff";

    private const string Uuid8 = "80000000-0000-0000-0000-000000000000";

    private const string UuidLow = "00000000-0000-0000-8000-000000000000";

    [Fact]
    public void TwoRowsHaveASampleVarianceAndOneHasOnlyAPopulationOne()
    {
        IStoredSchemaDataSet table = Numbers(("one", "5"), ("two", "1"), ("two", "3"));
        FieldDecimal x = X(table);
        PureQLProjection projection = Grouped(
            table,
            [
                new SelectItemGroup("pop", new VariancePopulationDecimalGroup(x)),
                new SelectItemGroup("sample", new VarianceSampleGroup(x)),
                new SelectItemGroup(
                    "pop_dev",
                    new StandardDeviationPopulationDecimalGroup(x)
                ),
                new SelectItemGroup("sample_dev", new StandardDeviationSampleGroup(x)),
            ]
        );

        Assert.Equal(["0", "1"], ResultColumn.Texts(projection, "pop"));
        Assert.Equal(["", "2"], ResultColumn.Texts(projection, "sample"));
        Assert.Equal(["0", "1"], ResultColumn.Texts(projection, "pop_dev"));
        Assert.Equal(
            ["", "1.414213562373095048801688724"],
            ResultColumn.Texts(projection, "sample_dev")
        );
    }

    // "A standard deviation is the square root of the exact variance, not
    // of its rounded value": these variances round to 0, their roots do not.
    [Fact]
    public void DeviationsOfTinyValuesAreTheRootsOfTheirExactVariances()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", Tenth),
            ("a", "0.0000000000000000000000000003"),
            ("b", Tenth),
            ("b", "0.0000000000000000000000000005")
        );
        FieldDecimal x = X(table);
        PureQLProjection projection = Grouped(
            table,
            [
                new SelectItemGroup("pop", new VariancePopulationDecimalGroup(x)),
                new SelectItemGroup("sample", new VarianceSampleGroup(x)),
                new SelectItemGroup(
                    "pop_dev",
                    new StandardDeviationPopulationDecimalGroup(x)
                ),
                new SelectItemGroup("sample_dev", new StandardDeviationSampleGroup(x)),
            ]
        );

        Assert.Equal(["0", "0"], ResultColumn.Texts(projection, "pop"));
        Assert.Equal(["0", "0"], ResultColumn.Texts(projection, "sample"));
        Assert.Equal(
            [Tenth, "0.0000000000000000000000000002"],
            ResultColumn.Texts(projection, "pop_dev")
        );
        Assert.Equal(
            [Tenth, "0.0000000000000000000000000003"],
            ResultColumn.Texts(projection, "sample_dev")
        );
    }

    // "A result that needs more digits is rounded half away from zero"; the
    // median of 0 and 9999999999999999999999999997 is
    // 4999999999999999999999999998.5, which has 29 digits.
    [Fact]
    public void MedianAndPercentilesOfWideValuesRoundHalfAwayFromZero()
    {
        IStoredSchemaDataSet table = Numbers(
            ("neg", "0"),
            ("neg", "-" + Wide),
            ("pos", "0"),
            ("pos", Wide)
        );
        FieldDecimal x = X(table);
        PureQLProjection projection = Grouped(
            table,
            [
                new SelectItemGroup("median", new MedianDecimalGroup(x)),
                new SelectItemGroup(
                    "tenth",
                    new PercentileContinuousDecimalGroup(x, new LiteralDecimal(0.1m))
                ),
            ]
        );

        Assert.Equal(
            ["-4999999999999999999999999999", "4999999999999999999999999999"],
            ResultColumn.Texts(projection, "median")
        );
        Assert.Equal(
            ["-8999999999999999999999999997", "999999999999999999999999999.7"],
            ResultColumn.Texts(projection, "tenth")
        );
    }

    // percentileContinuous "takes the types average takes and a percentile
    // of integer values is a decimal".
    [Fact]
    public void MedianAndPercentilesOfIntegersAreExactDecimals()
    {
        IStoredSchemaDataSet table = Integers(
            ("big", Max),
            ("big", "9223372036854775806"),
            ("small", "1"),
            ("small", "2")
        );
        FieldInteger n = N(table);
        PureQLProjection projection = Grouped(
            table,
            [
                new SelectItemGroup("median", new MedianDecimalGroup(n)),
                new SelectItemGroup(
                    "quarter",
                    new PercentileContinuousDecimalGroup(n, new LiteralDecimal(0.25m))
                ),
            ]
        );

        Assert.Equal(
            ["9223372036854775806.5", "1.5"],
            ResultColumn.Texts(projection, "median")
        );
        Assert.Equal(
            ["9223372036854775806.25", "1.25"],
            ResultColumn.Texts(projection, "quarter")
        );
    }

    // percentileDiscrete: "the value number ceiling(fraction x n), counted
    // from 1, ... the first value for the fraction 0", over 1, 2, 3 and a
    // null: the fractions next to a third and two thirds fall on either side
    // of a whole number.
    [Theory]
    [InlineData("0", "1")]
    [InlineData("0.0000000000000000000000000001", "1")]
    [InlineData("0.3333333333333333333333333333", "1")]
    [InlineData("0.3333333333333333333333333334", "2")]
    [InlineData("0.6666666666666666666666666666", "2")]
    [InlineData("0.6666666666666666666666666667", "3")]
    [InlineData("1", "3")]
    public void PercentileDiscreteAtTheEdgesOfTheThirds(string fraction, string expected)
    {
        IStoredSchemaDataSet table = Integers(
            ("a", ""),
            ("a", "3"),
            ("a", "1"),
            ("a", "2")
        );
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal(
                    "v",
                    new PercentileDiscreteIntegerNullableTotal(
                        new FieldIntegerNullable(Entity(table), "n"),
                        new LiteralDecimal(Decimal(fraction))
                    )
                ),
            ]
        );

        Assert.Equal([expected], ResultColumn.Texts(projection, "v"));
    }

    // "the value at position fraction x (n - 1), counted from 0, among the n
    // non-null values in order": 0 and 1 are the least and the greatest.
    [Fact]
    public void PercentileContinuousAtZeroAndOneIsTheLeastAndTheGreatestValue()
    {
        IStoredSchemaDataSet table = Integers(
            ("a", ""),
            ("a", "3"),
            ("a", "-1"),
            ("a", "2")
        );
        FieldIntegerNullable n = new(Entity(table), "n");
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal(
                    "zero",
                    new PercentileContinuousDecimalNullableTotal(
                        n,
                        new LiteralDecimal(0m)
                    )
                ),
                new SelectItemTotal(
                    "one",
                    new PercentileContinuousDecimalNullableTotal(
                        n,
                        new LiteralDecimal(1m)
                    )
                ),
                new SelectItemTotal(
                    "third",
                    new PercentileContinuousDecimalNullableTotal(
                        n,
                        new LiteralDecimal(0.3333333333333333333333333333m)
                    )
                ),
            ]
        );

        Assert.Equal(["-1"], ResultColumn.Texts(projection, "zero"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "one"));

        // Position 0.6666666666666666666666666666, between -1 and 2:
        // -1 + 0.6666666666666666666666666666 x 3, which is exact.
        Assert.Equal(
            ["0.9999999999999999999999999998"],
            ResultColumn.Texts(projection, "third")
        );
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData("1", "3")]
    public void FractionParametersOfZeroAndOneAreAccepted(
        string fraction,
        string expected
    )
    {
        IStoredSchemaDataSet table = Integers(("a", "3"), ("a", "1"), ("a", "2"));
        PureQLProjection projection = Fractioned(table, fraction);

        Assert.Equal([expected], ResultColumn.Texts(projection, "discrete"));
        Assert.Equal([expected], ResultColumn.Texts(projection, "continuous"));
    }

    // A fraction parameter "below 0 or above 1" is an error (Execution
    // errors; Validation: "a fraction parameter to a value from 0 to 1").
    [Theory]
    [InlineData("1.0000000000000000000000000001")]
    [InlineData("-0.0000000000000000000000000001")]
    public void FractionParametersJustBeyondZeroToOneAreAnError(string fraction)
    {
        IStoredSchemaDataSet table = Integers(("a", "3"), ("a", "1"), ("a", "2"));

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Fractioned(table, fraction).ToList()
        );
    }

    // Validation: "A parameter value is written like a literal of its type:
    // one spelling"; a decimal literal has "no exponent, no leading zeros,
    // no trailing zeros in the fraction, no -0" (Types).
    [Theory(
        Skip = "Bug: a decimal parameter must be written in one spelling (Validation); "
            + "ParameterValues parses 0.50, 1.0, .5, 00.5, 5e-1, +0.5, -0 with "
            + "NumberStyles.Float and runs the query"
    )]
    [InlineData("0.50")]
    [InlineData("1.0")]
    [InlineData(".5")]
    [InlineData("00.5")]
    [InlineData("5e-1")]
    [InlineData("+0.5")]
    [InlineData("-0")]
    public void FractionParametersWrittenInAnotherSpellingAreAnError(string fraction)
    {
        IStoredSchemaDataSet table = Integers(("a", "3"), ("a", "1"), ("a", "2"));

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Fractioned(table, fraction).ToList()
        );
    }

    // "null for a non-null parameter" is an error (Execution errors).
    [Fact]
    public void AFractionParameterBoundToNullIsAnError()
    {
        IStoredSchemaDataSet table = Integers(("a", "3"), ("a", "1"), ("a", "2"));

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Fractioned(
                    table,
                    new TextParameter(new TextParameterType("decimal", true), "")
                )
                .ToList()
        );
    }

    // "a parameter used in several places has one type" (Validation).
    [Fact]
    public void AParameterThatIsAFractionAndASeparatorIsAnError()
    {
        IStoredSchemaDataSet table = Integers(("a", "3"), ("a", "1"));
        string entity = Entity(table);

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            new PureQLProjection(
                [table],
                new MainTotalQuery(
                    from: new FromEntity(entity),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "discrete",
                            new PercentileDiscreteIntegerNullableTotal(
                                N(table),
                                new ParamDecimal("p")
                            )
                        ),
                        new SelectItemTotal(
                            "joined",
                            new ConcatAllTotal(
                                new FieldString(entity, "g"),
                                new ParamString("p"),
                                [new OrderItemRow(N(table))]
                            )
                        ),
                    ]
                ),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("decimal", "0.5"),
                }
            ).ToList()
        );
    }

    // mode: "the non-null value that occurs most often, compared with equal;
    // among several, the least by the order of the type".
    [Fact]
    public void ModeBreaksTiesByTheOrderOfEveryType()
    {
        IStoredSchemaDataSet table = new InlineTable(
            "t",
            "ties",
            ("id", new LongColumnType()),
            ("n", new LongColumnType()),
            ("d", new DoubleColumnType()),
            ("b", new BoolColumnType()),
            ("day", new DateColumnType()),
            ("t", new TimeColumnType()),
            ("u", new UuidColumnType()),
            ("s", new StringColumnType())
        ).With(
            ["1", "2", "2", "true", "2024-01-02", "10:00:01", Uuid8, "b"],
            ["2", "1", "1.0", "false", "2024-01-01", "10:00:00", Uuid7, "a"],
            ["3", "2", "2.00", "true", "2024-01-02", "10:00:01", Uuid8, "b"],
            ["4", "1", "1", "false", "2024-01-01", "10:00:00", Uuid7, "a"],
            ["5", "3", "3", "", "", "", "", ""]
        );
        const string entity = "t.ties";
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal(
                    "n",
                    new ModeIntegerNullableTotal(new FieldInteger(entity, "n"))
                ),
                new SelectItemTotal(
                    "d",
                    new ModeDecimalNullableTotal(new FieldDecimal(entity, "d"))
                ),
                new SelectItemTotal(
                    "b",
                    new ModeBooleanNullableTotal(new FieldBooleanNullable(entity, "b"))
                ),
                new SelectItemTotal(
                    "day",
                    new ModeDateNullableTotal(new FieldDateNullable(entity, "day"))
                ),
                new SelectItemTotal(
                    "t",
                    new ModeTimeNullableTotal(new FieldTimeNullable(entity, "t"))
                ),
                new SelectItemTotal(
                    "u",
                    new ModeUuidNullableTotal(new FieldUuidNullable(entity, "u"))
                ),
                new SelectItemTotal(
                    "s",
                    new ModeStringNullableTotal(new FieldStringNullable(entity, "s"))
                ),
            ]
        );

        Assert.Equal(["1"], ResultColumn.Texts(projection, "n"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "d"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "b"));
        Assert.Equal(["2024-01-01"], ResultColumn.Texts(projection, "day"));
        Assert.Equal(["10:00:00"], ResultColumn.Texts(projection, "t"));
        Assert.Equal([Uuid7], ResultColumn.Texts(projection, "u"));
        Assert.Equal(["a"], ResultColumn.Texts(projection, "s"));
    }

    [Fact]
    public void ModeAndPercentilesOfAGroupWithoutValuesAreNull()
    {
        IStoredSchemaDataSet table = Integers(
            ("a", "1"),
            ("a", "1"),
            ("b", ""),
            ("b", "")
        );
        FieldIntegerNullable n = new(Entity(table), "n");
        PureQLProjection projection = Grouped(
            table,
            [
                new SelectItemGroup("mode", new ModeIntegerNullableGroup(n)),
                new SelectItemGroup(
                    "discrete",
                    new PercentileDiscreteIntegerNullableGroup(
                        n,
                        new LiteralDecimal(0.5m)
                    )
                ),
                new SelectItemGroup(
                    "continuous",
                    new PercentileContinuousDecimalNullableGroup(
                        n,
                        new LiteralDecimal(0.5m)
                    )
                ),
                new SelectItemGroup("median", new MedianDecimalNullableGroup(n)),
                new SelectItemGroup(
                    "variance",
                    new VariancePopulationDecimalNullableGroup(n)
                ),
                new SelectItemGroup(
                    "dropped",
                    new ModeIntegerNullableGroup(
                        n,
                        new GreaterThanDecimalRow(n, new LiteralInteger(1))
                    )
                ),
            ]
        );

        Assert.Equal(["1", ""], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(["1", ""], ResultColumn.Texts(projection, "discrete"));
        Assert.Equal(["1", ""], ResultColumn.Texts(projection, "continuous"));
        Assert.Equal(["1", ""], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["0", ""], ResultColumn.Texts(projection, "variance"));
        Assert.Equal(["", ""], ResultColumn.Texts(projection, "dropped"));
    }

    // percentileDiscrete returns "a value that occurs in the rows, so [it
    // exists] for every type", in the order of the type.
    [Fact]
    public void PercentileDiscreteOfEveryTypeFollowsTheOrderOfTheType()
    {
        IStoredSchemaDataSet table = new InlineTable(
            "t",
            "kinds",
            ("id", new LongColumnType()),
            ("b", new BoolColumnType()),
            ("u", new UuidColumnType()),
            ("s", new StringColumnType()),
            ("at", new DateTimeColumnType())
        ).With(
            ["1", "true", Uuid8, "\U00010000", "2024-01-01T03:00:00+03:00"],
            ["2", "false", Uuid7, "￿", "2023-12-31T22:00:00Z"],
            ["3", "true", UuidLow, "z", "2023-12-31T23:30:00-01:00"]
        );
        const string entity = "t.kinds";
        LiteralDecimal third = new(0.34m);
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal(
                    "b",
                    new PercentileDiscreteBooleanNullableTotal(
                        new FieldBoolean(entity, "b"),
                        third
                    )
                ),
                new SelectItemTotal(
                    "u",
                    new PercentileDiscreteUuidNullableTotal(
                        new FieldUuid(entity, "u"),
                        third
                    )
                ),
                new SelectItemTotal(
                    "s",
                    new PercentileDiscreteStringNullableTotal(
                        new FieldString(entity, "s"),
                        third
                    )
                ),
                new SelectItemTotal(
                    "at",
                    new PercentileDiscreteDatetimeNullableTotal(
                        new FieldDatetime(entity, "at"),
                        third
                    )
                ),
            ]
        );

        // The second of three values: ceiling(0.34 x 3) = 2.
        Assert.Equal(["True"], ResultColumn.Texts(projection, "b"));
        Assert.Equal([Uuid7], ResultColumn.Texts(projection, "u"));
        Assert.Equal(["￿"], ResultColumn.Texts(projection, "s"));
        Assert.Equal(["2024-01-01T00:00:00Z"], ResultColumn.Texts(projection, "at"));
    }

    // "Over (1, 3), (2, 3), (3, 3) the line is flat ... Over (1, 2), (1, 5)
    // no line is defined"; correlation is null "when either column has the
    // same value in every pair". left/right are (x, y).
    [Fact]
    public void PairStatisticsOfColumnsThatHoldOneValue()
    {
        IStoredSchemaDataSet table = Pairs(
            ("both", "2", "2"),
            ("both", "2", "2.0"),
            ("x", "4", "1"),
            ("x", "4", "2"),
            ("y", "1", "5"),
            ("y", "2", "5"),
            ("y", "3", "5")
        );
        PureQLProjection projection = PairStatistics(table);

        Assert.Equal(["0", "0", "0"], ResultColumn.Texts(projection, "cov_pop"));
        Assert.Equal(["0", "0", "0"], ResultColumn.Texts(projection, "cov_sample"));
        Assert.Equal(["", "", ""], ResultColumn.Texts(projection, "correlation"));
        Assert.Equal(["", "", "0"], ResultColumn.Texts(projection, "slope"));
        Assert.Equal(["", "", "5"], ResultColumn.Texts(projection, "intercept"));
        Assert.Equal(["", "", "1"], ResultColumn.Texts(projection, "r2"));
    }

    // A falling line, a line through thirds and a line with no correlation;
    // each statistic computed from exact values and rounded once.
    [Fact]
    public void PairStatisticsOfAFallingAFractionalAndAnUncorrelatedLine()
    {
        IStoredSchemaDataSet table = Pairs(
            ("falling", "1", "10"),
            ("falling", "2", "8"),
            ("falling", "3", "6"),
            ("thirds", "0", "0"),
            ("thirds", "1", "1"),
            ("thirds", "2", "1"),
            ("zero", "1", "1"),
            ("zero", "2", "-1"),
            ("zero", "3", "1")
        );
        PureQLProjection projection = PairStatistics(table);

        Assert.Equal(
            ["-1.333333333333333333333333333", "0.3333333333333333333333333333", "0"],
            ResultColumn.Texts(projection, "cov_pop")
        );
        Assert.Equal(["-2", "0.5", "0"], ResultColumn.Texts(projection, "cov_sample"));
        Assert.Equal(
            ["-1", "0.8660254037844386467637231708", "0"],
            ResultColumn.Texts(projection, "correlation")
        );
        Assert.Equal(["-2", "0.5", "0"], ResultColumn.Texts(projection, "slope"));
        Assert.Equal(
            ["12", "0.1666666666666666666666666667", "0.3333333333333333333333333333"],
            ResultColumn.Texts(projection, "intercept")
        );
        Assert.Equal(["1", "0.75", "0"], ResultColumn.Texts(projection, "r2"));
    }

    // "a result of magnitude 10^28 or more is an error": 2.25 x 10^28 still
    // fits a System.Decimal, but not a PureQL decimal; its root does.
    [Fact]
    public void AVarianceOfMagnitudeTenToTheTwentyEighthIsAnErrorButItsRootIsNot()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", "-150000000000000"),
            ("a", "150000000000000")
        );
        FieldDecimal x = X(table);

        Assert.Equal(
            ["150000000000000"],
            ResultColumn.Texts(
                Total(
                    table,
                    [new SelectItemTotal("v", new StandardDeviationPopulationTotal(x))]
                ),
                "v"
            )
        );
        Assert.Equal(
            ["212132034355964.2573202533086"],
            ResultColumn.Texts(
                Total(
                    table,
                    [new SelectItemTotal("v", new StandardDeviationSampleTotal(x))]
                ),
                "v"
            )
        );
        Assert.Equal(
            ["1"],
            ResultColumn.Texts(
                Total(table, [new SelectItemTotal("v", new CorrelationTotal(x, x))]),
                "v"
            )
        );

        ExprTotal[] overflowing =
        [
            new VariancePopulationTotal(x),
            new VarianceSampleTotal(x),
            new CovariancePopulationTotal(x, x),
            new CovarianceSampleTotal(x, x),
        ];

        Assert.All(
            overflowing,
            aggregate =>
                Assert.Throws<OverflowException>(() =>
                    Total(table, [new SelectItemTotal("v", aggregate)]).ToList()
                )
        );
    }

    [Fact]
    public void AVarianceJustBelowTenToTheTwentyEighthIsExact()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", "-99999999999999"),
            ("a", "99999999999999")
        );

        Assert.Equal(
            ["9999999999999800000000000001"],
            Grouped(table, new VariancePopulationDecimalGroup(X(table)))
        );
    }

    // Inline rows "return one row for each row written", and their column is
    // the least type of its cells: 1, 2.5, null, 4 as decimal?.
    [Fact]
    public void StatisticsOverInlineRows()
    {
        FieldDecimalNullable v = new("cells", "v");
        PureQLProjection projection = new(
            [],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryRows(
                        "cells",
                        [
                            [new RowItem("v", new LiteralInteger(1))],
                            [new RowItem("v", new LiteralDecimal(2.5m))],
                            [new RowItem("v", new LiteralDecimalNullable())],
                            [new RowItem("v", new LiteralInteger(4))],
                        ]
                    ),
                ],
                from: new FromSubquery("cells"),
                aggregate:
                [
                    new SelectItemTotal("median", new MedianDecimalNullableTotal(v)),
                    new SelectItemTotal("pop", new VariancePopulationTotal(v)),
                    new SelectItemTotal("sample", new VarianceSampleTotal(v)),
                    new SelectItemTotal("mode", new ModeDecimalNullableTotal(v)),
                    new SelectItemTotal(
                        "discrete",
                        new PercentileDiscreteDecimalNullableTotal(
                            v,
                            new LiteralDecimal(0.5m)
                        )
                    ),
                    new SelectItemTotal("cov", new CovariancePopulationTotal(v, v)),
                    new SelectItemTotal("distinct", new CountDistinctTotal(v)),
                ]
            )
        );

        Assert.Equal(["2.5"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["1.5"], ResultColumn.Texts(projection, "pop"));
        Assert.Equal(["2.25"], ResultColumn.Texts(projection, "sample"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(["2.5"], ResultColumn.Texts(projection, "discrete"));
        Assert.Equal(["1.5"], ResultColumn.Texts(projection, "cov"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "distinct"));
    }

    // Temporal percentiles "interpolate as average averages ... with a value
    // exactly halfway going to the later one", before 1970 as after it.
    [Theory]
    [InlineData(
        "0.04",
        "2024-01-01",
        "1969-12-21",
        "00:00:00",
        "1969-12-31T23:59:59.99999Z"
    )]
    [InlineData(
        "0.05",
        "2024-01-02",
        "1969-12-22",
        "00:00:00.000001",
        "1969-12-31T23:59:59.999991Z"
    )]
    [InlineData(
        "0.15",
        "2024-01-03",
        "1969-12-23",
        "00:00:00.000002",
        "1969-12-31T23:59:59.999992Z"
    )]
    [InlineData(
        "0.25",
        "2024-01-04",
        "1969-12-24",
        "00:00:00.000003",
        "1969-12-31T23:59:59.999993Z"
    )]
    public void TemporalPercentilesExactlyHalfwayGoToTheLaterValue(
        string fraction,
        string day,
        string early,
        string time,
        string instant
    )
    {
        IStoredSchemaDataSet table = new InlineTable(
            "t",
            "moments",
            ("id", new LongColumnType()),
            ("d", new DateColumnType()),
            ("e", new DateColumnType()),
            ("t", new TimeColumnType()),
            ("at", new DateTimeColumnType())
        ).With(
            ["1", "2024-01-01", "1969-12-21", "00:00:00", "1969-12-31T23:59:59.99999Z"],
            ["2", "2024-01-11", "1969-12-31", "00:00:00.00001", "1970-01-01T00:00:00Z"]
        );
        const string entity = "t.moments";
        LiteralDecimal share = new(Decimal(fraction));
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal(
                    "d",
                    new PercentileContinuousDateNullableTotal(
                        new FieldDate(entity, "d"),
                        share
                    )
                ),
                new SelectItemTotal(
                    "e",
                    new PercentileContinuousDateNullableTotal(
                        new FieldDate(entity, "e"),
                        share
                    )
                ),
                new SelectItemTotal(
                    "t",
                    new PercentileContinuousTimeNullableTotal(
                        new FieldTime(entity, "t"),
                        share
                    )
                ),
                new SelectItemTotal(
                    "at",
                    new PercentileContinuousDatetimeNullableTotal(
                        new FieldDatetime(entity, "at"),
                        share
                    )
                ),
            ]
        );

        Assert.Equal([day], ResultColumn.Texts(projection, "d"));
        Assert.Equal([early], ResultColumn.Texts(projection, "e"));
        Assert.Equal([time], ResultColumn.Texts(projection, "t"));
        Assert.Equal([instant], ResultColumn.Texts(projection, "at"));
    }

    // "datetime: mean instant, returned in UTC", whatever offset storage
    // wrote it with; the median of two values is their mean.
    [Fact]
    public void MeansOfDatetimesStoredWithOffsetsAreMeansOfTheirInstants()
    {
        IStoredSchemaDataSet table = new InlineTable(
            "t",
            "instants",
            ("id", new LongColumnType()),
            ("at", new DateTimeColumnType())
        ).With(["1", "2024-01-01T03:00:00+03:00"], ["2", "2023-12-31T20:00:00-05:00"]);
        FieldDatetime at = new("t.instants", "at");
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal("mean", new AverageDatetimeNullableTotal(at)),
                new SelectItemTotal("median", new MedianDatetimeNullableTotal(at)),
            ]
        );

        Assert.Equal(["2024-01-01T00:30:00Z"], ResultColumn.Texts(projection, "mean"));
        Assert.Equal(["2024-01-01T00:30:00Z"], ResultColumn.Texts(projection, "median"));
    }

    // The first and the last instant and time of day are an odd number of
    // microseconds apart: their mean is exactly halfway and goes to the later
    // microsecond; at 0.9 the position is .1 of a microsecond past one.
    [Fact]
    public void TemporalMeansAndPercentilesOfTheWidestValuesAreExact()
    {
        IStoredSchemaDataSet table = new InlineTable(
            "t",
            "ends",
            ("id", new LongColumnType()),
            ("t", new TimeColumnType()),
            ("at", new DateTimeColumnType())
        ).With(
            ["1", "00:00:00", "0001-01-01T00:00:00Z"],
            ["2", "23:59:59.999999", "9999-12-31T23:59:59.999999Z"]
        );
        FieldTime t = new("t.ends", "t");
        FieldDatetime at = new("t.ends", "at");
        PureQLProjection projection = Total(
            table,
            [
                new SelectItemTotal("t_mean", new AverageTimeNullableTotal(t)),
                new SelectItemTotal("t_median", new MedianTimeNullableTotal(t)),
                new SelectItemTotal(
                    "t_ninth",
                    new PercentileContinuousTimeNullableTotal(t, new LiteralDecimal(0.9m))
                ),
                new SelectItemTotal("at_mean", new AverageDatetimeNullableTotal(at)),
                new SelectItemTotal("at_median", new MedianDatetimeNullableTotal(at)),
                new SelectItemTotal(
                    "at_ninth",
                    new PercentileContinuousDatetimeNullableTotal(
                        at,
                        new LiteralDecimal(0.9m)
                    )
                ),
            ]
        );

        Assert.Equal(["12:00:00"], ResultColumn.Texts(projection, "t_mean"));
        Assert.Equal(["12:00:00"], ResultColumn.Texts(projection, "t_median"));
        Assert.Equal(["21:35:59.999999"], ResultColumn.Texts(projection, "t_ninth"));
        Assert.Equal(["5000-07-02T12:00:00Z"], ResultColumn.Texts(projection, "at_mean"));
        Assert.Equal(
            ["5000-07-02T12:00:00Z"],
            ResultColumn.Texts(projection, "at_median")
        );
        Assert.Equal(
            ["9000-02-06T02:23:59.999999Z"],
            ResultColumn.Texts(projection, "at_ninth")
        );
    }

    // over: "all" reads every row left by where, for each group.
    [Fact]
    public void StatisticsOverAllReadTheRowsLeftByWhere()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", "1"),
            ("a", "2"),
            ("b", "3"),
            ("b", "100")
        );
        FieldDecimal x = X(table);
        string entity = Entity(table);
        PureQLProjection projection = new(
            [table],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(entity),
                where: new LessThanDecimalRow(x, new LiteralInteger(100)),
                groupBy: [new GroupKey("g", new FieldString(entity, "g"))],
                select:
                [
                    new SelectItemGroup(
                        "median",
                        new MedianDecimalGroup(x, AggregateOver.All)
                    ),
                    new SelectItemGroup(
                        "variance",
                        new VariancePopulationDecimalGroup(x, AggregateOver.All)
                    ),
                    new SelectItemGroup(
                        "mode",
                        new ModeDecimalGroup(x, AggregateOver.All)
                    ),
                ],
                orderBy: [new OrderItemGroupExpression(new KeyString("g"))]
            )
        );

        Assert.Equal(["2", "2"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(
            ["0.6666666666666666666666666667", "0.6666666666666666666666666667"],
            ResultColumn.Texts(projection, "variance")
        );
        Assert.Equal(["1", "1"], ResultColumn.Texts(projection, "mode"));
    }

    [Fact]
    public void PlainQueryPercentilesReadEveryRowLeftByWhere()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", "4"),
            ("a", "1"),
            ("b", "3"),
            ("b", "2")
        );
        FieldDecimal x = X(table);
        string entity = Entity(table);
        PureQLProjection projection = new(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(entity),
                where: new LessThanDecimalRow(x, new LiteralInteger(4)),
                select:
                [
                    new SelectItemProjection(
                        "median",
                        new MedianDecimalProjection(x)
                    ),
                    new SelectItemProjection(
                        "discrete",
                        new PercentileDiscreteIntegerProjection(
                            new FieldInteger(entity, "id"),
                            new LiteralDecimal(1m)
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger(entity, "id")),
                ]
            )
        );

        Assert.Equal(["2", "2", "2"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["4", "4", "4"], ResultColumn.Texts(projection, "discrete"));
    }

    // "sum, average and the statistical aggregates are computed from exact
    // values and rounded once" half away from zero: 1.0000000000000000000000000005
    // has 29 digits and is 1.000000000000000000000000001, where half to even
    // gives 1 (and System.Decimal keeps all 29).
    [Fact]
    public void SumsAndMeansExactlyHalfwayRoundAwayFromZeroInEveryContext()
    {
        IStoredSchemaDataSet table = Numbers(
            ("a", "1"),
            ("a", "0.0000000000000000000000000005"),
            ("b", "2"),
            ("b", "0.000000000000000000000000002")
        );
        FieldDecimal x = X(table);
        string entity = Entity(table);
        PureQLProjection grouped = Grouped(
            table,
            [
                new SelectItemGroup("sum", new SumDecimalGroup(x)),
                new SelectItemGroup("mean", new AverageDecimalGroup(x)),
                new SelectItemGroup(
                    "all_sum",
                    new SumDecimalGroup(x, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "all_mean",
                    new AverageDecimalGroup(x, AggregateOver.All)
                ),
            ]
        );
        PureQLProjection total = Total(
            table,
            [
                new SelectItemTotal("sum", new SumDecimalTotal(x)),
                new SelectItemTotal(
                    "mean_a",
                    new AverageDecimalNullableTotal(
                        x,
                        new EqualStringRow(
                            new FieldString(entity, "g"),
                            new LiteralString("a")
                        )
                    )
                ),
            ]
        );
        PureQLProjection plain = new(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(entity),
                select: [new SelectItemProjection("sum", new SumDecimalProjection(x))]
            )
        );

        Assert.Equal(
            ["1.000000000000000000000000001", "2.000000000000000000000000002"],
            ResultColumn.Texts(grouped, "sum")
        );
        Assert.Equal(
            ["0.5000000000000000000000000003", "1.000000000000000000000000001"],
            ResultColumn.Texts(grouped, "mean")
        );
        Assert.Equal(
            ["3.000000000000000000000000003", "3.000000000000000000000000003"],
            ResultColumn.Texts(grouped, "all_sum")
        );
        Assert.Equal(
            ["0.7500000000000000000000000006", "0.7500000000000000000000000006"],
            ResultColumn.Texts(grouped, "all_mean")
        );
        Assert.Equal(["3.000000000000000000000000003"], ResultColumn.Texts(total, "sum"));
        Assert.Equal(
            ["0.5000000000000000000000000003"],
            ResultColumn.Texts(total, "mean_a")
        );
        Assert.Equal(
            Enumerable.Repeat("3.000000000000000000000000003", 4),
            ResultColumn.Texts(plain, "sum")
        );
    }

    [Fact]
    public void SumsAndMeansOverInlineRowsAreExactAndRoundedOnce()
    {
        PureQLProjection projection = new(
            [],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryRows(
                        "cells",
                        [
                            [
                                new RowItem("x", new LiteralDecimal(1m)),
                                new RowItem("n", new LiteralInteger(long.MaxValue)),
                            ],
                            [
                                new RowItem(
                                    "x",
                                    new LiteralDecimal(0.0000000000000000000000000005m)
                                ),
                                new RowItem("n", new LiteralInteger(1)),
                            ],
                            [
                                new RowItem("x", new LiteralDecimal(2m)),
                                new RowItem("n", new LiteralInteger(-1)),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("cells"),
                aggregate:
                [
                    new SelectItemTotal(
                        "sum",
                        new SumDecimalTotal(new FieldDecimal("cells", "x"))
                    ),
                    new SelectItemTotal(
                        "mean",
                        new AverageDecimalNullableTotal(new FieldDecimal("cells", "x"))
                    ),
                    new SelectItemTotal(
                        "units",
                        new SumIntegerTotal(new FieldInteger("cells", "n"))
                    ),
                ]
            )
        );

        Assert.Equal(
            ["3.000000000000000000000000001"],
            ResultColumn.Texts(projection, "sum")
        );
        Assert.Equal(["1"], ResultColumn.Texts(projection, "mean"));
        Assert.Equal([Max], ResultColumn.Texts(projection, "units"));
    }

    private static PureQLProjection Fractioned(
        IStoredSchemaDataSet table,
        string fraction
    )
    {
        return Fractioned(table, new TextParameter("decimal", fraction));
    }

    private static PureQLProjection Fractioned(
        IStoredSchemaDataSet table,
        IParameter fraction
    )
    {
        FieldInteger n = N(table);

        return new PureQLProjection(
            [table],
            new MainTotalQuery(
                from: new FromEntity(Entity(table)),
                aggregate:
                [
                    new SelectItemTotal(
                        "discrete",
                        new PercentileDiscreteIntegerNullableTotal(
                            n,
                            new ParamDecimal("fraction")
                        )
                    ),
                    new SelectItemTotal(
                        "continuous",
                        new PercentileContinuousDecimalNullableTotal(
                            n,
                            new ParamDecimal("fraction")
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["fraction"] = fraction,
            }
        );
    }

    private static PureQLProjection PairStatistics(IStoredSchemaDataSet table)
    {
        string entity = Entity(table);
        FieldDecimalNullable x = new(entity, "x");
        FieldDecimalNullable y = new(entity, "y");

        return Grouped(
            table,
            [
                new SelectItemGroup(
                    "cov_pop",
                    new CovariancePopulationDecimalNullableGroup(x, y)
                ),
                new SelectItemGroup("cov_sample", new CovarianceSampleGroup(x, y)),
                new SelectItemGroup("correlation", new CorrelationGroup(x, y)),
                new SelectItemGroup("slope", new RegressionSlopeGroup(y, x)),
                new SelectItemGroup("intercept", new RegressionInterceptGroup(y, x)),
                new SelectItemGroup("r2", new RegressionRSquaredGroup(y, x)),
            ]
        );
    }

    // A table of groups g and decimals x, one row per value.
    private static IStoredSchemaDataSet Numbers(params (string G, string X)[] rows)
    {
        return new InlineTable(
            "t",
            "numbers",
            ("id", new LongColumnType()),
            ("g", new StringColumnType()),
            ("x", new DoubleColumnType())
        ).With(
            [
                .. rows.Select(
                    (row, index) => new[] { (index + 1).ToString(), row.G, row.X }
                ),
            ]
        );
    }

    // A table of groups g and integers n, one row per value.
    private static IStoredSchemaDataSet Integers(params (string G, string N)[] rows)
    {
        return new InlineTable(
            "t",
            "integers",
            ("id", new LongColumnType()),
            ("g", new StringColumnType()),
            ("n", new LongColumnType())
        ).With(
            [
                .. rows.Select(
                    (row, index) => new[] { (index + 1).ToString(), row.G, row.N }
                ),
            ]
        );
    }

    // A table of groups g and pairs (x, y).
    private static IStoredSchemaDataSet Pairs(
        params (string G, string X, string Y)[] rows
    )
    {
        return new InlineTable(
            "t",
            "pairs",
            ("id", new LongColumnType()),
            ("g", new StringColumnType()),
            ("x", new DoubleColumnType()),
            ("y", new DoubleColumnType())
        ).With(
            [
                .. rows.Select(
                    (row, index) =>
                        new[] { (index + 1).ToString(), row.G, row.X, row.Y }
                ),
            ]
        );
    }

    private static FieldDecimal X(IStoredSchemaDataSet table)
    {
        return new FieldDecimal(Entity(table), "x");
    }

    private static FieldInteger N(IStoredSchemaDataSet table)
    {
        return new FieldInteger(Entity(table), "n");
    }

    private static decimal Decimal(string text)
    {
        return decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Entity(IStoredSchemaDataSet table)
    {
        return "t." + table.Schema.Tables.Single().Name.TextValue;
    }

    private static IReadOnlyList<string> Grouped(
        IStoredSchemaDataSet table,
        ExprGroup expression
    )
    {
        return ResultColumn.Texts(
            Grouped(table, [new SelectItemGroup("v", expression)]),
            "v"
        );
    }

    private static PureQLProjection Grouped(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemGroup> select
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
            )
        );
    }

    private static PureQLProjection Total(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemTotal> aggregate
    )
    {
        return new PureQLProjection(
            [table],
            new MainTotalQuery(
                from: new FromEntity(Entity(table)),
                aggregate: aggregate
            )
        );
    }
}
