using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Samples.Queries.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Pagination carries skip/take as Int64, but they are applied through an
// unchecked cast to Int32, so values beyond int.MaxValue wrap and silently
// produce the wrong window (issue #85).
[Trait("Clause", "Pagination")]
[Trait("Feature", "Range")]
public sealed class PaginationRangeTests
{
    [Fact]
    public void TakeBeyondIntMaxReturnsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new TakeBeyondIntMaxQuery().Value
            )
        );

        Assert.Equal(userRows.Count, result.Count);
    }

    [Fact]
    public void SkipBeyondIntMaxReturnsNoRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new SkipBeyondIntMaxQuery().Value
            )
        );

        Assert.Equal(0, result.Count);
    }
}
