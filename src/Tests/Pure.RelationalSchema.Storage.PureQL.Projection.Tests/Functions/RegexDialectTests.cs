namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// matchesRegex, extractRegex and replaceRegex as the specification defines
// them under Regular expressions: matching by code point, anywhere in the
// string, leftmost and by the backtracking rule, with the ASCII sets and the
// anchors of the dialect whatever .NET's own regular expressions mean by
// them.
public sealed class RegexDialectTests
{
    private const string Grin = "\uD83D\uDE00";

    private const string Wink = "\uD83D\uDE09";

    [Theory]
    [InlineData("cabd", "ab", true)]
    [InlineData("cabd", "^ab", false)]
    [InlineData("cabd", "ab$", false)]
    [InlineData("ab", "^ab$", true)]
    [InlineData("", "", true)]
    [InlineData("abc", "", true)]
    [InlineData("", "a", false)]
    [InlineData("", "^$", true)]
    [InlineData("ann@example.com", "^[^@]+@[^@]+$", true)]
    [InlineData("ann.example.com", "^[^@]+@[^@]+$", false)]
    public void MatchesRegexIsTrueWhenThePatternMatchesAnywhere(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("ab", "a|ab", "a")]
    [InlineData("ab", "ab|a", "ab")]
    [InlineData("xaaay", "a+", "aaa")]
    [InlineData("xaaay", "a+?", "a")]
    [InlineData("xaaay", "a*", "")]
    [InlineData("xaaay", "a*?", "")]
    [InlineData("xaaay", "xa*?", "x")]
    [InlineData("xaaay", "xa*?y", "xaaay")]
    [InlineData("xaaay", "a{2}", "aa")]
    [InlineData("xaaay", "a{2,}", "aaa")]
    [InlineData("xaaay", "a{1,2}", "aa")]
    [InlineData("xaaay", "a{1,2}?", "a")]
    [InlineData("xaaay", "a{2,}?", "aa")]
    [InlineData("xaaay", "a{0}", "")]
    [InlineData("xaaay", "a?", "")]
    [InlineData("aaay", "a?", "a")]
    [InlineData("aaay", "a??", "")]
    [InlineData("ann@example.com", "@.+$", "@example.com")]
    [InlineData("one two", "[a-z]+$", "two")]
    public void ExtractRegexReturnsTheLeftmostMatchByTheBacktrackingRule(
        string operand,
        string pattern,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern));
    }

    [Fact]
    public void ExtractRegexIsNullWhenNothingMatches()
    {
        Assert.Null(PureQLText.ExtractRegex("abc", "x"));
        Assert.Null(PureQLText.ExtractRegex("abc", "(x)", 1));
        Assert.Null(PureQLText.ExtractRegex("", "a"));
    }

    [Fact]
    public void AMatchCanBeTheEmptyStringWhichIsNotNull()
    {
        Assert.Equal("", PureQLText.ExtractRegex("abc", "x*"));
        Assert.Equal("", PureQLText.ExtractRegex("", ""));
        Assert.Equal("", PureQLText.ExtractRegex("abc", "(x*)", 1));
        Assert.Equal("", PureQLText.ExtractRegex("abc", "()", 1));
    }

    [Theory]
    [InlineData("ann@example.com", "@(.+)$", 1, "example.com")]
    [InlineData("AB-123", "^([A-Z]+)-([0-9]+)", 1, "AB")]
    [InlineData("AB-123", "^([A-Z]+)-([0-9]+)", 2, "123")]
    [InlineData("abc", "((a)(?:b(c)))", 1, "abc")]
    [InlineData("abc", "((a)(?:b(c)))", 2, "a")]
    [InlineData("abc", "((a)(?:b(c)))", 3, "c")]
    [InlineData("file.tar.gz", "\\.([a-z]+)$", 1, "gz")]
    public void ExtractRegexWithAGroupReturnsWhatTheGroupCaptured(
        string operand,
        string pattern,
        long group,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern, group));
    }

    [Theory]
    [InlineData("b", "(a)|b", 1)]
    [InlineData("b", "(a)?b", 1)]
    [InlineData("ac", "a(b)*c", 1)]
    [InlineData("a", "(a)|(b)", 2)]
    public void ExtractRegexIsNullWhenTheGroupTookNoPartInTheMatch(
        string operand,
        string pattern,
        long group
    )
    {
        Assert.True(PureQLText.MatchesRegex(operand, pattern));
        Assert.Null(PureQLText.ExtractRegex(operand, pattern, group));
    }

    [Theory]
    [InlineData("abc", "(a|b|c)+", "c")]
    [InlineData("a1b2c3", "(?:([a-z])[0-9])+", "c")]
    [InlineData("abab", "(ab){2}", "ab")]
    [InlineData("xyz", "(.)*?z", "y")]
    public void AGroupInsideARepetitionHoldsItsLastRepetition(
        string operand,
        string pattern,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern, 1));
    }

    [Theory]
    [InlineData("(a)(b)", 0)]
    [InlineData("(a)(b)", -1)]
    [InlineData("(a)(b)", long.MinValue)]
    [InlineData("(a)(b)", 3)]
    [InlineData("(a)(b)", long.MaxValue)]
    [InlineData("(?:a)b", 1)]
    [InlineData("ab", 1)]
    [InlineData("\\(a\\)", 1)]
    [InlineData("[(]a[)]", 1)]
    public void AGroupBelowOneOrThatThePatternDoesNotHaveIsAnError(
        string pattern,
        long group
    )
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.ExtractRegex("ab", pattern, group)
        );
    }

    [Fact]
    public void APatternThatIsNotOneIsAnErrorBeforeTheGroupIsLookedAt()
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.ExtractRegex("ab", "(a", 5));
    }

    [Theory]
    [InlineData("a  b   c", "\\s+", " ", "a b c")]
    [InlineData("abc", "x*", "-", "-a-b-c-")]
    [InlineData("abxd", "x*", "-", "-a-b--d-")]
    [InlineData("abc", "", "-", "-a-b-c-")]
    [InlineData("", "", "-", "-")]
    [InlineData("", "x*", "-", "-")]
    [InlineData("", "x", "-", "")]
    [InlineData("abc", "x", "-", "abc")]
    [InlineData("aaa", "aa", "b", "ba")]
    [InlineData("banana", "an", "", "ba")]
    [InlineData("banana", "a|n", "", "b")]
    [InlineData("+1 (555) 010-0199", "\\D", "", "15550100199")]
    [InlineData("abc", "$", "!", "abc!")]
    [InlineData("abc", "^", "!", "!abc")]
    [InlineData("ab cd", "\\b", "|", "|ab| |cd|")]
    [InlineData("abc", "b*", "-", "-a--c-")]
    public void ReplaceRegexReplacesEveryMatchFromLeftToRightWithoutOverlap(
        string operand,
        string pattern,
        string replacement,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ReplaceRegex(operand, pattern, replacement));
    }

    [Theory]
    [InlineData("$1")]
    [InlineData("\\1")]
    [InlineData("$&")]
    [InlineData("$0")]
    [InlineData("${1}")]
    [InlineData("$$")]
    [InlineData("$`$'$+$_")]
    public void TheReplacementIsLiteralText(string replacement)
    {
        Assert.Equal(
            "x" + replacement + "z",
            PureQLText.ReplaceRegex("xyz", "(y)", replacement)
        );
    }

    [Theory]
    [InlineData("a" + Grin + "c", "x*", "-", "-a-" + Grin + "-c-")]
    [InlineData(Grin + Wink, "", "-", "-" + Grin + "-" + Wink + "-")]
    [InlineData(Grin + Wink, "x*", "-", "-" + Grin + "-" + Wink + "-")]
    [InlineData(Grin + Wink, "\\B", "-", "-" + Grin + "-" + Wink + "-")]
    [InlineData(Grin + Wink, "\\b|", "-", "-" + Grin + "-" + Wink + "-")]
    [InlineData("a" + Grin, "$|", "-", "-a-" + Grin + "-")]
    public void AfterAnEmptyMatchTheSearchMovesOnByOneCodePoint(
        string operand,
        string pattern,
        string replacement,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ReplaceRegex(operand, pattern, replacement));
    }

    [Theory]
    [InlineData("a" + Grin + "c", ".", "-", "---")]
    [InlineData("a" + Grin + "c", "a.c", "-", "-")]
    [InlineData("a" + Grin + "c", "a..c", "-", "a" + Grin + "c")]
    [InlineData(Grin + Grin + Wink, Grin + "+", "-", "-" + Wink)]
    [InlineData(Grin + Grin + Wink, Grin + "{2}", "-", "-" + Wink)]
    [InlineData(Grin + Grin + Wink, Grin + "?" + Wink, "-", Grin + "-")]
    [InlineData(Grin + Grin + Wink, "(?:" + Grin + ")*", "-", "--" + Wink + "-")]
    [InlineData("a" + Grin + "c", "[^a]", "-", "a--")]
    [InlineData("a" + Grin + "c", "\\W", "-", "a-c")]
    [InlineData("a" + Grin + "c", "\\D\\D", "-", "-c")]
    [InlineData("a" + Grin + "c", "\\S{3}", "-", "-")]
    public void ACharacterOutsideTheBasicMultilingualPlaneIsOneCodePoint(
        string operand,
        string pattern,
        string replacement,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ReplaceRegex(operand, pattern, replacement));
    }

    [Theory]
    [InlineData("a\nb", "a.b", false)]
    [InlineData("a\rb", "a.b", true)]
    [InlineData("a\u2028b", "a.b", true)]
    [InlineData("a\u0085b", "a.b", true)]
    [InlineData("a\0b", "a.b", true)]
    [InlineData("\n", ".", false)]
    [InlineData("\n", "[^a]", true)]
    [InlineData("\n", "\\s", true)]
    public void TheDotIsAnyCodePointExceptALineFeed(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("0123456789", "^\\d{10}$", true)]
    // Arabic-Indic, Devanagari and full-width digits are not \d.
    [InlineData("\u0663", "\\d", false)]
    [InlineData("\u0969", "\\d", false)]
    [InlineData("\uFF13", "\\d", false)]
    [InlineData("\u0663", "\\D", true)]
    [InlineData("\u0663", "[\\d]", false)]
    [InlineData("\u0663", "[^\\d]", true)]
    [InlineData("\u0663", "[^\\D]", false)]
    [InlineData("7", "[^\\D]", true)]
    [InlineData("azAZ09_", "^\\w{7}$", true)]
    // Letters outside ASCII, a combining mark and a connector are not \w.
    [InlineData("\u00E9", "\\w", false)]
    [InlineData("\u0436", "\\w", false)]
    [InlineData("\u0301", "\\w", false)]
    [InlineData("\u203F", "\\w", false)]
    [InlineData("\u212A", "\\w", false)]
    [InlineData("\u00E9", "\\W", true)]
    [InlineData("\u00E9", "[\\w]", false)]
    [InlineData("\u00E9", "[^\\W]", false)]
    [InlineData("-", "\\w", false)]
    [InlineData(" \t\n\r\f\v", "^\\s{6}$", true)]
    // The no-break space, the line separator, the next line and the ideographic
    // space are not \s.
    [InlineData("\u00A0", "\\s", false)]
    [InlineData("\u2028", "\\s", false)]
    [InlineData("\u0085", "\\s", false)]
    [InlineData("\u3000", "\\s", false)]
    [InlineData("\u001C", "\\s", false)]
    [InlineData("\u00A0", "\\S", true)]
    [InlineData("\u00A0", "[\\s]", false)]
    [InlineData("\u00A0", "[^\\S]", false)]
    [InlineData(" ", "\\S", false)]
    [InlineData(Grin, "^\\D\\W\\S$", false)]
    [InlineData(Grin, "^\\D$", true)]
    [InlineData(Grin, "^\\W$", true)]
    [InlineData(Grin, "^\\S$", true)]
    public void TheSetsOfDigitsWordCharactersAndWhiteSpaceAreAscii(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("a\tb", "a\\tb", true)]
    [InlineData("a\nb", "a\\nb", true)]
    [InlineData("a\rb", "a\\rb", true)]
    [InlineData("atb", "a\\tb", false)]
    [InlineData("a\tb", "a[\\t]b", true)]
    [InlineData("a\nb", "a[^\\n]b", false)]
    [InlineData("a\\nb", "a\\\\nb", true)]
    [InlineData("a\nb", "a\\\\nb", false)]
    [InlineData("a\\tb", "a[\\\\]tb", true)]
    public void TheEscapesOfTabLineFeedAndCarriageReturn(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("abc", "^abc$", true)]
    [InlineData("abc\n", "^abc$", false)]
    [InlineData("abc\n", "abc$", false)]
    [InlineData("abc\n", "abc\\n$", true)]
    [InlineData("x\nabc", "^abc", false)]
    [InlineData("abc\nx", "abc$", false)]
    [InlineData("x\nabc", "abc$", true)]
    [InlineData("\n", "^$", false)]
    [InlineData("a^b$", "a\\^b\\$", true)]
    public void TheAnchorsAreTheStartAndTheVeryEndOfTheStringAndNothingElse(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("a word here", "\\bword\\b", true)]
    [InlineData("swordfish", "\\bword\\b", false)]
    [InlineData("word", "^\\bword\\b$", true)]
    [InlineData("sword", "\\Bword", true)]
    [InlineData("a word", "\\Bword", false)]
    [InlineData("", "\\b", false)]
    [InlineData("", "\\B", true)]
    [InlineData(" ", "\\b", false)]
    [InlineData("a", "^\\b", true)]
    [InlineData("a", "\\b$", true)]
    // A letter outside ASCII is not a word character, so a boundary lies
    // between it and an ASCII letter and none between two of them.
    [InlineData("\u00E9a", "\u00E9\\ba", true)]
    [InlineData("\u00E9a", "\u00E9\\Ba", false)]
    [InlineData("\u00E9\u00E9", "\u00E9\\b\u00E9", false)]
    [InlineData("\u00E9\u00E9", "\u00E9\\B\u00E9", true)]
    [InlineData("\u00E9", "\\b", false)]
    [InlineData("a" + Grin, "a\\b" + Grin, true)]
    [InlineData("item discontinued.", "\\b(?:discontinued|recalled)\\b", true)]
    [InlineData("undiscontinued", "\\b(?:discontinued|recalled)\\b", false)]
    public void AWordBoundaryIsBetweenAnAsciiWordCharacterAndWhatIsNotOne(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Theory]
    [InlineData("A", "a", false)]
    [InlineData("a", "A", false)]
    [InlineData("K", "[a-z]", false)]
    // The Kelvin sign and the long s fold to k and s: a pattern does not fold.
    [InlineData("\u212A", "k", false)]
    [InlineData("\u212A", "K", false)]
    [InlineData("\u017F", "s", false)]
    [InlineData("\u017F", "[a-z]", false)]
    [InlineData("I", "\u0131", false)]
    [InlineData("i", "\u0130", false)]
    [InlineData("\u00E9", "e", false)]
    [InlineData("e\u0301", "\u00E9", false)]
    public void MatchingIsAlwaysCaseSensitiveAndNormalizesNothing(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Fact]
    public void ToIgnoreCaseThePatternIsWrittenFoldedAndTheOperandIsFolded()
    {
        Assert.True(
            PureQLText.MatchesRegex(PureQLText.FoldCase("Stra\u00DFe"), "^strasse$")
        );
        Assert.True(PureQLText.MatchesRegex(PureQLText.FoldCase("STRASSE"), "^strasse$"));
    }

    [Theory]
    [InlineData("a.b", "a\\.b", true)]
    [InlineData("axb", "a\\.b", false)]
    [InlineData(".*+?()[]{}|^$\\", "^\\.\\*\\+\\?\\(\\)\\[\\]\\{\\}\\|\\^\\$\\\\$", true)]
    [InlineData("a/b", "a\\/b", true)]
    [InlineData("a/b", "a/b", true)]
    [InlineData("a-b", "a-b", true)]
    [InlineData("a b#c", "a b#c", true)]
    [InlineData("a\"b'c", "a\"b'c", true)]
    [InlineData("a,b", "a,b", true)]
    [InlineData("a<b>c=d!e:f", "a<b>c=d!e:f", true)]
    [InlineData("stra\u00DFe", "\u00E9+\u00DF|a\u00DFe", true)]
    public void ALiteralAndAnEscapedSpecialCharacterStandForThemselves(
        string operand,
        string pattern,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, pattern));
    }

    [Fact]
    public void EveryRowOfTheCorpusGivesWhatPythonGives()
    {
        List<string> differences = [];

        foreach (string?[] row in RegexVectors.Rows)
        {
            string pattern = row[0]!;
            string operand = row[1]!;
            string? extracted = PureQLText.ExtractRegex(operand, pattern);
            string replaced = PureQLText.ReplaceRegex(operand, pattern, row[2]!);

            if (
                PureQLText.RegexGroups(pattern) != row.Length - 5
                || PureQLText.MatchesRegex(operand, pattern) != (row[4] is not null)
                || replaced != row[3]
                || extracted != row[4]
                || !Enumerable
                    .Range(1, row.Length - 5)
                    .All(group =>
                        PureQLText.ExtractRegex(operand, pattern, group) == row[4 + group]
                    )
            )
            {
                differences.Add($"{Shown(pattern)} on {Shown(operand)}");
            }
        }

        Assert.Empty(differences);
    }

    // Past the size of the cache of translated patterns, old and new patterns
    // still match as they did.
    [Fact]
    public void MorePatternsThanAreKeptStillMatch()
    {
        for (int number = 0; number < 3000; number++)
        {
            Assert.Equal(
                $"<{number}>",
                PureQLText.ExtractRegex($"a<{number}>b", $"<{number}>")
            );
        }

        Assert.Equal("<7>", PureQLText.ExtractRegex("a<7>b", "<7>"));
    }

    [Fact]
    public void APatternIsMatchedFromManyThreadsAtOnce()
    {
        string[] operands =
        [
            .. Enumerable.Range(0, 2000).Select(number => $"id-{number}"),
        ];
        string?[] extracted = new string?[operands.Length];

        _ = Parallel.For(
            0,
            operands.Length,
            index =>
                extracted[index] = PureQLText.ExtractRegex(
                    operands[index],
                    $"[0-9]+$|thread{index % 7}"
                )
        );

        Assert.Equal(operands.Select(operand => operand[3..]), extracted);
    }

    private static string Shown(string text)
    {
        return string.Concat(
            text.Select(unit =>
                unit is >= ' ' and <= '~' ? unit.ToString() : $"\\u{(int)unit:X4}"
            )
        );
    }
}
