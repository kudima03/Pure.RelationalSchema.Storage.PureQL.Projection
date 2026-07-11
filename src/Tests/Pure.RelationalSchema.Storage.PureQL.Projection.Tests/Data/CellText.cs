using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Formats a typed .NET value as the canonical invariant text that a stored
// cell holds, chosen so the translator's CellValueExtractor (TryParse with
// InvariantCulture) round-trips it back to the same value. The projection
// stores every cell as an IString, so this is the single source of the on-disk
// text representation for the sample dataset.
internal static class CellText
{
    internal static string From(bool value)
    {
        return value ? "True" : "False";
    }

    internal static string From(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    internal static string From(string value)
    {
        return value;
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

    internal static string From(Guid value)
    {
        return value.ToString();
    }
}
