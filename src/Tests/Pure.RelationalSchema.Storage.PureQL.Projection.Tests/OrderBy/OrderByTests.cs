using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// ORDER BY across each value type, ascending and descending, plus a stable
// multi-key sort. Expected sequences are produced by the equivalent stable
// LINQ ordering over the ground-truth records.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderBy")]
public sealed class OrderByTests
{
    [Fact]
    public void OrderByNumberAscendingSortsRowsLowToHigh()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.orders",
                            "order_total"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double("order_total"))]
        );
    }

    [Fact]
    public void OrderByNumberDescendingSortsRowsHighToLow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.orders",
                            "order_total"
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.OrderByDescending(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double("order_total"))]
        );
    }

    [Fact]
    public void OrderByStringAscendingSortsRowsAlphabetically()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            "schema_with_foreign_keys.users",
                            "user_name"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column("user_name").ToArray()
        );
    }

    [Fact]
    public void OrderByDateDescendingSortsRowsLatestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.users",
                                "signup_date"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new DateField(
                            "schema_with_foreign_keys.users",
                            "signup_date"
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderByDescending(user => user.SignupDate)
                .Select(user => (DateOnly?)user.SignupDate)
                .ToArray(),
            [.. result.Rows.Select(row => row.Date("signup_date"))]
        );
    }

    [Fact]
    public void OrderByDateTimeAscendingSortsRowsEarliestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.users",
                                "last_login"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            "schema_with_foreign_keys.users",
                            "last_login"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.LastLogin)
                .Select(user => (DateTime?)user.LastLogin)
                .ToArray(),
            [.. result.Rows.Select(row => row.DateTime("last_login"))]
        );
    }

    [Fact]
    public void OrderByTimeAscendingSortsRowsEarliestFirst()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "shift_start"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new TimeField(
                            "schema_with_foreign_keys.users",
                            "shift_start"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.ShiftStart)
                .Select(user => (TimeOnly?)user.ShiftStart)
                .ToArray(),
            [.. result.Rows.Select(row => row.Time("shift_start"))]
        );
    }

    [Fact]
    public void OrderByUuidAscendingMatchesGuidComparerOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.users",
                                "user_id"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new UuidField(
                            "schema_with_foreign_keys.users",
                            "user_id"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserId)
                .Select(user => (Guid?)user.UserId)
                .ToArray(),
            [.. result.Rows.Select(row => row.Uuid("user_id"))]
        );
    }

    [Fact]
    public void OrderByTwoKeysAppliesStableSecondaryOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            "schema_with_foreign_keys.users",
                            "user_age"
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new StringField(
                            "schema_with_foreign_keys.users",
                            "user_name"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.OrderBy(user => user.UserAge)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column("user_name").ToArray()
        );
    }
}
