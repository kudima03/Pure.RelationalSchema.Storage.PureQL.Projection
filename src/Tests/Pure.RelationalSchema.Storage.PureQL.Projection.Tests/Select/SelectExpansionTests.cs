using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Select clause expansion: many differently-typed columns combined in one
// query, projecting every column of a table, output-column ordering that
// follows the select-expression order (not the source table's declared
// order), the same field selected more than once, and wide projections with
// many select expressions.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectExpansion")]
public sealed class SelectExpansionTests
{
    [Fact]
    public void SelectBooleanAndTimeColumnsTogetherFromUsersProjectsBothCorrectly()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Equal(
            [.. db.UserRows.Select(user => (bool?)user.UserActive)],
            [.. result.Rows.Select(row => row.Bool(SampleDatabase.Users.Active))]
        );
        Assert.Equal(
            [.. db.UserRows.Select(user => (TimeOnly?)user.ShiftStart)],
            [.. result.Rows.Select(row => row.Time(SampleDatabase.Users.ShiftStart))]
        );
    }

    [Fact]
    public void SelectDateAndDateTimeColumnsTogetherFromOrdersProjectsBothCorrectly()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedAt
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Equal(
            [.. db.OrderRows.Select(order => (DateOnly?)order.PlacedOn)],
            [.. result.Rows.Select(row => row.Date(SampleDatabase.Orders.PlacedOn))]
        );
        Assert.Equal(
            [.. db.OrderRows.Select(order => (DateTime?)order.PlacedAt)],
            [.. result.Rows.Select(row => row.DateTime(SampleDatabase.Orders.PlacedAt))]
        );
    }

    [Fact]
    public void SelectUuidStringAndDoubleColumnsTogetherFromOrdersProjectsAllThreeCorrectly()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Equal(
            [.. db.OrderRows.Select(order => (Guid?)order.OrderId)],
            [.. result.Rows.Select(row => row.Uuid(SampleDatabase.Orders.Id))]
        );
        Assert.Equal(
            [.. db.OrderRows.Select(order => order.OrderStatus)],
            result.Column(SampleDatabase.Orders.Status)
        );
        Assert.Equal(
            [.. db.OrderRows.Select(order => (double?)order.OrderTotal)],
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void SelectAllOrderColumnsProjectsEveryColumnOfTheTable()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedAt
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(db.OrderRows.Count, result.Count);
        // Row-cell dictionaries do not preserve insertion order, so the
        // output-column order is asserted against the derived table schema
        // (backed by a plain, order-preserving sequence), not the
        // materialized row cells.
        Assert.Equal(
            [
                SampleDatabase.Orders.Id,
                SampleDatabase.Orders.UserId,
                SampleDatabase.Orders.Total,
                SampleDatabase.Orders.Status,
                SampleDatabase.Orders.PlacedAt,
                SampleDatabase.Orders.PlacedOn,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        OrderRow first = db.OrderRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.OrderId, row.Uuid(SampleDatabase.Orders.Id));
        Assert.Equal(first.OrderUserId, row.Uuid(SampleDatabase.Orders.UserId));
        Assert.Equal(first.OrderTotal, row.Double(SampleDatabase.Orders.Total));
        Assert.Equal(first.OrderStatus, row[SampleDatabase.Orders.Status]);
        Assert.Equal(first.PlacedAt, row.DateTime(SampleDatabase.Orders.PlacedAt));
        Assert.Equal(first.PlacedOn, row.Date(SampleDatabase.Orders.PlacedOn));
    }

    [Fact]
    public void SelectAllUserColumnsInReverseDeclaredOrderProjectsEveryColumn()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.LastLogin
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.SignupDate
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Equal(
            [
                SampleDatabase.Users.ShiftStart,
                SampleDatabase.Users.LastLogin,
                SampleDatabase.Users.SignupDate,
                SampleDatabase.Users.Active,
                SampleDatabase.Users.Age,
                SampleDatabase.Users.Name,
                SampleDatabase.Users.Id,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        UserRow first = db.UserRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.ShiftStart, row.Time(SampleDatabase.Users.ShiftStart));
        Assert.Equal(first.UserId, row.Uuid(SampleDatabase.Users.Id));
    }

    [Fact]
    public void SelectExpressionOrderOverridesDeclaredColumnOrderForOrders()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        // The declared table order is Id, UserId, Total, Status, ...; the
        // select order (Status, Id, Total) is neither the declared order
        // nor alphabetical, so this discriminates ordering-by-select from
        // any accidental ordering-by-schema.
        Assert.Equal(
            [
                SampleDatabase.Orders.Status,
                SampleDatabase.Orders.Id,
                SampleDatabase.Orders.Total,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        OrderRow first = db.OrderRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.OrderStatus, row[SampleDatabase.Orders.Status]);
        Assert.Equal(first.OrderId, row.Uuid(SampleDatabase.Orders.Id));
        Assert.Equal(first.OrderTotal, row.Double(SampleDatabase.Orders.Total));
    }

    [Fact]
    public void SelectExpressionOrderOverridesDeclaredColumnOrderForUsers()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [
                SampleDatabase.Users.Age,
                SampleDatabase.Users.Id,
                SampleDatabase.Users.Active,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        UserRow first = db.UserRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.UserAge, row.Double(SampleDatabase.Users.Age));
        Assert.Equal(first.UserId, row.Uuid(SampleDatabase.Users.Id));
        Assert.Equal(first.UserActive, row.Bool(SampleDatabase.Users.Active));
    }

    [Fact]
    public void DuplicateFieldWithDifferentAliasesProjectsBothColumnsIndependently()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    ),
                    "state_a"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    ),
                    "state_b"
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            ["state_a", "state_b"],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        string?[] expected = [.. db.OrderRows.Select(order => order.OrderStatus)];
        Assert.Equal(expected, result.Column("state_a"));
        Assert.Equal(expected, result.Column("state_b"));
    }

    [Fact]
    public void DuplicateFieldOnceBareOnceAliasedProjectsBothColumnsIndependently()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    ),
                    "status_alias"
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [SampleDatabase.Orders.Status, "status_alias"],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        string?[] expected = [.. db.OrderRows.Select(order => order.OrderStatus)];
        Assert.Equal(expected, result.Column(SampleDatabase.Orders.Status));
        Assert.Equal(expected, result.Column("status_alias"));
    }

    [Fact]
    public void DuplicateFieldWithoutAliasesThrowsOnColumnNameCollision()
    {
        // Selecting the same field twice with no alias on either produces
        // two output columns with the identical name (the field name);
        // the row projection's column dictionary rejects the duplicate key
        // instead of silently keeping only one. This is a defined failure,
        // not a silently-wrong result, so only the fact that it throws is
        // pinned - not a specific exception type, since the collision is
        // detected deep inside a third-party dictionary implementation.
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
            ]
        );

        _ = Assert.ThrowsAny<Exception>(() =>
            new ProjectionResult(new PureQLProjection(db.Datasets, query))
        );
    }

    [Fact]
    public void WideProjectionWithTwentyAliasedExpressionsFromUsersProjectsAllColumns()
    {
        SampleDatabase db = new SampleDatabase();

        List<SelectExpression> selectExpressions = [];
        for (int i = 0; i < 20; i++)
        {
            selectExpressions.Add(
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    ),
                    "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
            );
        }

        Query query = new Query(new FromExpression(SampleDatabase.Users.Entity), selectExpressions);

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Equal(20, result.ColumnNames.Count);
        Assert.Equal(20, result.ColumnNames.Distinct().Count());

        string?[] expected = [.. db.UserRows.Select(user => user.UserName)];
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(
                expected,
                result.Column("wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))
            );
        }
    }

    [Fact]
    public void WideProjectionWithEighteenAliasedExpressionsFromOrdersProjectsAllColumns()
    {
        SampleDatabase db = new SampleDatabase();

        List<SelectExpression> selectExpressions = [];
        for (int i = 0; i < 18; i++)
        {
            selectExpressions.Add(
                i % 2 == 0
                    ? new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.Status
                                )
                            )
                        ),
                        "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
                    : new SelectExpression(
                        new ArrayReturning(
                            new NumberArrayReturning(
                                new NumberField(
                                    SampleDatabase.Orders.Entity,
                                    SampleDatabase.Orders.Total
                                )
                            )
                        ),
                        "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
            );
        }

        Query query = new Query(new FromExpression(SampleDatabase.Orders.Entity), selectExpressions);

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Equal(18, result.ColumnNames.Count);
        Assert.Equal(18, result.ColumnNames.Distinct().Count());

        string?[] expectedStatus = [.. db.OrderRows.Select(order => order.OrderStatus)];
        double?[] expectedTotal =
            [.. db.OrderRows.Select(order => (double?)order.OrderTotal)];

        for (int i = 0; i < 18; i++)
        {
            string alias = "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (i % 2 == 0)
            {
                Assert.Equal(expectedStatus, result.Column(alias));
            }
            else
            {
                Assert.Equal(
                    expectedTotal,
                    [.. result.Rows.Select(row => row.Double(alias))]
                );
            }
        }
    }

    // Alias renames output column for a computed/expression column. A
    // single-value Arithmetic whose operands are all literal constants now
    // evaluates once (see ScalarCell/LiteralArithmeticEvaluator), and per SQL
    // result-set semantics the alias renames the computed column exactly as
    // it does for a plain field (SelectAliasTests, AliasCoverageTests).
    [Fact]
    public void AliasRenamesComputedArithmeticColumn()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Arithmetic(
                                new Add(
                                    [
                                        new NumberReturning(new NumberScalar(1)),
                                        new NumberReturning(new NumberScalar(2)),
                                    ]
                                )
                            )
                        )
                    ),
                    "sum"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(["sum"], result.ColumnNames);
        Assert.All(result.Rows, row => Assert.Equal(3, row.Double("sum")));
    }

    // KnownGap: a GROUP BY key expressed as a NullField (the model's
    // dedicated NULL-literal field, distinct from every typed field used
    // elsewhere in this suite) has no defined projection today. Grouping by
    // a constant NULL should - per SQL result-set semantics - collapse the
    // whole table into a single group (every NULL compares equal to every
    // other NULL for grouping purposes) whose projected key column reads as
    // NULL on every output row. The translator does collapse to a single
    // group, but the projected key column leaks an arbitrary row's real
    // field value ("shipped", the first row's status) instead of NULL.
    [Fact(
        Skip = "KnownGap: grouping by a NullField key collapses to a single "
            + "group (correct), but the projected key column returns an "
            + "arbitrary row's real field value instead of NULL."
    )]
    [Trait("Status", "KnownGap")]
    public void GroupByNullFieldKeyProjectsNullNotAnArbitraryRowValue()
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
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "cnt"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    ),
                    "grouped_status"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NullField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
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

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Row(0)["cnt"]);
        Assert.Null(result.Row(0)["grouped_status"]);
    }
}
