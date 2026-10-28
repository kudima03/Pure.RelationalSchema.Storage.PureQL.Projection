using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The read of one stored cell as a typed value: the invariant text the cell
// holds, row.Cells[column].Value.TextValue, parsed by the column's PureQL
// type. A cell the query declares nullable is null when the text is empty,
// the storage layer's NULL; a cell it declares non-null is parsed as it is,
// so the value is never null and a provider sees a non-null expression, which
// SQL can hash and index a join or a filter on (issue #243), and empty text
// fails the parse, as a null is not a value of the declared type. A datetime
// without an offset is read as UTC. Every read has one of these two shapes,
// so the column it reads can be recognized in the tree.
internal static class StoredColumn
{
    private static readonly MethodInfo CellOf = typeof(
        IReadOnlyDictionary<IColumn, ICell>
    ).GetMethod("get_Item")!;

    private static readonly MethodInfo IsNullOrEmpty = typeof(string).GetMethod(
        nameof(string.IsNullOrEmpty),
        [typeof(string)]
    )!;

    private static readonly Expression Invariant = Expression.Property(
        null,
        typeof(CultureInfo),
        nameof(CultureInfo.InvariantCulture)
    );

    public static Expression Read(Expression row, IColumn column, bool nullable)
    {
        Expression text = Expression.Property(
            Expression.Property(
                Expression.Call(
                    Expression.Property(row, nameof(IRow.Cells)),
                    CellOf,
                    Expression.Constant(column, typeof(IColumn))
                ),
                nameof(ICell.Value)
            ),
            nameof(IString.TextValue)
        );

        string type = ValueTypes.OfColumn(column.Type)!;
        Type clr = ValueTypes.Clr(type);

        Expression value = Typed.As(Parse(text, type), clr);

        return nullable
            ? Expression.Condition(
                Expression.Call(IsNullOrEmpty, text),
                Typed.Null(clr),
                value
            )
            : value;
    }

    private static Expression Parse(Expression text, string type)
    {
        return type switch
        {
            "integer" => Call(
                typeof(long),
                nameof(long.Parse),
                text,
                Expression.Constant(NumberStyles.Integer),
                Invariant
            ),
            "decimal" => Call(
                typeof(decimal),
                nameof(decimal.Parse),
                text,
                Expression.Constant(NumberStyles.Float),
                Invariant
            ),
            "boolean" => Call(typeof(bool), nameof(bool.Parse), text),
            "date" => Call(typeof(DateOnly), nameof(DateOnly.Parse), text, Invariant),
            "time" => Call(typeof(TimeOnly), nameof(TimeOnly.Parse), text, Invariant),
            "datetime" => Call(
                typeof(DateTime),
                nameof(DateTime.Parse),
                text,
                Invariant,
                Expression.Constant(
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                )
            ),
            "uuid" => Call(typeof(Guid), nameof(Guid.Parse), text),
            _ => text,
        };
    }

    private static MethodCallExpression Call(
        Type type,
        string method,
        params Expression[] arguments
    )
    {
        return Expression.Call(
            type.GetMethod(method, [.. arguments.Select(argument => argument.Type)])!,
            arguments
        );
    }
}
