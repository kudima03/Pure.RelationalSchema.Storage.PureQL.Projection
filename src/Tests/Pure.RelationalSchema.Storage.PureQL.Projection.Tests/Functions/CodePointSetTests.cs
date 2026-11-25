namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The classes of a regular expression: one code point of a set or of a
// range, in code point order, or one that is not in it, with characters
// outside the Basic Multilingual Plane as members like any other.
public sealed class CodePointSetTests
{
    private const string Grin = "\uD83D\uDE00";

    private const string Wink = "\uD83D\uDE09";

    [Theory]
    [InlineData("[abc]", "b", true)]
    [InlineData("[abc]", "d", false)]
    [InlineData("[^abc]", "b", false)]
    [InlineData("[^abc]", "d", true)]
    [InlineData("[a-z0-9]", "m", true)]
    [InlineData("[a-z0-9]", "5", true)]
    [InlineData("[a-z0-9]", "M", false)]
    [InlineData("[a-z0-9]", "-", false)]
    [InlineData("[a-cb-d]", "d", true)]
    [InlineData("[a-cd-f]", "e", true)]
    [InlineData("[a-ce-g]", "d", false)]
    [InlineData("[aaa]", "a", true)]
    [InlineData("[a-a]", "a", true)]
    [InlineData("[a-a]", "b", false)]
    [InlineData("[^a-zA-Z '-]", "\u00E9", true)]
    [InlineData("[^a-zA-Z '-]", "-", false)]
    [InlineData("[ -~]", "\u007F", false)]
    [InlineData("[\u00E0-\u00FF]", "\u00E9", true)]
    [InlineData("[\u00E0-\u00FF]", "e", false)]
    public void AClassIsOneCodePointOfTheSetOrOfARangeOrOneThatIsNotInIt(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    [InlineData("[-a]", "-", true)]
    [InlineData("[-a]", "a", true)]
    [InlineData("[a-]", "-", true)]
    [InlineData("[^-a]", "-", false)]
    [InlineData("[^-a]", "b", true)]
    [InlineData("[^a-]", "-", false)]
    [InlineData("[-]", "-", true)]
    [InlineData("[a\\-z]", "-", true)]
    [InlineData("[a\\-z]", "m", false)]
    [InlineData("[\\d-]", "-", true)]
    [InlineData("[\\d-]", "7", true)]
    // A hyphen that is first is also the start of a range: from - to a.
    [InlineData("[--a]", "A", true)]
    [InlineData("[--a]", "b", false)]
    [InlineData("[+--]", ",", true)]
    [InlineData("[+--]", ".", false)]
    public void AHyphenStandsForItselfWhenItIsEscapedFirstOrLast(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    [InlineData("[\\]\\[\\\\\\^\\-]", "]", true)]
    [InlineData("[\\]\\[\\\\\\^\\-]", "[", true)]
    [InlineData("[\\]\\[\\\\\\^\\-]", "\\", true)]
    [InlineData("[\\]\\[\\\\\\^\\-]", "^", true)]
    [InlineData("[\\]\\[\\\\\\^\\-]", "-", true)]
    [InlineData("[\\]\\[\\\\\\^\\-]", "a", false)]
    [InlineData("[*+?{}().|$]", "*", true)]
    [InlineData("[*+?{}().|$]", ".", true)]
    [InlineData("[*+?{}().|$]", "$", true)]
    [InlineData("[*+?{}().|$]", "a", false)]
    [InlineData("[\\/\\-\\.]", "/", true)]
    [InlineData("[\\.]", "a", false)]
    [InlineData("[.]", "a", false)]
    [InlineData("[\\^a]", "^", true)]
    public void InsideAClassTheSpecialCharactersAreThemselves(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    [InlineData("[\\d\\s,;]", "7", true)]
    [InlineData("[\\d\\s,;]", " ", true)]
    [InlineData("[\\d\\s,;]", ";", true)]
    [InlineData("[\\d\\s,;]", "a", false)]
    [InlineData("[\\Da]", "a", true)]
    [InlineData("[\\D5]", "5", true)]
    [InlineData("[\\D5]", "6", false)]
    [InlineData("[\\W\\d]", "7", true)]
    [InlineData("[\\W\\d]", "a", false)]
    [InlineData("[\\W\\d]", Grin, true)]
    [InlineData("[^\\W\\d]", "a", true)]
    [InlineData("[^\\W\\d]", "7", false)]
    [InlineData("[^\\Sx]", " ", true)]
    [InlineData("[^\\Sx]", "x", false)]
    [InlineData("[\\t\\n\\r]", "\u000A", true)]
    [InlineData("[\\t\\n\\r]", "n", false)]
    public void TheEscapesThatStandForASetWorkInsideAClass(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    [InlineData("[\\d\\D]", "a")]
    [InlineData("[\\d\\D]", "7")]
    [InlineData("[\\d\\D]", "\u000A")]
    [InlineData("[\\d\\D]", Grin)]
    [InlineData("[\\s\\S]", "\uFFFF")]
    [InlineData("[\\w\\W]", "\uDBFF\uDFFF")]
    public void ASetAndItsNegationTogetherAreEveryCodePoint(
        string pattern,
        string operand
    )
    {
        Assert.True(PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    [InlineData("[^\\d\\D]")]
    [InlineData("[^\\s\\S]")]
    [InlineData("[^\\w\\W]")]
    public void AClassThatLeavesOutEveryCodePointMatchesNothing(string pattern)
    {
        Assert.False(PureQLText.MatchesRegex("a7 \u000A" + Grin, pattern));
        Assert.True(PureQLText.MatchesRegex("a", pattern + "*"));
        Assert.Equal("a", PureQLText.ReplaceRegex("a", pattern, "-"));
    }

    [Theory]
    [InlineData("[" + Grin + "]", Grin, true)]
    [InlineData("[" + Grin + "]", Wink, false)]
    [InlineData("[" + Grin + "]", "a", false)]
    [InlineData("[a" + Grin + Wink + "]", Wink, true)]
    [InlineData("[a" + Grin + Wink + "]", "a", true)]
    [InlineData("[a" + Grin + Wink + "]", "\uD83D\uDE01", false)]
    [InlineData("[^" + Grin + "]", Grin, false)]
    [InlineData("[^" + Grin + "]", Wink, true)]
    [InlineData("[^" + Grin + "]", "a", true)]
    [InlineData("[^a]", Grin, true)]
    [InlineData("[^a]", "\uDBFF\uDFFF", true)]
    public void ACharacterOutsideTheBasicMultilingualPlaneIsAMemberLikeAnyOther(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
    }

    [Theory]
    // U+1F600 to U+1F64F: within the pairs of one high surrogate.
    [InlineData("[\uD83D\uDE00-\uD83D\uDE4F]", "\uD83D\uDDFF", false)]
    [InlineData("[\uD83D\uDE00-\uD83D\uDE4F]", "\uD83D\uDE00", true)]
    [InlineData("[\uD83D\uDE00-\uD83D\uDE4F]", "\uD83D\uDE4F", true)]
    [InlineData("[\uD83D\uDE00-\uD83D\uDE4F]", "\uD83D\uDE50", false)]
    [InlineData("[\uD83D\uDE00-\uD83D\uDE4F]", "\uD83C\uDE00", false)]
    // U+1F600 to U+20010: the end of one high surrogate, all of the ones
    // between, and the start of the last.
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD83D\uDDFF", false)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD83D\uDE00", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD83D\uDFFF", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD83E\uDC00", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD83F\uDFFF", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD840\uDC00", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD840\uDC10", true)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC11]", "\uD840\uDC12", false)]
    [InlineData("[\uD83D\uDE00-\uD840\uDC10]", "\uD841\uDC00", false)]
    // U+1F600 to U+1F800: no high surrogate lies between the two.
    [InlineData("[\uD83D\uDE00-\uD83E\uDC00]", "\uD83D\uDFFF", true)]
    [InlineData("[\uD83D\uDE00-\uD83E\uDC00]", "\uD83E\uDC00", true)]
    [InlineData("[\uD83D\uDE00-\uD83E\uDC00]", "\uD83E\uDC01", false)]
    // U+10000 to U+103FF: every pair of one high surrogate.
    [InlineData("[\uD800\uDC00-\uD800\uDFFF]", "\uD800\uDC00", true)]
    [InlineData("[\uD800\uDC00-\uD800\uDFFF]", "\uD800\uDFFF", true)]
    [InlineData("[\uD800\uDC00-\uD800\uDFFF]", "\uD801\uDC00", false)]
    // U+10000 to U+10FFFF: every code point above U+FFFF.
    [InlineData("[\uD800\uDC00-\uDBFF\uDFFF]", "\uD800\uDC00", true)]
    [InlineData("[\uD800\uDC00-\uDBFF\uDFFF]", "\uDBFF\uDFFF", true)]
    [InlineData("[\uD800\uDC00-\uDBFF\uDFFF]", "\uFFFF", false)]
    // U+10400 to U+107FF and U+10800 to U+10BFF: from the first pair of a
    // high surrogate, and to the last pair of one.
    [InlineData("[\uD801\uDC00-\uD802\uDC05]", "\uD801\uDFFF", true)]
    [InlineData("[\uD801\uDC00-\uD802\uDC05]", "\uD802\uDC06", false)]
    [InlineData("[\uD801\uDC05-\uD802\uDFFF]", "\uD801\uDC04", false)]
    [InlineData("[\uD801\uDC05-\uD802\uDFFF]", "\uD802\uDFFF", true)]
    // U+007A to U+1F600 and U+FFFF to U+10000: across the end of the Basic
    // Multilingual Plane.
    [InlineData("[z-\uD83D\uDE00]", "z", true)]
    [InlineData("[z-\uD83D\uDE00]", "y", false)]
    [InlineData("[z-\uD83D\uDE00]", "\uFFFF", true)]
    [InlineData("[z-\uD83D\uDE00]", "\uD800\uDC00", true)]
    [InlineData("[z-\uD83D\uDE00]", "\uD83D\uDE00", true)]
    [InlineData("[z-\uD83D\uDE00]", "\uD83D\uDE01", false)]
    [InlineData("[\uFFFF-\uD800\uDC00]", "\uFFFE", false)]
    [InlineData("[\uFFFF-\uD800\uDC00]", "\uFFFF", true)]
    [InlineData("[\uFFFF-\uD800\uDC00]", "\uD800\uDC00", true)]
    [InlineData("[\uFFFF-\uD800\uDC00]", "\uD800\uDC01", false)]
    public void ARangeIsInCodePointOrderAlsoAboveTheBasicMultilingualPlane(
        string pattern,
        string operand,
        bool expected
    )
    {
        Assert.Equal(expected, PureQLText.MatchesRegex(operand, "^" + pattern + "$"));
        Assert.Equal(
            !expected,
            PureQLText.MatchesRegex(operand, "^[^" + pattern[1..] + "$")
        );
    }

    [Fact]
    public void AClassTakesOneWholeCodePointAtATime()
    {
        Assert.Equal(
            Grin + Wink,
            PureQLText.ExtractRegex("a" + Grin + Wink + "b", "[^ab]+")
        );
        Assert.Equal(
            Wink,
            PureQLText.ExtractRegex("a" + Grin + Wink + "b", "[^ab" + Grin + "]+")
        );
        Assert.Equal(
            "a-b",
            PureQLText.ReplaceRegex(
                "a" + Grin + Wink + "b",
                "[" + Grin + "-" + Wink + "]{2}",
                "-"
            )
        );
        Assert.Equal(2, PureQLText.Length(PureQLText.ExtractRegex(Grin + Wink, "..")!));
    }
}
