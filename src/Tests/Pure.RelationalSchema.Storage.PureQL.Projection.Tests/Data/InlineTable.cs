using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using Pure.RelationalSchema.Storage.Samples.TableDataSets;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A one-table schema dataset built from cell texts, for the shapes the
// fixtures of Pure.RelationalSchema.Storage.Samples do not hold: an integer
// column, datetimes with offsets, code points outside the BMP, extreme
// values. Each row lists one cell text per column; empty text is NULL.
internal sealed class InlineTable(
    string schema,
    string table,
    params (string Name, IColumnType Type)[] columns
    )
{
    private readonly string _schema = schema;

    private readonly string _table = table;

    private readonly IReadOnlyList<IColumn> _columns =
        [
            .. columns.Select(column =>
                (IColumn)new Column.Column(new String(column.Name), column.Type)
            ),
        ];

    public IStoredSchemaDataSet With(params string[][] rows)
    {
        ITable table = new Table.Table(new String(_table), _columns, []);

        return new StoredSchemaDataSet(
            new Schema.Schema(new String(_schema), [table], []),
            [
                new StoredTableDataSet(
                    table,
                    [
                        .. rows.Select(texts =>
                            (IRow)
                                new Row(
                                    new Collections.Generic.Dictionary<
                                        int,
                                        IColumn,
                                        ICell
                                    >(
                                        Enumerable.Range(0, _columns.Count),
                                        index => _columns[index],
                                        index => new Cell(new String(texts[index])),
                                        column => new ColumnHash(column)
                                    )
                                )
                        ),
                    ]
                ),
            ]
        );
    }
}
