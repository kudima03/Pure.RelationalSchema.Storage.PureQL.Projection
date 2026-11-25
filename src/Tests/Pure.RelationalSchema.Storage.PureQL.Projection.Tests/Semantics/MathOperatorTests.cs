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

// The numeric operators of the specification run through queries, as
// "Integer and decimal arithmetic" defines them: powers, roots, logarithms
// and trigonometry are decimal, exact and rounded once (pi is pi rounded, so
// sin(pi) is not 0), and a value outside a function's domain is an execution
// error; round, truncate, floor and ceiling, with and without digits and the
// halfEven mode, where an integer rounded with digits stays an integer and
// one beyond 64 bits fails; integerDivide and modulo of decimals, truncated
// exactly; abs, sign, greatest and least, typed as add and lifted;
// nullIfEqual as the guard of a division by zero; the left fold of add,
// subtract, multiply and divide over a list, one rounded and range-checked
// step at a time. Each example of the specification is a test.
public sealed class MathOperatorTests
{
    // Row 1 holds every value, row 2 no x, row 3 no n; d is a divisor that is
    // zero in row 2.
    private static readonly IStoredSchemaDataSet Numbers = new InlineTable(
        "t",
        "numbers",
        ("id", new LongColumnType()),
        ("x", new DoubleColumnType()),
        ("n", new LongColumnType()),
        ("d", new LongColumnType()),
        ("g", new StringColumnType()),
        ("q", new LongColumnType())
    ).With(
        ["1", "2", "1", "4", "a", "1000"],
        ["2", "", "2", "0", "a", "249"],
        ["3", "6.25", "", "-4", "b", "1250"]
    );

    private static readonly FieldInteger Id = new("t.numbers", "id");

    private static readonly FieldDecimalNullable X = new("t.numbers", "x");

    private static readonly FieldIntegerNullable N = new("t.numbers", "n");

    private static readonly FieldInteger D = new("t.numbers", "d");

    private static readonly FieldInteger Q = new("t.numbers", "q");

    private static readonly string IntegerType = new LongColumnType().Name.TextValue;

