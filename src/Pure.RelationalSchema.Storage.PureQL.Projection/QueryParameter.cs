namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A named query parameter, which the tree reads like a captured variable.
internal sealed class QueryParameter<T>(string name)
{
    public string Name { get; } = name;

    public T Value => throw UnboundParameter.Error(Name);
}
