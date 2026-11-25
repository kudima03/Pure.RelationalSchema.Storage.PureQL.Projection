namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The string operators as the specification defines them under Strings:
// every position and length is counted in code points, every match is code
// point by code point, and each edge the specification lists has a test.
public sealed class PureQLTextTests
{
    private const string Grin = "\uD83D\uDE00";

    [Theory]
    [InlineData("", 0)]
    [InlineData("hello", 5)]
    [InlineData("stra\u00DFe", 6)]
    [InlineData("a" + Grin, 2)]
    [InlineData(Grin + Grin + Grin, 3)]
    [InlineData("e\u0301", 2)]
    public void LengthIsTheNumberOfCodePoints(string operand, long length)
    {
        Assert.Equal(length, PureQLText.Length(operand));
    }

    [Theory]
    [InlineData("Cr\u00E8me Br\u00FBl\u00E9e", "Creme Brulee")]
    [InlineData("Vi\u1EC7t Nam", "Viet Nam")]
    [InlineData("\u03AC\u03BB\u03C6\u03B1", "\u03B1\u03BB\u03C6\u03B1")]
    [InlineData("\u0141\u00F3d\u017A", "\u0141odz")]
    [InlineData("\u0439", "\u0438")]
    [InlineData("\u00C5ngstr\u00F6m", "Angstrom")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    public void RemoveDiacriticsDropsTheAccents(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.RemoveDiacritics(operand));
    }

    [Theory]
    [InlineData("\u00F8")]
    [InlineData("\u0142")]
    [InlineData("\u0111")]
    [InlineData("\u00E6")]
    [InlineData("\u00DF")]
    [InlineData("\uFB01")]
    [InlineData("\u00B2")]
    [InlineData(Grin)]
    public void RemoveDiacriticsKeepsALetterThatHasNoDecomposition(string operand)
    {
        Assert.Equal(operand, PureQLText.RemoveDiacritics(operand));
    }

