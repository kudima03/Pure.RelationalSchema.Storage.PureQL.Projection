using System.Collections;
using System.Linq.Expressions;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Literal values are constants of their type's value type. A typed null
// literal has no value; a datetime literal is the instant it denotes, kept in
// UTC like every other datetime; an integer read as a decimal is that decimal.
internal static class Literals
{
    public static Expression Value(ILiteral literal)
    {
        return Expression.Constant(Of(literal), ValueTypes.Clr(literal.Type));
    }

    // The value of a scalar literal.
    public static object? Of(ILiteral literal)
    {
        return ModelNode.Has(literal, "Value")
            ? Normalize(ModelNode.Property(literal, "Value"))
            : null;
    }

    // The items of a list literal, in an array of the type they are read as.
    public static Array Items(ILiteral list, Type item)
    {
        object?[] values =
        [
            .. ((IEnumerable)ModelNode.Child(list, "Value"))
                .Cast<object>()
                .Select(Normalize),
        ];

        Array array = Array.CreateInstance(item, values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            array.SetValue(As(values[i], item), i);
        }

        return array;
    }

    public static object? As(object? value, Type type)
    {
        return type == typeof(decimal?) && value is long number ? (decimal)number : value;
    }

    private static object? Normalize(object? value)
    {
        return value is DateTimeOffset instant ? instant.UtcDateTime : value;
    }
}
