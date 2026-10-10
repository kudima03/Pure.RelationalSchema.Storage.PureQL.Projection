using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A whole PureQL document: the main query and the subqueries it declares, in
// declaration order.
internal sealed record QueryProgram(
    QueryClauses Main,
    IReadOnlyList<NamedQuery> Subqueries
)
{
    public static QueryProgram Of(PureQLQuery query)
    {
        return query.Match(
            grouped => new QueryProgram(
                QueryClauses.Of(grouped),
                [.. (grouped.Subqueries ?? []).Select(Named)]
            ),
            plain => new QueryProgram(
                QueryClauses.Of(plain),
                [.. (plain.Subqueries ?? []).Select(Named)]
            )
        );
    }

    public static QueryProgram Of(Query query)
    {
        return new QueryProgram(QueryClauses.Of(query), []);
    }

    private static NamedQuery Named(Subquery subquery)
    {
        return new NamedQuery(subquery.Name, QueryClauses.Of(subquery.Query));
    }
}
