using PureQL.CSharp.Model;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The clauses of one query, the main query or a subquery, grouped or plain.
// GroupBy is null exactly for a plain query.
internal sealed record QueryClauses(
    Source From,
    IReadOnlyList<JoinClause> Joins,
    BooleanRow? Where,
    IReadOnlyList<TypedExpression>? GroupBy,
    BooleanGroup? Having,
    IReadOnlyList<TypedExpression> Select,
    IReadOnlyList<OrderKey> OrderBy,
    Pagination? Pagination,
    bool Distinct
)
{
    public static QueryClauses Of(Query query)
    {
        return query.Match(Of, Of);
    }

    public static QueryClauses Of(MainPlainQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            null,
            null,
            [.. query.Select.Select(TypedExpression.Of)],
            [.. (query.OrderBy ?? []).Select(OrderKey.Of)],
            query.Pagination,
            query.Distinct
        );
    }

    public static QueryClauses Of(MainGroupedQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            [.. query.GroupBy.Select(TypedExpression.Of)],
            query.Having,
            [.. query.Select.Select(TypedExpression.Of)],
            [.. (query.OrderBy ?? []).Select(OrderKey.Of)],
            query.Pagination,
            query.Distinct
        );
    }

    private static QueryClauses Of(PlainQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            null,
            null,
            [.. query.Select.Select(TypedExpression.Of)],
            [.. (query.OrderBy ?? []).Select(OrderKey.Of)],
            query.Pagination,
            query.Distinct
        );
    }

    private static QueryClauses Of(GroupedQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            [.. query.GroupBy.Select(TypedExpression.Of)],
            query.Having,
            [.. query.Select.Select(TypedExpression.Of)],
            [.. (query.OrderBy ?? []).Select(OrderKey.Of)],
            query.Pagination,
            query.Distinct
        );
    }
}
