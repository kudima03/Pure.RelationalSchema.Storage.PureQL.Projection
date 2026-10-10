using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The result of a PureQL query over stored schema datasets. The query is
// validated against the datasets' schemas when the projection is built, and
// runs each time the projection is enumerated.
public sealed record PureQLProjection : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, PureQLQuery query)
        : this(new Catalog(datasets), QueryProgram.Of(query)) { }

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
        : this(new Catalog(datasets), QueryProgram.Of(query)) { }

    private PureQLProjection(Catalog catalog, QueryProgram program)
    {
        QueryValidator.Validate(program, catalog);
        ResultTable table = new(program.Main.Select);
        TableSchema = table;
        _rows = new ResultRows(catalog, program, [.. table.Columns]).AsQueryable();
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
