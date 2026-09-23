using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Combined;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Capstone of the #72 roadmap: multi-clause, multi-table queries exercising
// JOIN -> WHERE -> ORDER BY -> GROUP BY -> HAVING -> SELECT -> DISTINCT ->
// pagination together, at the "5-level nesting" depth established per-clause
// by #97 (Where/Scalar/NestedBooleanTests) and #98 (Where/Each/NestedEachTests).
// The concrete tree shapes here are reused verbatim from those two files
// rather than invented from scratch, per issue #105.
[Trait("Clause", "Combined")]
[Trait("Feature", "DeepNestingEndToEnd")]
public sealed class DeepNestingEndToEndTests
{
    // JOIN (3 tables) -> WHERE (5-level nested scalar and/or/not tree) ->
    // ORDER BY (multi-key) -> pagination. The scalar tree is a constant
    // all-or-nothing filter, so every
    // joined row survives WHERE unfiltered; the join itself is still
    // restrictive (INNER JOIN order_items drops orders with no items:
    // 102, 104, 106), leaving four (order, item) rows to sort and page.
    [Fact]
    public void MultiJoinFiveLevelScalarTreeOrderByMultiKeyThenPaginateReturnsWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query =
            new MultiJoinFiveLevelScalarTreeOrderByMultiKeyThenPaginateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (Guid ItemId, double Qty, double Total)[] expected =
        [
            .. (
                from order in orderRows
                join user in userRows on order.OrderUserId equals user.UserId
                join item in orderItemRows on order.OrderId equals item.ItemOrderId
                select (order, item)
            )
                .OrderBy(row => row.order.OrderTotal)
                .ThenByDescending(row => row.item.ItemQty)
                .Skip(1)
                .Take(2)
                .Select(row => (row.item.ItemId, row.item.ItemQty, row.order.OrderTotal)),
        ];

