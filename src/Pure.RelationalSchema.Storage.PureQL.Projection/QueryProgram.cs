namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A whole PureQL document: the main query and the subqueries it declares, in
// declaration order.
internal sealed record QueryProgram(QueryShape Main, IReadOnlyList<NamedQuery> Subqueries)
{
    public static QueryProgram Of(object query)
    {
        object leaf = ModelNode.Unwrap(query);

        return new QueryProgram(
            QueryShape.Of(leaf),
            [.. ModelNode.OptionalChildren(leaf, "Subqueries").Select(NamedQuery.Of)]
        );
    }
}
