using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Time arm of the whole-array `equal` (`Equality` -> `ArrayEquality`)
// sequence-equality predicate. See ArrayEqualitySequenceTests.cs (the Number
// arm) for the full rationale: this is a single-value predicate evaluated
// once for the whole query, distinct from the per-row `eachEqual` family, and
// field-vs-literal operand shapes fail fast with NotSupportedException per
// the ratified issue #114 contract rather than silently degrading to per-row
// Enumerable.Contains ("IN") membership.
[Trait("Clause", "Where")]
[Trait("Feature", "ArrayEqualitySequence")]
public sealed class TimeArrayEqualitySequenceTests
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
    public void WholeTimeArrayEqualityOfTwoEqualLiteralArraysKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new TimeArrayEquality(
                            new TimeArrayReturning(
                                new TimeArrayScalar(
                                    [
                                        new TimeOnly(8, 0, 0),
                                        new TimeOnly(9, 0, 0),
                                        new TimeOnly(10, 0, 0),
                                    ]
                                )
                            ),
                            new TimeArrayReturning(
                                new TimeArrayScalar(
                                    [
                                        new TimeOnly(8, 0, 0),
                                        new TimeOnly(9, 0, 0),
                                        new TimeOnly(10, 0, 0),
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

    // Two literal arrays with the same length but a different order:
    // SequenceEqual is false (order-sensitive), so every row is removed.
    [Fact]
    public void WholeTimeArrayEqualityOfTwoReorderedLiteralArraysRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new TimeArrayEquality(
                            new TimeArrayReturning(
                                new TimeArrayScalar(
                                    [
                                        new TimeOnly(8, 0, 0),
                                        new TimeOnly(9, 0, 0),
                                        new TimeOnly(10, 0, 0),
                                    ]
                                )
                            ),
                            new TimeArrayReturning(
                                new TimeArrayScalar(
                                    [
                                        new TimeOnly(10, 0, 0),
                                        new TimeOnly(9, 0, 0),
                                        new TimeOnly(8, 0, 0),
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

        Assert.Equal(0, result.Count);
    }

    // Field vs. literal-array whole-array `equal` (either operand order) is
    // not implemented as true sequence equality (see issue #114) - the
    // translator fails fast with NotSupportedException instead of silently
    // falling back to per-row Enumerable.Contains ("IN") membership, which
    // would give a wrong answer here: reversing the literal doesn't change
    // set membership, so a membership-based implementation would wrongly
    // keep every row.
    [Fact]
    public void WholeTimeArrayEqualityOfFieldAgainstLiteralFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        TimeOnly[] reversedShiftStarts =
        [
            .. db.UserRows.Select(user => user.ShiftStart).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new TimeArrayEquality(
                            new TimeArrayReturning(
                                new TimeField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.ShiftStart
                                )
                            ),
                            new TimeArrayReturning(
                                new TimeArrayScalar(reversedShiftStarts)
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
    public void WholeTimeArrayEqualityOfLiteralAgainstFieldFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        TimeOnly[] reversedShiftStarts =
        [
            .. db.UserRows.Select(user => user.ShiftStart).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new TimeArrayEquality(
                            new TimeArrayReturning(
                                new TimeArrayScalar(reversedShiftStarts)
                            ),
                            new TimeArrayReturning(
                                new TimeField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.ShiftStart
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
