using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Matrix: each JoinType (Inner/Left/Right/Full) layered with WHERE and with
// GROUP BY + HAVING on top of the joined rows. Expectations are computed
// independently with GroupJoin/DefaultIfEmpty so outer-join padded rows are
// modelled the way SQL would (NULL on the unmatched side; a numeric/uuid
// comparison against NULL is unknown and drops the row from WHERE/HAVING).
[Trait("Clause", "Join")]
[Trait("Feature", "JoinPipelineCombo")]
public sealed class JoinPipelineWhereHavingTests
{
    // Every order with total >= 100, independent of unmatched-row padding:
    // Ann/101, Bob/103, Cara/105, Dan/106 survive; Ann/102 (50) and
    // Cara/104 (75.25) do not.
    private static string[] ExpectedNamesWithTotalAtLeast100(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. orderRows
                .Where(order => order.OrderTotal >= 100)
                .Select(order =>
                    userRows.Single(user => user.UserId == order.OrderUserId).UserName
                )
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void InnerJoinThenEachWhereOnJoinedTotalKeepsOnlyQualifyingOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinThenEachWhereOnJoinedTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(userRows, orderRows);

        string?[] actual =
        [
            .. result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinThenEachWhereOnJoinedTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // The padded rows for Eve/Fay carry a NULL total; NULL >= 100 is
        // unknown in SQL, so WHERE drops them exactly like every real row
        // that fails the threshold.
        string[] expected = ExpectedNamesWithTotalAtLeast100(userRows, orderRows);

        string?[] actual =
        [
            .. result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new RightJoinThenEachWhereOnJoinedTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(userRows, orderRows);

        string?[] actual =
        [
            .. result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinThenEachWhereOnJoinedTotalExcludesUnmatchedPaddedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FullJoinThenEachWhereOnJoinedTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected = ExpectedNamesWithTotalAtLeast100(userRows, orderRows);

        string?[] actual =
        [
            .. result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name),
        ];

        Assert.Equal(expected, actual);
    }

    // Extends OuterJoinAggregationTests (LEFT JOIN only) to RIGHT JOIN: an
    // unmatched right-side user still forms its own group with a real key
    // and a zero/NULL aggregate, exercising JoinApplicator.RightJoin's own
    // padding branch rather than LeftJoin's.
    [Fact]
    public void RightJoinGroupByUserCountsZeroAndSumsNullForUnmatchedUsers()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new RightJoinGroupByUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (double Count, double? Sum)> expected = userRows.ToDictionary(
            user => user.UserId,
            user =>
            {
                List<OrderRecord> matched =
                [
                    .. orderRows.Where(order => order.OrderUserId == user.UserId),
                ];
                return (
                    (double)matched.Count,
                    matched.Count == 0
                        ? (double?)null
                        : matched.Sum(order => order.OrderTotal)
                );
            }
        );

        Dictionary<Guid, (double Count, double? Sum)> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row => (row.Double("orderCount")!.Value, row.Double("totalSum"))
        );

        Assert.Equal(expected, actual);
    }

    // Same as above but for FULL JOIN, exercising JoinApplicator.FullJoin's
    // right-unmatched padding branch.
    [Fact]
    public void FullJoinGroupByUserCountsZeroAndSumsNullForUnmatchedUsers()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FullJoinGroupByUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (double Count, double? Sum)> expected = userRows.ToDictionary(
            user => user.UserId,
            user =>
            {
                List<OrderRecord> matched =
                [
                    .. orderRows.Where(order => order.OrderUserId == user.UserId),
                ];
                return (
                    (double)matched.Count,
                    matched.Count == 0
                        ? (double?)null
                        : matched.Sum(order => order.OrderTotal)
                );
            }
        );

        Dictionary<Guid, (double Count, double? Sum)> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row => (row.Double("orderCount")!.Value, row.Double("totalSum"))
        );

        Assert.Equal(expected, actual);
    }

    // GROUP BY + HAVING(sum) on top of the join: Ann/Bob/Cara's sums clear
    // the 150 bar, Dan's (100.50) does not, and unmatched users' NULL sum
    // is unknown against ">= 150" so they are excluded exactly like Dan.
    private static HashSet<Guid> ExpectedUsersWithTotalAtLeast150(
        IReadOnlyList<UserRecord> userRows,
        IReadOnlyList<OrderRecord> orderRows
    )
    {
        return
        [
            .. userRows
                .Where(user =>
                    orderRows.Any(order => order.OrderUserId == user.UserId)
                    && orderRows
                        .Where(order => order.OrderUserId == user.UserId)
                        .Sum(order => order.OrderTotal)
                        >= 150
                )
                .Select(user => user.UserId),
        ];
    }

    [Fact]
    public void InnerJoinGroupByHavingSumFiltersGroupsBelowThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new InnerJoinGroupByHavingSumQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(userRows, orderRows);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeftJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinGroupByHavingSumQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(userRows, orderRows);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RightJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new RightJoinGroupByHavingSumQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(userRows, orderRows);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullJoinGroupByHavingSumExcludesUnmatchedNullGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FullJoinGroupByHavingSumQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected = ExpectedUsersWithTotalAtLeast150(userRows, orderRows);

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }
}
