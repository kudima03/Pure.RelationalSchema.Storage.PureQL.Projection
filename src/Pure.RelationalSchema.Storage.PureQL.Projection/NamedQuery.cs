namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A subquery: what it holds under the name sources read it by.
internal sealed record NamedQuery(string Name, SubqueryBody Body)
{
    public static NamedQuery Of(object subquery)
    {
        object leaf = ModelNode.Unwrap(subquery);

        return new NamedQuery(
            (string)ModelNode.Child(leaf, "Name"),
            SubqueryBody.Of(leaf)
        );
    }
}
