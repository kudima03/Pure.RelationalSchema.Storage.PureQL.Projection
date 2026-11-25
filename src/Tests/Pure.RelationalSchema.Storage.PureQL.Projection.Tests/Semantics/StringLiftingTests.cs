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
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The string operators are lifted over every operand a row lists: the
// result is null when the operand is null and when an operand named by its
// role is (start, length, pattern, replacement, delimiter, index, fill,
// count), a field, a parameter or a literal alike, and an optional operand
// that is not written does not count. concatWith is not lifted: it skips
// its null operands and is never null.
public sealed class StringLiftingTests
{
    // Row 1 holds every value; rows 2 to 6 lack one each, in the order of
    // the columns; row 7 lacks them all.
    private static readonly IStoredSchemaDataSet Cells = new InlineTable(
        "t",
        "cells",
        ("id", new LongColumnType()),
        ("text", new StringColumnType()),
        ("part", new StringColumnType()),
        ("other", new StringColumnType()),
        ("two", new LongColumnType()),
        ("three", new LongColumnType())
    ).With(
        ["1", "banana", "an", "x", "2", "3"],
        ["2", "", "an", "x", "2", "3"],
        ["3", "banana", "", "x", "2", "3"],
        ["4", "banana", "an", "", "2", "3"],
        ["5", "banana", "an", "x", "", "3"],
        ["6", "banana", "an", "x", "2", ""],
        ["7", "", "", "", "", ""]
    );

    private static readonly FieldInteger Id = new("t.cells", "id");

    private static readonly FieldStringNullable Text = new("t.cells", "text");

    private static readonly FieldStringNullable Part = new("t.cells", "part");

    private static readonly FieldStringNullable Other = new("t.cells", "other");

    private static readonly FieldIntegerNullable Two = new("t.cells", "two");

    private static readonly FieldIntegerNullable Three = new("t.cells", "three");

    [Fact]
    public void ConcatIsNullWhenAnyOperandIs()
    {
        IsNullIn(
            new ConcatStringNullableProjection([Text, Part, Other]),
            "bananaanx",
            2,
            3,
            4,
            7
        );
        IsNullIn(new ConcatStringNullableProjection([Constant("a"), Part]), "aan", 3, 7);
    }

    [Fact]
    public void ConcatWithSkipsNullOperandsAndIsNeverNull()
    {
        PureQLProjection projection = Selected(
            new ConcatStringProjection([
                Constant("["),
                new ConcatWithProjection([Text, Part, Other], Constant("-")),
                Constant("]"),
            ])
        );

        Assert.Equal(
            [
                "[banana-an-x]",
                "[an-x]",
                "[banana-x]",
                "[banana-an]",
                "[banana-an-x]",
                "[banana-an-x]",
                "[]",
            ],
            ResultColumn.Texts(projection, "value")
        );
    }

    [Fact]
    public void LengthIsNullOfANullOperand()
    {
        IsNullIn(new LengthIntegerNullableProjection(Text), 6, 2, 7);
    }

    [Fact]
    public void TheCaseOperatorsAreNullOfANullOperand()
    {
        IsNullIn(new UpperStringNullableProjection(Text), "BANANA", 2, 7);
        IsNullIn(
            new LowerStringNullableProjection(new UpperStringNullableProjection(Text)),
            "banana",
            2,
            7
        );
        IsNullIn(
            new FoldCaseStringNullableProjection(new UpperStringNullableProjection(Text)),
            "banana",
            2,
            7
        );
    }

    [Theory]
    [InlineData(NormalizationForm.NFC)]
    [InlineData(NormalizationForm.NFD)]
    [InlineData(NormalizationForm.NFKC)]
    [InlineData(NormalizationForm.NFKD)]
    public void NormalizeIsNullOfANullOperand(NormalizationForm form)
    {
        IsNullIn(new NormalizeStringNullableProjection(Text, form), "banana", 2, 7);
    }

    [Fact]
    public void RemoveDiacriticsAndReverseAreNullOfANullOperand()
    {
        IsNullIn(new RemoveDiacriticsStringNullableProjection(Text), "banana", 2, 7);
        IsNullIn(new ReverseStringNullableProjection(Text), "ananab", 2, 7);
    }

    [Fact]
    public void TrimIsLiftedOverItsOperandOnly()
    {
        IsNullIn(
            new TrimStringNullableProjection(Text, [Constant("b"), Constant("a")]),
            "nan",
            2,
            7
        );
        IsNullIn(
            new TrimStartStringNullableProjection(Text, [Constant("b")]),
            "anana",
            2,
            7
        );
        IsNullIn(
            new TrimEndStringNullableProjection(Text, [Constant("a")]),
            "banan",
            2,
            7
        );
    }

