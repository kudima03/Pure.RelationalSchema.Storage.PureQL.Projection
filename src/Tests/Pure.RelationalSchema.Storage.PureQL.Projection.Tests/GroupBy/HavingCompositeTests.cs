using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING composed with and / or / not over aggregate comparisons, plus
// equality between two aggregates of the same group. Orders are grouped by
// their user; expected groups are computed from the ground-truth records.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingCompositeTests
{
    [Fact]
    public void HavingAndOfTwoAggregateComparisonsRequiresBoth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingAndOfCountAndMaxComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 && group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsAcceptsEither()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingOrOfCountAndMaxComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 || group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingNotInvertsAnAggregateComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() <= 1);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingEqualityOfMinAndMaxKeepsConstantGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingEqualityOfMinAndMaxQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Min(order => order.OrderTotal)
                == group.Max(order => order.OrderTotal)
            );

        Assert.Equal(expected, result.Count);
    }
}
