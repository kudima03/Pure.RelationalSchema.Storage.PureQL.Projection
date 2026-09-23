using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Deeply nested per-row boolean trees (eachAnd / eachOr / eachNot composed
// several levels deep), exercising the recursive predicate builder.
[Trait("Clause", "Where")]
[Trait("Feature", "EachNestedBoolean")]
public sealed class EachNestedBooleanTests
{
    [Fact]
    public void AndOfOrAndNotFiltersByTheCombinedPredicate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new AndOfOrAndNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                (order.OrderTotal > 100 || order.OrderStatus == "pending")
                && order.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    [Fact]
    public void OrOfTwoAndBranchesFiltersByEitherCombination()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new OrOfTwoAndBranchesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                (order.OrderStatus == "shipped" && order.OrderTotal >= 200)
                || (order.OrderStatus == "pending" && order.OrderTotal < 100)
            ),
            result.Count
        );
    }

    [Fact]
    public void DoubleNegationIsEquivalentToTheInnerCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new DoubleNegationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.OrderStatus == "shipped"),
            result.Count
        );
    }
}
