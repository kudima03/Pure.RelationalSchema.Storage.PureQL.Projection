using System.Collections;
using Pure.Primitives.Random.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;

internal sealed record FakeRows : IEnumerable<IRow>
{
    private readonly IEnumerable<IColumn> _columns;

    public FakeRows(IEnumerable<IColumn> columns)
    {
        _columns = columns;
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        return Enumerable
            .Range(0, 100)
            .Select(_ => new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    _columns,
                    x => x,
                    _ => new Cell(new RandomString()),
                    x => new ColumnHash(x)
                )
            ))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