    [Fact]
    public void SubstringIsNullWhenOperandStartOrLengthIs()
    {
        IsNullIn(
            new SubstringStringNullableProjection(Text, Number(2), Number(3)),
            "ana",
            2,
            7
        );
        IsNullIn(
            new SubstringStringNullableProjection(Constant("banana"), Two, Number(3)),
            "ana",
            5,
            7
        );
        IsNullIn(
            new SubstringStringNullableProjection(Constant("banana"), Number(2), Three),
            "ana",
            6,
            7
        );
        IsNullIn(
            new SubstringStringNullableProjection(Text, Two, Three),
            "ana",
            2,
            5,
            6,
            7
        );
    }

    [Fact]
    public void SubstringWithoutLengthIsLiftedOverOperandAndStartOnly()
    {
        IsNullIn(new SubstringStringNullableProjection(Text, Number(2)), "anana", 2, 7);
        IsNullIn(
            new SubstringStringNullableProjection(Constant("banana"), Two),
            "anana",
            5,
            7
        );
        IsNullIn(new SubstringStringNullableProjection(Text, Two), "anana", 2, 5, 7);
    }

    [Fact]
    public void ReplaceIsNullWhenOperandPatternOrReplacementIs()
    {
        IsNullIn(
            new ReplaceStringNullableProjection(Text, Constant("an"), Constant("x")),
            "bxxa",
            2,
            7
        );
        IsNullIn(
            new ReplaceStringNullableProjection(Constant("banana"), Part, Constant("x")),
            "bxxa",
            3,
            7
        );
        IsNullIn(
            new ReplaceStringNullableProjection(Constant("banana"), Constant("an"), Other),
            "bxxa",
            4,
            7
        );
        IsNullIn(
            new ReplaceStringNullableProjection(Text, Part, Other),
            "bxxa",
            2,
            3,
            4,
            7
        );
    }

    [Fact]
    public void PositionIsNullWhenOperandPatternOrStartIs()
    {
        IsNullIn(
            new PositionIntegerNullableProjection(Text, Constant("an"), Number(3)),
            4,
            2,
            7
        );
        IsNullIn(
            new PositionIntegerNullableProjection(Constant("banana"), Part, Number(3)),
            4,
            3,
            7
        );
        IsNullIn(
            new PositionIntegerNullableProjection(
                Constant("banana"),
                Constant("an"),
                Three
            ),
            4,
            6,
            7
        );
        IsNullIn(new PositionIntegerNullableProjection(Text, Part, Three), 4, 2, 3, 6, 7);
    }

    [Fact]
    public void PositionWithoutStartIsLiftedOverOperandAndPatternOnly()
    {
        IsNullIn(new PositionIntegerNullableProjection(Text, Constant("an")), 2, 2, 7);
        IsNullIn(new PositionIntegerNullableProjection(Constant("banana"), Part), 2, 3, 7);
        IsNullIn(new PositionIntegerNullableProjection(Text, Part), 2, 2, 3, 7);
    }

    [Fact]
    public void SplitPartIsNullWhenOperandDelimiterOrIndexIs()
    {
        IsNullIn(
            new SplitPartStringNullableProjection(Text, Constant("an"), Number(3)),
            "a",
            2,
            7
        );
        IsNullIn(
            new SplitPartStringNullableProjection(Constant("banana"), Part, Number(3)),
            "a",
            3,
            7
        );
        IsNullIn(
            new SplitPartStringNullableProjection(
                Constant("banana"),
                Constant("an"),
                Three
            ),
            "a",
            6,
            7
        );
        IsNullIn(
            new SplitPartStringNullableProjection(Text, Part, Three),
            "a",
            2,
            3,
            6,
            7
        );
    }

    [Fact]
    public void PadStartIsNullWhenOperandLengthOrFillIs()
    {
        IsNullIn(
            new PadStartStringNullableProjection(Part, Number(3), Constant("x")),
            "xan",
            3,
            7
        );
        IsNullIn(
            new PadStartStringNullableProjection(Constant("an"), Three, Constant("x")),
            "xan",
            6,
            7
        );
        IsNullIn(
            new PadStartStringNullableProjection(Constant("an"), Number(3), Other),
            "xan",
            4,
            7
        );
        IsNullIn(
            new PadStartStringNullableProjection(Part, Three, Other),
            "xan",
            3,
            4,
            6,
            7
        );
    }

