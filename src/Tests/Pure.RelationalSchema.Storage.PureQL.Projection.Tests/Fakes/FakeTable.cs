using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Number;
using Pure.Primitives.Random.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Index;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Char = Pure.Primitives.Char.Char;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeTable : ITable
{
    public IString Name { get; } =
        new RandomString(new UShort(5), new Char('a'), new Char('z'));

    public IEnumerable<IColumn> Columns { get; } =
    [
        new Column.Column(new RandomString(), new StringColumnType()),
        new Column.Column(new RandomString(), new StringColumnType()),
    ];

    public IEnumerable<IIndex> Indexes => [];
}
