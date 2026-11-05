using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The rows of a stored table a PureQL condition is true for: a row for which
// it is false or null is not a row of this table, as with a query's where.
// The condition is a row expression over the table's own columns, compiled as
// a query's is: a field is the typed read of the column it names
// (StoredColumn), null for an empty cell when the field is declared nullable
// and parsed as it is when declared non-null. A field's source is not read:
// every field of a table's condition reads that table, whatever a query
// calls it. The condition is closed: it reads no parameter, no group key and
// no subquery column.
//
// The table is its origin's rows under a Where, origin.Where(row =>
// condition), run by the origin's provider, so a provider that translates
// queries filters in the store; a provider that refuses the Where fails the
// read. A query tree roots a scan of the table on that Where over the
// constant holding the origin (Root).
//
// The condition is checked whenever the predicate is built, so before any
// row is read: a field that names no column, a field declared with another
// type than its column holds, or a read of a parameter, a group key or a
// subquery column is an ArgumentException. The rows are never read
// unfiltered.
internal sealed class FilteredTable(IStoredTableDataSet origin, BooleanRow condition)
    : IStoredTableDataSet
{
    private readonly IStoredTableDataSet _origin = origin;

    private readonly BooleanRow _condition = condition;

    public ITable TableSchema => _origin.TableSchema;

    public Type ElementType => typeof(IRow);

    public Expression Expression => Linq.Where(_origin.Expression, Predicate());

    public IQueryProvider Provider => _origin.Provider;

    // The root of a stored table in a query tree: the constant holding the
    // table, which providers and the in-memory run recognize, under the Where
    // of each condition the table is filtered by.
    public static Expression Root(IStoredTableDataSet table)
    {
        return table is FilteredTable filtered
            ? Linq.Where(Root(filtered._origin), filtered.Predicate())
            : Expression.Constant(table, typeof(IQueryable<IRow>));
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        return Rows().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return Rows().ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    }

    // The table read on its own: by the origin's provider, or in memory, as
    // a projection's tree is, when the origin is held in memory.
    private IEnumerable<IRow> Rows()
    {
        return _origin.Provider is EnumerableQuery
            ? LocalProvider.Run<IRow>(Root(this))
            : _origin.Provider.CreateQuery<IRow>(Expression);
    }

    // row => the condition is true, once the condition is checked.
    private LambdaExpression Predicate()
    {
        string table = TableSchema.Name.TextValue;

        foreach (object node in ModelNode.Nodes(_condition, _ => true))
        {
            if (node is IField field)
            {
                _ = Column(field);
            }
            else if (Outside(node) is string read)
            {
                throw new ArgumentException(
                    $"The condition of table '{table}' reads {read}, and a "
                        + "table's condition reads only the table's fields."
                );
            }
        }

        ParameterExpression row = Expression.Parameter(typeof(IRow), table);

        return Expression.Lambda(
            new Scope(
                null,
                field => StoredColumn.Read(row, Column(field), field.Type.Nullable),
                null
            ).Test(_condition),
            row
        );
    }

    // What a node reads outside the table's row, if it does.
    private static string? Outside(object node)
    {
        return node switch
        {
            IParameter parameter => $"parameter '{parameter.Name}'",
            IKey key => $"group key '{key.Key}'",
            _ => ModelNode.Has(node, "Subquery")
                ? $"column '{ModelNode.Child(node, "Field")}' of subquery "
                    + $"'{ModelNode.Child(node, "Subquery")}'"
                : null,
        };
    }

    // The column a field of the condition reads, by its name.
    private IColumn Column(IField field)
    {
        string table = TableSchema.Name.TextValue;
        IColumn column =
            TableSchema.Columns.FirstOrDefault(column =>
                column.Name.TextValue == field.Field
            )
            ?? throw new ArgumentException(
                $"The condition of table '{table}' reads field '{field.Field}', "
                    + "which the table does not have."
            );
        string? type = ValueTypes.OfColumn(column.Type);

        return type == field.Type.Name
            ? column
            : throw new ArgumentException(
                $"Field '{field.Field}' of the condition of table '{table}' is "
                    + $"declared {field.Type.Name} but holds "
                    + $"{type ?? "a type PureQL does not have"}."
            );
    }
}
