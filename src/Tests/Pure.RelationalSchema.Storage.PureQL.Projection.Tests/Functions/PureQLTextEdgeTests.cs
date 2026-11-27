namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// Edge cases of the functions of PureQLText, against the specification
// (Strings; Regular expressions): positions and lengths in code points
// inside emoji sequences and flags, occurrences that cross two flags,
// operands and fills longer or shorter than asked for, nonspacing marks that
// are not accents, and regular expressions over code points outside the
// Basic Multilingual Plane, with classes, escapes inside a class, groups
// that take no part or match the empty string, and the rule that the search
// moves on by one code point after an empty match.
public sealed class PureQLTextEdgeTests
{
    private const string Grin = "\U0001F600";

    private const string Beam = "\U0001F601";

    private const string IndicatorF = "\U0001F1EB";

    private const string IndicatorR = "\U0001F1F7";

    private const string France = IndicatorF + IndicatorR;

    private const string Family = "\U0001F468\x200D\U0001F469\x200D\U0001F467";

    [Theory]
    [InlineData(Family, 5)]
    [InlineData(France, 2)]
    [InlineData("\U0010FFFF\U00010000", 2)]
    [InlineData("\r\n", 2)]
    [InlineData("\x0915\x093F", 2)]
    public void LengthCountsTheCodePointsOfASequence(string operand, long expected)
    {
        Assert.Equal(expected, PureQLText.Length(operand));
    }

