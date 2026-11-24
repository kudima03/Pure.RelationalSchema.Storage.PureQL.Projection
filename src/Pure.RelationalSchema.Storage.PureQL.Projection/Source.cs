namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A source a query reads from: an entity ("schema.table") or a subquery,
// named by its alias or, without one, by what it reads.
internal sealed record Source(string Name, string Target, bool IsSubquery)
{
    public static Source Of(object from)
    {
        object leaf = ModelNode.Unwrap(from);
        bool subquery = ModelNode.Has(leaf, "Subquery");
        string target = (string)ModelNode.Child(leaf, subquery ? "Subquery" : "Entity");

        return new Source(
            (string?)ModelNode.Optional(leaf, "Alias") ?? target,
            target,
            subquery
        );
    }
}
