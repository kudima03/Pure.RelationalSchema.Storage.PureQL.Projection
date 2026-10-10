using PureQL.CSharp.Model.GroupKeys;
using PureQL.CSharp.Model.SelectItems;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A select column or a groupBy key: an expression with the type it declares
// and, for a column, the alias that names it.
internal sealed record TypedExpression(string? Alias, IType Type, object Expression)
{
    public static TypedExpression Of(SelectItemProjection item)
    {
        return Of((ISelectItem)ModelNode.Unwrap(item));
    }

    public static TypedExpression Of(SelectItemGroup item)
    {
        return Of((ISelectItem)ModelNode.Unwrap(item));
    }

    public static TypedExpression Of(GroupKey key)
    {
        IGroupKey leaf = (IGroupKey)ModelNode.Unwrap(key);
        return new TypedExpression(
            leaf.Alias,
            leaf.Type,
            ModelNode.Child(leaf, "Expression")
        );
    }

    private static TypedExpression Of(ISelectItem leaf)
    {
        return new TypedExpression(
            leaf.Alias,
            leaf.Type,
            ModelNode.Child(leaf, "Expression")
        );
    }
}
