using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A minimal one-table dataset built by hand (not through SampleDatabase,
// which always formats stored UUID text lowercase via Guid.ToString()) so
// the stored cell text of two logically-identical UUIDs differs only in
// hex casing (issue #103: "UUID casing round-trips to the same logical
// value in equality/comparison").
internal sealed class UuidCasingDatabase
{
    public const string SchemaName = "casing";

    public static class Things
    {
        public const string TableName = "things";
        public const string Entity = "casing.things";
        public const string Id = "thing_id";
        public const string Label = "thing_label";
    }

    // The same logical Guid stored once with lowercase hex text and once
    // with uppercase hex text.
    public static readonly Guid SharedId = new Guid(
        "0f9e8d7c-6b5a-4938-8271-605f4e3d2c1b"
    );

    private static readonly IReadOnlyList<IColumn> ThingColumns =
    [
        new Column.Column(new String(Things.Id), new UuidColumnType()),
        new Column.Column(new String(Things.Label), new StringColumnType()),
    ];

    private readonly IReadOnlyList<IStoredSchemaDataSet> _datasets;

    public UuidCasingDatabase()
    {
        ITable thingsTable = new Table.Table(
            new String(Things.TableName),
            ThingColumns,
            []
        );

        ISchema schema = new Schema.Schema(
            new String(SchemaName),
            [thingsTable],
            []
        );

        IReadOnlyDictionary<ITable, IStoredTableDataSet> datasetsByTable =
            new Collections.Generic.Dictionary<
                IStoredTableDataSet,
                ITable,
                IStoredTableDataSet
            >(
                [new SampleTableDataset(thingsTable, BuildThingRows())],
                dataset => dataset.TableSchema,
                dataset => dataset,
                table => new TableHash(table)
            );

        _datasets = [new StoredSchemaDataset(schema, datasetsByTable)];
    }

    public IEnumerable<IStoredSchemaDataSet> Datasets => _datasets;

    private static IReadOnlyList<IRow> BuildThingRows()
    {
        return
        [
            BuildRow(SharedId.ToString().ToLowerInvariant(), "lowercase"),
            BuildRow(SharedId.ToString().ToUpperInvariant(), "uppercase"),
        ];
    }

    private static IRow BuildRow(string idText, string label)
    {
        return new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                ThingColumns,
                column => column,
                column => new Cell(
                    new String(
                        column.Name.TextValue == Things.Id ? idText : label
                    )
                ),
                column => new ColumnHash(column)
            )
        );
    }
}
