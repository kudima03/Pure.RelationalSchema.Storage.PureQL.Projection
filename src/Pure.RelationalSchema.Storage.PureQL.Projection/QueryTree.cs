using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The tree of one query, in the specification's order, as standard query
// operators over IQueryable:
//
//   scan(from) [.Where] (join)* [.Where(rest of where)]
//     plain:   [.OrderBy/ThenBy] .Select(columns)
//     grouped: .GroupBy(keys) [.Where(having)] [.OrderBy/ThenBy] .Select(columns)
//     total:   one-row constant .Select(aggregates over the rows)
//   [.DistinctBy(columns)] [.Skip] [.Take]
//
// A stored source is scanned as Constant(dataset).Select(row => (fields)): a
// root per source, reading only the fields the query reads. A where condition
// is split into its and-ed conditions and each moves to the source it reads
// (or to the join that first sees all the sources it reads), unless an outer
// join may leave one of them without a row. A join is a hash join (Join, or
// GroupJoin then SelectMany with DefaultIfEmpty for an outer side) on the
// equalities of its condition between the rows so far and the joined source,
// or a correlated SelectMany when there is none (see Join). An order key the rows do not
// change, a repeated order key and a distinctBy the rows already satisfy are
// dropped, and a subquery read only for membership or by aggregates is not
// ordered. Rows are value tuples (Tuples); the query's own rows, which
// aggregates over all rows read, are one shared subtree.
internal sealed class QueryTree
{
    // The source of a total query: one row, which SQL writes as a SELECT
    // without FROM.
    private static readonly IQueryable<int> OneRow = new[] { 0 }.AsQueryable();

    private readonly DocumentTree _document;

    private readonly QueryClauses _query;

    private readonly IReadOnlyDictionary<string, int> _keys;

    private readonly bool _distinct;

    private readonly bool _ordered;

    private readonly IReadOnlyList<SourceScan> _sources;

    private QueryTree(DocumentTree document, QueryClauses query, QueryUse use)
    {
        _document = document;
        _query = query;
        _keys = (query.GroupBy ?? [])
            .Select((key, index) => KeyValuePair.Create(key.Alias, index))
            .ToDictionary(StringComparer.Ordinal);
        _distinct =
            query.DistinctBy.Count > 0
            && !(use == QueryUse.Membership && query.Pagination is null)
            && !DistinctByCoversGroupKeys();
        _ordered = use == QueryUse.Rows || query.Pagination is not null || _distinct;
        _sources = Scans(
            query.IsTotal || !_ordered ? QueryUse.Aggregated : QueryUse.Rows
        );
        RowType = _sources.Count == 1
            ? _sources[0].RowType
            : Tuples.Of([.. _sources.Select(source => source.RowType)]);
    }

    // A row of the from/joins product: the source's row for one source, a
    // tuple of one row per source for several.
    public Type RowType { get; }

    // The rows left by where: what aggregates over all rows fold.
    public Expression AllRows
    {
        get =>
            field
            ?? throw new InvalidOperationException(
                "Rows are read before where is built."
            );
        private set;
    }

    public static CompiledQuery Build(
        DocumentTree document,
        QueryClauses query,
        QueryUse use
    )
    {
        return new QueryTree(document, query, use).Build();
    }

    public Scope RowScope(ParameterExpression row)
    {
        return new Scope(this, field => Field(row, field), null);
    }

    public Expression Key(ParameterExpression group, string alias)
    {
        return Tuples.Item(Expression.Property(group, "Key"), _keys[alias]);
    }

    public Expression Parameter(IParameter parameter)
    {
        return _document.Parameter(parameter);
    }

    public Expression SubqueryColumn(string subquery, string field, Type item)
    {
        CompiledQuery compiled = _document.Subquery(subquery, QueryUse.Membership);
        ParameterExpression row = Expression.Parameter(compiled.RowType, subquery);

        return Linq.Select(
            compiled.Rows,
            Expression.Lambda(
                Typed.As(Tuples.Item(row, compiled.Column(field)), item),
                row
            )
        );
    }

    private CompiledQuery Build()
    {
        AllRows = Simplifier.Simplify(Filtered());

        Expression projected = _query.GroupBy is not null ? Grouped(_query.GroupBy)
            : _query.IsTotal ? Total()
            : Plain();

        return new CompiledQuery(
            Simplifier.Simplify(Page(Distinct(projected))),
            _query.Select
        );
    }

