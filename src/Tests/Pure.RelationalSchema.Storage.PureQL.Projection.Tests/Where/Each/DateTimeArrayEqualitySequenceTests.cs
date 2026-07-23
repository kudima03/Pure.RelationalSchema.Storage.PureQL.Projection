using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// DateTime arm of the whole-array `equal` (`Equality` -> `ArrayEquality`)
// sequence-equality predicate. See ArrayEqualitySequenceTests.cs (the Number
// arm) for the full rationale: this is a single-value predicate evaluated
// once for the whole query, distinct from the per-row `eachEqual` family, and
// field-vs-literal operand shapes fail fast with NotSupportedException per
// the ratified issue #114 contract rather than silently degrading to per-row
// Enumerable.Contains ("IN") membership.
[Trait("Clause", "Where")]
[Trait("Feature", "ArrayEqualitySequence")]
public sealed class DateTimeArrayEqualitySequenceTests
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
    public void WholeDateTimeArrayEqualityOfTwoEqualLiteralArraysKeepsEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateTimeArrayEquality(
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(
                                    [
                                        new DateTime(2024, 1, 1, 8, 0, 0),
                                        new DateTime(2024, 2, 1, 9, 0, 0),
                                        new DateTime(2024, 3, 1, 10, 0, 0),
                                    ]
                                )
                            ),
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(
                                    [
                                        new DateTime(2024, 1, 1, 8, 0, 0),
                                        new DateTime(2024, 2, 1, 9, 0, 0),
                                        new DateTime(2024, 3, 1, 10, 0, 0),
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
    public void WholeDateTimeArrayEqualityOfTwoReorderedLiteralArraysRemovesEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateTimeArrayEquality(
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(
                                    [
                                        new DateTime(2024, 1, 1, 8, 0, 0),
                                        new DateTime(2024, 2, 1, 9, 0, 0),
                                        new DateTime(2024, 3, 1, 10, 0, 0),
                                    ]
                                )
                            ),
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(
                                    [
                                        new DateTime(2024, 3, 1, 10, 0, 0),
                                        new DateTime(2024, 2, 1, 9, 0, 0),
                                        new DateTime(2024, 1, 1, 8, 0, 0),
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
    public void WholeDateTimeArrayEqualityOfFieldAgainstLiteralFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        DateTime[] reversedPlacedAt =
        [
            .. db.OrderRows.Select(order => order.PlacedAt).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateTimeArrayEquality(
                            new DateTimeArrayReturning(
                                new DateTimeField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.PlacedAt
                                )
                            ),
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(reversedPlacedAt)
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
    public void WholeDateTimeArrayEqualityOfLiteralAgainstFieldFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        DateTime[] reversedPlacedAt =
        [
            .. db.OrderRows.Select(order => order.PlacedAt).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateTimeArrayEquality(
                            new DateTimeArrayReturning(
                                new DateTimeArrayScalar(reversedPlacedAt)
                            ),
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
