namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Equality and ordering of runtime values, as the specification defines them
// for every clause: null equals null and sorts first, integer and decimal
// compare by numeric value, strings by code point, uuids by their 32 lower
// case hexadecimal digits and booleans false before true. A runtime value is
// null, long (integer), decimal, string, bool, DateOnly, TimeOnly, DateTime
// (a UTC instant) or Guid.
internal static class Values
{
    public static bool Equal(object? left, object? right)
    {
        return Compare(left, right) == 0;
    }

    public static int Compare(object? left, object? right)
    {
        return (left, right) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (long l, long r) => l.CompareTo(r),
            (long or decimal, long or decimal) => Decimal(left).CompareTo(Decimal(right)),
            (string l, string r) => CompareCodePoints(l, r),
            (bool l, bool r) => l.CompareTo(r),
            (DateOnly l, DateOnly r) => l.CompareTo(r),
            (TimeOnly l, TimeOnly r) => l.CompareTo(r),
            (DateTime l, DateTime r) => l.CompareTo(r),
            (Guid l, Guid r) => string.CompareOrdinal(l.ToString("N"), r.ToString("N")),
            _ => throw new InvalidOperationException(
                $"Values of {left.GetType().Name} and {right.GetType().Name} "
                    + "cannot be compared."
            ),
        };
    }

    public static int Hash(object? value)
    {
        return value switch
        {
            null => 0,
            long number => ((decimal)number).GetHashCode(),
            string text => StringComparer.Ordinal.GetHashCode(text),
            _ => value.GetHashCode(),
        };
    }

    public static decimal Decimal(object value)
    {
        return value is long number ? number : (decimal)value;
    }

    // UTF-16 code units order surrogates (U+D800-U+DFFF) below U+E000-U+FFFF,
    // while the code points they encode sort above every BMP character.
    private static int CompareCodePoints(string left, string right)
    {
        int length = Math.Min(left.Length, right.Length);

        for (int i = 0; i < length; i++)
        {
            if (left[i] != right[i])
            {
                return CodePointWeight(left[i]).CompareTo(CodePointWeight(right[i]));
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CodePointWeight(char unit)
    {
        return unit switch
        {
            >= '' => unit - 0x800,
            >= '\uD800' => unit + 0x2000,
            _ => unit,
        };
    }
}
