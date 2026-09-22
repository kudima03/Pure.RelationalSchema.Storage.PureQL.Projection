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
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row `each` range comparisons (>, >=, <, <=) across the comparable value
// types (number, string, date, datetime, time). Boolean and uuid have no
// comparison operator in PureQL, so they are intentionally absent.
[Trait("Clause", "Where")]
[Trait("Feature", "EachComparison")]
public sealed class EachComparisonTests
{
    [Fact]
    public void EachNumberGreaterThanFiltersRowsAboveThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(100))
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

        Assert.Equal(orderRows.Count(order => order.OrderTotal > 100), result.Count);
    }

    [Fact]
    public void EachNumberGreaterThanZeroKeepsEveryPositiveRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        )
                    ),
                    "hours"
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
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

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Every sample total is positive, so nothing may be filtered out
        // (issue #90's live symptom was zero rows for exactly this shape).
        Assert.Equal(orderRows.Count, result.Count);
    }

    [Fact]
    public void EachNumberGreaterThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThanOrEqual,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(100.50))
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

        Assert.Equal(
            orderRows.Count(order => order.OrderTotal >= 100.50),
            result.Count
        );
    }

    [Fact]
    public void EachNumberLessThanFiltersRowsBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachLessThan,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(100))
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

        Assert.Equal(orderRows.Count(order => order.OrderTotal < 100), result.Count);
    }

    [Fact]
    public void EachNumberLessThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachLessThanOrEqual,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(75.25))
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

        Assert.Equal(
            orderRows.Count(order => order.OrderTotal <= 75.25),
            result.Count
        );
    }

    [Fact]
    public void EachStringGreaterThanUsesOrdinalOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachStringComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        ),
                        new StringReturning(new StringScalar("pending"))
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

        Assert.Equal(
            orderRows.Count(order =>
                string.CompareOrdinal(order.OrderStatus, "pending") > 0
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateGreaterThanFiltersLaterDates()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateArrayReturning(
                            new DateField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new PlacedOnColumn().Name.TextValue
                            )
                        ),
                        new DateReturning(new DateScalar(threshold))
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

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn > threshold),
            result.Count
        );
    }

    [Fact]
    public void EachTimeGreaterThanFiltersLaterTimes()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(9, 0, 0);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new TimeArrayReturning(
                            new TimeField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new ShiftStartColumn().Name.TextValue
                            )
                        ),
                        new TimeReturning(new TimeScalar(threshold))
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

        Assert.Equal(
            userRows.Count(user => user.ShiftStart > threshold),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeGreaterThanFiltersLaterInstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime threshold = new DateTime(2024, 6, 2, 9, 15, 0);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateTimeComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new LastLoginColumn().Name.TextValue
                            )
                        ),
                        new DateTimeReturning(new DateTimeScalar(threshold))
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

        Assert.Equal(
            userRows.Count(user => user.LastLogin > threshold),
            result.Count
        );
    }
}
