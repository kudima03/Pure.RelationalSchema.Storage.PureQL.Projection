using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Deep nesting of the per-row (each*) boolean family (eachAnd/eachOr/eachNot
// composing eachEquality/eachComparison/eachArithmetic leaves), at increasing
// depth (2 through 5 levels), plus the other batch items from issue #98:
// mixed arithmetic+boolean nesting, each* over joined columns inside a
// nested tree, and negative/empty cases. Unlike the scalar family
// (NestedBooleanTests), each leaf here reads a real per-row field, so the
// keep/remove outcome varies row by row rather than being all-or-nothing;
// every test derives the expected per-row truth value inline and cross-checks
// against a LINQ-equivalent predicate over SampleDatabase.
[Trait("Clause", "Where")]
[Trait("Feature", "NestedEach")]
public sealed class NestedEachTests
{
    private static SelectExpression OrderIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                )
            )
        );
    }

    // 2-level: eachAnd(total > 100, status == "shipped")
    [Fact]
    public void TwoLevelEachAndOfComparisonAndEqualityFiltersByBothConditions()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(100))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(o => o.OrderTotal > 100 && o.OrderStatus == "shipped"),
            result.Count
        );
    }

    // 2-level: eachOr(eachNot(status == "cancelled"), total < 0)
    [Fact]
    public void TwoLevelEachOrOfNotAndComparisonFiltersByEitherCondition()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachNotOperator(
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachStringEquality(
                                            new StringArrayReturning(
                                                new StringField(
                                                    SampleDatabase.Orders.Entity,
                                                    SampleDatabase.Orders.Status
                                                )
                                            ),
                                            new StringReturning(
                                                new StringScalar("cancelled")
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachLessThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(0))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(o => o.OrderStatus != "cancelled" || o.OrderTotal < 0),
            result.Count
        );
    }

    // 3-level: eachAnd(eachOr(total >= 200, status == "pending"),
    //                   eachNot(status == "cancelled"))
    [Fact]
    public void ThreeLevelEachAndOfOrAndNotFiltersByCombinedCondition()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanArrayReturning totalAtLeast200 = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(200))
                )
            )
        );
        BooleanArrayReturning statusPending = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("pending"))
                )
            )
        );
        BooleanArrayReturning statusCancelled = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("cancelled"))
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator([totalAtLeast200, statusPending])
                        ),
                        new BooleanArrayReturning(new EachNotOperator(statusCancelled)),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(o =>
                (o.OrderTotal >= 200 || o.OrderStatus == "pending")
                && o.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    // 3-level, mixed arithmetic + boolean:
    //   eachAnd(eachComparison(eachAdd(total, 50) > 150),
    //           eachOr(status == "shipped", total < 60))
    [Fact]
    public void ThreeLevelPerRowArithmeticInsideComparisonInsideEachAndFilters()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new EachArithmetic(
                                            new EachAdd(
                                                [
                                                    new NumberArrayReturning(
                                                        new NumberField(
                                                            SampleDatabase.Orders.Entity,
                                                            SampleDatabase.Orders.Total
                                                        )
                                                    ),
                                                    new NumberReturning(
                                                        new NumberScalar(50)
                                                    ),
                                                ]
                                            )
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(150))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachStringEquality(
                                                new StringArrayReturning(
                                                    new StringField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Status
                                                    )
                                                ),
                                                new StringReturning(
                                                    new StringScalar("shipped")
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachLessThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(60))
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(o =>
                o.OrderTotal + 50 > 150
                && (o.OrderStatus == "shipped" || o.OrderTotal < 60)
            ),
            result.Count
        );
    }

    // 4-level: eachAnd(eachOr(eachNot(eachAnd(total > 90, status == "shipped")),
    //                          total >= 300),
    //                   eachNot(status == "cancelled"))
    [Fact]
    public void FourLevelEachAndOrNotTreeFiltersByCombinedCondition()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanArrayReturning a = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(90))
                )
            )
        );
        BooleanArrayReturning b = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("shipped"))
                )
            )
        );
        BooleanArrayReturning c = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(300))
                )
            )
        );
        BooleanArrayReturning d = new BooleanArrayReturning(
            new EachNotOperator(
                new BooleanArrayReturning(
                    new EachEquality(
                        new EachStringEquality(
                            new StringArrayReturning(
                                new StringField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.Status
                                )
                            ),
                            new StringReturning(new StringScalar("cancelled"))
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachNotOperator(
                                            new BooleanArrayReturning(
                                                new EachAndOperator([a, b])
                                            )
                                        )
                                    ),
                                    c,
                                ]
                            )
                        ),
                        d,
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.Count(o =>
                (!(o.OrderTotal > 90 && o.OrderStatus == "shipped") || o.OrderTotal >= 300)
                && o.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    // 5-level tree, AND-rooted:
    //   eachAnd(
    //     eachOr(eachNot(eachAnd(a, b)), c),
    //     eachOr(eachNot(d), eachAnd(e, f))
    //   )
    // where, per row:
    //   a = total > 100          b = status == "pending"
    //   c = total >= 300         d = status == "cancelled"
    //   e = total < 100          f = status == "shipped"
    //
    // Per-row derivation (order id / total / status):
    //   101 / 100.50 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T AND T = T.
    //   102 /  50.00 / pending   : a=F b=T -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   103 / 200.00 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   104 /  75.25 / cancelled : a=F b=F -> and=F -> not=T -> left=T.
    //                              d=T -> not=F. e=T f=F -> and=F.
    //                              right = F OR F = F. root = T AND F = F.
    //   105 / 300.00 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   106 / 100.50 / pending   : a=T b=T -> and=T -> not=F.
    //                              c=100.50>=300=F -> left = F OR F = F.
    //                              root = F AND (anything) = F.
    //
    // Kept: 101, 102, 103, 105. Removed: 104, 106.
    [Fact]
    public void FiveLevelAndRootedTreeMatchesRowByRowAgainstLinqPredicate()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanArrayReturning a = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(100))
                )
            )
        );
        BooleanArrayReturning b = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("pending"))
                )
            )
        );
        BooleanArrayReturning c = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThanOrEqual,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(300))
                )
            )
        );
        BooleanArrayReturning d = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("cancelled"))
                )
            )
        );
        BooleanArrayReturning e = new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachLessThan,
                    new NumberArrayReturning(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    new NumberReturning(new NumberScalar(100))
                )
            )
        );
        BooleanArrayReturning f = new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    new StringReturning(new StringScalar("shipped"))
                )
            )
        );

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

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator([leftBranch, rightBranch])
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    bool av = o.OrderTotal > 100;
                    bool bv = o.OrderStatus == "pending";
                    bool cv = o.OrderTotal >= 300;
                    bool dv = o.OrderStatus == "cancelled";
                    bool ev = o.OrderTotal < 100;
                    bool fv = o.OrderStatus == "shipped";
                    bool left = !(av && bv) || cv;
                    bool right = !dv || (ev && fv);
                    return left && right;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.Equal(4, expected.Length);
        Assert.Equal(expected, actual);
    }

    // Each* predicate over joined columns nested three levels deep:
    //   eachAnd(eachOr(user_age > 28, order_status == "pending"),
    //           eachNot(user_active == false))
    [Fact]
    public void EachTreeOverJoinedColumnsNestedInsideEachAndFiltersByBothSides()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachGreaterThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Users.Entity,
                                                        SampleDatabase.Users.Age
                                                    )
                                                ),
                                                new NumberReturning(new NumberScalar(28))
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachEquality(
                                            new EachStringEquality(
                                                new StringArrayReturning(
                                                    new StringField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Status
                                                    )
                                                ),
                                                new StringReturning(
                                                    new StringScalar("pending")
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachNotOperator(
                                new BooleanArrayReturning(
                                    new EachEquality(
                                        new EachBooleanEquality(
                                            new BooleanArrayReturning(
                                                new BooleanField(
                                                    SampleDatabase.Users.Entity,
                                                    SampleDatabase.Users.Active
                                                )
                                            ),
                                            new BooleanReturning(new BooleanScalar(false))
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            ),
            [
                new Join(
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
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(o =>
                {
                    UserRow user = db.UserRows.Single(u => u.UserId == o.OrderUserId);
                    return (user.UserAge > 28 || o.OrderStatus == "pending")
                        && user.UserActive;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < db.OrderRows.Count);
        Assert.Equal(expected, actual);
    }

    // Negative/empty: a structurally 3-level tree that is unsatisfiable by
    // every row (no order total is ever outside +-100000), regardless of the
    // second AND operand.
    [Fact]
    public void EachTreeThatIsUnsatisfiableForEveryRowReturnsEmptyResult()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachOrOperator(
                                [
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachGreaterThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(
                                                    new NumberScalar(100000)
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanArrayReturning(
                                        new EachComparison(
                                            new EachNumberComparison(
                                                EachComparisonOperator.EachLessThan,
                                                new NumberArrayReturning(
                                                    new NumberField(
                                                        SampleDatabase.Orders.Entity,
                                                        SampleDatabase.Orders.Total
                                                    )
                                                ),
                                                new NumberReturning(
                                                    new NumberScalar(-100000)
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("shipped"))
                                )
                            )
                        ),
                    ]
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // Negative/empty: a restrictive INNER JOIN that matches zero rows makes
    // every downstream row disappear, even when the each* WHERE tree (however
    // deeply nested) would otherwise evaluate true for every remaining row.
    [Fact]
    public void EachTreeOverRestrictiveJoinWithNoMatchesReturnsEmptyResult()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(0))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    ),
                                    new StringReturning(new StringScalar("nonexistent"))
                                )
                            )
                        ),
                    ]
                )
            ),
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachNumberComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Age
                                    )
                                ),
                                new NumberReturning(new NumberScalar(9999))
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
