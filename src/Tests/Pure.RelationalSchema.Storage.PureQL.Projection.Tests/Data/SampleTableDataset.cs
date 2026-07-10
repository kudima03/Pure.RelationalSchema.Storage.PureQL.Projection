using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Minimal IStoredTableDataSet backing the sample dataset: an in-memory
// IQueryable<IRow> plus its ITable schema. The translator consumes tables
// through the synchronous IQueryable path; the async enumerator is provided
// only to satisfy the interface.
internal sealed record SampleTableDataset : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    public SampleTableDataset(ITable tableSchema, IEnumerable<IRow> rows)
    {
        TableSchema = tableSchema;
        _rows = rows.ToArray().AsQueryable();
    }

    public ITable TableSchema { get; }

    public Type ElementType => _rows.ElementType;

    public Expression Expression => _rows.Expression;

    public IQueryProvider Provider => _rows.Provider;

    public async IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        foreach (IRow row in _rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }

        await Task.CompletedTask;
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
