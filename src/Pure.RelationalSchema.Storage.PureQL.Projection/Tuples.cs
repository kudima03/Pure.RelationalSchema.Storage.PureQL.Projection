using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Every row inside the tree is a value tuple: a source row holds the fields
// the query reads, a joined row one source row per source, a group key one
// value per key and a query result one value per column. A tuple of more than
// seven items nests the rest in its eighth, Rest, as C# tuples do. The default
// of a tuple of nullable items is all nulls, which is what an unmatched outer
// join side reads.
internal static class Tuples
{
    private const int Direct = 7;

    private static readonly Type[] Definitions =
    [
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    public static Type Of(IReadOnlyList<Type> items)
    {
        return items.Count switch
        {
            0 => typeof(ValueTuple),
            <= Direct => Definitions[items.Count - 1].MakeGenericType([.. items]),
            _ => Definitions[Direct].MakeGenericType(
                [.. items.Take(Direct), Of([.. items.Skip(Direct)])]
            ),
        };
    }

    public static Expression New(IReadOnlyList<Expression> items)
    {
        if (items.Count == 0)
        {
            return Expression.New(typeof(ValueTuple));
        }

        IReadOnlyList<Expression> arguments = items.Count <= Direct
            ? items
            : [.. items.Take(Direct), New([.. items.Skip(Direct)])];

        Type type = Of([.. items.Select(item => item.Type)]);
        ConstructorInfo constructor = type.GetConstructor(
            [.. arguments.Select(argument => argument.Type)]
        )!;

        return Expression.New(constructor, arguments);
    }

    public static Expression Item(Expression tuple, int index)
    {
        return index < Direct
            ? Expression.Field(tuple, $"Item{index + 1}")
            : Item(Expression.Field(tuple, "Rest"), index - Direct);
    }

    public static bool IsTuple(Type type)
    {
        return type == typeof(ValueTuple)
            || (
                type.IsGenericType
                && Definitions.Contains(type.GetGenericTypeDefinition())
            );
    }

    // The constructor argument a tuple field reads: Item1 to Item7, then Rest.
    public static int Position(string field)
    {
        return field == "Rest" ? Direct : int.Parse(field[4..]) - 1;
    }
}
