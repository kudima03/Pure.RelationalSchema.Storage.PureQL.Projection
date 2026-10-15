using System.Reflection;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A result row: one cell per result column, holding the invariant text of the
// value the query computed for it.
internal sealed class ResultRow(IReadOnlyList<IColumn> columns, object?[] values) : IRow
{
    public static ConstructorInfo Constructor { get; } =
        typeof(ResultRow).GetConstructor(
            [typeof(IReadOnlyList<IColumn>), typeof(object[])]
        )!;

    public IReadOnlyDictionary<IColumn, ICell> Cells { get; } =
        new Collections.Generic.Dictionary<int, IColumn, ICell>(
            Enumerable.Range(0, columns.Count),
            index => columns[index],
            index => new Cell(new String(CellText.Format(values[index]))),
            column => new ColumnHash(column)
        );
}
