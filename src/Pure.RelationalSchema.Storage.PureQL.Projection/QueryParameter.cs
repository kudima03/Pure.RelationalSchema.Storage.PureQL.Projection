namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A named query parameter, which the tree reads like a captured variable:
// the value it is bound to.
internal sealed class QueryParameter<T>(string name, T value)
{
    public string Name { get; } = name;

    public T Value { get; } = value;
}
