using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The result of a PureQL query over stored schema datasets. The query is
// validated against the datasets' schemas and compiled to one LINQ expression
// tree when the projection is built: standard query operators over one root
// per stored table (a constant holding its IStoredTableDataSet), for a LINQ
// provider to translate (see QueryTree). Enumerating the projection runs the
// tree in memory, again on every enumeration. Parameters are bound by name
// (see ParameterValues); reading one that is not bound fails the query.
public sealed record PureQLProjection : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, PureQLQuery query)
        : this(new Catalog(datasets), QueryProgram.Of(query), ParameterValues.None) { }

    public PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
        : this(new Catalog(datasets), QueryProgram.Of(query), ParameterValues.None) { }

    public PureQLProjection(
        IEnumerable<IStoredSchemaDataSet> datasets,
        PureQLQuery query,
        IReadOnlyDictionary<string, ILiteral> parameters
    )
        : this(
            new Catalog(datasets),
            QueryProgram.Of(query),
            new ParameterValues(parameters)
        )
    { }

    public PureQLProjection(
        IEnumerable<IStoredSchemaDataSet> datasets,
        Query query,
        IReadOnlyDictionary<string, ILiteral> parameters
    )
        : this(
            new Catalog(datasets),
            QueryProgram.Of(query),
            new ParameterValues(parameters)
        )
    { }

    private PureQLProjection(
        Catalog catalog,
        QueryProgram program,
        ParameterValues parameters
    )
    {
        QueryValidator.Validate(program, catalog, parameters);
        ResultTable table = new(program.Main.Select);
        TableSchema = table;
        _rows = new LocalQuery<IRow>(
            new DocumentTree(catalog, program.Subqueries, parameters).Rows(
                program.Main,
                [.. table.Columns]
            )
        );
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
