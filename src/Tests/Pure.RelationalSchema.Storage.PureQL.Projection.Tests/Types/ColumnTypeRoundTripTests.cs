using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;
using PureQL.CSharp.Model.Samples.Queries.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Types;

// Round-trips every stored column type through selection: the projected cell
// text must parse back to the ground-truth typed value for each of the seven
// PureQL value types. This also proves the CellText formatter and the
// translator's CellValueExtractor agree.
[Trait("Clause", "Select")]
[Trait("Feature", "ColumnTypeRoundTrip")]
public sealed class ColumnTypeRoundTripTests
{
    [Fact]
    public void UuidColumnRoundTrips()
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

    [Fact]
    public void StringColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new StringColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.UserName).ToArray(),
            result.Column(new UserNameColumn().Name.TextValue).ToArray()
        );
    }

    [Fact]
    public void DoubleColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DoubleColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (double?)user.UserAge).ToArray(),
            [.. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue))]
        );
    }

    [Fact]
    public void BoolColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new BoolColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (bool?)user.UserActive).ToArray(),
            [
                .. result.Rows.Select(
                    row => row.Bool(new UserActiveColumn().Name.TextValue)
                ),
            ]
        );
    }

    [Fact]
    public void DateColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DateColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (DateOnly?)user.SignupDate).ToArray(),
            [
                .. result.Rows.Select(
                    row => row.Date(new SignupDateColumn().Name.TextValue)
                ),
            ]
        );
    }

    [Fact]
    public void DateTimeColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new DateTimeColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (DateTime?)user.LastLogin).ToArray(),
            [
                .. result.Rows.Select(
                    row => row.DateTime(new LastLoginColumn().Name.TextValue)
                ),
            ]
        );
    }

    [Fact]
    public void TimeColumnRoundTrips()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new TimeColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => (TimeOnly?)user.ShiftStart).ToArray(),
            [
                .. result.Rows.Select(
                    row => row.Time(new ShiftStartColumn().Name.TextValue)
                ),
            ]
        );
    }
}
