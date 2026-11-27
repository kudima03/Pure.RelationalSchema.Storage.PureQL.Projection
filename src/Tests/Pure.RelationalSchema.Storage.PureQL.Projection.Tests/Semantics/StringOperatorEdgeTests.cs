using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Edge cases of the string operators of the specification (Strings): code
// points outside the Basic Multilingual Plane inside emoji sequences and
// flags, combining marks, compatibility and canonical normalization with
// reordering and composition exclusions, the full case mappings that change
// the length, nonspacing marks that are not accents, and matches that must
// not start or end inside a surrogate pair. Every expected value is derived
// from the specification and the Unicode Character Database, and each
// expression is run over a stored string and over the same string written
// as a literal (which the simplifier folds when the projection is built).
public sealed class StringOperatorEdgeTests
{
    // A family: man, zero width joiner, woman, zero width joiner, girl.
    private const string Family = "\U0001F468\x200D\U0001F469\x200D\U0001F467";

    // The flag of France: two regional indicator symbols, F and R.
    private const string France = "\U0001F1EB\U0001F1F7";

    private const string IndicatorF = "\U0001F1EB";

    private const string IndicatorR = "\U0001F1F7";

    private const string Grin = "\U0001F600";

    private const string Beam = "\U0001F601";

    private static readonly InlineTable Texts = new(
        "t",
        "texts",
        ("text", new StringColumnType())
    );

    // Strings: length counts code points, so a character outside the Basic
    // Multilingual Plane counts once and a sequence counts each code point.
    [Theory]
    [InlineData(Family, "5")]
    [InlineData(France, "2")]
    [InlineData("\r\n", "2")]
    [InlineData("\U0010FFFF", "1")]
    [InlineData("\U00010000", "1")]
    [InlineData("a\x0301\x0323", "3")]
    [InlineData("\U0001F44D\U0001F3FD", "2")]
    [InlineData("\x2764\xFE0F", "2")]
    public void LengthCountsEveryCodePointOfASequence(string text, string expected)
    {
        Yields(expected, text, operand => new LengthIntegerProjection(operand));
    }

    // Strings: "one code point may become several, and the length may
    // change".
    [Theory]
    [InlineData("\x0149", "upper", "2")]
    [InlineData("\xFB03", "upper", "3")]
    [InlineData("\x0390", "upper", "3")]
    [InlineData("\x0130", "lower", "2")]
    [InlineData("\x1E9E", "foldCase", "2")]
    [InlineData("\xFB13", "foldCase", "2")]
    [InlineData("\x1FBC", "foldCase", "2")]
    public void TheLengthOfAMappedStringCountsTheCodePointsItBecame(
        string text,
        string mapping,
        string expected
    )
    {
        Yields(
            expected,
            text,
            operand => new LengthIntegerProjection(
                mapping switch
                {
                    "upper" => new UpperStringProjection(operand),
                    "lower" => new LowerStringProjection(operand),
                    _ => new FoldCaseStringProjection(operand),
                }
            )
        );
    }

    // upper: the full mappings of SpecialCasing.txt, without a locale.
    [Theory]
    [InlineData("\x01F0", "J\x030C")]
    [InlineData("\xFB00\xFB03", "FFFFI")]
    [InlineData("\x1FB3", "\x0391\x0399")]
    [InlineData("\x1FBC", "\x0391\x0399")]
    [InlineData("\x1F80", "\x1F08\x0399")]
    [InlineData("\x0587", "\x0535\x0552")]
    [InlineData("\xFB13", "\x0544\x0546")]
    [InlineData("\x01C5", "\x01C4")]
    [InlineData("\U0001E922", "\U0001E900")]
    [InlineData("\x00DF\x0130", "SS\x0130")]
    public void UpperAppliesTheFullMappings(string text, string expected)
    {
        Yields(expected, text, operand => new UpperStringProjection(operand));
    }

