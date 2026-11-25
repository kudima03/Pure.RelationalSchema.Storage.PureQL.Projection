using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The window operator a tree holds for a window function of a plain query:
// for every row, the result of a function of its window. The rows are
// partitioned by a key, each partition is sorted, the rows equal on the peer
// key (the tuple of the window's sort keys) are peers, and the frame says
// which rows of the ordered partition the window of a row holds. A window
// function is written over the window: an aggregate over window.Rows,
// window.RowNumber, window.Rank, ..., or a row at an offset (window.At).
// Like a Queryable operator it comes in two forms: over an IQueryable with
// quoted lambdas, which is what the tree holds and a provider translates
// (SQL's OVER clause), and over a plain sequence, which computes it.
// Called as code, the IQueryable form computes the operator over a query
// of LINQ to Objects, builds a query of a projection's own provider, and
// refuses a query of any other provider with NotSupportedException
// (Linq.Composable).
public static class PureQLWindows
{
    private static readonly MethodInfo QueryableOver = typeof(PureQLWindows)
        .GetMethods()
        .Single(method =>
            method.Name == nameof(Over)
            && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
                == typeof(IQueryable<>)
        );

    public static IQueryable<TResult> Over<TRow, TPartition, TPeer, TResult>(
        this IQueryable<TRow> rows,
        Expression<Func<TRow, TPartition>> partitionBy,
        Expression<Func<IEnumerable<TRow>, IEnumerable<TRow>>> orderBy,
        Expression<Func<TRow, TPeer>> peers,
        PureQLFrame frame,
        Expression<Func<PureQLWindow<TRow>, TResult>> result
    )
        where TPartition : notnull
    {
        return rows.Provider is EnumerableQuery
            ? Over(
                    rows.AsEnumerable(),
                    partitionBy.Compile(),
                    orderBy.Compile(),
                    peers.Compile(),
                    frame,
                    result.Compile()
                )
                .AsQueryable()
            : Linq.Composable(rows.Provider, $"{nameof(PureQLWindows)}.{nameof(Over)}")
            .CreateQuery<TResult>(
            Expression.Call(
                QueryableOver.MakeGenericMethod(
                    typeof(TRow),
                    typeof(TPartition),
                    typeof(TPeer),
                    typeof(TResult)
                ),
                rows.Expression,
                Expression.Quote(partitionBy),
                Expression.Quote(orderBy),
                Expression.Quote(peers),
                Expression.Constant(frame),
                Expression.Quote(result)
            )
        );
    }

    public static IEnumerable<TResult> Over<TRow, TPartition, TPeer, TResult>(
        this IEnumerable<TRow> rows,
        Func<TRow, TPartition> partitionBy,
        Func<IEnumerable<TRow>, IEnumerable<TRow>> orderBy,
        Func<TRow, TPeer> peers,
        PureQLFrame frame,
        Func<PureQLWindow<TRow>, TResult> result
    )
        where TPartition : notnull
    {
        Dictionary<TPartition, List<TRow>> partitions = [];
        List<List<TRow>> ordered = [];

        foreach (TRow row in rows)
        {
            TPartition key = partitionBy(row);

            if (!partitions.TryGetValue(key, out List<TRow>? partition))
            {
                partition = [];
                partitions[key] = partition;
                ordered.Add(partition);
            }

            partition.Add(row);
        }

        foreach (List<TRow> partition in ordered)
        {
            TRow[] sorted = [.. orderBy(partition)];
            TPeer[] keys = [.. sorted.Select(peers)];
            int[] peerStart = new int[sorted.Length];
            int[] peerEnd = new int[sorted.Length];
            int[] dense = new int[sorted.Length];
            EqualityComparer<TPeer> equal = EqualityComparer<TPeer>.Default;

            for (int i = 0, start = 0, groups = 0; i < sorted.Length; i++)
            {
                if (i > 0 && !equal.Equals(keys[i], keys[i - 1]))
                {
                    start = i;
                    groups++;
                }

                peerStart[i] = start;
                dense[i] = groups;
            }

            for (int i = sorted.Length - 1, end = sorted.Length; i >= 0; i--)
            {
                peerEnd[i] = end;

                if (peerStart[i] == i)
                {
                    end = i;
                }
            }

            Dictionary<object, (BigInteger[] Sums, int[] Counts)> prefixes = [];
            RangeBounds<TPeer>? range = frame.IsRange
                ? new RangeBounds<TPeer>(keys, frame)
                : null;

            for (int i = 0; i < sorted.Length; i++)
            {
                (int first, int last) =
                    range is not null ? range.Of(i, peerStart[i], peerEnd[i] - 1)
                    : frame.IsRows
                        ? (
                            Bound(i, frame.Preceding, -1, 0),
                            Bound(i, frame.Following, 1, sorted.Length - 1)
                        )
                    : (0, sorted.Length - 1);

                yield return result(
                    new PureQLWindow<TRow>(
                        sorted,
                        prefixes,
                        peerStart,
                        peerEnd,
                        dense,
                        i,
                        first,
                        last
                    )
                );
            }
        }
    }

