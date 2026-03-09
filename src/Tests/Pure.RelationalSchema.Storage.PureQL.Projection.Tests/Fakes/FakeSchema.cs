using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Number;
using Pure.Primitives.Random.String;
using Pure.RelationalSchema.Abstractions.ForeignKey;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Char = Pure.Primitives.Char.Char;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeSchema : ISchema
{
    public IString Name { get; } =
        new RandomString(new UShort(5), new Char('a'), new Char('z'));

    public IEnumerable<ITable> Tables { get; } = [new FakeTable()];

    public IEnumerable<IForeignKey> ForeignKeys => [];
}
