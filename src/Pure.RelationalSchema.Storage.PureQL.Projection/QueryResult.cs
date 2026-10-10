namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The rows a query returns, as runtime values in select column order.
internal sealed record QueryResult(
    IReadOnlyList<TypedExpression> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows
)
{
    public IEnumerable<IRecord> Records()
    {
        return Rows.Select(row => (IRecord)new ValueRecord(
            Columns
                .Select((column, index) => KeyValuePair.Create(column.Alias!, row[index]))
                .ToDictionary(StringComparer.Ordinal)
        ));
    }

    public IEnumerable<object?> Column(string alias)
    {
        int index = Columns.Select(column => column.Alias).ToList().IndexOf(alias);
        return Rows.Select(row => row[index]);
    }
}