    // lower: the full mappings and the final sigma, after a cased letter
    // outside the Basic Multilingual Plane and before a case-ignorable mark.
    [Theory]
    [InlineData("\x1FBC", "\x1FB3")]
    [InlineData("\x1F88", "\x1F80")]
    [InlineData("\x01C5", "\x01C6")]
    [InlineData("\U0001E900", "\U0001E922")]
    [InlineData("\U00010400\x03A3\x0301", "\U00010428\x03C2\x0301")]
    [InlineData("\x00DF\x0130", "\x00DFi\x0307")]
    public void LowerAppliesTheFullMappingsAndTheFinalSigma(string text, string expected)
    {
        Yields(expected, text, operand => new LowerStringProjection(operand));
    }

    // foldCase: the full foldings of CaseFolding.txt (statuses C and F).
    [Theory]
    [InlineData("\x1FBC", "\x03B1\x03B9")]
    [InlineData("\x1FB3", "\x03B1\x03B9")]
    [InlineData("\x1F88", "\x1F00\x03B9")]
    [InlineData("\x0390", "\x03B9\x0308\x0301")]
    [InlineData("\x01C5", "\x01C6")]
    [InlineData("\x01C4", "\x01C6")]
    [InlineData("\x0587", "\x0565\x0582")]
    [InlineData("\x0149", "\x02BCn")]
    [InlineData("\U0001E900", "\U0001E922")]
    [InlineData("\U00010400\x03A3\x0301", "\U00010428\x03C3\x0301")]
    [InlineData("\x00DF\x0130", "ssi\x0307")]
    public void FoldCaseAppliesTheFullFoldings(string text, string expected)
    {
        Yields(expected, text, operand => new FoldCaseStringProjection(operand));
    }

    // Strings: to match without regard to case, apply foldCase to both
    // operands; a code point may fold to several.
    [Fact]
    public void AMatchOfFoldedOperandsIgnoresCase()
    {
        Yields(
            "True",
            "STRASSE",
            operand => new ContainsProjection(
                new FoldCaseStringProjection(operand),
                new FoldCaseStringProjection(new LiteralString("\x00DF"))
            )
        );
        Yields(
            "True",
            "\x01C5emal",
            operand => new StartsWithProjection(
                new FoldCaseStringProjection(operand),
                new FoldCaseStringProjection(new LiteralString("\x01C4"))
            )
        );
        Yields(
            "True",
            "\x1F88",
            operand => new EqualStringProjection(
                new FoldCaseStringProjection(operand),
                new FoldCaseStringProjection(new LiteralString("\x1F00\x0399"))
            )
        );
    }

    // normalize: the forms of Annex #15, with canonical reordering, singletons,
    // composition exclusions and compatibility mappings.
    [Theory]
    [InlineData("\x1E9B\x0323", NormalizationForm.NFC, "\x1E9B\x0323")]
    [InlineData("\x1E9B\x0323", NormalizationForm.NFD, "\x017F\x0323\x0307")]
    [InlineData("\x1E9B\x0323", NormalizationForm.NFKC, "\x1E69")]
    [InlineData("\x1E9B\x0323", NormalizationForm.NFKD, "s\x0323\x0307")]
    [InlineData("a\x0301\x0323", NormalizationForm.NFD, "a\x0323\x0301")]
    [InlineData("a\x0301\x0323", NormalizationForm.NFC, "\x1EA1\x0301")]
    [InlineData("a\x0323\x0301", NormalizationForm.NFC, "\x1EA1\x0301")]
    [InlineData("\xFF76\xFF9E", NormalizationForm.NFC, "\xFF76\xFF9E")]
    [InlineData("\xFF76\xFF9E", NormalizationForm.NFKC, "\x30AC")]
    [InlineData("\xFF76\xFF9E", NormalizationForm.NFKD, "\x30AB\x3099")]
    [InlineData("\x216B", NormalizationForm.NFKC, "XII")]
    [InlineData("\x2460", NormalizationForm.NFKD, "1")]
    [InlineData("\x0344", NormalizationForm.NFC, "\x0308\x0301")]
    [InlineData("\x212B", NormalizationForm.NFC, "\x00C5")]
    [InlineData("\x212B", NormalizationForm.NFD, "A\x030A")]
    [InlineData("\x2126", NormalizationForm.NFKD, "\x03A9")]
    [InlineData("\x0958", NormalizationForm.NFC, "\x0915\x093C")]
    [InlineData("\x0F73", NormalizationForm.NFC, "\x0F71\x0F72")]
    [InlineData("\U0002F800", NormalizationForm.NFC, "\x4E3D")]
    [InlineData("\U0001D15E", NormalizationForm.NFC, "\U0001D157\U0001D165")]
    [InlineData("\xFB01\U0001D15E", NormalizationForm.NFKC, "fi\U0001D157\U0001D165")]
    [InlineData("e\x0301" + Grin, NormalizationForm.NFC, "\x00E9" + Grin)]
    public void NormalizeGivesTheFormOfAnnex15(
        string text,
        NormalizationForm form,
        string expected
    )
    {
        Yields(expected, text, operand => new NormalizeStringProjection(operand, form));
    }

