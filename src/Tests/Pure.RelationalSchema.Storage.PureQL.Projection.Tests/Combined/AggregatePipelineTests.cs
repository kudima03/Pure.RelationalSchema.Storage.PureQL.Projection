using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Combined;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Aggregates combined with the rest of the pipeline: WHERE narrows the rows
// folded by GROUP BY, joins (same-schema and cross-schema) feed aggregates,
// and whole-set aggregates (no groupBy) compose with a preceding WHERE.
[Trait("Clause", "Combined")]
[Trait("Feature", "AggregatePipeline")]
public sealed class AggregatePipelineTests
{
    [Fact]
    public void WhereThenGroupByAggregatesOnlyFilteredRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SumOfShippedOrdersPerUserQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = orderRows
            .Where(order => order.OrderStatus == "shipped")
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value,
            row => row.Double("filteredSum")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JoinThenGroupByProjectsAggregatePerJoinedGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new JoinThenGroupByQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<string, double> expected = orderRows
            .Join(
                userRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => user.UserName
            )
            .GroupBy(name => name)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[new UserNameColumn().Name.TextValue]!,
            row => row.Double("orderCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CrossSchemaJoinThenGroupByCountsPerUser()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new CrossSchemaJoinThenGroupByQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, double> expected = loginRows
            .GroupBy(login => login.LoginUserId)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row => row.Double("loginCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByHavingOrderByPaginationComposeInOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new GroupByHavingOrderByPaginationQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string Status, double Sum)[] expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Sum(order => order.OrderTotal) > 0)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Sum(order => order.OrderTotal)))
                .Skip(1)
                .Take(1),
        ];

        (string Status, double Sum)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[new OrderStatusColumn().Name.TextValue]!, row.Double("statusSum")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetAggregateOverFilteredRowsProjectsSingleRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WholeSetAggregateOverFilteredRowsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expected = orderRows
            .Where(order => order.OrderStatus == "shipped")
            .Sum(order => order.OrderTotal);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("filteredSum"));
    }

    [Fact]
    public void WholeSetCountProjectsSingleRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WholeSetCountQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("total"));
    }

    [Fact]
    public void WholeSetMinAndMaxStringProjectInOneRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new WholeSetMinAndMaxStringQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string expectedMin = orderRows
            .Select(order => order.OrderStatus)
            .Min(StringComparer.Ordinal)!;
        string expectedMax = orderRows
            .Select(order => order.OrderStatus)
            .Max(StringComparer.Ordinal)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expectedMin, result.Row(0)["minStatus"]);
        Assert.Equal(expectedMax, result.Row(0)["maxStatus"]);
    }

    [Fact]
    public async Task AsyncEnumerationYieldsGroupedAggregateRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new GroupKeyAndSumQuery().Value;

        PureQLProjection projection = new PureQLProjection(datasets, query);

        Dictionary<Guid, double> actual = [];
        await foreach (IRow row in projection)
        {
            Guid userId = default;
            double total = 0;
            foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
            {
                if (cell.Key.Name.TextValue == new OrderUserIdColumn().Name.TextValue)
                {
                    userId = Guid.Parse(cell.Value.Value.TextValue);
                }
                else if (cell.Key.Name.TextValue == "userTotal")
                {
                    total = double.Parse(
                        cell.Value.Value.TextValue,
                        CultureInfo.InvariantCulture
                    );
                }
            }

            actual[userId] = total;
        }

        Dictionary<Guid, double> expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Assert.Equal(expected, actual);
    }
}
