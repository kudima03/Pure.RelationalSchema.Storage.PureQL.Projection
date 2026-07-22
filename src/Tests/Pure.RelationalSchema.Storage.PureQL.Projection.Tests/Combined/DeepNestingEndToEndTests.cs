using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

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
                            new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                        )
                    )
                )
            )
        );
    }

    private static Join OrdersToItemsJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.OrderItems.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
                            )
                        )
                    )
                )
            )
        );
    }

    private static SelectExpression ItemIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.OrderItems.Entity, SampleDatabase.OrderItems.Id)
                )
            )
        );
    }

    private static SelectExpression ItemQtySelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new NumberArrayReturning(
                    new NumberField(
                        SampleDatabase.OrderItems.Entity,
                        SampleDatabase.OrderItems.Qty
                    )
                )
            )
        );
    }

    private static SelectExpression OrderTotalSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new NumberArrayReturning(
                    new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
                )
            )
        );
    }

    // 5-level, AND-rooted, all-constant scalar tree - identical shape to
    // Where/Scalar/NestedBooleanTests.ScalarFiveLevelAndRootedTreeKeepsEveryRow:
    //   and(or(not(and(a, b)), c), or(not(d), and(e, f)))
    // a=true, b=false, c=true, d=false, e=true, f=true.
    // Left:  and(a,b)=false -> not=true -> or(true,c)=true.
    // Right: not(d)=true -> or(true, and(e,f))=true.
    // and(true,true)=true - keeps every row it is applied to, exactly as in
    // the per-clause test it is reused from.
    private static BooleanReturning ScalarFiveLevelAlwaysTrueTree()
    {
        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning c = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning d = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning e = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning f = new BooleanReturning(new BooleanScalar(true));

        BooleanReturning leftBranch = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(
                                        new BooleanOperator(new AndOperator([a, b]))
                                    )
                                )
                            )
                        ),
                        c,
                    ]
                )
            )
        );
        BooleanReturning rightBranch = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        new BooleanReturning(new BooleanOperator(new NotOperator(d))),
                        new BooleanReturning(
                            new BooleanOperator(new AndOperator([e, f]))
                        ),
                    ]
                )
            )
        );

        return new BooleanReturning(
            new BooleanOperator(new AndOperator([leftBranch, rightBranch]))
        );
    }

    private static NumberArrayReturning TotalField()
    {
        return new NumberArrayReturning(
            new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
        );
    }

    private static StringArrayReturning StatusField()
    {
        return new StringArrayReturning(
            new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)
        );
    }

    // The six per-row leaves shared by the each* 5-level tree and its De
    // Morgan transform, identical (field, operator, threshold) to
    // Where/Each/NestedEachTests.FiveLevelAndRootedTreeMatchesRowByRowAgainstLinqPredicate:
    //   a = total > 100          b = status == "pending"
    //   c = total >= 300         d = status == "cancelled"
    //   e = total < 100          f = status == "shipped"
    private static (
        BooleanArrayReturning A,
        BooleanArrayReturning B,
        BooleanArrayReturning C,
        BooleanArrayReturning D,
        BooleanArrayReturning E,
        BooleanArrayReturning F
    ) EachTreeLeaves()
    {
        BooleanArrayReturning a = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    TotalField(),
                    new NumberReturning(new NumberScalar(100))
                )
            )
        );
        BooleanArrayReturning b = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    StatusField(),
                    new StringReturning(new StringScalar("pending"))
                )
            )
        );
        BooleanArrayReturning c = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    TotalField(),
                    new NumberReturning(new NumberScalar(300))
                )
            )
        );
        BooleanArrayReturning d = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    StatusField(),
                    new StringReturning(new StringScalar("cancelled"))
                )
            )
        );
        BooleanArrayReturning e = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachLessThan,
                    TotalField(),
                    new NumberReturning(new NumberScalar(100))
                )
            )
        );
        BooleanArrayReturning f = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    StatusField(),
                    new StringReturning(new StringScalar("shipped"))
                )
            )
        );

        return (a, b, c, d, e, f);
    }

    // 5-level, AND-rooted each* tree, identical shape to
    // Where/Each/NestedEachTests.FiveLevelAndRootedTreeMatchesRowByRowAgainstLinqPredicate:
    //   eachAnd(
    //     eachOr(eachNot(eachAnd(a, b)), c),
    //     eachOr(eachNot(d), eachAnd(e, f))
    //   )
    // Per that test's derivation, orders 101/102/103/105 satisfy the tree and
    // 104/106 do not.
    private static BooleanArrayReturning EachFiveLevelTreeOverOrderFields()
    {
        (
            BooleanArrayReturning a,
            BooleanArrayReturning b,
            BooleanArrayReturning c,
            BooleanArrayReturning d,
            BooleanArrayReturning e,
            BooleanArrayReturning f
        ) = EachTreeLeaves();

        BooleanArrayReturning leftBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(
                        new EachNotOperator(
                            new BooleanArrayReturning(new EachAndOperator([a, b]))
                        )
                    ),
                    c,
                ]
            )
        );
        BooleanArrayReturning rightBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(new EachNotOperator(d)),
                    new BooleanArrayReturning(new EachAndOperator([e, f])),
                ]
            )
        );

        return new BooleanArrayReturning(new EachAndOperator([leftBranch, rightBranch]));
    }

    // De Morgan transform of the tree above: eachNot(eachAnd(a, b)) is
    // rewritten as eachOr(eachNot(a), eachNot(b)) - a differently-shaped but
    // logically identical left branch, same leaves, same overall depth.
    //   eachAnd(
    //     eachOr(eachOr(eachNot(a), eachNot(b)), c),
    //     eachOr(eachNot(d), eachAnd(e, f))
    //   )
    private static BooleanArrayReturning EachFiveLevelTreeDeMorganTransformed()
    {
        (
            BooleanArrayReturning a,
            BooleanArrayReturning b,
            BooleanArrayReturning c,
            BooleanArrayReturning d,
            BooleanArrayReturning e,
            BooleanArrayReturning f
        ) = EachTreeLeaves();

        BooleanArrayReturning leftBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(
                        new EachOrOperator(
                            [
                                new BooleanArrayReturning(new EachNotOperator(a)),
                                new BooleanArrayReturning(new EachNotOperator(b)),
                            ]
                        )
                    ),
                    c,
                ]
            )
        );
        BooleanArrayReturning rightBranch = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(new EachNotOperator(d)),
                    new BooleanArrayReturning(new EachAndOperator([e, f])),
                ]
            )
        );

        return new BooleanArrayReturning(new EachAndOperator([leftBranch, rightBranch]));
    }

    // Structurally 5-level (root AND -> OR -> NOT -> AND -> leaf) but
    // unsatisfiable for every row: branch1 is
    // or(not(and(alwaysTrue, alwaysTrue)), alwaysFalse) = or(false, false) =
    // false; branch2 is and(or(alwaysFalse, alwaysFalse), not(alwaysTrue)) =
    // and(false, false) = false. and(false, false) = false for every row.
    private static BooleanArrayReturning EachFiveLevelUnsatisfiableTree()
    {
        BooleanArrayReturning alwaysTrue1 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    TotalField(),
                    new NumberReturning(new NumberScalar(-1000000))
                )
            )
        );
        BooleanArrayReturning alwaysTrue2 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachLessThan,
                    TotalField(),
                    new NumberReturning(new NumberScalar(1000000))
                )
            )
        );
        BooleanArrayReturning alwaysFalse = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    StatusField(),
                    new StringReturning(new StringScalar("no_such_status_xyz"))
                )
            )
        );

        BooleanArrayReturning branch1 = new BooleanArrayReturning(
            new EachOrOperator(
                [
                    new BooleanArrayReturning(
                        new EachNotOperator(
                            new BooleanArrayReturning(
                                new EachAndOperator([alwaysTrue1, alwaysTrue2])
                            )
                        )
                    ),
                    alwaysFalse,
                ]
            )
        );
        BooleanArrayReturning branch2 = new BooleanArrayReturning(
            new EachAndOperator(
                [
                    new BooleanArrayReturning(
                        new EachOrOperator([alwaysFalse, alwaysFalse])
                    ),
                    new BooleanArrayReturning(new EachNotOperator(alwaysTrue1)),
                ]
            )
        );

        return new BooleanArrayReturning(new EachAndOperator([branch1, branch2]));
    }

    private static Field OrderIdKey()
    {
        return new Field(
            new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
        );
    }

    private static SelectExpression OrderIdFieldSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                )
            )
        );
    }

    private static NumberReturning ItemCountAggregate()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            SampleDatabase.OrderItems.Entity,
                            SampleDatabase.OrderItems.Id
                        )
                    )
                )
            )
        );
    }

    private static NumberReturning QtySumAggregate()
    {
        return new NumberReturning(
            new NumberAggregate(
                new SumNumber(
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.OrderItems.Entity,
                            SampleDatabase.OrderItems.Qty
                        )
                    )
                )
            )
        );
    }

    private static SelectExpression QtySumSelect()
    {
        return new SelectExpression(new SingleValueReturning(QtySumAggregate()), "qtySum");
    }

    private static SelectExpression ItemCountSelect()
    {
        return new SelectExpression(
            new SingleValueReturning(ItemCountAggregate()),
            "itemCount"
        );
    }

    private static BooleanReturning CountAtLeast(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
                    ItemCountAggregate(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning QtySumAtLeast(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
                    QtySumAggregate(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    // 3-level HAVING: and(or(count>=2, qtySum>=5), not(count>=100)).
    // count>=100 is unsatisfiable for these groups, so not(...) is always
    // true and the whole tree reduces to the or(...) clause - which keeps
    // the order101 group (count=2) and the order103 group (qtySum=5), and
    // drops the order105 group (count=1, qtySum=3).
    private static BooleanReturning HavingThreeLevelTree()
    {
        return new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(
                                new OrOperator([CountAtLeast(2), QtySumAtLeast(5)])
                            )
                        ),
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(CountAtLeast(100)))
                        ),
                    ]
                )
            )
        );
    }

    // Same 3-level shape as HavingThreeLevelTree, but the second operand is
    // not(count>=0), which is unsatisfiable (count is never negative), so
    // the and(...) is false for every group regardless of the first operand.
    private static BooleanReturning HavingThreeLevelAlwaysFalseTree()
    {
        return new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(
                                new OrOperator([CountAtLeast(2), QtySumAtLeast(5)])
                            )
                        ),
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(CountAtLeast(0)))
                        ),
                    ]
                )
            )
        );
    }

    private static Query FullPipelineQuery(
        BooleanArrayReturning where,
        BooleanReturning having,
        ModelPagination? pagination
    )
    {
        return new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdFieldSelect(), QtySumSelect(), ItemCountSelect()],
            where,
            [OrdersToUsersJoin(), OrdersToItemsJoin()],
            [OrderIdKey()],
            having,
            [new OrderByItem(OrderIdKey(), SortDirection.Asc)],
            pagination,
            distinct: true
        );
    }

    // JOIN (3 tables) -> WHERE (5-level nested scalar and/or/not tree) ->
    // ORDER BY (multi-key) -> pagination. The scalar tree is a constant
    // all-or-nothing filter (see ScalarFiveLevelAlwaysTrueTree), so every
    // joined row survives WHERE unfiltered; the join itself is still
    // restrictive (INNER JOIN order_items drops orders with no items:
    // 102, 104, 106), leaving four (order, item) rows to sort and page.
    [Fact]
    public void MultiJoinFiveLevelScalarTreeOrderByMultiKeyThenPaginateReturnsWindow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [ItemIdSelect(), ItemQtySelect(), OrderTotalSelect()],
            ScalarFiveLevelAlwaysTrueTree(),
            [OrdersToUsersJoin(), OrdersToItemsJoin()],
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.OrderItems.Entity,
                            SampleDatabase.OrderItems.Qty
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            new ModelPagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (Guid ItemId, double Qty, double Total)[] expected =
        [
            .. (
                from order in db.OrderRows
                join user in db.UserRows on order.OrderUserId equals user.UserId
                join item in db.OrderItemRows on order.OrderId equals item.ItemOrderId
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
                    row.Uuid(SampleDatabase.OrderItems.Id)!.Value,
                    row.Double(SampleDatabase.OrderItems.Qty)!.Value,
                    row.Double(SampleDatabase.Orders.Total)!.Value
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
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelTree(),
            new ModelPagination(0, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        bool EachTreePredicate(OrderRow order)
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
                from order in db.OrderRows
                join user in db.UserRows on order.OrderUserId equals user.UserId
                join item in db.OrderItemRows on order.OrderId equals item.ItemOrderId
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
                    row.Uuid(SampleDatabase.Orders.Id)!.Value,
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
        SampleDatabase db = new SampleDatabase();

        Query queryA = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelTree(),
            pagination: null
        );
        Query queryB = FullPipelineQuery(
            EachFiveLevelTreeDeMorganTransformed(),
            HavingThreeLevelTree(),
            pagination: null
        );

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryB)
        );

        static (Guid, double, double)[] Rows(ProjectionResult result)
        {
            return
            [
                .. result.Rows.Select(row =>
                    (
                        row.Uuid(SampleDatabase.Orders.Id)!.Value,
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

    // Boundary: the 5-level each* tree is unsatisfiable for every row (see
    // EachFiveLevelUnsatisfiableTree), so the result set is already empty
    // right after WHERE - before ORDER BY, GROUP BY, HAVING, DISTINCT or
    // pagination ever see a row.
    [Fact]
    public void FiveLevelEachTreeUnsatisfiableForEveryRowIsEmptyAfterWhere()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = FullPipelineQuery(
            EachFiveLevelUnsatisfiableTree(),
            HavingThreeLevelTree(),
            new ModelPagination(0, 10)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // Boundary: WHERE keeps rows and GROUP BY forms non-empty groups (proven
    // by the companion query using HavingThreeLevelTree, which keeps two
    // groups), but HavingThreeLevelAlwaysFalseTree's second operand -
    // not(count >= 0) - is unsatisfiable, so every group is dropped and the
    // result becomes empty specifically at the HAVING stage.
    [Fact]
    public void FiveLevelEachTreeWithAlwaysFalseHavingIsEmptyAfterHaving()
    {
        SampleDatabase db = new SampleDatabase();

        Query nonEmptyGroupsQuery = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelTree(),
            pagination: null
        );
        Query emptyAfterHavingQuery = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelAlwaysFalseTree(),
            pagination: null
        );

        ProjectionResult nonEmptyGroups = new ProjectionResult(
            new PureQLProjection(db.Datasets, nonEmptyGroupsQuery)
        );
        ProjectionResult emptyAfterHaving = new ProjectionResult(
            new PureQLProjection(db.Datasets, emptyAfterHavingQuery)
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
        SampleDatabase db = new SampleDatabase();

        Query unpagedQuery = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelTree(),
            pagination: null
        );
        Query pastEndQuery = FullPipelineQuery(
            EachFiveLevelTreeOverOrderFields(),
            HavingThreeLevelTree(),
            new ModelPagination(5, 2)
        );

        ProjectionResult unpaged = new ProjectionResult(
            new PureQLProjection(db.Datasets, unpagedQuery)
        );
        ProjectionResult pastEnd = new ProjectionResult(
            new PureQLProjection(db.Datasets, pastEndQuery)
        );

        Assert.Equal(2, unpaged.Count);
        Assert.Equal(0, pastEnd.Count);
    }
}
