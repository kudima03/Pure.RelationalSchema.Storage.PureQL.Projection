using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A multi-schema dataset where every table carries a same-named primary-key
// column ("id") — the shape of any real schema that uses a conventional PK
// name. This deliberately violates SampleDatabase's globally-unique-name
// invariant to reproduce the joined-column resolution bugs of issues #77 and
// #78: planner.needs joins to refs.specialties (needs.spec_id -> specialties.id)
// and to billing.estimates (needs.id -> estimates.need_id, 1:1).
internal sealed class CollidingNameDatabase
{
    public const string PlannerSchemaName = "planner";
    public const string RefsSchemaName = "refs";
    public const string BillingSchemaName = "billing";

    public static class Needs
    {
        public const string TableName = "needs";
        public const string Entity = "planner.needs";
        public const string Id = "id";
        public const string SpecialtyId = "spec_id";
        public const string PlannedHours = "planned_hours";
    }

    public static class Specialties
    {
        public const string TableName = "specialties";
        public const string Entity = "refs.specialties";
        public const string Id = "id";
        public const string Title = "title";
    }

    public static class Estimates
    {
        public const string TableName = "estimates";
        public const string Entity = "billing.estimates";
        public const string Id = "id";
        public const string NeedId = "need_id";
        public const string Status = "estimate_status";
        public const string ActualHours = "actual_hours";
    }

    private static readonly IReadOnlyList<IColumn> NeedColumns =
    [
        new Column.Column(new String(Needs.Id), new UuidColumnType()),
        new Column.Column(new String(Needs.SpecialtyId), new UuidColumnType()),
        new Column.Column(new String(Needs.PlannedHours), new DoubleColumnType()),
    ];

    private static readonly IReadOnlyList<IColumn> SpecialtyColumns =
    [
        new Column.Column(new String(Specialties.Id), new UuidColumnType()),
        new Column.Column(new String(Specialties.Title), new StringColumnType()),
    ];

    private static readonly IReadOnlyList<IColumn> EstimateColumns =
    [
        new Column.Column(new String(Estimates.Id), new UuidColumnType()),
        new Column.Column(new String(Estimates.NeedId), new UuidColumnType()),
        new Column.Column(new String(Estimates.Status), new StringColumnType()),
        new Column.Column(new String(Estimates.ActualHours), new DoubleColumnType()),
    ];

    private readonly IReadOnlyList<IStoredSchemaDataSet> _datasets;

    public CollidingNameDatabase()
    {
        ITable needsTable = new Table.Table(
            new String(Needs.TableName),
            NeedColumns,
            []
        );
        ITable specialtiesTable = new Table.Table(
            new String(Specialties.TableName),
            SpecialtyColumns,
            []
        );
        ITable estimatesTable = new Table.Table(
            new String(Estimates.TableName),
            EstimateColumns,
            []
        );

        _datasets =
        [
            SchemaDataset(
                PlannerSchemaName,
                new SampleTableDataset(needsTable, BuildNeedRows())
            ),
            SchemaDataset(
                RefsSchemaName,
                new SampleTableDataset(specialtiesTable, BuildSpecialtyRows())
            ),
            SchemaDataset(
                BillingSchemaName,
                new SampleTableDataset(estimatesTable, BuildEstimateRows())
            ),
        ];
    }

    public IEnumerable<IStoredSchemaDataSet> Datasets => _datasets;

    // Three referenced specialties plus one ("Rigger") no need points at.
    public IReadOnlyList<SpecialtyRow> SpecialtyRows { get; } = [
        new(Id(501), "Welder"),
        new(Id(502), "Fitter"),
        new(Id(503), "Painter"),
        new(Id(504), "Rigger"),
    ];

    // Six needs across three specialties; the need's own PK never equals any
    // specialty PK, so a query that confuses the two produces detectably
    // different values.
    public IReadOnlyList<NeedRow> NeedRows { get; } = [
        new(Id(1), Id(501), 10),
        new(Id(2), Id(501), 20),
        new(Id(3), Id(502), 30),
        new(Id(4), Id(502), 40),
        new(Id(5), Id(502), 50),
        new(Id(6), Id(503), 60),
    ];

    // One estimate per need (1:1), the shape of issue #78's fact table.
    public IReadOnlyList<EstimateRow> EstimateRows { get; } = [
        new(Id(601), Id(1), "draft", 12),
        new(Id(602), Id(2), "final", 18),
        new(Id(603), Id(3), "draft", 33),
        new(Id(604), Id(4), "final", 41),
        new(Id(605), Id(5), "draft", 47),
        new(Id(606), Id(6), "final", 66),
    ];

    private static Guid Id(int seed)
    {
        return new Guid(seed, 0, 0, new byte[8]);
    }

    private static IStoredSchemaDataSet SchemaDataset(
        string schemaName,
        SampleTableDataset tableDataset
    )
    {
        ISchema schema = new Schema.Schema(
            new String(schemaName),
            [tableDataset.TableSchema],
            []
        );

        IReadOnlyDictionary<ITable, IStoredTableDataSet> datasetsByTable =
            new Collections.Generic.Dictionary<
                IStoredTableDataSet,
                ITable,
                IStoredTableDataSet
            >(
                [tableDataset],
                dataset => dataset.TableSchema,
                dataset => dataset,
                table => new TableHash(table)
            );

        return new StoredSchemaDataset(schema, datasetsByTable);
    }

    private IReadOnlyList<IRow> BuildNeedRows()
    {
        return
        [
            .. NeedRows.Select(need =>
                BuildRow(
                    NeedColumns,
                    new Dictionary<string, string>
                    {
                        [Needs.Id] = CellText.From(need.NeedId),
                        [Needs.SpecialtyId] = CellText.From(need.NeedSpecialtyId),
                        [Needs.PlannedHours] = CellText.From(need.NeedPlannedHours),
                    }
                )
            ),
        ];
    }

    private IReadOnlyList<IRow> BuildSpecialtyRows()
    {
        return
        [
            .. SpecialtyRows.Select(specialty =>
                BuildRow(
                    SpecialtyColumns,
                    new Dictionary<string, string>
                    {
                        [Specialties.Id] = CellText.From(specialty.SpecialtyId),
                        [Specialties.Title] = CellText.From(
                            specialty.SpecialtyTitle
                        ),
                    }
                )
            ),
        ];
    }

    private IReadOnlyList<IRow> BuildEstimateRows()
    {
        return
        [
            .. EstimateRows.Select(estimate =>
                BuildRow(
                    EstimateColumns,
                    new Dictionary<string, string>
                    {
                        [Estimates.Id] = CellText.From(estimate.EstimateId),
                        [Estimates.NeedId] = CellText.From(
                            estimate.EstimateNeedId
                        ),
                        [Estimates.Status] = CellText.From(
                            estimate.EstimateStatus
                        ),
                        [Estimates.ActualHours] = CellText.From(
                            estimate.EstimateActualHours
                        ),
                    }
                )
            ),
        ];
    }

    private static IRow BuildRow(
        IEnumerable<IColumn> columns,
        IReadOnlyDictionary<string, string> textByName
    )
    {
        return new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                columns,
                column => column,
                column => new Cell(new String(textByName[column.Name.TextValue])),
                column => new ColumnHash(column)
            )
        );
    }
}
