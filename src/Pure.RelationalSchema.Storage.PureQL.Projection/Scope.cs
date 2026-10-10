using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What an expression is evaluated against. In row context that is one joined
// row; in projection context a row (absent when the result is a single row)
// and every row left by where, which aggregates fold; in group context the
// rows and key values of one group.
internal sealed class Scope(
    QueryRun run,
    JoinedRow? row,
    IReadOnlyList<JoinedRow> rows,
    IReadOnlyList<object?> keys
    )
{
    private readonly QueryRun _run = run;

    private readonly JoinedRow? _row = row;

    private readonly IReadOnlyList<JoinedRow> _rows = rows;

    private readonly IReadOnlyList<object?> _keys = keys;

    public object? Evaluate(object node)
    {
        object leaf = ModelNode.Unwrap(node);

        return leaf switch
        {
            IField field => Field(field),
            IKey key => _keys[key.Key],
            IParameter parameter => throw UnboundParameter.Error(parameter.Name),
            ILiteral literal => Literals.Value(literal),
            _ => Operator.Of(leaf.GetType()).Evaluate(leaf, this),
        };
    }

    public bool Test(object condition)
    {
        return Evaluate(condition) is true;
    }

    // A row of the same query in row context, as aggregate bodies see it.
    public Scope Row(JoinedRow row)
    {
        return new Scope(_run, row, [], []);
    }

    public IReadOnlyList<JoinedRow> Rows(AggregateOver over)
    {
        return over == AggregateOver.All ? _run.AllRows : _rows;
    }

    // An aggregate over every row has one value for the whole query.
    public object? Aggregate(
        object node,
        IReadOnlyList<JoinedRow> rows,
        Func<object?> evaluate
    )
    {
        return ReferenceEquals(rows, _run.AllRows)
            ? _run.Once(node, evaluate)
            : evaluate();
    }

    public IEnumerable<object?> SubqueryColumn(string subquery, string field)
    {
        return _run.SubqueryColumn(subquery, field);
    }

    private object? Field(IField field)
    {
        JoinedRow row =
            _row
            ?? throw new InvalidOperationException(
                $"Field '{field.Source}.{field.Field}' is read where there is no row."
            );

        return row[_run.Source(field.Source)]?.Value(field.Field);
    }
}
