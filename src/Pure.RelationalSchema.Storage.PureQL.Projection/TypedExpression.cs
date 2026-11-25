using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A select or aggregate column, or a groupBy key: an expression with the
// alias that names it and the type its expression declares.
internal sealed record TypedExpression(string Alias, IType Type, object Expression)
{
    public static TypedExpression Of(object item)
    {
        object expression = ModelNode.Unwrap(ModelNode.Child(item, "Expression"));

        return new TypedExpression(
            (string)ModelNode.Child(item, "Alias"),
            ModelNode.TypeOf(expression),
            expression
        );
    }
}
