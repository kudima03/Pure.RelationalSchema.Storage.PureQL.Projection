using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A stored table that counts how often its rows are read.
internal sealed class CountedTable(IStoredTableDataSet table) : IStoredTableDataSet
{
    private readonly IStoredTableDataSet _table = table;

    public int Reads { get; private set; }

    public ITable TableSchema => _table.TableSchema;

    public Type ElementType => _table.ElementType;

    public Expression Expression => _table.Expression;

    public IQueryProvider Provider => _table.Provider;

    public IEnumerator<IRow> GetEnumerator()
    {
        Reads++;
        return _table.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        Reads++;
        return _table.GetAsyncEnumerator(cancellationToken);
    }
}
