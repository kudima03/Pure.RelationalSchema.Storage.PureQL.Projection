using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class DistinctApplicator
{
    internal static IQueryable<IRow> Apply(IQueryable<IRow> source)
    {
        HashSet<string> seen = [];

        List<IRow> result = [];

        foreach (IRow row in source.AsEnumerable())
        {
            string key = BuildKey(row);

            if (seen.Add(key))
            {
                result.Add(row);
            }
        }

        return result.AsQueryable();
    }

    private static string BuildKey(IRow row)
    {
        return string.Join(
            "\0",
            row.Cells
                .OrderBy(c => c.Key.Name.TextValue, StringComparer.Ordinal)
                .Select(c => c.Key.Name.TextValue + "=" + (c.Value.Value.TextValue ?? ""))
        );
    }
}
