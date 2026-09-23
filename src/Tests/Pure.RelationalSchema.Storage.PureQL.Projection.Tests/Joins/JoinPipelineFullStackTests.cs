using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Every clause at once, layered on each JoinType in turn: JOIN -> WHERE ->
// GROUP BY -> HAVING -> ORDER BY -> DISTINCT -> pagination. Extends
// Combined/FullPipelineTests.cs (INNER JOIN only) to LEFT/RIGHT/FULL, and
// adds a cross-schema variant against audit.logins for breadth.
//
// Selecting only the aggregate (no group key in the output) makes DISTINCT
// do real work here: Ann and Cara both place 2 orders, so their projected
// group rows (orderCount = 2) collapse into a single distinct row once
// DISTINCT runs, alongside Dan's (orderCount = 1). Fay (active, but no
// orders) forms its own zero-count group, which HAVING then drops - so the
// outer-join-only "empty group" case is exercised here too.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinPipelineCombo")]
public sealed class JoinPipelineFullStackTests
{
    // Active users are Ann, Cara, Dan, Fay (Bob and Eve are inactive and
    // dropped by WHERE). Ann and Cara each place 2 orders, Dan places 1,
    // and Fay places none - Fay's zero-order group is dropped by HAVING.
    // Distinct group counts, sorted desc, are therefore [2, 1]; skipping
    // the first (2) and taking up to 5 leaves exactly [1].
    private static double[] ExpectedDistinctOrderCountsSkippingFirst(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. userRows
                .Where(user => user.UserActive)
                .Select(user =>
                    (double)orderRows.Count(order => order.OrderUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Skip(1)
                .Take(5),
        ];
    }

    [Fact]
    public void InnerJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new RightJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FullJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected = ExpectedDistinctOrderCountsSkippingFirst(userRows, orderRows);

        double[] actual = [.. result.Rows.Select(row => row.Double("orderCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    // Cross-schema breadth: shop.users <-> audit.logins. Ann has 2 logins,
    // Bob and Eve have 1 each, Cara/Dan/Fay have none. WHERE keeps every
    // row (bare true), GROUP BY the user, HAVING requires at least one
    // login, ORDER BY the count desc, DISTINCT collapses Bob's and Eve's
    // tied count of 1 into a single row alongside Ann's 2, and pagination
    // takes just the top entry.
    [Fact]
    public void CrossSchemaLeftJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new CrossSchemaLeftJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows
                .Select(user =>
                    (double)loginRows.Count(login => login.LoginUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Take(1),
        ];

        double[] actual = [.. result.Rows.Select(row => row.Double("loginCount")!.Value)];

        Assert.Equal(expected, actual);
    }

    // Cross-schema variant with an INNER JOIN: every emitted group already
    // has at least one login by construction, so HAVING is a pass-through
    // and the interesting behaviour is DISTINCT collapsing Bob's and Eve's
    // tied login count after ORDER BY + pagination select the full set.
    [Fact]
    public void CrossSchemaInnerJoinFullPipelineComposesEveryClauseInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new CrossSchemaInnerJoinFullPipelineQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows
                .Select(user =>
                    (double)loginRows.Count(login => login.LoginUserId == user.UserId)
                )
                .Where(count => count >= 1)
                .Distinct()
                .OrderByDescending(count => count)
                .Take(5),
        ];

        double[] actual = [.. result.Rows.Select(row => row.Double("loginCount")!.Value)];

        Assert.Equal(expected, actual);
    }
}
