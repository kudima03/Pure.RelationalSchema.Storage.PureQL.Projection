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
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Samples.Queries.Select;

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new SelectBooleanAndTimeColumnsTogetherFromUsersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.Equal(
            [.. userRows.Select(user => (bool?)user.UserActive)],
            [.. result.Rows.Select(row => row.Bool(new UserActiveColumn().Name.TextValue))]
        );
        Assert.Equal(
            [.. userRows.Select(user => (TimeOnly?)user.ShiftStart)],
            [.. result.Rows.Select(row => row.Time(new ShiftStartColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void SelectDateAndDateTimeColumnsTogetherFromOrdersProjectsBothCorrectly()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectDateAndDateTimeColumnsTogetherFromOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal(
            [.. orderRows.Select(order => (DateOnly?)order.PlacedOn)],
            [.. result.Rows.Select(row => row.Date(new PlacedOnColumn().Name.TextValue))]
        );
        Assert.Equal(
            [.. orderRows.Select(order => (DateTime?)order.PlacedAt)],
            [.. result.Rows.Select(row => row.DateTime(new PlacedAtColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void SelectUuidStringAndDoubleColumnsTogetherFromOrdersProjectsAllThreeCorrectly()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectUuidStringAndDoubleColumnsTogetherFromOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal(
            [.. orderRows.Select(order => (Guid?)order.OrderId)],
            [.. result.Rows.Select(row => row.Uuid(new OrderIdColumn().Name.TextValue))]
        );
        Assert.Equal(
            [.. orderRows.Select(order => order.OrderStatus)],
            result.Column(new OrderStatusColumn().Name.TextValue)
        );
        Assert.Equal(
            [.. orderRows.Select(order => (double?)order.OrderTotal)],
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void SelectAllOrderColumnsProjectsEveryColumnOfTheTable()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectAllOrderColumnsQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(orderRows.Count, result.Count);
        // Row-cell dictionaries do not preserve insertion order, so the
        // output-column order is asserted against the derived table schema
        // (backed by a plain, order-preserving sequence), not the
        // materialized row cells.
        Assert.Equal(
            [
                new OrderIdColumn().Name.TextValue,
                new OrderUserIdColumn().Name.TextValue,
                new OrderTotalColumn().Name.TextValue,
                new OrderStatusColumn().Name.TextValue,
                new PlacedAtColumn().Name.TextValue,
                new PlacedOnColumn().Name.TextValue,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        OrderRecord first = orderRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.OrderId, row.Uuid(new OrderIdColumn().Name.TextValue));
        Assert.Equal(first.OrderUserId, row.Uuid(new OrderUserIdColumn().Name.TextValue));
        Assert.Equal(first.OrderTotal, row.Double(new OrderTotalColumn().Name.TextValue));
        Assert.Equal(first.OrderStatus, row[new OrderStatusColumn().Name.TextValue]);
        Assert.Equal(first.PlacedAt, row.DateTime(new PlacedAtColumn().Name.TextValue));
        Assert.Equal(first.PlacedOn, row.Date(new PlacedOnColumn().Name.TextValue));
    }

    [Fact]
    public void SelectAllUserColumnsInReverseDeclaredOrderProjectsEveryColumn()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new SelectAllUserColumnsInReverseDeclaredOrderQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(userRows.Count, result.Count);
        Assert.Equal(
            [
                new ShiftStartColumn().Name.TextValue,
                new LastLoginColumn().Name.TextValue,
                new SignupDateColumn().Name.TextValue,
                new UserActiveColumn().Name.TextValue,
                new UserAgeColumn().Name.TextValue,
                new UserNameColumn().Name.TextValue,
                new UserIdColumn().Name.TextValue,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        UserRecord first = userRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.ShiftStart, row.Time(new ShiftStartColumn().Name.TextValue));
        Assert.Equal(first.UserId, row.Uuid(new UserIdColumn().Name.TextValue));
    }

    [Fact]
    public void SelectExpressionOrderOverridesDeclaredColumnOrderForOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectOrderColumnsOutOfDeclaredOrderQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        // The declared table order is Id, UserId, Total, Status, ...; the
        // select order (Status, Id, Total) is neither the declared order
        // nor alphabetical, so this discriminates ordering-by-select from
        // any accidental ordering-by-schema.
        Assert.Equal(
            [
                new OrderStatusColumn().Name.TextValue,
                new OrderIdColumn().Name.TextValue,
                new OrderTotalColumn().Name.TextValue,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        OrderRecord first = orderRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.OrderStatus, row[new OrderStatusColumn().Name.TextValue]);
        Assert.Equal(first.OrderId, row.Uuid(new OrderIdColumn().Name.TextValue));
        Assert.Equal(first.OrderTotal, row.Double(new OrderTotalColumn().Name.TextValue));
    }

    [Fact]
    public void SelectExpressionOrderOverridesDeclaredColumnOrderForUsers()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new SelectUserColumnsOutOfDeclaredOrderQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [
                new UserAgeColumn().Name.TextValue,
                new UserIdColumn().Name.TextValue,
                new UserActiveColumn().Name.TextValue,
            ],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        UserRecord first = userRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.UserAge, row.Double(new UserAgeColumn().Name.TextValue));
        Assert.Equal(first.UserId, row.Uuid(new UserIdColumn().Name.TextValue));
        Assert.Equal(first.UserActive, row.Bool(new UserActiveColumn().Name.TextValue));
    }

    [Fact]
    public void DuplicateFieldWithDifferentAliasesProjectsBothColumnsIndependently()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DuplicateFieldWithDifferentAliasesQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            ["state_a", "state_b"],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        string?[] expected = [.. orderRows.Select(order => order.OrderStatus)];
        Assert.Equal(expected, result.Column("state_a"));
        Assert.Equal(expected, result.Column("state_b"));
    }

    [Fact]
    public void DuplicateFieldOnceBareOnceAliasedProjectsBothColumnsIndependently()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DuplicateFieldOnceBareOnceAliasedQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);
        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [new OrderStatusColumn().Name.TextValue, "status_alias"],
            [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)]
        );

        string?[] expected = [.. orderRows.Select(order => order.OrderStatus)];
        Assert.Equal(expected, result.Column(new OrderStatusColumn().Name.TextValue));
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new DuplicateFieldWithoutAliasesQuery().Value;

        _ = Assert.ThrowsAny<Exception>(() =>
            new ProjectionResult(new PureQLProjection(datasets, query))
        );
    }

    [Fact]
    public void WideProjectionWithTwentyAliasedExpressionsFromUsersProjectsAllColumns()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        List<SelectExpression> selectExpressions = [];
        for (int i = 0; i < 20; i++)
        {
            selectExpressions.Add(
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    ),
                    "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
            );
        }

        Query query =
            new WideProjectionWithTwentyAliasedExpressionsFromUsersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.Equal(20, result.ColumnNames.Count);
        Assert.Equal(20, result.ColumnNames.Distinct().Count());

        string?[] expected = [.. userRows.Select(user => user.UserName)];
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        List<SelectExpression> selectExpressions = [];
        for (int i = 0; i < 18; i++)
        {
            selectExpressions.Add(
                i % 2 == 0
                    ? new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    new JoinedString(
                                        new DotString(),
                                        [
                                            new RelationalSchemaWithForeignKeys().Name,
                                            new OrdersTable().Name,
                                        ]
                                    ).TextValue,
                                    new OrderStatusColumn().Name.TextValue
                                )
                            )
                        ),
                        "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
                    : new SelectExpression(
                        new ArrayReturning(
                            new NumberArrayReturning(
                                new NumberField(
                                    new JoinedString(
                                        new DotString(),
                                        [
                                            new RelationalSchemaWithForeignKeys().Name,
                                            new OrdersTable().Name,
                                        ]
                                    ).TextValue,
                                    new OrderTotalColumn().Name.TextValue
                                )
                            )
                        ),
                        "wide_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
            );
        }

        Query query =
            new WideProjectionWithEighteenAliasedExpressionsFromOrdersQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal(18, result.ColumnNames.Count);
        Assert.Equal(18, result.ColumnNames.Distinct().Count());

        string?[] expectedStatus = [.. orderRows.Select(order => order.OrderStatus)];
        double?[] expectedTotal =
            [.. orderRows.Select(order => (double?)order.OrderTotal)];

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new AliasedLiteralArithmeticQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new GroupByNullFieldKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Row(0)["cnt"]);
        Assert.Null(result.Row(0)["grouped_status"]);
    }
}
