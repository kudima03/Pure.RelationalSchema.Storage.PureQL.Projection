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
//   collation) does, rather than by UTF-16 code unit;
// - computes decimals, sums and averages exactly, as the specification
//   defines them (ExactArithmetic), rather than as .NET's operators do.
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

    private sealed class Rewriter : ExactArithmetic
    {
        private readonly Dictionary<object, Expression> _tables = [with(
            ReferenceEqualityComparer.Instance
        )];

        private int _depth;

        public override Expression? Visit(Expression? node)
        {
            if (
                _depth == 0
                || node is not MethodCallExpression call
                || !Linq.IsQueryOperator(call.Method)
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

        // A query operator whose source has become a plain sequence is its
        // counterpart over one; a recursion's next then reads the level
        // before as a plain sequence too.
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

            if (IsRoundedQuotient(node))
            {
                return base.VisitMethodCall(node);
            }

            Expression[] arguments =
            [
                .. node.Arguments.Select(argument =>
                    Linq.IsQueryOperator(node.Method) && Level(argument) is Expression next
                        ? next
                        : Visit(argument)!
                ),
            ];

            return Framed(
                Linq.IsQueryOperator(node.Method) && !Linq.IsQueryable(arguments[0].Type)
                    ? Exactly(
                        (MethodCallExpression)Linq.OverEnumerable(node.Method, arguments)
                    )
                    : Exactly(node.Update(Visit(node.Object), arguments))
            );
        }

        // A sum, a mean or a count over the rows of a window's frame, read
        // from the running sums of the partition (PureQLWindow) instead of
        // folded from the frame's rows for every row: a running total over
        // n rows then costs n steps, not n squared. The selector and the
        // predicate must read nothing but their row.
        private static Expression Framed(Expression expression)
        {
            if (expression is not MethodCallExpression call)
            {
                return expression;
            }

            Expression? values =
                call.Method.DeclaringType == typeof(PureQLAggregates)
                && call.Method.Name
                    is nameof(PureQLAggregates.Sum)
                        or nameof(PureQLAggregates.Average)
                && call.Arguments[0] is MethodCallExpression selected
                && selected.Method.DeclaringType == typeof(Enumerable)
                && selected.Method.Name == nameof(Enumerable.Select)
                && selected.Arguments[1] is LambdaExpression
                    ? selected
                : call.Method.DeclaringType == typeof(Enumerable)
                && call.Method.Name == nameof(Enumerable.LongCount)
                    ? call
                : null;

            (Expression Window, LambdaExpression? Predicate)? frame = values
                is MethodCallExpression source
                ? Frame(source.Arguments[0])
                : null;

            if (
                values is not MethodCallExpression fold
                || frame is null
                || fold.Arguments.Skip(1).Any(argument => !Closed.Is(argument))
                || (frame.Value.Predicate is not null && !Closed.Is(frame.Value.Predicate))
            )
            {
                return expression;
            }

            Expression window = frame.Value.Window;
            LambdaExpression? predicate = frame.Value.Predicate;
            Type row = window.Type.GetGenericArguments()[0];
            Expression filter = predicate is null
                ? Expression.Constant(
                    null,
                    typeof(Func<,>).MakeGenericType(row, typeof(bool))
                )
                : predicate;
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;

            if (fold != call)
            {
                return Expression.Call(
                    window,
                    window.Type.GetMethod(
                        call.Method.Name,
                        hidden,
                        [typeof(object), fold.Arguments[1].Type, filter.Type]
                    )!,
                    Expression.Constant(new object()),
                    fold.Arguments[1],
                    filter
                );
            }

            LambdaExpression? counted =
                call.Arguments.Count == 2 ? call.Arguments[1] as LambdaExpression
                : predicate;

            return call.Arguments.Count == 2 && predicate is not null ? expression
                : counted is null
                    ? Expression.Call(
                        window,
                        window.Type.GetMethod(nameof(PureQLWindow<>.Count), hidden, [])!
                    )
                : Expression.Call(
                    window,
                    window.Type.GetMethod(
                        nameof(PureQLWindow<>.Count),
                        hidden,
                        [typeof(object), counted.Type]
                    )!,
                    Expression.Constant(new object()),
                    counted
                );
        }

        // The window whose frame a sequence is, and the predicate its rows
        // are filtered by, if it is window.Rows or window.Rows.Where(...).
        private static (Expression Window, LambdaExpression? Predicate)? Frame(
            Expression rows
        )
        {
            return
                rows is MethodCallExpression filtered
                && filtered.Method.DeclaringType == typeof(Enumerable)
                && filtered.Method.Name == nameof(Enumerable.Where)
                && filtered.Arguments[1] is LambdaExpression predicate
                && predicate.Parameters.Count == 1
                && Frame(filtered.Arguments[0]) is (Expression window, null)
                    ? (window, predicate)
                : rows
                    is MemberExpression
                {
                    Member.Name: nameof(PureQLWindow<>.Rows),
                    Expression: ParameterExpression owner
                }
                && owner.Type.IsGenericType
                && owner.Type.GetGenericTypeDefinition() == typeof(PureQLWindow<>)
                    ? (owner, null)
                : null;
        }

        // A lambda over a query, as a recursion's next is over the rows of
        // the level before: the same lambda over a plain sequence, so the
        // operators that read the level are those of plain sequences.
        private Expression? Level(Expression argument)
        {
            if (
                argument
                    is not UnaryExpression
                    {
                        NodeType: ExpressionType.Quote,
                        Operand: LambdaExpression lambda
                    }
                || !lambda.Parameters.Any(parameter => Linq.IsQueryable(parameter.Type))
            )
            {
                return null;
            }

            ParameterExpression[] parameters =
            [
                .. lambda.Parameters.Select(parameter =>
                    Linq.IsQueryable(parameter.Type)
                        ? Expression.Parameter(
                            typeof(IEnumerable<>).MakeGenericType(
                                Linq.ElementType(parameter.Type)
                            ),
                            parameter.Name
                        )
                        : parameter
                ),
            ];

            _depth++;
            Expression body = Visit(
                new Substitution(
                    lambda.Parameters.Zip(parameters).ToDictionary(
                        pair => pair.First,
                        pair => (Expression)pair.Second
                    )
                ).Visit(lambda.Body)
            )!;
            _depth--;

            return Expression.Lambda(body, parameters);
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

    // An expression with parameters replaced.
    private sealed class Substitution(
        IReadOnlyDictionary<ParameterExpression, Expression> replacements
    ) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return replacements.TryGetValue(node, out Expression? replacement)
                ? replacement
                : node;
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
