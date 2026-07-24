using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Matrix of aggregate (count / numeric sum-avg-min-max) over each-arithmetic
// (eachAdd/eachSubtract/eachMultiply/eachDivide) arguments, across varied
// group-key types (uuid/string/bool/number) and whole-set, plus HAVING on
// the computed aggregate. Temporal each-expression combos live in
// AggregateOverExpressionComboTemporalTests.cs. Every expected value is
// computed independently in LINQ over the ground-truth record lists under
// SQL aggregate rules: aggregates ignore NULL inputs, COUNT ignores NULL
// results the same as any other aggregate, and an all-NULL/empty group folds
// to NULL (COUNT folds to 0).
//
// eachDivide by zero: WhereExpressionBuilder.DivideDoubles raises
// DivideByZeroException for a zero divisor (matching SQL division-by-zero
// semantics), not a silent NULL - pinned for WHERE by
// Errors/NegativePathTests.EachDivideByZeroFailsFast. This suite pins the
// identical fail-fast behaviour when the same expression is folded by an
// aggregate instead of filtered by WHERE.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "AggregateOverExpressionCombo")]
public sealed class AggregateOverExpressionComboTests
{
    // ===== Join chains =====

    private static IReadOnlyList<Join> OrdersToItemsToProductsJoin()
    {
        return
        [
            new Join(
                JoinType.Inner,
                SampleDatabase.OrderItems.Entity,
                new BooleanArrayReturning(
                    new EachEquality(
                        new EachUuidEquality(
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.OrderItems.Entity,
                                    SampleDatabase.OrderItems.OrderId
                                )
                            ),
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.Id
                                )
                            )
                        )
                    )
                )
            ),
            new Join(
                JoinType.Inner,
                SampleDatabase.Products.Entity,
                new BooleanArrayReturning(
                    new EachEquality(
                        new EachUuidEquality(
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.OrderItems.Entity,
                                    SampleDatabase.OrderItems.ProductId
                                )
                            ),
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.Products.Entity,
                                    SampleDatabase.Products.Id
                                )
                            )
                        )
                    )
                )
            ),
        ];
    }

    private static Join OrdersToUsersJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Users.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                )
            )
        );
    }

    // ===== each-arithmetic arguments =====

    private static NumberArrayReturning QtyTimesPrice()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachMultiply(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.Qty
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Price
                            )
                        ),
                    ]
                )
            )
        );
    }

    private static NumberArrayReturning TotalPlusAge()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachAdd(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        ),
                    ]
                )
            )
        );
    }

    private static NumberArrayReturning TotalMinusAge()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachSubtract(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        ),
                    ]
                )
            )
        );
    }

    private static NumberArrayReturning TotalDividedByScore()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachDivide(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
                            )
                        ),
                    ]
                )
            )
        );
    }

    // Denominator is zero exactly for orders whose total is 100.50 (orders
    // 101 and 106), to pin the aggregate-context divide-by-zero fail-fast.
    private static NumberArrayReturning TotalDividedByTotalMinusThreshold()
    {
        return new NumberArrayReturning(
            new EachArithmetic(
                new EachDivide(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachSubtract(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(100.50)),
                                    ]
                                )
                            )
                        ),
                    ]
                )
            )
        );
    }

    // ===== select / group-by builders =====

    private static SelectExpression AggregateSelect(
        NumberAggregate aggregate,
        string alias
    )
    {
        return new SelectExpression(
            new SingleValueReturning(new NumberReturning(aggregate)),
            alias
        );
    }

    private static NumberReturning SumOf(NumberArrayReturning argument)
    {
        return new NumberReturning(new NumberAggregate(new SumNumber(argument)));
    }

    private static NumberReturning AverageOf(NumberArrayReturning argument)
    {
        return new NumberReturning(new NumberAggregate(new AverageNumber(argument)));
    }

    private static NumberReturning CountOf(NumberArrayReturning argument)
    {
        return new NumberReturning(new Count(new ArrayReturning(argument)));
    }

    private static SelectExpression CountSelect(NumberArrayReturning argument, string alias)
    {
        return new SelectExpression(new SingleValueReturning(CountOf(argument)), alias);
    }

    private static SelectExpression UuidGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new UuidArrayReturning(new UuidField(entity, field)))
        );
    }

    private static SelectExpression StringGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new StringArrayReturning(new StringField(entity, field)))
        );
    }

    private static SelectExpression BoolGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new BooleanArrayReturning(new BooleanField(entity, field)))
        );
    }

    private static SelectExpression NumberGroupKeySelect(string entity, string field)
    {
        return new SelectExpression(
            new ArrayReturning(new NumberArrayReturning(new NumberField(entity, field)))
        );
    }

    private static Field UuidGroupKeyField(string entity, string field)
    {
        return new Field(new UuidField(entity, field));
    }

    private static Field StringGroupKeyField(string entity, string field)
    {
        return new Field(new StringField(entity, field));
    }

    private static Field BoolGroupKeyField(string entity, string field)
    {
        return new Field(new BooleanField(entity, field));
    }

    // ===== SQL NULL-aware folds over a nullable-double sequence =====

    private static double? SqlSum(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Sum();
    }

    private static double SqlCount(IEnumerable<double?> values)
    {
        return values.Count(v => v.HasValue);
    }

    private static double? SqlMin(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Min();
    }

    private static double? SqlMax(IEnumerable<double?> values)
    {
        List<double> defined = [.. values.Where(v => v.HasValue).Select(v => v!.Value)];
        return defined.Count == 0 ? null : defined.Max();
    }

    // ===== A: eachMultiply(item_qty, product_price), grouped by uuid =====

    [Fact]
    public void SumOfEachMultiplyGroupedByOrderUserIdComputesRevenuePerUser()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                AggregateSelect(new NumberAggregate(new SumNumber(QtyTimesPrice())), "revenue"),
            ],
            where: null,
            OrdersToItemsToProductsJoin(),
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
            join product in db.ProductRows on item.ItemProductId equals product.ProductId
            select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("revenue")!.Value
        );

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachMultiplyGroupedByOrderUserIdComputesMeanLineValue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                AggregateSelect(
                    new NumberAggregate(new AverageNumber(QtyTimesPrice())),
                    "meanLineValue"
                ),
            ],
            where: null,
            OrdersToItemsToProductsJoin(),
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
            join product in db.ProductRows on item.ItemProductId equals product.ProductId
            select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
        )
            .GroupBy(x => x.OrderUserId)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("meanLineValue")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinAndMaxOfEachMultiplyGroupedByOrderUserIdBoundLineValues()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                AggregateSelect(
                    new NumberAggregate(new MinNumber(QtyTimesPrice())),
                    "minLineValue"
                ),
                AggregateSelect(
                    new NumberAggregate(new MaxNumber(QtyTimesPrice())),
                    "maxLineValue"
                ),
            ],
            where: null,
            OrdersToItemsToProductsJoin(),
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        IReadOnlyList<(Guid UserId, double Value)> lineValues =
        [
            .. from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
            join product in db.ProductRows on item.ItemProductId equals product.ProductId
            select (order.OrderUserId, Value: item.ItemQty * product.ProductPrice),
        ];

        Dictionary<Guid, double> expectedMin = lineValues
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Value));

        Dictionary<Guid, double> expectedMax = lineValues
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Value));

        Dictionary<Guid, double> actualMin = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("minLineValue")!.Value
        );

        Dictionary<Guid, double> actualMax = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("maxLineValue")!.Value
        );

        Assert.Equal(expectedMin, actualMin);
        Assert.Equal(expectedMax, actualMax);
    }

    [Fact]
    public void CountOfEachMultiplyGroupedByOrderUserIdCountsLineItems()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                CountSelect(QtyTimesPrice(), "lineCount"),
            ],
            where: null,
            OrdersToItemsToProductsJoin(),
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = (
            from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
            join product in db.ProductRows on item.ItemProductId equals product.ProductId
            select order.OrderUserId
        )
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("lineCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetSumOfEachMultiplyComputesTotalRevenue()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [AggregateSelect(new NumberAggregate(new SumNumber(QtyTimesPrice())), "revenue")],
            where: null,
            OrdersToItemsToProductsJoin(),
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expected = (
            from item in db.OrderItemRows
            join order in db.OrderRows on item.ItemOrderId equals order.OrderId
            join product in db.ProductRows on item.ItemProductId equals product.ProductId
            select item.ItemQty * product.ProductPrice
        ).Sum();

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("revenue"));
    }

    // ===== B: eachAdd/eachSubtract(order_total, user_age), grouped by
    // bool/number/string =====

    [Fact]
    public void SumOfEachAddGroupedByUserActiveComputesTotalPlusAge()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                BoolGroupKeySelect(SampleDatabase.Users.Entity, SampleDatabase.Users.Active),
                AggregateSelect(
                    new NumberAggregate(new SumNumber(TotalPlusAge())),
                    "totalPlusAge"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [BoolGroupKeyField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<bool, double> expected = (
            from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select new { user.UserActive, Value = order.OrderTotal + user.UserAge }
        )
            .GroupBy(x => x.UserActive)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(SampleDatabase.Users.Active)!.Value,
            row => row.Double("totalPlusAge")!.Value
        );

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AverageOfEachAddGroupedByUserAgeComputesMeanTotalPlusAge()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                NumberGroupKeySelect(SampleDatabase.Users.Entity, SampleDatabase.Users.Age),
                AggregateSelect(
                    new NumberAggregate(new AverageNumber(TotalPlusAge())),
                    "meanTotalPlusAge"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [new Field(new NumberField(SampleDatabase.Users.Entity, SampleDatabase.Users.Age))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<double, double> expected = (
            from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select new { user.UserAge, Value = order.OrderTotal + user.UserAge }
        )
            .GroupBy(x => x.UserAge)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Value));

        Dictionary<double, double> actual = result.Rows.ToDictionary(
            row => row.Double(SampleDatabase.Users.Age)!.Value,
            row => row.Double("meanTotalPlusAge")!.Value
        );

        // Ann and Cara share age 30, so this group merges two users' orders -
        // a real multi-user group, not merely one row per group.
        Assert.Contains(expected, pair => pair.Key == 30);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MinAndMaxOfEachSubtractGroupedByUserActiveBoundDifference()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                BoolGroupKeySelect(SampleDatabase.Users.Entity, SampleDatabase.Users.Active),
                AggregateSelect(new NumberAggregate(new MinNumber(TotalMinusAge())), "minDiff"),
                AggregateSelect(new NumberAggregate(new MaxNumber(TotalMinusAge())), "maxDiff"),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [BoolGroupKeyField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        IReadOnlyList<(bool Active, double Value)> diffs =
        [
            .. from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select (user.UserActive, Value: order.OrderTotal - user.UserAge),
        ];

        Dictionary<bool, double> expectedMin = diffs
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Value));

        Dictionary<bool, double> expectedMax = diffs
            .GroupBy(x => x.Active)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Value));

        Dictionary<bool, double> actualMin = result.Rows.ToDictionary(
            row => row.Bool(SampleDatabase.Users.Active)!.Value,
            row => row.Double("minDiff")!.Value
        );

        Dictionary<bool, double> actualMax = result.Rows.ToDictionary(
            row => row.Bool(SampleDatabase.Users.Active)!.Value,
            row => row.Double("maxDiff")!.Value
        );

        Assert.Equal(expectedMin, actualMin);
        Assert.Equal(expectedMax, actualMax);
    }

    [Fact]
    public void CountOfEachSubtractGroupedByOrderStatusCountsRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                StringGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status),
                CountSelect(TotalMinusAge(), "diffCount"),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [StringGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<string, double> expected = db.OrderRows
            .GroupBy(order => order.OrderStatus)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[SampleDatabase.Orders.Status]!,
            row => row.Double("diffCount")!.Value
        );

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetAverageOfEachSubtractComputesOverallMeanDifference()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [AggregateSelect(new NumberAggregate(new AverageNumber(TotalMinusAge())), "meanDiff")],
            where: null,
            [OrdersToUsersJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expected = (
            from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select order.OrderTotal - user.UserAge
        ).Average();

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("meanDiff"));
    }

    // ===== C: eachDivide(order_total, user_score) - NULL exclusion and
    // divide-by-zero fail-fast =====

    [Fact]
    public void SumAndCountOfEachDivideGroupedByOrderUserIdExcludeNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                AggregateSelect(
                    new NumberAggregate(new SumNumber(TotalDividedByScore())),
                    "sumRatio"
                ),
                CountSelect(TotalDividedByScore(), "ratioCount"),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        IReadOnlyList<(Guid UserId, double? Ratio)> ratios =
        [
            .. from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select (
                order.OrderUserId,
                Ratio: user.Score.HasValue ? order.OrderTotal / user.Score.Value : (double?)null
            ),
        ];

        Dictionary<Guid, double?> expectedSum = ratios
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => SqlSum(g.Select(x => x.Ratio)));

        Dictionary<Guid, double> expectedCount = ratios
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => SqlCount(g.Select(x => x.Ratio)));

        Dictionary<Guid, double?> actualSum = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("sumRatio")
        );

        Dictionary<Guid, double> actualCount = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("ratioCount")!.Value
        );

        // Bob and Dan each have exactly one order and a NULL score, so their
        // group folds to an all-NULL sum (SQL SUM over no defined rows) and
        // a zero count - not merely "smaller", but genuinely empty.
        Assert.Contains(expectedSum, pair => pair.Value is null);
        Assert.Contains(expectedCount, pair => pair.Value == 0);
        Assert.Equal(expectedSum, actualSum);
        Assert.Equal(expectedCount, actualCount);
    }

    [Fact]
    public void MinOfEachDivideWholeSetExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [AggregateSelect(new NumberAggregate(new MinNumber(TotalDividedByScore())), "minRatio")],
            where: null,
            [OrdersToUsersJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        IEnumerable<double?> ratios =
            from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select user.Score.HasValue ? order.OrderTotal / user.Score.Value : (double?)null;

        double? expected = SqlMin(ratios);

        _ = Assert.NotNull(expected);
        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("minRatio"));
    }

    [Fact]
    public void MaxOfEachDivideWholeSetExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [AggregateSelect(new NumberAggregate(new MaxNumber(TotalDividedByScore())), "maxRatio")],
            where: null,
            [OrdersToUsersJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        IEnumerable<double?> ratios =
            from order in db.OrderRows
            join user in db.UserRows on order.OrderUserId equals user.UserId
            select user.Score.HasValue ? order.OrderTotal / user.Score.Value : (double?)null;

        double? expected = SqlMax(ratios);

        _ = Assert.NotNull(expected);
        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("maxRatio"));
    }

    [Fact]
    public void AggregateOverEachDivideByZeroDenominatorThrowsDivideByZeroException()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                AggregateSelect(
                    new NumberAggregate(new SumNumber(TotalDividedByTotalMinusThreshold())),
                    "sumRatio"
                ),
            ]
        );

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ===== D: HAVING on the computed aggregate =====

    [Fact]
    public void HavingSumOfEachMultiplyGreaterThanKeepsQualifyingOrders()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                AggregateSelect(new NumberAggregate(new SumNumber(QtyTimesPrice())), "revenue"),
            ],
            where: null,
            OrdersToItemsToProductsJoin(),
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        SumOf(QtyTimesPrice()),
                        new NumberReturning(new NumberScalar(25))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. (
                from item in db.OrderItemRows
                join order in db.OrderRows on item.ItemOrderId equals order.OrderId
                join product in db.ProductRows
                    on item.ItemProductId equals product.ProductId
                select new { order.OrderUserId, Value = item.ItemQty * product.ProductPrice }
            )
                .GroupBy(x => x.OrderUserId)
                .Where(g => g.Sum(x => x.Value) > 25)
                .Select(g => g.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Orders.UserId)!.Value),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingAverageOfEachSubtractLessThanOrEqualKeepsQualifyingGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                BoolGroupKeySelect(SampleDatabase.Users.Entity, SampleDatabase.Users.Active),
                AggregateSelect(
                    new NumberAggregate(new AverageNumber(TotalMinusAge())),
                    "meanDiff"
                ),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [BoolGroupKeyField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.LessThanOrEqual,
                        AverageOf(TotalMinusAge()),
                        new NumberReturning(new NumberScalar(100))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<bool> expected =
        [
            .. (
                from order in db.OrderRows
                join user in db.UserRows on order.OrderUserId equals user.UserId
                select new { user.UserActive, Value = order.OrderTotal - user.UserAge }
            )
                .GroupBy(x => x.UserActive)
                .Where(g => g.Average(x => x.Value) <= 100)
                .Select(g => g.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row => row.Bool(SampleDatabase.Users.Active)!.Value),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HavingCountOfEachDivideEqualToZeroKeepsOnlyAllNullGroups()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                UuidGroupKeySelect(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId),
                CountSelect(TotalDividedByScore(), "ratioCount"),
            ],
            where: null,
            [OrdersToUsersJoin()],
            [UuidGroupKeyField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId)],
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new NumberEquality(
                            CountOf(TotalDividedByScore()),
                            new NumberReturning(new NumberScalar(0))
                        )
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. (
                from order in db.OrderRows
                join user in db.UserRows on order.OrderUserId equals user.UserId
                select (
                    order.OrderUserId,
                    Ratio: user.Score.HasValue
                        ? order.OrderTotal / user.Score.Value
                        : (double?)null
                )
            )
                .GroupBy(x => x.OrderUserId)
                .Where(g => SqlCount(g.Select(x => x.Ratio)) == 0)
                .Select(g => g.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(SampleDatabase.Orders.UserId)!.Value),
        ];

        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);
    }
}
