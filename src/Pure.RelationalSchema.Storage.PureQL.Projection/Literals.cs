using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Literal values. A typed null literal has no value; a datetime literal is
// the instant it denotes, kept in UTC like every other datetime.
internal static class Literals
{
    public static object? Value(ILiteral literal)
    {
        return ModelNode.Has(literal, "Value")
            ? Normalize(ModelNode.Property(literal, "Value"))
            : null;
    }

    public static object? Normalize(object? value)
    {
        return value is DateTimeOffset instant ? instant.UtcDateTime : value;
    }
}
