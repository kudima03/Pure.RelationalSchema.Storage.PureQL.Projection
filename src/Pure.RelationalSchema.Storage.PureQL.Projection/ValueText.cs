using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Formats computed values as the canonical invariant cell text understood by
// CellValueExtractor, so computed cells round-trip like stored ones.
internal static class ValueText
{
    internal static string From(bool value)
    {
        return value ? bool.TrueString : bool.FalseString;
    }

    internal static string From(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    internal static string From(Guid value)
    {
        return value.ToString("D");
    }

    internal static string From(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    internal static string From(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
    }

    internal static string From(TimeOnly value)
    {
        return value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
