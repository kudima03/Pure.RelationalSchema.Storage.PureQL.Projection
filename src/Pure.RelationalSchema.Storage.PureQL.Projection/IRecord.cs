namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One row of one source: a stored table row or a subquery result row, read
// field by field as runtime values.
internal interface IRecord
{
    public object? Value(string field);
}
