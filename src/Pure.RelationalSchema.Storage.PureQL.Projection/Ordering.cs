using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Rows sorted by the keys the nodes compile to, in turn, each breaking the ties of the previous one:
// null sorts first ascending and last descending, as LINQ orders nullable
// values, and strings order ordinally, like every string comparison of the
// tree. A key that is the same for every row, and a key that repeats an
// earlier one, sort nothing and are dropped.
internal static class Ordering
{
    public static Expression Sorted(
        Expression rows,
        ParameterExpression row,
        IEnumerable<(object Node, bool Descending)> keys,
        Func<object, Expression> compile
    )
    {
        List<object> seen = [];
        Expression ordered = rows;

        foreach ((object node, bool descending) in keys)
        {
            object leaf = ModelNode.Unwrap(node);

            if (seen.Contains(leaf))
            {
                continue;
            }

            seen.Add(leaf);
            Expression body = Simplifier.Simplify(compile(leaf));

            if (!Simplifier.Reads(body, row))
            {
                continue;
            }

            ordered = Linq.Order(
                ordered,
                Expression.Lambda(body, row),
                descending,
                ordered == rows,
                body.Type == typeof(string) ? Comparison.Ordinal : null
            );
        }

        return ordered;
    }
}