    private List<SourceScan> Scans(QueryUse use)
    {
        List<Source> sources = [_query.From, .. _query.Joins.Select(join => join.Source)];
        bool[] optional = new bool[sources.Count];

        for (int i = 1; i < sources.Count; i++)
        {
            switch (_query.Joins[i - 1].Kind)
            {
                case JoinKind.Left:
                    optional[i] = true;
                    break;
                case JoinKind.Right:
                    Array.Fill(optional, true, 0, i);
                    break;
                case JoinKind.Full:
                    Array.Fill(optional, true, 0, i + 1);
                    break;
                case JoinKind.Inner:
                default:
                    break;
            }
        }

        ILookup<string, string> fields = Expressions()
            .SelectMany(expression => ModelNode.Nodes(expression, _ => true))
            .OfType<IField>()
            .ToLookup(
                field => field.Source,
                field => field.Field,
                StringComparer.Ordinal
            );

        return
        [
            .. sources.Select(
                (source, index) => Scan(
                    source,
                    new HashSet<string>(fields[source.Name], StringComparer.Ordinal),
                    optional[index],
                    use
                )
            ),
        ];
    }

    private SourceScan Scan(
        Source source,
        HashSet<string> fields,
        bool optional,
        QueryUse use
    )
    {
        if (source.IsSubquery)
        {
            CompiledQuery subquery = _document.Subquery(source.Target, use);
            List<string> read =
            [
                .. subquery
                    .Columns.Select(column => column.Alias)
                    .Where(fields.Contains),
            ];
            ParameterExpression row = Expression.Parameter(subquery.RowType, source.Name);

            return new SourceScan(
                source.Name,
                read,
                Linq.Select(
                    subquery.Rows,
                    Expression.Lambda(
                        Tuples.New(
                            [
                                .. read.Select(alias =>
                                    Tuples.Item(row, subquery.Column(alias))
                                ),
                            ]
                        ),
                        row
                    )
                ),
                optional
            );
        }

        KeyValuePair<ITable, IStoredTableDataSet> table = _document.Table(source.Target);
        List<IColumn> columns =
        [
            .. table.Key.Columns.Where(column => fields.Contains(column.Name.TextValue)),
        ];
        ParameterExpression stored = Expression.Parameter(typeof(IRow), source.Name);

        return new SourceScan(
            source.Name,
            [.. columns.Select(column => column.Name.TextValue)],
            Linq.Select(
                Expression.Constant(table.Value, typeof(IQueryable<IRow>)),
                Expression.Lambda(
                    Tuples.New(
                        [.. columns.Select(column => StoredColumn.Read(stored, column))]
                    ),
                    stored
                )
            ),
            optional
        );
    }

    // from, joins and where: each and-ed condition of where at the source
    // or join it belongs to, the rest after the joins.
    private Expression Filtered()
    {
        int count = _sources.Count;
        List<object>[] atSource =
        [
            .. Enumerable.Range(0, count).Select(_ => new List<object>()),
        ];
        List<object>[] atJoin =
        [
            .. Enumerable.Range(0, count).Select(_ => new List<object>()),
        ];
        List<object> after = [];

        foreach (object condition in _query.Where is null ? [] : Conjuncts(_query.Where))
        {
            int[] read = [.. Sources(condition)];

            if (read.Any(source => _sources[source].Optional))
            {
                after.Add(condition);
            }
            else if (read.Length > 1)
            {
                atJoin[read.Max()].Add(condition);
            }
            else if (read.Length == 1)
            {
                atSource[read[0]].Add(condition);
            }
            else if (!_sources[0].Optional)
            {
                atSource[0].Add(condition);
            }
            else
            {
                after.Add(condition);
            }
        }

        Expression rows = Filtered(0, atSource[0]);

        for (int i = 1; i < count; i++)
        {
            rows = Join(rows, i, Filtered(i, atSource[i]), atJoin[i]);
        }

        if (after.Count == 0)
        {
            return rows;
        }

        ParameterExpression row = Expression.Parameter(RowType, "row");
        Scope scope = RowScope(row);

        return Linq.Where(
            rows,
            Expression.Lambda(Typed.All(after.Select(scope.Test)), row)
        );
    }

    private Expression Filtered(int source, List<object> conditions)
    {
        SourceScan scan = _sources[source];

        if (conditions.Count == 0)
        {
            return scan.Rows;
        }

        ParameterExpression row = Expression.Parameter(scan.RowType, scan.Name);
        Scope scope = new(
            this,
            field => Tuples.Item(row, scan.Field(field.Field)),
            null
        );

        return Linq.Where(
            scan.Rows,
            Expression.Lambda(Typed.All(conditions.Select(scope.Test)), row)
        );
    }

