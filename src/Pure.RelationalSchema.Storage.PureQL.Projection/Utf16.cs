namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A string is a sequence of Unicode code points held as UTF-16. A high
// surrogate followed by a low one is one code point; every other code unit is
// a code point of its own, a surrogate without its pair included, so text
// that is not well formed is still a sequence of code points.
internal static class Utf16
{
    // The number of code units of the code point that starts at index.
    public static int Width(ReadOnlySpan<char> text, int index)
    {
        return
            index + 1 < text.Length
            && char.IsHighSurrogate(text[index])
            && char.IsLowSurrogate(text[index + 1])
            ? 2
            : 1;
    }

    // The number of code units of the code point that ends before index.
    public static int WidthBefore(ReadOnlySpan<char> text, int index)
    {
        return
            index > 1
            && char.IsLowSurrogate(text[index - 1])
            && char.IsHighSurrogate(text[index - 2])
            ? 2
            : 1;
    }

    // The code point that starts at index and takes width code units.
    public static int CodePoint(ReadOnlySpan<char> text, int index, int width)
    {
        return width == 2
            ? char.ConvertToUtf32(text[index], text[index + 1])
            : text[index];
    }

    // Whether index is between the two code units of a surrogate pair, where
    // no code point starts or ends.
    public static bool SplitsPair(ReadOnlySpan<char> text, int index)
    {
        return index > 0
            && index < text.Length
            && char.IsHighSurrogate(text[index - 1])
            && char.IsLowSurrogate(text[index]);
    }

    // The number of code points of the text.
    public static long Count(ReadOnlySpan<char> text)
    {
        long count = 0;

        for (int index = 0; index < text.Length; index += Width(text, index))
        {
            count++;
        }

        return count;
    }

    // The index that count code points after index from, or the length of the
    // text when it has fewer.
    public static int Advance(ReadOnlySpan<char> text, int from, long count)
    {
        int index = from;

        for (long passed = 0; passed < count && index < text.Length; passed++)
        {
            index += Width(text, index);
        }

        return index;
    }
}
