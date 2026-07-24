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

    // Field resolution (a nonexistent column) and cell-text parsing (a
    // malformed or type-mismatched value) are different failure modes and
    // must not be conflated: an unresolved field always fails fast via
    // GetRequiredCell below, while an empty cell text is the sentinel for
    // SQL NULL and stays a silent null - only genuinely unparseable,
    // non-empty text throws.
    //
    // Empty text maps to null here for the same reason every other typed
    // getter below maps it to null: it is this storage layer's sole NULL
    // sentinel (there is no separate representation for a real, stored
    // empty string), and JoinApplicator.Pad writes exactly this empty
    // sentinel for an outer join's unmatched side. Returning the raw ""
    // instead (as this used to) made a padded string cell read back as a
    // present, non-null value while every other column type already read
    // padding as null - so string min/count wrongly folded in padded rows
    // (issue #167). Aligning this getter with the others makes padded
    // strings NULL for aggregates, count, DISTINCT, ordering and equality
    // alike, since all of those resolve field values through this class.
    internal static string? GetTextValue(IRow row, string entity, string fieldName)
    {
        string? text = GetRequiredCell(row, entity, fieldName).Value.TextValue;
        return text is null || text.Length == 0 ? null : text;
    }

    internal static double? GetDoubleValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : double.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double v
            )
            ? v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid number."
        );
    }

    internal static bool? GetBoolValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : bool.TryParse(text, out bool v)
            ? v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid boolean."
        );
    }

    internal static DateOnly? GetDateOnlyValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : DateOnly.TryParse(text, out DateOnly v)
            ? (DateOnly?)v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid date."
        );
    }

    internal static DateTime? GetDateTimeValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime v
            )
            ? (DateTime?)v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid datetime."
        );
    }

    internal static TimeOnly? GetTimeOnlyValue(
        IRow row,
        string entity,
        string fieldName
    )
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : TimeOnly.TryParse(text, out TimeOnly v)
            ? (TimeOnly?)v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid time."
        );
    }

    internal static Guid? GetGuidValue(IRow row, string entity, string fieldName)
    {
        string? text = GetTextValue(row, entity, fieldName);

        return text is null || text.Length == 0
            ? null
            : Guid.TryParse(text, out Guid v)
            ? (Guid?)v
            : throw new FormatException(
            $"Cell text '{text}' for field '{fieldName}' is not a valid uuid."
        );
    }
}
