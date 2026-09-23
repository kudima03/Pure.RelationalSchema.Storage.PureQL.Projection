using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Confirms two corrected #98 behaviours read straight from
// WhereExpressionBuilder.cs source (see Semantics/README.md): a literal
// array operand is never zipped by row index - every per-row evaluation
// uses only the literal's first element (`.FirstOrDefault()`), broadcast to
// every row regardless of the literal's declared length; and eachDivide by
// zero raises DivideByZeroException (see issue #104's follow-up fix) rather
// than silently yielding null. Also covers mixing a scalar-broadcast operand
// with a per-row array-aligned operand within the same predicate.
[Trait("Clause", "Where")]
[Trait("Feature", "EachBroadcastAndLiteral")]
public sealed class EachBroadcastAndLiteralTests
{
    // eachAnd(total > 50 [broadcast scalar], total >= total [array-aligned,
    // trivially true every row]) combines both operand kinds in one tree.
    [Fact]
    public void BroadcastScalarOperandAndArrayAlignedOperandCombineInSamePredicate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ScalarAndArrayOperandsInOnePredicateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count(o => o.OrderTotal > 50), result.Count);
    }

    // A 2-element literal array ([999, -5]) compared against a 6-row table:
    // if the literal were zipped by row index, only rows 0-1 would have a
    // defined comparison and the rest would need a fallback. Actual
    // behaviour broadcasts the literal's first element (999) to every row,
    // regardless of the literal's declared length (2) or the row count (6),
    // so every row satisfies `999 > total` (max total is 300).
    [Fact]
    public void LiteralNumberArrayOperandBroadcastsFirstElementRegardlessOfLength()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNumberMultiElementLiteralArrayOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every row keeps: 999 (first literal element) > 0. A row-index zip
        // would instead only define rows 0-1 and leave the rest ambiguous.
        Assert.Equal(orderRows.Count, result.Count);
    }

    // A 6-element literal string array whose *first* element is "shipped"
    // and whose remaining five are junk values broadcasts "shipped" to
    // every row's equality check against the (array-aligned) status field,
    // rather than zipping element[i] against row[i]'s status.
    [Fact]
    public void LiteralStringArrayOperandBroadcastsFirstElementOnEqualityCheck()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // The literal array is the left operand, checked per row against the
        // (per-row, array-aligned) status field on the right. If the literal
        // were zipped by row index, only row 0 could ever match (its own
        // index carries "shipped"); broadcast means every row is compared
        // against literal[0] = "shipped" instead.
        Query query = new EachStringMultiElementLiteralArrayOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o => o.OrderStatus == "shipped"),
            result.Count
        );
    }

    // eachDivide by zero raises DivideByZeroException (matching SQL
    // division-by-zero semantics) as soon as any row's division is
    // evaluated - it does not silently yield null, whether the divide
    // result feeds an equality check, ...
    [Fact]
    public void EachDivideByZeroFailsFastEvenUnderEqualityComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachDivideByZeroUnderEqualityQuery().Value;

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ... a per-row comparison, ...
    [Fact]
    public void EachDivideByZeroFailsFastEvenUnderComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachDivideByZeroUnderComparisonQuery().Value;

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ... or the divide-by-zero result compared against itself.
    [Fact]
    public void EachDivideByZeroFailsFastEvenComparedAgainstItself()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachDivideByZeroComparedAgainstItselfQuery().Value;

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }
}
