using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeStoredSchemaDataset : IStoredSchemaDataSet
{
    private readonly IStoredSchemaDataSet _inner;

    public FakeStoredSchemaDataset(ISchema schema)
        : this(schema, new FakeStoredTablesDatasets(schema.Tables)) { }

    public FakeStoredSchemaDataset(
        ISchema schema,
        IEnumerable<IStoredTableDataSet> datasets
    )
        : this(
            new StoredSchemaDataset(
                schema,
                new Collections.Generic.Dictionary<
                    IStoredTableDataSet,
                    ITable,
                    IStoredTableDataSet
                >(datasets, x => x.TableSchema, x => x, x => new TableHash(x))
            )
        )
    { }

    private FakeStoredSchemaDataset(IStoredSchemaDataSet inner)
    {
        _inner = inner;
    }

    public IStoredTableDataSet this[ITable key] => _inner[key];

    public ISchema Schema => _inner.Schema;

    public IEnumerable<ITable> Keys => _inner.Keys;

    public IEnumerable<IStoredTableDataSet> Values => _inner.Values;

    public int Count => _inner.Count;

    public bool ContainsKey(ITable key)
    {
        return _inner.ContainsKey(key);
    }

    public IEnumerator<KeyValuePair<ITable, IStoredTableDataSet>> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    public bool TryGetValue(
        ITable key,
        [MaybeNullWhen(false)] out IStoredTableDataSet value
    )
    {
        return TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
