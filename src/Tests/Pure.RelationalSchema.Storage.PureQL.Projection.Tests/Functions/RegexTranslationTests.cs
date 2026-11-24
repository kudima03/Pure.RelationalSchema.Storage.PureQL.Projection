namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// What is a pattern of the regular expression dialect of the specification
// and what is not: every construct outside the dialect is rejected, as are
// the mistakes the schema cannot see in a literal (parentheses that do not
// balance, a range or a {n,m} out of order), before any row is read.
public sealed class RegexTranslationTests
{
    private const string Grin = "\uD83D\uDE00";

    // The patterns of the valid samples of the specification.
    [Theory]
    [InlineData("", 0)]
    [InlineData("-", 0)]
    [InlineData("@", 0)]
    [InlineData("abc", 0)]
    [InlineData("^abc$", 0)]
    [InlineData("a.c", 0)]
    [InlineData("b+", 0)]
    [InlineData("a*b+c?", 0)]
    [InlineData("a*?b+?c??d{2,3}?", 0)]
    [InlineData("a{3}b{2,}c{2,5}", 0)]
    [InlineData("^[^@]+", 0)]
    [InlineData("^[ -~]*$", 0)]
    [InlineData("[0-9]", 0)]
    [InlineData("[0-9]+", 0)]
    [InlineData("^[0-9]{3}$", 0)]
    [InlineData("[0-9]{4,}", 0)]
    [InlineData("[0-9]{4}-[0-9]{2}", 0)]
    [InlineData("[^0-9a-f]", 0)]
    [InlineData("[abc]", 0)]
    [InlineData("[^abc]", 0)]
    [InlineData("[a-z]", 0)]
    [InlineData("^[A-Z]+", 0)]
    [InlineData("[a-z]+$", 0)]
    [InlineData("[-a][a-]", 0)]
    [InlineData("[^a-zA-Z '-]", 0)]
    [InlineData("[a-zA-Z0-9_]", 0)]
    [InlineData("[*+?{}().|$]", 0)]
    [InlineData("[\\]\\[\\\\\\^\\-]", 0)]
    [InlineData("\\/[\\/\\-\\.]", 0)]
    [InlineData("\\.\\*\\+\\?\\(\\)\\[\\]\\{\\}\\|\\^\\$\\\\", 0)]
    [InlineData("\\(a\\)", 0)]
    [InlineData("\\d", 0)]
    [InlineData("\\D", 0)]
    [InlineData("\\d+\\.\\d*", 0)]
    [InlineData("[\\d\\s,;]", 0)]
    [InlineData("\\d\\w\\s", 0)]
    [InlineData("\\D\\W\\S", 0)]
    [InlineData("\\bword\\b\\B", 0)]
    [InlineData("\\b(?:discontinued|recalled)\\b", 0)]
    [InlineData("cat|dog|", 0)]
    [InlineData("(?:ab)c", 0)]
    [InlineData("\u00E9+\u00DF", 0)]
    [InlineData("()", 1)]
    [InlineData("(ab)c", 1)]
    [InlineData("@(.+)$", 1)]
    [InlineData("\\.([a-z]+)$", 1)]
    [InlineData("(?:ab){2}(c|d)+", 1)]
    [InlineData("(a)([0-9])", 2)]
    [InlineData("^([A-Z]+)-([0-9]+)", 2)]
    [InlineData("((a)(?:b(c)))", 3)]
    public void RegexGroupsCountsTheCapturingGroupsOfAPattern(string pattern, int groups)
    {
        Assert.Equal(groups, PureQLText.RegexGroups(pattern));
    }

    [Theory]
    [InlineData("(a)\\(b\\)[(](?:c)", 1)]
    [InlineData("(((())))", 4)]
    [InlineData("(a)|(b)|(?:c)|(d)", 3)]
    [InlineData("(a)*(b)+?(c){2,3}", 3)]
    [InlineData("(" + Grin + ")", 1)]
    public void OnlyAnOpeningParenthesisThatIsNotEscapedOrInAClassCounts(
        string pattern,
        int groups
    )
    {
        Assert.Equal(groups, PureQLText.RegexGroups(pattern));
    }

