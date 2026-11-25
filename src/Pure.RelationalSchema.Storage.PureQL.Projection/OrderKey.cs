using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One orderBy item: an expression of the query's context, or the alias of a
// select column of the query, whose expression it then sorts by.
internal sealed record OrderKey(object? Expression, string? Column, SortDirection Direction)
{
    public static OrderKey Of(object item)
    {
        object leaf = ModelNode.Unwrap(item);

        return new OrderKey(
            ModelNode.Optional(leaf, "Expression"),
            (string?)ModelNode.Optional(leaf, "Column"),
            (SortDirection)ModelNode.Child(leaf, "Direction")
        );
    }
}
