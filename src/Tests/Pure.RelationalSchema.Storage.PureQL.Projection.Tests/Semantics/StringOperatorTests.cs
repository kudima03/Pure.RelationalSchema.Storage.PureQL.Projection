using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The string operators as the specification defines them under Strings: a
// string is a sequence of code points, so lengths and positions count code
// points from 1 (a character outside the Basic Multilingual Plane is one, a
// combining sequence one per code point), text is matched code point by
// code point, case is mapped by the full Unicode mappings without a locale,
// and nothing reads past a string. Each example of the specification is a
// test.
public sealed class StringOperatorTests
{
    private static readonly InlineTable Texts = new(
        "t",
        "texts",
        ("text", new StringColumnType())
    );

    private static readonly InlineTable Affixes = new(
        "t",
        "affixes",
        ("id", new LongColumnType()),
        ("text", new StringColumnType()),
        ("affix", new StringColumnType()),
        ("start", new LongColumnType())
    );

    [Fact]
    public void ConcatJoinsItsOperandsUnchanged()
    {
        Yields(
            " aB  c\U0001F600",
            " a",
            text => new ConcatStringProjection([
                text,
                Text("B "),
                Text(""),
                Text(" c\U0001F600"),
            ])
        );
    }

    [Fact]
    public void ConcatWithLeavesOutTheNullOperands()
    {
        Yields(
            "Ann Lee",
            "Ann",
            text => new ConcatWithProjection(
                [text, new LiteralStringNullable(), Text("Lee")],
                Text(" ")
            )
        );
    }

    [Fact]
    public void ConcatWithWritesNoSeparatorAroundANullOperand()
    {
        Yields(
            "a-b",
            "a",
            text => new ConcatWithProjection(
                [
                    new LiteralStringNullable(),
                    text,
                    new LiteralStringNullable(),
                    Text("b"),
                    new LiteralStringNullable(),
                ],
                Text("-")
            )
        );
    }

    [Fact]
    public void ConcatWithOfOnlyNullOperandsIsTheEmptyString()
    {
        Yields(
            "[]",
            "[",
            text => new ConcatStringProjection([
                text,
                new ConcatWithProjection(
                    [new LiteralStringNullable(), new LiteralStringNullable()],
                    Text("-")
                ),
                Text("]"),
            ])
        );
    }

    [Fact]
    public void ConcatWithKeepsAnEmptyStringAsAnOperand()
    {
        Yields(
            "a--b",
            "a",
            text => new ConcatWithProjection([text, Text(""), Text("b")], Text("-"))
        );
    }

    [Fact]
    public void ConcatWithTakesAnEmptySeparator()
    {
        Yields(
            "abc",
            "a",
            text => new ConcatWithProjection(
                [text, new LiteralStringNullable(), Text("b"), Text("c")],
                Text("")
            )
        );
    }

    [Fact]
    public void ConcatWithTakesASeparatorComputedForTheRow()
    {
        Yields(
            "a, b",
            ", ",
            separator => new ConcatWithProjection([Text("a"), Text("b")], separator)
        );
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("hello", "5")]
    [InlineData("a b ", "4")]
    [InlineData("\U0001F600", "1")]
    [InlineData("a\U0001F600b", "3")]
    [InlineData("é", "2")]
    [InlineData("é", "1")]
    public void LengthIsTheNumberOfCodePoints(string text, string expected)
    {
        Yields(expected, text, operand => new LengthIntegerProjection(operand));
    }

    [Theory]
    [InlineData("straße", "STRASSE")]
    [InlineData("i", "I")]
    [InlineData("ﬁ", "FI")]
    [InlineData("οδος", "ΟΔΟΣ")]
    [InlineData("1-你 \U0001F600", "1-你 \U0001F600")]
    [InlineData("", "")]
    public void UpperAppliesTheFullMappingsWithoutALocale(string text, string expected)
    {
        Yields(expected, text, operand => new UpperStringProjection(operand));
    }

    [Theory]
    [InlineData("ΟΔΟΣ", "οδος")]
    [InlineData("I", "i")]
    [InlineData("Straße", "straße")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("1-你 \U0001F600", "1-你 \U0001F600")]
    [InlineData("", "")]
    public void LowerAppliesTheFullMappingsWithoutALocale(string text, string expected)
    {
        Yields(expected, text, operand => new LowerStringProjection(operand));
    }

