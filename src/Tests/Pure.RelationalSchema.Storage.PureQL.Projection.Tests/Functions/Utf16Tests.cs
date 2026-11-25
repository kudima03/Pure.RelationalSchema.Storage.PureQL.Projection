namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// Text that is not well formed: a surrogate without its pair is one code
// point of its own in every operator, a surrogate pair is never taken apart,
// and nothing fails or hangs. The rows are written in the tests, not in
// attributes: the metadata of an attribute is UTF-8, which cannot hold a
// surrogate without its pair.
public sealed class Utf16Tests
{
    private const string High = "\uD83D";

    private const string Low = "\uDE00";

    private const string Grin = High + Low;

    [Fact]
    public void ASurrogateWithoutItsPairIsOneCodePoint()
    {
        (string Operand, long Length)[] rows =
        [
            (High, 1),
            (Low, 1),
            (Low + High, 2),
            (High + High, 2),
            (Low + Low, 2),
            (High + Grin, 2),
            (Grin + Low, 2),
            ("a" + High + "b" + Low + "c", 5),
        ];

        Assert.All(rows, row => Assert.Equal(row.Length, PureQLText.Length(row.Operand)));
    }

    [Fact]
    public void SubstringCountsASurrogateWithoutItsPairAsOnePosition()
    {
        Assert.Equal(Grin + "b", PureQLText.Substring(High + Grin + "b", 2));
        Assert.Equal(High, PureQLText.Substring(High + Grin + "b", 1, 1));
        Assert.Equal(Grin, PureQLText.Substring(High + Grin + "b", 2, 1));
        Assert.Equal(Low + "b", PureQLText.Substring(Grin + Low + "b", 2));
        Assert.Equal(Low, PureQLText.Substring("a" + Low, 2, 5));
    }

    [Fact]
    public void ReverseKeepsEachCodePointWhole()
    {
        Assert.Equal("b" + High + "a", PureQLText.Reverse("a" + High + "b"));
        Assert.Equal("b" + Low + "a", PureQLText.Reverse("a" + Low + "b"));
        Assert.Equal(Grin + High, PureQLText.Reverse(High + Grin));
        Assert.Equal(Low + Grin, PureQLText.Reverse(Grin + Low));
        Assert.Equal(High + High, PureQLText.Reverse(High + High));
    }

    [Fact]
    public void ReplaceNeverMatchesHalfOfASurrogatePair()
    {
        (string Operand, string Pattern, string Expected)[] rows =
        [
            (Grin, High, Grin),
            (Grin, Low, Grin),
            (High + Grin, High, "x" + Grin),
            (Grin + Low, Low, Grin + "x"),
            (Grin + High + Grin + Low, High, Grin + "x" + Grin + Low),
            ("a" + Grin, "a" + High, "a" + Grin),
            (Grin + "a", Low + "a", Grin + "a"),
            ("a" + High + "a" + Grin, "a" + High, "xa" + Grin),
        ];

        Assert.All(
            rows,
            row =>
                Assert.Equal(
                    row.Expected,
                    PureQLText.Replace(row.Operand, row.Pattern, "x")
                )
        );
    }

    [Fact]
    public void PositionNeverFindsHalfOfASurrogatePair()
    {
        Assert.Equal(0, PureQLText.Position(Grin, High));
        Assert.Equal(0, PureQLText.Position(Grin, Low));
        Assert.Equal(2, PureQLText.Position(Grin + Low, Low));
        Assert.Equal(1, PureQLText.Position(High + Grin, High));
        Assert.Equal(3, PureQLText.Position(Grin + Grin + High, High));
        Assert.Equal(3, PureQLText.Position(High + Grin + High, High, 2));
    }

    [Fact]
    public void SplitPartNeverSplitsASurrogatePair()
    {
        Assert.Equal(Grin + "a", PureQLText.SplitPart(Grin + "a" + High + "b", High, 1));
        Assert.Equal("b", PureQLText.SplitPart(Grin + "a" + High + "b", High, 2));
        Assert.Equal("b", PureQLText.SplitPart(Grin + "a" + High + "b", High, -1));
        Assert.Equal("", PureQLText.SplitPart(Grin + "a" + High + "b", High, 3));
    }

    [Fact]
    public void TrimNeverRemovesHalfOfASurrogatePair()
    {
        Assert.Equal(Grin, PureQLText.Trim(Grin, [High, Low]));
        Assert.Equal(Grin, PureQLText.TrimStart(High + High + Grin, [High]));
        Assert.Equal(Grin, PureQLText.TrimEnd(Grin + Low + Low, [Low]));
        Assert.Equal(Grin + Grin, PureQLText.Trim(Grin + Grin, [High, Low]));
        Assert.Equal("", PureQLText.Trim(Low + High, [High, Low]));
    }

    // "a" and a high surrogate are the start of the string in code units,
    // not in code points: only the "a" is removed.
    [Fact]
    public void ARemovableThatWouldSplitAPairGivesWayToAShorterOne()
    {
        Assert.Equal(Grin, PureQLText.TrimStart("a" + Grin, ["a" + High, "a"]));
        Assert.Equal(Grin, PureQLText.TrimEnd(Grin + "a", [Low + "a", "a"]));
        Assert.Equal("a" + Grin, PureQLText.TrimStart("a" + Grin, ["a" + High]));
        Assert.Equal(Grin + "a", PureQLText.TrimEnd(Grin + "a", [Low + "a"]));
    }

