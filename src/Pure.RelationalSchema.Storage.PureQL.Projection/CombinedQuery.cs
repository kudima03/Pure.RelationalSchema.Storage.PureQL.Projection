namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A combined query: the rows of two or more queries put together by one set
// operation. It has no clause of its own; its columns are those of its
// inputs, matched by alias, in the order of the first input.
internal sealed record CombinedQuery(
    SetOperation Operation,
    IReadOnlyList<QueryShape> Inputs
) : QueryShape
{
    // The operation a query record names, if it is a combined query.
    public static SetOperation? OperationOf(object query)
    {
        foreach (SetOperation operation in Enum.GetValues<SetOperation>())
        {
            if (ModelNode.Has(query, operation.ToString()))
            {
                return operation;
            }
        }

        return null;
    }
}
