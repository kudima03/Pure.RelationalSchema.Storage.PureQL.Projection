using System.Collections;
using Pure.Primitives.Number;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

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
            .Range(0, 10)
            .Select(c => new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    _columns,
                    x => x,
                    _ => new Cell(
                        new ConcatenatedString(new String("test"), new String(new Int(c)))
                    ),
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
