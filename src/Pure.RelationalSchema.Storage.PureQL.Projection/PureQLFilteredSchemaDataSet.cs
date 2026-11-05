using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A schema dataset whose tables hold only the rows a PureQL condition is
// true for: the origin's tables, each under the conditions given for it. A
// row for which a condition is false or null is not a row of the table, as
// with a query's where. A table given no condition is the origin's table
// itself; one given several holds the rows all of them are true for.
//
// A condition is a row expression over the columns of its table, and is
// given for the table by the table's hash (TableHash). A field of it names a
// column of that table and declares the column's type and whether it may be
// null, as a field of a query does; its source is not read, since every
// field of a table's condition reads that table, whatever a query calls it.
// A condition is closed: it reads no parameter, no group key and no
// subquery column.
//
// A PureQLProjection over this dataset roots every scan of a filtered
// table, in the main query and in subqueries, on the origin's table under
// the Where of its condition, below the joins, the aggregates and the rest
// of the query. The tables' provider is given the whole query still, so a
// provider that translates it filters in the store, and a row the condition
// drops is, for an outer join too, a row the table does not have.
//
// Nothing is checked when the dataset is built. A condition given for a
// table the origin does not have is an ArgumentException when the tables are
// first read, and a condition that cannot be applied to its table (a field
// that names no column or declares another type than its column holds, a
// read of a parameter, a group key or a subquery column) when that table is
// read: when a projection that reads it is built, or the table is
// enumerated. A table is never read unfiltered.
public sealed record PureQLFilteredSchemaDataSet : IStoredSchemaDataSet
{
    private readonly IStoredSchemaDataSet _origin;

    private readonly IReadOnlyDictionary<ITable, IStoredTableDataSet> _tables;

    public PureQLFilteredSchemaDataSet(
        IStoredSchemaDataSet origin,
        IEnumerable<KeyValuePair<ITable, BooleanRow>> conditions
    )
        : this(
            origin,
            new Dictionary<
                KeyValuePair<ITable, IStoredTableDataSet>,
                ITable,
                IStoredTableDataSet
            >(
                new FilteredTables(origin, conditions).Tables(),
                table => table.Key,
                table => table.Value,
                table => new TableHash(table)
            )
        )
    { }

    private PureQLFilteredSchemaDataSet(
        IStoredSchemaDataSet origin,
        IReadOnlyDictionary<ITable, IStoredTableDataSet> tables
    )
    {
        _origin = origin;
        _tables = tables;
    }

    public ISchema Schema => _origin.Schema;

    public IEnumerable<ITable> Keys => _tables.Keys;

    public IEnumerable<IStoredTableDataSet> Values => _tables.Values;

    public int Count => _tables.Count;

    public IStoredTableDataSet this[ITable key] => _tables[key];

    public bool ContainsKey(ITable key)
    {
        return _tables.ContainsKey(key);
    }

    public bool TryGetValue(
        ITable key,
        [MaybeNullWhen(false)] out IStoredTableDataSet value
    )
    {
        return _tables.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<ITable, IStoredTableDataSet>> GetEnumerator()
    {
        return _tables.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
