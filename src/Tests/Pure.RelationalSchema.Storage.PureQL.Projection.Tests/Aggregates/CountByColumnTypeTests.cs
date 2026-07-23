using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// `count` over every column type (Boolean, Date, DateTime, Number, Time;
// String/Uuid are already covered in CountTests.cs), both grouped and over
// the whole set, plus the SQL NULL-exclusion semantics of count(column):
// NULLs are dropped, not counted as present rows.
[Trait("Clause", "Aggregate")]
[Trait("Feature", "Count")]
public sealed class CountByColumnTypeTests
{
    [Fact]
    public void CountOfBooleanColumnGroupedByStockStatusProjectsGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            SampleDatabase.Products.Entity,
                                            SampleDatabase.Products.InStock
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(
                        SampleDatabase.Products.Entity,
                        SampleDatabase.Products.InStock
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.ProductRows.GroupBy(product => product.ProductInStock)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfBooleanColumnOverAllProductsProjectsWholeSetRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new BooleanArrayReturning(
                                        new BooleanField(
                                            SampleDatabase.Products.Entity,
                                            SampleDatabase.Products.InStock
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.ProductRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfDateColumnPerUserProjectsGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfDateColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfDateTimeColumnPerUserProjectsGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfDateTimeColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfNumberColumnPerUserProjectsGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfNumberColumnOverAllOrdersProjectsWholeSetRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("n"));
    }

    [Fact]
    public void CountOfTimeColumnGroupedByActiveStatusProjectsGroupRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.UserRows.GroupBy(user => user.UserActive)
                .Select(group => (double)group.Count())
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountOfTimeColumnOverAllUsersProjectsWholeSetRowCount()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.UserRows.Count, result.Row(0).Double("n"));
    }

    // SQL semantics: count(column) counts non-NULL values, not row presence.
    // Users.Score is NULL for Bob and Dan (issue #103 fixture), so the
    // expected count is 4, not the full row count of 6.
    [Fact]
    public void CountOfNullableScoreColumnExcludesNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Score
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(
            db.UserRows.Count(user => user.Score is not null),
            result.Row(0).Double("n")
        );
    }

    // Grouped cross-check of the same NULL-exclusion behaviour: Bob (Active =
    // false, Score = null) and Dan (Active = true, Score = null) each drop
    // out of their group's count, leaving Active=true at 3 (Ann, Cara, Fay)
    // and Active=false at 1 (Eve).
    [Fact]
    public void CountOfNullableScoreColumnGroupedByActiveExcludesNullRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Score
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new BooleanField(SampleDatabase.Users.Entity, SampleDatabase.Users.Active)
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.UserRows.GroupBy(user => user.UserActive)
                .Select(group => (double)group.Count(user => user.Score is not null))
                .OrderBy(value => value),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double("n")!.Value).OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
