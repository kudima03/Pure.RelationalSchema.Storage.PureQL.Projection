using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A regular expression of the dialect of the specification, ready to match:
// checked, translated for .NET's backtracking engine so that it matches by
// code point, case-sensitive and without a culture, and kept for the next
// row and the next query that use the same pattern.
internal sealed class RegexDialect
{
    // The patterns a process keeps translated; past it the cache starts over,
    // so patterns that arrive as parameters cannot fill the memory.
    private const int Kept = 1024;

    private static readonly ConcurrentDictionary<string, RegexDialect> Known = new(
        StringComparer.Ordinal
    );

    private readonly Regex _expression;

    private RegexDialect(string pattern)
    {
        RegexTranslation translation = new(pattern);

        _expression = new Regex(translation.Expression, RegexOptions.CultureInvariant);
        Groups = translation.Groups;
    }

    // The number of capturing groups, numbered from 1 by their opening
    // parentheses.
    public int Groups { get; }

    // The pattern, translated once; text that is not a pattern of the dialect
    // is a FormatException.
    public static RegexDialect Of(string pattern)
    {
        if (Known.TryGetValue(pattern, out RegexDialect? known))
        {
            return known;
        }

        RegexDialect dialect = new(pattern);

        if (Known.Count >= Kept)
        {
            Known.Clear();
        }

        return Known.GetOrAdd(pattern, dialect);
    }

    // The leftmost match that starts at index from or later, found as a
    // backtracking matcher finds it. A match starts where a code point
    // starts, never inside a surrogate pair.
    public Match First(string operand, int from)
    {
        Match match = _expression.Match(operand, from);

        while (match.Success && Utf16.SplitsPair(operand, match.Index))
        {
            match = _expression.Match(operand, match.Index + 1);
        }

        return match;
    }
}
