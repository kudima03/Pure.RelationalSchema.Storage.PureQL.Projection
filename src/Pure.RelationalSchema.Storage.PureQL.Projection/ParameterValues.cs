using System.Linq.Expressions;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The literals a query's parameters are bound to, by name. A parameter is a
// captured variable in the tree, the Value of a QueryParameter constant, as
// C# closures are, so a provider can pass it as a SQL parameter; it holds its
// literal's value. A literal binds a parameter of its type, a typed null only
// a nullable one, and an integer (list) a decimal (list), as an integer
// stands for a decimal in a decimal expression. A parameter given no literal
// fails the query when it is validated, as a literal of another type does
// (issue #251), instead of guessing a value.
internal sealed class ParameterValues(IReadOnlyDictionary<string, ILiteral> values)
{
    private readonly IReadOnlyDictionary<string, ILiteral> _values =
        new Dictionary<string, ILiteral>(values, StringComparer.Ordinal);

    public static ParameterValues None { get; } = new(new Dictionary<string, ILiteral>());

    // A scalar parameter has its type's value type, a list parameter is a
    // sequence of its item type.
    public Expression Value(IParameter parameter)
    {
        Type item = ValueTypes.Clr(parameter.Type);
        Type type = IsList(parameter.Type)
            ? typeof(IEnumerable<>).MakeGenericType(item)
            : item;

        return Expression.Property(
            Expression.Constant(
                Activator.CreateInstance(
                    typeof(QueryParameter<>).MakeGenericType(type),
                    parameter.Name,
                    Bound(parameter)
                )
            ),
            nameof(QueryParameter<>.Value)
        );
    }

    // Rejects a parameter given no literal, or a literal of another type than
    // the parameter's.
    public void Check(IParameter parameter)
    {
        _ = Bound(parameter);
    }

    public long Integer(ParamInteger parameter)
    {
        return (long)Bound(parameter)!;
    }

    private static bool IsList(IType type)
    {
        return type.Name.EndsWith("List", StringComparison.Ordinal);
    }

    private static bool Binds(IType parameter, IType literal)
    {
        bool typed =
            literal.Name == parameter.Name
            || (parameter.Name, literal.Name)
                is ("decimal", "integer")
                    or ("decimalList", "integerList");

        return typed && (parameter.Nullable || !literal.Nullable);
    }

    private static string Describe(IType type, string nullable)
    {
        return type.Nullable ? $"{nullable} {type.Name}" : type.Name;
    }

    // A parameter's value as the tree holds it: a value of its type's value
    // type, or the items of a list in an array.
    private object? Bound(IParameter parameter)
    {
        if (!_values.TryGetValue(parameter.Name, out ILiteral? literal))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' is not bound to a value."
            );
        }

        if (!Binds(parameter.Type, literal.Type))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' is {Describe(parameter.Type, "nullable")} "
                    + $"and cannot be bound to a {Describe(literal.Type, "null")} literal."
            );
        }

        Type item = ValueTypes.Clr(parameter.Type);

        return IsList(parameter.Type)
            ? Literals.Items(literal, item)
            : Literals.As(Literals.Of(literal), item);
    }
}
