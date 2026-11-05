using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The tables of a schema dataset, each holding only the rows the conditions
// given for it are true for (FilteredTable); a table given no condition is
// the origin's table itself, and one given several holds the rows all of
// them are true for. A condition belongs to the table with its table's hash
// (TableHash), so the table a condition is given for need not be the
// instance the dataset holds. A condition given for a table the dataset does
// not have is an ArgumentException when the tables are read: it would
// otherwise filter nothing, and the table it was meant for would be read
// whole. Nothing is read or hashed before the tables are.
internal sealed class FilteredTables(
    IStoredSchemaDataSet origin,
    IEnumerable<KeyValuePair<ITable, BooleanRow>> conditions
)
{
    private readonly IStoredSchemaDataSet _origin = origin;

    private readonly ILookup<ITable, BooleanRow> _conditions = new Lookup<
        KeyValuePair<ITable, BooleanRow>,
        ITable,
        BooleanRow
    >(
        conditions,
        condition => condition.Key,
        condition => condition.Value,
        table => new TableHash(table)
    );

    private readonly ILookup<ITable, ITable> _stored = new Lookup<
        KeyValuePair<ITable, IStoredTableDataSet>,
        ITable,
        ITable
    >(
        origin,
        table => table.Key,
        table => table.Key,
        table => new TableHash(table)
    );

    public IEnumerable<KeyValuePair<ITable, IStoredTableDataSet>> Tables()
    {
        ITable? unknown = _conditions
            .Select(given => given.Key)
            .FirstOrDefault(table => !_stored.Contains(table));

        if (unknown is not null)
        {
            throw new ArgumentException(
                $"A condition is given for table '{unknown.Name.TextValue}', which "
                    + $"schema '{_origin.Schema.Name.TextValue}' does not have."
            );
        }

        foreach (KeyValuePair<ITable, IStoredTableDataSet> table in _origin)
        {
            yield return KeyValuePair.Create(
                table.Key,
                _conditions[table.Key]
                    .Aggregate(
                        table.Value,
                        (rows, condition) => new FilteredTable(rows, condition)
                    )
            );
        }
    }
}
