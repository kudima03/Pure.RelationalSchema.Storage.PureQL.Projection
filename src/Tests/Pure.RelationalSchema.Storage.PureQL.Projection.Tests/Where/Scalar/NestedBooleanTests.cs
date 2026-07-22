using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Scalar;

// Nested single-value `and`/`or`/`not` composition over constant scalar
// equality/comparison leaves, at increasing depth (2 through 5 levels).
// Every leaf reduces to one value for the whole query, so - exactly like
// ScalarBooleanOpsTests - the composed tree is an all-or-nothing filter:
// it keeps every row when the tree evaluates to true, and removes every
// row when it evaluates to false. Each test spells out the leaf truth
// values inline so the expected keep-all/remove-all outcome can be
// hand-verified against the tree shape in the comment above it.
[Trait("Clause", "Where")]
[Trait("Feature", "NestedBoolean")]
public sealed class NestedBooleanTests
{
    // 2-level: and(a=true, or(b=false, c=true))
    // or(false, true) = true; and(true, true) = true.
    [Fact]
    public void ScalarAndOfTrueAndOrOfFalseTrueKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator(
                        [
                            new BooleanReturning(new BooleanScalar(true)),
                            new BooleanReturning(
                                new BooleanOperator(
                                    new OrOperator(
                                        [
                                            new BooleanReturning(
                                                new BooleanScalar(false)
                                            ),
                                            new BooleanReturning(
                                                new BooleanScalar(true)
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ]
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 2-level: not(and(a=true, b=false))
    // and(true, false) = false; not(false) = true.
    [Fact]
    public void ScalarNotOfAndOfTrueAndFalseKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new NotOperator(
                        new BooleanReturning(
                            new BooleanOperator(
                                new AndOperator(
                                    [
                                        new BooleanReturning(
                                            new BooleanScalar(true)
                                        ),
                                        new BooleanReturning(
                                            new BooleanScalar(false)
                                        ),
                                    ]
                                )
                            )
                        )
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 3-level: or(not(and(a, b)), c)
    // a = (5 > 3) = true; b = ("x" == "y") = false; c = false.
    // and(a, b) = false; not(false) = true; or(true, c) = true.
    [Fact]
    public void ScalarOrOfNotAndAtThreeLevelsKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    new NumberReturning(new NumberScalar(5)),
                    new NumberReturning(new NumberScalar(3))
                )
            )
        );
        BooleanReturning b = new BooleanReturning(
            new Equality(
                new SingleValueEquality(
                    new StringEquality(
                        new StringReturning(new StringScalar("x")),
                        new StringReturning(new StringScalar("y"))
                    )
                )
            )
        );
        BooleanReturning c = new BooleanReturning(new BooleanScalar(false));

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator(
                        [
                            new BooleanReturning(
                                new BooleanOperator(
                                    new NotOperator(
                                        new BooleanReturning(
                                            new BooleanOperator(
                                                new AndOperator([a, b])
                                            )
                                        )
                                    )
                                )
                            ),
                            c,
                        ]
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 3-level: and(not(or(a, b)), c)
    // a = (1 < 2) = true; b = ("x" == "y") = false; c = true.
    // or(a, b) = true; not(true) = false; and(false, c) = false.
    [Fact]
    public void ScalarAndOfNotOrAtThreeLevelsRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.LessThan,
                    new NumberReturning(new NumberScalar(1)),
                    new NumberReturning(new NumberScalar(2))
                )
            )
        );
        BooleanReturning b = new BooleanReturning(
            new Equality(
                new SingleValueEquality(
                    new StringEquality(
                        new StringReturning(new StringScalar("x")),
                        new StringReturning(new StringScalar("y"))
                    )
                )
            )
        );
        BooleanReturning c = new BooleanReturning(new BooleanScalar(true));

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator(
                        [
                            new BooleanReturning(
                                new BooleanOperator(
                                    new NotOperator(
                                        new BooleanReturning(
                                            new BooleanOperator(
                                                new OrOperator([a, b])
                                            )
                                        )
                                    )
                                )
                            ),
                            c,
                        ]
                    )
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

    // 3-level: not(not(not(a))), a = true.
    // not(true) = false; not(false) = true; not(true) = false.
    [Fact]
    public void ScalarTripleNotChainOfTrueRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new NotOperator(
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(
                                        new BooleanOperator(new NotOperator(a))
                                    )
                                )
                            )
                        )
                    )
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

    // 4-level: and(or(not(and(a, b)), c), d)
    // a = true, b = true -> and(a, b) = true -> not = false.
    // c = (2024-01-02 > 2024-01-01) = true -> or(false, true) = true.
    // d = (12:00 == 12:00) = true -> and(true, true) = true.
    [Fact]
    public void ScalarAndOrNotAtFourLevelsKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning c = new BooleanReturning(
            new Comparison(
                new DateComparison(
                    ComparisonOperator.GreaterThan,
                    new DateReturning(new DateScalar(new DateOnly(2024, 1, 2))),
                    new DateReturning(new DateScalar(new DateOnly(2024, 1, 1)))
                )
            )
        );
        TimeOnly noon = new TimeOnly(12, 0, 0);
        BooleanReturning d = new BooleanReturning(
            new Equality(
                new SingleValueEquality(
                    new TimeEquality(
                        new TimeReturning(new TimeScalar(noon)),
                        new TimeReturning(new TimeScalar(noon))
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator(
                        [
                            new BooleanReturning(
                                new BooleanOperator(
                                    new OrOperator(
                                        [
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(
                                                        new BooleanReturning(
                                                            new BooleanOperator(
                                                                new AndOperator(
                                                                    [a, b]
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            ),
                                            c,
                                        ]
                                    )
                                )
                            ),
                            d,
                        ]
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 4-level: or(and(not(or(a, b)), c), d)
    // a = false, b = false -> or(a, b) = false -> not = true.
    // c = (1 > 2) = false -> and(true, false) = false.
    // d = ("x" == "y") = false -> or(false, false) = false.
    [Fact]
    public void ScalarOrAndNotAtFourLevelsRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning c = new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    new NumberReturning(new NumberScalar(1)),
                    new NumberReturning(new NumberScalar(2))
                )
            )
        );
        BooleanReturning d = new BooleanReturning(
            new Equality(
                new SingleValueEquality(
                    new StringEquality(
                        new StringReturning(new StringScalar("x")),
                        new StringReturning(new StringScalar("y"))
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator(
                        [
                            new BooleanReturning(
                                new BooleanOperator(
                                    new AndOperator(
                                        [
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(
                                                        new BooleanReturning(
                                                            new BooleanOperator(
                                                                new OrOperator(
                                                                    [a, b]
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            ),
                                            c,
                                        ]
                                    )
                                )
                            ),
                            d,
                        ]
                    )
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

    // 4-level: not(not(not(not(a)))), a = true.
    // Four negations of true: false, true, false, true.
    [Fact]
    public void ScalarQuadrupleNotChainOfTrueKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new NotOperator(
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(
                                        new BooleanOperator(
                                            new NotOperator(
                                                new BooleanReturning(
                                                    new BooleanOperator(
                                                        new NotOperator(a)
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 5-level, AND-rooted:
    //   and(or(not(and(a, b)), c), or(not(d), and(e, f)))
    // a = true, b = false, c = true, d = false, e = true, f = true.
    // Left branch:  and(a, b) = false -> not = true -> or(true, c) = true.
    // Right branch: not(d) = true -> or(true, and(e, f)) = true.
    // and(true, true) = true.
    [Fact]
    public void ScalarFiveLevelAndRootedTreeKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

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
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(d))
                        ),
                        new BooleanReturning(
                            new BooleanOperator(new AndOperator([e, f]))
                        ),
                    ]
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator([leftBranch, rightBranch])
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // 5-level, OR-rooted:
    //   or(and(not(or(a, b)), c), and(not(d), or(e, f)))
    // a = false, b = false, c = false, d = true, e = false, f = false.
    // Left branch:  or(a, b) = false -> not = true -> and(true, c) = false.
    // Right branch: not(d) = false -> and(false, or(e, f)) = false.
    // or(false, false) = false.
    [Fact]
    public void ScalarFiveLevelOrRootedTreeRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning a = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning c = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning d = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning e = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning f = new BooleanReturning(new BooleanScalar(false));

        BooleanReturning leftBranch = new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(
                                        new BooleanOperator(new OrOperator([a, b]))
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
                new AndOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(new NotOperator(d))
                        ),
                        new BooleanReturning(
                            new BooleanOperator(new OrOperator([e, f]))
                        ),
                    ]
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(new OrOperator([leftBranch, rightBranch]))
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

    // De Morgan cross-check at depth 3: not(and(a, b)) == or(not(a), not(b)).
    // Both trees are embedded as the second operand of and(x, ...) so the
    // equivalence is exercised inside a larger composite, not in isolation.
    // x = true, a = true, b = false.
    //   TreeA: and(x, not(and(a, b)))
    //   TreeB: and(x, or(not(a), not(b)))
    // and(a, b) = false -> not = true; or(not(a)=false, not(b)=true) = true.
    // Both inner values are true, so and(x, ...) = true for both shapes.
    [Fact]
    public void DeMorganNotAndEquivalesOrOfNotsAtThreeLevelsProduceSameResult()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning x = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(false));

        Query queryA = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator(
                        [
                            x,
                            new BooleanReturning(
                                new BooleanOperator(
                                    new NotOperator(
                                        new BooleanReturning(
                                            new BooleanOperator(
                                                new AndOperator([a, b])
                                            )
                                        )
                                    )
                                )
                            ),
                        ]
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        Query queryB = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator(
                        [
                            x,
                            new BooleanReturning(
                                new BooleanOperator(
                                    new OrOperator(
                                        [
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(a)
                                                )
                                            ),
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(b)
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ]
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryB)
        );

        Assert.Equal(db.OrderRows.Count, resultA.Count);
        Assert.Equal(db.OrderRows.Count, resultB.Count);
    }

    // De Morgan cross-check at depth 4: not(or(a, b)) == and(not(a), not(b)),
    // each embedded as the second operand of or(y, ...).
    // y = false, a = true, b = false.
    //   TreeA: or(y, not(or(a, b)))
    //   TreeB: or(y, and(not(a), not(b)))
    // or(a, b) = true -> not = false; and(not(a)=false, not(b)=true) = false.
    // Both inner values are false, so or(y, ...) = false for both shapes.
    [Fact]
    public void DeMorganNotOrEquivalesAndOfNotsAtFourLevelsProduceSameResult()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning y = new BooleanReturning(new BooleanScalar(false));
        BooleanReturning a = new BooleanReturning(new BooleanScalar(true));
        BooleanReturning b = new BooleanReturning(new BooleanScalar(false));

        Query queryA = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator(
                        [
                            y,
                            new BooleanReturning(
                                new BooleanOperator(
                                    new NotOperator(
                                        new BooleanReturning(
                                            new BooleanOperator(
                                                new OrOperator([a, b])
                                            )
                                        )
                                    )
                                )
                            ),
                        ]
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        Query queryB = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator(
                        [
                            y,
                            new BooleanReturning(
                                new BooleanOperator(
                                    new AndOperator(
                                        [
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(a)
                                                )
                                            ),
                                            new BooleanReturning(
                                                new BooleanOperator(
                                                    new NotOperator(b)
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ]
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(db.Datasets, queryB)
        );

        Assert.Equal(0, resultA.Count);
        Assert.Equal(0, resultB.Count);
    }

    // Boundary, always-true: and(or(false, true), and(true, not(false)),
    //                             or(not(false), and(true, true)))
    // or(false, true) = true.
    // and(true, not(false)=true) = true.
    // or(not(false)=true, and(true, true)=true) = true.
    // and(true, true, true) = true -> full table.
    [Fact]
    public void DeeplyNestedTreeEvaluatingAlwaysTrueKeepsFullTable()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning branch1 = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        new BooleanReturning(new BooleanScalar(false)),
                        new BooleanReturning(new BooleanScalar(true)),
                    ]
                )
            )
        );
        BooleanReturning branch2 = new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(new BooleanScalar(true)),
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(new BooleanScalar(false))
                                )
                            )
                        ),
                    ]
                )
            )
        );
        BooleanReturning branch3 = new BooleanReturning(
            new BooleanOperator(
                new OrOperator(
                    [
                        new BooleanReturning(
                            new BooleanOperator(
                                new NotOperator(
                                    new BooleanReturning(new BooleanScalar(false))
                                )
                            )
                        ),
                        new BooleanReturning(
                            new BooleanOperator(
                                new AndOperator(
                                    [
                                        new BooleanReturning(
                                            new BooleanScalar(true)
                                        ),
                                        new BooleanReturning(
                                            new BooleanScalar(true)
                                        ),
                                    ]
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator([branch1, branch2, branch3])
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // Boundary, always-false: or(and(true, false), and(false, true),
    //                            not(or(true, true)))
    // and(true, false) = false.
    // and(false, true) = false.
    // or(true, true) = true -> not(true) = false.
    // or(false, false, false) = false -> empty result.
    [Fact]
    public void DeeplyNestedTreeEvaluatingAlwaysFalseProducesEmptyResult()
    {
        SampleDatabase db = new SampleDatabase();

        BooleanReturning branch1 = new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(new BooleanScalar(true)),
                        new BooleanReturning(new BooleanScalar(false)),
                    ]
                )
            )
        );
        BooleanReturning branch2 = new BooleanReturning(
            new BooleanOperator(
                new AndOperator(
                    [
                        new BooleanReturning(new BooleanScalar(false)),
                        new BooleanReturning(new BooleanScalar(true)),
                    ]
                )
            )
        );
        BooleanReturning branch3 = new BooleanReturning(
            new BooleanOperator(
                new NotOperator(
                    new BooleanReturning(
                        new BooleanOperator(
                            new OrOperator(
                                [
                                    new BooleanReturning(new BooleanScalar(true)),
                                    new BooleanReturning(new BooleanScalar(true)),
                                ]
                            )
                        )
                    )
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(new OrOperator([branch1, branch2, branch3]))
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

    // A single-value Arithmetic whose operands are all literal constants now
    // evaluates once, outside a per-row (each*) context, through the scalar
    // WHERE-predicate entry point - distinct from the per-row EachArithmetic
    // path exercised elsewhere in this suite. `where (1 + 2) > 0` folds to
    // `3 > 0`, a constant-true predicate matching every row.
    [Fact]
    public void ScalarArithmeticInComparisonPredicateMatchesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new Arithmetic(
                                new Add(
                                    [
                                        new NumberReturning(new NumberScalar(1)),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
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

        Assert.Equal(db.OrderRows.Count, result.Count);
    }
}
