namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A row of the from/joins product: one record per source of the query, in
// declaration order. A source on the unmatched side of an outer join has no
// record, so every one of its fields reads as null.
internal sealed class JoinedRow
{
    private readonly IRecord?[] _records;

    private JoinedRow(IRecord?[] records)
    {
        _records = records;
    }

    public static JoinedRow Of(int sourceCount, int source, IRecord record)
    {
        IRecord?[] records = new IRecord?[sourceCount];
        records[source] = record;
        return new JoinedRow(records);
    }

    public IRecord? this[int source] => _records[source];

    public JoinedRow With(int source, IRecord? record)
    {
        IRecord?[] records = (IRecord?[])_records.Clone();
        records[source] = record;
        return new JoinedRow(records);
    }
}
