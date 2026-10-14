namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// String order by code point, the specification's order and SQL's "C"
// collation. The tree orders strings ordinally (string.CompareOrdinal and
// StringComparer.Ordinal), which in UTF-16 differs from code point order only
// between surrogates (U+D800-U+DFFF) and U+E000-U+FFFF: the code points
// surrogates encode sort above every BMP character. Running the tree in
// memory compares by code point instead.
internal sealed class CodePoints : IComparer<string>
{
    private CodePoints() { }

    public static CodePoints Comparer { get; } = new();

    public static int Compare(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        int length = Math.Min(left.Length, right.Length);

        for (int i = 0; i < length; i++)
        {
            if (left[i] != right[i])
            {
                return Weight(left[i]).CompareTo(Weight(right[i]));
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    int IComparer<string>.Compare(string? x, string? y)
    {
        return Compare(x, y);
    }

    private static int Weight(char unit)
    {
        return unit switch
        {
            >= '' => unit - 0x800,
            >= '\uD800' => unit + 0x2000,
            _ => unit,
        };
    }
}
