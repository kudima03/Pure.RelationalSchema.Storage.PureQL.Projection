using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// The cell texts of one result column, in row order.
internal static class ResultColumn
{
    public static IReadOnlyList<string> Texts(IEnumerable<IRow> rows, string column)
    {
        return
        [
            .. rows.Select(row =>
                row.Cells.Single(cell => cell.Key.Name.TextValue == column)
                    .Value.Value.TextValue
            ),
        ];
    }
}
