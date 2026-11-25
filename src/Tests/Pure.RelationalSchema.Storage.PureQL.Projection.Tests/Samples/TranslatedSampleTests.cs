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
    // know yet (string.Join, a DateOnly built from parts or from a day number,
    // DateOnly.ToDateTime, string.Normalize, the Max of an array, an ordered
    // FirstOrDefault over a group, ...). A sample that leaves
    // this list when the provider learns its constructs fails the test, and
    // is removed from it.
    internal static readonly HashSet<string> Refused =
    [
        "AbsentAndNullableOperandsQuery",
        "AbsSignAndTruncateInATotalQuery",
        "AbsSignAndTruncateQuery",
        "ActivityTimelineQuery",
        "AddedAmountsInAGroupedQuery",
        "AddedAmountsInATotalQuery",
        "AddingAndTakingTheDifferenceQuery",
        "AgeBandsQuery",
        "AggregateEdgesOverAllRowsQuery",
        "AggregatesOverExactValuesQuery",
        "AncestorsQuery",
        "AsNullableOfEveryTypeQuery",
        "AsStringOfEveryTypeQuery",
        "AsStringOfOperatorResultsQuery",
        "BlankValuesOfEveryTypeAsNullQuery",
        "CalendarOfDaysQuery",
        "CircularMeanHourPerUserQuery",
        "CodesMatchedByRegexQuery",
        "ComputedKeysSelectorsAndDefaultsInWindowsQuery",
        "ConcatAllInATotalQuery",
        "ConcatAllNamesPerCountryQuery",
        "ConcatAllNamesPerOrderFromASubqueryQuery",
        "ConcatAllPerTenantQuery",
        "ConcatAllRepeatedOnEveryRowQuery",
        "ConstantOperandsNeverNullableQuery",
        "ContextsOfRecursiveStepsQuery",
        "ConversionsInATotalQuery",
        "ConversionsQuery",
        "CountDistinctInATotalQuery",
        "CountDistinctOverWindowsQuery",
        "CountDistinctSelectorKindsQuery",
        "CouponEffectivenessPerCouponQuery",
        "CovarianceAndCorrelationPerTenantQuery",
        "DailyRevenueTrendQuery",
        "DatePartsQuery",
        "DatesCombinedWithDatetimesQuery",
        "DatesFromPartsQuery",
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
        "FirstAndLastPerStatusOverAllRowsQuery",
        "FirstAndLastPerUserQuery",
        "FixedWidthReportLinesQuery",
        "FoldCaseQuery",
        "FrameOfEveryFormQuery",
        "FromPartsInAGroupedQuery",
        "FromPartsInATotalQuery",
        "FromPartsOverWindowsQuery",
        "GreatestAndLeastInATotalQuery",
        "GreatestAndLeastOfEveryTypeQuery",
        "GreatestAndLeastOverAggregatesQuery",
        "GreatestAndLeastQuery",
        "GroupByMonthQuery",
        "GroupedQueryAtItsEdgesQuery",
        "GroupedSortByColumnsAndExpressionsQuery",
        "GroupingWithoutAccentsQuery",
        "GrowthPerUserQuery",
        "HierarchyWalkWithAggregatedStepsQuery",
        "HourAsAnAngleQuery",
        "InlineStatusRowsQuery",
        "InOverEveryKindOfListQuery",
        "JoinOnNormalizedSkuQuery",
        "LastTwelveMonthsPerUserQuery",
        "LatestOrderPerUserFromASubqueryQuery",
        "LiftedOverEveryOperandQuery",
        "LineTotalsAfterCouponDiscountQuery",
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
        "MonthsWithoutOrdersFilledWithZeroQuery",
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
        "OperandsReadFromDeclaredTypesQuery",
        "OperatorsOverAListInEveryContextQuery",
        "OperatorsWithAndWithoutAZoneQuery",
        "OperatorTypingAtItsEdgesQuery",
        "OrderLabelsOverJoinsQuery",
        "OrdersActiveInAPeriodQuery",
        "OrdersPerDayOfADatetimeQuery",
        "OrdersPerListedStatusQuery",
        "OrdersWithTaxFromAParameterRowQuery",
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
        "RowCountsOfEverySetOperationQuery",
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
        "TotalQueryJoinedOnTrueQuery",
        "TreeInReadingOrderQuery",
        "TreeRollUpQuery",
        "TrialsEndingWithinThreeDaysQuery",
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

    // The samples whose result over the stored fixtures differs from the
    // sample's because the provider reads a stored value wrong, each run by a
    // skipped test of its own until the provider is fixed.
    internal static readonly HashSet<string> MisreadByTheProvider =
    [
        "BlankValuesOfEveryTypeAsNullQuery",
    ];

    public static TheoryData<string> SamplesWithResult =>
        [
            .. Sample
                .All.Where(sample =>
                    sample.Result is not null && !MisreadByTheProvider.Contains(sample.Name)
                )
                .Select(sample => sample.Name),
        ];

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

    // Refused, the query reads the users through the provider, which reads a
    // time cell as HH:mm: Fay's user_edge_time 23:59:59 is 23:59:00
    // (kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying#101).
    [Fact(
        Skip = "A stored time is read without its seconds: "
            + "kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying#101"
    )]
    public void BlankValuesOfEveryTypeAsNullReturnsItsExpectedResultInMemory()
    {
        SampleReturnsItsExpectedResultTranslatedUnlessRefused(
            "BlankValuesOfEveryTypeAsNullQuery"
        );
    }

    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithBoundParametersIsTranslatedUnlessRefused(string name)
    {
        PureQLProjection projection = new(
            _database.Datasets,
            Sample.Named(name).Query,
            SampleCatalogueTests.Parameters
        );

        _ = projection.ToList();

        AssertRan(name, projection);
    }
}
