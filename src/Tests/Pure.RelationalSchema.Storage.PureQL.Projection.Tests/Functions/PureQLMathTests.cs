using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

public sealed class PureQLMathTests
{
    private const decimal Largest = 9999999999999999999999999999m;

    private const decimal Smallest = 0.0000000000000000000000000001m;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void PiIsPiRoundedToTheDecimalPrecision()
    {
        Assert.Equal("3.141592653589793238462643383", Text(PureQLMath.Pi));
    }

    [Fact]
    public void AddIsExact()
    {
        Assert.Equal(0.3m, PureQLMath.Add(0.1m, 0.2m));
    }

    [Fact]
    public void SubtractIsExact()
    {
        Assert.Equal(0.1m, PureQLMath.Subtract(0.3m, 0.2m));
    }

    [Fact]
    public void DivideRoundsTheExactQuotientToTwentyEightDigits()
    {
        Assert.Equal("0.3333333333333333333333333333", Text(PureQLMath.Divide(1m, 3m)));
    }

    [Fact]
    public void EachStepOfADivisionOverAListIsRounded()
    {
        decimal step = PureQLMath.Divide(2m, 3m);

        Assert.Equal("0.6666666666666666666666666667", Text(step));
        Assert.Equal(
            "0.3333333333333333333333333334",
            Text(PureQLMath.Divide(step, 2m))
        );
        Assert.Equal("0.3333333333333333333333333333", Text(PureQLMath.Divide(2m, 6m)));
    }

    [Fact]
    public void EachStepOfAMultiplicationOverAListIsRounded()
    {
        decimal step = PureQLMath.Multiply(1.5m, Smallest);

        Assert.Equal("0.0000000000000000000000000002", Text(step));
        Assert.Equal(
            "0.0000000000000000000000000006",
            Text(PureQLMath.Multiply(step, 3m))
        );
        Assert.Equal(
            "0.0000000000000000000000000005",
            Text(PureQLMath.Multiply(1.5m, PureQLMath.Multiply(Smallest, 3m)))
        );
    }

