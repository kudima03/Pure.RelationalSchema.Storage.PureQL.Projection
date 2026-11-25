namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A query of a document: the clauses of a plain, grouped or total query, or
// a set operation over queries. The model has one record per variant of a
// query (with or without pagination, with or without orderBy, main or not),
// all with the same property names, so a query is read by what it has.
internal abstract record QueryShape
{
    public static QueryShape Of(object query)
    {
        object leaf = ModelNode.Unwrap(query);

        return CombinedQuery.OperationOf(leaf) is SetOperation operation
            ? new CombinedQuery(
                operation,
                [.. ModelNode.Children(leaf, operation.ToString()).Select(Of)]
            )
            : QueryClauses.Of(leaf);
    }
}
