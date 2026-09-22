using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// The post-aggregation tail of the pipeline (issue #158): GROUP BY -> HAVING
// -> ORDER BY (group key, aggregate alias, mixed multi-key/direction) ->
// DISTINCT -> pagination, in the documented order (CLAUDE.md / issue #126):
// ORDER BY runs after GROUP BY/HAVING/projection in group mode, and DISTINCT
// runs on the projected group rows. Every expectation is computed
// independently against the ground-truth record lists, mirroring SQL
// result-set semantics: GroupBy(...).Where(having).Select(project)
// .Distinct().OrderBy(...).ThenBy(...).Skip(s).Take(t).
[Trait("Clause", "Combined")]
[Trait("Feature", "PostAggregationPipelineCombo")]
public sealed class PostAggregationPipelineComboTests
{
    private static Field OrderUserIdField()
    {
        return new Field(
            new UuidField("schema_with_foreign_keys.orders", "order_user_id")
        );
    }

    private static Field OrderStatusField()
    {
        return new Field(
            new StringField("schema_with_foreign_keys.orders", "order_status")
        );
    }

    private static Field UserActiveField()
    {
        return new Field(
            new BooleanField("schema_with_foreign_keys.users", "user_active")
        );
    }

    private static SelectExpression OrderUserIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
                    )
                )
            )
        );
    }

    private static SelectExpression OrderStatusSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField(
                        "schema_with_foreign_keys.orders",
                        "order_status"
                    )
                )
            )
        );
    }

    private static SelectExpression UserActiveSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new BooleanArrayReturning(
                    new BooleanField(
                        "schema_with_foreign_keys.users",
                        "user_active"
                    )
                )
            )
        );
    }

    private static NumberArrayReturning OrderTotals()
    {
        return new NumberArrayReturning(
            new NumberField("schema_with_foreign_keys.orders", "order_total")
        );
    }

    private static NumberArrayReturning UserAges()
    {
        return new NumberArrayReturning(
            new NumberField("schema_with_foreign_keys.users", "user_age")
        );
    }

    private static NumberReturning OrderCountReturning()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            "schema_with_foreign_keys.orders",
                            "order_id"
                        )
                    )
                )
            )
        );
    }

    private static NumberReturning SumTotalReturning()
    {
        return new NumberReturning(
            new NumberAggregate(new SumNumber(OrderTotals()))
        );
    }

    private static NumberReturning MaxTotalReturning()
    {
        return new NumberReturning(
            new NumberAggregate(new MaxNumber(OrderTotals()))
        );
    }

    private static NumberReturning MaxAgeReturning()
    {
        return new NumberReturning(
            new NumberAggregate(new MaxNumber(UserAges()))
        );
    }

    private static SelectExpression OrderCountSelect(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(OrderCountReturning()),
            alias
        );
    }

    private static SelectExpression SumTotalSelect(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(SumTotalReturning()),
            alias
        );
    }

    private static SelectExpression MaxTotalSelect(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(MaxTotalReturning()),
            alias
        );
    }

    private static SelectExpression MaxAgeSelect(string alias)
    {
        return new SelectExpression(
            new SingleValueReturning(MaxAgeReturning()),
            alias
        );
    }

    private static OrderByItem AliasNumberOrderBy(
        string alias,
        SortDirection direction,
        string entity = "schema_with_foreign_keys.orders"
    )
    {
        return new OrderByItem(
            new Field(new NumberField(entity, alias)),
            direction
        );
    }

    private static BooleanReturning SumTotalCompare(
        ComparisonOperator @operator,
        double threshold
    )
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    @operator,
                    SumTotalReturning(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning OrderCountCompare(
        ComparisonOperator @operator,
        double threshold
    )
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    @operator,
                    OrderCountReturning(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    // --- ORDER BY target: group key, aggregate alias, a second aggregate ---

    [Fact]
    public void OrderByGroupKeyUuidAscOrdersGroupsByKeyWithAggregatePresent()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [new OrderByItem(OrderUserIdField(), SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Key)
                .OrderBy(key => key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByGroupKeyUuidDescOrdersGroupsByKeyDescending()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [new OrderByItem(OrderUserIdField(), SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Key)
                .OrderByDescending(key => key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByAggregateAliasAscOrdersEmittedGroupsByAggregateValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), SumTotalSelect("statusSum")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [AliasNumberOrderBy("statusSum", SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByAggregateAliasDescOrdersEmittedGroupsByAggregateValueDescending()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), SumTotalSelect("statusSum")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [AliasNumberOrderBy("statusSum", SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .OrderByDescending(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(expected, actual);
    }

    // Orders by a second aggregate (MAX) that is neither the group key nor
    // the aggregate used anywhere else in the select list's usual path.
    [Fact]
    public void OrderBySecondAggregateNotUsedElsewhereOrdersGroupsByItsValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), OrderCountSelect("orderCount"), MaxTotalSelect("maxTotal")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [AliasNumberOrderBy("maxTotal", SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .OrderByDescending(group => group.Max(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // --- Multi-key group ordering: two keys, mixed directions, stable ties ---

    // Order counts per user are 2, 1, 2, 1 (users 1&3 tie at 2; users 2&4 tie
    // at 1), so ordering by count desc then userId asc exercises a genuine
    // tie-break on the second key.
    [Fact]
    public void OrderByAggregateDescThenGroupKeyAscBreaksTiesByKey()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [
                AliasNumberOrderBy("orderCount", SortDirection.Desc),
                new OrderByItem(OrderUserIdField(), SortDirection.Asc),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Mixed directions the other way round: order by count asc, then a
    // different aggregate (sum) desc, over the same tied-count groups.
    [Fact]
    public void OrderByAggregateAscThenDifferentAggregateDescOrdersDeterministically()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                OrderUserIdSelect(),
                OrderCountSelect("orderCount"),
                SumTotalSelect("totalSum"),
            ],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [
                AliasNumberOrderBy("orderCount", SortDirection.Asc),
                AliasNumberOrderBy("totalSum", SortDirection.Desc),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .OrderBy(group => group.Count())
                .ThenByDescending(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Three-key composite: count asc, sum desc, key asc. The first two keys
    // are already fully discriminating for this data, but the third key must
    // still compile and apply without disturbing the result.
    [Fact]
    public void OrderByThreeKeysCountSumThenKeyOrdersGroupsDeterministically()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                OrderUserIdSelect(),
                OrderCountSelect("orderCount"),
                SumTotalSelect("totalSum"),
            ],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [
                AliasNumberOrderBy("orderCount", SortDirection.Asc),
                AliasNumberOrderBy("totalSum", SortDirection.Desc),
                new OrderByItem(OrderUserIdField(), SortDirection.Asc),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .OrderBy(group => group.Count())
                .ThenByDescending(group => group.Sum(order => order.OrderTotal))
                .ThenBy(group => group.Key)
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // --- HAVING + ORDER BY together: membership and order both asserted ---

    [Fact]
    public void HavingFiltersGroupsThenOrderByAggregateAliasOrdersSurvivors()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            SumTotalCompare(ComparisonOperator.GreaterThan, 150),
            [AliasNumberOrderBy("totalSum", SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .OrderBy(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
        Assert.True(expected.Length < orderRows.Select(o => o.OrderUserId).Distinct().Count());
    }

    [Fact]
    public void HavingWithCountThresholdThenOrderByGroupKeyDescOrdersSurvivors()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            OrderCountCompare(ComparisonOperator.GreaterThanOrEqual, 2),
            [new OrderByItem(OrderStatusField(), SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Count() >= 2)
                .OrderByDescending(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(expected, actual);
    }

    // --- DISTINCT over the projected group rows ---

    // Projecting only the count (not the userId key) makes multiple distinct
    // groups collapse to the same output tuple: users 1&3 both count 2,
    // users 2&4 both count 1. DISTINCT reduces four group rows to two.
    [Fact]
    public void DistinctOverGroupProjectionCollapsesDuplicateAggregateTuples()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .Distinct(),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("orderCount")!.Value),
        ];

        Assert.Equal(2, expected.Length);
        Assert.True(expected.Length < orderRows.Select(o => o.OrderUserId).Distinct().Count());
        Assert.Equal([.. expected.OrderBy(v => v)], [.. actual.OrderBy(v => v)]);
    }

    // The same duplicate-collapsing projection, but with ORDER BY ascending
    // over the aggregate applied first (per the documented group-mode
    // pipeline: GROUP BY -> ORDER BY -> DISTINCT): the deduplicated values
    // come out in ascending order as a result.
    [Fact]
    public void DistinctAfterOrderByAscOverGroupProjectionYieldsAscendingDistinctValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [AliasNumberOrderBy("orderCount", SortDirection.Asc)],
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .Distinct()
                .OrderBy(count => count),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("orderCount")!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctAfterOrderByDescOverGroupProjectionYieldsDescendingDistinctValues()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [AliasNumberOrderBy("orderCount", SortDirection.Desc)],
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .Distinct()
                .OrderByDescending(count => count),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("orderCount")!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    // --- Pagination over ordered, HAVING-filtered groups ---

    // GROUP BY status yields exactly 3 ordered groups (cancelled < pending <
    // shipped). skip 0 / take all returns every group in order.
    [Fact]
    public void PaginationSkipZeroTakeAllReturnsEveryOrderedGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(0, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(3, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationSkipPastEndOfOrderedGroupsReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(10, 5)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void PaginationTakeBeyondEndOfOrderedGroupsReturnsRemainingOnly()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(1, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key)
                .Skip(1),
        ];

        string?[] actual = [.. result.Column("order_status")];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // A non-boundary window over groups that survived HAVING and were then
    // ordered descending by the aggregate.
    [Fact]
    public void PaginationWindowAfterHavingAndOrderByReturnsExactSlice()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            OrderCountCompare(ComparisonOperator.GreaterThanOrEqual, 1),
            [AliasNumberOrderBy("totalSum", SortDirection.Desc)],
            new ModelPagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Any())
                .OrderByDescending(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key)
                .Skip(1)
                .Take(2),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // --- Full tail: GROUP BY -> HAVING -> ORDER BY -> DISTINCT -> paginate ---

    // Projecting only the count collapses duplicate groups (see the
    // DISTINCT tests above); ordering ascending then taking the first page
    // returns just the smallest distinct aggregate value.
    [Fact]
    public void FullTailWithTrivialHavingOrdersDistinctThenPaginatesFirstValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderUserIdField()],
            OrderCountCompare(ComparisonOperator.GreaterThanOrEqual, 1),
            [AliasNumberOrderBy("orderCount", SortDirection.Asc)],
            new ModelPagination(0, 1),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Any())
                .Select(group => (double)group.Count())
                .Distinct()
                .OrderBy(count => count)
                .Take(1),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("orderCount")!.Value),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    // HAVING excludes the lowest-sum group (cancelled, 75.25 <= 100); the
    // survivors are ordered descending by the aggregate alias; DISTINCT is a
    // no-op here (every survivor's tuple is already unique); pagination
    // skips the first (highest) survivor.
    [Fact]
    public void FullTailWithRealHavingFilterOrdersDistinctThenPaginatesSecondValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderStatusSelect(), SumTotalSelect("statusSum")],
            where: null,
            join: null,
            [OrderStatusField()],
            SumTotalCompare(ComparisonOperator.GreaterThan, 100),
            [AliasNumberOrderBy("statusSum", SortDirection.Desc)],
            new ModelPagination(1, 5),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Sum(order => order.OrderTotal) > 100)
                .Select(group => (
                    group.Key,
                    Sum: group.Sum(order => order.OrderTotal)
                ))
                .Distinct()
                .OrderByDescending(pair => pair.Sum)
                .Select(pair => pair.Key)
                .Skip(1)
                .Take(5),
        ];

        string?[] actual = [.. result.Column("order_status")];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    // Two-key ORDER BY (count desc, sum asc) over HAVING survivors, DISTINCT
    // (a no-op: every survivor's projected tuple is unique) then a window of
    // the first two ordered survivors.
    [Fact]
    public void FullTailWithTwoKeyOrderByOrdersDistinctThenPaginatesWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                OrderUserIdSelect(),
                OrderCountSelect("orderCount"),
                SumTotalSelect("totalSum"),
            ],
            where: null,
            join: null,
            [OrderUserIdField()],
            SumTotalCompare(ComparisonOperator.GreaterThanOrEqual, 150),
            [
                AliasNumberOrderBy("orderCount", SortDirection.Desc),
                AliasNumberOrderBy("totalSum", SortDirection.Asc),
            ],
            new ModelPagination(0, 2),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) >= 150)
                .Select(group => (
                    group.Key,
                    Count: group.Count(),
                    Sum: group.Sum(order => order.OrderTotal)
                ))
                .Distinct()
                .OrderByDescending(triple => triple.Count)
                .ThenBy(triple => triple.Sum)
                .Select(triple => triple.Key)
                .Skip(0)
                .Take(2),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // --- Boolean group key (varied key type): aggregate alias ordering ---

    [Fact]
    public void OrderByAggregateAliasOverBooleanGroupKeyOrdersActiveGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [UserActiveSelect(), MaxAgeSelect("maxAge")],
            where: null,
            join: null,
            [UserActiveField()],
            having: null,
            [
                AliasNumberOrderBy(
                    "maxAge",
                    SortDirection.Desc,
                    "schema_with_foreign_keys.users"
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        bool[] expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .OrderByDescending(group => group.Max(user => user.UserAge))
                .Select(group => group.Key),
        ];

        bool[] actual =
        [
            .. result.Rows.Select(row =>
                row.Bool("user_active")!.Value
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }
}
