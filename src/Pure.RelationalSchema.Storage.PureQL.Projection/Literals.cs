using System.Linq.Expressions;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Literal values are constants of their type's value type. A typed null
// literal has no value; a datetime literal is the instant it denotes, kept in
// UTC like every other datetime.
internal static class Literals
{
    public static Expression Value(ILiteral literal)
    {
        return Expression.Constant(
            ModelNode.Has(literal, "Value")
                ? Normalize(ModelNode.Property(literal, "Value"))
                : null,
            ValueTypes.Clr(literal.Type)
        );
    }

    public static object? Normalize(object? value)
    {
        return value is DateTimeOffset instant ? instant.UtcDateTime : value;
    }
}
