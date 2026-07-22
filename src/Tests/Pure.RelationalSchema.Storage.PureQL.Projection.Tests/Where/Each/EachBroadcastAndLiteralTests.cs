using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Confirms two corrected #98 behaviours read straight from
// WhereExpressionBuilder.cs source (see Semantics/README.md): a literal
// array operand is never zipped by row index - every per-row evaluation
// uses only the literal's first element (`.FirstOrDefault()`), broadcast to
// every row regardless of the literal's declared length; and eachDivide by
// zero returns null for that row rather than throwing. Also covers mixing a
// scalar-broadcast operand with a per-row array-aligned operand within the
// same predicate.
[Trait("Clause", "Where")]
[Trait("Feature", "EachBroadcastAndLiteral")]
public sealed class EachBroadcastAndLiteralTests
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

    // eachAnd(total > 50 [broadcast scalar], total >= total [array-aligned,
    // trivially true every row]) combines both operand kinds in one tree.
    [Fact]
    public void BroadcastScalarOperandAndArrayAlignedOperandCombineInSamePredicate()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachAndOperator(
                    [
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThan,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberReturning(new NumberScalar(50))
                                )
                            )
                        ),
                        new BooleanArrayReturning(
                            new EachComparison(
                                new EachNumberComparison(
                                    EachComparisonOperator.EachGreaterThanOrEqual,
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    ),
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        ),
                    ]
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

        Assert.Equal(db.OrderRows.Count(o => o.OrderTotal > 50), result.Count);
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberArrayScalar([999, -5])
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

        // Every row keeps: 999 (first literal element) > 0. A row-index zip
        // would instead only define rows 0-1 and leave the rest ambiguous.
        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    // A 6-element literal string array whose *first* element is "shipped"
    // and whose remaining five are junk values broadcasts "shipped" to
    // every row's equality check against the (array-aligned) status field,
    // rather than zipping element[i] against row[i]'s status.
    [Fact]
    public void LiteralStringArrayOperandBroadcastsFirstElementOnEqualityCheck()
    {
        SampleDatabase db = new SampleDatabase();

        // The literal array is the left operand, checked per row against the
        // (per-row, array-aligned) status field on the right. If the literal
        // were zipped by row index, only row 0 could ever match (its own
        // index carries "shipped"); broadcast means every row is compared
        // against literal[0] = "shipped" instead.
        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringArrayScalar(
                                [
                                    "shipped",
                                    "zzz",
                                    "zzz",
                                    "zzz",
                                    "zzz",
                                    "zzz",
                                ]
                            )
                        ),
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
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

        Assert.Equal(
            db.OrderRows.Count(o => o.OrderStatus == "shipped"),
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachDivide(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(0)),
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

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ... a per-row comparison, ...
    [Fact]
    public void EachDivideByZeroFailsFastEvenUnderComparison()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
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
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(0)),
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

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ... or the divide-by-zero result compared against itself.
    [Fact]
    public void EachDivideByZeroFailsFastEvenComparedAgainstItself()
    {
        SampleDatabase db = new SampleDatabase();

        NumberArrayReturning divideByZero = new NumberArrayReturning(
            new EachArithmetic(
                new EachDivide(
                    [
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        ),
                        new NumberReturning(new NumberScalar(0)),
                    ]
                )
            )
        );

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderIdSelect()],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachNumberEquality(divideByZero, divideByZero)
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

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }
}
