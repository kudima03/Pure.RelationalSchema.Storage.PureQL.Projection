using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The stored tables a query can read, addressed as "schema.table". Datasets
// may repeat a schema name (a host concatenating several stores), so every
// dataset is searched.
internal sealed class Catalog(IEnumerable<IStoredSchemaDataSet> datasets)
{
    private readonly IReadOnlyList<IStoredSchemaDataSet> _datasets = [.. datasets];

    public KeyValuePair<ITable, IStoredTableDataSet> Table(string entity)
    {
        foreach (IStoredSchemaDataSet dataset in _datasets)
        {
            foreach (KeyValuePair<ITable, IStoredTableDataSet> table in dataset)
            {
                string name =
                    $"{dataset.Schema.Name.TextValue}.{table.Key.Name.TextValue}";

                if (name == entity)
                {
                    return table;
                }
            }
        }

        throw new ArgumentException($"Entity '{entity}' does not exist.");
    }
}
