using System.Collections;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A query over a projection: the projection's tree, or one composed over it.
internal sealed class ProjectionQuery<T>(ProjectionProvider provider, Expression expression)
    : IOrderedQueryable<T>
{
    private readonly ProjectionProvider _provider = provider;

    public Type ElementType => typeof(T);

    public Expression Expression { get; } = expression;

    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        return _provider.Enumerate<T>(Expression);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
