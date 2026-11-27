using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The powers, roots, logarithms and trigonometry of the specification at the
// edges of their range and domain, in queries ("Integer and decimal
// arithmetic": each result is the exact value rounded half away from zero to
// the decimal precision; a result of magnitude 10^28 or more is an error and
// one too small for 28 decimal places is 0; a value outside the domain is an
// execution error; "Null semantics": every operand is lifted, also where the
// value would otherwise be an error; "Evaluation": if evaluates only the
// branch it takes). Each expected value is the exact value, computed with
// Python's decimal module at 140 digits and rounded by hand.
public sealed class MathOperatorEdgeTests
{
    private const string Largest = "9999999999999999999999999999";

    private const string Smallest = "0.0000000000000000000000000001";

    private const string HalfPi = "1.570796326794896619231321692";

    private const string Pi = "3.141592653589793238462643383";

    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    [Theory]
    [InlineData("2", "93", "9903520314283042199192993792")]
    [InlineData("-2", "93", "-9903520314283042199192993792")]
    [InlineData("0.5", "-93", "9903520314283042199192993792")]
    [InlineData("2", "-93", "0.0000000000000000000000000001")]
    [InlineData("2", "-94", "0.0000000000000000000000000001")]
    [InlineData("2", "-95", "0")]
    [InlineData("3", "-59", "0.0000000000000000000000000001")]
    [InlineData("3", "-60", "0")]
    [InlineData("-1", Largest, "-1")]
    [InlineData("-1", "9999999999999999999999999998", "1")]
    [InlineData("1", Largest, "1")]
    [InlineData("-1.5", "3", "-3.375")]
    [InlineData("-2", "-3", "-0.125")]
    [InlineData("-0.1", "28", Smallest)]
    [InlineData("-0.1", "29", "0")]
    [InlineData("0.1", "-27", "1000000000000000000000000000")]
    [InlineData("1.5", "-3", "0.2962962962962962962962962963")]
    [InlineData("2", "-0.5", "0.7071067811865475244008443621")]
    [InlineData("4", "1.5", "8")]
    [InlineData("0.25", "-1.5", "8")]
    [InlineData("10", "27.5", "3162277660168379331998893544")]
    [InlineData("10", "27.9999", "9997697679981565863514160464")]
    [InlineData("2", "0.1", "1.071773462536293164213006325")]
    [InlineData("1.0001", "10000", "2.718145926825224864037664675")]
    [InlineData(Largest, "0.5", "99999999999999.99999999999999")]
    [InlineData("0", Largest, "0")]
    [InlineData("0", "0.5", "0")]
    public void PowerAtTheEdgesOfItsRange(string operand, string exponent, string expected)
    {
        Assert.Equal(
            expected,
            Value(new PowerDecimalProjection(D(operand), D(exponent)))
        );
    }

    [Theory]
    [InlineData("2", "94")]
    [InlineData("0.5", "-94")]
    [InlineData("0.1", "-28")]
    [InlineData("10", "28")]
    [InlineData(Largest, "1.0000001")]
    [InlineData(Smallest, "-1")]
    public void PowerOfMagnitudeTenToTheTwentyEightFails(string operand, string exponent)
    {
        Fails<OverflowException>(new PowerDecimalProjection(D(operand), D(exponent)));
    }

    // "Domain": zero to a negative exponent, and a negative number to an
    // exponent that is not a whole number.
    [Theory]
    [InlineData("0", "-0.5")]
    [InlineData("0", "-" + Largest)]
    [InlineData("-8", "0.3333333333333333333333333333")]
    [InlineData("-2", "-0.5")]
    [InlineData("-" + Smallest, "0.5")]
    [InlineData("-1", Smallest)]
    public void PowerOutsideItsDomainFails(string operand, string exponent)
    {
        Fails<ArgumentOutOfRangeException>(
            new PowerDecimalProjection(D(operand), D(exponent))
        );
    }

