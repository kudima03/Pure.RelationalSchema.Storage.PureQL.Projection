using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A stored table a LINQ provider reads, as a database's table is read by
// the query a provider translates: its rows are held in memory, but a query
// over it is answered by its RecordingProvider, and enumerating the table
// itself (a read of the whole table) is counted.
internal sealed class ProvidedTable(IStoredTableDataSet table, RecordingProvider provider)
    : IStoredTableDataSet
{
    private readonly IStoredTableDataSet _table = table;

    private readonly RecordingProvider _provider = provider;

    public int Reads { get; private set; }

    public IEnumerable<IRow> Rows => _table.AsEnumerable();

    public ITable TableSchema => _table.TableSchema;

    public Type ElementType => typeof(IRow);

    public Expression Expression => Expression.Constant(this, typeof(IQueryable<IRow>));

    public IQueryProvider Provider => _provider;

    // Every table of a schema dataset, read through the provider.
    public static IStoredSchemaDataSet Schema(
        IStoredSchemaDataSet dataset,
        RecordingProvider provider
    )
    {
        return new StoredSchemaDataSet(
            dataset.Schema,
            [.. dataset.Values.Select(table => new ProvidedTable(table, provider))]
        );
    }

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
