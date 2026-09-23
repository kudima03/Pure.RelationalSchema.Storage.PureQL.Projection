using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join whose ON is a composite condition: an each-equality on the key AND an
// each-comparison filter, combined with eachAnd.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeJoinCondition")]
public sealed class CompositeJoinConditionTests
{
    [Fact]
    public void InnerJoinOnKeyAndQuantityKeepsMatchingHighQuantityItems()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        IReadOnlyList<OrderItemRecord> orderItemRows = [.. new OrderItemRecords()];
        Query query = new InnerJoinOnKeyAndQuantityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = (
            from order in orderRows
            from item in orderItemRows
            where item.ItemOrderId == order.OrderId && item.ItemQty > 1
            select 1
        ).Count();

        Assert.Equal(expected, result.Count);
    }
}
