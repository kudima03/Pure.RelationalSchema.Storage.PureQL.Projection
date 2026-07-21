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
// (BuildContainmentEquality) confirms two distinct, already-implemented
// paths depending on operand shape:
//   - literal array vs. literal array -> true SequenceEqual (order-sensitive,
//     evaluated once).
//   - field vs. literal array -> per-row Enumerable.Contains membership
//     (order-insensitive "IN"), which is the genuine #98/#72 KnownGap: SQL
//     result-set semantics say "column-as-a-whole-array equal literal-array"
//     should also be an order-sensitive sequence comparison, not membership.
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

    // KnownGap: `order_total equal [reverse of the six order totals]` is,
    // per SQL result-set/sequence-equality semantics, a single whole-array
    // comparison - the column's values in row order do not equal the
    // reversed literal, so the (once-evaluated) predicate should be false
    // and the whole result set empty. The translator instead evaluates this
    // shape as a per-row membership check (`order_total IN (...)`): since
    // reversing doesn't change set membership, every row's own total is
    // still present somewhere in the literal, so all six rows are kept.
    [Fact(
        Skip = "KnownGap: whole-array `equal` between a field and a literal "
            + "array evaluates as per-row membership (Enumerable.Contains) "
            + "instead of a single order-sensitive sequence-equality "
            + "comparison across the whole column; see "
            + "Semantics/README.md and epic #72 decision 7."
    )]
    [Trait("Status", "KnownGap")]
    public void WholeArrayEqualityOfFieldAgainstReversedLiteralRemovesEveryRow()
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

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // Spec-correct (sequence equality): the column's total values, in
        // row order, are not equal to the reversed literal, so the
        // whole-query predicate is false and nothing is kept.
        Assert.Equal(0, result.Count);
    }
}