    // The patterns of the invalid tests of the specification: what the schema
    // rejects in a literal is not a pattern as a parameter either.
    [Theory]
    [InlineData("(a)\\1")]
    [InlineData("(\\s)\\1")]
    [InlineData("a(?=b)")]
    [InlineData("@(?=.)")]
    [InlineData("a(?!b)")]
    [InlineData("(?<=a)b")]
    [InlineData("(?<!a)b")]
    [InlineData("(?<year>[0-9]{4})")]
    [InlineData("(?P<year>[0-9]{4})")]
    [InlineData("(?'year'[0-9]{4})")]
    [InlineData("(?i)abc")]
    [InlineData("(?i:abc)")]
    [InlineData("(?>a+)b")]
    [InlineData("a(?#note)b")]
    [InlineData("(?)")]
    [InlineData("(?")]
    [InlineData("a*+b")]
    [InlineData("a++")]
    [InlineData("a?+")]
    [InlineData("a{2}+")]
    [InlineData("a**")]
    [InlineData("a+*")]
    [InlineData("a{2}{3}")]
    [InlineData("a{2}*")]
    [InlineData("a+??")]
    [InlineData("a???")]
    [InlineData("a{2}??")]
    [InlineData("\\p{L}+")]
    [InlineData("\\P{L}")]
    [InlineData("\\x41")]
    [InlineData("\\u0041")]
    [InlineData("\\Aabc")]
    [InlineData("abc\\z")]
    [InlineData("abc\\Z")]
    [InlineData("\\0")]
    [InlineData("\\e")]
    [InlineData("\\f")]
    [InlineData("\\v")]
    [InlineData("\\G")]
    [InlineData("\\k")]
    [InlineData("\\Qa\\E")]
    [InlineData("\\#")]
    [InlineData("\\ ")]
    [InlineData("\\a")]
    [InlineData("a\\-b")]
    [InlineData("\\" + Grin)]
    [InlineData("a]")]
    [InlineData("a{")]
    [InlineData("a}")]
    [InlineData("a{,3}")]
    [InlineData("a{1,2,3}")]
    [InlineData("a{2, 3}")]
    [InlineData("a{ 2}")]
    [InlineData("a{2")]
    [InlineData("a{2,")]
    [InlineData("a{2,3")]
    [InlineData("a{x}")]
    [InlineData("a{-1}")]
    [InlineData("[a[bc]]")]
    [InlineData("[[:alpha:]]")]
    [InlineData("[]")]
    [InlineData("[^]")]
    [InlineData("[]a]")]
    [InlineData("[a^b]")]
    [InlineData("[\\b]")]
    [InlineData("[\\B]")]
    [InlineData("[\\A]")]
    [InlineData("[\\x]")]
    [InlineData("[abc")]
    [InlineData("[")]
    [InlineData("[^")]
    [InlineData("[a-")]
    [InlineData("[a\\")]
    [InlineData("abc\\")]
    [InlineData("\\")]
    public void AConstructOutsideTheDialectIsNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
    }

    [Theory]
    [InlineData("*abc")]
    [InlineData("+")]
    [InlineData("?")]
    [InlineData("{2}")]
    [InlineData("(+a)")]
    [InlineData("(?:*a)")]
    [InlineData("a|*b")]
    [InlineData("a|{2}")]
    [InlineData("^*abc")]
    [InlineData("^+")]
    [InlineData("a$?")]
    [InlineData("a${2}")]
    [InlineData("\\b+")]
    [InlineData("\\b*")]
    [InlineData("foo\\B{2}")]
    [InlineData("\\B?")]
    public void AQuantifierWithNothingToRepeatIsNotAPattern(string pattern)
    {
        FormatException error = Assert.Throws<FormatException>(() =>
            PureQLText.RegexGroups(pattern)
        );

        Assert.Contains("nothing to repeat", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("(")]
    [InlineData("(a")]
    [InlineData("((a)")]
    [InlineData("(?:a")]
    [InlineData("(a|b")]
    [InlineData(")")]
    [InlineData("a)")]
    [InlineData("(a))")]
    [InlineData("a)(")]
    [InlineData("())(")]
    public void ParenthesesThatDoNotBalanceAreNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
    }

    [Theory]
    [InlineData("[z-a]")]
    [InlineData("[b-a]")]
    [InlineData("[9-0]")]
    [InlineData("[a-Z]")]
    [InlineData("[" + Grin + "-a]")]
    [InlineData("[\uD83D\uDE01-\uD83D\uDE00]")]
    [InlineData("[a--]")]
    [InlineData("a{2,1}")]
    [InlineData("a{10,9}")]
    [InlineData("a{1,0}?")]
    public void ARangeOrCountsOutOfOrderAreNotAPattern(string pattern)
    {
        FormatException error = Assert.Throws<FormatException>(() =>
            PureQLText.RegexGroups(pattern)
        );

        Assert.Contains("out of order", error.Message, StringComparison.Ordinal);
    }

    // A hyphen between two members is a range, and a range runs from one
    // code point to another: not from or to a set, and not from the end of
    // another range.
    [Theory]
    [InlineData("[a-\\d]")]
    [InlineData("[\\d-a]")]
    [InlineData("[\\w-\\d]")]
    [InlineData("[a-z-9]")]
    [InlineData("[!--z]")]
    [InlineData("[a-\\b]")]
    [InlineData("[a-[]")]
    [InlineData("[a-^]")]
    public void AHyphenInAClassThatIsNoRangeIsNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
    }

    [Theory]
    [InlineData("a{2147483648}")]
    [InlineData("a{0,2147483648}")]
    [InlineData("a{99999999999999999999}")]
    public void ACountNoEngineHoldsIsNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
    }

    [Fact]
    public void EveryTextTheSchemaOrPythonRejectsIsNotAPattern()
    {
        Assert.All(
            RegexVectors.NotPatterns,
            text => Assert.Throws<FormatException>(() => PureQLText.RegexGroups(text))
        );
    }

    [Fact]
    public void TextThatIsNotAPatternIsAnErrorInEveryOperatorWhateverTheOperand()
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.MatchesRegex("", "a(?=b)"));
        _ = Assert.Throws<FormatException>(() => PureQLText.ExtractRegex("", "(a"));
        _ = Assert.Throws<FormatException>(() => PureQLText.ExtractRegex("", "(a", 1));
        _ = Assert.Throws<FormatException>(() =>
            PureQLText.ReplaceRegex("", "[z-a]", "")
        );
    }

    [Fact]
    public void TheErrorSaysWhatIsWrongAndAtWhichCodePoint()
    {
        FormatException error = Assert.Throws<FormatException>(() =>
            PureQLText.RegexGroups(Grin + "a**")
        );

        Assert.Equal(
            $"'{Grin}a**' is not a regular expression of the dialect: "
                + "a quantifier has nothing to repeat, at code point 4.",
            error.Message
        );
    }

    [Theory]
    [InlineData("a{0}", "aaa", "")]
    [InlineData("a{2}", "aaa", "aa")]
    [InlineData("a{2,}", "aaa", "aaa")]
    [InlineData("a{0,1}", "aaa", "a")]
    [InlineData("a{01,02}", "aaa", "aa")]
    [InlineData("a{000}b", "aab", "b")]
    [InlineData("a{2,2}", "aaa", "aa")]
    [InlineData("(?:ab){2}", "ababab", "abab")]
    [InlineData("[ab]{3}?", "abab", "aba")]
    [InlineData("a{1,}?", "aaa", "a")]
    public void ACountedQuantifierRepeatsThePrecedingAtom(
        string pattern,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern));
    }

    [Fact]
    public void TheLargestCountIsAccepted()
    {
        Assert.False(PureQLText.MatchesRegex("aaa", "a{2147483647}"));
        Assert.True(PureQLText.MatchesRegex("aaa", "a{3,2147483647}"));
    }

    [Theory]
    [InlineData("cat|dog|", "bird", "")]
    [InlineData("cat|dog|", "hotdog", "")]
    [InlineData("|a", "a", "")]
    [InlineData("a(|b)c", "ac", "ac")]
    [InlineData("a(?:)c", "ac", "ac")]
    [InlineData("()*a", "a", "a")]
    [InlineData("(?:){3}a", "a", "a")]
    [InlineData("a||b", "b", "")]
    public void AnAlternativeAndAGroupMayBeEmpty(
        string pattern,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern));
    }

    // The table of the dialect makes every code point but the special ones
    // stand for itself.
    [Theory]
    [InlineData("a\u0009b")]
    [InlineData("a\u0000b")]
    [InlineData("a b")]
    [InlineData("a#b")]
    public void ACodePointThatIsNotSpecialStandsForItself(string text)
    {
        Assert.Equal(0, PureQLText.RegexGroups(text));
        Assert.True(PureQLText.MatchesRegex("x" + text + "y", text));
        Assert.True(PureQLText.MatchesRegex(text, "^[" + text + "]{3}$"));
    }

    // A line feed is written with a backslash and n, in a literal pattern
    // and in a parameter alike: written as itself it is not a pattern.
    [Theory]
    [InlineData("a\u000Ab")]
    [InlineData("[a\u000A]")]
    [InlineData("\u000A")]
    public void ALineFeedWrittenAsItselfIsNotAPattern(string pattern)
    {
        _ = Assert.Throws<FormatException>(() => PureQLText.RegexGroups(pattern));
        _ = Assert.Throws<FormatException>(() =>
            PureQLText.MatchesRegex("a\u000Ab", pattern)
        );
        Assert.True(PureQLText.MatchesRegex("a\u000Ab", "a\\nb"));
    }

    [Fact]
    public void GroupsNestAsDeepAsAPatternWritesThem()
    {
        string pattern = new string('(', 5000) + "a" + new string(')', 5000);

        Assert.Equal(5000, PureQLText.RegexGroups(pattern));
        Assert.Equal("a", PureQLText.ExtractRegex("bab", pattern, 5000));
    }

    [Theory]
    [InlineData(Grin + "+", Grin + Grin + "a", Grin + Grin)]
    [InlineData(Grin + "{2}", Grin + Grin + Grin, Grin + Grin)]
    [InlineData(Grin + "?a", Grin + "a", Grin + "a")]
    [InlineData(Grin + "*?a", Grin + Grin + "a", Grin + Grin + "a")]
    [InlineData("a" + Grin + "*", "a" + Grin + Grin, "a" + Grin + Grin)]
    public void AQuantifierAfterACharacterOutsideTheBasicMultilingualPlaneRepeatsAllOfIt(
        string pattern,
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.ExtractRegex(operand, pattern));
    }
}
