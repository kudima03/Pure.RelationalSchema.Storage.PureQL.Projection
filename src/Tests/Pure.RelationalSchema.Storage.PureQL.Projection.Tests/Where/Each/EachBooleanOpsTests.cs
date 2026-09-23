using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row boolean composition: eachAnd / eachOr over per-row conditions, and
// eachNot (PureQL has no eachNotEqual; negation is expressed as not(eachEqual)).
[Trait("Clause", "Where")]
[Trait("Feature", "EachBooleanOps")]
public sealed class EachBooleanOpsTests
{
    [Fact]
    public void EachAndKeepsRowsSatisfyingEveryCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachAndQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                order.OrderTotal > 100 && order.OrderStatus == "shipped"
            ),
            result.Count
        );
    }

    [Fact]
    public void EachOrKeepsRowsSatisfyingAnyCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachOrQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                order.OrderStatus == "cancelled" || order.OrderTotal >= 300
            ),
            result.Count
        );
    }

    [Fact]
    public void EachNotNegatesAPerRowCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderStatus != "shipped"),
            result.Count
        );
    }
}
