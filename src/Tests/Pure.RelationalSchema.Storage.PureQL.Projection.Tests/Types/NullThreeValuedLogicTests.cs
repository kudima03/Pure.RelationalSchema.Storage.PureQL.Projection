using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Types;

// SQL three-valued logic (3VL) threaded through operator combinations that
// Types/NullSemanticsTests.cs does not already cover: each-comparison and
// each-arithmetic dropping NULL rows, negation of a NULL-cell equality,
// HAVING over a group whose members mix NULL and non-NULL Score, and
// string/temporal aggregate NULL-ignoring sourced from LEFT JOIN padding
// (JoinApplicator.Pad pads the unmatched side: string columns read back as
// "", every other typed column reads back as null - see
// CellValueExtractor and Semantics/README.md "Outer-join null extension").
// Every expectation below is computed independently from the ground-truth
// record lists under SQL semantics: a predicate comparison against NULL is
// unknown (row excluded); arithmetic with a NULL operand is NULL (excluded);
// aggregates ignore NULL inputs. Where the translator's actual behavior
// diverges from that oracle, the test is written to the SQL-correct
// expectation and disabled with a KnownGap skip rather than asserting the
// divergence as correct.
[Trait("Clause", "Types")]
[Trait("Feature", "NullThreeValuedLogic")]
public sealed class NullThreeValuedLogicTests
{
    private static Join UsersToOrdersLeftJoin()
    {
        return new Join(
            JoinType.Left,
            SampleDatabase.Orders.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                )
            )
        );
    }

    // ===== each-comparison over Users.Score (all 4 range operators) =====

    // WHERE each user_score > 20: Bob/Dan's NULL Score makes the comparison
    // unknown, so they are excluded exactly like Eve (Score = 10, a real
    // mismatch), never treated as satisfying or as an error.
    [Fact]
    public void EachNumberGreaterThanExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
                            )
                        ),
                        new NumberReturning(new NumberScalar(20))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score > 20)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score >= 28: same NULL-exclusion contract as above,
    // exercised against the inclusive operator.
    [Fact]
    public void EachNumberGreaterThanOrEqualExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThanOrEqual,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
                            )
                        ),
                        new NumberReturning(new NumberScalar(28))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score >= 28)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score < 29: Ann/Cara (Score = 30) fail the real
    // comparison; Bob/Dan fail because NULL is unknown, not because 30 was
    // ever evaluated. Both reasons must produce the same exclusion.
    [Fact]
    public void EachNumberLessThanExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachLessThan,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
                            )
                        ),
                        new NumberReturning(new NumberScalar(29))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score < 29)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score <= 10: only Eve's real Score qualifies; Bob/Dan
    // never qualify via their NULL cell however permissive the threshold.
    [Fact]
    public void EachNumberLessThanOrEqualExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachLessThanOrEqual,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
                            )
                        ),
                        new NumberReturning(new NumberScalar(10))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score <= 10)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== each-arithmetic with a NULL Score operand =====

    // WHERE each (user_score + 1) > -1000: the threshold is satisfied by
    // every real Score value, so only NULL propagation through eachAdd (not
    // a real comparison failure) can remove a row. Bob/Dan's NULL Score
    // makes the sum NULL, which is excluded rather than treated as
    // satisfying an always-true-looking threshold.
    [Fact]
    public void EachAddWithNullScoreOperandExcludesRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
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
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Score
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(1)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(-1000))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score - 10) > 15: Score - 10 > 15 <=> Score > 25, a
    // real partial match (Ann/Cara/Fay, not Eve); Bob/Dan's NULL Score
    // yields a NULL difference, excluded regardless of the threshold.
    [Fact]
    public void EachSubtractWithNullScoreOperandExcludesRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachSubtract(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Score
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(10)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(15))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score - 10 > 15)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score * 2) > 50: Score * 2 > 50 <=> Score > 25 (same
    // real partial match as above via a different operator); Bob/Dan's NULL
    // Score makes the product NULL, excluded.
    [Fact]
    public void EachMultiplyWithNullScoreOperandExcludesRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachMultiply(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Score
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(50))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score * 2 > 50)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score / 2) > -1000: an always-true-for-reals
    // threshold, isolating NULL propagation through eachDivide the same way
    // as the eachAdd case above. Bob/Dan's NULL Score divided by 2 is NULL,
    // excluded rather than satisfying the permissive threshold.
    [Fact]
    public void EachDivideWithNullScoreOperandExcludesRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachDivide(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Score
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(-1000))
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

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== Negating a NULL-cell equality (scalar and each family) =====

    // WHERE NOT(user_age = user_score): field-vs-field equality is the only
    // shape through which the scalar (non-each) BooleanReturning family can
    // reference a row's cells at all (Comparisons.NumberComparison etc. take
    // only single-value operands, never a field - see WhereExpressionBuilder
    // .BuildNumberReturningAsExpr). Under SQL 3VL, NOT(unknown) is still
    // unknown, so Bob/Dan's NULL-Score comparison must stay excluded after
    // negation, exactly as it was before negation; only Eve's real mismatch
    // (Age 25 != Score 10, a genuine false) should flip to true and appear.
    // The translator instead compiles the field-vs-field equality with C#'s
    // lifted nullable `==`, which yields `false` (not "unknown") for a NULL
    // operand, so `Expression.Not` flips that `false` to `true` and Bob/Dan
    // incorrectly reappear in the negated result. KnownGap: candidate bug -
    // NOT() over a NULL-cell scalar equality does not preserve SQL 3VL.
    [Fact(
        Skip = "KnownGap: NOT() over a NULL-cell field equality flips "
            + "SQL's unknown to true instead of keeping it excluded - the "
            + "translator lifts null-cell equality to a plain C# false "
            + "(not 3VL unknown), so negating it wrongly re-admits the "
            + "NULL rows (Bob, Dan) that a correct NOT(unknown) would "
            + "still exclude."
    )]
    [Trait("Status", "KnownGap")]
    public void NotOfScalarFieldEqualityStillExcludesNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new BooleanOperator(
                    new NotOperator(
                        new BooleanReturning(
                            new Equality(
                                new ArrayEquality(
                                    new NumberArrayEquality(
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Age
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Score
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

        // SQL-correct: NOT(unknown) stays excluded for Bob/Dan; only Eve's
        // genuine mismatch (25 != 10) flips from false to true.
        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score != user.UserAge)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE eachNot(each user_score = 30): the each-family analogue of the
    // scalar NOT bug above. SQL 3VL says NOT(unknown) is still unknown, so
    // Bob/Dan (NULL Score) must stay excluded; only Eve/Fay's genuine
    // mismatches (10 != 30, 28 != 30) should flip from false to true.
    // Empirically the translator's eachNot re-admits Bob/Dan the same way
    // NotOperator does for the scalar family. KnownGap: candidate bug.
    [Fact(
        Skip = "KnownGap: eachNot() over a per-row NULL-cell eachEqual "
            + "flips SQL's unknown to true instead of keeping it excluded, "
            + "the same divergence as NotOfScalarFieldEqualityStillExcludes"
            + "NullRows but in the each* family - Bob/Dan wrongly reappear."
    )]
    [Trait("Status", "KnownGap")]
    public void EachNotOfEachEqualityStillExcludesNullScoreRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachNumberEquality(
                                new NumberArrayReturning(
                                    new NumberField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Score
                                    )
                                ),
                                new NumberReturning(new NumberScalar(30))
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

        // SQL-correct: NOT(unknown) stays excluded for Bob/Dan; Eve/Fay's
        // genuine mismatches (10 != 30, 28 != 30) flip from false to true.
        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue && user.Score != 30)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== HAVING over a group mixing NULL and non-NULL Score =====

    // GROUP BY user_active HAVING avg(user_score) > 15: the true-group
    // (Ann 30, Cara 30, Dan NULL, Fay 28) mixes a NULL Score with three real
    // ones; avg must ignore Dan's NULL and fold only the three real values,
    // clearing the HAVING threshold. The false-group (Bob NULL, Eve 10) has
    // only one real Score (10), averaging to 10 and failing the threshold,
    // so exactly one group survives.
    [Fact]
    public void HavingAverageIgnoresNullScoreAcrossMixedGroup()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new AverageNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Score
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "avg_score"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Active
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new NumberAggregate(
                                new AverageNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Score
                                        )
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(15))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expectedAverage = db.UserRows
            .Where(user => user.UserActive)
            .Select(user => user.Score)
            .OfType<double>()
            .Average();

        Assert.Equal(1, result.Count);
        Assert.Equal("True", result.Row(0)[SampleDatabase.Users.Active]);
        Assert.Equal(expectedAverage, result.Row(0).Double("avg_score"));
    }

    // ===== Other-type aggregate NULL-ignoring via LEFT JOIN padding =====

    // SELECT min(order_placed_on) after users LEFT JOIN orders: Eve and Fay
    // have no orders, so their joined row is padded with a NULL PlacedOn
    // (JoinApplicator.Pad -> empty cell -> CellValueExtractor.GetDateOnly
    // Value returns null for empty text). min() must ignore those two
    // padded NULLs and fold only the six real order dates.
    [Fact]
    public void LeftJoinMinPlacedOnIgnoresPaddedNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MinDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "min_placed_on"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        DateOnly expected = db.OrderRows.Min(order => order.PlacedOn);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Date("min_placed_on"));
    }

    // SELECT max(placed_at) after users LEFT JOIN orders: same padded-NULL
    // source as above but for the datetime-typed PlacedAt column and the
    // max direction, so both aggregate directions are covered for a
    // temporal type sourced purely from join padding.
    [Fact]
    public void LeftJoinMaxPlacedAtIgnoresPaddedNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new DateTimeReturning(
                            new DateTimeAggregate(
                                new MaxDateTime(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedAt
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_placed_at"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        DateTime expected = db.OrderRows.Max(order => order.PlacedAt);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).DateTime("max_placed_at"));
    }

    // SELECT max(order_status) after users LEFT JOIN orders: string columns
    // pad to "" (empty text), not a true null (CellValueExtractor.GetText
    // Value returns the raw stored text directly, unlike the other typed
    // getters which map empty text to null). "" ordinally precedes every
    // real status, so it never wins a MAX fold - the padded rows are
    // harmless here even though they are not truly excluded, so max already
    // matches the SQL-correct answer.
    [Fact]
    public void LeftJoinMaxStatusIgnoresPaddedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MaxString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_status"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string expected = db.OrderRows.Max(order => order.OrderStatus)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0)["max_status"]);
    }

    // SELECT min(order_status) after users LEFT JOIN orders: unlike max
    // above, an empty-string pad always wins an ordinal MIN fold, so the
    // padded rows for Eve/Fay's unmatched join are not harmless here - SQL
    // (ignoring the two NULL/padded rows) would give "cancelled" (the
    // ordinal minimum of the six real statuses), but the translator's
    // FoldString only filters true C# nulls via `.OfType<string>()`, and a
    // padded "" is a real (non-null) empty string, so it wrongly survives
    // and wins the fold. KnownGap: candidate bug - the empty-string pad
    // participates in string MIN instead of being ignored like a NULL.
    [Fact(
        Skip = "KnownGap: string MIN over a LEFT JOIN's padded unmatched "
            + "rows wrongly folds in the padded \"\" (JoinApplicator.Pad's "
            + "empty cell reads back as a real, non-null empty string via "
            + "CellValueExtractor.GetTextValue, not null), so \"\" - which "
            + "ordinally precedes every real status - always wins MIN "
            + "instead of being ignored the way a NULL must be."
    )]
    [Trait("Status", "KnownGap")]
    public void LeftJoinMinStatusIgnoresPaddedNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
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
                        )
                    ),
                    "min_status"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string expected = db.OrderRows.Min(order => order.OrderStatus)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0)["min_status"]);
    }

    // count(order_total) after users LEFT JOIN orders: Total is a double
    // column, so a padded row's cell reads back as a true null (unlike the
    // string case above), and count() correctly counts only the six
    // matched rows, ignoring Eve/Fay's two padded rows entirely.
    [Fact]
    public void LeftJoinCountOfNumericColumnIgnoresPaddedNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "total_count"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("total_count"));
    }

    // count(order_status) after users LEFT JOIN orders: the string-family
    // counterpart of the numeric count above. SQL's count(column) ignores
    // NULL, so it should count only the six matched rows (complementing
    // issue #140's NULL-exclusion test with a join-sourced NULL) - but
    // AggregateEvaluator.HasValueSelector's string branch treats "selector
    // is not null" as presence, and a padded row's string selector returns
    // a real (non-null) "", so both padded rows are wrongly counted as
    // present. KnownGap: candidate bug, the same root cause (empty-string
    // pad vs. true null) as the string MIN divergence above.
    [Fact(
        Skip = "KnownGap: count() over a LEFT JOIN's padded string column "
            + "wrongly counts the padded \"\" rows as present - Aggregate"
            + "Evaluator.HasValueSelector's string branch treats any "
            + "non-null string (including the padded empty string, which "
            + "is not a C# null) as a counted value, so count(order_status)"
            + " returns 8 instead of the SQL-correct 6 matched rows."
    )]
    [Trait("Status", "KnownGap")]
    public void LeftJoinCountOfStringColumnIgnoresPaddedNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "status_count"
                ),
            ],
            where: null,
            [UsersToOrdersLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("status_count"));
    }
}
