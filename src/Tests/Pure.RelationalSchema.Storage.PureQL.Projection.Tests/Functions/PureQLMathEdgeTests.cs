using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// PureQLMath at the edges of the numbers of the specification ("Integer and
// decimal arithmetic"): results rounded half away from zero to the most
// decimal places that fit, at a midpoint and next to 10^28; zero written
// without a sign; round and truncate to every number of digits, from
// long.MinValue to long.MaxValue, of decimals and of integers next to the
// 64-bit range; integerDivide and modulo of the extreme values; powers,
// roots, logarithms and trigonometry where the result is exact, tiny, out
// of range or outside the domain. Each expected value is the exact value,
// computed with Python's decimal module at 140 digits and rounded by hand.
public sealed class PureQLMathEdgeTests
{
    private const decimal Largest = 9999999999999999999999999999m;

    private const decimal Smallest = 0.0000000000000000000000000001m;

    private const string HalfPi = "1.570796326794896619231321692";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData("1", "0.0000000000000000000000000004", "1")]
    [InlineData("1", "0.0000000000000000000000000005", "1.000000000000000000000000001")]
    [InlineData("-1", "-0.0000000000000000000000000005", "-1.000000000000000000000000001")]
    [InlineData("1", "-0.0000000000000000000000000005", "0.9999999999999999999999999995")]
    [InlineData("9999999999999999999999999999", "0.4", "9999999999999999999999999999")]
    [InlineData("9999999999999999999999999999", "-9999999999999999999999999999", "0")]
    [InlineData("9223372036854775807", "0.5", "9223372036854775807.5")]
    [InlineData("-1.5", "1.5", "0")]
    [InlineData("0.1234567890123456789012345678", "100", "100.1234567890123456789012346")]
    public void AddIsTheExactSumRoundedOnce(string left, string right, string expected)
    {
        Assert.Equal(Number(expected), PureQLMath.Add(Number(left), Number(right)));
        Assert.Equal(
            Number(expected),
            PureQLMath.Subtract(Number(left), -Number(right))
        );
    }

    [Theory]
    [InlineData("2.000000000000000000000000001", "0.5", "1.000000000000000000000000001")]
    [InlineData("-2.000000000000000000000000001", "0.5", "-1.000000000000000000000000001")]
    [InlineData("0.00000000000001", "0.000000000000001", "0")]
    [InlineData("-0.00000000000005", "0.000000000000001", "-0.0000000000000000000000000001")]
    [InlineData(
        "123456789012345678901234.5678",
        "1.0001",
        "123469134691246913469124.6913"
    )]
    [InlineData(
        "0.1234567890123456789012345678",
        "3",
        "0.3703703670370370367037037034"
    )]
    [InlineData(
        "99999999999999.99999999999999",
        "99999999999999",
        "9999999999999899999999999999"
    )]
    [InlineData("9999999999999999999999999999", "-1", "-9999999999999999999999999999")]
    [InlineData("-0.5", "0", "0")]
    public void MultiplyIsTheExactProductRoundedOnce(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(Number(expected), PureQLMath.Multiply(Number(left), Number(right)));
        Assert.Equal(Number(expected), PureQLMath.Multiply(Number(right), Number(left)));
    }

    [Theory]
    [InlineData("2.000000000000000000000000001", "2", "1.000000000000000000000000001")]
    [InlineData("-2", "3", "-0.6666666666666666666666666667")]
    [InlineData("2", "-3", "-0.6666666666666666666666666667")]
    [InlineData("0.0000000000000000000000000001", "2", "0.0000000000000000000000000001")]
    [InlineData("-0.0000000000000000000000000001", "2", "-0.0000000000000000000000000001")]
    [InlineData("-0.0000000000000000000000000001", "3", "0")]
    [InlineData("1", "9999999999999999999999999999", "0.0000000000000000000000000001")]
    [InlineData("9999999999999999999999999999", "7", "1428571428571428571428571428")]
    [InlineData("3.141592653589793238462643383", "2", HalfPi)]
    [InlineData("0", "-5", "0")]
    public void DivideIsTheExactQuotientRoundedOnce(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(Number(expected), PureQLMath.Divide(Number(left), Number(right)));
    }

    [Theory]
    [InlineData("add", "9999999999999999999999999999", "0.5")]
    [InlineData("add", "-9999999999999999999999999999", "-0.5")]
    [InlineData("subtract", "-9999999999999999999999999999", "0.5")]
    [InlineData("multiply", "5000000000000000000000000000", "2")]
    [InlineData("multiply", "9999999999999999999999999999", "9999999999999999999999999999")]
    [InlineData("divide", "1", "0.0000000000000000000000000001")]
    [InlineData("divide", "-1", "0.0000000000000000000000000001")]
    [InlineData("divide", "9999999999999999999999999999", "0.9999999999999999999999999999")]
    public void AResultThatRoundsToTenToTheTwentyEightIsAnError(
        string operation,
        string left,
        string right
    )
    {
        decimal l = Number(left);
        decimal r = Number(right);

        _ = Assert.Throws<OverflowException>(() =>
            operation switch
            {
                "add" => PureQLMath.Add(l, r),
                "subtract" => PureQLMath.Subtract(l, r),
                "multiply" => PureQLMath.Multiply(l, r),
                _ => PureQLMath.Divide(l, r),
            }
        );
    }

    [Theory]
    [InlineData("-0.4", 0L, false, "0")]
    [InlineData("-0.05", 1L, true, "0")]
    [InlineData("-0.05", 1L, false, "-0.1")]
    [InlineData("-0.15", 1L, true, "-0.2")]
    [InlineData("-0.0000000000000000000000000004", 27L, false, "0")]
    [InlineData("0.0000000000000000000000000005", 27L, false, "0.000000000000000000000000001")]
    [InlineData("0.0000000000000000000000000005", 27L, true, "0")]
    [InlineData("4999999999999999999999999999", -28L, false, "0")]
    [InlineData("5000000000000000000000000000", -28L, true, "0")]
    [InlineData("9999999999999999999999999999", -29L, false, "0")]
    [InlineData("9999999999999999999999999999", -30L, false, "0")]
    [InlineData("9999999999999999999999999999", long.MinValue, false, "0")]
    [InlineData("1234.5678", long.MaxValue, false, "1234.5678")]
    [InlineData("1234.5678", 28L, true, "1234.5678")]
    [InlineData("9999999999999999999999999999", -27L, false, "overflow")]
    [InlineData("9499999999999999999999999999", -27L, false, "9000000000000000000000000000")]
    [InlineData("1.5", 0L, true, "2")]
    [InlineData("-1250", -2L, true, "-1200")]
    public void RoundADecimalToEveryNumberOfDigits(
        string value,
        long digits,
        bool even,
        string expected
    )
    {
        MidpointRounding mode = even ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;

        Same(expected, () => PureQLMath.Round(Number(value), digits, mode));
    }

    [Theory]
    [InlineData("5000000000000000000000000000", -28L)]
    [InlineData("-5000000000000000000000000000", -28L)]
    [InlineData("9999999999999999999999999999", -1L)]
    [InlineData("-9999999999999999999999999999", -1L)]
    [InlineData("9999999999999999999999999999", -28L)]
    public void ADecimalRoundedToTenToTheTwentyEightIsAnError(string value, long digits)
    {
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.Round(Number(value), digits, MidpointRounding.AwayFromZero)
        );
    }

    [Theory]
    [InlineData("9999999999999999999999999999", -27L, "9000000000000000000000000000")]
    [InlineData("-9999999999999999999999999999", -27L, "-9000000000000000000000000000")]
    [InlineData("9999999999999999999999999999", -28L, "0")]
    [InlineData("9999999999999999999999999999", long.MinValue, "0")]
    [InlineData("1234.5678", long.MaxValue, "1234.5678")]
    [InlineData("-0.09", 1L, "0")]
    [InlineData("-0.0000000000000000000000000009", 27L, "0")]
    [InlineData("0.9999999999999999999999999999", 27L, "0.999999999999999999999999999")]
    public void TruncateADecimalToEveryNumberOfDigits(
        string value,
        long digits,
        string expected
    )
    {
        Assert.Equal(expected, Text(PureQLMath.Truncate(Number(value), digits)));
    }

    [Theory]
    [InlineData(-5L, -1L, -10L, 0L)]
    [InlineData(-15L, -1L, -20L, -20L)]
    [InlineData(-25L, -1L, -30L, -20L)]
    [InlineData(25L, -1L, 30L, 20L)]
    [InlineData(long.MinValue, -18L, -9000000000000000000L, -9000000000000000000L)]
    [InlineData(long.MinValue, -2L, -9223372036854775800L, -9223372036854775800L)]
    [InlineData(long.MaxValue, long.MinValue, 0L, 0L)]
    [InlineData(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue)]
    [InlineData(long.MaxValue, -20L, 0L, 0L)]
    [InlineData(4999999999999999999L, -19L, 0L, 0L)]
    [InlineData(-4999999999999999999L, -19L, 0L, 0L)]
    public void RoundAnIntegerToEveryNumberOfDigits(
        long value,
        long digits,
        long away,
        long even
    )
    {
        Assert.Equal(away, PureQLMath.Round(value, digits, MidpointRounding.AwayFromZero));
        Assert.Equal(even, PureQLMath.Round(value, digits, MidpointRounding.ToEven));
    }

    [Theory]
    [InlineData(long.MaxValue, -1L, false)]
    [InlineData(long.MinValue, -1L, false)]
    [InlineData(long.MinValue, -1L, true)]
    [InlineData(5000000000000000000L, -19L, false)]
    [InlineData(-5000000000000000000L, -19L, false)]
    [InlineData(long.MinValue, -19L, false)]
    public void AnIntegerRoundedOutsideSixtyFourBitsIsAnError(
        long value,
        long digits,
        bool even
    )
    {
        MidpointRounding mode = even ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;

        _ = Assert.Throws<OverflowException>(() => PureQLMath.Round(value, digits, mode));
    }

    [Theory]
    [InlineData(5000000000000000000L, -19L, 0L)]
    [InlineData(-5000000000000000000L, -19L, 0L)]
    [InlineData(15000000000000000L, -16L, 20000000000000000L)]
    [InlineData(-4500000000000000000L, -18L, -4000000000000000000L)]
    public void AnIntegerHalfwayRoundsToTheEvenNeighbour(long value, long digits, long even)
    {
        Assert.Equal(even, PureQLMath.Round(value, digits, MidpointRounding.ToEven));
    }

    [Theory]
    [InlineData(long.MinValue, -1L, -9223372036854775800L)]
    [InlineData(-5L, -1L, 0L)]
    [InlineData(long.MinValue, -19L, 0L)]
    [InlineData(long.MinValue, -18L, -9000000000000000000L)]
    [InlineData(long.MaxValue, long.MaxValue, long.MaxValue)]
    [InlineData(long.MinValue, 0L, long.MinValue)]
    public void TruncateAnIntegerToEveryNumberOfDigits(long value, long digits, long expected)
    {
        Assert.Equal(expected, PureQLMath.Truncate(value, digits));
    }

    [Theory]
    [InlineData("-9223372036854775808.9", "1", long.MinValue)]
    [InlineData("9223372036854775807.9", "1", long.MaxValue)]
    [InlineData("0", "-3", 0L)]
    [InlineData("9999999999999999999999999999", "9999999999999999999999999999", 1L)]
    [InlineData("0.0000000000000000000000000001", "9999999999999999999999999999", 0L)]
    [InlineData("-7.5", "-2", 3L)]
    [InlineData("-0.0000000000000000000000000001", "1", 0L)]
    [InlineData(
        "9999999999999999999999999999",
        "1084202172.485504434125002235",
        9223372036854775807L
    )]
    [InlineData(
        "9999999999999999999999999999",
        "1084202172.485504434125002236",
        9223372036854775806L
    )]
    [InlineData(
        "9999999999999999999999999999",
        "1084202172.485504434007452801",
        9223372036854775807L
    )]
    public void IntegerDivideTruncatesTheExactQuotient(
        string left,
        string right,
        long expected
    )
    {
        Assert.Equal(expected, PureQLMath.IntegerDivide(Number(left), Number(right)));
    }

    [Theory]
    [InlineData("9999999999999999999999999999", "0.0000000000000000000000000001")]
    [InlineData("9999999999999999999999999999", "1084202172.4855044340074528")]
    [InlineData("-9223372036854775809", "1")]
    public void AnIntegerQuotientOutsideSixtyFourBitsIsAnError(string left, string right)
    {
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.IntegerDivide(Number(left), Number(right))
        );
    }

    [Theory]
    [InlineData("-7.5", "-2", "-1.5")]
    [InlineData("5", "0.0000000000000000000000000003", "0.0000000000000000000000000002")]
    [InlineData("-0.0000000000000000000000000001", "1", "-0.0000000000000000000000000001")]
    [InlineData("9999999999999999999999999999", "2", "1")]
    [InlineData("-9999999999999999999999999999", "2", "-1")]
    [InlineData(
        "0.0000000000000000000000000001",
        "9999999999999999999999999999",
        "0.0000000000000000000000000001"
    )]
    [InlineData("-4.5", "1.5", "0")]
    [InlineData("-9999999999999999999999999999", "-9999999999999999999999999999", "0")]
    public void ModuloOfDecimalsIsWhatIsLeftWithTheSignOfLeft(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(expected, Text(PureQLMath.Modulo(Number(left), Number(right))));
    }

    [Theory]
    [InlineData(long.MinValue, long.MaxValue, -1L)]
    [InlineData(long.MinValue, 2L, 0L)]
    [InlineData(long.MinValue, long.MinValue, 0L)]
    [InlineData(long.MaxValue, long.MaxValue, 0L)]
    [InlineData(long.MaxValue, -1L, 0L)]
    [InlineData(0L, -1L, 0L)]
    [InlineData(-1L, long.MinValue, -1L)]
    [InlineData(long.MinValue + 1, long.MinValue, long.MinValue + 1)]
    public void ModuloOfIntegersAtTheEdgesOfTheRange(long left, long right, long expected)
    {
        Assert.Equal(expected, PureQLMath.Modulo(left, right));
    }

    [Fact]
    public void ADivisionOfTheExtremeIntegersByZeroIsAnError()
    {
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Modulo(long.MinValue, 0L));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Modulo(Largest, 0m));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.IntegerDivide(0m, 0m));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Divide(0m, 0m));
        _ = Assert.Throws<DivideByZeroException>(() =>
            PureQLMath.RoundQuotient(0m, 0m, MidpointRounding.ToEven)
        );
    }

    [Theory]
    [InlineData("-1", "2", false, "-1")]
    [InlineData("-1", "2", true, "0")]
    [InlineData("-3", "2", true, "-2")]
    [InlineData("1", "-2", false, "-1")]
    [InlineData("9999999999999999999999999999", "1", false, "9999999999999999999999999999")]
    [InlineData("-0.0000000000000000000000000001", "1", false, "0")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000002", false, "1")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000002", true, "0")]
    public void RoundQuotientRoundsTheExactQuotientToAWholeNumber(
        string dividend,
        string divisor,
        bool even,
        string expected
    )
    {
        MidpointRounding mode = even ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;

        Assert.Equal(
            expected,
            Text(PureQLMath.RoundQuotient(Number(dividend), Number(divisor), mode))
        );
    }

    [Fact]
    public void ARoundedQuotientOfMagnitudeTenToTheTwentyEightIsAnError()
    {
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.RoundQuotient(Largest, 0.5m, MidpointRounding.AwayFromZero)
        );
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.RoundQuotient(-Largest, 0.9m, MidpointRounding.ToEven)
        );
    }

    [Theory]
    [InlineData("2", "93", "9903520314283042199192993792")]
    [InlineData("-2", "93", "-9903520314283042199192993792")]
    [InlineData("0.5", "-93", "9903520314283042199192993792")]
    [InlineData("2", "-93", "0.0000000000000000000000000001")]
    [InlineData("2", "-94", "0.0000000000000000000000000001")]
    [InlineData("2", "-95", "0")]
    [InlineData("3", "-59", "0.0000000000000000000000000001")]
    [InlineData("3", "-60", "0")]
    [InlineData("-1", "9999999999999999999999999999", "-1")]
    [InlineData("-1", "9999999999999999999999999998", "1")]
    [InlineData("1", "9999999999999999999999999999", "1")]
    [InlineData("1", "-0.5", "1")]
    [InlineData("0", "9999999999999999999999999999", "0")]
    [InlineData("-0.5", "3", "-0.125")]
    [InlineData("-0.1", "28", "0.0000000000000000000000000001")]
    [InlineData("-0.1", "29", "0")]
    [InlineData("0.1", "-27", "1000000000000000000000000000")]
    [InlineData("4", "1.5", "8")]
    [InlineData("0.25", "-1.5", "8")]
    [InlineData("10", "27.9999", "9997697679981565863514160464")]
    [InlineData("1.0001", "10000", "2.718145926825224864037664675")]
    [InlineData("9999999999999999999999999999", "0.5", "99999999999999.99999999999999")]
    [InlineData("10", "-28", "0.0000000000000000000000000001")]
    [InlineData(
        "0.0000000000000000000000000001",
        "0.0000000000000000000000000001",
        "0.9999999999999999999999999936"
    )]
    [InlineData("2", "0.0000000000000000000000000001", "1")]
    [InlineData(
        "9999999999999999999999999999",
        "-0.0000000000000000000000000001",
        "0.9999999999999999999999999936"
    )]
    [InlineData("10", "27.99999999999999999999999999", "9999999999999999999999999770")]
    [InlineData("10", "27.9999999999999999999999999", "9999999999999999999999997697")]
    [InlineData("2", "92.5", "7002846371848267489841492278")]
    [InlineData("1.5", "159", "9965754625148675853717298925")]
    [InlineData("1.5", "160", "overflow")]
    [InlineData("1.5", "-160", "0.0000000000000000000000000001")]
    [InlineData(
        "0.9999999999999999999999999999",
        "9999999999999999999999999999",
        "0.3678794411714423215955237702"
    )]
    [InlineData("2", "94", "overflow")]
    [InlineData("0.5", "-94", "overflow")]
    [InlineData("0.1", "-28", "overflow")]
    [InlineData("0.0000000000000000000000000001", "-1", "overflow")]
    [InlineData("9999999999999999999999999999", "1.0000001", "overflow")]
    [InlineData("0", "-9999999999999999999999999999", "domain")]
    [InlineData("-1", "0.0000000000000000000000000001", "domain")]
    [InlineData("-0.0000000000000000000000000001", "0.5", "domain")]
    public void PowerAtTheEdgesOfItsRangeAndDomain(
        string operand,
        string exponent,
        string expected
    )
    {
        Same(expected, () => PureQLMath.Power(Number(operand), Number(exponent)));
    }

    [Theory]
    [InlineData("sqrt", "-0", "0")]
    [InlineData("sqrt", "1000000000000000000000000000", "31622776601683.79331998893544")]
    [InlineData("exp", "-64", "0.0000000000000000000000000002")]
    [InlineData("exp", "-64.5", "0.0000000000000000000000000001")]
    [InlineData("exp", "-65", "0.0000000000000000000000000001")]
    [InlineData("exp", "-65.2", "0")]
    [InlineData("exp", "-9999999999999999999999999999", "0")]
    [InlineData("exp", "64.4723", "9999173995783235459011188426")]
    [InlineData("exp", "64.4724", "overflow")]
    [InlineData("exp", "9999999999999999999999999999", "overflow")]
    [InlineData("exp", "0.0000000000000000000000000001", "1")]
    [InlineData("exp", "-0.0000000000000000000000000001", "0.9999999999999999999999999999")]
    [InlineData("exp", "0.6931471805599453094172321215", "2")]
    [InlineData("exp", "64.47238260383327915250376073", "9999999999999999999999999988")]
    [InlineData("exp", "-64.47238260383327915250376073", "0.0000000000000000000000000001")]
    [InlineData("exp", "64.47238260383327915250376", "9999999999999999999999992688")]
    [InlineData("sqrt", "0.9999999999999999999999999999", "0.9999999999999999999999999999")]
    [InlineData("sqrt", "1.000000000000000000000000001", "1")]
    [InlineData("log10", "1.000000000000000000000000001", "0.0000000000000000000000000004")]
    [InlineData("ln", "0.0000000000000000000000000001", "-64.47238260383327915250376073")]
    [InlineData("ln", "9999999999999999999999999999", "64.47238260383327915250376073")]
    [InlineData("ln", "1.000000000000000000000000001", "0.000000000000000000000000001")]
    [InlineData("ln", "0.9999999999999999999999999999", "-0.0000000000000000000000000001")]
    [InlineData("ln", "-0", "domain")]
    [InlineData("log10", "9999999999999999999999999999", "28")]
    [InlineData("log10", "0.0000000000000000000000000001", "-28")]
    [InlineData("log10", "0.0000000000000000000000000002", "-27.69897000433601880478626111")]
    [InlineData("log10", "-0", "domain")]
    public void RootsAndLogarithmsAtTheEdgesOfTheirRangeAndDomain(
        string function,
        string operand,
        string expected
    )
    {
        decimal value = operand == "-0" ? decimal.Negate(0m) : Number(operand);

        Same(
            expected,
            () =>
                function switch
                {
                    "sqrt" => PureQLMath.Sqrt(value),
                    "exp" => PureQLMath.Exp(value),
                    "ln" => PureQLMath.Ln(value),
                    _ => PureQLMath.Log10(value),
                }
        );
    }

    [Theory]
    [InlineData("8", "0.5", "-3")]
    [InlineData("0.25", "2", "-2")]
    [InlineData("27", "9", "1.5")]
    [InlineData("0.5", "0.25", "0.5")]
    [InlineData("2", "0.5", "-1")]
    [InlineData("1", "0.5", "0")]
    [InlineData("9999999999999999999999999999", "10", "28")]
    [InlineData("0.0000000000000000000000000001", "10", "-28")]
    [InlineData("10", "2", "3.321928094887362347870319429")]
    [InlineData(
        "0.9999999999999999999999999999",
        "0.9999999999999999999999999998",
        "0.5"
    )]
    [InlineData("9999999999999999999999999999", "0.9999999999999999999999999999", "overflow")]
    [InlineData("8", "1.0", "domain")]
    [InlineData("1", "1", "domain")]
    [InlineData("0", "1", "domain")]
    [InlineData("-8", "-2", "domain")]
    public void LogAtTheEdgesOfItsRangeAndDomain(
        string operand,
        string @base,
        string expected
    )
    {
        Same(expected, () => PureQLMath.Log(Number(operand), Number(@base)));
    }

    [Theory]
    [InlineData("sin", "1.570796326794896619231321692", "1")]
    [InlineData("cos", "1.570796326794896619231321692", "-0.0000000000000000000000000004")]
    [InlineData("sin", "-3.141592653589793238462643383", "-0.0000000000000000000000000003")]
    [InlineData("sin", "6.283185307179586476925286766", "-0.0000000000000000000000000006")]
    [InlineData("cos", "6.283185307179586476925286766", "1")]
    [InlineData("sin", "0.5235987755982988730771072305", "0.5")]
    [InlineData("cos", "1.047197551196597746154214461", "0.5000000000000000000000000001")]
    [InlineData("tan", "0.7853981633974483096156608458", "1")]
    [InlineData("tan", "-1", "-1.557407724654902230506974807")]
    [InlineData("sin", "-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [InlineData("tan", "-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [InlineData("asin", "-1.000", "-" + HalfPi)]
    [InlineData("asin", "0.9999999999999999999999999999", "1.570796326794882477095697961")]
    [InlineData("acos", "-1.0", "3.141592653589793238462643383")]
    [InlineData("acos", "1.00", "0")]
    [InlineData("acos", "0.5", "1.047197551196597746154214461")]
    [InlineData("acos", "-0.5", "2.094395102393195492308428922")]
    [InlineData("acos", "0.0000000000000000000000000001", HalfPi)]
    [InlineData("acos", "0.9999999999999999999999999999", "0.000000000000014142135623731")]
    [InlineData("acos", "-0.9999999999999999999999999999", "3.141592653589779096327019652")]
    [InlineData("atan", "9999999999999999999999999999", HalfPi)]
    [InlineData("atan", "-9999999999999999999999999999", "-" + HalfPi)]
    [InlineData("asin", "1.000000000000000000000000001", "domain")]
    [InlineData("acos", "-1.000000000000000000000000001", "domain")]
    [InlineData("acos", "9999999999999999999999999999", "domain")]
    public void TrigonometryAtTheEdgesOfItsDomain(
        string function,
        string operand,
        string expected
    )
    {
        decimal value = Number(operand);

        Same(
            expected,
            () =>
                function switch
                {
                    "sin" => PureQLMath.Sin(value),
                    "cos" => PureQLMath.Cos(value),
                    "tan" => PureQLMath.Tan(value),
                    "asin" => PureQLMath.Asin(value),
                    "acos" => PureQLMath.Acos(value),
                    _ => PureQLMath.Atan(value),
                }
        );
    }

    [Theory]
    [InlineData("0.0000000000000000000000000001", "0", HalfPi)]
    [InlineData("-0.0000000000000000000000000001", "0", "-" + HalfPi)]
    [InlineData("0", "-0.0000000000000000000000000001", "3.141592653589793238462643383")]
    [InlineData("-0", "-1", "3.141592653589793238462643383")]
    [InlineData("9999999999999999999999999999", "0.0000000000000000000000000001", HalfPi)]
    [InlineData("-9999999999999999999999999999", "0.0000000000000000000000000001", "-" + HalfPi)]
    [InlineData("0.0000000000000000000000000001", "9999999999999999999999999999", "0")]
    [InlineData("-0.5", "-0.5", "-2.356194490192344928846982537")]
    [InlineData("0", "-0", "domain")]
    [InlineData("-0", "0", "domain")]
    public void Atan2AtTheEdgesOfItsDomain(string y, string x, string expected)
    {
        Same(expected, () => PureQLMath.Atan2(Signed(y), Signed(x)));
    }

    // A zero computed from negative operands is written 0, not -0.
    [Fact]
    public void AZeroComputedFromNegativeOperandsIsWrittenWithoutASign()
    {
        Assert.Equal("0", Text(PureQLMath.Power(-0.1m, 29m)));
        Assert.Equal("0", Text(PureQLMath.Divide(-Smallest, 3m)));
        Assert.Equal(
            "0",
            Text(
                PureQLMath.Round(
                    -0.0000000000000000000000000004m,
                    27,
                    MidpointRounding.AwayFromZero
                )
            )
        );
        Assert.Equal("0", Text(PureQLMath.Truncate(-0.09m, 1)));
        Assert.Equal("0", Text(PureQLMath.Modulo(-4.5m, 1.5m)));
        Assert.Equal("0", Text(PureQLMath.Sqrt(decimal.Negate(0m))));
        Assert.Equal("0", Text(PureQLMath.Sin(decimal.Negate(0m))));
        Assert.Equal("0", Text(PureQLMath.Asin(decimal.Negate(0m))));
        Assert.Equal("0", Text(PureQLMath.Atan(decimal.Negate(0m))));
    }

    private static decimal Signed(string text)
    {
        return text == "-0" ? decimal.Negate(0m) : Number(text);
    }

    private static decimal Number(string text)
    {
        return decimal.Parse(text, NumberStyles.Number, Invariant);
    }

    private static string Text(decimal value)
    {
        return value.ToString(Invariant);
    }

    // The result as the specification spells it, or the execution error the
    // value names: a result outside the range, a value outside the domain.
    private static void Same(string expected, Func<decimal> compute)
    {
        if (expected == "overflow")
        {
            _ = Assert.Throws<OverflowException>(() => compute());
        }
        else if (expected == "domain")
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => compute());
        }
        else
        {
            Assert.Equal(expected, Text(compute()));
        }
    }
}