    // normalize: the length may change, and it counts code points.
    [Fact]
    public void ALengthAfterNormalizingCountsCodePointsOutsideThePlane()
    {
        Yields(
            "2",
            "\U0001D15E",
            operand => new LengthIntegerProjection(
                new NormalizeStringProjection(operand, NormalizationForm.NFC)
            )
        );
        Yields(
            "1",
            "\U0002F800",
            operand => new LengthIntegerProjection(
                new NormalizeStringProjection(operand, NormalizationForm.NFD)
            )
        );
    }

    // normalize: a noncharacter is a code point with no decomposition, so
    // every form keeps it; the text around it is normalized as usual.
    [Theory(
        Skip = "Bug: normalize of a string holding the noncharacter U+FFFE must keep it "
            + "(Strings, normalize: Annex #15 forms); string.Normalize throws "
            + "ArgumentException 'String contains invalid Unicode code points'"
    )]
    [InlineData(NormalizationForm.NFC, "\x00E9\xFFFE\x00E9")]
    [InlineData(NormalizationForm.NFD, "e\x0301\xFFFEe\x0301")]
    [InlineData(NormalizationForm.NFKC, "\x00E9\xFFFE\x00E9")]
    [InlineData(NormalizationForm.NFKD, "e\x0301\xFFFEe\x0301")]
    public void NormalizeKeepsANoncharacter(NormalizationForm form, string expected)
    {
        Yields(
            Bracketed(expected),
            "e\x0301\xFFFE\x00E9",
            operand => Bracketed(new NormalizeStringProjection(operand, form))
        );
    }

    [Theory]
    [InlineData(NormalizationForm.NFC)]
    [InlineData(NormalizationForm.NFD)]
    [InlineData(NormalizationForm.NFKC)]
    [InlineData(NormalizationForm.NFKD)]
    public void NormalizeKeepsTheLastNoncharacterOfThePlanes(NormalizationForm form)
    {
        Yields(
            "[a\xFFFF\U0010FFFF]",
            "a\xFFFF\U0010FFFF",
            operand => Bracketed(new NormalizeStringProjection(operand, form))
        );
    }

