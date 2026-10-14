using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Writes the invariant text a result cell holds: True/False, invariant
// numbers (a decimal as the double the result column holds), yyyy-MM-dd
// dates, HH:mm:ss times, yyyy-MM-ddTHH:mm:ss datetimes in UTC and lower case
// uuids; fractional seconds are written only when present. Null is empty
// text, the storage layer's NULL.
internal static class CellText
{
    private const string TimeFormat = "HH:mm:ss.FFFFFFF";

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";

    public static string Format(object? value)
    {
        return value switch
        {
            null => string.Empty,
            long number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => ((double)number).ToString(CultureInfo.InvariantCulture),
            string text => text,
            bool flag => flag ? bool.TrueString : bool.FalseString,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString(TimeFormat, CultureInfo.InvariantCulture),
            DateTime instant => instant.ToString(
                DateTimeFormat,
                CultureInfo.InvariantCulture
            ),
            Guid uuid => uuid.ToString("D"),
            object other => throw new InvalidOperationException(
                $"A {other.GetType().Name} value has no cell text."
            ),
        };
    }
}
