using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples, over its fixtures stored in
// PostgreSQL, returns the sample's result through the provider of the stored
// tables: translated to one SQL statement, or, for the samples the provider
// cannot translate yet (Refused), refused with NotSupportedException and run
// in memory over the stored tables. Needs Docker.
public sealed class TranslatedSampleTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private readonly PostgreSqlDatabase _database = database;

    // The samples Pure.RelationalSchema.Storage.PostgreSQL.Querying
    // 0.1.0-preview.0.10.0 refuses with NotSupportedException, so that they
    // run in memory: they hold a function or a query operator of this library
    // (a window, a recursion, the string, math, time and conversion
    // functions), inline rows, or a shape of the BCL its translator does not
    // know yet (string.Join, a DateOnly built from parts, the Max of an
    // array, an ordered FirstOrDefault over a group, ...). A sample that leaves
    // this list when the provider learns its constructs fails the test, and
    // is removed from it.
    internal static readonly HashSet<string> Refused =
    [
        "AbsSignAndTruncateQuery",
        "ActivityTimelineQuery",
        "AgeBandsQuery",
        "AncestorsQuery",
        "CalendarOfDaysQuery",
        "ConcatAllPerTenantQuery",
        "ConversionsQuery",
        "CovarianceAndCorrelationPerTenantQuery",
        "DailyRevenueTrendQuery",
        "DatePartsQuery",
        "DatesFromPartsQuery",
        "DecimalRemaindersQuery",
        "DescendantsQuery",
        "ExplicitWideningQuery",
        "FirstAndLastPerUserQuery",
        "FoldCaseQuery",
        "GreatestAndLeastQuery",
        "GroupByMonthQuery",
        "HourAsAnAngleQuery",
        "InlineStatusRowsQuery",
        "LiftedOverEveryOperandQuery",
        "LocalBusinessDayQuery",
        "LocalMonthsQuery",
        "LocalTimesAsInstantsQuery",
        "LocalWeekdayAndHourQuery",
        "MonthsBetweenQuery",
        "MonthsInALocalCalendarQuery",
        "MovingAveragesQuery",
        "NormalizeQuery",
        "PaddingAndJoiningQuery",
        "PartitionTotalsQuery",
        "PowersAndLogarithmsQuery",
        "PreviousAndNextOrderQuery",
        "RangeFramesQuery",
        "RankingQuery",
        "ReadingTextQuery",
        "RegularExpressionsQuery",
        "RemoveDiacriticsQuery",
        "ReplacePositionSplitPartQuery",
        "RowExpressionsInWindowsQuery",
        "RunningTotalOfDailySumsQuery",
        "RunningTotalsQuery",
        "StatisticsPerStatusQuery",
        "StringLengthQuery",
        "StringMatchingQuery",
        "SubstringQuery",
        "TemporalAveragesPerUserQuery",
        "TopOrdersPerTenantQuery",
        "TreeInReadingOrderQuery",
        "TreeRollUpQuery",
        "TrimQuery",
        "UpperLowerQuery",
        "WholeNumbersQuery",
    ];

    public static TheoryData<string> SamplesWithResult =>
        SampleCatalogueTests.SamplesWithResult;

    // Run through the provider of the stored tables: translated, or refused
    // with NotSupportedException and run in memory, as Execution says; a
    // sample that reads no stored table runs in memory with no provider.
    internal static void AssertRan(string name, PureQLProjection projection)
    {
        if (SampleCatalogueTests.ReadingNoStoredTable.Contains(name))
        {
            Assert.Null(projection.FallbackReason);
            Assert.Equal(QueryExecution.InMemory, projection.Execution);
        }
        else if (Refused.Contains(name))
        {
            _ = Assert.IsType<NotSupportedException>(projection.FallbackReason);
            Assert.Equal(QueryExecution.InMemory, projection.Execution);
        }
        else
        {
            Assert.Null(projection.FallbackReason);
            Assert.Equal(QueryExecution.Translated, projection.Execution);
        }
    }

    public static TheoryData<string> SamplesWithParameters =>
        SampleCatalogueTests.SamplesWithParameters;

    [Theory]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleReturnsItsExpectedResultTranslatedUnlessRefused(string name)
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

        AssertRan(name, actual);
    }

    [Theory]
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
