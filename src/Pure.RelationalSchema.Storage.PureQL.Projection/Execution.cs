namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One run of a PureQL document. Subqueries behave as if each ran once before
// the main query: each is run the first time it is read and then reused.
internal sealed class Execution(Catalog catalog, IEnumerable<NamedQuery> subqueries)
{
    private readonly Catalog _catalog = catalog;

    private readonly IReadOnlyDictionary<string, QueryClauses> _subqueries =
        subqueries.ToDictionary(
            subquery => subquery.Name,
            subquery => subquery.Clauses,
            StringComparer.Ordinal
        );

    private readonly Dictionary<string, QueryResult> _results = new(
        StringComparer.Ordinal
    );

    public QueryResult Run(QueryClauses query)
    {
        return new QueryRun(this, query).Result();
    }

    public QueryResult Subquery(string name)
    {
        if (!_results.TryGetValue(name, out QueryResult? result))
        {
            result = Run(_subqueries[name]);
            _results[name] = result;
        }

        return result;
    }

    public IEnumerable<IRecord> Records(Source source)
    {
        return source.IsSubquery
            ? Subquery(source.Target).Records()
            : _catalog
                .Table(source.Target)
                .Value.AsEnumerable()
                .Select(row => (IRecord)new StoredRecord(row));
    }
}
