using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Pipeline-order pin: DISTINCT deduplicates the projected rows before
// pagination slices them, and ordering applied upstream survives the
// deduplication (first-seen order of ordered rows is sorted order), so the
// window addresses the sorted distinct values, not the raw fan-out.
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctPaginationOrderTests
{
    [Fact]
    public void PaginationWindowsTheSortedDistinctValuesAfterJoinFanOut()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DistinctOrderByPaginationOverJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal)
                .Skip(1)
                .Take(1),
        ];

        Assert.Equal(
            expected,
            result.Column(new OrderStatusColumn().Name.TextValue).ToArray()
        );
    }
}
