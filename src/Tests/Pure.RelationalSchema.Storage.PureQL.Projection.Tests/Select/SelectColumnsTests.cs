using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

[Trait("Clause", "Select")]
[Trait("Feature", "SelectColumns")]
public sealed class SelectColumnsTests
{
    [Fact]
    public void SelectSingleStringColumnReturnsThatColumnForEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.orders", "order_status")
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal(["order_status"], result.ColumnNames);
        Assert.Equal(
            [.. orderRows.Select(order => order.OrderStatus)],
            result.Column("order_status")
        );
    }

    [Fact]
    public void SelectMultipleColumnsProjectsAllOfThemPreservingRowOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.orders", "order_status")
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField("schema_with_foreign_keys.orders", "order_total")
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Contains("order_status", result.ColumnNames);
        Assert.Contains("order_total", result.ColumnNames);
        Assert.Equal(
            orderRows.Select(order => (double?)order.OrderTotal).ToArray(),
            [.. result.Rows.Select(row => row.Double("order_total"))]
        );
    }

    [Fact]
    public void SelectUuidColumnRoundTripsEachIdentifier()
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
                            new UuidField("schema_with_foreign_keys.users", "user_id")
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (Guid?)user.UserId).ToArray(),
            [.. result.Rows.Select(row => row.Uuid("user_id"))]
        );
    }
}