    [Fact]
    public void PaddingCountsAndCutsByCodePoint()
    {
        Assert.Equal(High + High + "x", PureQLText.PadStart("x", 3, High));
        Assert.Equal("x" + Low + "a" + Low, PureQLText.PadEnd("x", 4, Low + "a"));
        Assert.Equal(High + "x", PureQLText.PadStart(High + "x", 2, "0"));
        Assert.Equal("0" + Low + "x", PureQLText.PadStart(Low + "x", 3, "0"));
        Assert.Equal(High + Low + High, PureQLText.Repeat(High, 1) + Low + High);
    }

    [Fact]
    public void TheCaseConversionsKeepASurrogateWithoutItsPair()
    {
        Assert.Equal("A" + High + "B", PureQLText.Upper("a" + High + "b"));
        Assert.Equal("a" + Low + "b", PureQLText.Lower("A" + Low + "B"));
        Assert.Equal("a" + High + "ss", PureQLText.FoldCase("A" + High + "\u00DF"));
        Assert.Equal(Low + High, PureQLText.Upper(Low + High));
        Assert.Equal(
            "\u03C3" + High + "\u03C3",
            PureQLText.Lower("\u03A3" + High + "\u03A3")
        );
    }

    [Fact]
    public void RemoveDiacriticsKeepsACodePointNoNormalizationFormHolds()
    {
        (string Operand, string Expected)[] rows =
        [
            ("e\u0301" + High + "e\u0301", "e" + High + "e"),
            (Low + "\u00E9", Low + "e"),
            ("\u00E9" + High, "e" + High),
            (High, High),
            (Low + High, Low + High),
            (High + High + "\u00E9" + Low + Low, High + High + "e" + Low + Low),
            // A mark after a surrogate without its pair is a nonspacing mark
            // all the same.
            (High + "\u0301a", High + "a"),
            // A surrogate pair is a code point like any other.
            (Grin + "\u00E9" + Grin, Grin + "e" + Grin),
            (High + Grin + "\u00E9", High + Grin + "e"),
            (Grin + Low + "\u00E9", Grin + Low + "e"),
            // U+FFFE is in no normalization form either.
            ("\uFFFE\u00E9", "\uFFFEe"),
            ("\u00E9\uFFFE\u00E9\uFFFE", "e\uFFFEe\uFFFE"),
            ("\uFFFF\u00E9", "\uFFFFe"),
        ];

        Assert.All(
            rows,
            row => Assert.Equal(row.Expected, PureQLText.RemoveDiacritics(row.Operand))
        );
    }

    [Fact]
    public void ARegularExpressionNeverMatchesHalfOfASurrogatePair()
    {
        (string Operand, string Pattern, string Expected)[] rows =
        [
            (High + "a" + Low, ".", "---"),
            (High + "a" + Low, "[^a]", "-a-"),
            (High + "a" + Low, "\\W", "-a-"),
            (High + "a" + Low, "x*", "-" + High + "-a-" + Low + "-"),
            (High + Grin + Low, ".", "---"),
            (High + Grin + Low, "", "-" + High + "-" + Grin + "-" + Low + "-"),
            (Grin, High, Grin),
            (Grin, Low, Grin),
            (Grin, "[" + High + "]", Grin),
            (Grin, "[" + Low + "]", Grin),
            (Grin, "[" + High + Low + "]", "-"),
            (Grin + Low, Low, Grin + "-"),
            (High + Grin, High, "-" + Grin),
            (High + Grin, High + "+", "-" + Grin),
            (High + High + Grin, High + "+", "-" + Grin),
            (Grin + Low + Low, Low + "{2}", Grin + "-"),
            (Grin + Low + Grin, "[" + Low + "-" + Low + "]", Grin + "-" + Grin),
            ("a" + High, "[\uD800-\uDBFF]", "a-"),
            ("a" + Grin, "[\uD800-\uDBFF]", "a" + Grin),
            ("a" + Low, "[\uD800-\uDFFF]", "a-"),
            ("a" + Grin, "[\uD800-\uDFFF]", "a" + Grin),
            ("a" + Grin + High + Low, "[\uD7FF-\uE000]", "a" + Grin + Grin),
            ("a" + Grin + Low + High, "[\uD7FF-\uE000]", "a" + Grin + "--"),
        ];

        Assert.All(
            rows,
            row =>
                Assert.Equal(
                    row.Expected,
                    PureQLText.ReplaceRegex(row.Operand, row.Pattern, "-")
                )
        );
    }

    [Fact]
    public void AMatchNeverStartsOrEndsInsideASurrogatePair()
    {
        Assert.Equal(Low, PureQLText.ExtractRegex(Grin + Low, Low));
        Assert.Equal(High, PureQLText.ExtractRegex(High + Grin, High + "+"));
        Assert.Equal(Low, PureQLText.ExtractRegex(Grin + Grin + Low + "x", "(.)x", 1));
        Assert.Null(PureQLText.ExtractRegex(Grin + Grin, Low));
        Assert.Null(PureQLText.ExtractRegex(Grin + Grin, High));
        Assert.False(PureQLText.MatchesRegex(Grin, "^.{2}$"));
        Assert.True(PureQLText.MatchesRegex(High + High, "^.{2}$"));
        Assert.True(PureQLText.MatchesRegex(Low + High, "^.{2}$"));
    }

    [Fact]
    public void ALongRunOfSurrogatesIsReadInLinearTime()
    {
        string highs = PureQLText.Repeat(High, 200000);
        string lows = PureQLText.Repeat(Low, 200000);

        Assert.Equal(200000, PureQLText.Length(highs));
        Assert.Equal(highs, PureQLText.Reverse(highs));
        Assert.Equal(highs, PureQLText.RemoveDiacritics(highs));
        Assert.Equal(lows, PureQLText.Lower(lows));
        Assert.Equal(0, PureQLText.Position(lows + highs, Grin));
        Assert.Equal(400001, PureQLText.Length(PureQLText.ReplaceRegex(lows, "", "-")));
    }
}
