using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

[Trait("Clause", "Select")]
[Trait("Feature", "SelectColumns")]
public sealed class SelectColumnsTests
{
    [Fact]
    public void SelectSingleStringColumnReturnsThatColumnForEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectOrderStatusQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Equal([new OrderStatusColumn().Name.TextValue], result.ColumnNames);
        Assert.Equal(
            [.. orderRows.Select(order => order.OrderStatus)],
            result.Column(new OrderStatusColumn().Name.TextValue)
        );
    }

    [Fact]
    public void SelectMultipleColumnsProjectsAllOfThemPreservingRowOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new SelectMultipleColumnsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(orderRows.Count, result.Count);
        Assert.Contains(new OrderStatusColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new OrderTotalColumn().Name.TextValue, result.ColumnNames);
        Assert.Equal(
            orderRows.Select(order => (double?)order.OrderTotal).ToArray(),
            [.. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void SelectUuidColumnRoundTripsEachIdentifier()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new SelectUuidColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (Guid?)user.UserId).ToArray(),
            [.. result.Rows.Select(row => row.Uuid(new UserIdColumn().Name.TextValue))]
        );
    }
}
