namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What a window function reads for one row of a plain query: the row, the
// rows of its frame in the window's order, its position in its ordered
// partition and the other rows of the partition by their distance from it.
// Rows equal on every sort key of the window are peers.
public sealed class PureQLWindow<TRow>
{
    private readonly TRow[] _partition;

    private readonly int[] _peerStart;

    private readonly int[] _peerEnd;

    private readonly int[] _dense;

    private readonly int _index;

    private readonly int _first;

    private readonly int _last;

    internal PureQLWindow(
        TRow[] partition,
        int[] peerStart,
        int[] peerEnd,
        int[] dense,
        int index,
        int first,
        int last
    )
    {
        _partition = partition;
        _peerStart = peerStart;
        _peerEnd = peerEnd;
        _dense = dense;
        _index = index;
        _first = first;
        _last = last;
    }

    // The current row.
    public TRow Row => _partition[_index];

    // The rows of the frame, the current row among them, in order.
    public IEnumerable<TRow> Rows =>
        new ArraySegment<TRow>(_partition, _first, _last - _first + 1);

    // The position of the row in its ordered partition, from 1.
    public long RowNumber => _index + 1L;

    // 1 plus the number of rows before the row that are not its peers.
    public long Rank => _peerStart[_index] + 1L;

    // 1 plus the number of groups of peers before the row's own.
    public long DenseRank => _dense[_index] + 1L;

    // (rank - 1) / (rows - 1), 0 in a partition of one row.
    public decimal PercentRank =>
        _partition.Length == 1
            ? 0m
            : Exact.Rounded(_peerStart[_index], _partition.Length - 1);

    // The rows before the row or its peers, itself included, over the rows.
    public decimal CumulativeDistribution =>
        Exact.Rounded(_peerEnd[_index], _partition.Length);

    // The bucket of the row, from 1, when the rows fill `buckets` buckets in
    // order, each of rows / buckets rows and the first rows mod buckets of
    // them of one more.
    public long Ntile(long buckets)
    {
        if (buckets < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buckets),
                buckets,
                "The number of buckets is 1 or more."
            );
        }

        long size = _partition.Length / buckets;
        long larger = _partition.Length % buckets;
        long inLarger = larger * (size + 1);

        return _index < inLarger
            ? (_index / (size + 1)) + 1
            : larger + ((_index - inLarger) / size) + 1;
    }

    // Whether the partition has a row `offset` positions after the current
    // one, before it for a negative offset.
    public bool Has(long offset)
    {
        return offset >= -_index && offset < _partition.Length - _index;
    }

    // That row.
    public TRow At(long offset)
    {
        return _partition[_index + offset];
    }
}
