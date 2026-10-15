using System.Collections;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A query over the projection, run in memory when enumerated.
internal sealed class LocalQuery<T>(Expression expression) : IOrderedQueryable<T>
{
    public Type ElementType => typeof(T);

    public Expression Expression { get; } = expression;

    public IQueryProvider Provider => LocalProvider.Instance;

    public IEnumerator<T> GetEnumerator()
    {
        return LocalProvider.Run<T>(Expression).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
