using System.Globalization;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The same numeric expression gives the same value, or fails with the same
// error, whether its operands are literals (which the tree folds when the
// projection is built), stored columns (read when it runs) or bound
// parameters (captured, never folded): the specification defines the value
// of an expression by its operands, not by where they come from. Each
// expected value is derived from the specification ("Integer and decimal
// arithmetic", "Execution errors", "Evaluation"); "overflow", "zero" and
// "domain" name the execution error of a result out of range, a division by
// zero and a value outside a function's domain.
public sealed class ConstantFoldingParityTests
{
    private const long Max = long.MaxValue;

    private const long Min = long.MinValue;

    private const string Largest = "9999999999999999999999999999";

    private const string Tiny4 = "0.0000000000000000000000000004";

    // "divide computes the exact quotient, rounded half away from zero to the
    // decimal precision", and round then rounds that decimal: 2.5 /
    // 0.9999999999999999999999999999 is 2.50000000000000000000000000025…,
    // which divide rounds to 2.5, so round(…, halfEven) is 2. Rounding the
    // exact quotient once instead would give 3.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void RoundOfAQuotientRoundsTheRoundedQuotientHalfToEven(Form form)
    {
        Same(form, "2", RoundOfAQuotient(RoundingMode.HalfEven), Dec("2.5"), Dec(OneLess));
    }

    // 2.499999999999999999999999999 / 0.9999999999999999999999999997 is
    // 2.49999999999999999999999999975…, which divide rounds to 2.5; round
    // sends 2.5 away from zero, to 3, where the exact quotient would give 2.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void RoundOfAQuotientRoundsTheRoundedQuotientHalfAwayFromZero(Form form)
    {
        Same(
            form,
            "3",
            RoundOfAQuotient(null),
            Dec("2.499999999999999999999999999"),
            Dec("0.9999999999999999999999999997")
        );
    }

    // "integerDivide is not truncate(divide(…)): divide rounds its quotient
    // to the decimal precision first": 2.999999999999999999999999999 /
    // 0.9999999999999999999999999997 is 2.99999999999999999999999999989…,
    // which divide rounds to 3, so floor, ceiling and truncate of it are 3,
    // where integerDivide truncates the exact quotient to 2.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void WholeNumbersOfAQuotientReadTheRoundedQuotient(Form form)
    {
        (string, string)[] positive =
        [
            Dec("2.999999999999999999999999999"),
            Dec("0.9999999999999999999999999997"),
        ];
        (string, string)[] negative =
        [
            Dec("-2.999999999999999999999999999"),
            Dec("0.9999999999999999999999999997"),
        ];

        Same(form, "3", values => new FloorIntegerProjection(Quotient(values)), positive);
        Same(form, "3", values => new CeilingIntegerProjection(Quotient(values)), positive);
        Same(
            form,
            "3",
            values => new TruncateWithoutDigitsIntegerProjection(Quotient(values)),
            positive
        );
        Same(form, "-3", values => new FloorIntegerProjection(Quotient(values)), negative);
        Same(
            form,
            "-3",
            values => new CeilingIntegerProjection(Quotient(values)),
            negative
        );
        Same(
            form,
            "2",
            values => new IntegerDivideIntegerProjection(
                values.Decimal(0),
                values.Decimal(1)
            ),
            positive
        );
        Same(
            form,
            "3",
            values => new TruncateDecimalProjection(Quotient(values), new LiteralInteger(0)),
            positive
        );
    }

    // Where the quotient needs no rounding of its own, both readings agree.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void RoundOfAnExactQuotientIsTheSameInEveryForm(Form form)
    {
        Same(form, "21", RoundOfAQuotient(null), Dec("1.025"), Dec("0.05"));
        Same(form, "20", RoundOfAQuotient(RoundingMode.HalfEven), Dec("1.025"), Dec("0.05"));
        Same(form, "3", RoundOfAQuotient(null), Dec("2.5"), Dec(OneLess));
    }