        (Guid ItemId, double Qty, double Total)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row.Uuid(new ItemIdColumn().Name.TextValue)!.Value,
                    row.Double(new ItemQtyColumn().Name.TextValue)!.Value,
                    row.Double(new OrderTotalColumn().Name.TextValue)!.Value
                )
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // Everything at once: multi-join -> WHERE (5-level nested each* tree,
    // mixing eachAnd/eachOr/eachNot over eachEquality/eachComparison leaves)
    // -> GROUP BY -> HAVING (3-level nested aggregate-comparison tree) ->
    // SELECT (key + two aggregates) -> ORDER BY -> DISTINCT -> pagination.
    [Fact]
    public void AllClausesComposeWithFiveLevelEachTreeAndThreeLevelHaving()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];

        Query query = new AllClausesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        bool EachTreePredicate(OrderRecord order)
        {
            bool a = order.OrderTotal > 100;
            bool b = order.OrderStatus == "pending";
            bool c = order.OrderTotal >= 300;
            bool d = order.OrderStatus == "cancelled";
            bool e = order.OrderTotal < 100;
            bool f = order.OrderStatus == "shipped";
            bool left = !(a && b) || c;
            bool right = !d || (e && f);
            return left && right;
        }

        (Guid OrderId, double QtySum, double ItemCount)[] expected =
        [
            .. (
                from order in orderRows
                join user in userRows on order.OrderUserId equals user.UserId
                join item in orderItemRows on order.OrderId equals item.ItemOrderId
                where EachTreePredicate(order)
                select (order, item)
            )
                .GroupBy(row => row.order.OrderId)
                .Select(group => (
                    OrderId: group.Key,
                    QtySum: group.Sum(row => row.item.ItemQty),
                    ItemCount: (double)group.Count()
                ))
                .Where(group => (group.ItemCount >= 2 || group.QtySum >= 5)
                    && !(group.ItemCount >= 100))
                .OrderBy(group => group.OrderId)
                .Skip(0)
                .Take(1),
        ];

        (Guid OrderId, double QtySum, double ItemCount)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row.Uuid(new OrderIdColumn().Name.TextValue)!.Value,
                    row.Double("qtySum")!.Value,
                    row.Double("itemCount")!.Value
                )
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    // Same full pipeline as the "everything at once" test above, but with a
    // differently-shaped, logically-equivalent 5-level WHERE tree (the De
    // Morgan transform of the first): the two queries must agree on every
    // row, catching translator bugs that are specific to how the nesting is
    // shaped rather than what it evaluates to.
    [Fact]
    public void DeMorganEquivalentFiveLevelEachTreeProducesIdenticalPipelineResult()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query queryA = new FiveLevelEachTreeWithHavingQuery().Value;
        Query queryB = new DeMorganEquivalentFiveLevelEachTreeQuery().Value;

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(datasets, queryB)
        );

        static (Guid, double, double)[] Rows(ProjectionResult result)
        {
            return
            [
                .. result.Rows.Select(row =>
                    (
                        row.Uuid(new OrderIdColumn().Name.TextValue)!.Value,
                        row.Double("qtySum")!.Value,
                        row.Double("itemCount")!.Value
                    )
                ),
            ];
        }

        (Guid OrderId, double QtySum, double ItemCount)[] expected =
        [
            (
                new Guid(101, 0, 0, new byte[8]),
                3.0,
                2.0
            ),
            (
                new Guid(103, 0, 0, new byte[8]),
                5.0,
                1.0
            ),
        ];

        Assert.Equal(expected, Rows(resultA));
        Assert.Equal(expected, Rows(resultB));
    }

    // Boundary: the 5-level each* tree is unsatisfiable for every row, so
    // the result set is already empty
    // right after WHERE - before ORDER BY, GROUP BY, HAVING, DISTINCT or
    // pagination ever see a row.
    [Fact]
    public void FiveLevelEachTreeUnsatisfiableForEveryRowIsEmptyAfterWhere()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new FiveLevelEachTreeUnsatisfiableForEveryRowQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // Boundary: WHERE keeps rows and GROUP BY forms non-empty groups (proven
    // by the companion FiveLevelEachTreeWithHavingQuery, which keeps two
    // groups), but the always-false HAVING tree's second operand -
    // not(count >= 0) - is unsatisfiable, so every group is dropped and the
    // result becomes empty specifically at the HAVING stage.
    [Fact]
    public void FiveLevelEachTreeWithAlwaysFalseHavingIsEmptyAfterHaving()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query nonEmptyGroupsQuery = new FiveLevelEachTreeWithHavingQuery().Value;
        Query emptyAfterHavingQuery =
            new FiveLevelEachTreeWithAlwaysFalseHavingQuery().Value;

        ProjectionResult nonEmptyGroups = new ProjectionResult(
            new PureQLProjection(datasets, nonEmptyGroupsQuery)
        );
        ProjectionResult emptyAfterHaving = new ProjectionResult(
            new PureQLProjection(datasets, emptyAfterHavingQuery)
        );

        Assert.Equal(2, nonEmptyGroups.Count);
        Assert.Equal(0, emptyAfterHaving.Count);
    }

    // Boundary: WHERE, GROUP BY and HAVING all leave two groups standing
    // (proven by the companion query with no pagination), but a pagination
    // offset past the end of that two-row set (skip 5) yields an empty
    // final result - emptiness introduced specifically by pagination.
    [Fact]
    public void FiveLevelEachTreeWithOutOfRangePaginationIsEmptyAfterPagination()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query unpagedQuery = new FiveLevelEachTreeWithHavingQuery().Value;
        Query pastEndQuery = new FiveLevelEachTreeWithOutOfRangePaginationQuery().Value;

        ProjectionResult unpaged = new ProjectionResult(
            new PureQLProjection(datasets, unpagedQuery)
        );
        ProjectionResult pastEnd = new ProjectionResult(
            new PureQLProjection(datasets, pastEndQuery)
        );

        Assert.Equal(2, unpaged.Count);
        Assert.Equal(0, pastEnd.Count);
    }
}
