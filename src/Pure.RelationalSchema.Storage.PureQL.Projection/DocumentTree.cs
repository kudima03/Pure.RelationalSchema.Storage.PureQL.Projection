using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The tree of a PureQL document: the main query's rows as result rows. A
// subquery is built where it is read, for the way it is read, and inlined
// there, as SQL inlines a common table expression; read twice the same way,
// it is the same subtree.
internal sealed class DocumentTree(
    Catalog catalog,
    IEnumerable<NamedQuery> subqueries,
    ParameterValues parameters
)
{
    private readonly Catalog _catalog = catalog;

    private readonly ParameterValues _parameters = parameters;

    private readonly IReadOnlyDictionary<string, QueryClauses> _subqueries =
        subqueries.ToDictionary(
            subquery => subquery.Name,
            subquery => subquery.Clauses,
            StringComparer.Ordinal
        );

    private readonly Dictionary<(string Name, QueryUse Use), CompiledQuery> _built = [];

    // The main query, its rows turned into IRow by the last Select, which
    // only maps each row of the query, item by item: the one step that runs
    // outside the query, as a LINQ provider's projection does. The query is
    // simplified as built; the mapping is not simplified into it, so the
    // query stays one subtree, which a provider runs on its own.
    public Expression Rows(QueryClauses main, IReadOnlyList<IColumn> columns)
    {
        CompiledQuery query = QueryTree.Build(this, main, QueryUse.Rows);
        ParameterExpression row = Expression.Parameter(query.RowType, "row");

        return Linq.Select(
            query.Rows,
            Expression.Lambda(
                typeof(Func<,>).MakeGenericType(query.RowType, typeof(IRow)),
                Expression.New(
                    ResultRow.Constructor,
                    Expression.Constant(columns, typeof(IReadOnlyList<IColumn>)),
                    Expression.NewArrayInit(
                        typeof(object),
                        [
                            .. query.Columns.Select(
                                (_, index) => Expression.Convert(
                                    Tuples.Item(row, index),
                                    typeof(object)
                                )
                            ),
                        ]
                    )
                ),
                row
            )
        );
    }

    public CompiledQuery Subquery(string name, QueryUse use)
    {
        if (!_built.TryGetValue((name, use), out CompiledQuery? query))
        {
            query = QueryTree.Build(this, _subqueries[name], use);
            _built[(name, use)] = query;
        }

        return query;
    }

    public KeyValuePair<ITable, IStoredTableDataSet> Table(string entity)
    {
        return _catalog.Table(entity);
    }

    public Expression Parameter(IParameter parameter)
    {
        return _parameters.Value(parameter);
    }
}