    [Theory]
    [InlineData(Family, 2, 1, "\x200D")]
    [InlineData(Family, 5, 1, "\U0001F467")]
    [InlineData(Family, 5, 9, "\U0001F467")]
    [InlineData(Family, 6, 1, "")]
    [InlineData(France, 2, long.MaxValue, IndicatorR)]
    [InlineData(Grin, 1, 0, "")]
    [InlineData(Grin, 2, 0, "")]
    public void SubstringCountsCodePoints(
        string operand,
        long start,
        long length,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Substring(operand, start, length));
    }

    [Theory]
    [InlineData(France + France, IndicatorR + IndicatorF, 2)]
    [InlineData(France + France, France, 1)]
    [InlineData(Family, "\x200D\U0001F467", 4)]
    [InlineData(Grin + Beam + Grin, Beam + Grin, 2)]
    public void PositionFindsARunOfCodePointsAnywhere(
        string operand,
        string pattern,
        long expected
    )
    {
        Assert.Equal(expected, PureQLText.Position(operand, pattern));
    }

    [Theory]
    [InlineData(France + France, France, 2, 3)]
    [InlineData(France + France, France, 4, 0)]
    [InlineData(France + France, IndicatorR + IndicatorF, 3, 0)]
    [InlineData(Grin + "a" + Grin + "a", Grin + "a", 2, 3)]
    [InlineData(Grin, "", 2, 2)]
    [InlineData(Grin, "", 3, 0)]
    public void PositionWithAStartCountsFromTheStartOfTheString(
        string operand,
        string pattern,
        long start,
        long expected
    )
    {
        Assert.Equal(expected, PureQLText.Position(operand, pattern, start));
    }

    [Fact]
    public void ReplaceAndSplitPartFindARunAcrossTwoFlags()
    {
        Assert.Equal(
            IndicatorF + "-" + IndicatorR,
            PureQLText.Replace(France + France, IndicatorR + IndicatorF, "-")
        );
        Assert.Equal(
            IndicatorF,
            PureQLText.SplitPart(France + France, IndicatorR + IndicatorF, 1)
        );
        Assert.Equal(
            IndicatorR,
            PureQLText.SplitPart(France + France, IndicatorR + IndicatorF, 2)
        );
        Assert.Equal(
            "",
            PureQLText.SplitPart(France + France, IndicatorR + IndicatorF, 3)
        );
    }

    [Theory]
    [InlineData("a", "abc", 1, "a")]
    [InlineData("a", "abc", -1, "a")]
    [InlineData("a", "abc", 2, "")]
    [InlineData("abcabc", "abc", 2, "")]
    [InlineData("abcabc", "abc", 3, "")]
    [InlineData("abcabc", "abc", -3, "")]
    [InlineData("abcabc", "abc", -4, "")]
    public void APartAroundALongDelimiterMayBeEmpty(
        string operand,
        string delimiter,
        long index,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.SplitPart(operand, delimiter, index));
    }

    [Theory]
    [InlineData("aaa", new[] { "aa" }, "a", "a", "a")]
    [InlineData("aaaa", new[] { "aa" }, "", "", "")]
    [InlineData("ab", new[] { "abc" }, "ab", "ab", "ab")]
    [InlineData(
        Grin + Beam + "x" + Beam + Grin,
        new[] { Grin, Grin + Beam },
        "x" + Beam + Grin,
        Grin + Beam + "x" + Beam,
        "x" + Beam
    )]
    [InlineData("e\x0301\x0301", new[] { "\x0301" }, "e\x0301\x0301", "e", "e")]
    [InlineData(
        "\x3000a\x2003",
        new[] { " " },
        "\x3000a\x2003",
        "\x3000a\x2003",
        "\x3000a\x2003"
    )]
    [InlineData("\x3000a\x2003", new[] { "\x3000", "\x2003" }, "a\x2003", "\x3000a", "a")]
    public void TrimRemovesWholeRemovablesOnly(
        string operand,
        string[] removables,
        string start,
        string end,
        string both
    )
    {
        Assert.Equal(
            [start, end, both],
            [
                PureQLText.TrimStart(operand, removables),
                PureQLText.TrimEnd(operand, removables),
                PureQLText.Trim(operand, removables),
            ]
        );
    }

    [Theory]
    [InlineData("a", 2, Grin + "x", Grin + "a", "a" + Grin)]
    [InlineData("a", 4, "x" + Grin, "x" + Grin + "xa", "ax" + Grin + "x")]
    [InlineData("", 3, Grin + Beam, Grin + Beam + Grin, Grin + Beam + Grin)]
    [InlineData(Grin + Grin, 2, "x", Grin + Grin, Grin + Grin)]
    [InlineData(Family, 6, "*", "*" + Family, Family + "*")]
    [InlineData("a", 3, France, France + "a", "a" + France)]
    [InlineData("a", 2, France, IndicatorF + "a", "a" + IndicatorF)]
    public void PaddingCutsTheFillInCodePoints(
        string operand,
        long length,
        string fill,
        string start,
        string end
    )
    {
        Assert.Equal(start, PureQLText.PadStart(operand, length, fill));
        Assert.Equal(end, PureQLText.PadEnd(operand, length, fill));
    }

    // padStart and padEnd: "An empty fill adds nothing", whatever the length.
    [Fact]
    public void AnEmptyFillAddsNothingWhateverTheLength()
    {
        Assert.Equal("a", PureQLText.PadStart("a", long.MaxValue, ""));
        Assert.Equal("a", PureQLText.PadEnd("a", long.MaxValue, ""));
    }

    [Theory]
    [InlineData(Family, "\U0001F467\x200D\U0001F469\x200D\U0001F468")]
    [InlineData(France, IndicatorR + IndicatorF)]
    [InlineData("a\x0301\x0323", "\x0323\x0301a")]
    [InlineData("\U0010FFFF\U00010000", "\U00010000\U0010FFFF")]
    public void ReverseTurnsEveryCodePoint(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.Reverse(operand));
    }

    [Theory]
    [InlineData(IndicatorF, 2, IndicatorF + IndicatorF)]
    [InlineData(Family, 2, Family + Family)]
    public void RepeatWritesTheCodePointsCountTimes(
        string operand,
        long count,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Repeat(operand, count));
    }

    [Theory]
    [InlineData("\x0149", "\x02BCN")]
    [InlineData("\x01F0", "J\x030C")]
    [InlineData("\xFB00\xFB03", "FFFFI")]
    [InlineData("\x1FB3", "\x0391\x0399")]
    [InlineData("\x0587", "\x0535\x0552")]
    [InlineData("\U0001E922", "\U0001E900")]
    public void UpperAppliesTheFullMappings(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.Upper(operand));
    }

    [Theory]
    [InlineData("\x1FBC", "\x03B1\x03B9")]
    [InlineData("\x1F88", "\x1F00\x03B9")]
    [InlineData("\x0390", "\x03B9\x0308\x0301")]
    [InlineData("\x01C5", "\x01C6")]
    [InlineData("\x0149", "\x02BCn")]
    [InlineData("\U0001E900", "\U0001E922")]
    [InlineData("\x00DF\x0130", "ssi\x0307")]
    public void FoldCaseAppliesTheFullFoldings(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.FoldCase(operand));
        Assert.Equal(expected, PureQLText.FoldCase(PureQLText.FoldCase(operand)));
    }

    // removeDiacritics: "every nonspacing mark (a code point of general
    // category Mn) is dropped", and the result is in the form NFC.
    [Theory]
    [InlineData("\x2764\xFE0F", "\x2764")]
    [InlineData("a\U0001D167b", "ab")]
    [InlineData("\U00011013\U00011038", "\U00011013")]
    [InlineData("a\x20DD", "a\x20DD")]
    [InlineData("\x0915\x093F", "\x0915\x093F")]
    [InlineData("\x0958", "\x0915")]
    [InlineData("\x30AC", "\x30AB")]
    [InlineData("\xFF76\xFF9E", "\xFF76\xFF9E")]
    [InlineData("\x0301\x0302", "")]
    [InlineData("\x1E9B\x0323", "\x017F")]
    [InlineData(Family, Family)]
    [InlineData("\U0001F44D\U0001F3FD", "\U0001F44D\U0001F3FD")]
    [InlineData("\x212B", "A")]
    [InlineData("\x2126", "\x03A9")]
    [InlineData("\U0001D15E", "\U0001D157\U0001D165")]
    [InlineData("\x0344x", "x")]
    public void RemoveDiacriticsDropsExactlyTheNonspacingMarks(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.RemoveDiacritics(operand));
    }

    [Fact]
    public void RemoveDiacriticsKeepsANoncharacterAndTreatsTheTextAroundIt()
    {
        Assert.Equal("e\xFFFEa", PureQLText.RemoveDiacritics("e\x0301\xFFFEa\x0300"));
        Assert.Equal("\xFFFF" + Grin, PureQLText.RemoveDiacritics("\xFFFF" + Grin));
    }

    [Theory]
    [InlineData("^[\U0001F600-\U0001F64F]+$", "\U0001F603" + Grin, true)]
    [InlineData("^[\U0001F600-\U0001F64F]+$", "\U0001F680", false)]
    [InlineData("[\xFFFF-\U00010001]", "\U00010000", true)]
    [InlineData("[\xFFFF-\U00010001]", "\xFFFF", true)]
    [InlineData("[\xFFFF-\U00010001]", "\xFFFE", false)]
    [InlineData("[\xE000-\U0001F600]", "\xD7FF", false)]
    [InlineData("[\xE000-\U0001F600]", "\xFFFD", true)]
    [InlineData("[\xE000-\U0001F600]", Beam, false)]
    [InlineData("[^\xE000-\U0001F600]", Beam, true)]
    [InlineData("^[^\xE000-\U0001F600]$", "\xFFFD", false)]
    [InlineData("^[a-c" + Grin + "-" + Beam + "]{3}$", "b" + Beam + Grin, true)]
    [InlineData("\\b1", Grin + "1", true)]
    [InlineData("1\\b", "1" + Grin, true)]
    [InlineData("\\B" + Grin, "a" + Grin, false)]
    [InlineData("\\B" + Grin, Beam + Grin, true)]
    public void AClassAndARangeHoldCodePoints(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    // "Inside a class, \, ], [ and ^ are written with a backslash, and -
    // stands for itself when it is escaped, first or last".
    [Theory]
    [InlineData("^[a\\-z]$", "-", true)]
    [InlineData("^[a\\-z]$", "b", false)]
    [InlineData("^[a\\-z]$", "z", true)]
    [InlineData("^[-z]$", "-", true)]
    [InlineData("^[z-]$", "-", true)]
    [InlineData("^[z-]$", "y", false)]
    [InlineData("^[\\^a]$", "^", true)]
    [InlineData("^[\\^a]$", "b", false)]
    [InlineData("^[^\\^]$", "^", false)]
    [InlineData("^[\\]]$", "]", true)]
    [InlineData("^[\\[]$", "[", true)]
    [InlineData("^[\\\\]$", "\\", true)]
    [InlineData("^[\\\\]$", "a", false)]
    [InlineData("^[.]$", "a", false)]
    [InlineData("^[.]$", ".", true)]
    [InlineData("^[$]$", "$", true)]
    public void TheEscapesInsideAClass(string pattern, string operand, bool expected)
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("abcd", "(a|ab)(c|bcd)", 0, "abcd")]
    [InlineData("abcd", "(a|ab)(c|bcd)", 1, "a")]
    [InlineData("abcd", "(a|ab)(c|bcd)", 2, "bcd")]
    [InlineData("b", "(a|)", 1, "")]
    [InlineData("b", "(a*)b", 1, "")]
    [InlineData(
        Grin + "a" + Grin + "a" + Grin + "a",
        "(?:" + Grin + "a){2}",
        0,
        Grin + "a" + Grin + "a"
    )]
    [InlineData(Grin + Grin + Grin, Grin + "{1,2}?", 0, Grin)]
    [InlineData(Grin + Grin + Grin, "(" + Grin + "{1,2})", 1, Grin + Grin)]
    [InlineData("x" + Grin + Beam + "y", "x(.*)y", 1, Grin + Beam)]
    [InlineData("x" + Grin + Beam + "y", "x(.*?)" + Beam, 1, Grin)]
    public void ExtractRegexReturnsWhatTheBacktrackingRuleFinds(
        string operand,
        string pattern,
        long group,
        string expected
    )
    {
        Assert.Equal(
            expected,
            group == 0
                ? PureQLText.ExtractRegex(operand, pattern)
                : PureQLText.ExtractRegex(operand, pattern, group)
        );
    }

    [Theory]
    [InlineData("b", "(a){0}b", 1)]
    [InlineData("ab", "(x)?ab", 1)]
    [InlineData("ab", "a|(b)", 1)]
    public void AGroupThatTookNoPartIsNull(string operand, string pattern, long group)
    {
        Assert.Null(PureQLText.ExtractRegex(operand, pattern, group));
    }

    // replaceRegex: "After an empty match the search moves on by one code
    // point", so a non-empty match that starts where an empty one did is
    // not found.
    [Theory]
    [InlineData("abc", "|b", "-", "-a-b-c-")]
    [InlineData("abc", "x*|b", "-", "-a-b-c-")]
    [InlineData(Grin + Beam, "", "-", "-" + Grin + "-" + Beam + "-")]
    [InlineData(Grin + "b" + Grin, "b*", "-", "-" + Grin + "--" + Grin + "-")]
    [InlineData(Grin + "a", "\\B", "-", "-" + Grin + "a")]
    [InlineData("I " + France + " it", "[\U0001F1E6-\U0001F1FF]{2}", "-", "I - it")]
    [InlineData("e\x0301", ".", "-", "--")]
    [InlineData(Family, "\x200D", "+", "\U0001F468+\U0001F469+\U0001F467")]
    public void ReplaceRegexMovesOnByCodePoints(
        string operand,
        string pattern,
        string replacement,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ReplaceRegex(operand, pattern, replacement));
    }

    [Theory]
    [InlineData("([" + Grin + "-" + Beam + "])", 1)]
    [InlineData("[(" + Grin + ")]", 0)]
    [InlineData("(" + Grin + ")(" + Beam + ")?", 2)]
    public void RegexGroupsCountsGroupsAroundCodePointsOutsideThePlane(
        string pattern,
        int expected
    )
    {
        Assert.Equal(expected, PureQLText.RegexGroups(pattern));
    }

    [Theory]
    [InlineData("[" + Beam + "-" + Grin + "]")]
    [InlineData("[\U00010000-\xFFFF]")]
    [InlineData(Grin + "{2,1}")]
    [InlineData("\\" + Grin)]
    public void AnOutOfOrderRangeOrEscapeOutsideThePlaneIsNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
    }
}
