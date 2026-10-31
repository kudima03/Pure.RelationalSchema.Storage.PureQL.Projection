namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A named query parameter, which the tree reads like a captured variable:
// the value it is bound to. A parameter bound to date math also holds the
// date math, and its value is the one at the instant it was bound; At binds
// it again at another instant, as Execution does when each run starts.
internal sealed class QueryParameter<T>(string name, T value) : IRelativeParameter
{
    private readonly DateMath? _dateMath;

    public QueryParameter(string name, DateMath dateMath, DateTime now)
        : this(name, (T)dateMath.At(now))
    {
        _dateMath = dateMath;
    }

    public string Name { get; } = name;

    public T Value { get; } = value;

    public bool Relative => _dateMath is not null;

    public object At(DateTime now)
    {
        return _dateMath is null ? this : new QueryParameter<T>(Name, _dateMath, now);
    }
}

// A query parameter whose value may depend on the instant a query runs.
internal interface IRelativeParameter
{
    public bool Relative { get; }

    // The parameter bound at the instant: itself when it is not relative.
    public object At(DateTime now);
}
