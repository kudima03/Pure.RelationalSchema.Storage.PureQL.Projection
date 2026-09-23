using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Scalar;

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarAndOfTrueAndOrOfFalseTrueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    // 2-level: not(and(a=true, b=false))
    // and(true, false) = false; not(false) = true.
    [Fact]
    public void ScalarNotOfAndOfTrueAndFalseKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarNotOfAndOfTrueAndFalseQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    // 3-level: or(not(and(a, b)), c)
    // a = (5 > 3) = true; b = ("x" == "y") = false; c = false.
    // and(a, b) = false; not(false) = true; or(true, c) = true.
    [Fact]
    public void ScalarOrOfNotAndAtThreeLevelsKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarOrOfNotAndAtThreeLevelsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    // 3-level: and(not(or(a, b)), c)
    // a = (1 < 2) = true; b = ("x" == "y") = false; c = true.
    // or(a, b) = true; not(true) = false; and(false, c) = false.
    [Fact]
    public void ScalarAndOfNotOrAtThreeLevelsRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ScalarAndOfNotOrAtThreeLevelsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // 3-level: not(not(not(a))), a = true.
    // not(true) = false; not(false) = true; not(true) = false.
    [Fact]
    public void ScalarTripleNotChainOfTrueRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ScalarTripleNotChainOfTrueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarAndOrNotAtFourLevelsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }

    // 4-level: or(and(not(or(a, b)), c), d)
    // a = false, b = false -> or(a, b) = false -> not = true.
    // c = (1 > 2) = false -> and(true, false) = false.
    // d = ("x" == "y") = false -> or(false, false) = false.
    [Fact]
    public void ScalarOrAndNotAtFourLevelsRemovesEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ScalarOrAndNotAtFourLevelsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // 4-level: not(not(not(not(a)))), a = true.
    // Four negations of true: false, true, false, true.
    [Fact]
    public void ScalarQuadrupleNotChainOfTrueKeepsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarQuadrupleNotChainOfTrueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarFiveLevelAndRootedTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ScalarFiveLevelOrRootedTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query queryA = new DeMorganNotOfAndAtThreeLevelsQuery().Value;

        Query queryB = new DeMorganOrOfNotsAtThreeLevelsQuery().Value;

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(datasets, queryB)
        );

        Assert.Equal(orderRows.Count, resultA.Count);
        Assert.Equal(orderRows.Count, resultB.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query queryA = new DeMorganNotOfOrAtFourLevelsQuery().Value;

        Query queryB = new DeMorganAndOfNotsAtFourLevelsQuery().Value;

        ProjectionResult resultA = new ProjectionResult(
            new PureQLProjection(datasets, queryA)
        );
        ProjectionResult resultB = new ProjectionResult(
            new PureQLProjection(datasets, queryB)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DeeplyNestedAlwaysTrueTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new DeeplyNestedAlwaysFalseTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarArithmeticInComparisonPredicateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
    }
}
