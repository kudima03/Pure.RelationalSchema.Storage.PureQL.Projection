using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Random.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Index;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeTable : ITable
{
    public IString Name => new RandomString();

    public IEnumerable<IColumn> Columns =>
        [new Column.Column(new RandomString(), new StringColumnType())];

    public IEnumerable<IIndex> Indexes => [];
}
