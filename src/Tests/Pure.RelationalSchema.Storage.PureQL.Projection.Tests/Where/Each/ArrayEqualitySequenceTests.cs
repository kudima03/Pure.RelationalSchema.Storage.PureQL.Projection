using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Whole-array `equal` (`Equality` -> `ArrayEquality`, distinct from the
// per-row `eachEqual` family exercised elsewhere in Where/Each) is a
// single-value predicate: it evaluates once for the whole query rather than
// per row, exactly like the scalar `and`/`or`/`not` family in
// Where/Scalar/NestedBooleanTests. Reading WhereExpressionBuilder.cs
// (BuildContainmentEquality) confirms two distinct operand shapes:
//   - literal array vs. literal array -> true SequenceEqual (order-sensitive,
//     evaluated once) - implemented and correct.
//   - field vs. literal array -> per SQL result-set semantics this should
//     also be a single order-sensitive sequence comparison of the column's
//     values (in row order) against the literal, evaluated once for the
//     whole query. Implementing that requires the full materialized row
//     sequence before the row-scoped predicate is built, which is out of
//     scope for issue #114's fix. Rather than silently returning wrong
//     results via per-row Enumerable.Contains ("IN") membership (the
//     original bug), the translator now fails fast with
//     NotSupportedException for this operand shape - see
//     WholeArrayEqualityOfFieldAgainstLiteralFailsFast below.
[Trait("Clause", "Where")]
[Trait("Feature", "ArrayEqualitySequence")]
public sealed class ArrayEqualitySequenceTests
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

    // Two identical literal arrays: SequenceEqual is true, so the predicate
    // (evaluated once, applied to the whole result) keeps every row.
    [Fact]
    public void WholeArrayEqualityOfTwoEqualLiteralArraysKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberArrayScalar([1, 2, 3])
                            ),
                            new NumberArrayReturning(
                                new NumberArrayScalar([1, 2, 3])
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

    // Two literal arrays with the same length but a different order:
    // SequenceEqual is false (order-sensitive), so every row is removed.
    [Fact]
    public void WholeArrayEqualityOfTwoDifferentlyOrderedLiteralArraysRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberArrayScalar([1, 2, 3])
                            ),
                            new NumberArrayReturning(
                                new NumberArrayScalar([3, 2, 1])
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

    // Field vs. literal-array whole-array `equal` (either operand order) is
    // not implemented as true sequence equality (see the header comment and
    // issue #114) - the translator fails fast with NotSupportedException
    // instead of silently falling back to per-row Enumerable.Contains ("IN")
    // membership, which would give a wrong answer here: reversing the
    // literal doesn't change set membership, so a membership-based
    // implementation would wrongly keep every row.
    [Fact]
    public void WholeArrayEqualityOfFieldAgainstLiteralFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        double[] reversedTotals =
        [
            .. db.OrderRows.Select(order => order.OrderTotal).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.Total
                                )
                            ),
                            new NumberArrayReturning(
                                new NumberArrayScalar(reversedTotals)
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

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // Mirrors the test above with the operand order swapped (literal array
    // on the left, field on the right) to cover the other arm of
    // BuildContainmentEquality's field-vs-literal handling.
    [Fact]
    public void WholeArrayEqualityOfLiteralAgainstFieldFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        double[] reversedTotals =
        [
            .. db.OrderRows.Select(order => order.OrderTotal).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberArrayScalar(reversedTotals)
                            ),
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }
}
