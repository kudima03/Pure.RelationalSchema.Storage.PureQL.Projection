using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The tree of a query: an IQueryable of value tuples, one item per select
// column, in column order.
internal sealed record CompiledQuery(
    Expression Rows,
    IReadOnlyList<TypedExpression> Columns
)
{
    public Type RowType => Linq.ElementType(Rows.Type);

    public int Column(string alias)
    {
        return Columns.Select(column => column.Alias).ToList().IndexOf(alias);
    }
}
