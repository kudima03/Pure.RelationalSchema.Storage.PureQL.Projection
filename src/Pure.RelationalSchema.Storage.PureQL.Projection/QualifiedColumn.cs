using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A joined table's column carried through the pipeline, tagged with the
// "schema.table" entity it came from. Same-named columns from different
// tables stay distinct in a merged row, and CellValueExtractor resolves an
// entity-qualified field reference to the column of the matching entity.
// A class (not a record) on purpose: the wrapped column's Equals/GetHashCode
// throw by design, so identity semantics must not delegate to it.
internal sealed class QualifiedColumn(string entity, IColumn origin) : IColumn
{
    private readonly IColumn _origin = origin;

    public string Entity { get; } = entity;

    public IString Name => _origin.Name;

    public IColumnType Type => _origin.Type;
}
