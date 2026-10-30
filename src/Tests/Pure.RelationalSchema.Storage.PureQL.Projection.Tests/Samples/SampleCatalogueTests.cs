using Pure.Primitives.Date;
using Pure.Primitives.Number;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using DateTime = Pure.Primitives.DateTime.DateTime;
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples that carries an expected result
// returns exactly that result over the fixtures it was computed on, run in
// memory and run through the provider of the fixtures' tables.
public sealed class SampleCatalogueTests
{
    internal static readonly Dictionary<string, IParameter> Parameters = new()
    {
        ["min_age"] = new DecimalParameter(new Decimal(18m)),
        ["min_total"] = new DecimalParameter(new Decimal(100m)),
        ["min_orders"] = new IntegerParameter(new Long(2)),
        ["statuses"] = new StringListParameter([new String("paid"), new String("shipped")]),
        ["currency"] = new StringParameter(new String("USD")),
        ["since"] = new DateTimeParameter(new DateTime(new Date(new DateOnly(2024, 1, 1)))),
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
    public void SampleWithUnboundParameterIsRejectedWhenItIsBuilt(string name)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
                Sample.Named(name).Query
            )
        );
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
