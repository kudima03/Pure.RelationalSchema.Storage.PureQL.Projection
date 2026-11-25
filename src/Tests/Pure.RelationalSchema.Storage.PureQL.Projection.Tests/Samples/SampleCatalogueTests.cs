using Pure.Primitives.Bool;
using Pure.Primitives.Date;
using Pure.Primitives.Number;
using Pure.Primitives.Time;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using DateTime = Pure.Primitives.DateTime.DateTime;
using Decimal = Pure.Primitives.Number.Decimal;
using Guid = Pure.Primitives.Guid.Guid;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples that carries an expected result
// returns exactly that result over the fixtures it was computed on, run in
// memory and run through the provider of the fixtures' tables.
public sealed class SampleCatalogueTests
{
    // A value for every parameter of every sample that has parameters, each
    // of the one type the catalogue gives it. The list parameters of
    // ListParametersThatMayBeEmptyQuery are bound to empty lists, the nullable
    // filters of OptionalFiltersSwitchedOffByNullQuery to typed nulls: the
    // case each is about.
    internal static readonly Dictionary<string, IParameter> Parameters = new()
    {
        ["min_age"] = new DecimalParameter(new Decimal(18m)),
        ["min_total"] = new DecimalParameter(new Decimal(100m)),
        ["min_orders"] = new IntegerParameter(new Long(2)),
        ["statuses"] = new StringListParameter([
            new String("paid"),
            new String("shipped"),
        ]),
        ["currency"] = new StringParameter(new String("USD")),
        ["since"] = new DateTimeParameter(
            new DateTime(new Date(new DateOnly(2024, 1, 1)))
        ),
        ["filter_by_status"] = new BooleanParameter(new True()),
        ["filter_by_user"] = new BooleanParameter(new False()),
        ["user_ids"] = new UuidListParameter([
            new Guid(new System.Guid("00000001-0000-0000-0000-000000000000")),
        ]),
        ["fraction"] = new DecimalParameter(new Decimal(0.5m)),
        ["group"] = new IntegerParameter(new Long(1)),
        ["zone"] = new StringParameter(new String("Europe/Berlin")),
        ["pattern"] = new StringParameter(new String("([a-z]+)")),
        ["separator"] = new StringParameter(new String(", ")),
        ["offset"] = new IntegerParameter(new Long(2)),
        ["buckets"] = new IntegerParameter(new Long(3)),
        ["page_size"] = new IntegerParameter(new Long(2)),
        ["skipped_users"] = new IntegerParameter(new Long(1)),
        ["user_id"] = new UuidParameter(
            new Guid(new System.Guid("00000001-0000-0000-0000-000000000000"))
        ),
        ["status"] = new NullableStringParameter(),
        ["placed_since"] = new NullableDateTimeParameter(),
        ["tax_rate"] = new DecimalParameter(new Decimal(0.2m)),
        ["as_of"] = new DateParameter(new Date(new DateOnly(2024, 3, 1))),
        ["note"] = new StringParameter(new String("net of tax")),
        ["counts"] = new IntegerListParameter([new Long(1), new Long(2)]),
        ["round_totals"] = new DecimalListParameter([
            new Decimal(100m),
            new Decimal(250.5m),
        ]),
        ["use_integer"] = new BooleanParameter(new True()),
        ["use_decimal"] = new BooleanParameter(new True()),
        ["use_string"] = new BooleanParameter(new True()),
        ["use_boolean"] = new BooleanParameter(new True()),
        ["use_date"] = new BooleanParameter(new True()),
        ["use_time"] = new BooleanParameter(new True()),
        ["use_datetime"] = new BooleanParameter(new True()),
        ["use_uuid"] = new BooleanParameter(new True()),
        ["integers"] = new IntegerListParameter([]),
        ["decimals"] = new DecimalListParameter([]),
        ["strings"] = new StringListParameter([]),
        ["booleans"] = new BooleanListParameter([]),
        ["dates"] = new DateListParameter([]),
        ["times"] = new TimeListParameter([]),
        ["datetimes"] = new DateTimeListParameter([]),
        ["uuids"] = new UuidListParameter([]),
        ["amount"] = new NullableIntegerParameter(),
        ["label"] = new StringParameter(new String("from parameters")),
        ["flag"] = new BooleanParameter(new True()),
        ["day"] = new DateParameter(new Date(new DateOnly(2024, 2, 29))),
        ["at"] = new DateTimeParameter(
            new DateTime(new Date(new DateOnly(2024, 2, 29)), new Time(new TimeOnly(12, 0)))
        ),
        ["time"] = new TimeParameter(new Time(new TimeOnly(23, 59, 59))),
        ["id"] = new UuidParameter(
            new Guid(new System.Guid("00000002-0000-0000-0000-000000000000"))
        ),
    };

    public static TheoryData<string> SamplesWithResult =>
        [
            .. Sample
                .All.Where(sample => sample.Result is not null)
                .Select(sample => sample.Name),
        ];

    // The samples that read no stored table (only inline rows), and so run
    // in memory: there is no provider to give them to.
    internal static readonly string[] ReadingNoStoredTable =
    [
        "BisectionOfARateQuery",
        "FoldCaseCornersQuery",
        "FoldCaseInATotalQuery",
        "GreatCircleDistanceQuery",
        "GroupedNextOverTotalAndCombinedSeedsQuery",
        "GroupingByNormalizedKeysQuery",
        "GroupingWithoutCaseQuery",
        "InlineRowsOfEveryCellTypeQuery",
        "QuadrantsOfTwoInlineListsQuery",
        "RateOfReturnByIterationQuery",
        "RoundAndTruncateKeepTheOperandTypeQuery",
        "RoundedIntegersAcrossGroupedClausesQuery",
        "RoundedIntegersFromSubqueriesQuery",
        "WalkThatStopsAtACycleQuery",
    ];

    public static TheoryData<string> SamplesWithParameters =>
        [
            .. Sample
                .All.Where(sample => sample.Result is null)
                .Select(sample => sample.Name),
        ];

    [Fact]
    public void CatalogueHoldsThreeHundredEightySixSamples()
    {
        Assert.Equal(386, Sample.All.Count);
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

        if (ReadingNoStoredTable.Contains(name))
        {
            Assert.Equal(QueryExecution.InMemory, actual.Execution);
            Assert.Null(actual.FallbackReason);
            Assert.Empty(provider.Queries);
            return;
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

        if (ReadingNoStoredTable.Contains(name))
        {
            Assert.Equal(QueryExecution.InMemory, projection.Execution);
            Assert.Empty(provider.Queries);
            return;
        }

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.All(
            datasets.SelectMany(dataset => dataset.Values).Cast<ProvidedTable>(),
            table => Assert.Equal(0, table.Reads)
        );
    }
}
