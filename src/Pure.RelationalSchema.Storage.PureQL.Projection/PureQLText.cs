using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The string operators of the specification. A string is a sequence of
// Unicode code points: every position, length and match is counted in code
// points and compared code point by code point, with no case folding,
// normalization, collation or locale. The operands are never null: an
// operator that is lifted is null for a null operand before it gets here.
public static class PureQLText
{
    // length: the number of code points, so a character outside the Basic
    // Multilingual Plane counts once and a combining sequence counts each of
    // its code points.
    public static long Length(string operand)
    {
        return Utf16.Count(operand);
    }

    // upper: the default upper case conversion of the Unicode Standard, with
    // the full mappings and no locale, so upper("straße") is "STRASSE".
    public static string Upper(string operand)
    {
        return CaseMapping.Upper(operand);
    }

    // lower: the default lower case conversion of the Unicode Standard, with
    // the full mappings, the rule for a final sigma and no locale.
    public static string Lower(string operand)
    {
        return CaseMapping.Lower(operand);
    }

    // foldCase: the default full case folding of the Unicode Standard, a key
    // under which strings that differ only in case are equal.
    public static string FoldCase(string operand)
    {
        return CaseMapping.Fold(operand);
    }

    // removeDiacritics: the operand decomposed (NFD), every nonspacing mark
    // (general category Mn) dropped, and what is left composed again (NFC).
    // A code point no normalization form may hold (a surrogate without its
    // pair, U+FFFE) is kept where it is, and the text around it is treated
    // on its own.
    public static string RemoveDiacritics(string operand)
    {
        StringBuilder? result = null;
        int start = 0;

        for (int index = 0; index < operand.Length; index++)
        {
            if (IsNeverNormalized(operand, index))
            {
                result ??= new StringBuilder(operand.Length);
                _ = result
                    .Append(WithoutMarks(operand[start..index]))
                    .Append(operand[index]);
                start = index + 1;
            }
        }

        return result is null
            ? WithoutMarks(operand)
            : result.Append(WithoutMarks(operand[start..])).ToString();
    }

    // trim: trimStart, then trimEnd of the result.
    public static string Trim(string operand, string[] removables)
    {
        string[] longestFirst = LongestFirst(removables);
        int start = LeadingRemovables(operand, longestFirst);
        int end = start + TrailingRemovables(operand.AsSpan(start), longestFirst);

        return operand[start..end];
    }

    // trimStart: removes from the start, at each step, the longest removable
    // the string starts with, until none matches. An empty removable never
    // matches.
    public static string TrimStart(string operand, string[] removables)
    {
        return operand[LeadingRemovables(operand, LongestFirst(removables))..];
    }

    // trimEnd: removes from the end, at each step, the longest removable the
    // string ends with, until none matches.
    public static string TrimEnd(string operand, string[] removables)
    {
        return operand[..TrailingRemovables(operand, LongestFirst(removables))];
    }

    // substring without length: the code points from position start, counted
    // from 1, to the end of the string; the empty string when start is past
    // the end.
    public static string Substring(string operand, long start)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(start, 1);

