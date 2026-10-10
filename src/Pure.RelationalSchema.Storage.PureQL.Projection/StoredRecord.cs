using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A stored table row. Cells are parsed by their column's type when read, so a
// cell the query never reads is never parsed.
internal sealed class StoredRecord(IRow row) : IRecord
{
    private readonly Dictionary<string, KeyValuePair<IColumn, ICell>> _cells =
        row.Cells.ToDictionary(
            cell => cell.Key.Name.TextValue,
            cell => cell,
            StringComparer.Ordinal
        );

    public object? Value(string field)
    {
        KeyValuePair<IColumn, ICell> cell = _cells.TryGetValue(
            field,
            out KeyValuePair<IColumn, ICell> found
        )
            ? found
            : throw new KeyNotFoundException($"Row has no column named '{field}'.");

        return CellText.Parse(cell.Value.Value.TextValue, cell.Key.Type);
    }
}
