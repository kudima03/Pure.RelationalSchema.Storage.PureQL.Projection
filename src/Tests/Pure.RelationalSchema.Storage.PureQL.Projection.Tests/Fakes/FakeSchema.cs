using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Random.String;
using Pure.RelationalSchema.Abstractions.ForeignKey;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeSchema : ISchema
{
    public IString Name => new RandomString();

    public IEnumerable<ITable> Tables => [new FakeTable()];

    public IEnumerable<IForeignKey> ForeignKeys => [];
}
