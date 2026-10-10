using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Runs one query in the specification's order: from and joins, where,
// groupBy, having, select, distinct, orderBy, pagination.
internal sealed class QueryRun(Execution execution, QueryClauses query)
{
    private readonly Execution _execution = execution;

    private readonly QueryClauses _query = query;

    private readonly IReadOnlyDictionary<string, int> _sources = new[] { query.From }
            .Concat(query.Joins.Select(join => join.Source))
            .Select((source, index) => KeyValuePair.Create(source.Name, index))
            .ToDictionary(StringComparer.Ordinal);

    private readonly Dictionary<object, object?> _aggregates = new(
        ReferenceEqualityComparer.Instance
    );

    // The rows left by where: what aggregates over all rows fold.
    public IReadOnlyList<JoinedRow> AllRows { get; private set; } = [];

    public int Source(string name)
    {
        return _sources[name];
    }

    public object? Once(object node, Func<object?> evaluate)
    {
        if (!_aggregates.TryGetValue(node, out object? value))
        {
            value = evaluate();
            _aggregates[node] = value;
        }

        return value;
    }

    public IEnumerable<object?> SubqueryColumn(string subquery, string field)
    {
        return _execution.Subquery(subquery).Column(field);
    }

    public QueryResult Result()
    {
        List<JoinedRow> rows = Joined();

        AllRows = _query.Where is null
            ? rows
            : [.. rows.Where(row => RowScope(row).Test(_query.Where))];

        IEnumerable<ProjectedRow> projected = _query.GroupBy is null
            ? Plain()
            : Grouped(_query.GroupBy);

        if (_query.Distinct)
        {
            projected = projected.DistinctBy(row => new ValueKey(row.Values));
        }

        return new QueryResult(
            _query.Select,
            [.. Page(Ordered(projected)).Select(row => row.Values)]
        );
    }

    private List<JoinedRow> Joined()
    {
        int sourceCount = _sources.Count;

        List<JoinedRow> rows =
        [
            .. _execution
                .Records(_query.From)
                .Select(record => JoinedRow.Of(sourceCount, 0, record)),
        ];

        for (int i = 0; i < _query.Joins.Count; i++)
        {
            rows = Join(rows, i + 1, _query.Joins[i]);
        }

        return rows;
    }

    // inner keeps the pairs on is true for; left also keeps every row so far
    // that matched nothing, right every joined row that matched nothing, and
    // full both, with no record for the missing side.
    private List<JoinedRow> Join(List<JoinedRow> left, int source, JoinClause join)
    {
        List<IRecord> right = [.. _execution.Records(join.Source)];
        bool[] rightMatched = new bool[right.Count];
        List<JoinedRow> result = [];

        foreach (JoinedRow row in left)
        {
            bool matched = false;

            for (int i = 0; i < right.Count; i++)
            {
                JoinedRow pair = row.With(source, right[i]);

                if (RowScope(pair).Test(join.On))
                {
                    result.Add(pair);
                    matched = true;
                    rightMatched[i] = true;
                }
            }

            if (!matched && join.Type is JoinType.Left or JoinType.Full)
            {
                result.Add(row);
            }
        }

        if (join.Type is JoinType.Right or JoinType.Full)
        {
            result.AddRange(
                right
                    .Where((_, i) => !rightMatched[i])
                    .Select(record => JoinedRow.Of(_sources.Count, source, record))
            );
        }

        return result;
    }

    // One row per row when a column reads a field outside an aggregate,
    // otherwise exactly one row, even over no rows.
    private IEnumerable<ProjectedRow> Plain()
    {
        bool perRow = _query.Select.Any(column => ReadsRowField(column.Expression));

        IEnumerable<Scope> scopes = perRow
            ? AllRows.Select(row => new Scope(this, row, AllRows, []))
            : [new Scope(this, null, AllRows, [])];

        return scopes.Select(Project);
    }

    // Groups by keys compared with equal, in order of first appearance. A
    // group always has a row, so no rows give no groups.
    private IEnumerable<ProjectedRow> Grouped(IReadOnlyList<TypedExpression> groupBy)
    {
        return AllRows
            .GroupBy(
                row =>
                {
                    Scope scope = RowScope(row);

                    return new ValueKey(
                        [
                            .. groupBy.Select(key =>
                                ValueTypes.Normalize(
                                    scope.Evaluate(key.Expression),
                                    key.Type
                                )
                            ),
                        ]
                    );
                }
            )
            .Select(group => new Scope(this, null, [.. group], group.Key.Items))
            .Where(scope => _query.Having is null || scope.Test(_query.Having))
            .Select(Project);
    }

    private ProjectedRow Project(Scope scope)
    {
        return new ProjectedRow(
            [
                .. _query.Select.Select(column =>
                    ValueTypes.Normalize(scope.Evaluate(column.Expression), column.Type)
                ),
            ],
            [.. _query.OrderBy.Select(key => scope.Evaluate(key.Expression))]
        );
    }

    // Each key breaks the ties of the previous one; null sorts first ascending
    // and last descending. The sort is stable, so fully tied rows keep their
    // order.
    private IEnumerable<ProjectedRow> Ordered(IEnumerable<ProjectedRow> rows)
    {
        IOrderedEnumerable<ProjectedRow>? ordered = null;

        for (int i = 0; i < _query.OrderBy.Count; i++)
        {
            int index = i;
            Comparer<object?> comparer = _query.OrderBy[i].Direction == SortDirection.Desc
                ? Comparer<object?>.Create((left, right) => Values.Compare(right, left))
                : Comparer<object?>.Create(Values.Compare);

            ordered = ordered is null
                ? rows.OrderBy(row => row.OrderKeys[index], comparer)
                : ordered.ThenBy(row => row.OrderKeys[index], comparer);
        }

        return ordered ?? rows;
    }

    private IEnumerable<ProjectedRow> Page(IEnumerable<ProjectedRow> rows)
    {
        if (_query.Pagination is null)
        {
            return rows;
        }

        long skip = Count(_query.Pagination.Skip);
        long take = Count(_query.Pagination.Take);

        return rows.Skip((int)Math.Min(skip, int.MaxValue))
            .Take((int)Math.Min(take, int.MaxValue));
    }

    private static long Count(OneOf.OneOf<long, ParamInteger> count)
    {
        return count.Match(
            number => number,
            parameter => throw UnboundParameter.Error(parameter.Name)
        );
    }

    private Scope RowScope(JoinedRow row)
    {
        return new Scope(this, row, [], []);
    }

    private static bool ReadsRowField(object expression)
    {
        return ModelNode
            .Nodes(
                expression,
                node => Operator.Find(node.GetType()) is not { IsAggregate: true }
            )
            .Any(node => node is IField);
    }

    private sealed record ProjectedRow(
        IReadOnlyList<object?> Values,
        IReadOnlyList<object?> OrderKeys
    );
}
