using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
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

// Date arm of the whole-array `equal` (`Equality` -> `ArrayEquality`)
// sequence-equality predicate. See ArrayEqualitySequenceTests.cs (the Number
// arm) for the full rationale: this is a single-value predicate evaluated
// once for the whole query, distinct from the per-row `eachEqual` family, and
// field-vs-literal operand shapes fail fast with NotSupportedException per
// the ratified issue #114 contract rather than silently degrading to per-row
// Enumerable.Contains ("IN") membership.
[Trait("Clause", "Where")]
[Trait("Feature", "ArrayEqualitySequence")]
public sealed class DateArrayEqualitySequenceTests
{
    private static SelectExpression OrderIdSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new UuidArrayReturning(
                    new UuidField(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue, new OrderIdColumn().Name.TextValue)
                )
            )
        );
    }

    // Two identical literal arrays: SequenceEqual is true, so the predicate
    // (evaluated once, applied to the whole result) keeps every row.
    [Fact]
    public void WholeDateArrayEqualityOfTwoEqualLiteralArraysKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateArrayEquality(
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 1, 1),
                                        new DateOnly(2024, 2, 1),
                                        new DateOnly(2024, 3, 1),
                                    ]
                                )
                            ),
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 1, 1),
                                        new DateOnly(2024, 2, 1),
                                        new DateOnly(2024, 3, 1),
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
    public void WholeDateArrayEqualityOfTwoReorderedLiteralArraysRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateArrayEquality(
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 1, 1),
                                        new DateOnly(2024, 2, 1),
                                        new DateOnly(2024, 3, 1),
                                    ]
                                )
                            ),
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 3, 1),
                                        new DateOnly(2024, 2, 1),
                                        new DateOnly(2024, 1, 1),
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

    // Two literal arrays of different lengths: SequenceEqual is false
    // (length mismatch alone rules out equality), so every row is removed.
    [Fact]
    public void WholeDateArrayEqualityOfDifferentLengthLiteralArraysRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateArrayEquality(
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 1, 1),
                                        new DateOnly(2024, 2, 1),
                                        new DateOnly(2024, 3, 1),
                                    ]
                                )
                            ),
                            new DateArrayReturning(
                                new DateArrayScalar(
                                    [
                                        new DateOnly(2024, 1, 1),
                                        new DateOnly(2024, 2, 1),
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
    public void WholeDateArrayEqualityOfFieldAgainstLiteralFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        DateOnly[] reversedPlacedOn =
        [
            .. orderRows.Select(order => order.PlacedOn).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateArrayEquality(
                            new DateArrayReturning(
                                new DateField(
                                    new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                    new PlacedOnColumn().Name.TextValue
                                )
                            ),
                            new DateArrayReturning(
                                new DateArrayScalar(reversedPlacedOn)
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
    public void WholeDateArrayEqualityOfLiteralAgainstFieldFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        DateOnly[] reversedPlacedOn =
        [
            .. orderRows.Select(order => order.PlacedOn).Reverse(),
        ];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [OrderIdSelect()],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new DateArrayEquality(
                            new DateArrayReturning(
                                new DateArrayScalar(reversedPlacedOn)
                            ),
                            new DateArrayReturning(
                                new DateField(
                                    new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                    new PlacedOnColumn().Name.TextValue
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
