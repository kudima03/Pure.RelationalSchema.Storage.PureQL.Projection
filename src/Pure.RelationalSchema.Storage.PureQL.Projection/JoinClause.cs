using PureQL.CSharp.Model;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One join of a query: the joined source, the join type and its condition.
internal sealed record JoinClause(Source Source, JoinType Type, BooleanRow On)
{
    public static JoinClause Of(Join join)
    {
        return join.Match(
            entity => new JoinClause(
                new Source(entity.Alias ?? entity.Entity, entity.Entity, false),
                entity.Type,
                entity.On
            ),
            subquery => new JoinClause(
                new Source(subquery.Alias ?? subquery.Subquery, subquery.Subquery, true),
                subquery.Type,
                subquery.On
            )
        );
    }
}
