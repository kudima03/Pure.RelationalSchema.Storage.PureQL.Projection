using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Runs a tree in memory, as LINQ to Objects: a projection's tree when the
// stored tables' provider does not run its query (see Execution), and a
// query composed over a projection. The tree itself is left as built; the
// copy that runs
// - reads each stored table once per run, through its enumerator;
// - computes a query nested in a lambda that reads nothing of the lambda (a
//   joined source, an in list, an aggregate over all rows) once per run, the
//   first time it is read, as a SQL engine evaluates an uncorrelated subquery
//   once, rather than once per row, and answers Contains over it from a set;
// - is Enumerable calls over those sequences, so nothing of it is a query
//   that LINQ to Objects would rewrite and compile again every time a row
//   reads it, and is compiled once per run;
// - orders strings by code point, as the specification (and SQL's "C"
//   collation) does, rather than by UTF-16 code unit.
internal static class LocalProvider
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

    public static object? Execute(Expression expression)
    {
        return Compile<object?>(Expression.Convert(InMemory(expression), typeof(object)))();
    }

    public static TResult Execute<TResult>(Expression expression)
    {
        return Compile<TResult>(InMemory(expression))();
    }

    public static IEnumerable<T> Run<T>(Expression expression)
    {
        return Compile<IEnumerable<T>>(InMemory(expression))();
    }

    private static Expression InMemory(Expression expression)
    {
        return new Rewriter().Visit(expression)!;
    }

    private static Func<T> Compile<T>(Expression body)
    {
        return Expression.Lambda<Func<T>>(body).Compile();
    }

    private static Expression Sequence<T>(Expression query)
    {
        return Expression.Constant(
            new Memo<T>(Compile<IEnumerable<T>>(query)),
            typeof(IEnumerable<T>)
        );
    }

    private static Expression Scalar<T>(Expression query)
    {
        return Expression.Property(
            Expression.Constant(new Lazy<T>(Compile<T>(query))),
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
                IsSequence(query.Type)
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

        // A stored table is its rows, read once; a constant query (the
        // one-row source of a total query) is the sequence it holds.
        protected override Expression VisitConstant(ConstantExpression node)
        {
            return node.Value switch
            {
                IStoredTableDataSet dataset => Table(dataset),
                IQueryable query => Expression.Constant(
                    query,
                    typeof(IEnumerable<>).MakeGenericType(query.ElementType)
                ),
                StringComparer comparer when comparer == StringComparer.Ordinal =>
                    Expression.Constant(CodePoints.Comparer, node.Type),
                _ => node,
            };
        }

        // A Queryable call whose source has become a plain sequence is the
        // Enumerable call over it.
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method == CompareOrdinal)
            {
                return Expression.Call(
                    CompareCodePoints,
                    Visit(node.Arguments[0])!,
                    Visit(node.Arguments[1])!
                );
            }

            Expression[] arguments = [.. node.Arguments.Select(argument => Visit(argument)!)];

            return node.Method.DeclaringType == typeof(Queryable)
                && !Linq.IsQueryable(arguments[0].Type)
                ? Linq.OverEnumerable(node.Method, arguments)
                : node.Update(Visit(node.Object), arguments);
        }

        private static bool IsSequence(Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private Expression Table(IStoredTableDataSet dataset)
        {
            if (!_tables.TryGetValue(dataset, out Expression? table))
            {
                table = Expression.Constant(
                    new Memo<IRow>(() => dataset.AsEnumerable()),
                    typeof(IEnumerable<IRow>)
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

    // A sequence read once, the first time it is enumerated, and a collection,
    // so that Contains over it (in over a subquery, which LINQ to Objects
    // asks the collection) looks a value up in a set rather than scanning the
    // sequence for every row.
    private sealed class Memo<T>(Func<IEnumerable<T>> source) : ICollection<T>
    {
        private readonly Func<IEnumerable<T>> _source = source;

        private List<T>? _items;

        private HashSet<T>? _set;

        public int Count => Items().Count;

        public bool IsReadOnly => true;

        public IEnumerator<T> GetEnumerator()
        {
            return Items().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(T item)
        {
            _set ??= [.. Items()];
            return _set.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Items().CopyTo(array, arrayIndex);
        }

        public void Add(T item)
        {
            throw new NotSupportedException("A memoized sequence is read-only.");
        }

        public void Clear()
        {
            throw new NotSupportedException("A memoized sequence is read-only.");
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException("A memoized sequence is read-only.");
        }

        private List<T> Items()
        {
            return _items ??= [.. _source()];
        }
    }
}
