using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Integer and decimal arithmetic as the specification defines it: integer is
// a checked 64-bit integer, decimal is exact, integerDivide truncates toward
// zero, modulo takes the sign of its left operand, round rounds half away
// from zero, and lifted operators are null when an operand is null.
public sealed class ArithmeticTests
{
    private static readonly IStoredSchemaDataSet Numbers = new InlineTable(
        "t",
        "numbers",
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("m", new DoubleColumnType())
    ).With(["7", "2.5", "1234.5"], ["-7", "-2.5", ""]);

    [Fact]
    public void IntegerDivideTruncatesTowardZeroAndModuloTakesTheLeftSign()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "quotient",
                                    new IntegerProjection(
                                        new ArithmeticIntegerProjection(
                                            new IntegerDivideIntegerProjection(
                                                new IntegerProjection(
                                                    new FieldInteger("t.numbers", "n")
                                                ),
                                                new IntegerProjection(new LiteralInteger(2))
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "remainder",
                                    new IntegerProjection(
                                        new ArithmeticIntegerProjection(
                                            new ModuloIntegerProjection(
                                                new IntegerProjection(
                                                    new FieldInteger("t.numbers", "n")
                                                ),
                                                new IntegerProjection(new LiteralInteger(2))
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["3", "-3"], ResultColumn.Texts(projection, "quotient"));
        Assert.Equal(["1", "-1"], ResultColumn.Texts(projection, "remainder"));
    }

    [Fact]
    public void IntegerAdditionBeyondSixtyFourBitsFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "sum",
                                    new IntegerProjection(
                                        new ArithmeticIntegerProjection(
                                            new AddIntegerProjection(
                                                [
                                                    new IntegerProjection(
                                                        new LiteralInteger(long.MaxValue)
                                                    ),
                                                    new IntegerProjection(
                                                        new LiteralInteger(1)
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        _ = Assert.Throws<OverflowException>(() => projection.ToList());
    }

    [Fact]
    public void IntegerSubtractionAndMultiplicationStayIntegers()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "result",
                                    new IntegerProjection(
                                        new ArithmeticIntegerProjection(
                                            new SubtractIntegerProjection(
                                                [
                                                    new IntegerProjection(
                                                        new ArithmeticIntegerProjection(
                                                            new MultiplyIntegerProjection(
                                                                [
                                                                    new IntegerProjection(
                                                                        new FieldInteger(
                                                                            "t.numbers",
                                                                            "n"
                                                                        )
                                                                    ),
                                                                    new IntegerProjection(
                                                                        new LiteralInteger(3)
                                                                    ),
                                                                ]
                                                            )
                                                        )
                                                    ),
                                                    new IntegerProjection(
                                                        new LiteralInteger(1)
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["20", "-22"], ResultColumn.Texts(projection, "result"));
    }

    [Fact]
    public void DivisionByZeroFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "ratio",
                                    new DecimalProjection(
                                        new ArithmeticDecimalProjection(
                                            new DivideDecimalProjection(
                                                [
                                                    new DecimalProjection(
                                                        new FieldAsDecimal(
                                                            new FieldDecimal("t.numbers", "d")
                                                        )
                                                    ),
                                                    new DecimalProjection(
                                                        new LiteralAsDecimal(
                                                            new LiteralInteger(0)
                                                        )
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        _ = Assert.Throws<DivideByZeroException>(() => projection.ToList());
    }

    [Fact]
    public void DecimalAdditionIsExact()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "exact",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new EqualProjection(
                                                new EqualDecimalProjection(
                                                    new DecimalNullableProjection(
                                                        new ArithmeticDecimalNullableProjection(
                                                            new AddDecimalNullableProjection(
                                                                [
                                                                    new DecimalNullableProjection(
                                                                        new LiteralAsDecimalNullable(
                                                                            new LiteralDecimal(0.1m)
                                                                        )
                                                                    ),
                                                                    new DecimalNullableProjection(
                                                                        new LiteralAsDecimalNullable(
                                                                            new LiteralDecimal(0.2m)
                                                                        )
                                                                    ),
                                                                ]
                                                            )
                                                        )
                                                    ),
                                                    new DecimalNullableProjection(
                                                        new LiteralAsDecimalNullable(
                                                            new LiteralDecimal(0.3m)
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["True"], ResultColumn.Texts(projection, "exact"));
    }

    [Fact]
    public void RoundFloorAndCeilingRoundHalfAwayFromZeroAndTowardInfinities()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "rounded",
                                    new IntegerProjection(
                                        new RoundingIntegerProjection(
                                            new RoundIntegerProjection(
                                                new DecimalProjection(
                                                    new FieldAsDecimal(
                                                        new FieldDecimal("t.numbers", "d")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "floored",
                                    new IntegerProjection(
                                        new RoundingIntegerProjection(
                                            new FloorIntegerProjection(
                                                new DecimalProjection(
                                                    new FieldAsDecimal(
                                                        new FieldDecimal("t.numbers", "d")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "ceiled",
                                    new IntegerProjection(
                                        new RoundingIntegerProjection(
                                            new CeilingIntegerProjection(
                                                new DecimalProjection(
                                                    new FieldAsDecimal(
                                                        new FieldDecimal("t.numbers", "d")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["3", "-3"], ResultColumn.Texts(projection, "rounded"));
        Assert.Equal(["2", "-3"], ResultColumn.Texts(projection, "floored"));
        Assert.Equal(["3", "-2"], ResultColumn.Texts(projection, "ceiled"));
    }

    [Theory]
    [InlineData(-2, "1200")]
    [InlineData(-4, "0")]
    [InlineData(-29, "0")]
    [InlineData(0, "1235")]
    [InlineData(1, "1234.5")]
    [InlineData(40, "1234.5")]
    public void RoundToDigitsRoundsToThatManyDecimalPlaces(long digits, string expected)
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDecimalNullable(
                                    "rounded",
                                    new DecimalNullableProjection(
                                        new RoundingDecimalNullableProjection(
                                            new RoundDecimalDigitsNullableProjection(
                                                new DecimalNullableProjection(
                                                    new FieldAsDecimalNullable(
                                                        new FieldDecimalNullable(
                                                            "t.numbers",
                                                            "m"
                                                        )
                                                    )
                                                ),
                                                new IntegerProjection(
                                                    new LiteralInteger(digits)
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal([expected, ""], ResultColumn.Texts(projection, "rounded"));
    }

    [Fact]
    public void LiftedArithmeticIsNullWhenAnOperandIsNull()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDecimalNullable(
                                    "product",
                                    new DecimalNullableProjection(
                                        new ArithmeticDecimalNullableProjection(
                                            new MultiplyDecimalNullableProjection(
                                                [
                                                    new DecimalNullableProjection(
                                                        new FieldAsDecimalNullable(
                                                            new FieldDecimalNullable(
                                                                "t.numbers",
                                                                "m"
                                                            )
                                                        )
                                                    ),
                                                    new DecimalNullableProjection(
                                                        new LiteralAsDecimalNullable(
                                                            new LiteralInteger(2)
                                                        )
                                                    ),
                                                ]
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionIntegerNullable(
                                    "quotient",
                                    new IntegerNullableProjection(
                                        new ArithmeticIntegerNullableProjection(
                                            new IntegerDivideIntegerNullableProjection(
                                                new IntegerNullableProjection(
                                                    new LiteralAsIntegerNullable(
                                                        new LiteralIntegerNullable()
                                                    )
                                                ),
                                                new IntegerNullableProjection(
                                                    new LiteralAsIntegerNullable(
                                                        new LiteralInteger(0)
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["2469", ""], ResultColumn.Texts(projection, "product"));
        Assert.Equal(["", ""], ResultColumn.Texts(projection, "quotient"));
    }

    [Fact]
    public void ConcatIsNullWhenAnOperandIsNull()
    {
        PureQLProjection projection = new(
            [Numbers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.numbers")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "label",
                                    new StringNullableProjection(
                                        new ConcatStringNullableProjection(
                                            [
                                                new StringNullableProjection(
                                                    new LiteralAsStringNullable(
                                                        new LiteralString("a")
                                                    )
                                                ),
                                                new StringNullableProjection(
                                                    new LiteralAsStringNullable(
                                                        new LiteralStringNullable()
                                                    )
                                                ),
                                            ]
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal([""], ResultColumn.Texts(projection, "label"));
    }
}
