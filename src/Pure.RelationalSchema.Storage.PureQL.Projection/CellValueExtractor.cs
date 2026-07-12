using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Resolves a field reference against a row. A column tagged with the
// reference's entity wins; otherwise the base table's (untagged) same-named
// column; otherwise any same-named column. The fallbacks keep unqualified
// rows (no joins, projected rows) and alias-style entities working.
internal static class CellValueExtractor
{
    internal static ICell? GetCell(IRow row, string entity, string fieldName)
    {
        ICell? unqualified = null;
        ICell? sameName = null;

        foreach (KeyValuePair<IColumn, ICell> kvp in row.Cells)
        {
            if (kvp.Key.Name.TextValue != fieldName)
            {
                continue;
            }

            if (kvp.Key is not QualifiedColumn qualified)
            {
                unqualified ??= kvp.Value;
            }
            else if (qualified.Entity == entity)
            {
                return kvp.Value;
            }
            else
            {
                sameName ??= kvp.Value;
            }
        }

        return unqualified ?? sameName;
    }

    internal static ICell GetRequiredCell(IRow row, string entity, string fieldName)
    {
        return GetCell(row, entity, fieldName)
            ?? throw new KeyNotFoundException(
                $"Row has no column named '{fieldName}'."
            );
    }

    internal static string? GetTextValue(IRow row, string entity, string fieldName)
    {
        return GetCell(row, entity, fieldName)?.Value.TextValue;
    }

    internal static double? GetDoubleValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

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

    internal static bool? GetBoolValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is not null && bool.TryParse(text, out bool v) ? v : null;
    }

    internal static DateOnly? GetDateOnlyValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is not null && DateOnly.TryParse(text, out DateOnly v) ? v : null;
    }

    internal static DateTime? GetDateTimeValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

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

    internal static TimeOnly? GetTimeOnlyValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is not null && TimeOnly.TryParse(text, out TimeOnly v) ? v : null;
    }

    internal static Guid? GetGuidValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is not null && Guid.TryParse(text, out Guid v) ? v : null;
    }
}
