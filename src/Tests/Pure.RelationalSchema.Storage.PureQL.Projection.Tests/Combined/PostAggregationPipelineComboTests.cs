using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)
        );
    }

    private static Field OrderStatusField()
    {
        return new Field(
            new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)
        );
    }

    private static Field UserActiveField()
    {
        return new Field(
            new BooleanField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)
        );
    }

    private static SelectExpression OrderUserIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
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
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Active
                    )
                )
            )
        );
    }

    private static NumberArrayReturning OrderTotals()
    {
        return new NumberArrayReturning(
            new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
        );
    }

    private static NumberArrayReturning UserAges()
    {
        return new NumberArrayReturning(
            new NumberField(SampleDatabase.Users.Entity, SampleDatabase.Users.Age)
        );
    }

    private static NumberReturning OrderCountReturning()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Id
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
        string entity = SampleDatabase.Orders.Entity
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [new OrderByItem(OrderUserIdField(), SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Key)
                .OrderBy(key => key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByGroupKeyUuidDescOrdersGroupsByKeyDescending()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [new OrderByItem(OrderUserIdField(), SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Key)
                .OrderByDescending(key => key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByAggregateAliasAscOrdersEmittedGroupsByAggregateValue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), SumTotalSelect("statusSum")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [AliasNumberOrderBy("statusSum", SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByAggregateAliasDescOrdersEmittedGroupsByAggregateValueDescending()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), SumTotalSelect("statusSum")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [AliasNumberOrderBy("statusSum", SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .OrderByDescending(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }

    // Orders by a second aggregate (MAX) that is neither the group key nor
    // the aggregate used anywhere else in the select list's usual path.
    [Fact]
    public void OrderBySecondAggregateNotUsedElsewhereOrdersGroupsByItsValue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderUserIdSelect(), OrderCountSelect("orderCount"), MaxTotalSelect("maxTotal")],
            where: null,
            join: null,
            [OrderUserIdField()],
            having: null,
            [AliasNumberOrderBy("maxTotal", SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .OrderByDescending(group => group.Max(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Mixed directions the other way round: order by count asc, then a
    // different aggregate (sum) desc, over the same tied-count groups.
    [Fact]
    public void OrderByAggregateAscThenDifferentAggregateDescOrdersDeterministically()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .OrderBy(group => group.Count())
                .ThenByDescending(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .OrderBy(group => group.Count())
                .ThenByDescending(group => group.Sum(order => order.OrderTotal))
                .ThenBy(group => group.Key)
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // --- HAVING + ORDER BY together: membership and order both asserted ---

    [Fact]
    public void HavingFiltersGroupsThenOrderByAggregateAliasOrdersSurvivors()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            SumTotalCompare(ComparisonOperator.GreaterThan, 150),
            [AliasNumberOrderBy("totalSum", SortDirection.Asc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .OrderBy(group => group.Sum(order => order.OrderTotal))
                .Select(group => group.Key),
        ];

        Guid[] actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
        Assert.True(expected.Length < db.OrderRows.Select(o => o.OrderUserId).Distinct().Count());
    }

    [Fact]
    public void HavingWithCountThresholdThenOrderByGroupKeyDescOrdersSurvivors()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            OrderCountCompare(ComparisonOperator.GreaterThanOrEqual, 2),
            [new OrderByItem(OrderStatusField(), SortDirection.Desc)],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Count() >= 2)
                .OrderByDescending(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }

    // --- DISTINCT over the projected group rows ---

    // Projecting only the count (not the userId key) makes multiple distinct
    // groups collapse to the same output tuple: users 1&3 both count 2,
    // users 2&4 both count 1. DISTINCT reduces four group rows to two.
    [Fact]
    public void DistinctOverGroupProjectionCollapsesDuplicateAggregateTuples()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .Distinct(),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("orderCount")!.Value),
        ];

        Assert.Equal(2, expected.Length);
        Assert.True(expected.Length < db.OrderRows.Select(o => o.OrderUserId).Distinct().Count());
        Assert.Equal([.. expected.OrderBy(v => v)], [.. actual.OrderBy(v => v)]);
    }

    // The same duplicate-collapsing projection, but with ORDER BY ascending
    // over the aggregate applied first (per the documented group-mode
    // pipeline: GROUP BY -> ORDER BY -> DISTINCT): the deduplicated values
    // come out in ascending order as a result.
    [Fact]
    public void DistinctAfterOrderByAscOverGroupProjectionYieldsAscendingDistinctValues()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(0, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(3, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PaginationSkipPastEndOfOrderedGroupsReturnsEmpty()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(10, 5)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void PaginationTakeBeyondEndOfOrderedGroupsReturnsRemainingOnly()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderStatusSelect(), OrderCountSelect("orderCount")],
            where: null,
            join: null,
            [OrderStatusField()],
            having: null,
            [new OrderByItem(OrderStatusField(), SortDirection.Asc)],
            new ModelPagination(1, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key)
                .Skip(1),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // A non-boundary window over groups that survived HAVING and were then
    // ordered descending by the aggregate.
    [Fact]
    public void PaginationWindowAfterHavingAndOrderByReturnsExactSlice()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderUserIdSelect(), SumTotalSelect("totalSum")],
            where: null,
            join: null,
            [OrderUserIdField()],
            OrderCountCompare(ComparisonOperator.GreaterThanOrEqual, 1),
            [AliasNumberOrderBy("totalSum", SortDirection.Desc)],
            new ModelPagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
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

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    // Two-key ORDER BY (count desc, sum asc) over HAVING survivors, DISTINCT
    // (a no-op: every survivor's projected tuple is unique) then a window of
    // the first two ordered survivors.
    [Fact]
    public void FullTailWithTwoKeyOrderByOrdersDistinctThenPaginatesWindow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
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
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }

    // --- Boolean group key (varied key type): aggregate alias ordering ---

    [Fact]
    public void OrderByAggregateAliasOverBooleanGroupKeyOrdersActiveGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserActiveSelect(), MaxAgeSelect("maxAge")],
            where: null,
            join: null,
            [UserActiveField()],
            having: null,
            [
                AliasNumberOrderBy(
                    "maxAge",
                    SortDirection.Desc,
                    SampleDatabase.Users.Entity
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        bool[] expected =
        [
            .. db.UserRows
                .GroupBy(user => user.UserActive)
                .OrderByDescending(group => group.Max(user => user.UserAge))
                .Select(group => group.Key),
        ];

        bool[] actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(SampleDatabase.Users.Active)!.Value
            ),
        ];

        Assert.Equal(2, expected.Length);
        Assert.Equal(expected, actual);
    }
}
