using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// A join on fields the query declares non-null, run over the fixtures stored
// in PostgreSQL through the provider of their tables, is one SQL statement
// whose join PostgreSQL hashes or merges: the keys are read without a null
// check, so the SQL compares them with =, which a hash or merge join serves,
// not with IS NOT DISTINCT FROM, which only a nested loop over every pair of
// rows can evaluate (issue #243). The fixture's session disables nested
// loops, so one in the plan means the planner had no other way. Needs Docker.
public sealed class TranslatedJoinTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private readonly PostgreSqlDatabase _database = database;

    [Fact]
    public void JoinOnFieldsDeclaredNonNullIsTranslatedToAHashOrMergeJoin()
    {
        Sample sample = Sample.Named("InnerJoinQuery");
        PureQLProjection projection = new(_database.Datasets, sample.Query);

        List<IRow> rows = [.. projection];
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        new ExpectedTable(sample.Result!).AssertUnordered(projection);
        Assert.Equal(sample.Result!.Count(), rows.Count);
        Assert.Matches("(Hash|Merge) Join", plan);
        Assert.Matches("(Hash|Merge) Cond:", plan);
        Assert.DoesNotContain("Nested Loop", plan);
        Assert.DoesNotContain("Join Filter", plan);
        Assert.DoesNotContain("IS NOT DISTINCT FROM", plan);
        Assert.Contains(" AS uuid) = CAST(", plan);
    }
}
