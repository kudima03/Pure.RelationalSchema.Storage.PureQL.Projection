using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Runs a projection's tree, Select(query, row => new ResultRow(...)). The
// query, everything below the result rows, goes to the provider of the
// stored tables it reads, which runs it as it can (as one SQL statement over
// one database), and every value it returns becomes a result row here. The
// provider of a table held in memory (LINQ to Objects' EnumerableQuery) is
// never asked: the tree runs in memory (LocalProvider), as it does when the
// provider cannot run the query, which it says with NotSupportedException
// before it returns a row. Which happened is kept for the projection to
// report.
internal sealed class Execution
{
    private readonly Expression _query;

    private readonly IQueryProvider? _provider;

    private readonly Lazy<Func<object?, IRow>> _row;

    public Execution(Expression tree)
    {
        if (
            tree is not MethodCallExpression call
            || !Linq.Is(call, nameof(Queryable.Select))
            || Linq.Lambda(call.Arguments[1]) is not { Body: NewExpression created } map
            || created.Constructor != ResultRow.Constructor
        )
        {
            throw new InvalidOperationException(
                "A projection's tree ends in the Select that builds its result rows."
            );
        }

        Tree = tree;
        _query = call.Arguments[0];
        _provider = Roots
            .In(_query)
            .Select(table => table.Provider)
            .FirstOrDefault(provider => provider is not EnumerableQuery);
        _row = new Lazy<Func<object?, IRow>>(() => Row(map));
    }

    public Expression Tree { get; }

    public QueryExecution Path { get; private set; }

    public NotSupportedException? FallbackReason { get; private set; }

    public IEnumerator<IRow> Rows()
    {
        if (_provider is not null)
        {
            try
            {
                IEnumerator<IRow> rows = Translated(_provider);
                Path = QueryExecution.Translated;
                FallbackReason = null;
                return rows;
            }
            catch (NotSupportedException exception)
            {
                FallbackReason = exception;
            }
        }

        Path = QueryExecution.InMemory;
        return LocalProvider.Run<IRow>(Tree).GetEnumerator();
    }

    // The query's values as the provider returns them, the first read now:
    // a provider that refuses a query does so when the query is created,
    // enumerated or first read, never after a value.
    private IEnumerator<IRow> Translated(IQueryProvider provider)
    {
        IEnumerator values = provider.CreateQuery(_query).GetEnumerator();

        try
        {
            return Mapped(values, values.MoveNext()).GetEnumerator();
        }
        catch
        {
            (values as IDisposable)?.Dispose();
            throw;
        }
    }

    private IEnumerable<IRow> Mapped(IEnumerator values, bool any)
    {
        using IDisposable? disposable = values as IDisposable;

        while (any)
        {
            yield return _row.Value(values.Current);
            any = values.MoveNext();
        }
    }

    // The result row of a value of the query, boxed as the provider returns it.
    private static Func<object?, IRow> Row(LambdaExpression map)
    {
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        return Expression
            .Lambda<Func<object?, IRow>>(
                Expression.Invoke(
                    map,
                    Expression.Convert(value, map.Parameters[0].Type)
                ),
                value
            )
            .Compile();
    }

    // The stored tables a query reads, in the order it reads them.
    private sealed class Roots : ExpressionVisitor
    {
        private readonly List<IStoredTableDataSet> _tables = [];

        private Roots() { }

        public static IReadOnlyList<IStoredTableDataSet> In(Expression query)
        {
            Roots roots = new();
            _ = roots.Visit(query);
            return roots._tables;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is IStoredTableDataSet table)
            {
                _tables.Add(table);
            }

            return node;
        }
    }
}
