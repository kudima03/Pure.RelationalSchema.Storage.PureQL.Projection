using PureQL.CSharp.Model;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The clauses of one query, the main query or a subquery: plain, grouped or
// total. GroupBy is null except in a grouped query; a total query's aggregate
// columns are its Select, and it has no having, orderBy, distinctBy or
// pagination.
internal sealed record QueryClauses(
    Source From,
    IReadOnlyList<JoinClause> Joins,
    BooleanRow? Where,
    IReadOnlyList<TypedExpression>? GroupBy,
    BooleanGroup? Having,
    IReadOnlyList<TypedExpression> Select,
    IReadOnlyList<OrderKey> OrderBy,
    IReadOnlyList<string> DistinctBy,
    Pagination? Pagination,
    bool IsTotal
)
{
    public static QueryClauses Of(Query query)
    {
        return query.Match(Of, Of, Of);
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
            [.. query.DistinctBy ?? []],
            query.Pagination,
            false
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
            [.. query.DistinctBy ?? []],
            query.Pagination,
            false
        );
    }

    public static QueryClauses Of(MainTotalQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            null,
            null,
            [.. query.Aggregate.Select(TypedExpression.Of)],
            [],
            [],
            null,
            true
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
            [.. query.DistinctBy ?? []],
            query.Pagination,
            false
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
            [.. query.DistinctBy ?? []],
            query.Pagination,
            false
        );
    }

    private static QueryClauses Of(TotalQuery query)
    {
        return new QueryClauses(
            Source.Of(query.From),
            [.. (query.Joins ?? []).Select(JoinClause.Of)],
            query.Where,
            null,
            null,
            [.. query.Aggregate.Select(TypedExpression.Of)],
            [],
            [],
            null,
            true
        );
    }
}