    // inner keeps the pairs on is true for; left also keeps every row so far
    // that matched nothing, right every joined row that matched nothing, and
    // full both, with an all-null source row for the missing side. An and-ed
    // condition of on (or of where, moved into an inner join) that equates
    // an expression over the rows so far with one over the joined source is
    // a join key. With keys the join is a hash join on them, which costs the
    // rows of both sides rather than their product: Join for inner, GroupJoin
    // then DefaultIfEmpty for an outer side, the other conditions filtering
    // the pairs; full is the left join plus the joined rows whose group of
    // matches is empty. Keys compare as equal does (null equals null, an
    // integer equals its decimal), and a tuple of keys is never null, so a
    // null key matches. Without a key the join is a correlated SelectMany
    // over the joined rows the condition holds for.
    private Expression Join(
        Expression rows,
        int source,
        Expression right,
        List<object> conditions
    )
    {
        JoinClause join = _query.Joins[source - 1];
        ParameterExpression left = Expression.Parameter(
            Linq.ElementType(rows.Type),
            "left"
        );
        ParameterExpression joined = Expression.Parameter(
            Linq.ElementType(right.Type),
            _sources[source].Name
        );
        List<(object Left, object Right)> keys = [];
        List<object> rest = [];

        foreach (object condition in Conjuncts(join.On).Concat(conditions))
        {
            if (KeySides(condition, source) is (object l, object r))
            {
                keys.Add((l, r));
            }
            else
            {
                rest.Add(condition);
            }
        }

        if (keys.Count == 0)
        {
            return NestedLoopJoin(rows, source, right, left, joined, rest);
        }

        Scope scope = new(this, field => JoinField(left, joined, source, field), null);
        List<(Expression Left, Expression Right)> compiled =
        [
            .. keys.Select(key =>
            {
                Expression l = scope.Compile(key.Left);
                Expression r = scope.Compile(key.Right);
                Type type = Typed.Common(l.Type, r.Type);
                return (Typed.As(l, type), Typed.As(r, type));
            }),
        ];
        LambdaExpression leftKey = Expression.Lambda(
            Tuples.New([.. compiled.Select(key => key.Left)]),
            left
        );
        LambdaExpression rightKey = Expression.Lambda(
            Tuples.New([.. compiled.Select(key => key.Right)]),
            joined
        );

        return join.Kind switch
        {
            JoinKind.Left => OuterJoin(
                rows,
                right,
                leftKey,
                rightKey,
                (left, joined),
                source,
                rest,
                false
            ),
            JoinKind.Right => OuterJoin(
                right,
                rows,
                rightKey,
                leftKey,
                (left, joined),
                source,
                rest,
                true
            ),
            JoinKind.Full => Linq.Concat(
                OuterJoin(
                    rows,
                    right,
                    leftKey,
                    rightKey,
                    (left, joined),
                    source,
                    rest,
                    false
                ),
                Unmatched(rows, right, leftKey, rightKey, (left, joined), source, rest)
            ),
            JoinKind.Inner or _ => InnerJoin(
                rows,
                right,
                leftKey,
                rightKey,
                (left, joined),
                source,
                rest
            ),
        };
    }

    // The pairs equal on the keys, filtered by the other conditions.
    private Expression InnerJoin(
        Expression rows,
        Expression right,
        LambdaExpression leftKey,
        LambdaExpression rightKey,
        (ParameterExpression Left, ParameterExpression Joined) sides,
        int source,
        List<object> rest
    )
    {
        LambdaExpression pair = Expression.Lambda(
            Pair(sides.Left, sides.Joined, source),
            sides.Left,
            sides.Joined
        );
        Expression pairs = Linq.Join(rows, right, leftKey, rightKey, pair);

        if (rest.Count == 0)
        {
            return pairs;
        }

        ParameterExpression row = Expression.Parameter(pair.ReturnType, "row");
        Scope scope = RowScope(row);

        return Linq.Where(
            pairs,
            Expression.Lambda(Typed.All(rest.Select(scope.Test)), row)
        );
    }