    // removeDiacritics: every code point of general category Mn goes, also
    // a variation selector, a nukta, a dakuten and a mark outside the plane;
    // a spacing (Mc) or enclosing (Me) mark, a joiner and an emoji modifier
    // stay, and the result is in the form NFC (so a composition exclusion
    // stays decomposed).
    [Theory]
    [InlineData("\x2764\xFE0F", "\x2764")]
    [InlineData("a\U0001D167b", "ab")]
    [InlineData("\U00011013\U00011038", "\U00011013")]
    [InlineData("a\x20DD", "a\x20DD")]
    [InlineData("\x0915\x093F", "\x0915\x093F")]
    [InlineData("\x0958", "\x0915")]
    [InlineData("\x30AC", "\x30AB")]
    [InlineData("\x0301", "")]
    [InlineData("\x1E9B\x0323", "\x017F")]
    [InlineData(Family, Family)]
    [InlineData("\U0001F44D\U0001F3FD", "\U0001F44D\U0001F3FD")]
    [InlineData("\x01C5", "\x01C5")]
    [InlineData("\x212B", "A")]
    [InlineData("\x2126", "\x03A9")]
    [InlineData("\U0001D15E", "\U0001D157\U0001D165")]
    [InlineData("\x1EC7" + Grin, "e" + Grin)]
    public void RemoveDiacriticsDropsExactlyTheNonspacingMarks(
        string text,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new RemoveDiacriticsStringProjection(operand))
        );
    }

    // trim: the longest removable first, again and again, and only the white
    // space listed.
    [Theory]
    [InlineData("aaa", "start", "a")]
    [InlineData("aaa", "end", "a")]
    [InlineData("aaa", "both", "a")]
    [InlineData("aaaa", "both", "")]
    [InlineData("aaaaa", "both", "a")]
    public void ARemovableIsTakenWholeAsOftenAsItMatches(
        string text,
        string side,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(Trimmed(side, operand, Text("aa")))
        );
    }

    [Fact]
    public void ARemovableOutsideThePlaneIsOneCodePoint()
    {
        Yields(
            "[x]",
            Grin + Grin + "x",
            operand => Bracketed(new TrimStartStringProjection(operand, [Text(Grin)]))
        );
        Yields(
            "[x" + Beam + "]",
            Grin + Beam + "x" + Beam + Grin,
            operand => Bracketed(
                new TrimStringProjection(operand, [Text(Grin), Text(Grin + Beam)])
            )
        );
        Yields(
            "[" + IndicatorF + "]",
            France + IndicatorF,
            operand => Bracketed(
                new TrimEndStringProjection(operand, [Text(IndicatorR + IndicatorF)])
            )
        );
    }

    [Fact]
    public void AMarkIsTrimmedApartFromItsBase()
    {
        Yields(
            "[e]",
            "e\x0301\x0301",
            operand => Bracketed(new TrimEndStringProjection(operand, [Text("\x0301")]))
        );
    }

    // trim: "tabs, line breaks, the no-break space U+00A0 and any other white
    // space are removed only if listed".
    [Fact]
    public void UnicodeWhiteSpaceIsRemovedOnlyWhenListed()
    {
        const string spaced = "\x3000\x2003\x200Ba\x2003\x00A0\x3000";

        Yields(
            Bracketed(spaced),
            spaced,
            operand => Bracketed(new TrimStringProjection(operand, [Text(" ")]))
        );
        Yields(
            "[a]",
            spaced,
            operand => Bracketed(
                new TrimStringProjection(
                    operand,
                    [Text("\x3000"), Text("\x2003"), Text("\x200B"), Text("\x00A0")]
                )
            )
        );
        Yields(
            "[\x200Ba\x2003\x00A0\x3000]",
            spaced,
            operand => Bracketed(
                new TrimStartStringProjection(operand, [Text("\x3000"), Text("\x2003")])
            )
        );
    }

    // substring: positions are code points.
    [Theory]
    [InlineData(Family, 2, 1, "\x200D")]
    [InlineData(Family, 3, 3, "\U0001F469\x200D\U0001F467")]
    [InlineData(France, 2, 1, IndicatorR)]
    [InlineData("\U0010FFFFa", 2, 1, "a")]
    [InlineData("a" + Grin, 2, 1000, Grin)]
    [InlineData("a" + Grin, 3, 1, "")]
    [InlineData(Grin + Grin, 1, 1, Grin)]
    [InlineData(Grin + Grin, 2, 0, "")]
    public void SubstringCountsCodePointsOfASequence(
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

    // position: an occurrence of code points, at any code point, also where
    // the run of code points crosses two flags.
    [Theory]
    [InlineData(France + France, IndicatorR + IndicatorF, "2")]
    [InlineData(France + France, France, "1")]
    [InlineData(Family, "\x200D\U0001F467", "4")]
    [InlineData(Family, "\U0001F467", "5")]
    [InlineData("a" + Grin + "b", "b", "3")]
    public void PositionCountsTheCodePointsBeforeAnOccurrence(
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
    [InlineData(France + France, France, 2, "3")]
    [InlineData(France + France, France, 4, "0")]
    [InlineData(Grin + "a" + Grin + "a", Grin + "a", 2, "3")]
    public void PositionWithStartCountsFromTheStartOfTheString(
        string text,
        string pattern,
        long start,
        string expected
    )
    {
        Yields(
            expected,
            text,
            operand => new PositionIntegerProjection(
                operand,
                Text(pattern),
                Number(start)
            )
        );
    }

    // replace and splitPart find their pattern as position finds it.
    [Fact]
    public void ReplaceAndSplitPartFindARunAcrossTwoFlags()
    {
        Yields(
            "[" + IndicatorF + "-" + IndicatorR + "]",
            France + France,
            operand => Bracketed(
                new ReplaceStringProjection(
                    operand,
                    Text(IndicatorR + IndicatorF),
                    Text("-")
                )
            )
        );
        Yields(
            "[" + IndicatorF + "]",
            France + France,
            operand => Bracketed(
                new SplitPartStringProjection(
                    operand,
                    Text(IndicatorR + IndicatorF),
                    Number(1)
                )
            )
        );
        Yields(
            "[" + IndicatorR + "]",
            France + France,
            operand => Bracketed(
                new SplitPartStringProjection(
                    operand,
                    Text(IndicatorR + IndicatorF),
                    Number(-1)
                )
            )
        );
    }

    // contains, startsWith and endsWith match code points. A half of a
    // surrogate pair is not a code point of the string the pair encodes, and
    // "greaterThan(position(s, p), 0) is contains(s, p)" (Strings, position).
    [Fact(
        Skip = "Bug: contains(\"a\U0001F600\", \"\\uDE00\") must be false like "
            + "position (which gives 0; Strings: position > 0 is contains); "
            + "contains and endsWith match UTF-16 code units and give true"
    )]
    public void HalfASurrogatePairIsNotContainedInThePair()
    {
        string[] expected = ["False", "False", "False", "0"];

        Assert.Equal(expected, Matches("a" + Grin, "\xDE00"));
        Assert.Equal(expected, Matches(Grin + "a", "\xD83D"));
        Assert.Equal(expected, Matches("x" + Grin + "y", "\xDE00y"));
    }

    [Fact]
    public void AWholeCodePointIsContainedWhereItStands()
    {
        Assert.Equal(["True", "False", "True", "2"], Matches("a" + Grin, Grin));
        Assert.Equal(["True", "True", "False", "1"], Matches(France + "!", IndicatorF));
        Assert.Equal(
            ["True", "False", "False", "2"],
            Matches(France + France, IndicatorR + IndicatorF)
        );
    }

    // padStart and padEnd: the fill is cut to fit in code points.
    [Theory]
    [InlineData("a", 2, Grin + "x", "start", Grin + "a")]
    [InlineData("a", 3, "x" + Grin + "y", "end", "ax" + Grin)]
    [InlineData("", 3, Grin + Beam, "start", Grin + Beam + Grin)]
    [InlineData(Grin + Grin, 2, "x", "start", Grin + Grin)]
    [InlineData(Grin, 2, France, "end", Grin + IndicatorF)]
    [InlineData(Family, 6, "*", "start", "*" + Family)]
    public void PaddingCountsCodePoints(
        string text,
        long length,
        string fill,
        string side,
        string expected
    )
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(
                side == "start"
                    ?
                        new PadStartStringProjection(operand, Number(length), Text(fill))
                    : new PadEndStringProjection(operand, Number(length), Text(fill))
            )
        );
    }

    // repeat writes the operand count times: two indicators make a flag, and
    // its length counts both.
    [Fact]
    public void RepeatingAnIndicatorWritesItsCodePoints()
    {
        Yields(
            "[" + IndicatorF + IndicatorF + "]",
            IndicatorF,
            operand => Bracketed(new RepeatStringProjection(operand, Number(2)))
        );
        Yields(
            "6",
            France,
            operand => new LengthIntegerProjection(
                new RepeatStringProjection(operand, Number(3))
            )
        );
    }

    // reverse: the code points in the opposite order, so a sequence comes out
    // reversed code point by code point.
    [Theory]
    [InlineData(Family, "\U0001F467\x200D\U0001F469\x200D\U0001F468")]
    [InlineData(France, IndicatorR + IndicatorF)]
    [InlineData("\U0010FFFFa", "a\U0010FFFF")]
    [InlineData("\U0001F44D\U0001F3FD", "\U0001F3FD\U0001F44D")]
    [InlineData("a\x0301\x0323", "\x0323\x0301a")]
    public void ReverseTurnsEveryCodePointOfASequence(string text, string expected)
    {
        Yields(
            Bracketed(expected),
            text,
            operand => Bracketed(new ReverseStringProjection(operand))
        );
    }

    // concat is lifted; concatWith skips only null and keeps an empty string.
    [Fact]
    public void ConcatOfANullLiteralIsNullAndConcatWithSkipsIt()
    {
        Yields(
            "NULL",
            "a",
            operand => new CoalesceStringProjection([
                new ConcatStringNullableProjection([
                    operand,
                    new LiteralStringNullable(),
                    Text("b"),
                ]),
                Text("NULL"),
            ])
        );
        Yields(
            "[]",
            "",
            operand => Bracketed(
                new ConcatWithProjection(
                    [new LiteralStringNullable(), operand, new LiteralStringNullable()],
                    Text(",")
                )
            )
        );
        Yields(
            "[" + Grin + "," + Grin + "]",
            Grin,
            operand => Bracketed(
                new ConcatWithProjection([operand, operand], Text(","))
            )
        );
    }

    // Strings: equal compares code points, so the two encodings of \x00E9, and a
    // letter and its folded form, differ until normalized or folded.
    [Fact]
    public void EqualityNeitherNormalizesNorFolds()
    {
        Yields(
            "False",
            "e\x0301",
            operand => new EqualStringProjection(operand, Text("\x00E9"))
        );
        Yields(
            "True",
            "e\x0301",
            operand => new EqualStringProjection(
                new NormalizeStringProjection(operand, NormalizationForm.NFC),
                Text("\x00E9")
            )
        );
        Yields(
            "False",
            "\x212B",
            operand => new EqualStringProjection(operand, Text("\x00C5"))
        );
    }

    private static StringProjection Trimmed(
        string side,
        StringProjection operand,
        StringProjection removable
    )
    {
        return side switch
        {
            "start" => new TrimStartStringProjection(operand, [removable]),
            "end" => new TrimEndStringProjection(operand, [removable]),
            _ => new TrimStringProjection(operand, [removable]),
        };
    }

    private static LiteralString Text(string value)
    {
        return new LiteralString(value);
    }

    private static LiteralInteger Number(long value)
    {
        return new LiteralInteger(value);
    }

    // A result between brackets, so an empty string is told from a null.
    private static string Bracketed(string text)
    {
        return $"[{text}]";
    }

    private static ConcatStringProjection Bracketed(StringProjection expression)
    {
        return new ConcatStringProjection([Text("["), expression, Text("]")]);
    }

    // contains, startsWith, endsWith and position of a pattern in a text,
    // over one stored row that holds the text.
    private static string[] Matches(string text, string pattern)
    {
        FieldString operand = new("t.texts", "text");
        PureQLProjection projection = new(
            [Texts.With([text])],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                select:
                [
                    new SelectItemProjection(
                        "contains",
                        new ContainsProjection(operand, Text(pattern))
                    ),
                    new SelectItemProjection(
                        "startsWith",
                        new StartsWithProjection(operand, Text(pattern))
                    ),
                    new SelectItemProjection(
                        "endsWith",
                        new EndsWithProjection(operand, Text(pattern))
                    ),
                    new SelectItemProjection(
                        "position",
                        new PositionIntegerProjection(operand, Text(pattern))
                    ),
                ]
            )
        );

        return
        [
            Assert.Single(ResultColumn.Texts(projection, "contains")),
            Assert.Single(ResultColumn.Texts(projection, "startsWith")),
            Assert.Single(ResultColumn.Texts(projection, "endsWith")),
            Assert.Single(ResultColumn.Texts(projection, "position")),
        ];
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
