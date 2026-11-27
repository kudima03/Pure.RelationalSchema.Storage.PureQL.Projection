using System.Globalization;
using System.Runtime.CompilerServices;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The edges of integer and decimal arithmetic in queries, each expected value
// derived from the specification ("Numbers", "Integer and decimal
// arithmetic", "Execution errors", "Conversion results"), not from the
// implementation: integer results at and beyond the 64-bit range, for every
// operator that yields an integer; division by zero in every operator that
// divides; decimal results rounded half away from zero to the most decimal
// places that fit (28 significant digits, 28 decimal places), and those of
// magnitude 10^28 or more; zero without a sign; the one spelling of a
// decimal result; and parameter values that are not written like a literal.
public sealed class ArithmeticEdgeTests
{
    private const long Max = long.MaxValue;

    private const long Min = long.MinValue;

    private const string Largest = "9999999999999999999999999999";

    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    private static readonly string IntegerType = new LongColumnType().Name.TextValue;

    // "Execution errors": an integer outside the 64-bit range, in a result or
    // in a step of an operator over a list; "Integer and decimal arithmetic":
    // any integer result outside that range is an error, including abs,
    // round, truncate, floor and ceiling.
    [Theory]
    [InlineData("subtract least - 1")]
    [InlineData("add least + -1")]
    [InlineData("subtract greatest - -1")]
    [InlineData("subtract 0 - least")]
    [InlineData("multiply least x -1")]
    [InlineData("multiply 2^32 x 2^32")]
    [InlineData("multiply 3037000500 x 3037000500")]
    [InlineData("multiply list greatest, 2, 0.5")]
    [InlineData("subtract list least, 1, 0.5")]
    [InlineData("floor -9223372036854775808.1")]
    [InlineData("ceiling 9223372036854775807.1")]
    [InlineData("round 9223372036854775807.5")]
    [InlineData("round -9223372036854775808.5")]
    [InlineData("truncate 9223372036854775808.5")]
    [InlineData("round greatest to tens")]
    [InlineData("round least to tens")]
    [InlineData("round 5000000000000000000 to 10^19")]
    [InlineData("integerDivide -9223372036854775809 by 1")]
    public void IntegerResultOutsideSixtyFourBitsFails(string example)
    {
        ExprProjection expression = example switch
        {
            "subtract least - 1" => new SubtractIntegerProjection([I(Min), I(1)]),
            "add least + -1" => new AddIntegerProjection([I(Min), I(-1)]),
            "subtract greatest - -1" => new SubtractIntegerProjection([I(Max), I(-1)]),
            "subtract 0 - least" => new SubtractIntegerProjection([I(0), I(Min)]),
            "multiply least x -1" => new MultiplyIntegerProjection([I(Min), I(-1)]),
            "multiply 2^32 x 2^32" => new MultiplyIntegerProjection([
                I(4294967296),
                I(4294967296),
            ]),
            "multiply 3037000500 x 3037000500" => new MultiplyIntegerProjection([
                I(3037000500),
                I(3037000500),
            ]),
            "multiply list greatest, 2, 0.5" => new MultiplyDecimalProjection([
                I(Max),
                I(2),
                D("0.5"),
            ]),
            "subtract list least, 1, 0.5" => new SubtractDecimalProjection([
                I(Min),
                I(1),
                D("0.5"),
            ]),
            "floor -9223372036854775808.1" => new FloorIntegerProjection(
                D("-9223372036854775808.1")
            ),
            "ceiling 9223372036854775807.1" => new CeilingIntegerProjection(
                D("9223372036854775807.1")
            ),
            "round 9223372036854775807.5" => new RoundWithoutDigitsIntegerProjection(
                D("9223372036854775807.5")
            ),
            "round -9223372036854775808.5" => new RoundWithoutDigitsIntegerProjection(
                D("-9223372036854775808.5")
            ),
            "truncate 9223372036854775808.5" =>
                new TruncateWithoutDigitsIntegerProjection(D("9223372036854775808.5")),
            "round greatest to tens" => new RoundWithDigitsIntegerProjection(
                I(Max),
                I(-1)
            ),
            "round least to tens" => new RoundWithDigitsIntegerProjection(I(Min), I(-1)),
            "round 5000000000000000000 to 10^19" => new RoundWithDigitsIntegerProjection(
                I(5000000000000000000),
                I(-19)
            ),
            "integerDivide -9223372036854775809 by 1" =>
                new IntegerDivideIntegerProjection(D("-9223372036854775809"), I(1)),
            _ => throw new ArgumentException(example),
        };

        Fails<OverflowException>(expression);
    }

