using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

public sealed record PureQLProjection : IStoredTableDataSet
{
#pragma warning disable IDE0052 // Remove unread private members

    private readonly IEnumerable<IStoredSchemaDataSet> _datasets;

    private readonly Query _query;

#pragma warning restore IDE0052 // Remove unread private members

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
    {
        _datasets = datasets;
        _query = query;
    }

    public ITable TableSchema => throw new NotImplementedException();

    public Type ElementType => throw new NotImplementedException();

    public Expression Expression => throw new NotImplementedException();

    public IQueryProvider Provider => throw new NotImplementedException();

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
