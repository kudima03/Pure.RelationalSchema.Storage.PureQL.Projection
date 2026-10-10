using System.Globalization;
using Pure.RelationalSchema.Abstractions.ColumnType;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Reads and writes the invariant text a stored cell holds: True/False,
// invariant numbers, yyyy-MM-dd dates, HH:mm:ss times, yyyy-MM-ddTHH:mm:ss
// datetimes and lower case uuids. Empty text is the storage layer's NULL.
// A datetime without an offset is read as UTC, and every datetime is written
// in UTC; fractional seconds are written only when present.
internal static class CellText
{
    private const string TimeFormat = "HH:mm:ss.FFFFFFF";

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";

    public static object? Parse(string? text, IColumnType columnType)
    {
        return string.IsNullOrEmpty(text)
            ? null
            : ValueTypes.OfColumn(columnType) switch
            {
                "integer" => long.Parse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture
                ),
                "decimal" => decimal.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture
                ),
                "string" => text,
                "boolean" => bool.Parse(text),
                "date" => DateOnly.Parse(text, CultureInfo.InvariantCulture),
                "time" => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
                "datetime" => DateTime.Parse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                ),
                "uuid" => Guid.Parse(text),
                _ => throw new NotSupportedException(
                    $"Column type '{columnType.Name.TextValue}' has no PureQL type."
                ),
            };
    }

    public static string Format(object? value, IType type)
    {
        return ValueTypes.Normalize(value, type) switch
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