    // Each driving row with its matches, (row, matches), then the pairs with
    // each match the other conditions hold for, or with the default row of
    // the other side when there is none. The joined source drives a right
    // join, the rows so far a left join.
    private Expression OuterJoin(
        Expression driving,
        Expression other,
        LambdaExpression drivingKey,
        LambdaExpression otherKey,
        (ParameterExpression Left, ParameterExpression Joined) sides,
        int source,
        List<object> rest,
        bool rightDriven
    )
    {
        Expression groups = Groups(driving, other, drivingKey, otherKey, rightDriven);
        ParameterExpression match = Expression.Parameter(
            Linq.ElementType(groups.Type),
            "match"
        );
        ParameterExpression inner = rightDriven ? sides.Left : sides.Joined;
        Expression driver = Tuples.Item(match, 0);
        (Expression Left, Expression Joined) pair = rightDriven
            ? (inner, driver)
            : (driver, inner);

        return Linq.SelectMany(
            groups,
            Expression.Lambda(
                typeof(Func<,>).MakeGenericType(
                    match.Type,
                    typeof(IEnumerable<>).MakeGenericType(inner.Type)
                ),
                Linq.DefaultIfEmpty(Matches(match, inner, pair, source, rest)),
                match
            ),
            Expression.Lambda(Pair(pair.Left, pair.Joined, source), match, inner)
        );
    }

    // The joined rows no row so far matches, each paired with the default row
    // of the rows so far.
    private Expression Unmatched(
        Expression rows,
        Expression right,
        LambdaExpression leftKey,
        LambdaExpression rightKey,
        (ParameterExpression Left, ParameterExpression Joined) sides,
        int source,
        List<object> rest
    )
    {
        Expression groups = Groups(right, rows, rightKey, leftKey, true);
        ParameterExpression match = Expression.Parameter(
            Linq.ElementType(groups.Type),
            "match"
        );
        Expression joined = Tuples.Item(match, 0);

        return Linq.Select(
            Linq.Where(
                groups,
                Expression.Lambda(
                    Expression.Not(
                        Linq.Any(Matches(match, sides.Left, (sides.Left, joined), source, rest))
                    ),
                    match
                )
            ),
            Expression.Lambda(
                Pair(Expression.Default(sides.Left.Type), joined, source),
                match
            )
        );
    }

    // (driving row, the other side's rows equal on the keys).
    private static Expression Groups(
        Expression driving,
        Expression other,
        LambdaExpression drivingKey,
        LambdaExpression otherKey,
        bool rightDriven
    )
    {
        ParameterExpression row = drivingKey.Parameters[0];
        ParameterExpression matches = Expression.Parameter(
            typeof(IEnumerable<>).MakeGenericType(Linq.ElementType(other.Type)),
            rightDriven ? "lefts" : "matches"
        );

        return Linq.GroupJoin(
            driving,
            other,
            drivingKey,
            otherKey,
            Expression.Lambda(Tuples.New([row, matches]), row, matches)
        );
    }

    // The matches of a driving row the other conditions hold for.
    private Expression Matches(
        ParameterExpression match,
        ParameterExpression inner,
        (Expression Left, Expression Joined) pair,
        int source,
        List<object> rest
    )
    {
        Expression matches = Tuples.Item(match, 1);

        if (rest.Count == 0)
        {
            return matches;
        }

        Scope scope = new(
            this,
            field => JoinField(pair.Left, pair.Joined, source, field),
            null
        );

        return Linq.Where(
            matches,
            Expression.Lambda(Typed.All(rest.Select(scope.Test)), inner)
        );
    }

    // A correlated SelectMany: the joined rows the condition holds for, for
    // each row so far (or the other way round for right), DefaultIfEmpty for
    // an outer side; full is the left join plus the unmatched joined rows.
    private Expression NestedLoopJoin(
        Expression rows,
        int source,
        Expression right,
        ParameterExpression left,
        ParameterExpression joined,
        List<object> conditions
    )
    {
        JoinClause join = _query.Joins[source - 1];
        Scope scope = new(this, field => JoinField(left, joined, source, field), null);
        Expression on = Typed.All(conditions.Select(scope.Test));

        LambdaExpression pair = Expression.Lambda(
            Pair(left, joined, source),
            left,
            joined
        );

        Expression leftJoin(bool outer)
        {
            Expression matches = Linq.Where(right, Expression.Lambda(on, joined));

            return Linq.SelectMany(
                rows,
                Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(
                        left.Type,
                        typeof(IEnumerable<>).MakeGenericType(joined.Type)
                    ),
                    outer ? Linq.DefaultIfEmpty(matches) : matches,
                    left
                ),
                pair
            );
        }

