using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
                    new UuidField("schema_with_foreign_keys.orders", "order_id")
                )
            )
        );
    }

    // Two identical literal arrays: SequenceEqual is true, so the predicate
    // (evaluated once, applied to the whole result) keeps every row.
    [Fact]
    public void WholeTimeArrayEqualityOfTwoEqualLiteralArraysKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    // Two literal arrays with the same length but a different order:
    // SequenceEqual is false (order-sensitive), so every row is removed.
    [Fact]
    public void WholeTimeArrayEqualityOfTwoReorderedLiteralArraysRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
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
            new PureQLProjection(datasets, query)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        TimeOnly[] reversedShiftStarts =
        [
            .. userRows.Select(user => user.ShiftStart).Reverse(),
        ];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new TimeArrayEquality(
                            new TimeArrayReturning(
                                new TimeField(
                                    "schema_with_foreign_keys.users",
                                    "shift_start"
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
            new PureQLProjection(datasets, query)
        ));
    }

    // Mirrors the test above with the operand order swapped (literal array
    // on the left, field on the right) to cover the other arm of
    // BuildContainmentEquality's field-vs-literal handling.
    [Fact]
    public void WholeTimeArrayEqualityOfLiteralAgainstFieldFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        TimeOnly[] reversedShiftStarts =
        [
            .. userRows.Select(user => user.ShiftStart).Reverse(),
        ];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
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
                                    "schema_with_foreign_keys.users",
                                    "shift_start"
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
            new PureQLProjection(datasets, query)
        ));
    }
}