        return operand[Utf16.Advance(operand, 0, start - 1)..];
    }

    // substring: length code points from position start, or as many as the
    // string has from there. It never reads past the string.
    public static string Substring(string operand, long start, long length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(start, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        int from = Utf16.Advance(operand, 0, start - 1);

        return operand[from..Utf16.Advance(operand, from, length)];
    }

    // replace: every occurrence of the literal text pattern replaced by
    // replacement. Occurrences are found from the start and do not overlap,
    // the text put in is not searched again, and an empty pattern occurs
    // nowhere.
    public static string Replace(string operand, string pattern, string replacement)
    {
        int found = Occurrence(operand, pattern, 0);

        if (found < 0)
        {
            return operand;
        }

        StringBuilder result = new();
        int copied = 0;

        while (found >= 0)
        {
            _ = result.Append(operand, copied, found - copied).Append(replacement);
            copied = found + pattern.Length;
            found = Occurrence(operand, pattern, copied);
        }

        return result.Append(operand, copied, operand.Length - copied).ToString();
    }

    // position without start: the position of the first occurrence of
    // pattern, counted in code points from 1, or 0 when there is none.
    public static long Position(string operand, string pattern)
    {
        return Position(operand, pattern, 1);
    }

    // position: the first occurrence of pattern at position start or later,
    // still counted from the start of the string, or 0. An empty pattern is
    // found where the search begins, and a start past the end finds nothing.
    public static long Position(string operand, string pattern, long start)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(start, 1);

        int from = Utf16.Advance(operand, 0, start - 1);

        if (from == operand.Length && Utf16.Count(operand) < start - 1)
        {
            return 0;
        }

        int found =
            pattern.Length == 0 ? from : Occurrence(operand, pattern, from);

        return found < 0 ? 0 : start + Utf16.Count(operand.AsSpan(from, found - from));
    }

    // splitPart: the operand split at every occurrence of delimiter, found
    // as replace finds its pattern, and the part numbered index: from 1, or
    // from the end when index is negative. A part that does not exist is the
    // empty string, and an empty delimiter leaves one part.
    public static string SplitPart(string operand, string delimiter, long index)
    {
        ArgumentOutOfRangeException.ThrowIfZero(index);

        long wanted = index > 0 ? index : Parts(operand, delimiter) + 1 + index;
        int start = 0;
        int end = Occurrence(operand, delimiter, 0);

        for (long part = 1; part < wanted && start >= 0; part++)
        {
            start = end < 0 ? -1 : end + delimiter.Length;
            end = end < 0 ? -1 : Occurrence(operand, delimiter, start);
        }

        return wanted < 1 || start < 0 ? string.Empty
            : end < 0 ? operand[start..]
            : operand[start..end];
    }

    // padStart: repetitions of fill before the operand until the string has
    // length code points, the last one cut to fit. Padding never cuts the
    // operand, and an empty fill adds nothing.
    public static string PadStart(string operand, long length, string fill)
    {
        return Padding(operand, length, fill) + operand;
    }

    // padEnd: repetitions of fill after the operand until the string has
    // length code points, the last one cut to fit.
    public static string PadEnd(string operand, long length, string fill)
    {
        return operand + Padding(operand, length, fill);
    }

    // repeat: the operand written count times, the empty string for 0.
    public static string Repeat(string operand, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return operand.Length == 0 ? operand : Repeated(operand, count, 0);
    }

    // reverse: the code points in the opposite order, so the marks of a
    // combining sequence come out before their base character.
    public static string Reverse(string operand)
    {
        return string.Create(operand.Length, operand, WriteReversed);
    }

    // concatWith: the operands that are not null joined with separator
    // between them; the empty string when every operand is null.
    public static string ConcatWith(string separator, string?[] values)
    {
        return string.Join(separator, values.Where(value => value is not null));
    }

    // matchesRegex: whether the pattern matches anywhere in the operand.
    public static bool MatchesRegex(string operand, string pattern)
    {
        return RegexDialect.Of(pattern).First(operand, 0).Success;
    }

    // extractRegex without group: the text of the leftmost match, which may
    // be the empty string, or null when nothing matches.
    public static string? ExtractRegex(string operand, string pattern)
    {
        Match match = RegexDialect.Of(pattern).First(operand, 0);

        return match.Success ? match.Value : null;
    }

    // extractRegex: what the group numbered group, from 1 by its opening
    // parenthesis, captured in the leftmost match: its last repetition, or
    // null when nothing matches or the group took no part in the match.
    public static string? ExtractRegex(string operand, string pattern, long group)
    {
        RegexDialect dialect = RegexDialect.Of(pattern);

        ArgumentOutOfRangeException.ThrowIfLessThan(group, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(group, dialect.Groups);

        Group captured = dialect.First(operand, 0).Groups[(int)group];

        return captured.Success ? captured.Value : null;
    }

    // replaceRegex: every match, found from left to right without overlap,
    // replaced by the literal text replacement. After an empty match the
    // search moves on by one code point.
    public static string ReplaceRegex(string operand, string pattern, string replacement)
    {
        RegexDialect dialect = RegexDialect.Of(pattern);
        Match match = dialect.First(operand, 0);

        if (!match.Success)
        {
            return operand;
        }

        StringBuilder result = new();
        int copied = 0;

        while (match.Success)
        {
            _ = result
                .Append(operand, copied, match.Index - copied)
                .Append(replacement);
            copied = match.Index + match.Length;

            if (match.Length == 0 && copied == operand.Length)
            {
                break;
            }

            match = dialect.First(
                operand,
                match.Length == 0 ? copied + Utf16.Width(operand, copied) : copied
            );
        }

        return result.Append(operand, copied, operand.Length - copied).ToString();
    }

    // The number of capturing groups of a pattern of the dialect of the
    // specification; text that is not such a pattern is a FormatException.
    public static int RegexGroups(string pattern)
    {
        return RegexDialect.Of(pattern).Groups;
    }

    // The index of the first occurrence of pattern at index from or later,
    // or -1. The pattern is matched code point by code point, so an
    // occurrence never starts or ends inside a surrogate pair, and an empty
    // pattern occurs nowhere.
    private static int Occurrence(string operand, string pattern, int from)
    {
        if (pattern.Length == 0)
        {
            return -1;
        }

        int found = operand.IndexOf(pattern, from, StringComparison.Ordinal);

        while (
            found >= 0
            && (
                Utf16.SplitsPair(operand, found)
                || Utf16.SplitsPair(operand, found + pattern.Length)
            )
        )
        {
            found = operand.IndexOf(pattern, found + 1, StringComparison.Ordinal);
        }

        return found;
    }

    // The number of parts the delimiter splits the operand into: one more
    // than its occurrences.
    private static long Parts(string operand, string delimiter)
    {
        long parts = 1;
        int found = Occurrence(operand, delimiter, 0);

        while (found >= 0)
        {
            parts++;
            found = Occurrence(operand, delimiter, found + delimiter.Length);
        }

        return parts;
    }

    // The removables that can match, the longest first: of the removables a
    // string starts or ends with, the longest is then the first one found.
    private static string[] LongestFirst(string[] removables)
    {
        return
        [
            .. removables
                .Where(removable => removable.Length > 0)
                .OrderByDescending(removable => removable.Length),
        ];
    }

    // The index after the removables at the start of the text.
    private static int LeadingRemovables(ReadOnlySpan<char> text, string[] longestFirst)
    {
        int start = 0;
        int removed = 0;

        while (removed >= 0)
        {
            start += removed;
            removed = -1;

            foreach (string removable in longestFirst)
            {
                if (
                    text[start..].StartsWith(removable, StringComparison.Ordinal)
                    && !Utf16.SplitsPair(text, start + removable.Length)
                )
                {
                    removed = removable.Length;
                    break;
                }
            }
        }

        return start;
    }

    // The index before the removables at the end of the text.
    private static int TrailingRemovables(ReadOnlySpan<char> text, string[] longestFirst)
    {
        int end = text.Length;
        int removed = 0;

        while (removed >= 0)
        {
            end -= removed;
            removed = -1;

            foreach (string removable in longestFirst)
            {
                if (
                    text[..end].EndsWith(removable, StringComparison.Ordinal)
                    && !Utf16.SplitsPair(text, end - removable.Length)
                )
                {
                    removed = removable.Length;
                    break;
                }
            }
        }

        return end;
    }

    // The repetitions of fill that bring the operand to length code points,
    // the last one cut to fit; nothing when the operand is long enough.
    private static string Padding(string operand, long length, string fill)
    {
        long present = Utf16.Count(operand);

        if (length <= present || fill.Length == 0)
        {
            return string.Empty;
        }

        long missing = length - present;
        long codePoints = Utf16.Count(fill);

        return Repeated(
            fill,
            missing / codePoints,
            Utf16.Advance(fill, 0, missing % codePoints)
        );
    }

    // The text written times times and then its first tail code units. A
    // result longer than a string can hold fails as .NET fails for one.
    private static string Repeated(string text, long times, int tail)
    {
        int length =
            times > (int.MaxValue - tail) / text.Length
                ? int.MaxValue
                : (int)(times * text.Length) + tail;

        return string.Create(length, (text, tail), WriteRepeated);
    }

    private static void WriteRepeated(Span<char> result, (string Text, int Tail) repeated)
    {
        int whole = result.Length - repeated.Tail;

        for (int at = 0; at < whole; at += repeated.Text.Length)
        {
            repeated.Text.CopyTo(result[at..]);
        }

        repeated.Text.AsSpan(0, repeated.Tail).CopyTo(result[whole..]);
    }

    private static void WriteReversed(Span<char> result, string text)
    {
        int index = text.Length;

        while (index > 0)
        {
            int width = Utf16.WidthBefore(text, index);

            index -= width;
            text.AsSpan(index, width).CopyTo(result[(text.Length - index - width)..]);
        }
    }

    // Whether the code unit at index is a code point that normalization
    // refuses: a surrogate without its pair, or U+FFFE.
    private static bool IsNeverNormalized(string text, int index)
    {
        return char.IsSurrogate(text[index])
            ? Utf16.Width(text, index) == 1 && !Utf16.SplitsPair(text, index)
            : text[index] == '\uFFFE';
    }

    private static string WithoutMarks(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        StringBuilder kept = new(decomposed.Length);
        int index = 0;

        while (index < decomposed.Length)
        {
            int width = Utf16.Width(decomposed, index);
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(
                Utf16.CodePoint(decomposed, index, width)
            );

            if (category != UnicodeCategory.NonSpacingMark)
            {
                _ = kept.Append(decomposed, index, width);
            }

            index += width;
        }

        return kept.ToString().Normalize(NormalizationForm.FormC);
    }
}