    [Theory]
    [InlineData("sqrt", "0", "0")]
    [InlineData("sqrt", Smallest, "0.00000000000001")]
    [InlineData("sqrt", "0.0000000000000000000000000002", "0.000000000000014142135623731")]
    [InlineData("sqrt", Largest, "99999999999999.99999999999999")]
    [InlineData("sqrt", "1000000000000000000000000000", "31622776601683.79331998893544")]
    [InlineData("sqrt", "2.25", "1.5")]
    [InlineData("exp", "-64", "0.0000000000000000000000000002")]
    [InlineData("exp", "-64.5", Smallest)]
    [InlineData("exp", "-65", Smallest)]
    [InlineData("exp", "-65.2", "0")]
    [InlineData("exp", "-" + Largest, "0")]
    [InlineData("exp", "64.4", "9301749392230034903163564822")]
    [InlineData("exp", "64.4723", "9999173995783235459011188426")]
    [InlineData("exp", Smallest, "1")]
    [InlineData("exp", "-" + Smallest, "0.9999999999999999999999999999")]
    [InlineData("exp", "0.6931471805599453094172321215", "2")]
    [InlineData("ln", Smallest, "-64.47238260383327915250376073")]
    [InlineData("ln", Largest, "64.47238260383327915250376073")]
    [InlineData("ln", "1.000000000000000000000000001", "0.000000000000000000000000001")]
    [InlineData("ln", "0.9999999999999999999999999999", "-" + Smallest)]
    [InlineData("log10", Largest, "28")]
    [InlineData("log10", Smallest, "-28")]
    [InlineData("log10", "2", "0.3010299956639811952137388947")]
    [InlineData("log10", "0.5", "-0.3010299956639811952137388947")]
    [InlineData("log10", "0.0000000000000000000000000002", "-27.69897000433601880478626111")]
    public void RootsAndLogarithmsAtTheEdgesOfTheirRange(
        string function,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, Value(Unary(function, D(operand))));
    }

    [Theory]
    [InlineData("exp", "64.4724")]
    [InlineData("exp", "65")]
    [InlineData("exp", Largest)]
    public void ARootOrLogarithmOfMagnitudeTenToTheTwentyEightFails(
        string function,
        string operand
    )
    {
        Fails<OverflowException>(Unary(function, D(operand)));
    }

    // "Domain": a square root of a negative number, a logarithm of a number
    // that is not positive.
    [Theory]
    [InlineData("sqrt", "-" + Smallest)]
    [InlineData("sqrt", "-" + Largest)]
    [InlineData("ln", "-" + Smallest)]
    [InlineData("ln", "-" + Largest)]
    [InlineData("log10", "0")]
    [InlineData("log10", "-" + Smallest)]
    [InlineData("asin", "1.000000000000000000000000001")]
    [InlineData("asin", "-1.000000000000000000000000001")]
    [InlineData("acos", "1.000000000000000000000000001")]
    [InlineData("acos", "-1.000000000000000000000000001")]
    [InlineData("acos", Largest)]
    public void ARootLogarithmOrInverseOutsideItsDomainFails(
        string function,
        string operand
    )
    {
        Fails<ArgumentOutOfRangeException>(Unary(function, D(operand)));
    }

    [Theory]
    [InlineData("8", "0.5", "-3")]
    [InlineData("0.25", "2", "-2")]
    [InlineData("2", "4", "0.5")]
    [InlineData("27", "9", "1.5")]
    [InlineData("0.5", "0.25", "0.5")]
    [InlineData("2", "0.5", "-1")]
    [InlineData("1", "0.5", "0")]
    [InlineData(Largest, "10", "28")]
    [InlineData(Smallest, "10", "-28")]
    [InlineData("10", "2", "3.321928094887362347870319429")]
    public void LogIsTheLogarithmToItsBase(string operand, string @base, string expected)
    {
        Assert.Equal(expected, Value(new LogDecimalProjection(D(operand), D(@base))));
    }

    // "Domain": a logarithm to a base that is not positive or is 1, and of a
    // number that is not positive, whatever the base.
    [Theory]
    [InlineData("8", "1")]
    [InlineData("1", "1")]
    [InlineData("8", "0")]
    [InlineData("8", "-2")]
    [InlineData("0", "2")]
    [InlineData("-8", "-2")]
    [InlineData("0", "1")]
    public void LogOutsideItsDomainFails(string operand, string @base)
    {
        Fails<ArgumentOutOfRangeException>(new LogDecimalProjection(D(operand), D(@base)));
    }

    [Theory]
    [InlineData("sin", "-1", "-0.8414709848078965066525023216")]
    [InlineData("cos", "-1", "0.5403023058681397174009366074")]
    [InlineData("tan", "-1", "-1.557407724654902230506974807")]
    [InlineData("sin", HalfPi, "1")]
    [InlineData("cos", HalfPi, "-0.0000000000000000000000000004")]
    [InlineData("sin", "-" + Pi, "-0.0000000000000000000000000003")]
    [InlineData("cos", "-" + Pi, "-1")]
    [InlineData("sin", "6.283185307179586476925286766", "-0.0000000000000000000000000006")]
    [InlineData("cos", "6.283185307179586476925286766", "1")]
    [InlineData("sin", "0.5235987755982988730771072305", "0.5")]
    [InlineData("cos", "1.047197551196597746154214461", "0.5000000000000000000000000001")]
    [InlineData("tan", "0.7853981633974483096156608458", "1")]
    [InlineData("sin", Smallest, Smallest)]
    [InlineData("tan", Smallest, Smallest)]
    [InlineData("asin", "1", HalfPi)]
    [InlineData("asin", "-1", "-" + HalfPi)]
    [InlineData("asin", Smallest, Smallest)]
    [InlineData("asin", "0.9999999999999999999999999999", "1.570796326794882477095697961")]
    [InlineData("acos", "-1", Pi)]
    [InlineData("acos", "0.5", "1.047197551196597746154214461")]
    [InlineData("acos", "-0.5", "2.094395102393195492308428922")]
    [InlineData("acos", Smallest, HalfPi)]
    [InlineData("acos", "0.9999999999999999999999999999", "0.000000000000014142135623731")]
    [InlineData("acos", "-0.9999999999999999999999999999", "3.141592653589779096327019652")]
    [InlineData("atan", Largest, HalfPi)]
    [InlineData("atan", "-" + Largest, "-" + HalfPi)]
    [InlineData("atan", Smallest, Smallest)]
    public void TrigonometryAtTheEdgesOfItsDomain(
        string function,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, Value(Unary(function, D(operand))));
    }

    // "atan2": the angle of the point (x, y), above -pi up to pi.
    [Theory]
    [InlineData("1", "0", HalfPi)]
    [InlineData("-1", "0", "-" + HalfPi)]
    [InlineData(Smallest, "0", HalfPi)]
    [InlineData("0", "-0.5", Pi)]
    [InlineData("0", "-" + Smallest, Pi)]
    [InlineData("0", Largest, "0")]
    [InlineData("-0.5", "-0.5", "-2.356194490192344928846982537")]
    [InlineData("1", "1", "0.7853981633974483096156608458")]
    [InlineData(Largest, Smallest, HalfPi)]
    public void Atan2IsTheAngleAboveMinusPiUpToPi(string y, string x, string expected)
    {
        Assert.Equal(expected, Value(new Atan2DecimalProjection(D(y), D(x))));
    }

    // "pi" is pi rounded, and every operation over it rounds its own result.
    [Fact]
    public void PiTakesPartInArithmeticAsTheDecimalItIs()
    {
        Assert.Equal(
            "6.283185307179586476925286766",
            Value(new MultiplyDecimalProjection([new PiProjection(), I(2)]))
        );
        Assert.Equal(
            "-0.0000000000000000000000000006",
            Value(
                new SinDecimalProjection(
                    new MultiplyDecimalProjection([new PiProjection(), I(2)])
                )
            )
        );
        Assert.Equal(
            "0",
            Value(new SubtractDecimalProjection([new PiProjection(), D(Pi)]))
        );
        Assert.Equal(
            "-1",
            Value(
                new SignIntegerProjection(
                    new SubtractDecimalProjection([I(0), new PiProjection()])
                )
            )
        );
    }

    // "Null semantics": powers, logarithms and trigonometry are lifted over
    // every operand, so a null operand gives null where the other operands
    // would otherwise be an error or a fixed value.
    [Theory]
    [InlineData("power null to 0")]
    [InlineData("power 0 to null")]
    [InlineData("power null to -1")]
    [InlineData("log null to 1")]
    [InlineData("log 0 to null")]
    [InlineData("atan2 null, 0")]
    [InlineData("atan2 0, null")]
    [InlineData("sqrt null")]
    [InlineData("exp null")]
    [InlineData("asin null")]
    public void ANullOperandGivesNullAndNoError(string example)
    {
        LiteralDecimalNullable none = new();
        ExprProjection expression = example switch
        {
            "power null to 0" => new PowerDecimalNullableProjection(none, I(0)),
            "power 0 to null" => new PowerDecimalNullableProjection(I(0), none),
            "power null to -1" => new PowerDecimalNullableProjection(none, I(-1)),
            "log null to 1" => new LogDecimalNullableProjection(none, I(1)),
            "log 0 to null" => new LogDecimalNullableProjection(I(0), none),
            "atan2 null, 0" => new Atan2DecimalNullableProjection(none, I(0)),
            "atan2 0, null" => new Atan2DecimalNullableProjection(I(0), none),
            "sqrt null" => new SqrtDecimalNullableProjection(none),
            "exp null" => new ExpDecimalNullableProjection(none),
            "asin null" => new AsinDecimalNullableProjection(none),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal("", Value(expression));
    }

    // "Evaluation": if evaluates only the branch it takes, so a guarded
    // operation outside its domain never fails.
    [Theory]
    [InlineData("sqrt", "-1")]
    [InlineData("ln", "0")]
    [InlineData("exp", "65")]
    [InlineData("asin", "2")]
    public void IfEvaluatesOnlyTheBranchItTakes(string function, string operand)
    {
        IfDecimalProjection guarded = new(
            Operand(function, D(operand)),
            I(7),
            new EqualDecimalProjection(I(1), I(2))
        );

        Assert.Equal("7", Value(guarded));
    }

    private static ExprProjection Unary(string function, DecimalProjection operand)
    {
        return function switch
        {
            "sqrt" => new SqrtDecimalProjection(operand),
            "exp" => new ExpDecimalProjection(operand),
            "ln" => new LnDecimalProjection(operand),
            "log10" => new Log10DecimalProjection(operand),
            "sin" => new SinDecimalProjection(operand),
            "cos" => new CosDecimalProjection(operand),
            "tan" => new TanDecimalProjection(operand),
            "asin" => new AsinDecimalProjection(operand),
            "acos" => new AcosDecimalProjection(operand),
            "atan" => new AtanDecimalProjection(operand),
            _ => throw new ArgumentException(function),
        };
    }

    private static DecimalProjection Operand(string function, DecimalProjection operand)
    {
        return function switch
        {
            "sqrt" => new SqrtDecimalProjection(operand),
            "exp" => new ExpDecimalProjection(operand),
            "ln" => new LnDecimalProjection(operand),
            "log10" => new Log10DecimalProjection(operand),
            "sin" => new SinDecimalProjection(operand),
            "cos" => new CosDecimalProjection(operand),
            "tan" => new TanDecimalProjection(operand),
            "asin" => new AsinDecimalProjection(operand),
            "acos" => new AcosDecimalProjection(operand),
            "atan" => new AtanDecimalProjection(operand),
            _ => throw new ArgumentException(function),
        };
    }

    private static LiteralInteger I(long value)
    {
        return new LiteralInteger(value);
    }

    private static LiteralDecimal D(string text)
    {
        return new LiteralDecimal(decimal.Parse(text, CultureInfo.InvariantCulture));
    }

    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return new PureQLProjection(
            [One],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.one"),
                select: [new SelectItemProjection("value", expression)]
            )
        );
    }

    private static string Value(ExprProjection expression)
    {
        return Assert.Single(ResultColumn.Texts(Selecting(expression), "value"));
    }

    // The projection is built, so the query is valid; only reading it fails.
    private static void Fails<TException>(ExprProjection expression)
        where TException : Exception
    {
        PureQLProjection projection = Selecting(expression);

        _ = Assert.Throws<TException>(() => projection.ToList());
    }
}
