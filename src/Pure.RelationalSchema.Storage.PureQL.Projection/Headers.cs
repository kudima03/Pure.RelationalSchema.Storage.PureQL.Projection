using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The columns of a query and of a subquery: the select (or aggregate) aliases
// of a query, each with the type its expression declares; for a set operation
// and for inline rows the columns of the first input, each with the least
// type that accepts it in every input. Inputs that do not have the same
// aliases, each once, or whose types do not meet, are an ArgumentException.
internal static class Headers
{
    public static IReadOnlyList<QueryColumn> Of(QueryShape query)
    {
        return query switch
        {
            QueryClauses clauses => Of(clauses.Select),
            CombinedQuery combined => Merged(
                [.. combined.Inputs.Select(Of)],
                "The inputs of a set operation"
            ),
            _ => throw new NotSupportedException(
                $"{query.GetType().Name} is not a query this interpreter knows."
            ),
        };
    }

    public static IReadOnlyList<QueryColumn> Of(SubqueryBody body)
    {
        return body switch
        {
            PlainSubquery query => Of(query.Query),
            InlineRows rows => Merged([.. rows.Rows.Select(Of)], "The rows of a subquery"),
            RecursiveQuery recursive => Of(recursive.Seed),
            _ => throw new NotSupportedException(
                $"{body.GetType().Name} is not a subquery this interpreter knows."
            ),
        };
    }

    private static IReadOnlyList<QueryColumn> Of(IEnumerable<TypedExpression> columns)
    {
        return [.. columns.Select(column => new QueryColumn(column.Alias, column.Type))];
    }

    private static IReadOnlyList<QueryColumn> Merged(
        IReadOnlyList<IReadOnlyList<QueryColumn>> inputs,
        string what
    )
    {
        List<QueryColumn> merged = [.. inputs[0]];

        foreach (IReadOnlyList<QueryColumn> input in inputs)
        {
            string? repeated = input
                .GroupBy(column => column.Alias, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;

            if (repeated is not null)
            {
                throw new ArgumentException(
                    $"Column alias '{repeated}' is used more than once."
                );
            }

            if (
                input.Count != merged.Count
                || input.Any(column => merged.All(other => other.Alias != column.Alias))
            )
            {
                throw new ArgumentException($"{what} do not have the same column aliases.");
            }

            for (int i = 0; i < merged.Count; i++)
            {
                IType type = input.First(column => column.Alias == merged[i].Alias).Type;

                merged[i] = new QueryColumn(
                    merged[i].Alias,
                    DeclaredType.Least(merged[i].Type, type)
                        ?? throw new ArgumentException(
                            $"{what} give column '{merged[i].Alias}' the types "
                                + $"{merged[i].Type.Name} and {type.Name}, which no "
                                + "one type accepts."
                        )
                );
            }
        }

        return merged;
    }
}
