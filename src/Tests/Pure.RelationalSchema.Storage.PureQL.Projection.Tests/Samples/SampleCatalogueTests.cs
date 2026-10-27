using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples that carries an expected result
// returns exactly that result over the fixtures it was computed on, run in
// memory and run through the provider of the fixtures' tables.
public sealed class SampleCatalogueTests
{
    internal static readonly Dictionary<string, ILiteral> Parameters = new()
    {
        ["min_age"] = new LiteralDecimal(18m),
        ["min_total"] = new LiteralDecimal(100m),
        ["min_orders"] = new LiteralInteger(2),
        ["statuses"] = new ListLiteralString(["paid", "shipped"]),
        ["currency"] = new LiteralString("USD"),
        ["since"] = new LiteralDatetime(
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        ),
    };

    public static TheoryData<string> SamplesWithResult =>
        [
            .. Sample
                .All.Where(sample => sample.Result is not null)
                .Select(sample => sample.Name),
        ];

    public static TheoryData<string> SamplesWithParameters =>
        [
            .. Sample
                .All.Where(sample => sample.Result is null)
                .Select(sample => sample.Name),
        ];

    [Fact]
    public void CatalogueHoldsEightySixSamples()
    {
        Assert.Equal(86, Sample.All.Count);
    }

    [Theory]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleReturnsItsExpectedResult(string name)
    {
        Sample sample = Sample.Named(name);
        ExpectedTable expected = new(sample.Result!);

        PureQLProjection actual = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            sample.Query
        );

        if (sample.IsOrdered)
        {
            expected.AssertOrdered(actual);
        }
        else
        {
            expected.AssertUnordered(actual);
        }
    }

    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithUnboundParameterFailsWhenItRuns(string name)
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            Sample.Named(name).Query
        );

        _ = Assert.Throws<NotSupportedException>(() => projection.ToList());
    }

    // The catalogue gives no result for a sample with parameters, which is
    // only run with values bound for every parameter of every such sample.
    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithBoundParametersRuns(string name)
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            Sample.Named(name).Query,
            Parameters
        );

        Assert.Null(Record.Exception(() => projection.ToList()));
    }

    // The guard against running in memory: over tables a provider reads, the
    // query goes to the provider, whole, and no table is read in full.
    [Theory]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleRunsThroughTheProviderOfItsTablesWithoutReadingThem(string name)
    {
        Sample sample = Sample.Named(name);
        ExpectedTable expected = new(sample.Result!);
        RecordingProvider provider = new();
        IStoredSchemaDataSet[] datasets =
        [
            ProvidedTable.Schema(new SchemaDataSetWithForeignKeys(), provider),
            ProvidedTable.Schema(new AuditSchemaDataSet(), provider),
        ];

        PureQLProjection actual = new(datasets, sample.Query);

        if (sample.IsOrdered)
        {
            expected.AssertOrdered(actual);
        }
        else
        {
            expected.AssertUnordered(actual);
        }

        Assert.Equal(QueryExecution.Translated, actual.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.All(
            datasets.SelectMany(dataset => dataset.Values).Cast<ProvidedTable>(),
            table => Assert.Equal(0, table.Reads)
        );
    }

    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithBoundParametersRunsThroughTheProviderOfItsTables(string name)
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet[] datasets =
        [
            ProvidedTable.Schema(new SchemaDataSetWithForeignKeys(), provider),
            ProvidedTable.Schema(new AuditSchemaDataSet(), provider),
        ];

        PureQLProjection projection = new(datasets, Sample.Named(name).Query, Parameters);

        Assert.Null(Record.Exception(() => projection.ToList()));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.All(
            datasets.SelectMany(dataset => dataset.Values).Cast<ProvidedTable>(),
            table => Assert.Equal(0, table.Reads)
        );
    }
}
