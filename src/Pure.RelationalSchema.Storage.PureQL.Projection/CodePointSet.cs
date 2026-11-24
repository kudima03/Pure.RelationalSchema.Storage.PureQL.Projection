using System.Globalization;
using System.Text;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A set of code points: what a class, an escape such as \d or the dot of a
// regular expression of the specification matches. It is written for .NET's
// regular expressions, which match UTF-16 code units, as an expression that
// takes one whole code point of the set.
internal sealed class CodePointSet
{
    private const int Last = 0x10FFFF;

    private readonly List<(int First, int Last)> _ranges = [];

    // \d: the digits 0 to 9.
    public static CodePointSet Digits => new CodePointSet().With('0', '9');

    // \w: the ASCII letters, the digits and the underscore.
    public static CodePointSet WordCharacters =>
        new CodePointSet().With('0', '9').With('A', 'Z').With('_', '_').With('a', 'z');

    // \s: tab, line feed, vertical tab, form feed, carriage return, space.
    public static CodePointSet WhiteSpace =>
        new CodePointSet().With('\t', '\r').With(' ', ' ');

    // The dot: every code point except a line feed.
    public static CodePointSet NotLineFeed =>
        new CodePointSet().With('\n', '\n').Inverse();

    // The set with the code points from first to last added.
    public CodePointSet With(int first, int last)
    {
        _ranges.Add((first, last));

        return this;
    }

    // The set with the code points of another set added.
    public CodePointSet With(CodePointSet other)
    {
        _ranges.AddRange(other._ranges);

        return this;
    }

    // The code points that are not in the set.
    public CodePointSet Inverse()
    {
        CodePointSet inverse = new();
        int next = 0;

        foreach ((int first, int last) in Merged())
        {
            if (first > next)
            {
                _ = inverse.With(next, first - 1);
            }

            next = last + 1;
        }

        return next > Last ? inverse : inverse.With(next, Last);
    }

    // A .NET regular expression that, where a code point starts, takes one
    // code point of the set. A low surrogate found there has no pair, a high
    // surrogate stands for itself only when no low surrogate follows, and a
    // code point above U+FFFF is its surrogate pair, so the expression never
    // takes half of a pair.
    public string Expression()
    {
        List<(int First, int Last)> ranges = Merged();
        List<string> choices = [];
        string units = Units(
            Within(ranges, 0, 0xD7FF).Concat(Within(ranges, 0xDC00, 0xFFFF))
        );
        string unpaired = Units(Within(ranges, 0xD800, 0xDBFF));

        if (units.Length > 0)
        {
            choices.Add(units);
        }

        if (unpaired.Length > 0)
        {
            choices.Add(unpaired + @"(?![\uDC00-\uDFFF])");
        }

        foreach ((int first, int last) in Within(ranges, 0x10000, Last))
        {
            choices.AddRange(Pairs(first, last));
        }

        return choices.Count == 0 ? "(?!)"
            : choices.Count == 1 && units.Length > 0 ? units
            : "(?:" + string.Join('|', choices) + ")";
    }

    // The ranges of the set in ascending order, with ranges that touch or
    // overlap joined.
    private List<(int First, int Last)> Merged()
    {
        List<(int First, int Last)> merged = [];

        foreach ((int first, int last) in _ranges.Order())
        {
            if (merged.Count > 0 && first <= merged[^1].Last + 1)
            {
                merged[^1] = (merged[^1].First, Math.Max(merged[^1].Last, last));
            }
            else
            {
                merged.Add((first, last));
            }
        }

        return merged;
    }

    // The parts of the ranges that lie from lowest to highest.
    private static IEnumerable<(int First, int Last)> Within(
        List<(int First, int Last)> ranges,
        int lowest,
        int highest
    )
    {
        return ranges
            .Where(range => range.First <= highest && range.Last >= lowest)
            .Select(range =>
                (Math.Max(range.First, lowest), Math.Min(range.Last, highest))
            );
    }

    // A class of UTF-16 code units, or nothing for no ranges.
    private static string Units(IEnumerable<(int First, int Last)> ranges)
    {
        StringBuilder units = new();

        foreach ((int first, int last) in ranges)
        {
            _ = units.Append(Unit(first));

            if (last > first)
            {
                _ = units.Append('-').Append(Unit(last));
            }
        }

        return units.Length == 0
            ? string.Empty
            : units.Insert(0, '[').Append(']').ToString();
    }

    // The surrogate pairs of the code points from first to last, both above
    // U+FFFF: those of the first high surrogate, those of the last one, and
    // every pair of the high surrogates between them.
    private static IEnumerable<string> Pairs(int first, int last)
    {
        int firstHigh = High(first);
        int lastHigh = High(last);

        if (firstHigh == lastHigh)
        {
            yield return Pair(firstHigh, firstHigh, Low(first), Low(last));
            yield break;
        }

        if (Low(first) > 0xDC00)
        {
            yield return Pair(firstHigh, firstHigh, Low(first), 0xDFFF);
            firstHigh++;
        }

        if (Low(last) < 0xDFFF)
        {
            yield return Pair(lastHigh, lastHigh, 0xDC00, Low(last));
            lastHigh--;
        }

        if (firstHigh <= lastHigh)
        {
            yield return Pair(firstHigh, lastHigh, 0xDC00, 0xDFFF);
        }
    }

    private static string Pair(int firstHigh, int lastHigh, int firstLow, int lastLow)
    {
        return Units([(firstHigh, lastHigh)]) + Units([(firstLow, lastLow)]);
    }

    private static int High(int codePoint)
    {
        return 0xD800 + ((codePoint - 0x10000) >> 10);
    }

    private static int Low(int codePoint)
    {
        return 0xDC00 + ((codePoint - 0x10000) & 0x3FF);
    }

    // One UTF-16 code unit as .NET's regular expressions escape it.
    public static string Unit(int unit)
    {
        return @"\u" + unit.ToString("X4", CultureInfo.InvariantCulture);
    }
}