    [Fact]
    public void UpperAndLowerAreTheSameUnderATurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            Yields("I", "i", operand => new UpperStringProjection(operand));
            Yields("i", "I", operand => new LowerStringProjection(operand));
            Yields("i", "I", operand => new FoldCaseStringProjection(operand));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData("Straße", "strasse")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("ΟΔΟΣ", "οδοσ")]
    [InlineData("οδος", "οδοσ")]
    [InlineData("I", "i")]
    [InlineData("ı", "ı")]
    [InlineData("İ", "i̇")]
    [InlineData("ﬁ", "fi")]
    [InlineData("É", "é")]
    [InlineData("É", "é")]
    [InlineData("1-你 \U0001F600", "1-你 \U0001F600")]
    public void FoldCaseAppliesTheFullCaseFoldingWithoutALocale(
        string text,
        string expected
    )
    {
        Yields(expected, text, operand => new FoldCaseStringProjection(operand));
    }

    [Theory]
    [InlineData("Straße", "strasse")]
    [InlineData("ΟΔΟΣ", "οδοσ")]
    [InlineData("İ", "i̇")]
    public void FoldingAFoldedStringChangesNothing(string text, string expected)
    {
        Yields(
            expected,
            text,
            operand => new FoldCaseStringProjection(new FoldCaseStringProjection(operand))
        );
    }

    [Theory]
    [InlineData("é", NormalizationForm.NFC, "é")]
    [InlineData("é", NormalizationForm.NFC, "é")]
    [InlineData("ﬁ²", NormalizationForm.NFC, "ﬁ²")]
    [InlineData("é", NormalizationForm.NFD, "é")]
    [InlineData("é", NormalizationForm.NFD, "é")]
    [InlineData("ﬁ²", NormalizationForm.NFD, "ﬁ²")]
    [InlineData("ﬁ", NormalizationForm.NFKC, "fi")]
    [InlineData("ｆ", NormalizationForm.NFKC, "f")]
    [InlineData("²", NormalizationForm.NFKC, "2")]
    [InlineData("ﬁé", NormalizationForm.NFKC, "fié")]
    [InlineData("ﬁ", NormalizationForm.NFKD, "fi")]
    [InlineData("ｆ²", NormalizationForm.NFKD, "f2")]
    [InlineData("ﬁé", NormalizationForm.NFKD, "fié")]
    public void NormalizeReturnsTheOperandInTheNamedForm(
        string text,
        NormalizationForm form,
        string expected
    )
    {
        Yields(expected, text, operand => new NormalizeStringProjection(operand, form));
    }

    [Fact]
    public void NormalizeMayChangeTheLength()
    {
        Yields(
            "2",
            "é",
            operand => new LengthIntegerProjection(
                new NormalizeStringProjection(operand, NormalizationForm.NFD)
            )
        );
    }

    [Theory]
    [InlineData(NormalizationForm.NFC, "éfi")]
    [InlineData(NormalizationForm.NFD, "éﬁ")]
    [InlineData(NormalizationForm.NFKC, "éfi")]
    [InlineData(NormalizationForm.NFKD, "éfi")]
    public void NormalizingANormalizedStringChangesNothing(
        NormalizationForm form,
        string expected
    )
    {
        string text = form is NormalizationForm.NFC or NormalizationForm.NFD
            ? "éﬁ"
            : "éfi";

        Yields(
            expected,
            form is NormalizationForm.NFC ? "éfi" : text,
            operand => new NormalizeStringProjection(
                new NormalizeStringProjection(operand, form),
                form
            )
        );
    }

    [Theory]
    [InlineData("É", "é")]
    [InlineData("É", "é")]
    public void FoldingThenNormalizingGivesOneKeyForEitherEncoding(
        string text,
        string expected
    )
    {
        Yields(
            expected,
            text,
            operand => new NormalizeStringProjection(
                new FoldCaseStringProjection(operand),
                NormalizationForm.NFC
            )
        );
    }

    [Theory]
    [InlineData("Crème Brûlée", "Creme Brulee")]
    [InlineData("Việt Nam", "Viet Nam")]
    [InlineData("άλφα", "αλφα")]
    [InlineData("Łódź", "Łodz")]
    [InlineData("øłđæß", "øłđæß")]
    [InlineData("й", "и")]
    [InlineData(
        "שָׁלוֹם",
        "שלום"
    )]
    [InlineData("École", "Ecole")]
    [InlineData("Crème", "Creme")]
    [InlineData("plain \U0001F600", "plain \U0001F600")]
    public void RemoveDiacriticsDropsEveryNonspacingMark(string text, string expected)
    {
        Yields(expected, text, operand => new RemoveDiacriticsStringProjection(operand));
    }

    [Theory]
    [InlineData("한", "한")]
    [InlineData("Ångström Å", "Angstrom A")]
    public void RemoveDiacriticsAlwaysGivesTheComposedForm(string text, string expected)
    {
        Yields(expected, text, operand => new RemoveDiacriticsStringProjection(operand));
    }

    [Theory]
    [InlineData("Crème Brûlée", "Creme Brulee")]
    [InlineData("Łódź", "Łodz")]
    public void RemovingDiacriticsTwiceChangesNothing(string text, string expected)
    {
        Yields(
            expected,
            text,
            operand => new RemoveDiacriticsStringProjection(
                new RemoveDiacriticsStringProjection(operand)
            )
        );
    }

    [Theory]
    [InlineData("000120", "120")]
    [InlineData("0", "")]
    [InlineData("120", "120")]
    [InlineData("1200", "1200")]
    public void TrimStartRemovesFromTheStartOnly(string text, string expected)
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new TrimStartStringProjection(operand, [Text("0")]))
        );
    }

    [Fact]
    public void TrimEndRemovesFromTheEndUntilNoRemovableMatches()
    {
        Yields(
            "12.5",
            "12.50 USD",
            operand => new TrimEndStringProjection(
                operand,
                [Text(" USD"), Text("0"), Text(".")]
            )
        );
    }

    [Theory]
    [InlineData("a", "ab")]
    [InlineData("ab", "a")]
    public void TrimTakesTheLongestRemovableFirstWhateverTheirOrder(
        string first,
        string second
    )
    {
        Yields(
            "[]",
            "abab",
            operand => Bracketed(
                new TrimStartStringProjection(operand, [Text(first), Text(second)])
            )
        );
        Yields(
            "[]",
            "baba",
            operand => Bracketed(
                new TrimEndStringProjection(
                    operand,
                    [Text(Reversed(first)), Text(Reversed(second))]
                )
            )
        );
    }

    [Fact]
    public void ARepeatedRemovableChangesNothing()
    {
        Yields(
            "120",
            "000120",
            operand => new TrimStartStringProjection(operand, [Text("0"), Text("0")])
        );
    }

    [Theory]
    [InlineData("xxaxx", "axx", "xxa", "a")]
    [InlineData("xyxhi thereyx", "hi thereyx", "xyxhi there", "hi there")]
    [InlineData("xx", "", "", "")]
    [InlineData("none", "none", "none", "none")]
    public void TrimRemovesFromBothEnds(
        string text,
        string start,
        string end,
        string both
    )
    {
        Yields(
            Bracketed(start),
            text,
            operand => Bracketed(
                new TrimStartStringProjection(operand, [Text("x"), Text("y")])
            )
        );
        Yields(
            Bracketed(end),
            text,
            operand => Bracketed(
                new TrimEndStringProjection(operand, [Text("x"), Text("y")])
            )
        );
        Yields(
            Bracketed(both),
            text,
            operand => Bracketed(
                new TrimStringProjection(operand, [Text("x"), Text("y")])
            )
        );
    }

    [Fact]
    public void TrimIsTrimStartThenTrimEndOfTheResult()
    {
        Yields(
            "b",
            "abab",
            operand => new TrimStringProjection(operand, [Text("aba"), Text("bab")])
        );
    }

    [Fact]
    public void TrimRemovesOnlyTheWhiteSpaceItLists()
    {
        Yields(
            "\t a b ",
            "  \t a b  ",
            operand => new TrimStringProjection(operand, [Text(" ")])
        );
        Yields(
            "a b",
            "  \t a b  ",
            operand => new TrimStringProjection(
                operand,
                [Text(" "), Text("\t"), Text(" ")]
            )
        );
    }

    [Fact]
    public void AnEmptyRemovableNeverMatches()
    {
        Yields(
            "b",
            "aab",
            operand => new TrimStringProjection(operand, [Text(""), Text("a")])
        );
        Yields("abc", "abc", operand => new TrimStringProjection(operand, [Text("")]));
        Yields(
            "abc",
            "abc",
            operand => new TrimStartStringProjection(operand, [Text(""), Text("")])
        );
        Yields("abc", "abc", operand => new TrimEndStringProjection(operand, [Text("")]));
    }

    [Fact]
    public void ARemovableIsMatchedCodePointByCodePoint()
    {
        Yields("Aa", "aAa", operand => new TrimStartStringProjection(operand, [Text("a")]));
        Yields(
            "\U0001F601x\U0001F601",
            "\U0001F600\U0001F601x\U0001F601\U0001F600",
            operand => new TrimStringProjection(operand, [Text("\U0001F600")])
        );
        Yields(
            "été",
            "été",
            operand => new TrimStringProjection(operand, [Text("e"), Text("́")])
        );
    }

    [Fact]
    public void TrimTakesRemovablesComputedForTheRow()
    {
        PureQLProjection projection = new(
            [
                Affixes.With(
                    ["1", "xxhixx", "x", "1"],
                    ["2", "--hi--", "-", "1"],
                    ["3", "hi", "", "1"],
                    ["4", "ABhiab", "ab", "1"]
                ),
            ],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.affixes"),
                select:
                [
                    new SelectItemProjection(
                        "trimmed",
                        new TrimStringProjection(
                            new FieldString("t.affixes", "text"),
                            [
                                new FieldString("t.affixes", "affix"),
                                new UpperStringProjection(
                                    new FieldString("t.affixes", "affix")
                                ),
                            ]
                        )
                    ),
                    new SelectItemProjection(
                        "start",
                        new TrimStartStringProjection(
                            new FieldString("t.affixes", "text"),
                            [new FieldString("t.affixes", "affix")]
                        )
                    ),
                    new SelectItemProjection(
                        "end",
                        new TrimEndStringProjection(
                            new FieldString("t.affixes", "text"),
                            [new FieldString("t.affixes", "affix")]
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.affixes", "id")),
                ]
            )
        );

        Assert.Equal(["hi", "hi", "hi", "hi"], ResultColumn.Texts(projection, "trimmed"));
        Assert.Equal(
            ["hixx", "hi--", "hi", "ABhiab"],
            ResultColumn.Texts(projection, "start")
        );
        Assert.Equal(["xxhi", "--hi", "hi", "ABhi"], ResultColumn.Texts(projection, "end"));
    }

    [Theory]
    [InlineData("hello", 4, 10, "lo")]
    [InlineData("hello", 2, 0, "")]
    [InlineData("hello", 1, 3, "hel")]
    [InlineData("hi", 1, 5, "hi")]
    [InlineData("hello", 2, 3, "ell")]
    [InlineData("hello", 5, 1, "o")]
    [InlineData("hello", 6, 1, "")]
    [InlineData("hello", 100, 2, "")]
    [InlineData("", 1, 3, "")]
    [InlineData("a\U0001F600b", 2, 1, "\U0001F600")]
    [InlineData("a\U0001F600b", 1, 2, "a\U0001F600")]
    [InlineData("\U0001F600\U0001F601\U0001F602", 2, 1, "\U0001F601")]
    [InlineData("éx", 2, 1, "́")]
    public void SubstringReturnsLengthCodePointsFromStart(
        string text,
        long start,
        long length,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                new SubstringStringProjection(operand, Number(start), Number(length))
            )
        );
    }

    [Theory]
    [InlineData("hello", 1, "hello")]
    [InlineData("hello", 2, "ello")]
    [InlineData("hello", 5, "o")]
    [InlineData("hello", 6, "")]
    [InlineData("hello", 100, "")]
    [InlineData("", 1, "")]
    [InlineData("a\U0001F600b", 3, "b")]
    [InlineData("\U0001F600\U0001F601\U0001F602", 2, "\U0001F601\U0001F602")]
    public void SubstringWithoutLengthReturnsTheRestOfTheString(
        string text,
        long start,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new SubstringStringProjection(operand, Number(start)))
        );
    }

    [Fact]
    public void SubstringTakesAStartAndALengthComputedForTheRow()
    {
        PureQLProjection projection = new(
            [
                Affixes.With(
                    ["1", "hello", "", "1"],
                    ["2", "hello", "", "2"],
                    ["3", "a\U0001F600bcd", "", "3"]
                ),
            ],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.affixes"),
                select:
                [
                    new SelectItemProjection(
                        "part",
                        new SubstringStringProjection(
                            new FieldString("t.affixes", "text"),
                            new FieldInteger("t.affixes", "start"),
                            new AddIntegerProjection([
                                new FieldInteger("t.affixes", "start"),
                                Number(1),
                            ])
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.affixes", "id")),
                ]
            )
        );

        Assert.Equal(["he", "ell", "bcd"], ResultColumn.Texts(projection, "part"));
    }

    [Theory]
    [InlineData("aaa", "aa", "b", "ba")]
    [InlineData("a", "a", "aa", "aa")]
    [InlineData("banana", "an", "", "ba")]
    [InlineData("abc", "", "x", "abc")]
    [InlineData("", "a", "x", "")]
    [InlineData("Aa", "a", "x", "Ax")]
    [InlineData("é", "e", "x", "é")]
    [InlineData("é", "e", "x", "x́")]
    [InlineData("a.c abc", ".", "x", "axc abc")]
    [InlineData("50% off, 5% back", "%", " percent", "50 percent off, 5 percent back")]
    [InlineData("a\U0001F600b\U0001F601", "\U0001F600", "-", "a-b\U0001F601")]
    [InlineData("abc", "abc", "", "")]
    public void ReplaceReplacesEveryOccurrenceOfTheLiteralPattern(
        string text,
        string pattern,
        string replacement,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                new ReplaceStringProjection(operand, Text(pattern), Text(replacement))
            )
        );
    }

    [Theory]
    [InlineData("banana", "an", "2")]
    [InlineData("banana", "x", "0")]
    [InlineData("banana", "b", "1")]
    [InlineData("banana", "banana", "1")]
    [InlineData("banana", "bananas", "0")]
    [InlineData("banana", "A", "0")]
    [InlineData("banana", "", "1")]
    [InlineData("", "", "1")]
    [InlineData("", "x", "0")]
    [InlineData("a\U0001F600b", "b", "3")]
    [InlineData("\U0001F600\U0001F601", "\U0001F601", "2")]
    [InlineData("é", "́", "2")]
    [InlineData("é", "e", "0")]
    public void PositionIsTheFirstOccurrenceCountedInCodePointsFromOne(
        string text,
        string pattern,
        string expected
    )
    {
        Yields(
            expected,
            text,
            operand => new PositionIntegerProjection(operand, Text(pattern))
        );
    }

    [Theory]
    [InlineData("banana", "an", 3, "4")]
    [InlineData("banana", "an", 1, "2")]
    [InlineData("banana", "an", 2, "2")]
    [InlineData("banana", "an", 4, "4")]
    [InlineData("banana", "an", 5, "0")]
    [InlineData("banana", "a", 6, "6")]
    [InlineData("banana", "a", 7, "0")]
    [InlineData("banana", "a", 100, "0")]
    [InlineData("banana", "", 3, "3")]
    [InlineData("\U0001F600\U0001F600b", "b", 3, "3")]
    [InlineData("\U0001F600ab\U0001F600ab", "a", 3, "5")]
    public void PositionWithStartSearchesFromThatPosition(
        string text,
        string pattern,
        long start,
        string expected
    )
    {
        Yields(
            expected,
            text,
            operand => new PositionIntegerProjection(operand, Text(pattern), Number(start))
        );
    }

    [Theory]
    [InlineData("a-b-c", "-", 2, "b")]
    [InlineData("a-b-c", "-", -1, "c")]
    [InlineData("a-b-c", "-", 1, "a")]
    [InlineData("a-b-c", "-", 3, "c")]
    [InlineData("a-b-c", "-", -2, "b")]
    [InlineData("a-b-c", "-", -3, "a")]
    [InlineData("a-b-c", "-", 5, "")]
    [InlineData("a-b-c", "-", -5, "")]
    [InlineData("a--c", "-", 2, "")]
    [InlineData("-a-", "-", 1, "")]
    [InlineData("-a-", "-", 3, "")]
    [InlineData("-a-", "-", 2, "a")]
    [InlineData("abc", "", 1, "abc")]
    [InlineData("abc", "", -1, "abc")]
    [InlineData("abc", "", 2, "")]
    [InlineData("abc", "-", 1, "abc")]
    [InlineData("", "-", 1, "")]
    [InlineData("a, b, c", ", ", 2, "b")]
    [InlineData("aaa", "aa", 1, "")]
    [InlineData("aaa", "aa", 2, "a")]
    [InlineData("aaa", "aa", 3, "")]
    [InlineData("a\U0001F600b\U0001F601c", "\U0001F600", 2, "b\U0001F601c")]
    [InlineData("A-b", "a", 1, "A-b")]
    public void SplitPartReturnsThePartNumberedIndex(
        string text,
        string delimiter,
        long index,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                new SplitPartStringProjection(operand, Text(delimiter), Number(index))
            )
        );
    }

    [Theory]
    [InlineData("7", 3, "0", "007")]
    [InlineData("abc", 6, "xy", "xyxabc")]
    [InlineData("abc", 7, "xy", "xyxyabc")]
    [InlineData("abcdef", 3, "0", "abcdef")]
    [InlineData("abc", 3, "0", "abc")]
    [InlineData("abc", 0, "0", "abc")]
    [InlineData("abc", -5, "0", "abc")]
    [InlineData("7", 3, "", "7")]
    [InlineData("", 2, "ab", "ab")]
    [InlineData("", 0, "ab", "")]
    [InlineData("\U0001F600", 3, "x", "xx\U0001F600")]
    [InlineData("a", 4, "\U0001F600x", "\U0001F600x\U0001F600a")]
    [InlineData("é", 3, "0", "0é")]
    public void PadStartAddsTheFillBeforeUntilTheLengthInCodePoints(
        string text,
        long length,
        string fill,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                new PadStartStringProjection(operand, Number(length), Text(fill))
            )
        );
    }

    [Theory]
    [InlineData("7", 3, "0", "700")]
    [InlineData("abc", 6, "xy", "abcxyx")]
    [InlineData("abc", 7, "xy", "abcxyxy")]
    [InlineData("abcdef", 3, "0", "abcdef")]
    [InlineData("abc", 3, "0", "abc")]
    [InlineData("abc", 0, "0", "abc")]
    [InlineData("abc", -5, "0", "abc")]
    [InlineData("7", 3, "", "7")]
    [InlineData("", 2, "ab", "ab")]
    [InlineData("\U0001F600", 3, "x", "\U0001F600xx")]
    [InlineData("a", 4, "\U0001F600x", "a\U0001F600x\U0001F600")]
    public void PadEndAddsTheFillAfterUntilTheLengthInCodePoints(
        string text,
        long length,
        string fill,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                new PadEndStringProjection(operand, Number(length), Text(fill))
            )
        );
    }

    [Theory]
    [InlineData("ab", 3, "ababab")]
    [InlineData("ab", 1, "ab")]
    [InlineData("ab", 0, "")]
    [InlineData("", 5, "")]
    [InlineData("\U0001F600-", 2, "\U0001F600-\U0001F600-")]
    public void RepeatWritesTheOperandCountTimes(string text, long count, string expected)
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new RepeatStringProjection(operand, Number(count)))
        );
    }

    [Theory]
    [InlineData("abc", "cba")]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("éx", "x́e")]
    [InlineData("a\U0001F600b", "b\U0001F600a")]
    [InlineData("\U0001F600\U0001F601", "\U0001F601\U0001F600")]
    public void ReverseReturnsTheCodePointsInTheOppositeOrder(string text, string expected)
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new ReverseStringProjection(operand))
        );
    }

    [Fact]
    public void PositionAndPaddingTakeOperandsReadFromTheRow()
    {
        PureQLProjection projection = new(
            [
                Affixes.With(
                    ["1", "banana", "an", "3"],
                    ["2", "banana", "na", "4"],
                    ["3", "a-b-c", "-", "2"]
                ),
            ],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.affixes"),
                select:
                [
                    new SelectItemProjection(
                        "position",
                        new PositionIntegerProjection(
                            new FieldString("t.affixes", "text"),
                            new FieldString("t.affixes", "affix"),
                            new FieldInteger("t.affixes", "start")
                        )
                    ),
                    new SelectItemProjection(
                        "part",
                        new SplitPartStringProjection(
                            new FieldString("t.affixes", "text"),
                            new FieldString("t.affixes", "affix"),
                            new FieldInteger("t.affixes", "start")
                        )
                    ),
                    new SelectItemProjection(
                        "padded",
                        new PadEndStringProjection(
                            new FieldString("t.affixes", "affix"),
                            new FieldInteger("t.affixes", "start"),
                            new FieldString("t.affixes", "text")
                        )
                    ),
                    new SelectItemProjection(
                        "repeated",
                        new RepeatStringProjection(
                            new FieldString("t.affixes", "affix"),
                            new FieldInteger("t.affixes", "start")
                        )
                    ),
                    new SelectItemProjection(
                        "replaced",
                        new ReplaceStringProjection(
                            new FieldString("t.affixes", "text"),
                            new FieldString("t.affixes", "affix"),
                            new ReverseStringProjection(
                                new FieldString("t.affixes", "affix")
                            )
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.affixes", "id")),
                ]
            )
        );

        Assert.Equal(["4", "5", "2"], ResultColumn.Texts(projection, "position"));
        Assert.Equal(["a", "", "b"], ResultColumn.Texts(projection, "part"));
        Assert.Equal(["anb", "naba", "-a"], ResultColumn.Texts(projection, "padded"));
        Assert.Equal(
            ["ananan", "nananana", "--"],
            ResultColumn.Texts(projection, "repeated")
        );
        Assert.Equal(
            ["bnanaa", "baanan", "a-b-c"],
            ResultColumn.Texts(projection, "replaced")
        );
    }

    private static LiteralString Text(string value)
    {
        return new LiteralString(value);
    }

    private static LiteralInteger Number(long value)
    {
        return new LiteralInteger(value);
    }

    private static string Reversed(string text)
    {
        return new string([.. text.Reverse()]);
    }

    // A result between brackets: an empty string is "[]", where a null, which
    // a result cell writes as the empty text too, would leave the cell empty.
    private static string Bracketed(string text)
    {
        return $"[{text}]";
    }

    private static ConcatStringProjection Bracketed(StringProjection expression)
    {
        return new ConcatStringProjection([Text("["), expression, Text("]")]);
    }

    // An expression of one string has the expected value both when the
    // string is read from a stored row and when it is written as a literal.
    private static void Yields(
        string expected,
        string text,
        Func<StringProjection, ExprProjection> expression
    )
    {
        Assert.Equal(
            [expected, expected],
            [
                Value(text, expression(new FieldString("t.texts", "text"))),
                Value(text, expression(new LiteralString(text))),
            ]
        );
    }

    // The same for an expression that takes the string where a null may
    // stand.
    private static void Yields(
        string expected,
        string text,
        Func<StringNullableProjection, ExprProjection> expression
    )
    {
        Assert.Equal(
            [expected, expected],
            [
                Value(text, expression(new FieldString("t.texts", "text"))),
                Value(text, expression(new LiteralString(text))),
            ]
        );
    }

    private static string Value(string text, ExprProjection expression)
    {
        IStoredSchemaDataSet texts = Texts.With([text]);
        PureQLProjection projection = new(
            [texts],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                select: [new SelectItemProjection("value", expression)]
            )
        );

        return Assert.Single(ResultColumn.Texts(projection, "value"));
    }
}
