using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Id
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning Totals()
    {
        return new NumberArrayReturning(
            new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.PlacedOn
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
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
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
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAverageLessThanOrEqualKeepsQualifyingGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Average(order => order.OrderTotal) <= 100.50)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMinStringComparisonKeepsQualifyingGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingMaxDateComparisonKeepsQualifyingGroups()
    {
        SampleDatabase db = new SampleDatabase();
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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) < threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAndOfTwoAggregateComparisonsKeepsIntersection()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 && group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsKeepsUnion()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Count() > 1 || group.Sum(order => order.OrderTotal) > 150
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingNotInvertsAggregateComparison()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() <= 1)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingComparingTwoAggregatesOfSameGroupKeepsQualifyingGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
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
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Count < db.OrderRows.Select(o => o.OrderUserId).Distinct().Count());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingEqualityOverCountKeepsExactMatches()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() == 2)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(SampleDatabase.Orders.UserId)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingRejectingEveryGroupReturnsEmpty()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersGroupedByUser(
            new BooleanReturning(new BooleanScalar(false))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void HavingAcceptingEveryGroupKeepsAllGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersGroupedByUser(
            new BooleanReturning(new BooleanScalar(true))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedGroups = db.OrderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
