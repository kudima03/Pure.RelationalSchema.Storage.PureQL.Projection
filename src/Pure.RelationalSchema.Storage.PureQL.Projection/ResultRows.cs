using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The rows of the main query. The query runs again on every enumeration, so
// the result always reflects the datasets as they are when read.
internal sealed class ResultRows(
    Catalog catalog,
    QueryProgram program,
    IReadOnlyList<IColumn> columns
) : IEnumerable<IRow>
{
    private readonly Catalog _catalog = catalog;

    private readonly QueryProgram _program = program;

    private readonly IReadOnlyList<IColumn> _columns = columns;

    public IEnumerator<IRow> GetEnumerator()
    {
        QueryResult result = new Execution(_catalog, _program.Subqueries).Run(
            _program.Main
        );

        foreach (IReadOnlyList<object?> values in result.Rows)
        {
            yield return new Row(
                new Collections.Generic.Dictionary<int, IColumn, ICell>(
                    Enumerable.Range(0, _columns.Count),
                    index => _columns[index],
                    index => new Cell(
                        new Primitives.String.String(
                            CellText.Format(values[index], result.Columns[index].Type)
                        )
                    ),
                    column => new ColumnHash(column)
                )
            );
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
