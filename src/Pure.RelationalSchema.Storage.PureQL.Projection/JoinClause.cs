using PureQL.CSharp.Model;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One join of a query: the joined source, the join kind and its condition.
internal sealed record JoinClause(Source Source, JoinKind Kind, BooleanRow On)
{
    public static JoinClause Of(Join join)
    {
        return join.Match(
            entity => new JoinClause(
                new Source(entity.Alias ?? entity.Entity, entity.Entity, false),
                entity.Kind,
                entity.On
            ),
            subquery => new JoinClause(
                new Source(subquery.Alias ?? subquery.Subquery, subquery.Subquery, true),
                subquery.Kind,
                subquery.On
            )
        );
    }
}
