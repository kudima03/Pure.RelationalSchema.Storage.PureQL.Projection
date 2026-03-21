using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

public sealed record PureQLProjection : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
    {
        TableSchema = new TableFromQuery(query);
        _rows = new RowsFromDatasets(datasets, query);
    }

    public ITable TableSchema { get; }

    public Type ElementType => _rows.ElementType;

    public Expression Expression => _rows.Expression;

    public IQueryProvider Provider => _rows.Provider;

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return _rows.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
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
