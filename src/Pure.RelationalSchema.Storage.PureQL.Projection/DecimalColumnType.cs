using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.ColumnType;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The column type of a decimal result column, named "decimal": its cells hold
// the exact invariant text of a decimal, up to 28 significant digits, where
// Pure.RelationalSchema's double and float column types hold binary floating
// point numbers, which a decimal beyond 15 significant digits does not fit
// (issue #252).
internal sealed record DecimalColumnType : IColumnType
{
    public IString Name { get; } = new Primitives.String.String("decimal");
}
