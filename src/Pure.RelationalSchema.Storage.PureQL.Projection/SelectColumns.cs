using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.ColumnType;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Returnings;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The single source of truth for the output columns of a query: one column
// per select expression, named by the alias (falling back to the underlying
// field name) and typed by the expression's value type. Used both for the
// projected table schema and for the projected row cells, so they never
// diverge.
internal sealed record SelectColumns : IEnumerable<IColumn>
{
    private readonly IEnumerable<SelectExpression> _expressions;

    public SelectColumns(IEnumerable<SelectExpression> expressions)
    {
        _expressions = expressions;
    }

    public IEnumerator<IColumn> GetEnumerator()
    {
        return _expressions.Select(OutputColumn).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal static IColumn OutputColumn(SelectExpression expression)
    {
        return expression.TryPickT0(
            out SingleValueReturning singleValue,
            out ArrayReturning arrayReturning
        )
            ? new Column.Column(
                new String(expression.Alias ?? string.Empty),
                SingleValueType(singleValue)
            )
            : new Column.Column(
                new String(expression.Alias ?? FieldName(arrayReturning)),
                ArrayType(arrayReturning)
            );
    }

    internal static string FieldName(ArrayReturning returning)
    {
        return returning.Match(
            b => b.AsT1.Field,
            d => d.AsT1.Field,
            dt => dt.AsT1.Field,
            n => n.AsT1.Field,
            s => s.AsT1.Field,
            t => t.AsT1.Field,
            u => u.AsT1.Field
        );
    }

    internal static string FieldEntity(ArrayReturning returning)
    {
        return returning.Match(
            b => b.AsT1.Entity,
            d => d.AsT1.Entity,
            dt => dt.AsT1.Entity,
            n => n.AsT1.Entity,
            s => s.AsT1.Entity,
            t => t.AsT1.Entity,
            u => u.AsT1.Entity
        );
    }

    private static IColumnType SingleValueType(SingleValueReturning returning)
    {
        return returning.Match<IColumnType>(
            _ => new BoolColumnType(),
            _ => new DateColumnType(),
            _ => new DateTimeColumnType(),
            _ => new DoubleColumnType(),
            _ => new StringColumnType(),
            _ => new TimeColumnType(),
            _ => new UuidColumnType()
        );
    }

    private static IColumnType ArrayType(ArrayReturning returning)
    {
        return returning.Match<IColumnType>(
            _ => new BoolColumnType(),
            _ => new DateColumnType(),
            _ => new DateTimeColumnType(),
            _ => new DoubleColumnType(),
            _ => new StringColumnType(),
            _ => new TimeColumnType(),
            _ => new UuidColumnType()
        );
    }
}
