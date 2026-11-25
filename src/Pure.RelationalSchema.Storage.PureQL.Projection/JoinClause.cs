using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One join of a query: the joined source, the join kind and its condition.
internal sealed record JoinClause(Source Source, JoinKind Kind, object On)
{
    public static JoinClause Of(object join)
    {
        object leaf = ModelNode.Unwrap(join);

        return new JoinClause(
            Source.Of(leaf),
            (JoinKind)ModelNode.Child(leaf, "Kind"),
            ModelNode.Unwrap(ModelNode.Child(leaf, "On"))
        );
    }
}
