using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;

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
    // 0.1.0-preview.0.12.0 (translator Linq.Expressions.Translation.PostgreSql
    // 0.1.0-preview.0.10.0) refuses with NotSupportedException, so that they
    // run in memory: they hold a function or a query operator of this library
    // (a window, a recursion, the string, math, time, conversion and
    // statistics functions), the Min of uuids, which PostgreSQL orders
    // otherwise than .NET, or a shape of the BCL its translator does not know
    // yet (an int conversion of a Math.Min, ...). A sample that leaves this
    // list when the provider learns its constructs fails the test, and is
    // removed from it.
    internal static readonly HashSet<string> Refused =
    [
        "AbsentAndNullableOperandsQuery",
        "AbsSignAndTruncateInATotalQuery",
        "AbsSignAndTruncateQuery",
        "AddedAmountsInAGroupedQuery",
        "AddingAndTakingTheDifferenceQuery",
        "AgeBandsQuery",
        "AggregatesOverExactValuesQuery",
        "AncestorsQuery",
        "AsNullableOfEveryTypeQuery",
        "AsStringOfEveryTypeQuery",
        "AsStringOfOperatorResultsQuery",
        "BlankValuesOfEveryTypeAsNullQuery",
        "CircularMeanHourPerUserQuery",
        "CodesMatchedByRegexQuery",
        "ComputedKeysSelectorsAndDefaultsInWindowsQuery",
        "ConcatAllInATotalQuery",
        "ConcatAllNamesPerCountryQuery",
        "ConcatAllRepeatedOnEveryRowQuery",
        "ConstantOperandsNeverNullableQuery",
        "ContextsOfRecursiveStepsQuery",
        "ConversionsInATotalQuery",
        "ConversionsQuery",
        "CountDistinctInATotalQuery",
        "CountDistinctOverWindowsQuery",
        "CountDistinctSelectorKindsQuery",
        "CovarianceAndCorrelationPerTenantQuery",
        "DailyRevenueTrendQuery",
        "DatePartsQuery",
        "DatetimeZoneKindsQuery",
        "DecimalRemainderCornersQuery",
        "DecimalRemaindersInAGroupedQuery",
        "DecimalRemaindersInATotalQuery",
        "DecimalRemaindersQuery",
        "DeclaredOperatorTypesInGroupsQuery",
        "DescendantsQuery",
        "DurationsInPartsQuery",
        "EveryAggregateOverARangeQuery",
        "EveryAggregateOverWindowsQuery",
        "EveryOperatorSignatureInAGroupedSelectQuery",
        "EveryOperatorSignatureInAPlainSelectQuery",
        "EveryOperatorSignatureInATotalQuery",
        "ExplicitWideningQuery",
        "FixedWidthReportLinesQuery",
        "FoldCaseQuery",
        "FrameOfEveryFormQuery",
        "FromPartsInAGroupedQuery",
        "FromPartsInATotalQuery",
        "FromPartsOverWindowsQuery",
        "GreatestAndLeastOfEveryTypeQuery",
        "GreatestAndLeastOverAggregatesQuery",
        "GroupByMonthQuery",
        "GroupedSortByColumnsAndExpressionsQuery",
        "GroupingWithoutAccentsQuery",
        "GrowthPerUserQuery",
        "HierarchyWalkWithAggregatedStepsQuery",
        "HourAsAnAngleQuery",
        "JoinOnNormalizedSkuQuery",
        "LastTwelveMonthsPerUserQuery",
        "LiftedOverEveryOperandQuery",
        "LocalBusinessDayQuery",
        "LocalDatesGroupedAndTotalledQuery",
        "LocalDatesInEveryClauseQuery",
        "LocalDayBucketsQuery",
        "LocalInstantRoundTripsQuery",
        "LocalInstantsInEveryClauseQuery",
        "LocalInstantsInGroupedAndTotalQueriesQuery",
        "LocalMonthIdentitiesQuery",
        "LocalMonthKindsQuery",
        "LocalMonthsInEveryClauseQuery",
        "LocalMonthsInGroupedAndTotalQueriesQuery",
        "LocalMonthsQuery",
        "LocalPartKindsQuery",
        "LocalPartsInConditionsQuery",
        "LocalPartsInEveryClauseQuery",
        "LocalPartsInGroupedAndTotalQueriesQuery",
        "LocalPeriodStartKindsQuery",
        "LocalPeriodStartsInEveryClauseQuery",
        "LocalPeriodStartsInGroupedAndTotalQueriesQuery",
        "LocalTimesAsInstantsQuery",
        "LocalWeekdayAndHourQuery",
        "MatchingWithoutCaseOverJoinsQuery",
        "MonthArithmeticOfLocalDatesQuery",
        "MonthsBetweenQuery",
        "MonthsInALocalCalendarQuery",
        "MovingAveragesQuery",
        "NormalizeCornersQuery",
        "NormalizeInATotalQuery",
        "NormalizeMatchingOverJoinsQuery",
        "NormalizeQuery",
        "NullableOperandsByRoleQuery",
        "NullableRoleOperandsInAGroupedQuery",
        "NullableRoleOperandsInATotalQuery",
        "NumericFunctionCornersQuery",
        "NumericFunctionsInEveryClauseQuery",
        "OperatorsOverAListInEveryContextQuery",
        "OperatorsWithAndWithoutAZoneQuery",
        "OrderLabelsOverJoinsQuery",
        "OrdersPerDayOfADatetimeQuery",
        "PaddingAndJoiningCornersQuery",
        "PaddingAndJoiningInATotalQuery",
        "PaddingAndJoiningQuery",
        "PagedSetOperationInputsQuery",
        "PairStatisticsInAGroupedQuery",
        "PairStatisticsInATotalQuery",
        "PairStatisticsOperandKindsQuery",
        "PairStatisticsOverWindowsQuery",
        "PartitionTotalsQuery",
        "PartsAndBackQuery",
        "PartsLiftedOverEveryPartQuery",
        "PathCountsPerDepthQuery",
        "PowersAndLogarithmsCornersQuery",
        "PowersAndLogarithmsInEveryClauseQuery",
        "PowersAndLogarithmsQuery",
        "PreviousAndNextOrderQuery",
        "PriceDeviationPerProductQuery",
        "PriceStatisticsPerProductQuery",
        "RangeDistancesOverDatesQuery",
        "RangeDistancesOverNumbersQuery",
        "RangeFramesQuery",
        "RankingQuery",
        "ReadingTextInATotalQuery",
        "ReadingTextOfEveryTypeQuery",
        "ReadingTextQuery",
        "RecursiveColumnTypesQuery",
        "RecursiveNextWithJoinsAndAliasesQuery",
        "RecursiveSeedsOfEveryKindQuery",
        "RecursiveSubqueryReadersQuery",
        "RegressionInAGroupedQuery",
        "RegressionInATotalQuery",
        "RegressionOperandKindsQuery",
        "RegressionOverWindowsQuery",
        "RegularExpressionDialectQuery",
        "RegularExpressionsInATotalQuery",
        "RegularExpressionsQuery",
        "RemoveDiacriticsCornersQuery",
        "RemoveDiacriticsInATotalQuery",
        "RemoveDiacriticsMatchingOverJoinsQuery",
        "RemoveDiacriticsQuery",
        "ReplacePositionSplitPartCornersQuery",
        "ReplacePositionSplitPartInATotalQuery",
        "ReplacePositionSplitPartQuery",
        "RoundedAverageAndQuotientPerTenantQuery",
        "RoundedIntegersAsIntegerOperandsQuery",
        "RoundHalfToEvenCornersQuery",
        "RoundHalfToEvenInATotalQuery",
        "RoundHalfToEvenOverWindowsQuery",
        "RoundingDecimalResultsQuery",
        "RoundingIntegersToTensAndHundredsQuery",
        "RowExpressionsInWindowsQuery",
        "RunningTotalOfDailySumsQuery",
        "RunningTotalsQuery",
        "ScoresAgainstTheDistributionQuery",
        "SortingBySelectWindowColumnsQuery",
        "StandardDeviationByHandQuery",
        "StatisticsForEveryTypeQuery",
        "StatisticsFromAGroupedSubqueryQuery",
        "StatisticsPerStatusQuery",
        "StreaksAndFirstValuesQuery",
        "StringLengthQuery",
        "StringMatchingQuery",
        "SubstringQuery",
        "TemporalAveragesPerUserQuery",
        "TemporalMeansInATotalQuery",
        "TemporalMeansOfComputedValuesQuery",
        "TemporalMeansOverWindowsQuery",
        "TemporalMeansWithPredicatesQuery",
        "TextKeysJoinedToTypedKeysQuery",
        "TopOrdersPerTenantQuery",
        "TreeInReadingOrderQuery",
        "TreeRollUpQuery",
        "TrigonometricIdentitiesQuery",
        "TrigonometryOverAggregatesInATotalQuery",
        "TrimQuery",
        "TwoRecursiveSubqueriesQuery",
        "UnparsedPostalCodesPerCountryQuery",
        "UpperLowerQuery",
        "UsersPerEmailDomainByRegexQuery",
        "UsersPerEmailDomainQuery",
        "WholeNumbersQuery",
        "WidenedSeedColumnsOfARecursionQuery",
        "WindowFunctionsByKindQuery",
        "WindowsInEveryPositionQuery",
        "WindowsKeyedByRowExpressionsQuery",
        "WindowsOverNullableFieldsQuery",
        "ZoneExpressionKindsQuery",
        "ZoneFreeOperatorsInEveryClauseQuery",
        "ZoneNameFormsQuery",
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

    // The catalogue gives no result for a sample with parameters: over the
    // stored fixtures, it returns what it returns over the same fixtures held
    // in memory, with the same values bound.
    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithBoundParametersReturnsTheInMemoryResultTranslatedUnlessRefused(
        string name
    )
    {
        Sample sample = Sample.Named(name);
        ExpectedTable expected = new(
            new PureQLProjection(
                [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
                sample.Query,
                SampleCatalogueTests.Parameters
            )
        );

        PureQLProjection actual = new(
            _database.Datasets,
            sample.Query,
            SampleCatalogueTests.Parameters
        );

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
}
