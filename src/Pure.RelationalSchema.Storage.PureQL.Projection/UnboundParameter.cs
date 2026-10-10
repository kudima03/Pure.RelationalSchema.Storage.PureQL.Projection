namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// PureQLProjection has no way to bind parameter values yet, so evaluating a
// parameter fails the query instead of guessing a value.
internal static class UnboundParameter
{
    public static NotSupportedException Error(string name)
    {
        return new NotSupportedException(
            $"Parameter '{name}' cannot be bound: parameter binding is not supported."
        );
    }
}