    // round with digits rounds the decimal divide gives, in every form.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void RoundOfAQuotientToDigitsRoundsTheRoundedQuotient(Form form)
    {
        Same(
            form,
            "2",
            values => new RoundDecimalProjection(
                new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                values.Integer(2),
                RoundingMode.HalfEven
            ),
            Dec("2.5"),
            Dec(OneLess),
            Int(0)
        );
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void EachStepOfAListIsRoundedInEveryForm(Form form)
    {
        Same(form, "1", Add3, Int(1), Dec(Tiny4), Dec(Tiny4));
        Same(form, "1.000000000000000000000000001", Add3, Dec(Tiny4), Dec(Tiny4), Int(1));
        Same(
            form,
            "0.3333333333333333333333333334",
            values => new DivideDecimalProjection([
                values.Decimal(0),
                values.Decimal(1),
                values.Decimal(2),
            ]),
            Int(2),
            Int(3),
            Int(2)
        );
        Same(
            form,
            "0.0000000000000000000000000006",
            values => new MultiplyDecimalProjection([
                values.Decimal(0),
                values.Decimal(1),
                values.Decimal(2),
            ]),
            Dec("1.5"),
            Dec("0.0000000000000000000000000001"),
            Int(3)
        );
        Same(form, "9223372036854775807", Add3, Int(Max), Int(-1), Int(1));
        Same(form, "overflow", Add3, Int(Max), Int(1), Dec("0.5"));
        Same(form, "overflow", Add3, Dec(Largest), Int(1), Int(-1));
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void IntegerOverflowIsAnErrorInEveryForm(Form form)
    {
        Same(form, "overflow", Add2Integers, Int(Max), Int(1));
        Same(form, "overflow", Add2Integers, Int(Min), Int(-1));
        Same(
            form,
            "overflow",
            values => new MultiplyIntegerProjection([
                values.Integer(0),
                values.Integer(1),
            ]),
            Int(Min),
            Int(-1)
        );
        Same(
            form,
            "overflow",
            values => new SubtractIntegerProjection([
                values.Integer(0),
                values.Integer(1),
            ]),
            Int(0),
            Int(Min)
        );
        Same(
            form,
            "overflow",
            values => new IntegerDivideIntegerProjection(
                values.Decimal(0),
                values.Decimal(1)
            ),
            Int(Min),
            Int(-1)
        );
        Same(
            form,
            "overflow",
            values => new AbsIntegerProjection(values.Integer(0)),
            Int(Min)
        );
        Same(
            form,
            "overflow",
            values => new RoundWithDigitsIntegerProjection(
                values.Integer(0),
                values.Integer(1)
            ),
            Int(Max),
            Int(-1)
        );
        Same(
            form,
            "overflow",
            values => new CeilingIntegerProjection(values.Declared(0)),
            Dec("9223372036854775807.1")
        );
        Same(
            form,
            "overflow",
            values => new FloorIntegerProjection(values.Declared(0)),
            Dec("-9223372036854775808.1")
        );
        Same(
            form,
            "overflow",
            values => new RoundWithoutDigitsIntegerProjection(values.Declared(0)),
            Dec("9223372036854775807.5")
        );
        Same(
            form,
            "overflow",
            values => new TruncateWithoutDigitsIntegerProjection(values.Declared(0)),
            Dec("9223372036854775808.5")
        );
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void IntegerResultsAtTheEdgeAreTheSameInEveryForm(Form form)
    {
        Same(
            form,
            "0",
            values => new ModuloIntegerProjection(values.Integer(0), values.Integer(1)),
            Int(Min),
            Int(-1)
        );
        Same(
            form,
            "-1",
            values => new ModuloIntegerProjection(values.Integer(0), values.Integer(1)),
            Int(-7),
            Int(-2)
        );
        Same(
            form,
            "-3",
            values => new IntegerDivideIntegerProjection(
                values.Decimal(0),
                values.Decimal(1)
            ),
            Dec("-7.5"),
            Int(2)
        );
        Same(
            form,
            "-1.5",
            values => new ModuloDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Dec("-7.5"),
            Int(2)
        );
        Same(
            form,
            "-9223372036854775808",
            values => new RoundWithoutDigitsIntegerProjection(
                values.Declared(0),
                RoundingMode.HalfEven
            ),
            Dec("-9223372036854775808.5")
        );
        Same(
            form,
            "9223372036854775807",
            values => new AbsIntegerProjection(values.Integer(0)),
            Int(Min + 1)
        );
        Same(
            form,
            "-1",
            values => new SignIntegerProjection(values.Decimal(0)),
            Dec("-0.0000000000000000000000000001")
        );
        Same(
            form,
            "-9223372036854775800",
            values => new TruncateWithDigitsIntegerProjection(
                values.Integer(0),
                values.Integer(1)
            ),
            Int(Min),
            Int(-1)
        );
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void DivisionByZeroIsAnErrorInEveryForm(Form form)
    {
        Same(
            form,
            "zero",
            values => new IntegerDivideIntegerProjection(
                values.Decimal(0),
                values.Decimal(1)
            ),
            Int(7),
            Int(0)
        );
        Same(
            form,
            "zero",
            values => new ModuloIntegerProjection(values.Integer(0), values.Integer(1)),
            Int(7),
            Int(0)
        );
        Same(
            form,
            "zero",
            values => new ModuloDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Dec("7.5"),
            Int(0)
        );
        Same(
            form,
            "zero",
            values => new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
            Int(1),
            Int(0)
        );
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void DecimalRoundingAndRangeAreTheSameInEveryForm(Form form)
    {
        Same(form, Largest, Add2Decimals, Dec(Largest), Dec("0.4"));
        Same(form, "overflow", Add2Decimals, Dec(Largest), Dec("0.5"));
        Same(form, "1.000000000000000000000000001", Add2Decimals, Int(1), Dec(Tiny(5)));
        Same(
            form,
            "-0.6666666666666666666666666667",
            values => new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
            Int(-2),
            Int(3)
        );
        Same(
            form,
            "1.000000000000000000000000001",
            values => new MultiplyDecimalProjection([
                values.Decimal(0),
                values.Decimal(1),
            ]),
            Dec("2.000000000000000000000000001"),
            Dec("0.5")
        );
        Same(
            form,
            "0",
            values => new MultiplyDecimalProjection([
                values.Decimal(0),
                values.Decimal(1),
            ]),
            Dec("-0.5"),
            Dec("0")
        );
        Same(
            form,
            "0",
            values => new AsStringDecimalStringProjection(
                new MultiplyDecimalProjection([values.Decimal(0), values.Decimal(1)])
            ),
            Dec("-0.5"),
            Dec("0")
        );
        Same(
            form,
            "overflow",
            values => new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
            Dec(Largest),
            Dec("0.5")
        );
    }

    // Digits written as a literal and digits read when the query runs round
    // the same way, to places, to tens and hundreds, and beyond every digit.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void RoundAndTruncateWithDigitsAreTheSameInEveryForm(Form form)
    {
        Same(form, "1234.57", RoundDigits(null), Dec("1234.5678"), Int(2));
        Same(form, "1200", RoundDigits(null), Dec("1234.5678"), Int(-2));
        Same(form, "1234.5678", RoundDigits(null), Dec("1234.5678"), Int(Max));
        Same(form, "0", RoundDigits(null), Dec("1234.5678"), Int(Min));
        Same(form, "0", RoundDigits(null), Dec("1234.5678"), Int(-29));
        Same(form, "2.34", RoundDigits(RoundingMode.HalfEven), Dec("2.345"), Int(2));
        Same(form, "1300", RoundDigits(null), Dec("1250"), Int(-2));
        Same(form, "1200", RoundDigits(RoundingMode.HalfEven), Dec("1250"), Int(-2));
        Same(form, "0", RoundDigits(RoundingMode.HalfEven), Dec("-0.05"), Int(1));
        Same(form, "-0.1", RoundDigits(null), Dec("-0.05"), Int(1));
        Same(form, "0", RoundDigits(null), Dec("-0.4"), Int(0));
        Same(form, "overflow", RoundDigits(null), Dec(Largest), Int(-1));
        Same(
            form,
            "overflow",
            RoundDigits(null),
            Dec("5000000000000000000000000000"),
            Int(-28)
        );
        Same(
            form,
            "0",
            RoundDigits(RoundingMode.HalfEven),
            Dec("5000000000000000000000000000"),
            Int(-28)
        );
        Same(
            form,
            "1200",
            values => new TruncateDecimalProjection(values.Declared(0), values.Integer(1)),
            Dec("1299.99"),
            Int(-2)
        );
        Same(
            form,
            "9000000000000000000000000000",
            values => new TruncateDecimalProjection(values.Declared(0), values.Integer(1)),
            Dec(Largest),
            Int(-27)
        );
        Same(
            form,
            "1300",
            values => new RoundWithDigitsIntegerProjection(
                values.Integer(0),
                values.Integer(1)
            ),
            Int(1250),
            Int(-2)
        );
        Same(
            form,
            "0",
            values => new RoundWithDigitsIntegerProjection(
                values.Integer(0),
                values.Integer(1),
                RoundingMode.HalfEven
            ),
            Int(5000000000000000000),
            Int(-19)
        );
    }

    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void PowersLogarithmsAndTrigonometryAreTheSameInEveryForm(Form form)
    {
        Same(
            form,
            "9903520314283042199192993792",
            values => new PowerDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Int(2),
            Int(93)
        );
        Same(
            form,
            "overflow",
            values => new PowerDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Int(2),
            Int(94)
        );
        Same(
            form,
            "domain",
            values => new PowerDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Int(-8),
            Dec("0.5")
        );
        Same(
            form,
            "domain",
            values => new SqrtDecimalProjection(values.Decimal(0)),
            Dec("-0.0000000000000000000000000001")
        );
        Same(
            form,
            "overflow",
            values => new ExpDecimalProjection(values.Decimal(0)),
            Int(65)
        );
        Same(
            form,
            "domain",
            values => new LogDecimalProjection(values.Decimal(0), values.Decimal(1)),
            Int(8),
            Int(1)
        );
        Same(
            form,
            "0.0000000000000000000000000003",
            values => new SinDecimalProjection(values.Decimal(0)),
            Dec("3.141592653589793238462643383")
        );
        Same(
            form,
            "domain",
            values => new Atan2DecimalProjection(values.Decimal(0), values.Decimal(1)),
            Int(0),
            Int(0)
        );
        Same(
            form,
            "28",
            values => new Log10DecimalProjection(values.Decimal(0)),
            Dec(Largest)
        );
    }

    // "Integer and decimal arithmetic": comparisons between integer and
    // decimal compare numeric values, of the results as the operators round
    // them.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void ComparisonsReadTheRoundedResultsInEveryForm(Form form)
    {
        Same(
            form,
            "True",
            values => new EqualDecimalProjection(
                new AddDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                values.Nullable(2)
            ),
            Dec("0.1"),
            Dec("0.2"),
            Dec("0.3")
        );
        Same(
            form,
            "True",
            values => new EqualDecimalProjection(
                new AddDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                values.Nullable(2)
            ),
            Int(1),
            Dec(Tiny4),
            Int(1)
        );
        Same(
            form,
            "True",
            values => new EqualDecimalProjection(
                new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                values.Nullable(2)
            ),
            Int(1),
            Int(3),
            Dec("0.3333333333333333333333333333")
        );
        Same(
            form,
            "False",
            values => new EqualDecimalProjection(values.Nullable(0), values.Nullable(1)),
            Int(Max),
            Dec("9223372036854775807.5")
        );
        Same(
            form,
            "True",
            values => new LessThanDecimalProjection(values.Nullable(0), values.Nullable(1)),
            Int(Max),
            Dec("9223372036854775807.5")
        );
        Same(
            form,
            "",
            values => new NullIfEqualDecimalNullableProjection(
                new AddDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                values.Nullable(2)
            ),
            Int(1),
            Dec(Tiny4),
            Int(1)
        );
        Same(
            form,
            "9223372036854775807.5",
            values => new GreatestDecimalProjection([values.Decimal(0), values.Decimal(1)]),
            Int(Max),
            Dec("9223372036854775807.5")
        );
    }

    // "Evaluation": if evaluates only the branch it takes, so
    // if(equal(n, 0), 0, divide(x, n)) never divides by zero.
    [Theory]
    [InlineData(Form.Literals)]
    [InlineData(Form.Columns)]
    [InlineData(Form.Parameters)]
    public void AGuardedDivisionNeverDividesByZeroInAnyForm(Form form)
    {
        Same(
            form,
            "0",
            values => new IfDecimalProjection(
                new LiteralInteger(0),
                new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]),
                new EqualDecimalProjection(values.Nullable(1), new LiteralInteger(0))
            ),
            Int(1),
            Int(0)
        );
        Same(
            form,
            "",
            values => new DivideDecimalNullableProjection([
                values.Nullable(0),
                new NullIfEqualIntegerNullableProjection(
                    values.NullableInteger(1),
                    new LiteralInteger(0)
                ),
            ]),
            Int(1),
            Int(0)
        );
    }

    public enum Form
    {
        Literals,
        Columns,
        Parameters,
    }

    private const string OneLess = "0.9999999999999999999999999999";

    private static Func<Values, ExprProjection> RoundOfAQuotient(RoundingMode? mode)
    {
        return values =>
        {
            DivideDecimalProjection quotient = new([values.Decimal(0), values.Decimal(1)]);

            return mode is RoundingMode named
                ? new RoundWithoutDigitsIntegerProjection(quotient, named)
                : new RoundWithoutDigitsIntegerProjection(quotient);
        };
    }

    private static DivideDecimalProjection Quotient(Values values)
    {
        return new DivideDecimalProjection([values.Decimal(0), values.Decimal(1)]);
    }

    private static Func<Values, ExprProjection> RoundDigits(RoundingMode? mode)
    {
        return values => mode is RoundingMode named
            ? new RoundDecimalProjection(values.Declared(0), values.Integer(1), named)
            : new RoundDecimalProjection(values.Declared(0), values.Integer(1));
    }

    private static ExprProjection Add3(Values values)
    {
        return new AddDecimalProjection([
            values.Decimal(0),
            values.Decimal(1),
            values.Decimal(2),
        ]);
    }

    private static ExprProjection Add2Decimals(Values values)
    {
        return new AddDecimalProjection([values.Decimal(0), values.Decimal(1)]);
    }

    private static ExprProjection Add2Integers(Values values)
    {
        return new AddIntegerProjection([values.Integer(0), values.Integer(1)]);
    }

    private static string Tiny(int digit)
    {
        return "0." + new string('0', 27) + digit.ToString(CultureInfo.InvariantCulture);
    }

    private static (string Type, string Text) Int(long value)
    {
        return ("integer", value.ToString(CultureInfo.InvariantCulture));
    }

    private static (string Type, string Text) Dec(string text)
    {
        return ("decimal", text);
    }

    // The value the expression gives over these operands in this form, or
    // the error it fails with.
    private static void Same(
        Form form,
        string expected,
        Func<Values, ExprProjection> build,
        params (string Type, string Text)[] operands
    )
    {
        Values values = new(form, operands);

        PureQLProjection Projection()
        {
            return values.Selecting(build(values));
        }

        if (expected == "overflow")
        {
            _ = Assert.Throws<OverflowException>(() => Projection().ToList());
        }
        else if (expected == "zero")
        {
            _ = Assert.Throws<DivideByZeroException>(() => Projection().ToList());
        }
        else if (expected == "domain")
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => Projection().ToList());
        }
        else
        {
            Assert.Equal(
                expected,
                Assert.Single(ResultColumn.Texts(Projection(), "value"))
            );
        }
    }

    // The operands of one expression: literals, the cells of one stored row,
    // or bound parameters.
    private sealed class Values(Form form, (string Type, string Text)[] operands)
    {
        private readonly Form _form = form;

        private readonly (string Type, string Text)[] _operands = operands;

        public DecimalProjection Decimal(int index)
        {
            (string type, string text) = _operands[index];
            string name = Name(index);

            return type == "integer"
                ? _form switch
                {
                    Form.Literals => new LiteralInteger(Long(text)),
                    Form.Columns => new FieldInteger("t.values", name),
                    Form.Parameters => new ParamInteger(name),
                    _ => throw new InvalidOperationException(_form.ToString()),
                }
                : _form switch
                {
                    Form.Literals => new LiteralDecimal(Number(text)),
                    Form.Columns => new FieldDecimal("t.values", name),
                    Form.Parameters => new ParamDecimal(name),
                    _ => throw new InvalidOperationException(_form.ToString()),
                };
        }

        // An operand that declares decimal, not integer.
        public DeclaresDecimalProjection Declared(int index)
        {
            string name = Name(index);

            return _form switch
            {
                Form.Literals => new LiteralDecimal(Number(_operands[index].Text)),
                Form.Columns => new FieldDecimal("t.values", name),
                Form.Parameters => new ParamDecimal(name),
                _ => throw new InvalidOperationException(_form.ToString()),
            };
        }

        public DecimalNullableProjection Nullable(int index)
        {
            (string type, string text) = _operands[index];
            string name = Name(index);

            return type == "integer"
                ? _form switch
                {
                    Form.Literals => new LiteralInteger(Long(text)),
                    Form.Columns => new FieldInteger("t.values", name),
                    Form.Parameters => new ParamInteger(name),
                    _ => throw new InvalidOperationException(_form.ToString()),
                }
                : _form switch
                {
                    Form.Literals => new LiteralDecimal(Number(text)),
                    Form.Columns => new FieldDecimal("t.values", name),
                    Form.Parameters => new ParamDecimal(name),
                    _ => throw new InvalidOperationException(_form.ToString()),
                };
        }

        public IntegerNullableProjection NullableInteger(int index)
        {
            string name = Name(index);

            return _form switch
            {
                Form.Literals => new LiteralInteger(Long(_operands[index].Text)),
                Form.Columns => new FieldInteger("t.values", name),
                Form.Parameters => new ParamInteger(name),
                _ => throw new InvalidOperationException(_form.ToString()),
            };
        }

        public IntegerProjection Integer(int index)
        {
            string name = Name(index);

            return _form switch
            {
                Form.Literals => new LiteralInteger(Long(_operands[index].Text)),
                Form.Columns => new FieldInteger("t.values", name),
                Form.Parameters => new ParamInteger(name),
                _ => throw new InvalidOperationException(_form.ToString()),
            };
        }

        public PureQLProjection Selecting(ExprProjection expression)
        {
            IColumnType[] types =
            [
                .. _operands.Select(operand =>
                    operand.Type == "integer"
                        ? (IColumnType)new LongColumnType()
                        : new DoubleColumnType()
                ),
            ];
            IStoredSchemaDataSet table = new InlineTable(
                "t",
                "values",
                [
                    ("id", new LongColumnType()),
                    .. types.Select((type, index) => (Name(index), type)),
                ]
            ).With([.. _operands.Select(operand => operand.Text).Prepend("1")]);
            PureQLQuery query = new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.values"),
                select: [new SelectItemProjection("value", expression)]
            );

            return _form == Form.Parameters
                ? new PureQLProjection(
                    [table],
                    query,
                    _operands
                        .Select((operand, index) => (operand, index))
                        .ToDictionary(
                            pair => Name(pair.index),
                            pair =>
                                (IParameter)
                                    new TextParameter(pair.operand.Type, pair.operand.Text)
                        )
                )
                : new PureQLProjection([table], query);
        }

        private static string Name(int index)
        {
            return "v" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static long Long(string text)
        {
            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        private static decimal Number(string text)
        {
            return decimal.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
