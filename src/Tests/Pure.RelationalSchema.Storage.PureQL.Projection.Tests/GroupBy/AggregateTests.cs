using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Aggregate projections (sum/count/... over each group): one result row per
// group, holding the folded aggregate value.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Aggregate")]
public sealed class AggregateTests
{
    [Fact]
    public void SumAggregateProjectsPerGroupTotal()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "group_total"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField("schema_with_foreign_keys.orders", "order_user_id"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }

    [Fact]
    public void CountAggregateProjectsPerGroupRowCount()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            "schema_with_foreign_keys.orders",
                                            "order_id"
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "group_count"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField("schema_with_foreign_keys.orders", "order_user_id"))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedGroups = orderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expectedGroups, result.Count);
    }
}
