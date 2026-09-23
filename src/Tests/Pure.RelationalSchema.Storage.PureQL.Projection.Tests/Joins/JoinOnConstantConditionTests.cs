using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join's on clause may be a single boolean-returning expression; constant
// conditions pin the degenerate join shapes: true yields the full cross
// product, false yields no matches (empty for INNER, fully padded for the
// outer types).
[Trait("Clause", "Join")]
[Trait("Feature", "ConstantCondition")]
public sealed class JoinOnConstantConditionTests
{
    [Fact]
    public void InnerJoinOnConstantTrueProducesTheCrossProduct()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new InnerJoinOnConstantTrueQuery().Value
            )
        );

        Assert.Equal(userRows.Count * productRows.Count, result.Count);
    }

    [Fact]
    public void InnerJoinOnConstantFalseReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new InnerJoinOnConstantFalseQuery().Value
            )
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void LeftJoinOnConstantFalsePadsEveryLeftRowOnce()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new LeftJoinOnConstantFalseQuery().Value
            )
        );

        Assert.Equal(userRows.Count, result.Count);

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FullJoinOnConstantFalseKeepsEverySideUnmatched()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                new FullJoinOnConstantFalseQuery().Value
            )
        );

        Assert.Equal(userRows.Count + productRows.Count, result.Count);
    }
}