    [Theory]
    [InlineData("1.5", "2", "2.25")]
    [InlineData("2", "-2", "0.25")]
    [InlineData("-2", "3", "-8")]
    [InlineData("0", "0", "1")]
    [InlineData("2", "0.5", "1.414213562373095048801688724")]
    [InlineData("10", "27.9", "7943282347242815020659182828")]
    public void PowerIsTheExactValueRoundedOnce(
        string operand,
        string exponent,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new PowerDecimalProjection(Decimal(operand), Decimal(exponent)))
        );
    }

    [Fact]
    public void PowerOfIntegersIsADecimalThatRoundMakesAnInteger()
    {
        PowerDecimalProjection power = new(
            new LiteralInteger(2),
            new LiteralInteger(10)
        );

        Assert.Equal("decimal", ColumnType(power));
        Assert.Equal("1024", Value(new RoundWithoutDigitsIntegerProjection(power)));
        Assert.Equal(
            IntegerType,
            ColumnType(new RoundWithoutDigitsIntegerProjection(power))
        );
    }

    [Theory]
    [InlineData("sqrt", "2", "1.414213562373095048801688724")]
    [InlineData("exp", "1", "2.718281828459045235360287471")]
    [InlineData("exp", "64", "6235149080811616882909238709")]
    [InlineData("exp", "-100", "0")]
    [InlineData("ln", "2", "0.6931471805599453094172321215")]
    [InlineData("log10", "1000", "3")]
    public void RootsAndLogarithmsAreExactAndRoundedOnce(
        string function,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, Value(Unary(function, Decimal(operand))));
    }

    [Theory]
    [InlineData("8", "2", "3")]
    [InlineData("2", "8", "0.3333333333333333333333333333")]
    [InlineData("8", "6.25", "1.134706196049045044148158041")]
    public void LogIsTheLogarithmOfTheOperandToTheBase(
        string operand,
        string @base,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(new LogDecimalProjection(Decimal(operand), Decimal(@base)))
        );
    }

    [Theory]
    [InlineData("sin", "1", "0.8414709848078965066525023216")]
    [InlineData("tan", "1", "1.557407724654902230506974807")]
    [InlineData("asin", "0.5", "0.5235987755982988730771072305")]
    [InlineData("acos", "0", "1.570796326794896619231321692")]
    [InlineData("sin", "1000000000000000000000000000", "0.7180634961391176660795156521")]
    [InlineData("cos", "1000000000000000000000000000", "-0.6959775969903538128251834949")]
    public void TrigonometryIsInRadiansExactAndRoundedOnce(
        string function,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, Value(Unary(function, Decimal(operand))));
    }

    [Theory]
    [InlineData("1", "-1", "2.356194490192344928846982537")]
    [InlineData("-1", "-1", "-2.356194490192344928846982537")]
    [InlineData("0", "-1", "3.141592653589793238462643383")]
    public void Atan2IsTheAngleOfThePointInItsQuadrant(
        string y,
        string x,
        string expected
    )
    {
        Assert.Equal(expected, Value(new Atan2DecimalProjection(Decimal(y), Decimal(x))));
    }

    [Fact]
    public void PiIsPiRoundedSoItsSineIsNotZero()
    {
        Assert.Equal("3.141592653589793238462643383", Value(new PiProjection()));
        Assert.Equal(
            "0.0000000000000000000000000003",
            Value(new SinDecimalProjection(new PiProjection()))
        );
        Assert.Equal("-1", Value(new CosDecimalProjection(new PiProjection())));
        Assert.Equal(
            "-0.0000000000000000000000000003",
            Value(new TanDecimalProjection(new PiProjection()))
        );
    }

    [Fact]
    public void DegreesBecomeRadiansThroughTwoRoundedSteps()
    {
        Assert.Equal(
            "3.141592653589793238462643386",
            Value(
                new MultiplyDecimalProjection([
                    new LiteralInteger(180),
                    new DivideDecimalProjection([
                        new PiProjection(),
                        new LiteralInteger(180),
                    ]),
                ])
            )
        );
    }

    [Theory]
    [InlineData("sqrt", "-1", "")]
    [InlineData("ln", "0", "")]
    [InlineData("log", "8", "1")]
    [InlineData("power", "0", "-1")]
    [InlineData("power", "-8", "0.5")]
    [InlineData("asin", "1.0000000000000000000000000001", "")]
    [InlineData("atan2", "0", "0")]
    public void ValueOutsideTheDomainIsAnExecutionError(
        string function,
        string operand,
        string second
    )
    {
        ExprProjection expression = function switch
        {
            "log" => new LogDecimalProjection(Decimal(operand), Decimal(second)),
            "power" => new PowerDecimalProjection(Decimal(operand), Decimal(second)),
            "atan2" => new Atan2DecimalProjection(Decimal(operand), Decimal(second)),
            _ => Unary(function, Decimal(operand)),
        };

        Fails<ArgumentOutOfRangeException>(expression);
    }

    [Fact]
    public void DomainErrorOfARowValueFailsTheQueryAndAGuardAvoidsIt()
    {
        SubtractDecimalNullableProjection below = new([X, new LiteralInteger(3)]);

        Fails<ArgumentOutOfRangeException>(new SqrtDecimalNullableProjection(below));
        Assert.Equal(
            ["0", "", "1.802775637731994646559610634"],
            Values(
                new SqrtDecimalNullableProjection(
                    new GreatestDecimalNullableProjection([below, new LiteralInteger(0)])
                )
            )
        );
    }

    [Fact]
    public void PowersLogarithmsAndAtan2AreLiftedOverEveryOperand()
    {
        Assert.Equal(
            ["4", "", "76.10925536017414826991999812"],
            Values(new PowerDecimalNullableProjection(new LiteralInteger(2), X))
        );
        Assert.Equal(
            ["", "", ""],
            Values(
                new PowerDecimalNullableProjection(X, new LiteralDecimalNullable())
            )
        );
        Assert.Equal(
            ["3", "", "1.134706196049045044148158041"],
            Values(new LogDecimalNullableProjection(new LiteralInteger(8), X))
        );
        Assert.Equal(
            ["1.414213562373095048801688724", "", "2.5"],
            Values(new SqrtDecimalNullableProjection(X))
        );
        Assert.Equal(
            ["0.5880026035475675512456110806", "", "1.123276351637726739509678965"],
            Values(new Atan2DecimalNullableProjection(X, new LiteralInteger(3)))
        );
    }

    [Theory]
    [InlineData("exp 65")]
    [InlineData("round integer beyond 64 bits")]
    [InlineData("abs least integer")]
    [InlineData("integerDivide beyond 64 bits")]
    [InlineData("integerDivide least integer by -1")]
    public void ResultOutsideItsTypeIsAnExecutionError(string example)
    {
        DecimalProjection large = Decimal("9999999999999999999999999999");
        ExprProjection expression = example switch
        {
            "exp 65" => new ExpDecimalProjection(new LiteralInteger(65)),
            "round integer beyond 64 bits" => new RoundWithDigitsIntegerProjection(
                new LiteralInteger(long.MaxValue),
                new LiteralInteger(-3)
            ),
            "abs least integer" => new AbsIntegerProjection(
                new LiteralInteger(long.MinValue)
            ),
            "integerDivide beyond 64 bits" => new IntegerDivideIntegerProjection(
                large,
                Decimal("0.5")
            ),
            "integerDivide least integer by -1" => new IntegerDivideIntegerProjection(
                new LiteralInteger(long.MinValue),
                new LiteralInteger(-1)
            ),
            _ => throw new ArgumentException(example),
        };

        Fails<OverflowException>(expression);
    }

    [Theory]
    [InlineData("2.5", "3", "2")]
    [InlineData("-2.5", "-3", "-2")]
    [InlineData("2.5000001", "3", "3")]
    public void RoundWithoutDigitsRoundsHalfAwayFromZeroOrToEven(
        string operand,
        string awayFromZero,
        string toEven
    )
    {
        Assert.Equal(
            awayFromZero,
            Value(new RoundWithoutDigitsIntegerProjection(Decimal(operand)))
        );
        Assert.Equal(
            toEven,
            Value(
                new RoundWithoutDigitsIntegerProjection(
                    Decimal(operand),
                    RoundingMode.HalfEven
                )
            )
        );
    }

    [Theory]
    [InlineData("2.345", 2, "2.35", "2.34")]
    [InlineData("2.355", 2, "2.36", "2.36")]
    [InlineData("149.9999999999999999999999999", -2, "100", "100")]
    [InlineData(
        "0.0000000000000000000000000025",
        27,
        "0.000000000000000000000000003",
        "0.000000000000000000000000002"
    )]
    public void RoundADecimalToDigitsRoundsOnce(
        string operand,
        long digits,
        string awayFromZero,
        string toEven
    )
    {
        Assert.Equal(
            awayFromZero,
            Value(new RoundDecimalProjection(Decimal(operand), new LiteralInteger(digits)))
        );
        Assert.Equal(
            toEven,
            Value(
                new RoundDecimalProjection(
                    Decimal(operand),
                    new LiteralInteger(digits),
                    RoundingMode.HalfEven
                )
            )
        );
    }

    [Theory]
    [InlineData(1250, -2, "1300", "1200")]
    [InlineData(-1250, -2, "-1300", "-1200")]
    [InlineData(1234, 2, "1234", "1234")]
    public void RoundAnIntegerToDigitsKeepsAnInteger(
        long operand,
        long digits,
        string awayFromZero,
        string toEven
    )
    {
        Assert.Equal(
            awayFromZero,
            Value(
                new RoundWithDigitsIntegerProjection(
                    new LiteralInteger(operand),
                    new LiteralInteger(digits)
                )
            )
        );
        Assert.Equal(
            toEven,
            Value(
                new RoundWithDigitsIntegerProjection(
                    new LiteralInteger(operand),
                    new LiteralInteger(digits),
                    RoundingMode.HalfEven
                )
            )
        );
    }

    [Fact]
    public void RoundWithDigitsKeepsTheTypeOfItsOperand()
    {
        Assert.Equal(
            "decimal",
            ColumnType(new RoundDecimalProjection(Decimal("1234.5"), new LiteralInteger(-2)))
        );
        Assert.Equal(
            IntegerType,
            ColumnType(
                new RoundWithDigitsIntegerProjection(
                    new LiteralInteger(1250),
                    new LiteralInteger(-2)
                )
            )
        );
    }

    [Fact]
    public void RoundIsLiftedOverItsDigits()
    {
        Assert.Equal(
            ["1234.6", "1234.57", ""],
            Values(new RoundDecimalNullableProjection(Decimal("1234.567"), N))
        );
        Assert.Equal(
            ["1250", "1250", ""],
            Values(
                new RoundWithDigitsIntegerNullableProjection(new LiteralInteger(1250), N)
            )
        );
    }

    [Theory]
    [InlineData("2.7", "2")]
    [InlineData("-2.7", "-2")]
    public void TruncateWithoutDigitsRoundsTowardZero(string operand, string expected)
    {
        Assert.Equal(
            expected,
            Value(new TruncateWithoutDigitsIntegerProjection(Decimal(operand)))
        );
    }

    [Theory]
    [InlineData("1234.567", 2, "1234.56")]
    [InlineData("1234.567", -2, "1200")]
    public void TruncateADecimalToDigitsKeepsThatManyPlaces(
        string operand,
        long digits,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new TruncateDecimalProjection(Decimal(operand), new LiteralInteger(digits))
            )
        );
    }

    [Theory]
    [InlineData(1299, "1200")]
    [InlineData(long.MaxValue, "9223372036854775800")]
    public void TruncateAnIntegerToHundredsKeepsAnInteger(long operand, string expected)
    {
        TruncateWithDigitsIntegerProjection truncated = new(
            new LiteralInteger(operand),
            new LiteralInteger(-2)
        );

        Assert.Equal(expected, Value(truncated));
        Assert.Equal(IntegerType, ColumnType(truncated));
    }

    [Fact]
    public void FloorAndCeilingLeaveTheIntegersForTheSmallestFraction()
    {
        LiteralDecimal tiny = new(0.0000000000000000000000000001m);

        Assert.Equal(
            "-1",
            Value(new FloorIntegerProjection(Decimal("-0.0000000000000000000000000001")))
        );
        Assert.Equal("0", Value(new FloorIntegerProjection(tiny)));
        Assert.Equal("1", Value(new CeilingIntegerProjection(tiny)));
    }

    [Theory]
    [InlineData("7.5", "2", "3", "1.5")]
    [InlineData("-7.5", "2", "-3", "-1.5")]
    [InlineData("7.5", "-2", "-3", "1.5")]
    [InlineData("1", "0.3", "3", "0.1")]
    [InlineData("19.95", "0.05", "399", "0")]
    [InlineData("3", "1.0000000000000000000000000001", "2", "0.9999999999999999999999999998")]
    public void IntegerDivideAndModuloOfDecimalsAreExact(
        string left,
        string right,
        string quotient,
        string remainder
    )
    {
        Assert.Equal(
            quotient,
            Value(new IntegerDivideIntegerProjection(Decimal(left), Decimal(right)))
        );
        Assert.Equal(
            remainder,
            Value(new ModuloDecimalProjection(Decimal(left), Decimal(right)))
        );
    }

    [Fact]
    public void IntegerDivideIsNotTheTruncatedRoundedQuotient()
    {
        Assert.Equal(
            "3",
            Value(
                new TruncateWithoutDigitsIntegerProjection(
                    new DivideDecimalProjection([
                        new LiteralInteger(3),
                        Decimal("1.0000000000000000000000000001"),
                    ])
                )
            )
        );
        Assert.Equal(
            IntegerType,
            ColumnType(
                new IntegerDivideIntegerProjection(Decimal("7.5"), new LiteralInteger(2))
            )
        );
        Assert.Equal(
            "decimal",
            ColumnType(new ModuloDecimalProjection(Decimal("7.5"), new LiteralInteger(2)))
        );
    }

    [Fact]
    public void ModuloOfTheLeastIntegerByMinusOneIsZero()
    {
        Assert.Equal(
            "0",
            Value(
                new ModuloIntegerProjection(
                    new LiteralInteger(long.MinValue),
                    new LiteralInteger(-1)
                )
            )
        );
    }

    [Theory]
    [InlineData("integerDivide")]
    [InlineData("modulo")]
    public void DecimalRemainderByZeroIsAnExecutionError(string function)
    {
        ExprProjection expression = function == "modulo"
            ? new ModuloDecimalProjection(Decimal("1.5"), Decimal("0"))
            : new IntegerDivideIntegerProjection(Decimal("1.5"), Decimal("0"));

        Fails<DivideByZeroException>(expression);
    }

    [Fact]
    public void AbsKeepsTheTypeOfItsOperandAndSignIsAnInteger()
    {
        AbsIntegerProjection integer = new(new LiteralInteger(-3));
        SignIntegerProjection sign = new(Decimal("-2.5"));

        Assert.Equal("3", Value(integer));
        Assert.Equal(IntegerType, ColumnType(integer));
        Assert.Equal("2.5", Value(new AbsDecimalProjection(Decimal("-2.5"))));
        Assert.Equal("-1", Value(sign));
        Assert.Equal(IntegerType, ColumnType(sign));
        Assert.Equal("0", Value(new SignIntegerProjection(Decimal("0"))));
        Assert.Equal(
            "1",
            Value(new SignIntegerProjection(Decimal("0.0000000000000000000000000001")))
        );
        Assert.Equal("-1", Value(new SignIntegerProjection(new LiteralInteger(-7))));
    }

    [Fact]
    public void AbsAndSignAreLifted()
    {
        Assert.Equal(["2", "", "6.25"], Values(new AbsDecimalNullableProjection(X)));
        Assert.Equal(["1", "", "1"], Values(new SignIntegerNullableProjection(X)));
    }

    [Fact]
    public void GreatestAndLeastAreTypedAsAdd()
    {
        LeastIntegerProjection integers = new([
            new LiteralInteger(3),
            new LiteralInteger(1),
            new LiteralInteger(2),
        ]);
        LeastDecimalProjection mixed = new([Decimal("2.5"), new LiteralInteger(1)]);

        Assert.Equal(
            "2.5",
            Value(
                new GreatestDecimalProjection([
                    new LiteralInteger(1),
                    Decimal("2.5"),
                    new LiteralInteger(2),
                ])
            )
        );
        Assert.Equal("1", Value(integers));
        Assert.Equal(IntegerType, ColumnType(integers));
        Assert.Equal("1", Value(mixed));
        Assert.Equal("decimal", ColumnType(mixed));
    }

    [Fact]
    public void GreatestAndLeastAreNullWhenAnyOperandIsNull()
    {
        Assert.Equal(
            ["2", "", "6.25"],
            Values(new GreatestDecimalNullableProjection([X, new LiteralInteger(1)]))
        );
        Assert.Equal(
            ["3", "", "5"],
            Values(
                new LeastDecimalNullableProjection([
                    new GreatestDecimalNullableProjection([X, new LiteralInteger(3)]),
                    new LiteralInteger(5),
                ])
            )
        );
    }

    [Fact]
    public void NullIfEqualGuardsADivisionByZero()
    {
        NullIfEqualIntegerNullableProjection divisor = new(D, new LiteralInteger(0));

        Assert.Equal(
            ["2.5", "", "-2.5"],
            Values(new DivideDecimalNullableProjection([new LiteralInteger(10), divisor]))
        );
        Assert.Equal(
            ["2.5", "0", "-2.5"],
            Values(
                new CoalesceDecimalProjection([
                    new DivideDecimalNullableProjection([new LiteralInteger(10), divisor]),
                    new LiteralInteger(0),
                ])
            )
        );
        Assert.Equal(
            ["1", "", "-1"],
            Values(new IntegerDivideIntegerNullableProjection(new LiteralInteger(7), divisor))
        );
        Assert.Equal(
            ["3", "", "3"],
            Values(new ModuloIntegerNullableProjection(new LiteralInteger(7), divisor))
        );
    }

    [Fact]
    public void NullIfEqualComparesNumbersByValueAndKeepsTheTypeOfLeft()
    {
        NullIfEqualIntegerNullableProjection kept = new(
            new LiteralInteger(2),
            Decimal("2.5")
        );

        Assert.Equal(
            "",
            Value(new NullIfEqualDecimalNullableProjection(Decimal("0"), new LiteralInteger(0)))
        );
        Assert.Equal(
            "",
            Value(new NullIfEqualIntegerNullableProjection(new LiteralInteger(2), Decimal("2")))
        );
        Assert.Equal("2", Value(kept));
        Assert.Equal(IntegerType, ColumnType(kept));
        Assert.Equal(
            ["4", "0", "-4"],
            Values(new NullIfEqualIntegerNullableProjection(D, new LiteralDecimalNullable()))
        );
        Assert.Equal(
            ["", "", "6.25"],
            Values(new NullIfEqualDecimalNullableProjection(X, new LiteralInteger(2)))
        );
    }

    [Fact]
    public void DivideAndMultiplyOverAListRoundEveryStep()
    {
        Assert.Equal(
            "0.3333333333333333333333333334",
            Value(
                new DivideDecimalProjection([
                    new LiteralInteger(2),
                    new LiteralInteger(3),
                    new LiteralInteger(2),
                ])
            )
        );
        Assert.Equal(
            "0.3333333333333333333333333333",
            Value(
                new DivideDecimalProjection([
                    new LiteralInteger(2),
                    new MultiplyIntegerProjection([
                        new LiteralInteger(3),
                        new LiteralInteger(2),
                    ]),
                ])
            )
        );
        Assert.Equal(
            ["0.25", "", "0.78125"],
            Values(
                new DivideDecimalNullableProjection([
                    X,
                    new LiteralInteger(4),
                    new LiteralInteger(2),
                ])
            )
        );
        Assert.Equal(
            "0.0000000000000000000000000006",
            Value(
                new MultiplyDecimalProjection([
                    Decimal("1.5"),
                    Decimal("0.0000000000000000000000000001"),
                    new LiteralInteger(3),
                ])
            )
        );
    }

    [Fact]
    public void AddAndSubtractOverAListGoFromLeftToRight()
    {
        Assert.Equal(
            "9223372036854775807",
            Value(
                new AddIntegerProjection([
                    new LiteralInteger(long.MaxValue),
                    new LiteralInteger(-1),
                    new LiteralInteger(1),
                ])
            )
        );
        Assert.Equal(
            "5",
            Value(
                new SubtractIntegerProjection([
                    new LiteralInteger(10),
                    new LiteralInteger(3),
                    new LiteralInteger(2),
                ])
            )
        );
    }

    [Theory]
    [InlineData("add integers")]
    [InlineData("add before a decimal")]
    [InlineData("add decimals")]
    public void StepOfAListOutsideItsTypeFailsWhenALaterStepWouldBringItBack(
        string example
    )
    {
        LiteralInteger max = new(long.MaxValue);
        ExprProjection expression = example switch
        {
            "add integers" => new AddIntegerProjection([
                max,
                new LiteralInteger(1),
                new LiteralInteger(-1),
            ]),
            "add before a decimal" => new AddDecimalProjection([
                max,
                new LiteralInteger(1),
                Decimal("0.5"),
            ]),
            "add decimals" => new AddDecimalProjection([
                Decimal("9999999999999999999999999999"),
                new LiteralInteger(1),
                new LiteralInteger(-1),
            ]),
            _ => throw new ArgumentException(example),
        };

        Fails<OverflowException>(expression);
    }

    [Fact]
    public void MathOperatorsFilterRowsInWhere()
    {
        Assert.Equal(
            ["3"],
            Ids(new GreaterThanDecimalRow(new SqrtDecimalNullableRow(X), new LiteralInteger(2)))
        );
        Assert.Equal(
            ["1", "3"],
            Ids(
                new EqualDecimalRow(
                    new PowerDecimalNullableRow(X, new LiteralInteger(0)),
                    new LiteralInteger(1)
                )
            )
        );
        Assert.Equal(
            ["2"],
            Ids(
                new EqualDecimalRow(
                    new ModuloIntegerRow(Id, new LiteralInteger(2)),
                    new LiteralInteger(0)
                )
            )
        );
    }

    [Fact]
    public void MathOperatorsReadTheAggregatesOfAGroup()
    {
        PureQLProjection projection = new(
            [Numbers],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                groupBy: [new GroupKey("g", new FieldString("t.numbers", "g"))],
                select:
                [
                    new SelectItemGroup("g", new KeyString("g")),
                    new SelectItemGroup(
                        "squared",
                        new PowerDecimalGroup(new SumDecimalGroup(X), new LiteralInteger(2))
                    ),
                    new SelectItemGroup(
                        "root",
                        new SqrtDecimalGroup(new SumDecimalGroup(X))
                    ),
                    new SelectItemGroup(
                        "hundreds",
                        new RoundWithDigitsIntegerGroup(
                            new SumIntegerGroup(Q),
                            new LiteralInteger(-2)
                        )
                    ),
                    new SelectItemGroup(
                        "even",
                        new RoundWithDigitsIntegerGroup(
                            new SumIntegerGroup(Q),
                            new LiteralInteger(-2),
                            RoundingMode.HalfEven
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("g")]
            )
        );

        Assert.Equal(["4", "39.0625"], ResultColumn.Texts(projection, "squared"));
        Assert.Equal(
            ["1.414213562373095048801688724", "2.5"],
            ResultColumn.Texts(projection, "root")
        );
        Assert.Equal(["1200", "1300"], ResultColumn.Texts(projection, "hundreds"));
        Assert.Equal(["1200", "1200"], ResultColumn.Texts(projection, "even"));
    }

    [Fact]
    public void MathOperatorsReadTheAggregatesOfATotalQuery()
    {
        PureQLProjection projection = new(
            [Numbers],
            new MainTotalQuery(
                from: new FromEntity("t.numbers"),
                aggregate:
                [
                    new SelectItemTotal(
                        "rounded",
                        new RoundWithoutDigitsIntegerNullableTotal(
                            new AverageDecimalNullableTotal(X)
                        )
                    ),
                    new SelectItemTotal(
                        "root",
                        new SqrtDecimalNullableTotal(new MaxDecimalNullableTotal(X))
                    ),
                    new SelectItemTotal(
                        "log",
                        new LogDecimalNullableTotal(
                            new MaxDecimalNullableTotal(X),
                            Decimal("2.5")
                        )
                    ),
                    new SelectItemTotal("pi", new PiTotal()),
                ]
            )
        );

        Assert.Equal(["4"], ResultColumn.Texts(projection, "rounded"));
        Assert.Equal(["2.5"], ResultColumn.Texts(projection, "root"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "log"));
        Assert.Equal(["3.141592653589793238462643383"], ResultColumn.Texts(projection, "pi"));
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

    private static LiteralDecimal Decimal(string text)
    {
        return new LiteralDecimal(decimal.Parse(text, CultureInfo.InvariantCulture));
    }

    // The query that selects one expression for every row, in the order of id.
    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return new PureQLProjection(
            [Numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
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

    private static IReadOnlyList<string> Ids(BooleanRow where)
    {
        return ResultColumn.Texts(
            new PureQLProjection(
                [Numbers],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.numbers"),
                    where: where,
                    select: [new SelectItemProjection("id", Id)],
                    orderBy: [new OrderItemProjectionColumn("id")]
                )
            ),
            "id"
        );
    }
}
