using System.Collections.Frozen;
using System.Text;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The default case conversion and case folding of the Unicode Standard
// (toUppercase, toLowercase, toCasefold): the full mappings, so one code point
// may become several, and no locale, so no language-specific rule applies.
// The only rule that reads the context is Final_Sigma of toLowercase. A code
// point without a mapping, a surrogate without its pair included, is kept.
internal static class CaseMapping
{
    private const int Sigma = 0x03A3;

    private const string FinalSigma = "\u03C2";

    private static readonly FrozenDictionary<int, string> UpperCase = Table(
        UnicodeCaseData.UpperRuns,
        UnicodeCaseData.UpperSequences
    );

    private static readonly FrozenDictionary<int, string> LowerCase = Table(
        UnicodeCaseData.LowerRuns,
        UnicodeCaseData.LowerSequences
    );

    private static readonly FrozenDictionary<int, string> Folding = Table(
        UnicodeCaseData.FoldRuns,
        UnicodeCaseData.FoldSequences
    );

    // toUppercase: every code point replaced by its full upper case mapping.
    public static string Upper(string text)
    {
        return Mapped(text, UpperCase, false);
    }

    // toLowercase: every code point replaced by its full lower case mapping,
    // and a capital sigma at the end of a word by the final sigma.
    public static string Lower(string text)
    {
        return Mapped(text, LowerCase, true);
    }

    // toCasefold: every code point replaced by its full case folding.
    public static string Fold(string text)
    {
        return Mapped(text, Folding, false);
    }

    private static string Mapped(
        string text,
        FrozenDictionary<int, string> table,
        bool finalSigma
    )
    {
        StringBuilder? mapped = null;
        int index = 0;

        while (index < text.Length)
        {
            int width = Utf16.Width(text, index);
            int codePoint = Utf16.CodePoint(text, index, width);
            string? mapping =
                finalSigma && codePoint == Sigma && IsFinal(text, index)
                    ? FinalSigma
                    : table.GetValueOrDefault(codePoint);

            if (mapping is null)
            {
                _ = mapped?.Append(text, index, width);
            }
            else
            {
                mapped ??= new StringBuilder(text, 0, index, text.Length + 8);
                _ = mapped.Append(mapping);
            }

            index += width;
        }

        return mapped is null ? text : mapped.ToString();
    }

    // The condition Final_Sigma of the Unicode Standard for the sigma at
    // index: before it, a cased letter and then any number of case-ignorable
    // code points; after it, no such run that leads to a cased letter. A code
    // point that is both cased and case-ignorable is skipped as ignorable.
    private static bool IsFinal(string text, int index)
    {
        int before = index;
        bool cased = false;

        while (before > 0 && !cased)
        {
            int width = Utf16.WidthBefore(text, before);
            int codePoint = Utf16.CodePoint(text, before - width, width);

            if (!Contains(UnicodeCaseData.CaseIgnorable, codePoint))
            {
                if (!Contains(UnicodeCaseData.Cased, codePoint))
                {
                    return false;
                }

                cased = true;
            }

            before -= width;
        }

        int after = index + 1;

        while (cased && after < text.Length)
        {
            int width = Utf16.Width(text, after);
            int codePoint = Utf16.CodePoint(text, after, width);

            if (!Contains(UnicodeCaseData.CaseIgnorable, codePoint))
            {
                return !Contains(UnicodeCaseData.Cased, codePoint);
            }

            after += width;
        }

        return cased;
    }

    // Whether a code point is in one of the ranges, written as the first and
    // the last code point of each in ascending order.
    private static bool Contains(ReadOnlySpan<int> ranges, int codePoint)
    {
        int low = 0;
        int high = (ranges.Length / 2) - 1;

        while (low <= high)
        {
            int middle = (low + high) / 2;

            if (codePoint < ranges[2 * middle])
            {
                high = middle - 1;
            }
            else if (codePoint > ranges[(2 * middle) + 1])
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    // The mappings of one conversion by code point: the runs of code points
    // that map to one code point, and those that map to a sequence.
    private static FrozenDictionary<int, string> Table(
        ReadOnlySpan<int> runs,
        ReadOnlySpan<int> sequences
    )
    {
        Dictionary<int, string> table = [];

        for (int run = 0; run < runs.Length; run += 4)
        {
            for (int item = 0; item < runs[run + 1]; item++)
            {
                int codePoint = runs[run] + (item * runs[run + 2]);
                table[codePoint] = char.ConvertFromUtf32(codePoint + runs[run + 3]);
            }
        }

        int at = 0;

        while (at < sequences.Length)
        {
            StringBuilder mapping = new();

            for (int item = 0; item < sequences[at + 1]; item++)
            {
                _ = mapping.Append(char.ConvertFromUtf32(sequences[at + 2 + item]));
            }

            table[sequences[at]] = mapping.ToString();
            at += 2 + sequences[at + 1];
        }

        return table.ToFrozenDictionary();
    }
}
