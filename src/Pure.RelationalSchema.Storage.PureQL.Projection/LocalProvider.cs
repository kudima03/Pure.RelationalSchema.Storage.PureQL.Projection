using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Runs a projection's tree in memory, as LINQ to Objects. The tree itself is
// left as built, for providers that translate it; the copy that runs
// - reads each stored table once per run, through its enumerator;
// - computes a query nested in a lambda that reads nothing of the lambda (a
//   joined source, an in list, an aggregate over all rows) once per run, the
//   first time it is read, as a SQL engine evaluates an uncorrelated subquery
//   once, rather than once per row;
// - orders strings by code point, as the specification (and SQL's "C"
//   collation) does, rather than by UTF-16 code unit.
internal sealed class LocalProvider : IQueryProvider
{
    private static readonly MethodInfo CompareOrdinal = typeof(string).GetMethod(
        nameof(string.CompareOrdinal),
        [typeof(string), typeof(string)]
    )!;

    private static readonly MethodInfo CompareCodePoints = typeof(CodePoints).GetMethod(
        nameof(CodePoints.Compare)
    )!;

    private static readonly MethodInfo SequenceMethod = typeof(LocalProvider).GetMethod(
        nameof(Sequence),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo ScalarMethod = typeof(LocalProvider).GetMethod(
        nameof(Scalar),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly IQueryProvider Objects = Array.Empty<object>()
        .AsQueryable()
        .Provider;

    private LocalProvider() { }

    public static LocalProvider Instance { get; } = new();

    public IQueryable CreateQuery(Expression expression)
    {
        return (IQueryable)Activator.CreateInstance(
            typeof(LocalQuery<>).MakeGenericType(Linq.ElementType(expression.Type)),
            expression
        )!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new LocalQuery<TElement>(expression);
    }

    public object? Execute(Expression expression)
    {
        return Objects.Execute(InMemory(expression));
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return Objects.Execute<TResult>(InMemory(expression));
    }

    public static IEnumerable<T> Run<T>(Expression expression)
    {
        return Objects.CreateQuery<T>(InMemory(expression));
    }

    private static Expression InMemory(Expression expression)
    {
        return new Rewriter().Visit(expression)!;
    }

    private static Expression Sequence<T>(Expression query)
    {
        return Expression.Constant(
            new EnumerableQuery<T>(new Memo<T>(() => Objects.CreateQuery<T>(query))),
            query.Type
        );
    }

    private static Expression Scalar<T>(Expression query)
    {
        return Expression.Property(
            Expression.Constant(new Lazy<T>(() => Objects.Execute<T>(query))),
            nameof(Lazy<>.Value)
        );
    }

    private sealed class Rewriter : ExpressionVisitor
    {
        private readonly Dictionary<object, Expression> _tables = new(
            ReferenceEqualityComparer.Instance
        );

        private int _depth;

        public override Expression? Visit(Expression? node)
        {
            if (
                _depth == 0
                || node is not MethodCallExpression call
                || call.Method.DeclaringType != typeof(Queryable)
                || !Closed.Is(call)
            )
            {
                return base.Visit(node);
            }

            int depth = _depth;
            _depth = 0;
            Expression query = base.Visit(call)!;
            _depth = depth;

            return (Expression)(
                Linq.IsQueryable(query.Type)
                    ? SequenceMethod.MakeGenericMethod(Linq.ElementType(query.Type))
                    : ScalarMethod.MakeGenericMethod(query.Type)
            ).Invoke(null, [query])!;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _depth++;
            Expression lambda = base.VisitLambda(node);
            _depth--;
            return lambda;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            return node.Value switch
            {
                IStoredTableDataSet dataset => Table(dataset, node.Type),
                StringComparer comparer when comparer == StringComparer.Ordinal =>
                    Expression.Constant(CodePoints.Comparer, node.Type),
                _ => node,
            };
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            return node.Method == CompareOrdinal
                ? Expression.Call(
                    CompareCodePoints,
                    Visit(node.Arguments[0])!,
                    Visit(node.Arguments[1])!
                )
                : base.VisitMethodCall(node);
        }

        private Expression Table(IStoredTableDataSet dataset, Type type)
        {
            if (!_tables.TryGetValue(dataset, out Expression? table))
            {
                table = Expression.Constant(
                    new EnumerableQuery<IRow>(
                        new Memo<IRow>(() => dataset.AsEnumerable())
                    ),
                    type
                );
                _tables[dataset] = table;
            }

            return table;
        }
    }

    // Whether an expression reads no parameter it does not declare.
    private sealed class Closed : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _declared = [];

        private bool _free;

        private Closed() { }

        public static bool Is(Expression node)
        {
            Closed closed = new();
            _ = closed.Visit(node);
            return !closed._free;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _declared.UnionWith(node.Parameters);
            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _free |= !_declared.Contains(node);
            return node;
        }
    }

    // A sequence read once, the first time it is enumerated.
    private sealed class Memo<T>(Func<IEnumerable<T>> source) : IEnumerable<T>
    {
        private readonly Func<IEnumerable<T>> _source = source;

        private List<T>? _items;

        public IEnumerator<T> GetEnumerator()
        {
            _items ??= [.. _source()];
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