    // The row a count of rows away from a row, or the end of the partition
    // on that side when the count reaches it or is "all".
    private static int Bound(int index, decimal? count, int direction, int end)
    {
        return count is decimal rows && rows < Math.Abs(end - index)
            ? index + (direction * (int)rows)
            : end;
    }

    // The rows of an ordered partition within a distance of a row's sort
    // key. The key is the one item of the peer key; rows whose key is null
    // are peers, at one end of the partition, and are only each other's
    // window. A bound that falls outside the type reaches the end of the
    // partition on its side: it is only compared, never returned.
    private sealed class RangeBounds<TPeer>
    {
        private readonly IComparable?[] _keys;

        private readonly PureQLFrame _frame;

        private readonly int _firstValue;

        private readonly int _lastValue;

        public RangeBounds(TPeer[] keys, PureQLFrame frame)
        {
            _keys = [.. keys.Select(key => (IComparable?)((ITuple)key!)[0])];
            _frame = frame;
            _firstValue = Array.FindIndex(_keys, key => key is not null);
            _lastValue = Array.FindLastIndex(_keys, key => key is not null);
        }

        public (int First, int Last) Of(int index, int firstPeer, int lastPeer)
        {
            if (_keys[index] is not IComparable key)
            {
                return (firstPeer, lastPeer);
            }

            // The side of the frame that reaches towards the start of the
            // ordered partition is preceding; it reaches smaller keys when
            // the key ascends and greater ones when it descends.
            int sign = _frame.Descending ? -1 : 1;
            IComparable? low = Moved(
                key,
                _frame.Preceding,
                _frame.PrecedingUnit,
                -sign
            );
            IComparable? high = Moved(
                key,
                _frame.Following,
                _frame.FollowingUnit,
                sign
            );

            return (
                low is null ? _firstValue : First(low, sign, firstPeer),
                high is null ? _lastValue : Last(high, sign, lastPeer)
            );
        }

        // The first row whose key is not before the bound, in the order of
        // the partition; it is never after the row's first peer.
        private int First(IComparable bound, int sign, int firstPeer)
        {
            int low = _firstValue;
            int high = firstPeer;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (sign * _keys[middle]!.CompareTo(bound) < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        // The last row whose key is not after the bound; it is never before
        // the row's last peer.
        private int Last(IComparable bound, int sign, int lastPeer)
        {
            int low = lastPeer;
            int high = _lastValue;

            while (low < high)
            {
                int middle = low + ((high - low + 1) / 2);

                if (sign * _keys[middle]!.CompareTo(bound) > 0)
                {
                    high = middle - 1;
                }
                else
                {
                    low = middle;
                }
            }

            return low;
        }

        // The key moved by a distance towards greater (direction 1) or
        // smaller values; null when the distance is "all" or the bound is
        // outside the type.
        private static IComparable? Moved(
            IComparable key,
            decimal? distance,
            PureQLRangeUnit unit,
            int direction
        )
        {
            if (distance is not decimal amount)
            {
                return null;
            }

            try
            {
                return key switch
                {
                    long number => checked(number + (direction * (long)amount)),
                    decimal number => Exact.Add(number, direction * amount),
                    DateOnly date => unit == PureQLRangeUnit.Months
                        ? date.AddMonths(checked(direction * (int)amount))
                        : date.AddDays(checked(direction * (int)amount)),
                    DateTime instant => unit == PureQLRangeUnit.Months
                        ? instant.AddMonths(checked(direction * (int)amount))
                        : instant.AddTicks(
                            checked(
                                direction
                                    * (long)
                                        Math.Round(
                                            Exact.Multiply(amount, 1_000_000m),
                                            MidpointRounding.AwayFromZero
                                        )
                                    * TimeSpan.TicksPerMicrosecond
                            )
                        ),
                    _ => throw new NotSupportedException(
                        $"A range window has no distance for a {key.GetType().Name} key."
                    ),
                };
            }
            catch (Exception exception)
                when (exception is OverflowException or ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
