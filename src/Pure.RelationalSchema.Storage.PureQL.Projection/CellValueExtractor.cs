using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class CellValueExtractor
{
    internal static ICell? GetCell(IRow row, string fieldName)
    {
        foreach (KeyValuePair<IColumn, ICell> kvp in row.Cells)
        {
            if (kvp.Key.Name.TextValue == fieldName)
            {
                return kvp.Value;
            }
        }

        return null;
    }

    internal static ICell GetRequiredCell(IRow row, string fieldName)
    {
        return GetCell(row, fieldName)
            ?? throw new KeyNotFoundException(
                $"Row has no column named '{fieldName}'."
            );
    }

    internal static string? GetTextValue(IRow row, string fieldName)
    {
        return GetCell(row, fieldName)?.Value.TextValue;
    }

    internal static double? GetDoubleValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return
            text is not null
            && double.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double v
            )
            ? v
            : null;
    }

    internal static bool? GetBoolValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return text is not null && bool.TryParse(text, out bool v) ? v : null;
    }

    internal static DateOnly? GetDateOnlyValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return text is not null && DateOnly.TryParse(text, out DateOnly v) ? v : null;
    }

    internal static DateTime? GetDateTimeValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return
            text is not null
            && DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime v
            )
            ? v
            : null;
    }

    internal static TimeOnly? GetTimeOnlyValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return text is not null && TimeOnly.TryParse(text, out TimeOnly v) ? v : null;
    }

    internal static Guid? GetGuidValue(IRow row, string fieldName)
    {
        string? text = GetTextValue(row, fieldName);

        return text is not null && Guid.TryParse(text, out Guid v) ? v : null;
    }
}
