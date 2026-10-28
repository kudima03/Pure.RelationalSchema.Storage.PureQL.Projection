using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Writes the invariant text a result cell holds: True/False, invariant
// numbers (a decimal in the specification's one spelling: exact, no
// exponent, no trailing zeros in the fraction, no -0), yyyy-MM-dd dates,
// HH:mm:ss times, yyyy-MM-ddTHH:mm:ss datetimes in UTC and lower case uuids;
// fractional seconds are written only when present. Null is empty text, the
// storage layer's NULL.
internal static class CellText
{
    private const string TimeFormat = "HH:mm:ss.FFFFFFF";

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";

    // Dividing by one with the most decimal places gives the same value with
    // the fewest: the scale a decimal carries (1.50, 4.00) is dropped.
    private const decimal One = 1.0000000000000000000000000000m;

    public static string Format(object? value)
    {
        return value switch
        {
            null => string.Empty,
            long number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => (number / One).ToString(CultureInfo.InvariantCulture),
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
