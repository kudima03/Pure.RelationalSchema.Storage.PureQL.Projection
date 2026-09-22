using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING beyond a bare count comparison: aggregate comparisons over sum,
// average, min/max (string and date), boolean composites, equality over an
// aggregate, and the always-true/always-false boundary conditions. Orders
// are grouped by their user; expected groups are computed from the
// ground-truth records.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingAggregateTests
{
    private static NumberReturning OrderCount()
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

    private static NumberArrayReturning Totals()
    {
        return new NumberArrayReturning(
            new NumberField("schema_with_foreign_keys.orders", "order_total")
        );
    }

    private static NumberReturning SumTotal()
    {
        return new NumberReturning(new NumberAggregate(new SumNumber(Totals())));
    }

    private static NumberReturning AverageTotal()
    {
        return new NumberReturning(new NumberAggregate(new AverageNumber(Totals())));
    }

    private static NumberReturning MaxTotal()
    {
        return new NumberReturning(new NumberAggregate(new MaxNumber(Totals())));
    }

    private static StringReturning MinStatus()
    {
        return new StringReturning(
            new StringAggregate(
                new MinString(
                    new StringArrayReturning(
                        new StringField(
                            "schema_with_foreign_keys.orders",
                            "order_status"
                        )
                    )
                )
            )
        );
    }

    private static DateReturning MaxPlacedOn()
    {
        return new DateReturning(
            new DateAggregate(
                new MaxDate(
                    new DateArrayReturning(
                        new DateField(
                            "schema_with_foreign_keys.orders",
                            "placed_on"
                        )
                    )
                )
            )
        );
    }

    private static Query OrdersGroupedByUser(
        BooleanReturning having,
        string countAlias = "orderCount"
    )
    {
        return new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
                            )
                        )
                    )
                ),
                new SelectExpression(new SingleValueReturning(OrderCount()), countAlias),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
                    )
                ),
            ],
            having,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void HavingSumGreaterThanKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        SumTotal(),
                        new NumberReturning(new NumberScalar(150))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAverageLessThanOrEqualKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThanOrEqual,
                        AverageTotal(),
                        new NumberReturning(new NumberScalar(100.50))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Average(order => order.OrderTotal) <= 100.50)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMinStringComparisonKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new global::PureQL.CSharp.Model.Comparisons.StringComparison(
                        ComparisonOperator.GreaterThanOrEqual,
                        MinStatus(),
                        new StringReturning(new StringScalar("pending"))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) >= 0
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMaxDateComparisonKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 4);

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new DateComparison(
                        ComparisonOperator.LessThan,
                        MaxPlacedOn(),
                        new DateReturning(new DateScalar(threshold))
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) < threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAndOfTwoAggregateComparisonsKeepsIntersection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        BooleanReturning countGreaterThanOne = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(1))
                )
            )
        );
        BooleanReturning sumGreaterThan150 = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    SumTotal(),
                    new NumberReturning(new NumberScalar(150))
                )
            )
        );

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator([countGreaterThanOne, sumGreaterThan150])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 && group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsKeepsUnion()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        BooleanReturning countGreaterThanOne = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(1))
                )
            )
        );
        BooleanReturning sumGreaterThan150 = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    SumTotal(),
                    new NumberReturning(new NumberScalar(150))
                )
            )
        );

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator([countGreaterThanOne, sumGreaterThan150])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 || group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingNotInvertsAggregateComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        BooleanReturning countGreaterThanOne = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(1))
                )
            )
        );

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(new NotOperator(countGreaterThanOne))
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() <= 1)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingComparingTwoAggregatesOfSameGroupKeepsQualifyingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        MaxTotal(),
                        AverageTotal()
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    > group.Average(order => order.OrderTotal)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Count < orderRows.Select(o => o.OrderUserId).Distinct().Count());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingEqualityOverCountKeepsExactMatches()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new NumberEquality(
                            OrderCount(),
                            new NumberReturning(new NumberScalar(2))
                        )
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() == 2)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid("order_user_id")!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingRejectingEveryGroupReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(new BooleanScalar(false))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void HavingAcceptingEveryGroupKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(new BooleanScalar(true))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
