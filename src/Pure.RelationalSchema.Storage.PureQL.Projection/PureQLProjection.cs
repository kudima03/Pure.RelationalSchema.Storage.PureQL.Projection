using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The result of a PureQL query over stored schema datasets. The query is
// validated against the datasets' schemas and compiled to one LINQ expression
// tree when the projection is built: standard query operators over one root
// per stored table (a constant holding its IStoredTableDataSet), ending in
// the Select that builds the result rows (see QueryTree, DocumentTree).
// Enumerating the projection, again on every enumeration, hands the query
// below the result rows to the stored tables' provider, which translates it,
// and runs the tree in memory when the tables are held in memory or the
// provider cannot run the query (see Execution). Parameters are bound by
// name to PureQL literals or to Pure parameters, which stand for the literals
// (see ParameterValues, ParameterLiterals); reading one that is not bound
// fails the query.
public sealed record PureQLProjection : IStoredTableDataSet
{
    private readonly Execution _execution;

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

    public PureQLProjection(
        IEnumerable<IStoredSchemaDataSet> datasets,
        PureQLQuery query,
        IReadOnlyDictionary<string, IParameter> parameters
    )
        : this(datasets, query, ParameterLiterals.Of(parameters)) { }

    public PureQLProjection(
        IEnumerable<IStoredSchemaDataSet> datasets,
        Query query,
        IReadOnlyDictionary<string, IParameter> parameters
    )
        : this(datasets, query, ParameterLiterals.Of(parameters)) { }

    private PureQLProjection(
        Catalog catalog,
        QueryProgram program,
        ParameterValues parameters
    )
    {
        QueryValidator.Validate(program, catalog, parameters);
        ResultTable table = new(program.Main.Select);
        TableSchema = table;
        _execution = new Execution(
            new DocumentTree(catalog, program.Subqueries, parameters).Rows(
                program.Main,
                [.. table.Columns]
            )
        );
        _rows = new ProjectionQuery<IRow>(new ProjectionProvider(_execution), _execution.Tree);
    }

    public ITable TableSchema { get; }

    // Where the last enumeration ran the query: NotRun before the first;
    // Translated when the stored tables' provider ran it; InMemory when the
    // tree ran here, because the tables are held in memory (FallbackReason
    // is null) or the provider could not run the query (FallbackReason).
    public QueryExecution Execution => _execution.Path;

    // The NotSupportedException the stored tables' provider refused the
    // last query with, when it did; null otherwise.
    public NotSupportedException? FallbackReason => _execution.FallbackReason;

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
