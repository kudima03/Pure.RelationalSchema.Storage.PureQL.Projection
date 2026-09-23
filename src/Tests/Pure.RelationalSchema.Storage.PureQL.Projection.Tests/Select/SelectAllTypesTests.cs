using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Projecting all seven typed columns of a table in a single query: every column
// is present and each cell round-trips to its ground-truth typed value.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAllTypes")]
public sealed class SelectAllTypesTests
{
    [Fact]
    public void SelectAllUserColumnsProjectsEveryTypedColumn()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new SelectAllUserColumnsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(userRows.Count, result.Count);
        Assert.Contains(new UserIdColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new UserNameColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new UserAgeColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new UserActiveColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new SignupDateColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new LastLoginColumn().Name.TextValue, result.ColumnNames);
        Assert.Contains(new ShiftStartColumn().Name.TextValue, result.ColumnNames);

        UserRecord first = userRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.UserId, row.Uuid(new UserIdColumn().Name.TextValue));
        Assert.Equal(first.UserName, row[new UserNameColumn().Name.TextValue]);
        Assert.Equal(first.UserAge, row.Double(new UserAgeColumn().Name.TextValue));
        Assert.Equal(first.UserActive, row.Bool(new UserActiveColumn().Name.TextValue));
        Assert.Equal(first.SignupDate, row.Date(new SignupDateColumn().Name.TextValue));
        Assert.Equal(first.LastLogin, row.DateTime(new LastLoginColumn().Name.TextValue));
        Assert.Equal(first.ShiftStart, row.Time(new ShiftStartColumn().Name.TextValue));
    }
}
