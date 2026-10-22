using System.Linq.Expressions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What an expression compiles against. In row context that is one row, whose
// fields are members of a tuple parameter; in projection context a row
// (absent in a total query) and every row left by where, which aggregates
// fold; in group context a group parameter, whose key holds the group key
// values and whose elements are the group's rows.
internal sealed class Scope(
    QueryTree query,
    Func<IField, Expression>? field,
    ParameterExpression? group
    )
{
    private readonly QueryTree _query = query;

    private readonly Func<IField, Expression>? _field = field;

    private readonly ParameterExpression? _group = group;

    public Expression Compile(object node)
    {
        object leaf = ModelNode.Unwrap(node);

        return leaf switch
        {
            IField reference => Field(reference),
            IKey key => _query.Key(
                _group
                    ?? throw new InvalidOperationException(
                        $"Group key '{key.Key}' is read where there is no group."
                    ),
                key.Key
            ),
            IParameter parameter => Parameter(parameter),
            ILiteral literal => Literals.Value(literal),
            _ => Operator.Of(leaf.GetType()).Compile(leaf, this),
        };
    }

    public Expression Test(object condition)
    {
        return Typed.Test(Compile(condition));
    }

    // A group's own rows, or every row of the query.
    public Expression Rows(AggregateOver over)
    {
        return over == AggregateOver.Group && _group is not null
            ? _group
            : _query.AllRows;
    }

    public ParameterExpression RowParameter()
    {
        return Expression.Parameter(_query.RowType, "row");
    }

    // A row of the same query in row context, as aggregate bodies see it.
    public Scope Row(ParameterExpression row)
    {
        return _query.RowScope(row);
    }

    public Expression SubqueryColumn(string subquery, string field, Type item)
    {
        return _query.SubqueryColumn(subquery, field, item);
    }

    public Expression Parameter(IParameter parameter)
    {
        return _query.Parameter(parameter);
    }

    private Expression Field(IField field)
    {
        return (
            _field
            ?? throw new InvalidOperationException(
                $"Field '{field.Source}.{field.Field}' is read where there is no row."
            )
        )(field);
    }
}
