using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Aggregates;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// Aggregates folding a column of a table joined in from another schema:
// per-user login statistics from audit.logins joined onto shop.users, with
// a temporal max and a count over the joined side.
[Trait("Clause", "Select")]
[Trait("Feature", "CrossSchemaAggregate")]
public sealed class CrossSchemaJoinAggregateTests
{
    [Fact]
    public void PerUserMaxAndCountOverCrossSchemaLoginsFoldTheJoinedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<LoginRecord> loginRows = [.. new LoginRecords()];

        Query query = new PerUserMaxAndCountOverCrossSchemaLoginsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Dictionary<Guid, (DateTime, double)> expected = loginRows
            .GroupBy(login => login.LoginUserId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (
                        group.Max(login => login.LoginAt),
                        (double)group.Count()
                    )
            );

        Dictionary<Guid, (DateTime, double)> actual = result.Rows.ToDictionary(
            row => row.Uuid(new UserIdColumn().Name.TextValue)!.Value,
            row =>
                (
                    row.DateTime("lastLoginAt")!.Value,
                    row.Double("loginCount")!.Value
                )
        );

        Assert.Equal(expected, actual);
    }
}
