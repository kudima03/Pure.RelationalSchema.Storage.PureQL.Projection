using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Readable, materialized view over a PureQLProjection result. Enumerates the
// projection once and exposes each row as a column-name -> cell-text map so
// tests can assert on plain expected values (and typed getters parse the same
// way the translator's CellValueExtractor does).
internal sealed class ProjectionResult
{
    public ProjectionResult(IStoredTableDataSet projection)
    {
        List<ResultRow> rows = [];

        foreach (IRow row in projection)
        {
            Dictionary<string, string?> cells = [];

            foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
            {
                cells[cell.Key.Name.TextValue] = cell.Value.Value.TextValue;
            }

            rows.Add(new ResultRow(cells));
        }

        Rows = rows;
    }

    public int Count => Rows.Count;

    public IReadOnlyList<string> ColumnNames =>
        Rows.Count == 0 ? [] : [.. Rows[0].ColumnNames];

    public ResultRow Row(int index)
    {
        return Rows[index];
    }

    public IReadOnlyList<ResultRow> Rows { get; }

    public IReadOnlyList<string?> Column(string name)
    {
        return [.. Rows.Select(row => row[name])];
    }
}

internal sealed class ResultRow(IReadOnlyDictionary<string, string?> cells)
{
    private readonly IReadOnlyDictionary<string, string?> _cells = cells;

    public IEnumerable<string> ColumnNames => _cells.Keys;

    public bool Has(string column)
    {
        return _cells.ContainsKey(column);
    }

    public string? this[string column] =>
        _cells.TryGetValue(column, out string? value)
            ? value
            : throw new KeyNotFoundException(
                $"Result row has no column '{column}'. Present: "
                    + string.Join(", ", _cells.Keys)
            );

    public double? Double(string column)
    {
        return this[column] is string text
            && double.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double value
            )
            ? value
            : null;
    }

    public bool? Bool(string column)
    {
        return this[column] is string text && bool.TryParse(text, out bool value)
            ? value
            : null;
    }

    public DateOnly? Date(string column)
    {
        return this[column] is string text && DateOnly.TryParse(text, out DateOnly value)
            ? value
            : null;
    }

    public DateTime? DateTime(string column)
    {
        return this[column] is string text
            && System.DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime value
            )
            ? value
            : null;
    }

    public TimeOnly? Time(string column)
    {
        return this[column] is string text && TimeOnly.TryParse(text, out TimeOnly value)
            ? value
            : null;
    }

    public Guid? Uuid(string column)
    {
        return this[column] is string text && Guid.TryParse(text, out Guid value)
            ? value
            : null;
    }
}
