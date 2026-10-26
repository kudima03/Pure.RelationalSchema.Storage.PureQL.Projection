using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The provider of a projection: its own tree, the projection's Rows, runs
// through its Execution, and a query composed over it runs in memory over
// those rows, the tree standing for them.
internal sealed class ProjectionProvider : IQueryProvider
{
    private readonly Execution _execution;

    public ProjectionProvider(Execution execution)
    {
        _execution = execution;
        Rows = new ProjectionQuery<IRow>(this, execution.Tree);
    }

    public IQueryable<IRow> Rows { get; }

    public IQueryable CreateQuery(Expression expression)
    {
        return (IQueryable)Activator.CreateInstance(
            typeof(ProjectionQuery<>).MakeGenericType(Linq.ElementType(expression.Type)),
            this,
            expression
        )!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new ProjectionQuery<TElement>(this, expression);
    }

    public object? Execute(Expression expression)
    {
        return LocalProvider.Execute(Composed(expression));
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return LocalProvider.Execute<TResult>(Composed(expression));
    }

    public IEnumerator<T> Enumerate<T>(Expression expression)
    {
        return expression == _execution.Tree
            ? (IEnumerator<T>)_execution.Rows()
            : LocalProvider.Run<T>(Composed(expression)).GetEnumerator();
    }

    // The projection's tree, inside a query composed over it, is the
    // projection's rows: a query LocalProvider reads as a sequence.
    private Expression Composed(Expression expression)
    {
        return new Replacer(_execution.Tree, Expression.Constant(Rows, typeof(IQueryable<IRow>)))
            .Visit(expression)!;
    }

    private sealed class Replacer(Expression tree, Expression rows) : ExpressionVisitor
    {
        private readonly Expression _tree = tree;

        private readonly Expression _rows = rows;

        public override Expression? Visit(Expression? node)
        {
            return node == _tree ? _rows : base.Visit(node);
        }
    }
}