    // The same operators at the edges of the range: every result fits.
    [Theory]
    [InlineData("add greatest + least", "-1")]
    [InlineData("subtract -1 - greatest", "-9223372036854775808")]
    [InlineData("multiply least x 1", "-9223372036854775808")]
    [InlineData("multiply 3037000499 x 3037000499", "9223372030926249001")]
    [InlineData("integerDivide least by 2", "-4611686018427387904")]
    [InlineData("integerDivide greatest by -1", "-9223372036854775807")]
    [InlineData("integerDivide least by greatest", "-1")]
    [InlineData("modulo least by greatest", "-1")]
    [InlineData("modulo greatest by least", "9223372036854775807")]
    [InlineData("integerDivide -7 by -2", "3")]
    [InlineData("modulo -7 by -2", "-1")]
    [InlineData("integerDivide 7 by -2", "-3")]
    [InlineData("modulo 7 by -2", "1")]
    [InlineData("abs least + 1", "9223372036854775807")]
    [InlineData("abs 0", "0")]
    [InlineData("sign least", "-1")]
    [InlineData("sign greatest", "1")]
    [InlineData("sign -0.0000000000000000000000000001", "-1")]
    [InlineData("sign -9999999999999999999999999999", "-1")]
    [InlineData("floor 9223372036854775807.9", "9223372036854775807")]
    [InlineData("ceiling -9223372036854775808.9", "-9223372036854775808")]
    [InlineData("round 9223372036854775807.4", "9223372036854775807")]
    [InlineData("round halfEven -9223372036854775808.5", "-9223372036854775808")]
    [InlineData("round halfEven 9223372036854775806.5", "9223372036854775806")]
    [InlineData("truncate -9223372036854775808.9", "-9223372036854775808")]
    [InlineData("integerDivide -9223372036854775808.5 by 1", "-9223372036854775808")]
    [InlineData("floor -0.5", "-1")]
    [InlineData("ceiling -0.5", "0")]
    [InlineData("ceiling -0.9999999999999999999999999999", "0")]
    [InlineData("round -0.5", "-1")]
    [InlineData("round halfEven -0.5", "0")]
    [InlineData("round halfEven -1.5", "-2")]
    [InlineData("round -0.4999999999999999999999999999", "0")]
    [InlineData("truncate -0.9999999999999999999999999999", "0")]
    [InlineData("round -5 to tens", "-10")]
    [InlineData("round halfEven -5 to tens", "0")]
    [InlineData("round halfEven -15 to tens", "-20")]
    [InlineData("round halfEven -25 to tens", "-20")]
    [InlineData("round least to 10^18", "-9000000000000000000")]
    [InlineData("round least to hundreds", "-9223372036854775800")]
    [InlineData("round 4999999999999999999 to 10^19", "0")]
    [InlineData("round halfEven 5000000000000000000 to 10^19", "0")]
    [InlineData("round greatest to the least digits", "0")]
    [InlineData("round greatest to the greatest digits", "9223372036854775807")]
    [InlineData("truncate least to tens", "-9223372036854775800")]
    [InlineData("truncate -5 to tens", "0")]
    [InlineData("truncate greatest to the least digits", "0")]
    public void IntegerResultAtTheEdgeOfTheRangeFits(string example, string expected)
    {
        ExprProjection expression = example switch
        {
            "add greatest + least" => new AddIntegerProjection([I(Max), I(Min)]),
            "subtract -1 - greatest" => new SubtractIntegerProjection([I(-1), I(Max)]),
            "multiply least x 1" => new MultiplyIntegerProjection([I(Min), I(1)]),
            "multiply 3037000499 x 3037000499" => new MultiplyIntegerProjection([
                I(3037000499),
                I(3037000499),
            ]),
            "integerDivide least by 2" => new IntegerDivideIntegerProjection(I(Min), I(2)),
            "integerDivide greatest by -1" => new IntegerDivideIntegerProjection(
                I(Max),
                I(-1)
            ),
            "integerDivide least by greatest" => new IntegerDivideIntegerProjection(
                I(Min),
                I(Max)
            ),
            "modulo least by greatest" => new ModuloIntegerProjection(I(Min), I(Max)),
            "modulo greatest by least" => new ModuloIntegerProjection(I(Max), I(Min)),
            "integerDivide -7 by -2" => new IntegerDivideIntegerProjection(I(-7), I(-2)),
            "modulo -7 by -2" => new ModuloIntegerProjection(I(-7), I(-2)),
            "integerDivide 7 by -2" => new IntegerDivideIntegerProjection(I(7), I(-2)),
            "modulo 7 by -2" => new ModuloIntegerProjection(I(7), I(-2)),
            "abs least + 1" => new AbsIntegerProjection(I(Min + 1)),
            "abs 0" => new AbsIntegerProjection(I(0)),
            "sign least" => new SignIntegerProjection(I(Min)),
            "sign greatest" => new SignIntegerProjection(I(Max)),
            "sign -0.0000000000000000000000000001" => new SignIntegerProjection(
                D("-0.0000000000000000000000000001")
            ),
            "sign -9999999999999999999999999999" => new SignIntegerProjection(
                D("-" + Largest)
            ),
            "floor 9223372036854775807.9" => new FloorIntegerProjection(
                D("9223372036854775807.9")
            ),
            "ceiling -9223372036854775808.9" => new CeilingIntegerProjection(
                D("-9223372036854775808.9")
            ),
            "round 9223372036854775807.4" => new RoundWithoutDigitsIntegerProjection(
                D("9223372036854775807.4")
            ),
            "round halfEven -9223372036854775808.5" =>
                new RoundWithoutDigitsIntegerProjection(
                    D("-9223372036854775808.5"),
                    RoundingMode.HalfEven
                ),
            "round halfEven 9223372036854775806.5" =>
                new RoundWithoutDigitsIntegerProjection(
                    D("9223372036854775806.5"),
                    RoundingMode.HalfEven
                ),
            "truncate -9223372036854775808.9" =>
                new TruncateWithoutDigitsIntegerProjection(D("-9223372036854775808.9")),
            "integerDivide -9223372036854775808.5 by 1" =>
                new IntegerDivideIntegerProjection(D("-9223372036854775808.5"), I(1)),
            "floor -0.5" => new FloorIntegerProjection(D("-0.5")),
            "ceiling -0.5" => new CeilingIntegerProjection(D("-0.5")),
            "ceiling -0.9999999999999999999999999999" => new CeilingIntegerProjection(
                D("-0.9999999999999999999999999999")
            ),
            "round -0.5" => new RoundWithoutDigitsIntegerProjection(D("-0.5")),
            "round halfEven -0.5" => new RoundWithoutDigitsIntegerProjection(
                D("-0.5"),
                RoundingMode.HalfEven
            ),
            "round halfEven -1.5" => new RoundWithoutDigitsIntegerProjection(
                D("-1.5"),
                RoundingMode.HalfEven
            ),
            "round -0.4999999999999999999999999999" =>
                new RoundWithoutDigitsIntegerProjection(
                    D("-0.4999999999999999999999999999")
                ),
            "truncate -0.9999999999999999999999999999" =>
                new TruncateWithoutDigitsIntegerProjection(
                    D("-0.9999999999999999999999999999")
                ),
            "round -5 to tens" => new RoundWithDigitsIntegerProjection(I(-5), I(-1)),
            "round halfEven -5 to tens" => new RoundWithDigitsIntegerProjection(
                I(-5),
                I(-1),
                RoundingMode.HalfEven
            ),
            "round halfEven -15 to tens" => new RoundWithDigitsIntegerProjection(
                I(-15),
                I(-1),
                RoundingMode.HalfEven
            ),
            "round halfEven -25 to tens" => new RoundWithDigitsIntegerProjection(
                I(-25),
                I(-1),
                RoundingMode.HalfEven
            ),
            "round least to 10^18" => new RoundWithDigitsIntegerProjection(
                I(Min),
                I(-18)
            ),
            "round least to hundreds" => new RoundWithDigitsIntegerProjection(
                I(Min),
                I(-2)
            ),
            "round 4999999999999999999 to 10^19" => new RoundWithDigitsIntegerProjection(
                I(4999999999999999999),
                I(-19)
            ),
            "round halfEven 5000000000000000000 to 10^19" =>
                new RoundWithDigitsIntegerProjection(
                    I(5000000000000000000),
                    I(-19),
                    RoundingMode.HalfEven
                ),
            "round greatest to the least digits" => new RoundWithDigitsIntegerProjection(
                I(Max),
                I(Min)
            ),
            "round greatest to the greatest digits" =>
                new RoundWithDigitsIntegerProjection(I(Max), I(Max)),
            "truncate least to tens" => new TruncateWithDigitsIntegerProjection(
                I(Min),
                I(-1)
            ),
            "truncate -5 to tens" => new TruncateWithDigitsIntegerProjection(
                I(-5),
                I(-1)
            ),
            "truncate greatest to the least digits" =>
                new TruncateWithDigitsIntegerProjection(I(Max), I(Min)),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal(expected, Value(expression));
        Assert.Equal(IntegerType, ColumnType(expression));
    }

    // "Execution errors": a division by zero in divide, integerDivide or
    // modulo, of integers as of decimals, also in a later step of a list.
    [Theory]
    [InlineData("integerDivide 7 by 0")]
    [InlineData("modulo 7 by 0")]
    [InlineData("modulo least by 0")]
    [InlineData("divide 1 by 0")]
    [InlineData("divide 1, 2, 0")]
    [InlineData("divide 0 by 0")]
    [InlineData("modulo 7 by 0 as a decimal")]
    public void DivisionByZeroFails(string example)
    {
        ExprProjection expression = example switch
        {
            "integerDivide 7 by 0" => new IntegerDivideIntegerProjection(I(7), I(0)),
            "modulo 7 by 0" => new ModuloIntegerProjection(I(7), I(0)),
            "modulo least by 0" => new ModuloIntegerProjection(I(Min), I(0)),
            "divide 1 by 0" => new DivideDecimalProjection([I(1), I(0)]),
            "divide 1, 2, 0" => new DivideDecimalProjection([I(1), I(2), I(0)]),
            "divide 0 by 0" => new DivideDecimalProjection([D("0"), D("0")]),
            "modulo 7 by 0 as a decimal" => new ModuloDecimalProjection(D("7.5"), I(0)),
            _ => throw new ArgumentException(example),
        };

        Fails<DivideByZeroException>(expression);
    }

    // "Integer and decimal arithmetic": a decimal result that needs more
    // digits is rounded half away from zero to the most decimal places that
    // fit (|m| < 10^28, s <= 28), and every step of a list is such a result.
    // Each expected value is the exact value rounded so, by hand.
    [Theory]
    [InlineData("add 1 + 4e-28", "1")]
    [InlineData("add 1 + 5e-28", "1.000000000000000000000000001")]
    [InlineData("add -1 + -5e-28", "-1.000000000000000000000000001")]
    [InlineData("subtract 1 - 5e-28", "0.9999999999999999999999999995")]
    [InlineData("add list 1, 4e-28, 4e-28", "1")]
    [InlineData("add list 4e-28, 4e-28, 1", "1.000000000000000000000000001")]
    [InlineData("add largest + 0.4", Largest)]
    [InlineData("multiply 2.000000000000000000000000001 x 0.5", "1.000000000000000000000000001")]
    [InlineData("divide 2.000000000000000000000000001 by 2", "1.000000000000000000000000001")]
    [InlineData(
        "divide -2.000000000000000000000000001 by 2",
        "-1.000000000000000000000000001"
    )]
    [InlineData("divide -2 by 3", "-0.6666666666666666666666666667")]
    [InlineData("divide 1e-28 by 2", "0.0000000000000000000000000001")]
    [InlineData("divide -1e-28 by 2", "-0.0000000000000000000000000001")]
    [InlineData("divide 1 by largest", "0.0000000000000000000000000001")]
    [InlineData("divide largest by 7", "1428571428571428571428571428")]
    [InlineData("divide largest by 3", "3333333333333333333333333333")]
    [InlineData("multiply -5e-14 x 1e-15", "-0.0000000000000000000000000001")]
    [InlineData(
        "multiply 123456789012345678901234.5678 x 1.0001",
        "123469134691246913469124.6913"
    )]
    [InlineData(
        "multiply 0.1234567890123456789012345678 x 3",
        "0.3703703670370370367037037034"
    )]
    [InlineData(
        "multiply 99999999999999.99999999999999 x 99999999999999",
        "9999999999999899999999999999"
    )]
    [InlineData("multiply list 0.5, greatest, 2", "9223372036854775807")]
    [InlineData("subtract list least, 0.5, 1", "-9223372036854775809.5")]
    [InlineData("add greatest + 0.5", "9223372036854775807.5")]
    [InlineData("divide pi by 2", "1.570796326794896619231321692")]
    public void DecimalResultIsRoundedHalfAwayFromZeroToTheDigitsThatFit(
        string example,
        string expected
    )
    {
        ExprProjection expression = example switch
        {
            "add 1 + 4e-28" => new AddDecimalProjection([I(1), D(Tiny(4))]),
            "add 1 + 5e-28" => new AddDecimalProjection([I(1), D(Tiny(5))]),
            "add -1 + -5e-28" => new AddDecimalProjection([I(-1), D("-" + Tiny(5))]),
            "subtract 1 - 5e-28" => new SubtractDecimalProjection([I(1), D(Tiny(5))]),
            "add list 1, 4e-28, 4e-28" => new AddDecimalProjection([
                I(1),
                D(Tiny(4)),
                D(Tiny(4)),
            ]),
            "add list 4e-28, 4e-28, 1" => new AddDecimalProjection([
                D(Tiny(4)),
                D(Tiny(4)),
                I(1),
            ]),
            "add largest + 0.4" => new AddDecimalProjection([D(Largest), D("0.4")]),
            "multiply 2.000000000000000000000000001 x 0.5" =>
                new MultiplyDecimalProjection([
                    D("2.000000000000000000000000001"),
                    D("0.5"),
                ]),
            "divide 2.000000000000000000000000001 by 2" => new DivideDecimalProjection([
                D("2.000000000000000000000000001"),
                I(2),
            ]),
            "divide -2.000000000000000000000000001 by 2" => new DivideDecimalProjection([
                D("-2.000000000000000000000000001"),
                I(2),
            ]),
            "divide -2 by 3" => new DivideDecimalProjection([I(-2), I(3)]),
            "divide 1e-28 by 2" => new DivideDecimalProjection([D(Tiny(1)), I(2)]),
            "divide -1e-28 by 2" => new DivideDecimalProjection([D("-" + Tiny(1)), I(2)]),
            "divide 1 by largest" => new DivideDecimalProjection([I(1), D(Largest)]),
            "divide largest by 7" => new DivideDecimalProjection([D(Largest), I(7)]),
            "divide largest by 3" => new DivideDecimalProjection([D(Largest), I(3)]),
            "multiply -5e-14 x 1e-15" => new MultiplyDecimalProjection([
                D("-0.00000000000005"),
                D("0.000000000000001"),
            ]),
            "multiply 123456789012345678901234.5678 x 1.0001" =>
                new MultiplyDecimalProjection([
                    D("123456789012345678901234.5678"),
                    D("1.0001"),
                ]),
            "multiply 0.1234567890123456789012345678 x 3" =>
                new MultiplyDecimalProjection([
                    D("0.1234567890123456789012345678"),
                    I(3),
                ]),
            "multiply 99999999999999.99999999999999 x 99999999999999" =>
                new MultiplyDecimalProjection([
                    D("99999999999999.99999999999999"),
                    I(99999999999999),
                ]),
            "multiply list 0.5, greatest, 2" => new MultiplyDecimalProjection([
                D("0.5"),
                I(Max),
                I(2),
            ]),
            "subtract list least, 0.5, 1" => new SubtractDecimalProjection([
                I(Min),
                D("0.5"),
                I(1),
            ]),
            "add greatest + 0.5" => new AddDecimalProjection([I(Max), D("0.5")]),
            "divide pi by 2" => new DivideDecimalProjection([new PiProjection(), I(2)]),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal(expected, Value(expression));
        Assert.Equal("decimal", ColumnType(expression));
    }

    // "Execution errors": a decimal of magnitude 10^28 or more, also when the
    // exact value is below it but rounds to it.
    [Theory]
    [InlineData("add largest + 0.5")]
    [InlineData("subtract -largest - 0.5")]
    [InlineData("add largest + 1")]
    [InlineData("multiply 5000000000000000000000000000 x 2")]
    [InlineData("multiply largest x largest")]
    [InlineData("divide largest by 0.5")]
    [InlineData("divide 1 by 1e-28")]
    [InlineData("round largest to tens")]
    [InlineData("round 5000000000000000000000000000 to 10^28")]
    [InlineData("abs of a sum beyond the range")]
    public void DecimalResultOfMagnitudeTenToTheTwentyEightFails(string example)
    {
        ExprProjection expression = example switch
        {
            "add largest + 0.5" => new AddDecimalProjection([D(Largest), D("0.5")]),
            "subtract -largest - 0.5" => new SubtractDecimalProjection([
                D("-" + Largest),
                D("0.5"),
            ]),
            "add largest + 1" => new AddDecimalProjection([D(Largest), I(1)]),
            "multiply 5000000000000000000000000000 x 2" => new MultiplyDecimalProjection([
                D("5000000000000000000000000000"),
                I(2),
            ]),
            "multiply largest x largest" => new MultiplyDecimalProjection([
                D(Largest),
                D(Largest),
            ]),
            "divide largest by 0.5" => new DivideDecimalProjection([
                D(Largest),
                D("0.5"),
            ]),
            "divide 1 by 1e-28" => new DivideDecimalProjection([I(1), D(Tiny(1))]),
            "round largest to tens" => new RoundDecimalProjection(D(Largest), I(-1)),
            "round 5000000000000000000000000000 to 10^28" => new RoundDecimalProjection(
                D("5000000000000000000000000000"),
                I(-28)
            ),
            "abs of a sum beyond the range" => new AbsDecimalProjection(
                new SubtractDecimalProjection([D("-" + Largest), I(1)])
            ),
            _ => throw new ArgumentException(example),
        };

        Fails<OverflowException>(expression);
    }

    // "Integer and decimal arithmetic" and "Conversion results": round with
    // digits keeps a decimal a decimal whatever the digits, and a decimal is
    // written by its value, without -0.
    [Theory]
    [InlineData("round -0.05 to 1 place", "-0.1")]
    [InlineData("round halfEven -0.15 to 1 place", "-0.2")]
    [InlineData("round 4999999999999999999999999999 to 10^28", "0")]
    [InlineData("round halfEven 5000000000000000000000000000 to 10^28", "0")]
    [InlineData("round 5000000000000000000000000000 to 10^29", "0")]
    [InlineData("round largest to the least digits", "0")]
    [InlineData("round 1234.5678 to the greatest digits", "1234.5678")]
    [InlineData("truncate largest to 10^27", "9000000000000000000000000000")]
    [InlineData("truncate -largest to 10^27", "-9000000000000000000000000000")]
    [InlineData("truncate largest to 10^28", "0")]
    [InlineData("truncate largest to the least digits", "0")]
    [InlineData("truncate 1234.5678 to the greatest digits", "1234.5678")]
    public void RoundAndTruncateOfADecimalWithDigitsReachEveryPlace(
        string example,
        string expected
    )
    {
        ExprProjection expression = example switch
        {
            "round -0.05 to 1 place" => new RoundDecimalProjection(D("-0.05"), I(1)),
            "round halfEven -0.15 to 1 place" => new RoundDecimalProjection(
                D("-0.15"),
                I(1),
                RoundingMode.HalfEven
            ),
            "round 4999999999999999999999999999 to 10^28" => new RoundDecimalProjection(
                D("4999999999999999999999999999"),
                I(-28)
            ),
            "round halfEven 5000000000000000000000000000 to 10^28" =>
                new RoundDecimalProjection(
                    D("5000000000000000000000000000"),
                    I(-28),
                    RoundingMode.HalfEven
                ),
            "round 5000000000000000000000000000 to 10^29" => new RoundDecimalProjection(
                D("5000000000000000000000000000"),
                I(-29)
            ),
            "round largest to the least digits" => new RoundDecimalProjection(
                D(Largest),
                I(Min)
            ),
            "round 1234.5678 to the greatest digits" => new RoundDecimalProjection(
                D("1234.5678"),
                I(Max)
            ),
            "truncate largest to 10^27" => new TruncateDecimalProjection(
                D(Largest),
                I(-27)
            ),
            "truncate -largest to 10^27" => new TruncateDecimalProjection(
                D("-" + Largest),
                I(-27)
            ),
            "truncate largest to 10^28" => new TruncateDecimalProjection(
                D(Largest),
                I(-28)
            ),
            "truncate largest to the least digits" => new TruncateDecimalProjection(
                D(Largest),
                I(Min)
            ),
            "truncate 1234.5678 to the greatest digits" => new TruncateDecimalProjection(
                D("1234.5678"),
                I(Max)
            ),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal(expected, Value(expression));
        Assert.Equal("decimal", ColumnType(expression));
    }

    // "Types" and "Conversion results": a decimal has no -0, so a zero is
    // written 0 however it was computed, also from negative operands.
    [Theory]
    [InlineData("multiply -0.5 x 0")]
    [InlineData("multiply 0 x -0.5")]
    [InlineData("multiply -1e-14 x 1e-15")]
    [InlineData("subtract 0.5 - 0.5")]
    [InlineData("add -1.5 + 1.5")]
    [InlineData("divide 0 by -5")]
    [InlineData("divide -1e-28 by 3")]
    [InlineData("modulo -4.5 by 1.5")]
    [InlineData("round -4 to tens")]
    [InlineData("truncate -0.09 to 1 place")]
    [InlineData("abs -0 product")]
    public void ADecimalZeroHasNoSign(string example)
    {
        ExprProjection expression = example switch
        {
            "multiply -0.5 x 0" => new MultiplyDecimalProjection([D("-0.5"), D("0")]),
            "multiply 0 x -0.5" => new MultiplyDecimalProjection([D("0"), D("-0.5")]),
            "multiply -1e-14 x 1e-15" => new MultiplyDecimalProjection([
                D("-0.00000000000001"),
                D("0.000000000000001"),
            ]),
            "subtract 0.5 - 0.5" => new SubtractDecimalProjection([D("0.5"), D("0.5")]),
            "add -1.5 + 1.5" => new AddDecimalProjection([D("-1.5"), D("1.5")]),
            "divide 0 by -5" => new DivideDecimalProjection([D("0"), I(-5)]),
            "divide -1e-28 by 3" => new DivideDecimalProjection([D("-" + Tiny(1)), I(3)]),
            "modulo -4.5 by 1.5" => new ModuloDecimalProjection(D("-4.5"), D("1.5")),
            "round -4 to tens" => new RoundDecimalProjection(D("-4"), I(-1)),
            "truncate -0.09 to 1 place" => new TruncateDecimalProjection(
                D("-0.09"),
                I(1)
            ),
            "abs -0 product" => new AbsDecimalProjection(
                new MultiplyDecimalProjection([D("-0.5"), D("0")])
            ),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal("0", Value(expression));
    }

    // round of a negative decimal to a zero with digits that are not
    // negative: Math.Round keeps the sign of its operand, the specification
    // has no -0.
    [Theory]
    [InlineData("-0.4", 0, false)]
    [InlineData("-0.04", 1, false)]
    [InlineData("-0.05", 1, true)]
    [InlineData("-0.0000000000000000000000000004", 27, false)]
    public void ADecimalRoundedToZeroHasNoSign(string operand, long digits, bool even)
    {
        RoundDecimalProjection rounded = even
            ? new RoundDecimalProjection(D(operand), I(digits), RoundingMode.HalfEven)
            : new RoundDecimalProjection(D(operand), I(digits));

        Assert.Equal("0", Value(rounded));
        Assert.Equal("0", Value(new AsStringDecimalStringProjection(rounded)));
    }

    // "Conversion results": a decimal is written by its value, not by how it
    // was computed: no exponent, no trailing zeros in the fraction, and no
    // point for a whole number.
    [Theory]
    [InlineData("multiply 2.5 x 4", "10")]
    [InlineData("add 0.75 + 0.25", "1")]
    [InlineData("add 1.25 + 1.25", "2.5")]
    [InlineData("multiply 0.5 x 0.2", "0.1")]
    [InlineData("divide 10 by 4", "2.5")]
    [InlineData("divide 1 by 10^27", "0.000000000000000000000000001")]
    [InlineData("multiply 10^15 x 1000000000000.5", "1000000000000500000000000000")]
    [InlineData("divide 1000 by 0.001", "1000000")]
    [InlineData("the smallest decimal", "0.0000000000000000000000000001")]
    [InlineData("the largest decimal", Largest)]
    [InlineData("divide 6 by 3", "2")]
    public void ADecimalResultIsWrittenInItsOneSpelling(string example, string expected)
    {
        DecimalProjection expression = example switch
        {
            "multiply 2.5 x 4" => new MultiplyDecimalProjection([D("2.5"), I(4)]),
            "add 0.75 + 0.25" => new AddDecimalProjection([D("0.75"), D("0.25")]),
            "add 1.25 + 1.25" => new AddDecimalProjection([D("1.25"), D("1.25")]),
            "multiply 0.5 x 0.2" => new MultiplyDecimalProjection([D("0.5"), D("0.2")]),
            "divide 10 by 4" => new DivideDecimalProjection([I(10), I(4)]),
            "divide 1 by 10^27" => new DivideDecimalProjection([
                I(1),
                D("1000000000000000000000000000"),
            ]),
            "multiply 10^15 x 1000000000000.5" => new MultiplyDecimalProjection([
                I(1000000000000000),
                D("1000000000000.5"),
            ]),
            "divide 1000 by 0.001" => new DivideDecimalProjection([I(1000), D("0.001")]),
            "the smallest decimal" => D(Tiny(1)),
            "the largest decimal" => D(Largest),
            "divide 6 by 3" => new DivideDecimalProjection([I(6), I(3)]),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal(expected, Value(Unwrapped(expression)));
        Assert.Equal(expected, Value(new AsStringDecimalStringProjection(expression)));
    }

    // "Conditional and null handling" and "Evaluation": greatest and least
    // compare numbers by value, a decimal operand makes the result a decimal
    // whichever operand it comes from, and they are null when an operand is;
    // nullIfEqual compares as equal does and keeps the type of left.
    [Theory]
    [InlineData("greatest greatest, 9223372036854775807.5", "9223372036854775807.5")]
    [InlineData("least least, -9223372036854775808.5", "-9223372036854775808.5")]
    [InlineData("least 2, 2.5, 3", "2")]
    [InlineData("greatest -1e-28, 0", "0")]
    [InlineData("least -1e-28, 0", "-0.0000000000000000000000000001")]
    [InlineData("greatest of a sum that rounds", "1")]
    [InlineData("greatest 1, null", "")]
    [InlineData("least null, 2.5", "")]
    [InlineData("nullIfEqual 1e-28, 0", "0.0000000000000000000000000001")]
    [InlineData("nullIfEqual greatest, 9223372036854775807.5", "9223372036854775807")]
    [InlineData("nullIfEqual greatest, greatest", "")]
    [InlineData("nullIfEqual null, 0", "")]
    [InlineData("nullIfEqual 5, null", "5")]
    [InlineData("nullIfEqual 1 + 4e-28, 1", "")]
    public void GreatestLeastAndNullIfEqualCompareNumbersByValue(
        string example,
        string expected
    )
    {
        ExprProjection expression = example switch
        {
            "greatest greatest, 9223372036854775807.5" => new GreatestDecimalProjection([
                I(Max),
                D("9223372036854775807.5"),
            ]),
            "least least, -9223372036854775808.5" => new LeastDecimalProjection([
                I(Min),
                D("-9223372036854775808.5"),
            ]),
            "least 2, 2.5, 3" => new LeastDecimalProjection([I(2), D("2.5"), I(3)]),
            "greatest -1e-28, 0" => new GreatestDecimalProjection([
                D("-" + Tiny(1)),
                I(0),
            ]),
            "least -1e-28, 0" => new LeastDecimalProjection([D("-" + Tiny(1)), I(0)]),
            "greatest of a sum that rounds" => new GreatestDecimalProjection([
                new AddDecimalProjection([I(1), D(Tiny(4))]),
                I(1),
            ]),
            "greatest 1, null" => new GreatestIntegerNullableProjection([
                I(1),
                new LiteralIntegerNullable(),
            ]),
            "least null, 2.5" => new LeastDecimalNullableProjection([
                new LiteralIntegerNullable(),
                D("2.5"),
            ]),
            "nullIfEqual 1e-28, 0" => new NullIfEqualDecimalNullableProjection(
                D(Tiny(1)),
                I(0)
            ),
            "nullIfEqual greatest, 9223372036854775807.5" =>
                new NullIfEqualIntegerNullableProjection(
                    I(Max),
                    D("9223372036854775807.5")
                ),
            "nullIfEqual greatest, greatest" => new NullIfEqualIntegerNullableProjection(
                I(Max),
                I(Max)
            ),
            "nullIfEqual null, 0" => new NullIfEqualIntegerNullableProjection(
                new LiteralIntegerNullable(),
                I(0)
            ),
            "nullIfEqual 5, null" => new NullIfEqualIntegerNullableProjection(
                I(5),
                new LiteralDecimalNullable()
            ),
            "nullIfEqual 1 + 4e-28, 1" => new NullIfEqualDecimalNullableProjection(
                new AddDecimalProjection([I(1), D(Tiny(4))]),
                I(1)
            ),
            _ => throw new ArgumentException(example),
        };

        Assert.Equal(expected, Value(expression));
    }

    // "Null semantics": a lifted operator is null when any operand is null,
    // nullIfEqual is null when its operands are equal and if takes the value
    // of the branch it takes; the operators nest, each rounding its own
    // result. The expression is
    // add(multiply(x, nullIfEqual(n, 0)), integerDivide(n, nullIfEqual(d, 0)),
    //     if(equal(n, 1), null, 0.5)), over stored rows and over literals.
    [Fact]
    public void NestedLiftedOperatorsAreNullWhereAnyOperandIsNull()
    {
        IStoredSchemaDataSet rows = new InlineTable(
            "t",
            "rows",
            ("id", new LongColumnType()),
            ("x", new DoubleColumnType()),
            ("n", new LongColumnType()),
            ("d", new LongColumnType())
        ).With(
            ["1", "2.5", "3", "2"],
            ["2", "2.5", "0", "2"],
            ["3", "2.5", "1", "2"],
            ["4", "", "3", "2"],
            ["5", "-1.5", "-3", "0"],
            ["6", "-1.5", "-3", "-2"],
            ["7", "0.0000000000000000000000000001", "4", "3"]
        );
        PureQLProjection projection = new(
            [rows],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                select:
                [
                    new SelectItemProjection(
                        "value",
                        Nested(
                            new FieldDecimalNullable(
                                "t.rows",
                                "x"
                            ),
                            new FieldIntegerNullable(
                                "t.rows",
                                "n"
                            ),
                            new FieldIntegerNullable(
                                "t.rows",
                                "n"
                            ),
                            new FieldInteger("t.rows", "d")
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldInteger("t.rows", "id")
                    ),
                ]
            )
        );

        Assert.Equal(
            ["9", "", "", "", "", "6", "1.5"],
            ResultColumn.Texts(projection, "value")
        );
        Assert.Equal("9", Value(Nested(D("2.5"), I(3), I(3), I(2))));
        Assert.Equal("", Value(Nested(D("2.5"), I(0), I(0), I(2))));
        Assert.Equal("", Value(Nested(D("2.5"), I(1), I(1), I(2))));
        Assert.Equal("", Value(Nested(D("-1.5"), I(-3), I(-3), I(0))));
        Assert.Equal("6", Value(Nested(D("-1.5"), I(-3), I(-3), I(-2))));
        Assert.Equal("1.5", Value(Nested(D(Tiny(1)), I(4), I(4), I(3))));
    }

    private static AddDecimalNullableProjection Nested(
        DecimalNullableProjection x,
        IntegerNullableProjection n,
        DecimalNullableProjection again,
        IntegerNullableProjection d
    )
    {
        return new AddDecimalNullableProjection([
            new MultiplyDecimalNullableProjection([
                x,
                new NullIfEqualIntegerNullableProjection(n, I(0)),
            ]),
            new IntegerDivideIntegerNullableProjection(
                again,
                new NullIfEqualIntegerNullableProjection(d, I(0))
            ),
            new IfDecimalNullableProjection(
                new LiteralDecimalNullable(),
                D("0.5"),
                new EqualDecimalProjection(again, I(1))
            ),
        ]);
    }

    // "divide computes the exact quotient, rounded half away from zero to the
    // decimal precision", and round rounds that decimal: 2.5 /
    // 0.9999999999999999999999999999 is 2.50000000000000000000000000025…,
    // which divide rounds to 2.5, so round(…, halfEven) is 2; and
    // 2.499999999999999999999999999 / 0.9999999999999999999999999997 is
    // 2.49999999999999999999999999975…, which divide rounds to 2.5, so
    // round(…) is 3. Here over nullable stored columns.
    [Theory]
    [InlineData(OneLessHalf, true, "none", "2")]
    [InlineData(BelowHalf, false, "none", "3")]
    public void RoundOfANullableQuotientRoundsTheRoundedQuotient(
        string operands,
        bool even,
        string guard,
        string expected
    )
    {
        Assert.Equal([expected], RoundedQuotient(operands, even, guard));
    }

    // The same quotient with its divisor guarded as "Conditional and null
    // handling" advises, divide(x, nullIfEqual(y, 0)), or with if: the guard
    // changes nothing where the divisor is not 0.
    [Theory(
        Skip = "Bug: round(divide(2.5, nullIfEqual(0.9999999999999999999999999999, "
            + "0)), halfEven) is 2 by the spec (divide rounds to 2.5 first); over a "
            + "guarded nullable divisor the library rounds the exact quotient once: 3 "
            + "(and 2 instead of 3 half away from zero)"
    )]
    [InlineData(OneLessHalf, true, "nullIfEqual", "2")]
    [InlineData(BelowHalf, false, "nullIfEqual", "3")]
    [InlineData(OneLessHalf, true, "if", "2")]
    [InlineData(BelowHalf, false, "if", "3")]
    public void RoundOfAGuardedQuotientRoundsTheRoundedQuotient(
        string operands,
        bool even,
        string guard,
        string expected
    )
    {
        Assert.Equal([expected], RoundedQuotient(operands, even, guard));
    }

    // The same rule over aggregates: round of the quotient of two sums, in
    // a grouped and in a total query, each group a single row.
    [Theory]
    [InlineData(OneLessHalf, true, "2")]
    [InlineData(BelowHalf, false, "3")]
    public void RoundOfAQuotientOfAggregatesRoundsTheRoundedQuotient(
        string operands,
        bool even,
        string expected
    )
    {
        string[] texts = operands.Split(' ');
        IStoredSchemaDataSet rows = new InlineTable(
            "t",
            "rows",
            ("g", new StringColumnType()),
            ("x", new DoubleColumnType()),
            ("y", new DoubleColumnType())
        ).With(["a", texts[0], texts[1]]);
        FieldDecimal x = new("t.rows", "x");
        FieldDecimal y = new("t.rows", "y");
        DivideDecimalGroup group = new([new SumDecimalGroup(x), new SumDecimalGroup(y)]);
        DivideDecimalTotal total = new([new SumDecimalTotal(x), new SumDecimalTotal(y)]);
        PureQLProjection grouped = new(
            [rows],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                groupBy: [new GroupKey("g", new FieldString("t.rows", "g"))],
                select:
                [
                    new SelectItemGroup(
                        "value",
                        even
                            ? new RoundWithoutDigitsIntegerGroup(group, RoundingMode.HalfEven)
                            : new RoundWithoutDigitsIntegerGroup(group)
                    ),
                ]
            )
        );
        PureQLProjection totals = new(
            [rows],
            new MainTotalQuery(
                from: new FromEntity("t.rows"),
                aggregate:
                [
                    new SelectItemTotal(
                        "value",
                        even
                            ? new RoundWithoutDigitsIntegerTotal(total, RoundingMode.HalfEven)
                            : new RoundWithoutDigitsIntegerTotal(total)
                    ),
                ]
            )
        );

        Assert.Equal([expected], ResultColumn.Texts(grouped, "value"));
        Assert.Equal([expected], ResultColumn.Texts(totals, "value"));
    }

    // Operators over a guarded nullable operand keep their range checks,
    // their rounding and the remainder of the least integer by -1.
    [Theory]
    [InlineData("modulo", "-9223372036854775808", "-1", "0")]
    [InlineData("integerDivide", "-9223372036854775808", "-1", "overflow")]
    [InlineData("integerDivide", "-7", "2", "-3")]
    [InlineData("add integers", "9223372036854775807", "1", "overflow")]
    [InlineData("multiply integers", "-9223372036854775808", "-1", "overflow")]
    [InlineData("add decimals", Largest, "0.5", "overflow")]
    [InlineData("add decimals", "1", "0.0000000000000000000000000004", "1")]
    [InlineData(
        "multiply decimals",
        "2.000000000000000000000000001",
        "0.5",
        "1.000000000000000000000000001"
    )]
    [InlineData("divide", "-2", "3", "-0.6666666666666666666666666667")]
    [InlineData("divide", "1", "0", "")]
    public void AnOperatorOverAGuardedOperandKeepsItsArithmetic(
        string operation,
        string left,
        string right,
        string expected
    )
    {
        bool integers = !operation.Contains("decimals") && operation != "divide";
        IStoredSchemaDataSet rows = new InlineTable(
            "t",
            "rows",
            ("l", integers ? new LongColumnType() : new DoubleColumnType()),
            ("r", integers ? new LongColumnType() : new DoubleColumnType())
        ).With([left, right]);
        FieldIntegerNullable n = new("t.rows", "l");
        FieldIntegerNullable m = new("t.rows", "r");
        FieldDecimalNullable x = new("t.rows", "l");
        FieldDecimalNullable y = new("t.rows", "r");
        NullIfEqualIntegerNullableProjection guardedInteger = new(m, I(0));
        NullIfEqualDecimalNullableProjection guardedDecimal = new(y, I(0));
        ExprProjection expression = operation switch
        {
            "modulo" => new ModuloIntegerNullableProjection(n, guardedInteger),
            "integerDivide" => new IntegerDivideIntegerNullableProjection(
                n,
                guardedInteger
            ),
            "add integers" => new AddIntegerNullableProjection([n, guardedInteger]),
            "multiply integers" => new MultiplyIntegerNullableProjection([
                n,
                guardedInteger,
            ]),
            "add decimals" => new AddDecimalNullableProjection([x, guardedDecimal]),
            "multiply decimals" => new MultiplyDecimalNullableProjection([
                x,
                guardedDecimal,
            ]),
            _ => new DivideDecimalNullableProjection([x, guardedDecimal]),
        };
        PureQLProjection projection = new(
            [rows],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                select: [new SelectItemProjection("value", expression)]
            )
        );

        if (expected == "overflow")
        {
            _ = Assert.Throws<OverflowException>(() => projection.ToList());
        }
        else
        {
            Assert.Equal([expected], ResultColumn.Texts(projection, "value"));
        }
    }

    // Dividend and divisor of the quotients above, separated by a space.
    private const string OneLessHalf = "2.5 0.9999999999999999999999999999";

    private const string BelowHalf =
        "2.499999999999999999999999999 0.9999999999999999999999999997";

    private static IReadOnlyList<string> RoundedQuotient(
        string operands,
        bool even,
        string guard
    )
    {
        string[] texts = operands.Split(' ');
        IStoredSchemaDataSet rows = new InlineTable(
            "t",
            "rows",
            ("x", new DoubleColumnType()),
            ("y", new DoubleColumnType())
        ).With(texts);
        FieldDecimalNullable x = new("t.rows", "x");
        FieldDecimalNullable y = new("t.rows", "y");
        DivideDecimalNullableProjection quotient = guard switch
        {
            "nullIfEqual" => new([x, new NullIfEqualDecimalNullableProjection(y, I(0))]),
            "if" => new([
                x,
                new IfDecimalNullableProjection(
                    new LiteralDecimalNullable(),
                    y,
                    new EqualDecimalProjection(y, I(0))
                ),
            ]),
            _ => new([x, y]),
        };
        PureQLProjection projection = new(
            [rows],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                select:
                [
                    new SelectItemProjection(
                        "value",
                        even
                            ? new RoundWithoutDigitsIntegerNullableProjection(
                                quotient,
                                RoundingMode.HalfEven
                            )
                            : new RoundWithoutDigitsIntegerNullableProjection(quotient)
                    ),
                ]
            )
        );

        return ResultColumn.Texts(projection, "value");
    }

    // "Parameters": a value is written exactly like a literal of the declared
    // type, in the same one spelling, a decimal as a string within its range;
    // "Conversion results": no surrounding spaces, no +, no leading zeros, no
    // trailing zeros in a fraction, no exponent ("This is the rule a
    // parameter value follows"); "Execution errors": a parameter value not
    // written like a literal of its type fails the query.
    [Theory(
        Skip = "Bug: the spec rejects a decimal parameter not in the one literal "
            + "spelling; the library parses it with NumberStyles.Float and binds it"
    )]
    [InlineData("1.50")]
    [InlineData("+1.5")]
    [InlineData("1e3")]
    [InlineData("01.5")]
    [InlineData(" 1.5")]
    [InlineData("1.5 ")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("-0")]
    public void ADecimalParameterNotWrittenAsALiteralFailsTheQuery(string text)
    {
        _ = Assert.ThrowsAny<Exception>(() => Parameter("decimal", text).ToList());
    }

    // "Integer and decimal arithmetic": a decimal has at most 28 decimal
    // places, and a parameter outside that range is an execution error.
    [Fact(
        Skip = "Bug: the spec makes a decimal parameter with 29 decimal places an "
            + "execution error; the library rounds it silently and binds it"
    )]
    public void ADecimalParameterWithTwentyNineDecimalPlacesFailsTheQuery()
    {
        _ = Assert.ThrowsAny<Exception>(() =>
            Parameter("decimal", "0.00000000000000000000000000001").ToList()
        );
    }

    [Fact]
    public void ADecimalParameterOfMagnitudeTenToTheTwentyEightFailsTheQuery()
    {
        _ = Assert.ThrowsAny<Exception>(() =>
            Parameter("decimal", "10000000000000000000000000000").ToList()
        );
    }

    // "Parameters": an integer is a JSON number, which has no + and no
    // leading zeros.
    [Theory(
        Skip = "Bug: the spec rejects an integer parameter written with + or leading "
            + "zeros; the library parses it with NumberStyles.Integer and binds it"
    )]
    [InlineData("+5")]
    [InlineData("007")]
    [InlineData("-05")]
    public void AnIntegerParameterNotWrittenAsALiteralFailsTheQuery(string text)
    {
        _ = Assert.ThrowsAny<Exception>(() => Parameter("integer", text).ToList());
    }

    // The values at the ends of each range are literals and are read exactly.
    [Theory]
    [InlineData("decimal", Largest)]
    [InlineData("decimal", "-" + Largest)]
    [InlineData("decimal", "0.0000000000000000000000000001")]
    [InlineData("decimal", "-0.0000000000000000000000000001")]
    [InlineData("decimal", "0.9999999999999999999999999999")]
    [InlineData("integer", "-9223372036854775808")]
    [InlineData("integer", "9223372036854775807")]
    public void ANumberParameterAtTheEndOfItsRangeIsReadExactly(string type, string text)
    {
        Assert.Equal([text], ResultColumn.Texts(Parameter(type, text), "value"));
    }

    private static PureQLProjection Parameter(string type, string text)
    {
        ExprProjection read = type == "integer"
            ? new ParamInteger("p")
            : new ParamDecimal("p");

        return new PureQLProjection(
            [One],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.one"),
                select: [new SelectItemProjection("value", read)]
            ),
            new Dictionary<string, IParameter> { ["p"] = new TextParameter(type, text) }
        );
    }

    // 0.000…0n with n in the 28th decimal place.
    private static string Tiny(int digit)
    {
        return "0." + new string('0', 27) + digit.ToString(CultureInfo.InvariantCulture);
    }

    private static LiteralInteger I(long value)
    {
        return new LiteralInteger(value);
    }

    private static LiteralDecimal D(string text)
    {
        return new LiteralDecimal(decimal.Parse(text, CultureInfo.InvariantCulture));
    }

    // The record a decimal expression holds, as a select expression.
    private static ExprProjection Unwrapped(DecimalProjection expression)
    {
        return ((IUnion)expression).Value switch
        {
            LiteralDecimal literal => literal,
            AddDecimalProjection sum => sum,
            MultiplyDecimalProjection product => product,
            DivideDecimalProjection quotient => quotient,
            _ => throw new ArgumentException(expression.ToString()),
        };
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

    private static string ColumnType(ExprProjection expression)
    {
        return Assert
            .Single(Selecting(expression).TableSchema.Columns)
            .Type.Name.TextValue;
    }

    // The projection is built, so the query is valid; only reading it fails.
    private static void Fails<TException>(ExprProjection expression)
        where TException : Exception
    {
        PureQLProjection projection = Selecting(expression);

        _ = Assert.Throws<TException>(() => projection.ToList());
    }
}
