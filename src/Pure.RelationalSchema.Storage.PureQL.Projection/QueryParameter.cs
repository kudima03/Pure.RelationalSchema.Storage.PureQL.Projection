namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A named query parameter, which the tree reads like a captured variable:
// the value it is bound to, or, when it is not bound, a failure.
internal sealed class QueryParameter<T>
{
    private readonly bool _bound;

    public QueryParameter(string name)
    {
        Name = name;
        Value = default!;
        _bound = false;
    }

    public QueryParameter(string name, T value)
    {
        Name = name;
        Value = value;
        _bound = true;
    }

    public string Name { get; }

    public T Value => _bound ? field : throw ParameterValues.Unbound(Name);
}