        return join.Kind switch
        {
            JoinKind.Left => leftJoin(true),
            JoinKind.Right => Linq.SelectMany(
                right,
                Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(
                        joined.Type,
                        typeof(IEnumerable<>).MakeGenericType(left.Type)
                    ),
                    Linq.DefaultIfEmpty(Linq.Where(rows, Expression.Lambda(on, left))),
                    joined
                ),
                Expression.Lambda(Pair(left, joined, source), joined, left)
            ),
            JoinKind.Full => Linq.Concat(
                leftJoin(true),
                Linq.Select(
                    Linq.Where(
                        right,
                        Expression.Lambda(
                            Expression.Not(Linq.Any(rows, Expression.Lambda(on, left))),
                            joined
                        )
                    ),
                    Expression.Lambda(
                        Pair(Expression.Default(left.Type), joined, source),
                        joined
                    )
                )
            ),
            JoinKind.Inner or _ => leftJoin(false),
        };
    }

    // The sides of an equality that is a join key: one reads only sources
    // before the joined one, the other only the joined source. The one over
    // the rows so far comes first.
    private (object Left, object Right)? KeySides(object condition, int source)
    {
        object leaf = ModelNode.Unwrap(condition);

        if (
            leaf is not IOperator
            || !leaf.GetType().Name.StartsWith("Equal", StringComparison.Ordinal)
        )
        {
            return null;
        }

        object left = ModelNode.Child(leaf, "Left");
        object right = ModelNode.Child(leaf, "Right");
        int[] leftReads = [.. Sources(left)];
        int[] rightReads = [.. Sources(right)];

        bool before(int[] read)
        {
            return read.Length > 0 && read.All(index => index < source);
        }

        bool joined(int[] read)
        {
            return read.Length == 1 && read[0] == source;
        }

        return before(leftReads) && joined(rightReads) ? (left, right)
            : joined(leftReads) && before(rightReads) ? (right, left)
            : null;
    }

    // The joined row of the rows so far and the row of the joined source.
    private static Expression Pair(Expression left, Expression joined, int source)
    {
        return Tuples.New(
            source == 1
                ? [left, joined]
                :
                [
                    .. Enumerable.Range(0, source).Select(i => Tuples.Item(left, i)),
                    joined,
                ]
        );
    }

    private Expression JoinField(
        Expression left,
        Expression joined,
        int source,
        IField field
    )
    {
        int index = Source(field.Source);
        Expression row = index == source ? joined
            : source == 1 ? left
            : Tuples.Item(left, index);

        return Tuples.Item(row, _sources[index].Field(field.Field));
    }

    private Expression Field(Expression row, IField field)
    {
        int index = Source(field.Source);
        Expression source = _sources.Count == 1 ? row : Tuples.Item(row, index);

        return Tuples.Item(source, _sources[index].Field(field.Field));
    }

    private Expression Plain()
    {
        ParameterExpression row = Expression.Parameter(RowType, "row");
        Scope scope = RowScope(row);

        return Linq.Select(
            Ordered(AllRows, row, scope),
            Expression.Lambda(Columns(scope), row)
        );
    }

    // Groups by keys compared with equal. A group always has a row, so no
    // rows give no groups.
    private Expression Grouped(IReadOnlyList<TypedExpression> groupBy)
    {
        ParameterExpression row = Expression.Parameter(RowType, "row");
        Scope rowScope = RowScope(row);

        Expression groups = Linq.GroupBy(
            AllRows,
            Expression.Lambda(
                Tuples.New(
                    [
                        .. groupBy.Select(key =>
                            Typed.As(
                                rowScope.Compile(key.Expression),
                                ValueTypes.Clr(key.Type)
                            )
                        ),
                    ]
                ),
                row
            )
        );

        ParameterExpression group = Expression.Parameter(
            Linq.ElementType(groups.Type),
            "group"
        );
        Scope scope = new(this, null, group);

        if (_query.Having is not null)
        {
            groups = Linq.Where(
                groups,
                Expression.Lambda(scope.Test(_query.Having), group)
            );
        }

        return Linq.Select(
            Ordered(groups, group, scope),
            Expression.Lambda(Columns(scope), group)
        );
    }

    // Exactly one row, even over no rows: the aggregates over the rows, read
    // from a one-row constant source.
    private Expression Total()
    {
        Scope scope = new(this, null, null);

        return Linq.Select(
            Expression.Constant(OneRow, typeof(IQueryable<int>)),
            Expression.Lambda(Columns(scope), Expression.Parameter(typeof(int), "one"))
        );
    }

    private Expression Columns(Scope scope)
    {
        return Tuples.New(
            [
                .. _query.Select.Select(column =>
                    Typed.As(
                        scope.Compile(column.Expression),
                        ValueTypes.Clr(column.Type)
                    )
                ),
            ]
        );
    }

    // Each key breaks the ties of the previous one; null sorts first ascending
    // and last descending, and strings order ordinally.
    private Expression Ordered(Expression rows, ParameterExpression row, Scope scope)
    {
        if (!_ordered)
        {
            return rows;
        }

        List<object> seen = [];
        Expression ordered = rows;

        foreach (OrderKey key in _query.OrderBy)
        {
            object leaf = ModelNode.Unwrap(key.Expression);

            if (seen.Contains(leaf))
            {
                continue;
            }

            seen.Add(leaf);
            Expression body = Simplifier.Simplify(scope.Compile(key.Expression));

            if (!Simplifier.Reads(body, row))
            {
                continue;
            }

            ordered = Linq.Order(
                ordered,
                Expression.Lambda(body, row),
                key.Direction == SortDirection.Desc,
                ordered == rows,
                body.Type == typeof(string)
                    ? Expression.Constant(
                        StringComparer.Ordinal,
                        typeof(IComparer<string>)
                    )
                    : null
            );
        }

        return ordered;
    }

    // Keeps, of the rows equal on the distinctBy columns, the first in order.
    private Expression Distinct(Expression rows)
    {
        if (!_distinct)
        {
            return rows;
        }

        ParameterExpression row = Expression.Parameter(
            Linq.ElementType(rows.Type),
            "row"
        );

        return Linq.DistinctBy(
            rows,
            Expression.Lambda(
                Tuples.New(
                    [
                        .. _query
                            .DistinctBy.Distinct(StringComparer.Ordinal)
                            .Select(alias => Tuples.Item(row, Column(alias))),
                    ]
                ),
                row
            )
        );
    }

    private Expression Page(Expression rows)
    {
        if (_query.Pagination is null)
        {
            return rows;
        }

        Expression skip = Count(_query.Pagination.Skip);
        Expression paged = skip is ConstantExpression { Value: 0 }
            ? rows
            : Linq.Skip(rows, skip);

        return Linq.Take(paged, Count(_query.Pagination.Take));
    }

    private Expression Count(OneOf.OneOf<long, ParamInteger> count)
    {
        return count.Match<Expression>(
            number => Expression.Constant((int)Math.Min(number, int.MaxValue)),
            parameter => Expression.Convert(
                Expression.Call(
                    typeof(Math).GetMethod(
                        nameof(Math.Min),
                        [typeof(long), typeof(long)]
                    )!,
                    Typed.As(Parameter(parameter), typeof(long)),
                    Expression.Constant((long)int.MaxValue)
                ),
                typeof(int)
            )
        );
    }

    // Groups are unique on their keys, so a distinctBy listing a column of
    // every key keeps every row.
    private bool DistinctByCoversGroupKeys()
    {
        return _query.GroupBy is not null
            && _query.GroupBy.All(key =>
                _query.DistinctBy.Any(alias =>
                    ModelNode.Unwrap(_query.Select[Column(alias)].Expression) is IKey read
                    && read.Key == key.Alias
                )
            );
    }

    private int Column(string alias)
    {
        return _query.Select.Select(column => column.Alias).ToList().IndexOf(alias);
    }

    private int Source(string name)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].Name == name)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Source '{name}' is not declared.");
    }

    private IEnumerable<int> Sources(object condition)
    {
        return ModelNode
            .Nodes(condition, _ => true)
            .OfType<IField>()
            .Select(field => Source(field.Source))
            .Distinct();
    }

    private IEnumerable<object> Expressions()
    {
        return new object?[] { _query.Where, _query.Having }
            .Concat(_query.Joins.Select(join => join.On))
            .Concat((_query.GroupBy ?? []).Select(key => key.Expression))
            .Concat(_query.Select.Select(column => column.Expression))
            .Concat(_ordered ? _query.OrderBy.Select(key => key.Expression) : [])
            .OfType<object>();
    }

    private static IEnumerable<object> Conjuncts(object condition)
    {
        return ModelNode.Unwrap(condition) is AndRow and
            ? and.Conditions.SelectMany(Conjuncts)
            : [condition];
    }
}
