using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The tree of a PureQL document: the main query's rows as result rows. A
// subquery is built where it is read, for the way it is read, and inlined
// there, as SQL inlines a common table expression; read twice the same way,
// it is the same subtree. A query is a tree of its clauses (QueryTree) or
// its inputs put together by a set operation; a subquery may also be rows
// written in the document, read from a one-row constant source, or a
// recursion (PureQLRecursion.Recursive), whose next reads the rows of the
// level before under the subquery's own name.
internal sealed class DocumentTree(
    Catalog catalog,
    IEnumerable<NamedQuery> subqueries,
    ParameterValues parameters
)
{
    // A source of one row, which SQL writes as a SELECT without FROM: of a
    // total query, and of rows written in the document.
    private static readonly IQueryable<int> OneRow = new[] { 0 }.AsQueryable();

    private readonly Catalog _catalog = catalog;

    private readonly ParameterValues _parameters = parameters;

    private readonly IReadOnlyDictionary<string, SubqueryBody> _subqueries =
        subqueries.ToDictionary(
            subquery => subquery.Name,
            subquery => subquery.Body,
            StringComparer.Ordinal
        );

    private readonly Dictionary<(string Name, QueryUse Use), CompiledQuery> _built = [];

    // The rows of the level before, under the name of the recursive
    // subquery whose next is being built.
    private readonly Dictionary<string, CompiledQuery> _levels = [with(
        StringComparer.Ordinal
    )];

    public static Expression One => Expression.Constant(OneRow, typeof(IQueryable<int>));

    // The main query, its rows turned into IRow by the last Select, which
    // only maps each row of the query, item by item: the one step that runs
    // outside the query, as a LINQ provider's projection does. The query is
    // simplified as built; the mapping is not simplified into it, so the
    // query stays one subtree, which a provider runs on its own.
    public Expression Rows(QueryShape main, IReadOnlyList<IColumn> columns)
    {
        CompiledQuery query = Query(main, QueryUse.Rows);
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
        if (_levels.TryGetValue(name, out CompiledQuery? level))
        {
            return level;
        }

        if (!_built.TryGetValue((name, use), out CompiledQuery? query))
        {
            SubqueryBody body = _subqueries[name];
            query =
                body is PlainSubquery plain ? Query(plain.Query, use)
                : body is InlineRows rows ? Inline(rows)
                : Recursive(name, (RecursiveQuery)body);
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

    private CompiledQuery Query(QueryShape query, QueryUse use)
    {
        return query switch
        {
            QueryClauses clauses => QueryTree.Build(this, clauses, use),
            CombinedQuery combined => Combined(combined),
            _ => throw new NotSupportedException(
                $"{query.GetType().Name} is not a query this interpreter knows."
            ),
        };
    }

    // The inputs, each with the columns of the operation in their order and
    // types, put together two at a time: Concat for unionAll, Union,
    // Intersect and Except otherwise, which compare rows item by item, as
    // equal compares values, and return each distinct row once. An input is
    // unordered, so it is built as a query that is only aggregated.
    private CompiledQuery Combined(CombinedQuery combined)
    {
        IReadOnlyList<QueryColumn> columns = Headers.Of(combined);

        return new CompiledQuery(
            Simplifier.Simplify(
                combined
                    .Inputs.Select(input =>
                        Shaped(Query(input, QueryUse.Aggregated), columns)
                    )
                    .Aggregate(
                        (first, second) => Linq.Combined(
                            combined.Operation,
                            first,
                            second
                        )
                    )
            ),
            columns
        );
    }

    // One row of a one-row source for each row written, as an array of the
    // rows: a cell is a constant or a captured parameter, in the type of its
    // column.
    private CompiledQuery Inline(InlineRows rows)
    {
        IReadOnlyList<QueryColumn> columns = Headers.Of(rows);
        Scope scope = new(null, null, null);
        Type row = Tuples.Of([.. columns.Select(column => ValueTypes.Clr(column.Type))]);
        ParameterExpression one = Expression.Parameter(typeof(int), "one");
        ParameterExpression written = Expression.Parameter(row, "row");

        return new CompiledQuery(
            Linq.SelectMany(
                One,
                Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(
                        typeof(int),
                        typeof(IEnumerable<>).MakeGenericType(row)
                    ),
                    Expression.NewArrayInit(
                        row,
                        rows.Rows.Select(cells =>
                            Tuples.New(
                                [
                                    .. columns.Select(column =>
                                        Typed.As(
                                            Cell(
                                                cells
                                                    .First(cell =>
                                                        cell.Alias == column.Alias
                                                    )
                                                    .Expression,
                                                scope
                                            ),
                                            ValueTypes.Clr(column.Type)
                                        )
                                    ),
                                ]
                            )
                        )
                    ),
                    one
                ),
                Expression.Lambda(written, one, written)
            ),
            columns
        );
    }

    private Expression Cell(object cell, Scope scope)
    {
        return cell is IParameter parameter ? Parameter(parameter) : scope.Compile(cell);
    }

    // seed.Recursive(level => next over level, maxDepth): the seed fixes the
    // columns and their types, and next, built with the subquery's own name
    // standing for the rows of the level before, gives its columns in the
    // seed's order and types.
    private CompiledQuery Recursive(string name, RecursiveQuery recursive)
    {
        CompiledQuery seed = Query(recursive.Seed, QueryUse.Aggregated);
        ParameterExpression level = Expression.Parameter(
            typeof(IQueryable<>).MakeGenericType(seed.RowType),
            name
        );

        _levels[name] = new CompiledQuery(level, seed.Columns);
        Expression next = Shaped(
            QueryTree.Build(this, recursive.Next, QueryUse.Aggregated),
            seed.Columns
        );
        _ = _levels.Remove(name);

        CompiledQuery query = new(
            Expression.Call(
                typeof(PureQLRecursion)
                    .GetMethods()
                    .Single(method =>
                        method.Name == nameof(PureQLRecursion.Recursive)
                        && Linq.IsQueryable(method.GetParameters()[0].ParameterType)
                    )
                    .MakeGenericMethod(seed.RowType),
                seed.Rows,
                Expression.Quote(
                    Expression.Lambda(
                        typeof(Func<,>).MakeGenericType(level.Type, level.Type),
                        next,
                        level
                    )
                ),
                recursive.MaxDepth is IParameter depth
                    ? Typed.As(Parameter(depth), typeof(long))
                    : Expression.Constant((long)recursive.MaxDepth)
            ),
            seed.Columns
        );

        foreach (QueryUse use in Enum.GetValues<QueryUse>())
        {
            _built[(name, use)] = query;
        }

        return query;
    }

    // The rows of a query as rows of the given columns: each by its alias,
    // in the given order and type.
    private static Expression Shaped(CompiledQuery query, IReadOnlyList<QueryColumn> columns)
    {
        Type shape = Tuples.Of([.. columns.Select(column => ValueTypes.Clr(column.Type))]);

        if (
            query.RowType == shape
            && columns
                .Select(column => column.Alias)
                .SequenceEqual(query.Columns.Select(column => column.Alias))
        )
        {
            return query.Rows;
        }

        ParameterExpression row = Expression.Parameter(query.RowType, "row");

        return Linq.Select(
            query.Rows,
            Expression.Lambda(
                Tuples.New(
                    [
                        .. columns.Select(column =>
                            Typed.As(
                                Tuples.Item(row, query.Column(column.Alias)),
                                ValueTypes.Clr(column.Type)
                            )
                        ),
                    ]
                ),
                row
            )
        );
    }
}
