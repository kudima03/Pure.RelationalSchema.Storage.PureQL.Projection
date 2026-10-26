using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// When a provider refuses a query, with NotSupportedException: never, when
// the query is created, when it is enumerated, or when its first value is
// read.
public enum Refusal
{
    Never,
    OnCreation,
    OnEnumeration,
    OnFirstValue,
}

// A LINQ provider over ProvidedTables that records every query it is given
// and answers it with LINQ to Objects over the tables' rows, as a translating
// provider answers it from a database; or refuses it, as a translating
// provider refuses a query it cannot translate; or fails it with any other
// exception, as a database error does.
internal sealed class RecordingProvider(Refusal refusal = Refusal.Never, Exception? failure = null)
    : IQueryProvider
{
    private readonly Refusal _refusal = refusal;

    private readonly Exception? _failure = failure;

    public List<Expression> Queries { get; } = [];

    public IQueryable CreateQuery(Expression expression)
    {
        Record(expression, Refusal.OnCreation);
        return new Query<object>(this, expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        Record(expression, Refusal.OnCreation);
        return new Query<TElement>(this, expression);
    }

    public object? Execute(Expression expression)
    {
        Record(expression, Refusal.OnCreation);
        return Run(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return (TResult)Execute(expression)!;
    }

    private void Record(Expression expression, Refusal stage)
    {
        Queries.Add(expression);
        Refuse(stage);
    }

    private void Refuse(Refusal stage)
    {
        if (_refusal == stage)
        {
            throw new NotSupportedException($"The query is refused {stage}.");
        }
    }

    private object? Run(Expression expression)
    {
        Refuse(Refusal.OnEnumeration);

        if (_failure is not null)
        {
            throw _failure;
        }

        object? result = Expression.Lambda(new InMemory().Visit(expression)).Compile().DynamicInvoke();

        return _refusal == Refusal.OnFirstValue && result is IEnumerable values
            ? Refused(values)
            : result;
    }

    // The values, refused before the first is read.
    private static IEnumerable Refused(IEnumerable values)
    {
        foreach (object? value in values)
        {
            throw new NotSupportedException($"The query is refused {Refusal.OnFirstValue}.");
        }

        yield break;
    }

    // A provided table is its rows, as LINQ to Objects queries them.
    private sealed class InMemory : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            return node.Value is ProvidedTable table
                ? Expression.Constant(table.Rows.AsQueryable(), typeof(IQueryable<IRow>))
                : node;
        }
    }

    private sealed class Query<T>(RecordingProvider provider, Expression expression)
        : IQueryable<T>
    {
        private readonly RecordingProvider _provider = provider;

        public Type ElementType => typeof(T);

        public Expression Expression { get; } = expression;

        public IQueryProvider Provider => _provider;

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable)_provider.Run(Expression)!).Cast<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