    [Theory]
    // Hebrew points.
    [InlineData("\u05E9\u05B8\u05C1\u05DC\u05D5\u05B9\u05DD", "\u05E9\u05DC\u05D5\u05DD")]
    // Arabic vowel marks.
    [InlineData("\u0643\u064E\u062A\u064E\u0628\u064E", "\u0643\u062A\u0628")]
    // A Thai tone mark.
    [InlineData("\u0E19\u0E49\u0E33", "\u0E19\u0E33")]
    // The virama of Devanagari.
    [InlineData("\u0915\u094D\u0937", "\u0915\u0937")]
    public void RemoveDiacriticsDropsEveryNonspacingMark(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.RemoveDiacritics(operand));
    }

    [Theory]
    // Decomposed on the way in, composed on the way out.
    [InlineData("A\u030A\u0328", "A")]
    [InlineData("\u1E69", "s")]
    // A Hangul syllable written as its jamo is composed: the result is NFC.
    [InlineData("\u1112\u1161\u11AB", "\uD55C")]
    // The ohm sign is not in NFC: its canonical form is the Greek omega.
    [InlineData("\u2126", "\u03A9")]
    [InlineData("e\u0301\u0302x\u0303", "ex")]
    public void RemoveDiacriticsGivesTheFormNfcWhateverTheFormOfTheOperand(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.RemoveDiacritics(operand));
    }

    [Theory]
    [InlineData("Cr\u00E8me Br\u00FBl\u00E9e")]
    [InlineData("\u1112\u1161\u11AB e\u0301")]
    [InlineData("\u0141\u00F3d\u017A \u05E9\u05B8\u05C1")]
    public void RemovingDiacriticsTwiceChangesNothing(string operand)
    {
        string once = PureQLText.RemoveDiacritics(operand);

        Assert.Equal(once, PureQLText.RemoveDiacritics(once));
    }

    [Fact]
    public void RemoveDiacriticsKeepsTheCase()
    {
        Assert.Equal("ECOLE Ecole", PureQLText.RemoveDiacritics("\u00C9COLE \u00C9cole"));
    }

    [Fact]
    public void ASearchKeyThatIgnoresCaseAndAccentsFoldsFirst()
    {
        Assert.Equal(
            PureQLText.RemoveDiacritics(PureQLText.FoldCase("CR\u00C8ME")),
            PureQLText.RemoveDiacritics(PureQLText.FoldCase("cre\u0300me"))
        );
    }

    [Theory]
    [InlineData("000120", new[] { "0" }, "120")]
    [InlineData("abab", new[] { "a", "ab" }, "")]
    [InlineData("abab", new[] { "ab", "a" }, "")]
    [InlineData("xxhixx", new[] { "x" }, "hixx")]
    [InlineData("  hi", new[] { " " }, "hi")]
    [InlineData("\t hi", new[] { " " }, "\t hi")]
    [InlineData("\u00A0hi", new[] { " " }, "\u00A0hi")]
    [InlineData("\t \u00A0hi", new[] { " ", "\t", "\u00A0" }, "hi")]
    [InlineData("Ab", new[] { "a" }, "Ab")]
    [InlineData("hello", new[] { "ello" }, "hello")]
    [InlineData("", new[] { "a" }, "")]
    public void TrimStartRemovesTheLongestRemovableUntilNoneMatches(
        string operand,
        string[] removables,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.TrimStart(operand, removables));
    }

    [Theory]
    [InlineData("12.50 USD", new[] { " USD", "0", "." }, "12.5")]
    [InlineData("12.50 USD", new[] { ".", "0", " USD" }, "12.5")]
    [InlineData("abab", new[] { "b", "ab" }, "")]
    [InlineData("xxhixx", new[] { "x" }, "xxhi")]
    [InlineData("hi\n", new[] { " " }, "hi\n")]
    [InlineData("hello", new[] { "hell" }, "hello")]
    [InlineData("", new[] { "a" }, "")]
    public void TrimEndRemovesTheLongestRemovableUntilNoneMatches(
        string operand,
        string[] removables,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.TrimEnd(operand, removables));
    }

    [Theory]
    [InlineData("  hi  ", new[] { " " }, "hi")]
    [InlineData("xxhixx", new[] { "x" }, "hi")]
    [InlineData("xyhiyx", new[] { "x", "y" }, "hi")]
    [InlineData("aaa", new[] { "a" }, "")]
    [InlineData("abab", new[] { "ab" }, "")]
    [InlineData("aXa", new[] { "a", "aXa" }, "")]
    [InlineData("hi", new[] { "x" }, "hi")]
    // trimStart first: it takes "ab", so the "ba" at the end has only its "a" left.
    [InlineData("aba", new[] { "ab", "ba" }, "a")]
    public void TrimIsTrimStartThenTrimEndOfTheResult(
        string operand,
        string[] removables,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Trim(operand, removables));
        Assert.Equal(
            expected,
            PureQLText.TrimEnd(PureQLText.TrimStart(operand, removables), removables)
        );
    }

    [Fact]
    public void ARepeatedRemovableChangesNothing()
    {
        Assert.Equal("1", PureQLText.Trim("0010", ["0", "0", "0"]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RemovablesThatAreAllEmptyRemoveNothing(int count)
    {
        string[] removables = [.. Enumerable.Repeat("", count)];

        Assert.Equal(" a ", PureQLText.Trim(" a ", removables));
        Assert.Equal(" a ", PureQLText.TrimStart(" a ", removables));
        Assert.Equal(" a ", PureQLText.TrimEnd(" a ", removables));
    }

    [Fact]
    public void AnEmptyRemovableIsIgnoredNextToOthers()
    {
        Assert.Equal("a", PureQLText.Trim(" a ", ["", " "]));
    }

    [Theory]
    [InlineData(Grin + "a" + Grin, new[] { Grin }, "a")]
    [InlineData("e\u0301e", new[] { "e" }, "\u0301")]
    [InlineData("\u00E9a\u00E9", new[] { "e" }, "\u00E9a\u00E9")]
    public void ARemovableIsMatchedCodePointByCodePoint(
        string operand,
        string[] removables,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Trim(operand, removables));
    }

    [Theory]
    [InlineData("hello", 1, "hello")]
    [InlineData("hello", 4, "lo")]
    [InlineData("hello", 5, "o")]
    [InlineData("hello", 6, "")]
    [InlineData("hello", 7, "")]
    [InlineData("hello", long.MaxValue, "")]
    [InlineData("", 1, "")]
    [InlineData("a" + Grin + "b", 2, Grin + "b")]
    [InlineData("a" + Grin + "b", 3, "b")]
    public void SubstringWithoutLengthRunsToTheEndOfTheString(
        string operand,
        long start,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Substring(operand, start));
    }

    [Theory]
    [InlineData("hello", 4, 10, "lo")]
    [InlineData("hello", 2, 0, "")]
    [InlineData("hello", 2, 3, "ell")]
    [InlineData("hello", 1, 3, "hel")]
    [InlineData("hello", 1, 5, "hello")]
    [InlineData("hello", 1, 99, "hello")]
    [InlineData("hello", 6, 1, "")]
    [InlineData("hello", 9, 0, "")]
    [InlineData("hello", 5, long.MaxValue, "o")]
    [InlineData("hello", long.MaxValue, long.MaxValue, "")]
    [InlineData("", 1, 0, "")]
    [InlineData("a" + Grin + "b", 2, 1, Grin)]
    [InlineData(Grin + Grin + Grin, 2, 2, Grin + Grin)]
    [InlineData("e\u0301x", 2, 1, "\u0301")]
    public void SubstringNeverReadsPastTheString(
        string operand,
        long start,
        long length,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Substring(operand, start, length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void SubstringWithAStartBelowOneIsAnError(long start)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.Substring("hello", start)
        );
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.Substring("hello", start, 1)
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void SubstringWithANegativeLengthIsAnError(long length)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.Substring("hello", 1, length)
        );
    }

    [Theory]
    [InlineData("aaa", "aa", "b", "ba")]
    [InlineData("a", "a", "aa", "aa")]
    [InlineData("banana", "an", "", "ba")]
    [InlineData("banana", "", "x", "banana")]
    [InlineData("", "", "x", "")]
    [InlineData("", "a", "x", "")]
    [InlineData("banana", "x", "y", "banana")]
    [InlineData("banana", "banana", "", "")]
    [InlineData("banana", "a", "o", "bonono")]
    [InlineData("abcabc", "bc", "-", "a-a-")]
    [InlineData("Aa", "a", "b", "Ab")]
    [InlineData("\u00E9e", "e", "x", "\u00E9x")]
    [InlineData("e\u0301", "\u00E9", "x", "e\u0301")]
    [InlineData("50%_", "%", "\\", "50\\_")]
    [InlineData("a" + Grin + "b" + Grin, Grin, "-", "a-b-")]
    [InlineData("a.b", ".", Grin, "a" + Grin + "b")]
    public void ReplaceReplacesEveryOccurrenceFromTheStartWithoutOverlap(
        string operand,
        string pattern,
        string replacement,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Replace(operand, pattern, replacement));
    }

    [Theory]
    [InlineData("banana", "an", 2)]
    [InlineData("banana", "x", 0)]
    [InlineData("banana", "b", 1)]
    [InlineData("banana", "a", 2)]
    [InlineData("banana", "na", 3)]
    [InlineData("banana", "banana", 1)]
    [InlineData("banana", "bananas", 0)]
    [InlineData("banana", "", 1)]
    [InlineData("", "", 1)]
    [InlineData("", "a", 0)]
    [InlineData("Banana", "b", 0)]
    [InlineData(Grin + "ab", "b", 3)]
    [InlineData("a" + Grin + Grin, Grin, 2)]
    [InlineData("e\u0301x", "x", 3)]
    public void PositionIsCountedInCodePointsFromOneAndZeroWhenAbsent(
        string operand,
        string pattern,
        long expected
    )
    {
        Assert.Equal(expected, PureQLText.Position(operand, pattern));
        Assert.Equal(expected, PureQLText.Position(operand, pattern, 1));
    }

    [Theory]
    [InlineData("banana", "an", 3, 4)]
    [InlineData("banana", "an", 2, 2)]
    [InlineData("banana", "an", 4, 4)]
    [InlineData("banana", "an", 5, 0)]
    [InlineData("banana", "a", 6, 6)]
    [InlineData("banana", "a", 7, 0)]
    [InlineData("banana", "a", 8, 0)]
    [InlineData("banana", "a", long.MaxValue, 0)]
    [InlineData(Grin + "a" + Grin + "a", "a", 3, 4)]
    [InlineData(Grin + "a" + Grin + "a", Grin, 2, 3)]
    public void PositionWithAStartBeginsThereAndStillCountsFromTheStart(
        string operand,
        string pattern,
        long start,
        long expected
    )
    {
        Assert.Equal(expected, PureQLText.Position(operand, pattern, start));
    }

    [Theory]
    [InlineData("abc", 1, 1)]
    [InlineData("abc", 3, 3)]
    [InlineData("abc", 4, 4)]
    [InlineData("abc", 5, 0)]
    [InlineData("abc", long.MaxValue, 0)]
    [InlineData("", 1, 1)]
    [InlineData("", 2, 0)]
    [InlineData(Grin + Grin, 3, 3)]
    [InlineData(Grin + Grin, 4, 0)]
    public void AnEmptyPatternIsFoundWhereTheSearchBeginsUnlessThatIsPastTheEnd(
        string operand,
        long start,
        long expected
    )
    {
        Assert.Equal(expected, PureQLText.Position(operand, "", start));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void PositionWithAStartBelowOneIsAnError(long start)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.Position("banana", "an", start)
        );
    }

    [Theory]
    [InlineData("banana", "an")]
    [InlineData("banana", "x")]
    [InlineData("banana", "")]
    [InlineData("", "")]
    public void APositionAboveZeroSaysThatThePatternIsContained(
        string operand,
        string pattern
    )
    {
        Assert.Equal(
            operand.Contains(pattern, StringComparison.Ordinal),
            PureQLText.Position(operand, pattern) > 0
        );
    }

    [Theory]
    [InlineData("a-b-c", "-", 1, "a")]
    [InlineData("a-b-c", "-", 2, "b")]
    [InlineData("a-b-c", "-", 3, "c")]
    [InlineData("a-b-c", "-", 4, "")]
    [InlineData("a-b-c", "-", 5, "")]
    [InlineData("a-b-c", "-", long.MaxValue, "")]
    [InlineData("a-b-c", "-", -1, "c")]
    [InlineData("a-b-c", "-", -2, "b")]
    [InlineData("a-b-c", "-", -3, "a")]
    [InlineData("a-b-c", "-", -4, "")]
    [InlineData("a-b-c", "-", long.MinValue, "")]
    [InlineData("a--c", "-", 2, "")]
    [InlineData("a--c", "-", 3, "c")]
    [InlineData("a--c", "-", -2, "")]
    [InlineData("-a-", "-", 1, "")]
    [InlineData("-a-", "-", 2, "a")]
    [InlineData("-a-", "-", 3, "")]
    [InlineData("-a-", "-", -1, "")]
    [InlineData("-a-", "-", -3, "")]
    [InlineData("abc", "-", 1, "abc")]
    [InlineData("abc", "-", -1, "abc")]
    [InlineData("abc", "-", 2, "")]
    [InlineData("abc", "-", -2, "")]
    [InlineData("", "-", 1, "")]
    [InlineData("", "-", -1, "")]
    [InlineData("one, two, three", ", ", 2, "two")]
    [InlineData("one, two, three", ", ", -1, "three")]
    // Occurrences are found from the start and do not overlap.
    [InlineData("aaa", "aa", 1, "")]
    [InlineData("aaa", "aa", 2, "a")]
    [InlineData("aaa", "aa", -1, "a")]
    [InlineData("aaa", "aa", -2, "")]
    [InlineData("a" + Grin + "b" + Grin + "c", Grin, 2, "b")]
    [InlineData("a" + Grin + "b" + Grin + "c", Grin, -1, "c")]
    public void SplitPartReturnsThePartNumberedFromOneOrFromTheEnd(
        string operand,
        string delimiter,
        long index,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.SplitPart(operand, delimiter, index));
    }

    [Theory]
    [InlineData("a-b-c", 1, "a-b-c")]
    [InlineData("a-b-c", -1, "a-b-c")]
    [InlineData("a-b-c", 2, "")]
    [InlineData("a-b-c", -2, "")]
    [InlineData("", 1, "")]
    public void AnEmptyDelimiterOccursNowhereSoTheStringIsOnePart(
        string operand,
        long index,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.SplitPart(operand, "", index));
    }

    [Fact]
    public void SplitPartWithAnIndexOfZeroIsAnError()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.SplitPart("a-b-c", "-", 0)
        );
    }

    [Theory]
    [InlineData("7", 3, "0", "007")]
    [InlineData("abc", 6, "xy", "xyxabc")]
    [InlineData("abc", 7, "xy", "xyxyabc")]
    [InlineData("abc", 4, "xy", "xabc")]
    [InlineData("abc", 3, "xy", "abc")]
    [InlineData("abc", 2, "xy", "abc")]
    [InlineData("abc", 0, "xy", "abc")]
    [InlineData("abc", -5, "xy", "abc")]
    [InlineData("abc", long.MinValue, "xy", "abc")]
    [InlineData("abc", 6, "", "abc")]
    [InlineData("", 3, "ab", "aba")]
    [InlineData("", 0, "ab", "")]
    [InlineData(Grin, 3, "0", "00" + Grin)]
    [InlineData("a", 4, Grin + "b", Grin + "b" + Grin + "a")]
    [InlineData("a", 3, "e\u0301", "e\u0301a")]
    [InlineData("a", 2, "e\u0301", "ea")]
    public void PadStartAddsRepetitionsOfFillBeforeTheOperand(
        string operand,
        long length,
        string fill,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.PadStart(operand, length, fill));
    }

    [Theory]
    [InlineData("7", 3, "0", "700")]
    [InlineData("abc", 6, "xy", "abcxyx")]
    [InlineData("abc", 7, "xy", "abcxyxy")]
    [InlineData("abc", 3, "xy", "abc")]
    [InlineData("abc", 1, "xy", "abc")]
    [InlineData("abc", 0, "xy", "abc")]
    [InlineData("abc", -1, "xy", "abc")]
    [InlineData("abc", 6, "", "abc")]
    [InlineData("", 2, "ab", "ab")]
    [InlineData(Grin, 3, "0", Grin + "00")]
    [InlineData("a", 4, Grin + "b", "a" + Grin + "b" + Grin)]
    public void PadEndAddsRepetitionsOfFillAfterTheOperand(
        string operand,
        long length,
        string fill,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.PadEnd(operand, length, fill));
    }

    [Theory]
    [InlineData("ab", 3, "ababab")]
    [InlineData("ab", 1, "ab")]
    [InlineData("ab", 0, "")]
    [InlineData("", 3, "")]
    [InlineData("", long.MaxValue, "")]
    [InlineData("", 0, "")]
    [InlineData(Grin, 2, Grin + Grin)]
    public void RepeatWritesTheOperandCountTimes(
        string operand,
        long count,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Repeat(operand, count));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void RepeatWithANegativeCountIsAnError(long count)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PureQLText.Repeat("ab", count)
        );
    }

    // The specification leaves the longest string unspecified: a result no
    // .NET string can hold fails at once, as .NET fails for one.
    [Fact]
    public void AResultLongerThanAStringCanHoldFails()
    {
        _ = Assert.Throws<OutOfMemoryException>(() =>
            PureQLText.Repeat("ab", long.MaxValue)
        );
        _ = Assert.Throws<OutOfMemoryException>(() =>
            PureQLText.PadStart("a", long.MaxValue, "xy")
        );
        _ = Assert.Throws<OutOfMemoryException>(() =>
            PureQLText.PadEnd("a", long.MaxValue, "xy")
        );
    }

    [Theory]
    [InlineData("abc", "cba")]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("ab", "ba")]
    [InlineData("a" + Grin + "b", "b" + Grin + "a")]
    [InlineData(Grin + "\uD83D\uDE01", "\uD83D\uDE01" + Grin)]
    // The marks of a combining sequence come out before their base character.
    [InlineData("e\u0301x", "x\u0301e")]
    public void ReverseReturnsTheCodePointsInTheOppositeOrder(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Reverse(operand));
    }

    [Fact]
    public void ConcatWithLeavesOutTheOperandsThatAreNull()
    {
        Assert.Equal("Ann Lee", PureQLText.ConcatWith(" ", ["Ann", null, "Lee"]));
        Assert.Equal("Ann", PureQLText.ConcatWith(" ", [null, "Ann", null]));
    }

    [Fact]
    public void ConcatWithIsTheEmptyStringWhenEveryOperandIsNull()
    {
        Assert.Equal("", PureQLText.ConcatWith(", ", [null, null]));
    }

    [Fact]
    public void ConcatWithKeepsAnEmptyStringAsAnOperand()
    {
        Assert.Equal("a--b", PureQLText.ConcatWith("-", ["a", "", "b"]));
        Assert.Equal("-", PureQLText.ConcatWith("-", ["", ""]));
    }

    [Fact]
    public void ConcatWithTakesAnEmptySeparator()
    {
        Assert.Equal("ab", PureQLText.ConcatWith("", ["a", null, "b"]));
    }
}
