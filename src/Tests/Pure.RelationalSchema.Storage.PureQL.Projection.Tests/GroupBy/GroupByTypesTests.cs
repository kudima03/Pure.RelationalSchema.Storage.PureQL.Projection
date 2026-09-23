using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY on the remaining key types (number, date, datetime, time),
// complementing GroupByTests (string / bool / uuid / composite). Each projects
// its grouping key and yields one row per distinct value.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByTypes")]
public sealed class GroupByTypesTests
{
    [Fact]
    public void GroupByNumberKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByUserAgeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double[] expected =
        [
            .. userRows.Select(user => user.UserAge).Distinct().OrderBy(v => v),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(new UserAgeColumn().Name.TextValue)!.Value)
                .OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByDateKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByDateKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.SignupDate).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByDateTimeKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByDateTimeKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.LastLogin).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByTimeKeyYieldsOneRowPerDistinctValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        Query query = new GroupByTimeKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Select(user => user.ShiftStart).Distinct().Count(),
            result.Count
        );
    }
}
