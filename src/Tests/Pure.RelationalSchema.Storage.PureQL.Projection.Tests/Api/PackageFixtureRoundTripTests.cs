using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;
using PureQL.CSharp.Model.Samples.Queries.Joins;
using PureQL.CSharp.Model.Samples.Queries.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Permanent guard for MIGRATION-PLAN.md's evidence table: pins the
// Pure.RelationalSchema.Storage.Samples package's cell-text contract, which is
// the one thing a future package bump could silently break for this
// translator (InvariantCellText's date/datetime/time/NULL rendering must stay
// TryParse-with-InvariantCulture-round-trippable; SchemaDataSetWithForeignKeys
// + AuditSchemaDataSet must keep enough row variety for real joins/groups).
[Trait("Feature", "PackageFixtureContract")]
public sealed class PackageFixtureRoundTripTests
{
    [Fact]
    public void TemporalColumnsRoundTripThroughStoredCellText()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new TemporalColumnsQuery().Value;

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        Assert.Equal(userRows.Count, result.Count);

        foreach (ResultRow row in result.Rows)
        {
            Guid userId = row.Uuid(new UserIdColumn().Name.TextValue)!.Value;
            UserRecord expected = userRows.Single(user => user.UserId == userId);

            Assert.Equal(expected.SignupDate, row.Date(new SignupDateColumn().Name.TextValue));
            Assert.Equal(expected.LastLogin, row.DateTime(new LastLoginColumn().Name.TextValue));
            Assert.Equal(expected.ShiftStart, row.Time(new ShiftStartColumn().Name.TextValue));
        }
    }

    [Fact]
    public void NullableColumnSurvivesAsNullForExactlyTheRecordsWithoutAValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new NullableScoreColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        Guid[] expectedNullIds =
        [
            .. userRows.Where(user => user.UserScore is null).Select(user => user.UserId),
        ];
        Guid[] actualNullIds =
        [
            .. result.Rows
                .Where(row => row.Double(new UserScoreColumn().Name.TextValue) is null)
                .Select(row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value),
        ];

        Assert.Equal(2, expectedNullIds.Length);
        Assert.Equal(expectedNullIds.OrderBy(id => id), actualNullIds.OrderBy(id => id));
    }

    [Fact]
    public void GroupByUserAgeYieldsTheRealDistinctAgeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new GroupByUserAgeQuery().Value;

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        double[] expectedAges =
        [
            .. userRows.Select(user => user.UserAge).Distinct().OrderBy(age => age),
        ];

        Assert.True(expectedAges.Length < userRows.Count);
        Assert.Equal(expectedAges.Length, result.Count);
        Assert.Equal(
            expectedAges,
            result.Column(new UserAgeColumn().Name.TextValue)
                .Select(age => double.Parse(age!))
                .OrderBy(age => age)
        );
    }

    [Fact]
    public void CrossSchemaJoinFromUsersToAuditLoginsMatchesGroundTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new CrossSchemaJoinFromUsersToLoginsQuery().Value;

        ProjectionResult result = new ProjectionResult(new PureQLProjection(datasets, query));

        string[] expected =
        [
            .. (
                from user in userRows
                join login in loginRows on user.UserId equals login.LoginUserId
                select user.UserName
            ).OrderBy(name => name),
        ];

        Assert.Equal(4, loginRows.Count);
        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }
}
