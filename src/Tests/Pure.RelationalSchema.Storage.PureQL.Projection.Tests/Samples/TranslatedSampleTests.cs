using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples, over its fixtures stored in
// PostgreSQL, is translated by the provider of the stored tables to one SQL
// statement and returns the sample's result: the provider never refuses it,
// so it never falls back to running in memory. Skipped until the provider
// translates every sample: today it refuses all but three of them (parsing a
// cell's text, Guid.Parse, reading Guid, date, time and decimal values from a
// SQL result), and the projection runs them in memory. Remove the Skip to run
// them; Docker must be available.
public sealed class TranslatedSampleTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private const string NotYetTranslated =
        "Pure.RelationalSchema.Storage.PostgreSQL.Querying does not translate "
        + "every sample yet.";

    private readonly PostgreSqlDatabase _database = database;

    public static TheoryData<string> SamplesWithResult =>
        SampleCatalogueTests.SamplesWithResult;

    public static TheoryData<string> SamplesWithParameters =>
        SampleCatalogueTests.SamplesWithParameters;

    [Theory(Skip = NotYetTranslated)]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleIsTranslatedAndReturnsItsExpectedResult(string name)
    {
        Sample sample = Sample.Named(name);
        ExpectedTable expected = new(sample.Result!);

        PureQLProjection actual = new(_database.Datasets, sample.Query);

        if (sample.IsOrdered)
        {
            expected.AssertOrdered(actual);
        }
        else
        {
            expected.AssertUnordered(actual);
        }

        Assert.Null(actual.FallbackReason);
        Assert.Equal(QueryExecution.Translated, actual.Execution);
    }

    [Theory(Skip = NotYetTranslated)]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithBoundParametersIsTranslated(string name)
    {
        PureQLProjection projection = new(
            _database.Datasets,
            Sample.Named(name).Query,
            SampleCatalogueTests.Parameters
        );

        _ = projection.ToList();

        Assert.Null(projection.FallbackReason);
        Assert.Equal(QueryExecution.Translated, projection.Execution);
    }
}
