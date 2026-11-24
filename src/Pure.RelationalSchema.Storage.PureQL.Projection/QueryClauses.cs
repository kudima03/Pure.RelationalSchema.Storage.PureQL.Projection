using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The clauses of one plain, grouped or total query, or of the next of a
// recursive subquery. GroupBy is null except in a grouped query; a total
// query's aggregate columns are its Select, and it has no having, orderBy,
// distinctBy or pagination.
internal sealed record QueryClauses(
    Source From,
    IReadOnlyList<JoinClause> Joins,
    object? Where,
    IReadOnlyList<TypedExpression>? GroupBy,
    object? Having,
    IReadOnlyList<TypedExpression> Select,
    IReadOnlyList<OrderKey> OrderBy,
    IReadOnlyList<string> DistinctBy,
    Pagination? Pagination,
    bool IsTotal
) : QueryShape
{
    public static new QueryClauses Of(object query)
    {
        object leaf = ModelNode.Unwrap(query);
        bool total = ModelNode.Has(leaf, "Aggregate");

        return new QueryClauses(
            Source.Of(ModelNode.Child(leaf, "From")),
            [.. ModelNode.OptionalChildren(leaf, "Joins").Select(JoinClause.Of)],
            ModelNode.Optional(leaf, "Where"),
            ModelNode.Has(leaf, "GroupBy")
                ? [.. ModelNode.Children(leaf, "GroupBy").Select(TypedExpression.Of)]
                : null,
            ModelNode.Optional(leaf, "Having"),
            [
                .. ModelNode
                    .Children(leaf, total ? "Aggregate" : "Select")
                    .Select(TypedExpression.Of),
            ],
            [.. ModelNode.OptionalChildren(leaf, "OrderBy").Select(OrderKey.Of)],
            [.. ModelNode.OptionalChildren(leaf, "DistinctBy").Cast<string>()],
            (Pagination?)ModelNode.Optional(leaf, "Pagination"),
            total
        );
    }

    // The expression an orderBy item sorts by: its own, or that of the select
    // column it names.
    public object Sorted(OrderKey key)
    {
        return key.Expression
            ?? Select.First(column => column.Alias == key.Column).Expression;
    }
}
