using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Skip/Take pagination. Paired with ORDER BY for a deterministic window, plus
// boundary cases (take beyond the end, skip beyond the end, full page).
[Trait("Clause", "Pagination")]
[Trait("Feature", "Pagination")]
public sealed class PaginationTests
{
    [Fact]
    public void SkipAndTakeReturnTheRequestedWindowOfAnOrderedResult()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SkipAndTakeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderBy(order => order.OrderTotal)
                .Skip(2)
                .Take(2)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void TakeBeyondEndReturnsAllRemainingRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new TakeBeyondEndQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void SkipBeyondEndReturnsNoRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new SkipBeyondEndQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void FullPageReturnsEveryRowInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FullPageQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }
}
