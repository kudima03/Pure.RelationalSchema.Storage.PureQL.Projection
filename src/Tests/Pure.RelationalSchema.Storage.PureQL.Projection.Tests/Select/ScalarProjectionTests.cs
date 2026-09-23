using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Scalar select expressions (SELECT 5 AS version FROM t) project a constant
// cell repeated on every output row — standard SQL semantics. Covers all
// seven scalar types, aliasing, mixing with field columns, and interaction
// with WHERE, DISTINCT and pagination.
[Trait("Clause", "Select")]
[Trait("Feature", "ScalarProjection")]
public sealed class ScalarProjectionTests
{
    [Fact]
    public void NumberScalarProjectsConstantOnEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new NumberScalarQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(5, row.Double("version")));
    }

    [Fact]
    public void AllSevenScalarTypesProjectTypedConstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Guid marker = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        DateOnly release = new DateOnly(2024, 12, 31);
        DateTime builtAt = new DateTime(2024, 12, 31, 23, 59, 58);
        TimeOnly cutoff = new TimeOnly(17, 30, 15);

        Query query = new AllSevenScalarTypesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Assert.Equal(true, row.Bool("active"));
                Assert.Equal(release, row.Date("release"));
                Assert.Equal(builtAt, row.DateTime("built_at"));
                Assert.Equal(42.5, row.Double("amount"));
                Assert.Equal("v2", row["label"]);
                Assert.Equal(cutoff, row.Time("cutoff"));
                Assert.Equal(marker, row.Uuid("marker"));
            }
        );
    }

    [Fact]
    public void ScalarAlongsideFieldColumnRepeatsPerRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new ScalarAlongsideFieldColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);

        for (int i = 0; i < userRows.Count; i++)
        {
            Assert.Equal("v2", result.Row(i)["release"]);
            Assert.Equal(
                userRows[i].UserName,
                result.Row(i)[new UserNameColumn().Name.TextValue]
            );
        }
    }

    [Fact]
    public void ScalarWithoutAliasProjectsEmptyNamedColumn()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new ScalarWithoutAliasQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.Contains(string.Empty, result.ColumnNames);
        Assert.All(result.Rows, row => Assert.Equal(7, row.Double(string.Empty)));
    }

    [Fact]
    public void ScalarUnderWhereRepeatsOnlyOnFilteredRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new ScalarUnderWhereQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count(user => user.UserActive), result.Count);
        Assert.All(result.Rows, row => Assert.Equal("active-user", row["tag"]));
    }

    [Fact]
    public void DistinctCollapsesIdenticalScalarOnlyRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new DistinctScalarOnlyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal("tag", result.Row(0)["tag"]);
    }

    [Fact]
    public void ScalarWithPaginationProjectsConstantOnPagedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ScalarWithPaginationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(2, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(9, row.Double("page_marker")));
    }

    [Fact]
    public void NegativeFractionalNumberScalarRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        Query query = new NegativeFractionalNumberScalarQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(productRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(-12.75, row.Double("adjustment")));
    }

    [Fact]
    public void ScalarProjectsConstantOnEveryJoinedRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarOverJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal("joined", row["source"]));
    }

    [Fact]
    public void FalseBooleanScalarRoundTripsDistinctFromEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new FalseBooleanScalarQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(false, row.Bool("flag")));
    }
}
