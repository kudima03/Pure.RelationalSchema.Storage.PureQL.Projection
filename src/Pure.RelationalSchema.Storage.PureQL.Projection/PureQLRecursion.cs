using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The operator a tree holds for a recursive subquery: the rows of the seed,
// level 0, then of every run of next over the rows of the level before, up
// to the first level that is empty; every row of every level, duplicates
// included. A row at a level beyond maxDepth fails the query with an
// InvalidOperationException before any row is returned, so no query runs
// forever or returns a result that was cut off. Like a Queryable
// operator it comes in two forms: over an IQueryable with a quoted lambda,
// which is what the tree holds and a provider translates (SQL's WITH
// RECURSIVE, or one statement per level when next groups), and over a plain
// sequence, which computes it (and a third, which LINQ to Objects runs the
// tree's form with).
// Called as code, the IQueryable form computes the operator over a query
// of LINQ to Objects, builds a query of a projection's own provider, and
// refuses a query of any other provider with NotSupportedException
// (Linq.Composable).
public static class PureQLRecursion
{
    private static readonly MethodInfo QueryableRecursive = typeof(PureQLRecursion)
        .GetMethods()
        .Single(method =>
            method.Name == nameof(Recursive)
            && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
                == typeof(IQueryable<>)
        );

    public static IQueryable<T> Recursive<T>(
        this IQueryable<T> seed,
        Expression<Func<IQueryable<T>, IQueryable<T>>> next,
        long maxDepth
    )
    {
        if (seed.Provider is EnumerableQuery)
        {
            Func<IQueryable<T>, IQueryable<T>> step = next.Compile();

            return Linq.Deferred(() =>
                    Levels(seed, level => step(level.AsQueryable()), maxDepth)
                )
                .AsQueryable();
        }

        return Linq.Composable(
                seed.Provider,
                $"{nameof(PureQLRecursion)}.{nameof(Recursive)}"
            )
            .CreateQuery<T>(
            Expression.Call(
                QueryableRecursive.MakeGenericMethod(typeof(T)),
                seed.Expression,
                Expression.Quote(next),
                Expression.Constant(maxDepth)
            )
        );
    }

    public static IEnumerable<T> Recursive<T>(
        this IEnumerable<T> seed,
        Func<IEnumerable<T>, IEnumerable<T>> next,
        long maxDepth
    )
    {
        return Levels(seed, next, maxDepth);
    }

    // The form LINQ to Objects runs the tree's form with, when the tree is
    // run as it is over an EnumerableQuery (AsQueryable): there next keeps
    // its query parameter, and is given each level as a query over it.
    public static IEnumerable<T> Recursive<T>(
        this IEnumerable<T> seed,
        Func<IQueryable<T>, IEnumerable<T>> next,
        long maxDepth
    )
    {
        return Levels(seed, level => next(level.AsQueryable()), maxDepth);
    }

    private static List<T> Levels<T>(
        IEnumerable<T> seed,
        Func<IEnumerable<T>, IEnumerable<T>> next,
        long maxDepth
    )
    {
        if (maxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDepth),
                maxDepth,
                "The depth a recursive subquery may reach is 1 or more."
            );
        }

        List<T> rows = [];
        List<T> level = [.. seed];

        for (long depth = 0; level.Count > 0; depth++)
        {
            if (depth > maxDepth)
            {
                throw new InvalidOperationException(
                    $"A recursive subquery has a row deeper than its maxDepth, {maxDepth}."
                );
            }

            rows.AddRange(level);
            level = [.. next(level)];
        }

        return rows;
    }
}
