using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Pagination is always the last pipeline stage (see RowsFromDatasets.Build), so
// "pagination after X" really means "paginate a query whose pipeline includes
// X". These tests exercise pagination windows over rows that GROUP BY, DISTINCT
// and JOIN have already reshaped, plus tie-stability under ORDER BY and the
// skip/take boundary cases not already covered by PaginationTests. Pagination
// after DISTINCT (single-column, ordered) and a plain inner-join pagination
// window are already covered by DistinctInteractionTests.DistinctAppliesBefore
// Pagination and JoinWithClausesTests.InnerJoinThenOrderByTotalWithPagination
// ReturnsWindow, so this file adds complementary, non-duplicate scenarios
// instead of repeating them.
[Trait("Clause", "Pagination")]
[Trait("Feature", "Pagination")]
public sealed class PaginationExpansionTests
{
    [Fact]
    public void PaginationAfterGroupByWindowsGroupProjectedRowsNotSourceRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new PaginationAfterGroupByQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] distinctGroups =
        [
            .. orderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal),
        ];

        string[] expected = [.. distinctGroups.Skip(1).Take(1)];
        string?[] actual = [.. result.Column(new OrderStatusColumn().Name.TextValue)];

        // The window addresses the 3 grouped rows, not the 6 source orders.
        Assert.True(distinctGroups.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterDistinctOnMultiColumnTuplesWindowsDeduplicatedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new PaginationAfterDistinctOnMultiColumnTuplesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (double Age, bool Active)[] expected =
        [
            .. userRows
                .OrderBy(user => user.UserAge)
                .ThenBy(user => user.UserActive)
                .Select(user => (user.UserAge, user.UserActive))
                .Distinct()
                .Skip(1)
                .Take(2),
        ];

        (double Age, bool Active)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row.Double(new UserAgeColumn().Name.TextValue)!.Value,
                    row.Bool(new UserActiveColumn().Name.TextValue)!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationAfterJoinWindowsTheFullyJoinedAndFilteredRowSet()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new PaginationAfterJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderItemRows
                .Where(item => item.ItemQty > 1)
                .OrderBy(item => item.ItemQty)
                .Select(item => item.ItemQty)
                .Skip(1)
                .Take(1),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row =>
                row.Double(new ItemQtyColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationWindowIsStableAndDeterministicAcrossRepeatedRunsWithTies()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // Orders 101 and 106 tie on Total (100.50), so a stable sort must keep
        // them in their original relative (insertion) order across runs.
        Assert.Equal(
            orderRows[0].OrderTotal,
            orderRows.Single(order => order.OrderId == Id(101)).OrderTotal
        );

        ProjectionResult firstRun = new ProjectionResult(
            new PureQLProjection(datasets, new PaginationWindowOverTiesQuery().Value)
        );
        ProjectionResult secondRun = new ProjectionResult(
            new PureQLProjection(datasets, new PaginationWindowOverTiesQuery().Value)
        );

        Guid[] expected = [Id(101), Id(106)];

        Guid[] firstRunIds =
        [
            .. firstRun.Rows.Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value),
        ];
        Guid[] secondRunIds =
        [
            .. secondRun.Rows.Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, firstRunIds);
        Assert.Equal(expected, secondRunIds);
        Assert.Equal(firstRunIds, secondRunIds);
    }

    // TakeBeyondEndReturnsAllRemainingRows and SkipBeyondEndReturnsNoRows in
    // PaginationTests.cs already cover skip=0/take-beyond-the-set full
    // passthrough and skip-beyond-the-set empty pages; not duplicated here.

    [Fact]
    public void NegativeSkipIsClampedToZeroInsteadOfThrowingOrWrapping()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // Pagination does not validate skip >= 0 at construction. RowsFromDatasets
        // clamps skip into [0, int.MaxValue] before calling Skip, so a negative
        // skip behaves exactly like skip = 0 rather than throwing or wrapping.
        Query query = new NegativeSkipQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderTotal)
                .Take(3)
                .Select(order => (double?)order.OrderTotal),
        ];

        Assert.Equal(
            expected,
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void NonPositiveTakeIsClampedToZeroYieldingAnEmptyPage()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        // Pagination does not validate take >= 1 at construction. A take of
        // zero or a negative value clamps to 0, so Take(0) yields an empty
        // page rather than throwing or returning every remaining row.
        Query zeroTakeQuery = new ZeroTakeQuery().Value;

        Query negativeTakeQuery = new NegativeTakeQuery().Value;

        ProjectionResult zeroTakeResult = new ProjectionResult(
            new PureQLProjection(datasets, zeroTakeQuery)
        );
        ProjectionResult negativeTakeResult = new ProjectionResult(
            new PureQLProjection(datasets, negativeTakeQuery)
        );

        Assert.Equal(0, zeroTakeResult.Count);
        Assert.Equal(0, negativeTakeResult.Count);
    }

    private static Guid Id(int seed)
    {
        return new Guid(seed, 0, 0, new byte[8]);
    }
}
