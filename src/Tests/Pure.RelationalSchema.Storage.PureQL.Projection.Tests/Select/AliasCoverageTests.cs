using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Aliases can cover every select item in a query, and an alias may collide
// with a different source column's name without the projected value being
// confused with that other column.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAlias")]
public sealed class AliasCoverageTests
{
    [Fact]
    public void AliasesOnEverySelectItemRenameAllColumns()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
                            )
                        )
                    ),
                    "id"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    ),
                    "state"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
                            )
                        )
                    ),
                    "amount"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(["id", "state", "amount"], result.ColumnNames);
        Assert.DoesNotContain("order_id", result.ColumnNames);
        Assert.DoesNotContain("order_status", result.ColumnNames);
        Assert.DoesNotContain("order_total", result.ColumnNames);
    }

    [Fact]
    public void AliasEqualToAnotherFieldNameShadowsInProjection()
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
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    ),
                    "order_total"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(["order_total"], result.ColumnNames);

        string?[] expected = [.. orderRows.Select(order => order.OrderStatus)];
        string?[] actual = [.. result.Column("order_total")];

        Assert.Equal(expected, actual);
    }
}
