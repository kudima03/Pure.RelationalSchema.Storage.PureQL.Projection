using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One source of a query as the tree reads it: its rows, tuples of only the
// fields the query reads, and whether an outer join may leave it without a
// row.
internal sealed record SourceScan(
    string Name,
    IReadOnlyList<string> Fields,
    Expression Rows,
    bool Optional
)
{
    public Type RowType => Linq.ElementType(Rows.Type);

    public int Field(string name)
    {
        return Fields.ToList().IndexOf(name);
    }
}
