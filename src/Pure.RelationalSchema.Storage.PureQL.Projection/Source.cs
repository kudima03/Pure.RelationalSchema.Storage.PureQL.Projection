using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A source a query reads from: an entity ("schema.table") or a subquery,
// named by its alias or, without one, by what it reads.
internal sealed record Source(string Name, string Target, bool IsSubquery)
{
    public static Source Of(From from)
    {
        return from.Match(
            entity => new Source(entity.Alias ?? entity.Entity, entity.Entity, false),
            subquery => new Source(
                subquery.Alias ?? subquery.Subquery,
                subquery.Subquery,
                true
            )
        );
    }
}
