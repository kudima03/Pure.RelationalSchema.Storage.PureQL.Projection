using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// Rounds out the GroupBy suite with pipeline combinations not already covered
// by GroupByOrderByTests, CrossEntityGroupByTests, HavingCompositeTests or
// Combined/AggregatePipelineTests: ordering + pagination over the
// group-projected rows, a GROUP BY key sourced from a joined (QualifiedColumn)
// table, a NULL-valued group key produced by an unmatched LEFT JOIN row, an
// empty group set that still carries a HAVING clause, and HAVING conditions
// nested 3+ levels deep.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByPipeline")]
public sealed class GroupByPipelineTests
{
    // GROUP BY the age key with no HAVING, ordered by the key itself
    // descending, then a pagination window taken over the group-projected
    // rows: exact ordered window is asserted (not just membership).
    [Fact]
    public void GroupByAgeDescOrderThenPaginateReturnsExactOrderedWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByAgeDescOrderThenPaginateQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge)
                .Distinct()
                .OrderByDescending(age => age)
                .Take(2),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(expected, actual);
    }

    // Skipping past every distinct key once ordered leaves an empty, but
    // still valid, paginated window.
    [Fact]
    public void GroupByOrderByThenPaginateSkippingPastAllGroupsReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new GroupByOrderByThenSkipPastAllGroupsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // GROUP BY key sourced from the joined side (Users.Active is tagged as a
    // QualifiedColumn because Users is the joined table here, not the FROM
    // table), aggregating base-table (Orders) values per group.
    [Fact]
    public void GroupByJoinedBooleanKeyAggregatesBaseTableTotalsPerSide()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new GroupByJoinedBooleanKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<bool, double> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => (user.UserActive, order.OrderTotal)
            )
            .GroupBy(pair => pair.UserActive)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.OrderTotal)
            );

        Dictionary<bool, double> actual = result.Rows.ToDictionary(
            row => row.Bool(new UserActiveColumn().Name.TextValue)!.Value,
            row => row.Double("totalByActive")!.Value
        );

        Assert.Equal(expected, actual);
    }

    // LEFT JOIN Orders onto Users: unmatched users (no orders) expose the
    // joined Orders.Total column as a NULL cell (CellValueExtractor parses
    // the padded empty cell back to a real null for a numeric field).
    // Grouping by that joined key collapses every unmatched row into a
    // single NULL-keyed group, distinct from the matched, per-total groups.
    [Fact]
    public void LeftJoinGroupByJoinedTotalKeyPlacesUnmatchedUsersInOwnNullGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new LeftJoinGroupByJoinedTotalKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> matchedUserIds =
        [
            .. orderRows.Select(order => order.OrderUserId),
        ];

        int distinctTotals = orderRows
            .Select(order => order.OrderTotal)
            .Distinct()
            .Count();

        double unmatchedAgeSum = userRows
            .Where(user => !matchedUserIds.Contains(user.UserId))
            .Sum(user => user.UserAge);

        // One group per distinct matched total, plus exactly one group for
        // every unmatched (NULL-total) user.
        Assert.Equal(distinctTotals + 1, result.Count);

        double? nullGroupAgeSum = result.Rows
            .Where(row => row.Double(new OrderTotalColumn().Name.TextValue) is null)
            .Select(row => row.Double("ageSum"))
            .SingleOrDefault();

        Assert.Equal(unmatchedAgeSum, nullGroupAgeSum);
    }

    // WHERE eliminates every row before GROUP BY runs, and a HAVING clause
    // is still present: the whole-set/grouped pipeline must short-circuit to
    // zero groups rather than evaluating HAVING against an empty group.
    [Fact]
    public void WhereEliminatesAllRowsGroupByWithHavingStillReturnsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new WhereMatchingNothingThenGroupByHavingQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // HAVING nested 4 levels deep: NOT( OR( AND(countGt, maxAtLeast),
    // NOT(minAtLeast) ) ).
    [Fact]
    public void HavingFourLevelNestedNotOrAndNotFiltersGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingFourLevelNestedNotOrAndNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                !(
                    (group.Count() > 1 && group.Max(order => order.OrderTotal) >= 200)
                    || !(group.Min(order => order.OrderTotal) >= 100)
                )
            );

        Assert.Equal(expected, result.Count);
    }

    // HAVING nested 3 levels deep with a different shape: AND( OR(a, NOT(b)),
    // OR(NOT(c), d) ).
    [Fact]
    public void HavingThreeLevelNestedAndOfTwoOrClausesFiltersGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingThreeLevelNestedAndOfTwoOrClausesQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                (group.Count() > 2 || !(group.Max(order => order.OrderTotal) >= 300))
                && (!group.Any() || group.Min(order => order.OrderTotal) >= 50)
            );

        Assert.Equal(expected, result.Count);
    }
}