    [Fact]
    public void PadEndIsNullWhenOperandLengthOrFillIs()
    {
        IsNullIn(
            new PadEndStringNullableProjection(Part, Number(3), Constant("x")),
            "anx",
            3,
            7
        );
        IsNullIn(
            new PadEndStringNullableProjection(Constant("an"), Three, Constant("x")),
            "anx",
            6,
            7
        );
        IsNullIn(
            new PadEndStringNullableProjection(Constant("an"), Number(3), Other),
            "anx",
            4,
            7
        );
        IsNullIn(
            new PadEndStringNullableProjection(Part, Three, Other),
            "anx",
            3,
            4,
            6,
            7
        );
    }

    [Fact]
    public void PaddingAStringThatIsLongEnoughIsStillNullOfANullFill()
    {
        IsNullIn(
            new PadStartStringNullableProjection(Constant("banana"), Number(3), Other),
            "banana",
            4,
            7
        );
        IsNullIn(
            new PadEndStringNullableProjection(Constant("banana"), Number(3), Other),
            "banana",
            4,
            7
        );
    }

    [Fact]
    public void RepeatIsNullWhenOperandOrCountIs()
    {
        IsNullIn(new RepeatStringNullableProjection(Part, Number(2)), "anan", 3, 7);
        IsNullIn(new RepeatStringNullableProjection(Constant("an"), Two), "anan", 5, 7);
        IsNullIn(new RepeatStringNullableProjection(Part, Two), "anan", 3, 5, 7);
    }

    [Fact]
    public void ReplaceRegexIsNullWhenOperandOrReplacementIs()
    {
        IsNullIn(
            new ReplaceRegexStringNullableProjection(Text, Constant("x"), Constant("an")),
            "bxxa",
            2,
            7
        );
        IsNullIn(
            new ReplaceRegexStringNullableProjection(
                Constant("banana"),
                Other,
                Constant("an")
            ),
            "bxxa",
            4,
            7
        );
        IsNullIn(
            new ReplaceRegexStringNullableProjection(Text, Other, Constant("an")),
            "bxxa",
            2,
            4,
            7
        );
    }

    [Theory]
    [InlineData("concat")]
    [InlineData("length")]
    [InlineData("upper")]
    [InlineData("lower")]
    [InlineData("foldCase")]
    [InlineData("normalize")]
    [InlineData("removeDiacritics")]
    [InlineData("reverse")]
    [InlineData("trim")]
    [InlineData("trimStart")]
    [InlineData("trimEnd")]
    [InlineData("substring operand")]
    [InlineData("substring start")]
    [InlineData("substring length")]
    [InlineData("replace operand")]
    [InlineData("replace pattern")]
    [InlineData("replace replacement")]
    [InlineData("position operand")]
    [InlineData("position pattern")]
    [InlineData("position start")]
    [InlineData("splitPart operand")]
    [InlineData("splitPart delimiter")]
    [InlineData("splitPart index")]
    [InlineData("padStart operand")]
    [InlineData("padStart length")]
    [InlineData("padStart fill")]
    [InlineData("padEnd operand")]
    [InlineData("padEnd length")]
    [InlineData("padEnd fill")]
    [InlineData("repeat operand")]
    [InlineData("repeat count")]
    [InlineData("replaceRegex operand")]
    [InlineData("replaceRegex replacement")]
    public void ALiftedOperatorOverANullLiteralIsNull(string operand)
    {
        Assert.Equal(
            [.. Enumerable.Repeat("NULL", 7)],
            ResultColumn.Texts(Selected(OverANullLiteral(operand)), "value")
        );
    }

    [Fact]
    public void ALiftedOperatorIsNullOfANullParameter()
    {
        Dictionary<string, IParameter> missing = new()
        {
            ["count"] = new NullableIntegerParameter(),
            ["fill"] = new NullableStringParameter(),
        };

        Assert.Equal(
            ["NULL", "NULL", "NULL", "NULL"],
            RoleParameters(missing)
        );
    }

    [Fact]
    public void ALiftedOperatorReadsItsRoleOperandsFromParameters()
    {
        Dictionary<string, IParameter> bound = new()
        {
            ["count"] = new IntegerParameter(new Long(2)),
            ["fill"] = new StringParameter(new String("an")),
        };

        Assert.Equal(["abab", "anana", "an7", "ba"], RoleParameters(bound));
    }

