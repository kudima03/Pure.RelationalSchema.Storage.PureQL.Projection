using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Index;
using Pure.RelationalSchema.Abstractions.Table;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal sealed record TableFromQuery : ITable
{
    public TableFromQuery(Query query)
        : this(new EmptyString(), new SelectColumns(query.SelectExpressions), []) { }

    internal TableFromQuery(
        IString name,
        IEnumerable<IColumn> columns,
        IEnumerable<IIndex> indexes
    )
    {
        Name = name;
        Columns = columns;
        Indexes = indexes;
    }

    public IString Name { get; }

    public IEnumerable<IColumn> Columns { get; }

    public IEnumerable<IIndex> Indexes { get; }
}
