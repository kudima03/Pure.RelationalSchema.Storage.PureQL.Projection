using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.ArrayScalars;
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row each* predicates whose right operand is a *literal array* (an
// X ArrayScalar), for the five element types not already covered by
// EachArrayOperandTests/EachBroadcastAndLiteralTests (Number, String): Bool,
// Date, DateTime, Time, Uuid. WhereExpressionBuilder.Build<Type>ArrayValuePerRow
// resolves a literal array operand with `.FirstOrDefault()` - every row
// evaluation uses only the literal's first element, broadcast to every row
// regardless of the literal's declared length (ratified behaviour, see
// Semantics/README.md "Literal-array each* operand vs. row count"). Every
// query here is constructed explicitly and inline, and expected surviving
// rows are computed independently from the ground-truth record lists.
[Trait("Clause", "Where")]
[Trait("Feature", "EachLiteralOperand")]
public sealed class EachLiteralOperandTypeMatrixTests
{
    private static SelectExpression ProductNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField(SampleDatabase.Products.Entity, SampleDatabase.Products.Name)
                )
            )
        );
    }

    private static SelectExpression OrderTotalSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new NumberArrayReturning(
                    new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
                )
            )
        );
    }

    private static SelectExpression UserNameSelect()
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField(SampleDatabase.Users.Entity, SampleDatabase.Users.Name)
                )
            )
        );
    }

    // ===== Boolean: Products.InStock (Widget=true, Gadget=false, =====
    // ===== Gizmo=true, Deluxe=true)                                =====

    [Fact]
    public void EachEqualBooleanLiteralArrayKeepsRowsMatchingTheFirstElement()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.InStock
                            )
                        ),
                        new BooleanArrayReturning(new BooleanArrayScalar([true]))
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
            db.ProductRows.Count(product => product.ProductInStock),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 5-element literal ([false, true, true, true, true]) broadcasts only
    // its first element (false) to every row, regardless of its own length
    // (5) or the table's row count (4): every row is checked against
    // `InStock == false`, keeping only Gadget. If the trailing `true`
    // elements had any effect, more rows would survive.
    [Fact]
    public void EachEqualBooleanLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.InStock
                            )
                        ),
                        new BooleanArrayReturning(
                            new BooleanArrayScalar([false, true, true, true, true])
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
            db.ProductRows.Count(product => !product.ProductInStock),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Gadget", Assert.Single(result.Column(SampleDatabase.Products.Name)));
    }

    // eachNot wraps a literal-operand eachEqual, showing the broadcast first
    // element (true) still applies under negation: keeps rows whose InStock
    // is NOT true, i.e. only Gadget.
    [Fact]
    public void EachNotOfBooleanLiteralArrayEqualityKeepsRowsNotMatchingFirstElement()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [ProductNameSelect()],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachBooleanEquality(
                                new BooleanArrayReturning(
                                    new BooleanField(
                                        SampleDatabase.Products.Entity,
                                        SampleDatabase.Products.InStock
                                    )
                                ),
                                new BooleanArrayReturning(new BooleanArrayScalar([true]))
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
            db.ProductRows.Count(product => !product.ProductInStock),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Gadget", Assert.Single(result.Column(SampleDatabase.Products.Name)));
    }

    // ===== Uuid: Orders.Id (each order id is distinct) =====

    [Fact]
    public void EachEqualUuidLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        SampleDatabase db = new SampleDatabase();
        Guid target = db.OrderRows.Single(order => order.OrderTotal == 200.00).OrderId;

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                        ),
                        new UuidArrayReturning(new UuidArrayScalar([target]))
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
            db.OrderRows.Count(order => order.OrderId == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal(200.00, result.Row(0).Double(SampleDatabase.Orders.Total));
    }

    // A 3-element literal whose first element is the target order id and
    // whose remaining two point at other, non-matching orders still filters
    // purely on the first element, keeping only the target's row.
    [Fact]
    public void EachEqualUuidLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        SampleDatabase db = new SampleDatabase();
        Guid target = db.OrderRows.Single(order => order.OrderTotal == 200.00).OrderId;
        Guid decoyOne = db.OrderRows.Single(order => order.OrderTotal == 50.00).OrderId;
        Guid decoyTwo = db.OrderRows.Single(order => order.OrderTotal == 75.25).OrderId;

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Id)
                        ),
                        new UuidArrayReturning(
                            new UuidArrayScalar([target, decoyOne, decoyTwo])
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
            db.OrderRows.Count(order => order.OrderId == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal(200.00, result.Row(0).Double(SampleDatabase.Orders.Total));
    }

    // ===== Date: Orders.PlacedOn (2024-06-01 .. 2024-06-06, one per row) =====

    [Fact]
    public void EachEqualDateLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly target = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([target]))
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
            db.OrderRows.Count(order => order.PlacedOn == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    // A 3-element literal whose first element is 2024-06-01 and whose
    // remaining two (2024-06-02, 2024-06-03) each match a different order
    // still filters purely on the first element, keeping only the
    // 2024-06-01 order.
    [Fact]
    public void EachEqualDateLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly target = new DateOnly(2024, 6, 1);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateArrayReturning(
                            new DateArrayScalar(
                                [
                                    target,
                                    new DateOnly(2024, 6, 2),
                                    new DateOnly(2024, 6, 3),
                                ]
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
            db.OrderRows.Count(order => order.PlacedOn == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void EachGreaterThanDateLiteralArrayFiltersLaterDates()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([threshold]))
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
            db.OrderRows.Count(order => order.PlacedOn > threshold),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 2-element literal ([2024-06-03, 2024-06-01]) broadcasts only its
    // first element under `eachLessThan` too: every row is checked against
    // `PlacedOn < 2024-06-03`. If the trailing 2024-06-01 element had any
    // effect (e.g. as a second per-row-zipped value), the surviving count
    // would differ (comparing against 2024-06-01 keeps zero rows).
    [Fact]
    public void EachLessThanDateLiteralArrayBroadcastsFirstElementUnderComparison()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachLessThan,
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateArrayReturning(
                            new DateArrayScalar([threshold, new DateOnly(2024, 6, 1)])
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
            db.OrderRows.Count(order => order.PlacedOn < threshold),
            result.Count
        );
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EachEqualDateLiteralArrayWithNoMatchesReturnsEmpty()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly target = new DateOnly(2099, 1, 1);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateEquality(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateArrayReturning(new DateArrayScalar([target]))
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

    // ===== DateTime: Orders.PlacedAt =====
    // ===== (2024-06-01T10:00 .. 2024-06-06T15:00, one per row) =====

    [Fact]
    public void EachEqualDateTimeLiteralArrayKeepsOnlyTheMatchingOrder()
    {
        SampleDatabase db = new SampleDatabase();
        DateTime target = new DateTime(2024, 6, 1, 10, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateTimeEquality(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedAt
                            )
                        ),
                        new DateTimeArrayReturning(new DateTimeArrayScalar([target]))
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
            db.OrderRows.Count(order => order.PlacedAt == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    // A 2-element literal whose first element is the target instant and
    // whose second element (2024-06-02T11:00) matches a different order
    // still filters purely on the first element.
    [Fact]
    public void EachEqualDateTimeLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        SampleDatabase db = new SampleDatabase();
        DateTime target = new DateTime(2024, 6, 1, 10, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachDateTimeEquality(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedAt
                            )
                        ),
                        new DateTimeArrayReturning(
                            new DateTimeArrayScalar(
                                [target, new DateTime(2024, 6, 2, 11, 0, 0)]
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
            db.OrderRows.Count(order => order.PlacedAt == target),
            result.Count
        );
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void EachGreaterThanDateTimeLiteralArrayFiltersLaterInstants()
    {
        SampleDatabase db = new SampleDatabase();
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [OrderTotalSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateTimeComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedAt
                            )
                        ),
                        new DateTimeArrayReturning(new DateTimeArrayScalar([threshold]))
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
            db.OrderRows.Count(order => order.PlacedAt > threshold),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // ===== Time: Users.ShiftStart =====
    // ===== (Ann 09:00, Bob 10:00, Cara 09:00, Dan 11:30, Eve 08:00, =====
    // ===== Fay 09:00)                                               =====

    [Fact]
    public void EachEqualTimeLiteralArrayKeepsRowsMatchingTheFirstElement()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly target = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachTimeEquality(
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([target]))
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
            db.UserRows.Count(user => user.ShiftStart == target),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    // A 3-element literal whose first element is 09:00 and whose remaining
    // two (11:30, 08:00) each match a different, non-09:00 user still
    // filters purely on the first element, keeping only the three 09:00
    // users.
    [Fact]
    public void EachEqualTimeLiteralArrayBroadcastsFirstElementRegardlessOfLength()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly target = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachTimeEquality(
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        ),
                        new TimeArrayReturning(
                            new TimeArrayScalar(
                                [target, new TimeOnly(11, 30, 0), new TimeOnly(8, 0, 0)]
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
            db.UserRows.Count(user => user.ShiftStart == target),
            result.Count
        );
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void EachLessThanTimeLiteralArrayFiltersEarlierTimes()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachLessThan,
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([threshold]))
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
            db.UserRows.Count(user => user.ShiftStart < threshold),
            result.Count
        );
        Assert.Equal(1, result.Count);
        Assert.Equal("Eve", Assert.Single(result.Column(SampleDatabase.Users.Name)));
    }

    [Fact]
    public void EachGreaterThanOrEqualTimeLiteralArrayIncludesTheThreshold()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [UserNameSelect()],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachGreaterThanOrEqual,
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        ),
                        new TimeArrayReturning(new TimeArrayScalar([threshold]))
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
            db.UserRows.Count(user => user.ShiftStart >= threshold),
            result.Count
        );
        Assert.Equal(5, result.Count);
    }
}
