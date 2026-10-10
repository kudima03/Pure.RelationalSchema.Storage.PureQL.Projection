namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A subquery result row: its values keyed by the subquery's column aliases.
internal sealed class ValueRecord(IReadOnlyDictionary<string, object?> values) : IRecord
{
    private readonly IReadOnlyDictionary<string, object?> _values = values;

    public object? Value(string field)
    {
        return _values[field];
    }
}