    [Fact]
    public void ALiftedOperatorInWhereIsNullOfANullOperand()
    {
        PureQLProjection projection = new(
            [Cells],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.cells"),
                where: new EqualStringRow(
                    new SubstringStringNullableRow(Text, Two, Three),
                    new LiteralStringNullable()
                ),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(["2", "5", "6", "7"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void ALiftedOperatorOverANullGroupKeyIsNull()
    {
        PureQLProjection projection = new(
            [Cells],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.cells"),
                groupBy: [new GroupKey("part", Part)],
                select:
                [
                    new SelectItemGroup(
                        "padded",
                        new CoalesceStringGroup([
                            new PadEndStringNullableGroup(
                                new KeyStringNullable("part"),
                                Number(4),
                                Constant("!")
                            ),
                            Constant("NULL"),
                        ])
                    ),
                    new SelectItemGroup("rows", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("rows")]
            )
        );

        Assert.Equal(["NULL", "an!!"], ResultColumn.Texts(projection, "padded"));
        Assert.Equal(["2", "5"], ResultColumn.Texts(projection, "rows"));
    }

    [Theory]
    [InlineData("x", "X")]
    [InlineData("none", "NULL")]
    public void ALiftedOperatorOverAnAggregateOfATotalQueryIsNullOverNoRows(
        string other,
        string expected
    )
    {
        PureQLProjection projection = new(
            [Cells],
            new MainTotalQuery(
                from: new FromEntity("t.cells"),
                aggregate:
                [
                    new SelectItemTotal(
                        "upper",
                        new CoalesceStringTotal([
                            new UpperStringNullableTotal(
                                new MinStringNullableTotal(
                                    Other,
                                    new EqualStringRow(Other, Constant(other))
                                )
                            ),
                            Constant("NULL"),
                        ])
                    ),
                ]
            )
        );

        Assert.Equal([expected], ResultColumn.Texts(projection, "upper"));
    }

    private static LiteralString Constant(string value)
    {
        return new LiteralString(value);
    }

    private static LiteralInteger Number(long value)
    {
        return new LiteralInteger(value);
    }

    // An operator with a null literal as the named operand and literals as
    // the others, with NULL written for a null result.
    private static ExprProjection OverANullLiteral(string operand)
    {
        LiteralStringNullable text = new();
        LiteralIntegerNullable number = new();
        LiteralString banana = Constant("banana");

        if (operand is "length")
        {
            return new CoalesceStringProjection([
                new AsStringDecimalStringNullableProjection(
                    new LengthIntegerNullableProjection(text)
                ),
                Constant("NULL"),
            ]);
        }

        if (operand.StartsWith("position", StringComparison.Ordinal))
        {
            return new CoalesceStringProjection([
                new AsStringDecimalStringNullableProjection(
                    operand switch
                    {
                        "position operand" => new PositionIntegerNullableProjection(
                            text,
                            Constant("an")
                        ),
                        "position pattern" => new PositionIntegerNullableProjection(
                            banana,
                            text
                        ),
                        _ => new PositionIntegerNullableProjection(
                            banana,
                            Constant("an"),
                            number
                        ),
                    }
                ),
                Constant("NULL"),
            ]);
        }

        StringNullableProjection expression = operand switch
        {
            "concat" => new ConcatStringNullableProjection([Constant("a"), text]),
            "upper" => new UpperStringNullableProjection(text),
            "lower" => new LowerStringNullableProjection(text),
            "foldCase" => new FoldCaseStringNullableProjection(text),
            "normalize" => new NormalizeStringNullableProjection(
                text,
                NormalizationForm.NFC
            ),
            "removeDiacritics" => new RemoveDiacriticsStringNullableProjection(text),
            "reverse" => new ReverseStringNullableProjection(text),
            "trim" => new TrimStringNullableProjection(text, [Constant(" ")]),
            "trimStart" => new TrimStartStringNullableProjection(text, [Constant(" ")]),
            "trimEnd" => new TrimEndStringNullableProjection(text, [Constant(" ")]),
            "substring operand" => new SubstringStringNullableProjection(text, Number(1)),
            "substring start" => new SubstringStringNullableProjection(banana, number),
            "substring length" => new SubstringStringNullableProjection(
                banana,
                Number(1),
                number
            ),
            "replace operand" => new ReplaceStringNullableProjection(
                text,
                Constant("an"),
                Constant("x")
            ),
            "replace pattern" => new ReplaceStringNullableProjection(
                banana,
                text,
                Constant("x")
            ),
            "replace replacement" => new ReplaceStringNullableProjection(
                banana,
                Constant("an"),
                text
            ),
            "splitPart operand" => new SplitPartStringNullableProjection(
                text,
                Constant("an"),
                Number(1)
            ),
            "splitPart delimiter" => new SplitPartStringNullableProjection(
                banana,
                text,
                Number(1)
            ),
            "splitPart index" => new SplitPartStringNullableProjection(
                banana,
                Constant("an"),
                number
            ),
            "padStart operand" => new PadStartStringNullableProjection(
                text,
                Number(8),
                Constant("0")
            ),
            "padStart length" => new PadStartStringNullableProjection(
                banana,
                number,
                Constant("0")
            ),
            "padStart fill" => new PadStartStringNullableProjection(
                banana,
                Number(8),
                text
            ),
            "padEnd operand" => new PadEndStringNullableProjection(
                text,
                Number(8),
                Constant("0")
            ),
            "padEnd length" => new PadEndStringNullableProjection(
                banana,
                number,
                Constant("0")
            ),
            "padEnd fill" => new PadEndStringNullableProjection(banana, Number(8), text),
            "repeat operand" => new RepeatStringNullableProjection(text, Number(2)),
            "repeat count" => new RepeatStringNullableProjection(banana, number),
            "replaceRegex operand" => new ReplaceRegexStringNullableProjection(
                text,
                Constant("x"),
                Constant("an")
            ),
            _ => new ReplaceRegexStringNullableProjection(
                banana,
                text,
                Constant("an")
            ),
        };

        return OrNull(expression);
    }

    // The results of four operators whose role operand is a nullable
    // parameter, over the first row.
    private static IReadOnlyList<string> RoleParameters(
        Dictionary<string, IParameter> parameters
    )
    {
        PureQLProjection projection = new(
            [Cells],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.cells"),
                where: new EqualDecimalRow(Id, Number(1)),
                select:
                [
                    new SelectItemProjection(
                        "repeat",
                        OrNull(
                            new RepeatStringNullableProjection(
                                Constant("ab"),
                                new ParamIntegerNullable("count")
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "substring",
                        OrNull(
                            new SubstringStringNullableProjection(
                                Constant("banana"),
                                new ParamIntegerNullable("count")
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "padEnd",
                        OrNull(
                            new PadEndStringNullableProjection(
                                Constant("an"),
                                Number(3),
                                new ConcatStringNullableProjection([
                                    Constant("7"),
                                    new ParamStringNullable("fill"),
                                ])
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "replace",
                        OrNull(
                            new ReplaceStringNullableProjection(
                                Constant("banana"),
                                new ParamStringNullable("fill"),
                                Constant("")
                            )
                        )
                    ),
                ]
            ),
            parameters
        );

        return
        [
            Assert.Single(ResultColumn.Texts(projection, "repeat")),
            Assert.Single(ResultColumn.Texts(projection, "substring")),
            Assert.Single(ResultColumn.Texts(projection, "padEnd")),
            Assert.Single(ResultColumn.Texts(projection, "replace")),
        ];
    }

    // A string expression is null in exactly the listed rows and has the
    // given value in every other.
    private static void IsNullIn(
        StringNullableProjection expression,
        string value,
        params int[] rows
    )
    {
        Assert.Equal(
            [.. Enumerable.Range(1, 7).Select(row => rows.Contains(row) ? "NULL" : value)],
            ResultColumn.Texts(Selected(OrNull(expression)), "value")
        );
    }

    // The same for an integer expression, which is never negative here.
    private static void IsNullIn(
        IntegerNullableProjection expression,
        long value,
        params int[] rows
    )
    {
        Assert.Equal(
            [
                .. Enumerable
                    .Range(1, 7)
                    .Select(row => rows.Contains(row) ? "-1" : $"{value}"),
            ],
            ResultColumn.Texts(
                Selected(new CoalesceIntegerProjection([expression, Number(-1)])),
                "value"
            )
        );
    }

    // A null is written as the text NULL: a result cell holds the empty text
    // for a null and for an empty string alike.
    private static CoalesceStringProjection OrNull(StringNullableProjection expression)
    {
        return new CoalesceStringProjection([expression, Constant("NULL")]);
    }

    private static PureQLProjection Selected(ExprProjection expression)
    {
        return new PureQLProjection(
            [Cells],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.cells"),
                select: [new SelectItemProjection("value", expression)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );
    }
}
