using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// The IQueryable forms of the query operators of this library, called as
// code rather than read as nodes of a tree: over a query of LINQ to Objects
// they compute the operator, over a query of another provider they refuse it
// with NotSupportedException. A provider that reads a call returning a query
// as a value (Pure.RelationalSchema.Storage.PostgreSQL.Querying inlines such
// a call when it reads no lambda parameter) would otherwise be given the same
// node back, without end.
public sealed class QueryOperatorCallTests
{
    [Fact]
    public void OverAQueryOfLinqToObjectsComputesTheWindowWhenItIsRead()
    {
        // Each row plus its number in the order of the rows.
        IQueryable<long> numbered = new long[] { 30, 10, 20 }
            .AsQueryable()
            .Over(
                row => 0,
                partition => partition.OrderBy(row => row),
                row => row,
                PureQLFrame.Partition,
                window => window.Row + window.RowNumber
            );

        Assert.Equal([11L, 22L, 33L], numbered.AsEnumerable().Order());
    }

    [Fact]
    public void OverAQueryOfAnotherProviderIsRefused()
    {
        IQueryable<long> rows = Foreign();

        _ = Assert.Throws<NotSupportedException>(() =>
            rows.Over(
                row => 0,
                partition => partition,
                row => row,
                PureQLFrame.Partition,
                window => window.RowNumber
            )
        );
    }

    [Fact]
    public void RecursiveOverAQueryOfLinqToObjectsComputesEveryLevelWhenItIsRead()
    {
        IQueryable<long> levels = new long[] { 1 }
            .AsQueryable()
            .Recursive(level => level.Where(row => row < 3).Select(row => row + 1), 5);

        Assert.Equal([1L, 2L, 3L], levels.AsEnumerable());
    }

    [Fact]
    public void RecursiveOverAQueryOfLinqToObjectsFailsBeyondMaxDepthWhenItIsRead()
    {
        IQueryable<long> levels = new long[] { 1 }
            .AsQueryable()
            .Recursive(level => level.Select(row => row + 1), 3);

        _ = Assert.Throws<InvalidOperationException>(() => levels.ToList());
    }

    [Fact]
    public void RecursiveOverAQueryOfAnotherProviderIsRefused()
    {
        IQueryable<long> rows = Foreign();

        _ = Assert.Throws<NotSupportedException>(() =>
            rows.Recursive(level => level.Select(row => row + 1), 3)
        );
    }

    private static IQueryable<long> Foreign()
    {
        return new RecordingProvider().CreateQuery<long>(
            Expression.Constant(new long[] { 1 }.AsQueryable())
        );
    }
}