    [Fact]
    public void AResultOfMagnitudeTenToTheTwentyEightIsAnError()
    {
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Add(Largest, 1m));
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Subtract(-Largest, 1m));
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Multiply(Largest, 1.1m));
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Divide(Largest, 0.9m));
    }

    [Fact]
    public void DivisionByZeroIsAnError()
    {
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Divide(1m, 0m));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.IntegerDivide(1m, 0m));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Modulo(1m, 0m));
        _ = Assert.Throws<DivideByZeroException>(() => PureQLMath.Modulo(1L, 0L));
        _ = Assert.Throws<DivideByZeroException>(() =>
            PureQLMath.RoundQuotient(1m, 0m, MidpointRounding.AwayFromZero)
        );
    }

    [Theory]
    [MemberData(
        nameof(PureQLMathVectors.Arithmetic),
        MemberType = typeof(PureQLMathVectors)
    )]
    public void ArithmeticIsExactAndRoundedOnce(
        string left,
        string right,
        string sum,
        string difference,
        string product,
        string quotient
    )
    {
        Same(sum, () => Plain(PureQLMath.Add(Number(left), Number(right))));
        Same(difference, () => Plain(PureQLMath.Subtract(Number(left), Number(right))));
        Same(product, () => Plain(PureQLMath.Multiply(Number(left), Number(right))));
        Same(quotient, () => Plain(PureQLMath.Divide(Number(left), Number(right))));
    }

    [Theory]
    [InlineData("7.5", "2", 3, "1.5")]
    [InlineData("-7.5", "2", -3, "-1.5")]
    [InlineData("7.5", "-2", -3, "1.5")]
    [InlineData("1", "0.3", 3, "0.1")]
    [InlineData("19.95", "0.05", 399, "0")]
    [InlineData(
        "3",
        "1.0000000000000000000000000001",
        2,
        "0.9999999999999999999999999998"
    )]
    [InlineData("-7", "2", -3, "-1")]
    public void IntegerDivideTruncatesTowardZeroAndModuloIsWhatIsLeft(
        string left,
        string right,
        long quotient,
        string remainder
    )
    {
        Assert.Equal(quotient, PureQLMath.IntegerDivide(Number(left), Number(right)));
        Assert.Equal(remainder, Text(PureQLMath.Modulo(Number(left), Number(right))));
    }

    [Fact]
    public void IntegerDivideIsNotTheTruncatedRoundedQuotient()
    {
        decimal right = 1.0000000000000000000000000001m;

        Assert.Equal(3m, PureQLMath.Truncate(PureQLMath.Divide(3m, right), 0));
        Assert.Equal(2L, PureQLMath.IntegerDivide(3m, right));
    }

    [Theory]
    [MemberData(
        nameof(PureQLMathVectors.Remainders),
        MemberType = typeof(PureQLMathVectors)
    )]
    public void IntegerDivideAndModuloAreExact(
        string left,
        string right,
        string quotient,
        string remainder
    )
    {
        Same(quotient, () => PureQLMath.IntegerDivide(Number(left), Number(right)));
        Same(remainder, () => PureQLMath.Modulo(Number(left), Number(right)));
    }

    [Fact]
    public void AnIntegerQuotientOutsideSixtyFourBitsIsAnError()
    {
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.IntegerDivide(9223372036854775808m, 1m)
        );
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.IntegerDivide(-9223372036854775809m, 1m)
        );
        Assert.Equal(long.MaxValue, PureQLMath.IntegerDivide(9223372036854775807.9m, 1m));
        Assert.Equal(long.MinValue, PureQLMath.IntegerDivide(9223372036854775808m, -1m));
    }

    [Fact]
    public void ModuloDoesNotFailWhenItsQuotientIsOutsideSixtyFourBits()
    {
        decimal left = 1000000000000000000000000000m;
        decimal right = 0.0000000000000000000000000003m;

        _ = Assert.Throws<OverflowException>(() => PureQLMath.IntegerDivide(left, right));
        Assert.Equal(Smallest, PureQLMath.Modulo(left, right));
    }

    [Fact]
    public void ModuloByOneIsTheFractionalPart()
    {
        Assert.Equal(0.75m, PureQLMath.Modulo(2.75m, 1m));
        Assert.Equal(-0.75m, PureQLMath.Modulo(-2.75m, 1m));
        Assert.Equal(0m, PureQLMath.Modulo(19.95m, 0.05m));
    }

    [Fact]
    public void ARemainderWithTheSignOfTheDivisorIsAComposition()
    {
        decimal remainder = PureQLMath.Modulo(
            PureQLMath.Add(PureQLMath.Modulo(-7m, 3m), 3m),
            3m
        );

        Assert.Equal(2m, remainder);
    }

    [Theory]
    [InlineData(-7, 2, -1)]
    [InlineData(7, -2, 1)]
    [InlineData(7, 2, 1)]
    [InlineData(-7, -2, -1)]
    [InlineData(6, 3, 0)]
    [InlineData(long.MinValue, -1, 0)]
    [InlineData(long.MinValue, 1, 0)]
    [InlineData(long.MaxValue, long.MinValue, long.MaxValue)]
    public void ModuloOfIntegersHasTheSignOfLeft(long left, long right, long expected)
    {
        Assert.Equal(expected, PureQLMath.Modulo(left, right));
    }

    [Theory]
    [InlineData("2.5", 0, "3", "2")]
    [InlineData("3.5", 0, "4", "4")]
    [InlineData("-2.5", 0, "-3", "-2")]
    [InlineData("2.345", 2, "2.35", "2.34")]
    [InlineData("2.355", 2, "2.36", "2.36")]
    [InlineData("1250", -2, "1300", "1200")]
    [InlineData("2.5000001", 0, "3", "3")]
    [InlineData("1234.5", -2, "1200", "1200")]
    public void RoundSendsAValueHalfwayAwayFromZeroOrToTheEvenNeighbour(
        string value,
        long digits,
        string away,
        string even
    )
    {
        Assert.Equal(
            away,
            Text(PureQLMath.Round(Number(value), digits, MidpointRounding.AwayFromZero))
        );
        Assert.Equal(
            even,
            Text(PureQLMath.Round(Number(value), digits, MidpointRounding.ToEven))
        );
    }

    [Theory]
    [InlineData("2.7", 0, "2")]
    [InlineData("-2.7", 0, "-2")]
    [InlineData("1234.567", 2, "1234.56")]
    [InlineData("1299", -2, "1200")]
    [InlineData("-1299.99", -2, "-1200")]
    public void TruncateDropsDigitsTowardZero(string value, long digits, string expected)
    {
        Assert.Equal(expected, Text(PureQLMath.Truncate(Number(value), digits)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Digits), MemberType = typeof(PureQLMathVectors))]
    public void RoundAndTruncateKeepThatManyDecimalPlaces(
        string value,
        string digits,
        string away,
        string even,
        string truncated
    )
    {
        long places = long.Parse(digits, Invariant);

        Same(
            away,
            () => PureQLMath.Round(Number(value), places, MidpointRounding.AwayFromZero)
        );
        Same(
            even,
            () => PureQLMath.Round(Number(value), places, MidpointRounding.ToEven)
        );
        Same(truncated, () => PureQLMath.Truncate(Number(value), places));
    }

    [Fact]
    public void ARoundedDecimalHasNoNegativeZero()
    {
        Assert.Equal("0", Text(PureQLMath.Round(-0.4m, 0, MidpointRounding.ToEven)));
        Assert.Equal("0", Text(PureQLMath.Round(-0.5m, 0, MidpointRounding.ToEven)));
        Assert.Equal("0", Text(PureQLMath.Truncate(-0.9m, 0)));
        Assert.Equal("0", Text(PureQLMath.Round(-4m, -1, MidpointRounding.AwayFromZero)));
    }

    [Theory]
    [InlineData(1250, -2, 1300, 1200)]
    [InlineData(1350, -2, 1400, 1400)]
    [InlineData(-1250, -2, -1300, -1200)]
    [InlineData(1249, -2, 1200, 1200)]
    [InlineData(1251, -2, 1300, 1300)]
    [InlineData(1250, 0, 1250, 1250)]
    [InlineData(1250, 5, 1250, 1250)]
    [InlineData(1250, -4, 0, 0)]
    [InlineData(5000, -4, 10000, 0)]
    public void RoundOfAnIntegerStaysAnInteger(
        long value,
        long digits,
        long away,
        long even
    )
    {
        Assert.Equal(
            away,
            PureQLMath.Round(value, digits, MidpointRounding.AwayFromZero)
        );
        Assert.Equal(even, PureQLMath.Round(value, digits, MidpointRounding.ToEven));
    }

    [Theory]
    [InlineData(1299, -2, 1200)]
    [InlineData(-1299, -2, -1200)]
    [InlineData(1299, 0, 1299)]
    [InlineData(1299, 7, 1299)]
    [InlineData(1299, -4, 0)]
    [InlineData(long.MaxValue, -18, 9000000000000000000)]
    [InlineData(long.MinValue, -18, -9000000000000000000)]
    [InlineData(long.MaxValue, -19, 0)]
    [InlineData(long.MinValue, long.MinValue, 0)]
    public void TruncateOfAnIntegerStaysAnInteger(long value, long digits, long expected)
    {
        Assert.Equal(expected, PureQLMath.Truncate(value, digits));
    }

    [Theory]
    [MemberData(
        nameof(PureQLMathVectors.IntegerDigits),
        MemberType = typeof(PureQLMathVectors)
    )]
    public void RoundAndTruncateOfAnIntegerGoToTensAndHundreds(
        string value,
        string digits,
        string away,
        string even,
        string truncated
    )
    {
        long whole = long.Parse(value, Invariant);
        long places = long.Parse(digits, Invariant);

        Same(away, () => PureQLMath.Round(whole, places, MidpointRounding.AwayFromZero));
        Same(even, () => PureQLMath.Round(whole, places, MidpointRounding.ToEven));
        Same(truncated, () => PureQLMath.Truncate(whole, places));
    }

    [Fact]
    public void ARoundedIntegerOutsideSixtyFourBitsIsAnError()
    {
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.Round(long.MaxValue, -3, MidpointRounding.AwayFromZero)
        );
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.Round(long.MinValue, -19, MidpointRounding.ToEven)
        );
        Assert.Equal(
            9223372036854775800,
            PureQLMath.Round(9223372036854775750, -2, MidpointRounding.ToEven)
        );
    }

    [Theory]
    [InlineData(MidpointRounding.ToZero)]
    [InlineData(MidpointRounding.ToNegativeInfinity)]
    [InlineData(MidpointRounding.ToPositiveInfinity)]
    [InlineData((MidpointRounding)17)]
    public void RoundTakesNoOtherMode(MidpointRounding mode)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLMath.Round(2.5m, 0, mode)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLMath.Round(25L, -1, mode)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLMath.RoundQuotient(5m, 2m, mode)
        );
    }

    [Theory]
    [InlineData("1.025", "0.05", "21", "20")]
    [InlineData("1.075", "0.05", "22", "22")]
    [InlineData("-1.025", "0.05", "-21", "-20")]
    [InlineData("1.025", "-0.05", "-21", "-20")]
    [InlineData("-1.025", "-0.05", "21", "20")]
    [InlineData("1", "3", "0", "0")]
    [InlineData("2", "3", "1", "1")]
    [InlineData("0", "3", "0", "0")]
    [InlineData("-1", "3", "0", "0")]
    public void RoundQuotientRoundsTheExactQuotientToAWholeNumber(
        string dividend,
        string divisor,
        string away,
        string even
    )
    {
        Assert.Equal(
            away,
            Text(
                PureQLMath.RoundQuotient(
                    Number(dividend),
                    Number(divisor),
                    MidpointRounding.AwayFromZero
                )
            )
        );
        Assert.Equal(
            even,
            Text(
                PureQLMath.RoundQuotient(
                    Number(dividend),
                    Number(divisor),
                    MidpointRounding.ToEven
                )
            )
        );
    }

    [Theory]
    [MemberData(
        nameof(PureQLMathVectors.RoundedQuotients),
        MemberType = typeof(PureQLMathVectors)
    )]
    public void RoundQuotientIsOneRounding(
        string dividend,
        string divisor,
        string away,
        string even
    )
    {
        Same(
            away,
            () =>
                PureQLMath.RoundQuotient(
                    Number(dividend),
                    Number(divisor),
                    MidpointRounding.AwayFromZero
                )
        );
        Same(
            even,
            () =>
                PureQLMath.RoundQuotient(
                    Number(dividend),
                    Number(divisor),
                    MidpointRounding.ToEven
                )
        );
    }

    [Fact]
    public void RoundQuotientIsNotTheRoundingOfARoundedQuotient()
    {
        decimal dividend = 2.4999999999999999999999999999m;
        decimal divisor = 0.9999999999999999999999999999m;
        decimal twice = PureQLMath.Round(
            PureQLMath.Divide(dividend, divisor),
            0,
            MidpointRounding.ToEven
        );

        Assert.Equal(2m, twice);
        Assert.Equal(
            3m,
            PureQLMath.RoundQuotient(dividend, divisor, MidpointRounding.ToEven)
        );
    }

    [Fact]
    public void RoundingToAStepIsARoundedQuotientTimesTheStep()
    {
        decimal away = PureQLMath.Multiply(
            PureQLMath.RoundQuotient(1.025m, 0.05m, MidpointRounding.AwayFromZero),
            0.05m
        );
        decimal even = PureQLMath.Multiply(
            PureQLMath.RoundQuotient(1.025m, 0.05m, MidpointRounding.ToEven),
            0.05m
        );

        Assert.Equal(1.05m, away);
        Assert.Equal(1m, even);
    }

    [Fact]
    public void PowerOfTenIsAOneWithThatManyZeros()
    {
        Assert.Equal(1m, PureQLMath.PowerOfTen(0));
        Assert.Equal(1000m, PureQLMath.PowerOfTen(3));
        Assert.Equal(10000000000000000000000000000m, PureQLMath.PowerOfTen(28));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.PowerOfTen(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.PowerOfTen(29));
    }

    [Theory]
    [InlineData("2", "10", "1024")]
    [InlineData("1.5", "2", "2.25")]
    [InlineData("2", "-2", "0.25")]
    [InlineData("-2", "3", "-8")]
    [InlineData("0", "0", "1")]
    [InlineData("-7.5", "0", "1")]
    [InlineData("0", "2.5", "0")]
    [InlineData("0.5", "29", "0.0000000018626451492309570313")]
    [InlineData("0.25", "14.5", "0.0000000018626451492309570313")]
    [InlineData("-0.5", "29", "-0.0000000018626451492309570313")]
    [InlineData("8", "0.3333333333333333333333333333", "2")]
    [InlineData("1.0000000000000000000000000001", "10000000000", "1.000000000000000001")]
    public void PowerOfAValueADecimalHoldsIsThatValue(
        string operand,
        string exponent,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Text(PureQLMath.Power(Number(operand), Number(exponent)))
        );
    }

    [Theory]
    [InlineData("2")]
    [InlineData("6.25")]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("9999999999999999999999999999")]
    [InlineData("0.1")]
    [InlineData("12345.6789")]
    public void SqrtEqualsThePowerOfOneHalf(string operand)
    {
        Assert.Equal(
            PureQLMath.Sqrt(Number(operand)),
            PureQLMath.Power(Number(operand), 0.5m)
        );
    }

    [Fact]
    public void RootsAndLogarithmsOfValuesADecimalHoldsAreExact()
    {
        Assert.Equal("2.5", Text(PureQLMath.Sqrt(6.25m)));
        Assert.Equal("3", Text(PureQLMath.Log10(1000m)));
        Assert.Equal("-3", Text(PureQLMath.Log10(0.001m)));
        Assert.Equal("3", Text(PureQLMath.Log(8m, 2m)));
        Assert.Equal("1.5", Text(PureQLMath.Log(8m, 4m)));
        Assert.Equal("1", Text(PureQLMath.Exp(0m)));
        Assert.Equal("0", Text(PureQLMath.Ln(1m)));
        Assert.Equal("0", Text(PureQLMath.Log10(1m)));
        Assert.Equal("0", Text(PureQLMath.Log(1m, 7m)));
    }

    [Fact]
    public void EveryOtherPowerRootAndLogarithmHasTheDigitsOfTheSpecification()
    {
        Assert.Equal("1.414213562373095048801688724", Text(PureQLMath.Sqrt(2m)));
        Assert.Equal("2.718281828459045235360287471", Text(PureQLMath.Exp(1m)));
        Assert.Equal("0.6931471805599453094172321215", Text(PureQLMath.Ln(2m)));
    }

    [Fact]
    public void AResultOfAPowerOutsideTheRangeIsAnErrorAndATinyOneIsZero()
    {
        Assert.Equal("6235149080811616882909238709", Text(PureQLMath.Exp(64m)));
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Exp(65m));
        Assert.Equal("0", Text(PureQLMath.Exp(-100m)));
        _ = Assert.Throws<OverflowException>(() => PureQLMath.Power(10m, 28m));
        Assert.Equal("0", Text(PureQLMath.Power(10m, -29m)));
    }

    [Fact]
    public void AValueOutsideTheDomainOfAPowerOrALogarithmIsAnError()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Sqrt(-1m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Ln(0m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Ln(-1m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log10(0m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log10(-10m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log(0m, 2m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log(-8m, 2m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log(8m, 0m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log(8m, -2m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Log(8m, 1m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Power(0m, -1m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Power(-2m, 0.5m));
    }

    [Fact]
    public void TrigonometryHasTheDigitsOfTheSpecification()
    {
        Assert.Equal("0.8414709848078965066525023216", Text(PureQLMath.Sin(1m)));
        Assert.Equal("0.5403023058681397174009366074", Text(PureQLMath.Cos(1m)));
        Assert.Equal("1.557407724654902230506974807", Text(PureQLMath.Tan(1m)));
        Assert.Equal("0.7853981633974483096156608458", Text(PureQLMath.Atan(1m)));
        Assert.Equal("0.5235987755982988730771072305", Text(PureQLMath.Asin(0.5m)));
        Assert.Equal("1.570796326794896619231321692", Text(PureQLMath.Acos(0m)));
        Assert.Equal("2.356194490192344928846982537", Text(PureQLMath.Atan2(1m, -1m)));
    }

    [Fact]
    public void PiIsNotPiItselfSoItsSineIsNotZero()
    {
        Assert.Equal(
            "0.0000000000000000000000000003",
            Text(PureQLMath.Sin(PureQLMath.Pi))
        );
        Assert.Equal("-1", Text(PureQLMath.Cos(PureQLMath.Pi)));
    }

    [Fact]
    public void TrigonometryOfZeroAndOneIsExact()
    {
        Assert.Equal("0", Text(PureQLMath.Sin(0m)));
        Assert.Equal("1", Text(PureQLMath.Cos(0m)));
        Assert.Equal("0", Text(PureQLMath.Tan(0m)));
        Assert.Equal("0", Text(PureQLMath.Asin(0m)));
        Assert.Equal("0", Text(PureQLMath.Acos(1m)));
        Assert.Equal("0", Text(PureQLMath.Atan(0m)));
        Assert.Equal("0", Text(PureQLMath.Atan2(0m, 1m)));
        Assert.Equal(PureQLMath.Pi, PureQLMath.Acos(-1m));
        Assert.Equal(PureQLMath.Pi, PureQLMath.Atan2(0m, -1m));
    }

    [Fact]
    public void DegreesBecomeRadiansWithPiOverOneHundredAndEighty()
    {
        decimal degree = PureQLMath.Divide(PureQLMath.Pi, 180m);
        decimal thirty = PureQLMath.Multiply(30m, degree);

        Assert.Equal("0.0174532925199432957692369077", Text(degree));
        Assert.Equal("0.523598775598298873077107231", Text(thirty));
        Assert.Equal("0.5000000000000000000000000004", Text(PureQLMath.Sin(thirty)));
    }

    [Fact]
    public void RadiansBecomeDegreesWithOneHundredAndEightyOverPi()
    {
        decimal radian = PureQLMath.Divide(180m, PureQLMath.Pi);

        Assert.Equal("57.29577951308232087679815482", Text(radian));
        Assert.Equal("180", Text(PureQLMath.Multiply(PureQLMath.Pi, radian)));
    }

    [Fact]
    public void ALargeAngleIsReducedWithEnoughDigitsOfPi()
    {
        decimal angle = 1000000000000000000000000000m;

        Assert.Equal("0.7180634961391176660795156521", Text(PureQLMath.Sin(angle)));
        Assert.Equal("-0.6959775969903538128251834949", Text(PureQLMath.Cos(angle)));
    }

    [Fact]
    public void TheTangentNextToAPoleIsFiniteUnlessItIsOutsideTheRange()
    {
        Assert.Equal(
            "-2775861216004249611331142063",
            Text(PureQLMath.Tan(1.570796326794896619231321692m))
        );
        _ = Assert.Throws<OverflowException>(() =>
            PureQLMath.Tan(1.5707963267948966192313216916m)
        );
    }

    [Fact]
    public void AValueOutsideTheDomainOfAnInverseFunctionIsAnError()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Asin(1.01m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Asin(-2m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Acos(1.01m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Acos(-2m));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PureQLMath.Atan2(0m, 0m));
    }

    [Fact]
    public void ARangeThatHoldsARoundingPointIsNarrowedUntilItDoesNot()
    {
        Assert.Equal(
            "1.000000000000000000000000001",
            Text(PureQLMath.Exp(0.0000000000000000000000000005m))
        );
        Assert.Equal("1", Text(PureQLMath.Cos(0.00000000000001m)));
        Assert.Equal(
            "1.000000000000000000000000016",
            Text(PureQLMath.Power(1.0000000000000000000000000005m, 31m))
        );
    }

    [Fact]
    public void TrailingZerosAndANegativeZeroAreTheSameOperand()
    {
        decimal zero = decimal.Negate(0.00m);

        Assert.Equal("8", Text(PureQLMath.Power(2.0m, 3.00m)));
        Assert.Equal("2", Text(PureQLMath.Sqrt(4.00m)));
        Assert.Equal("2", Text(PureQLMath.Log10(100.0m)));
        Assert.Equal("3", Text(PureQLMath.Log(8.0m, 2.00m)));
        Assert.Equal(PureQLMath.Atan(1m), PureQLMath.Atan2(2.50m, 2.5m));
        Assert.Equal("1", Text(PureQLMath.Exp(zero)));
        Assert.Equal("0", Text(PureQLMath.Sin(zero)));
        Assert.Equal("1", Text(PureQLMath.Power(zero, zero)));
        Assert.Equal("0", Text(PureQLMath.Atan2(zero, 1.0m)));
        Assert.Equal("0", Text(PureQLMath.Modulo(-4.0m, 2m)));
    }

    [Fact]
    public void AnAngleTooSmallForADecimalIsZeroWithoutASign()
    {
        Assert.Equal("0", Text(PureQLMath.Atan2(-Smallest, Largest)));
        Assert.Equal("0", Text(PureQLMath.Tan(-0m)));
        Assert.Equal(PureQLMath.Pi, PureQLMath.Atan2(Smallest, -Largest));
        Assert.Equal(-PureQLMath.Pi, PureQLMath.Atan2(-Smallest, -Largest));
    }

    [Fact]
    public void AResultDoesNotDependOnTheThreadThatComputesIt()
    {
        string[] results = new string[64];

        _ = Parallel.For(
            0,
            results.Length,
            index =>
                results[index] = string.Join(
                    ' ',
                    Text(PureQLMath.Sin(1000000000000000000000000000m)),
                    Text(PureQLMath.Exp(0.0000000000000000000000000005m)),
                    Text(PureQLMath.Log(2m, 0.9999999999999999999999999999m)),
                    Text(PureQLMath.Power(123.456m, 7.89m)),
                    Text(PureQLMath.Atan2(1m, -1m))
                )
        );

        Assert.All(
            results,
            result =>
                Assert.Equal(
                    "0.7180634961391176660795156521 1.000000000000000000000000001 "
                        + "-6931471805599453094172321214 31771028258180977.30906865968 "
                        + "2.356194490192344928846982537",
                    result
                )
        );
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Sqrt), MemberType = typeof(PureQLMathVectors))]
    public void SqrtIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Sqrt(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Exp), MemberType = typeof(PureQLMathVectors))]
    public void ExpIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Exp(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Ln), MemberType = typeof(PureQLMathVectors))]
    public void LnIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Ln(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Log10), MemberType = typeof(PureQLMathVectors))]
    public void Log10IsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Log10(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Log), MemberType = typeof(PureQLMathVectors))]
    public void LogIsTheExactValueRoundedOnce(
        string operand,
        string @base,
        string expected
    )
    {
        Same(expected, () => PureQLMath.Log(Number(operand), Number(@base)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Power), MemberType = typeof(PureQLMathVectors))]
    public void PowerIsTheExactValueRoundedOnce(
        string operand,
        string exponent,
        string expected
    )
    {
        Same(expected, () => PureQLMath.Power(Number(operand), Number(exponent)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Sin), MemberType = typeof(PureQLMathVectors))]
    public void SinIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Sin(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Cos), MemberType = typeof(PureQLMathVectors))]
    public void CosIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Cos(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Tan), MemberType = typeof(PureQLMathVectors))]
    public void TanIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Tan(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Asin), MemberType = typeof(PureQLMathVectors))]
    public void AsinIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Asin(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Acos), MemberType = typeof(PureQLMathVectors))]
    public void AcosIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Acos(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Atan), MemberType = typeof(PureQLMathVectors))]
    public void AtanIsTheExactValueRoundedOnce(string operand, string expected)
    {
        Same(expected, () => PureQLMath.Atan(Number(operand)));
    }

    [Theory]
    [MemberData(nameof(PureQLMathVectors.Atan2), MemberType = typeof(PureQLMathVectors))]
    public void Atan2IsTheExactValueRoundedOnce(string y, string x, string expected)
    {
        Same(expected, () => PureQLMath.Atan2(Number(y), Number(x)));
    }

    private static decimal Number(string text)
    {
        return decimal.Parse(text, NumberStyles.Number, Invariant);
    }

    // The value without the zeros that end its fraction: a sum, a difference
    // or a product of two short decimals keeps the scale System.Decimal gives
    // it, as 19.95 + 0.05 is 20.00, which is the same number.
    private static decimal Plain(decimal value)
    {
        return Number(value.ToString("0.############################", Invariant));
    }

    private static string Text(decimal value)
    {
        return value.ToString(Invariant);
    }

    // The result as the specification spells it, or the execution error the
    // vector names: a result outside the range, a value outside the domain,
    // a division by zero.
    private static void Same<T>(string expected, Func<T> compute)
        where T : IFormattable
    {
        if (expected == "overflow")
        {
            _ = Assert.Throws<OverflowException>(() => compute());
        }
        else if (expected == "domain")
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => compute());
        }
        else if (expected == "zero")
        {
            _ = Assert.Throws<DivideByZeroException>(() => compute());
        }
        else
        {
            Assert.Equal(
                expected,
                compute().ToString(null, Invariant)
            );
        }
    }
}
