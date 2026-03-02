using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.ColumnType;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Returnings;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal sealed record ColumnsFromQuery : IEnumerable<IColumn>
{
    private readonly IEnumerable<SelectExpression> _expressions;

    public ColumnsFromQuery(IEnumerable<SelectExpression> expressions)
    {
        _expressions = expressions;
    }

    public IEnumerator<IColumn> GetEnumerator()
    {
        return _expressions
            .Select<SelectExpression, (string?, IColumnType)>(x =>
                (
                    x.Alias,
                    x.TryPickT0(out SingleValueReturning singleValue, out _)
                        ? singleValue.IsT0
                            ? new BoolColumnType()
                            : singleValue.IsT1
                                ? new DateColumnType()
                                : singleValue.IsT2
                                    ? new DateTimeColumnType()
                                    : singleValue.IsT3
                                        ? new DoubleColumnType()
                                        : singleValue.IsT4
                                            ? new StringColumnType()
                                            : singleValue.IsT5
                                                ? new TimeColumnType()
                                                : singleValue.IsT6
                                                    ? new UuidColumnType()
                                                    : throw new NotSupportedException()
                        : x.TryPickT1(out ArrayReturning arrayReturning, out _)
                            ? arrayReturning.IsT0
                                ? new BoolColumnType()
                                : arrayReturning.IsT1
                                    ? new DateColumnType()
                                    : arrayReturning.IsT2
                                        ? new DateTimeColumnType()
                                        : arrayReturning.IsT3
                                            ? new DoubleColumnType()
                                            : arrayReturning.IsT4
                                                ? new StringColumnType()
                                                : arrayReturning.IsT5
                                                    ? new TimeColumnType()
                                                    : arrayReturning.IsT6
                                                        ? new UuidColumnType()
                                                        : throw new NotSupportedException()
                            : throw new NotSupportedException()
                )
            )
            .Select(x => new Column.Column(new String(x.Item1!), x.Item2))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
