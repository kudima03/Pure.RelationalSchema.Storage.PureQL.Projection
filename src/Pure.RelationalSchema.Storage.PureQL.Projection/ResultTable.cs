using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Index;
using Pure.RelationalSchema.Abstractions.Table;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The schema of a query result: an unnamed table with one column per select
// item, named by its alias and typed by its declared PureQL type.
internal sealed record ResultTable : ITable
{
    public ResultTable(IEnumerable<TypedExpression> select)
    {
        Columns =
        [
            .. select.Select(column => (IColumn)
                new Column.Column(
                    new Primitives.String.String(column.Alias!),
                    ValueTypes.ColumnOf(column.Type)
                )
            ),
        ];
    }

    public IString Name { get; } = new EmptyString();

    public IEnumerable<IColumn> Columns { get; }

    public IEnumerable<IIndex> Indexes { get; } = [];
}
