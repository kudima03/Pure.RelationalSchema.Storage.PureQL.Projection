using System.Collections;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeStoredTablesDatasets : IEnumerable<IStoredTableDataSet>
{
    private readonly IEnumerable<IStoredTableDataSet> _datasets;

    public FakeStoredTablesDatasets(IEnumerable<ITable> tables)
        : this(tables.Select(x => new FakeStoredTableDataset(x))) { }

    private FakeStoredTablesDatasets(IEnumerable<IStoredTableDataSet> datasets)
    {
        _datasets = datasets;
    }

    public IEnumerator GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator<IStoredTableDataSet> IEnumerable<IStoredTableDataSet>.GetEnumerator()
    {
        return _datasets.GetEnumerator();
    }
}
