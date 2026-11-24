namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What a subquery holds: a query, rows written in the document, or a
// recursion.
internal abstract record SubqueryBody
{
    public static SubqueryBody Of(object subquery)
    {
        return ModelNode.Optional(subquery, "Rows") is not null
                ? new InlineRows(
                    [
                        .. ModelNode
                            .Children(subquery, "Rows")
                            .Select(row =>
                                (IReadOnlyList<TypedExpression>)
                                    [
                                        .. ((IEnumerable<object>)row).Select(
                                            TypedExpression.Of
                                        ),
                                    ]
                            ),
                    ]
                )
            : ModelNode.Optional(subquery, "Recursive") is object recursion
                ? new RecursiveQuery(
                    QueryShape.Of(ModelNode.Child(recursion, "Seed")),
                    QueryClauses.Of(ModelNode.Child(recursion, "Next")),
                    ModelNode.Unwrap(ModelNode.Child(recursion, "MaxDepth"))
                )
            : new PlainSubquery(QueryShape.Of(ModelNode.Child(subquery, "Query")));
    }
}

// A subquery that is a query.
internal sealed record PlainSubquery(QueryShape Query) : SubqueryBody;

// Rows written in the document: each a list of cells, a literal or a
// parameter under an alias.
internal sealed record InlineRows(IReadOnlyList<IReadOnlyList<TypedExpression>> Rows)
    : SubqueryBody;

// A recursive subquery: level 0 is the seed, each next level the next query
// run with the subquery's own name standing for the rows of the level before;
// MaxDepth is a number or an integer parameter.
internal sealed record RecursiveQuery(QueryShape Seed, QueryClauses Next, object MaxDepth)
    : SubqueryBody;
