using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Tree;

// The shape of the tree of every construct specification 0.1.0-preview.3.0.0
// added, as a provider meets it (the README lists them under "Shapes of
// specification 3.0.0"): BCL members where the BCL computes exactly what the
// specification defines, the functions and query operators of this library
// where it does not, each lifted operator checking its operands for null
// around the call. The texts are the query below the result rows.
public sealed class NewConstructTreeTests
{
    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("price", new DoubleColumnType()),
        ("day", new DateColumnType()),
        ("at", new DateTimeColumnType()),
        ("parent", new LongColumnType())
    ).With(["1", "pen", "1.5", "2024-01-31", "2024-01-31T10:00:00Z", ""]);

    private static readonly FieldInteger Id = new("i", "id");

    private static readonly FieldString Name = new("i", "name");

    private static readonly FieldDecimalNullable Price = new("i", "price");

    private static readonly FieldDate Day = new("i", "day");

    private static readonly FieldDatetime At = new("i", "at");

    [Fact]
    public void AtLeastANullLiteralIsWhetherTheOtherOperandIsNull()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => ((bool?)(row.Item1 == null)))",
            Tree(
                new GreaterThanOrEqualDecimalProjection(
                    Price,
                    new LiteralDecimalNullable()
                )
            )
        );
    }

    [Fact]
    public void AtLeastOfTwoNullableOperandsAlsoHoldsForTwoNulls()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => ((bool?)((row.Item1 >= row.Item1) || ((row.Item1 == null) && (row.Item1 == null)))))",
            Tree(new GreaterThanOrEqualDecimalProjection(Price, Price))
        );
    }

    [Fact]
    public void ListOfIntegersAndADecimalKeepsItsIntegerSteps()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[price])).Select(row => (((decimal?)(row.Item1 + row.Item1) + row.Item2)))",
            Tree(new AddDecimalNullableProjection([Id, Id, Price]))
        );
    }

    [Fact]
    public void RoundToLiteralNegativeDigitsDividesByAConstantPowerOfTen()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : (decimal?)(Math.Round((row.Item1.Value / 100m), MidpointRounding.AwayFromZero) * 100m))))",
            Tree(new RoundDecimalNullableProjection(Price, new LiteralInteger(-2)))
        );
    }

    [Fact]
    public void RoundHalfEvenIsMathRoundToEven()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : (decimal?)Math.Round(row.Item1.Value, 2, MidpointRounding.ToEven))))",
            Tree(
                new RoundDecimalNullableProjection(
                    Price,
                    new LiteralInteger(2),
                    RoundingMode.HalfEven
                )
            )
        );
    }

    [Fact]
    public void RoundToComputedDigitsIsTheThreeWayConditional()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[price])).Select(row => ((((row.Item2 == null) || (row.Item1 == null)) ? null : (decimal?)((row.Item1.Value >= 0L) ? Math.Round(row.Item2.Value, (int)Math.Min(row.Item1.Value, 28L), MidpointRounding.AwayFromZero) : ((row.Item1.Value < -28L) ? 0m : (Math.Round((row.Item2.Value / (decimal)Math.Pow(10, (double)-row.Item1.Value)), MidpointRounding.AwayFromZero) * (decimal)Math.Pow(10, (double)-row.Item1.Value)))))))",
            Tree(new RoundDecimalNullableProjection(Price, Id))
        );
    }

    [Fact]
    public void RoundOfAnIntegerWithDigitsIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!)).Select(row => (((row.Item1 == null) ? null : (long?)PureQLMath.Round(row.Item1.Value, -1L, MidpointRounding.AwayFromZero))))",
            Tree(new RoundWithDigitsIntegerProjection(Id, new LiteralInteger(-1)))
        );
    }

    [Fact]
    public void TruncateWithoutDigitsIsMathTruncate()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : (long?)(long)Math.Truncate(row.Item1.Value))))",
            Tree(new TruncateWithoutDigitsIntegerNullableProjection(Price))
        );
    }

    [Fact]
    public void AbsIsMathAbs()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : (decimal?)Math.Abs(row.Item1.Value))))",
            Tree(new AbsDecimalNullableProjection(Price))
        );
    }

    [Fact]
    public void GreatestOfNumbersIsMathMaxInTheirCommonType()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[price])).Select(row => (((((decimal?)row.Item1 == null) || (row.Item2 == null)) ? null : (decimal?)Math.Max((decimal?)row.Item1.Value, row.Item2.Value))))",
            Tree(new GreatestDecimalNullableProjection([Id, Price]))
        );
    }

    [Fact]
    public void GreatestOfDatesIsTheMaxOfTheirList()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (DateOnly?)new[] { row.Item1.Value, 02/01/2024 }.Max())))",
            Tree(
                new GreatestDateProjection([
                    Day,
                    new LiteralDate(new DateOnly(2024, 2, 1)),
                ])
            )
        );
    }

    [Fact]
    public void PowerIsAFunctionOfThisLibraryLiftedOverItsOperands()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : (decimal?)PureQLMath.Power(row.Item1.Value, 2m))))",
            Tree(new PowerDecimalNullableProjection(Price, new LiteralInteger(2)))
        );
    }

    [Fact]
    public void PiIsADecimalConstant()
    {
        Assert.Equal(
            "[items].Select(i => ()).Select(row => (3.141592653589793238462643383m))",
            Tree(new PiProjection())
        );
    }

    [Fact]
    public void NullIfEqualIsAConditionalOnEquality()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == 0m) ? null : row.Item1)))",
            Tree(new NullIfEqualDecimalNullableProjection(Price, new LiteralInteger(0)))
        );
    }

    [Fact]
    public void IntegerDivideOfADecimalIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[price])).Select(row => ((((row.Item2 == null) || ((decimal?)row.Item1 == null)) ? null : (long?)PureQLMath.IntegerDivide(row.Item2.Value, (decimal?)row.Item1.Value))))",
            Tree(new IntegerDivideIntegerNullableProjection(Price, Id))
        );
    }

    [Fact]
    public void ContainsIsStringContainsAfterNullChecks()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => ((bool?)((row.Item1 != null) && row.Item1.Contains(\"e\"))))",
            Tree(new ContainsProjection(Name, new LiteralString("e")))
        );
    }

    [Fact]
    public void StartsWithComparesOrdinally()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => ((bool?)((row.Item1 != null) && row.Item1.StartsWith(\"p\", StringComparison.Ordinal))))",
            Tree(new StartsWithProjection(Name, new LiteralString("p")))
        );
    }

    [Fact]
    public void LengthIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (((row.Item1 == null) ? null : (long?)PureQLText.Length(row.Item1))))",
            Tree(new LengthIntegerProjection(Name))
        );
    }

    [Fact]
    public void NormalizeIsStringNormalize()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (((row.Item1 == null) ? null : row.Item1.Normalize(NormalizationForm.FormKC))))",
            Tree(new NormalizeStringProjection(Name, NormalizationForm.NFKC))
        );
    }

    [Fact]
    public void TrimTakesItsRemovablesAsAnArray()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (((row.Item1 == null) ? null : PureQLText.Trim(row.Item1, new[] { \" \" }))))",
            Tree(new TrimStringProjection(Name, [new LiteralString(" ")]))
        );
    }

    [Fact]
    public void SubstringIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (((row.Item1 == null) ? null : PureQLText.Substring(row.Item1, 2L))))",
            Tree(new SubstringStringProjection(Name, new LiteralInteger(2)))
        );
    }

    [Fact]
    public void ConcatWithTakesItsOperandsAsAnArrayAndIsNotLifted()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (PureQLText.ConcatWith(\"-\", new[] { row.Item1, null })))",
            Tree(
                new ConcatWithProjection(
                    [Name, new LiteralStringNullable()],
                    new LiteralString("-")
                )
            )
        );
    }

    [Fact]
    public void MatchesRegexIsFalseForANullOperand()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => ((bool?)((row.Item1 != null) && PureQLText.MatchesRegex(row.Item1, \"^p\"))))",
            Tree(new MatchesRegexProjection(Name, new LiteralString("^p")))
        );
    }

    [Fact]
    public void ExtractRegexIsNullForANullOperand()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (((row.Item1 == null) ? null : PureQLText.ExtractRegex(row.Item1, \"(p)e\", 1L))))",
            Tree(
                new ExtractRegexProjection(
                    Name,
                    new LiteralString("(p)e"),
                    new LiteralInteger(1)
                )
            )
        );
    }

    [Fact]
    public void YearOfADateIsItsMember()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (long?)(long)row.Item1.Value.Year)))",
            Tree(new YearDateIntegerProjection(Day))
        );
    }

    [Fact]
    public void WeekOfADateIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (long?)PureQLTime.Week(row.Item1.Value))))",
            Tree(new WeekDateIntegerProjection(Day))
        );
    }

    [Fact]
    public void HourOfADatetimeReadsItsTimeOfDayInUtc()
    {
        Assert.Equal(
            "[items].Select(i => (i[at]!)).Select(row => (((row.Item1 == null) ? null : (long?)(long)TimeOnly.FromDateTime(row.Item1.Value).Hour)))",
            Tree(new HourDatetimeIntegerProjection(At))
        );
    }

    [Fact]
    public void HourOfADatetimeInAZoneReadsItsLocalTime()
    {
        Assert.Equal(
            "[items].Select(i => (i[at]!)).Select(row => (((row.Item1 == null) ? null : (long?)(long)PureQLTime.LocalTime(row.Item1.Value, \"Europe/Moscow\").Hour)))",
            Tree(
                new HourDatetimeIntegerProjection(At, new LiteralString("Europe/Moscow"))
            )
        );
    }

    [Fact]
    public void StartOfMonthIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (DateOnly?)PureQLTime.StartOfMonth(row.Item1.Value))))",
            Tree(new StartOfMonthDateProjection(Day))
        );
    }

    [Fact]
    public void StartOfDayInAZoneTakesTheZone()
    {
        Assert.Equal(
            "[items].Select(i => (i[at]!)).Select(row => (((row.Item1 == null) ? null : (DateTime?)PureQLTime.StartOfDay(row.Item1.Value, \"Europe/Moscow\"))))",
            Tree(new StartOfDayDatetimeProjection(At, new LiteralString("Europe/Moscow")))
        );
    }

    [Fact]
    public void DateAddMonthsIsDateOnlyAddMonths()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (DateOnly?)row.Item1.Value.AddMonths(1))))",
            Tree(new DateAddMonthsDateProjection(Day, new LiteralInteger(1)))
        );
    }

    [Fact]
    public void DatetimeAddMonthsInAZoneIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[at]!)).Select(row => (((row.Item1 == null) ? null : (DateTime?)PureQLTime.AddMonths(row.Item1.Value, 1L, \"Europe/Moscow\"))))",
            Tree(
                new DatetimeAddMonthsDatetimeProjection(
                    At,
                    new LiteralInteger(1),
                    new LiteralString("Europe/Moscow")
                )
            )
        );
    }

    [Fact]
    public void DateDiffMonthsIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => ((((row.Item1 == null) || (row.Item1 == null)) ? null : (long?)PureQLTime.DiffMonths(row.Item1.Value, row.Item1.Value))))",
            Tree(new DateDiffMonthsIntegerProjection(Day, Day))
        );
    }

    [Fact]
    public void DateFromPartsIsTheDateOnlyConstructor()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!)).Select(row => (((((row.Item1 == null) || (row.Item1 == null)) || (row.Item1 == null)) ? null : (DateOnly?)new DateOnly(checked((int)row.Item1.Value), checked((int)row.Item1.Value), checked((int)row.Item1.Value)))))",
            Tree(new DateFromPartsDateProjection(Id, Id, Id))
        );
    }

    [Fact]
    public void AsDateOfADatetimeIsDateOnlyFromDateTime()
    {
        Assert.Equal(
            "[items].Select(i => (i[at]!)).Select(row => (((row.Item1 == null) ? null : (DateOnly?)DateOnly.FromDateTime(row.Item1.Value))))",
            Tree(new AsDateDatetimeDateProjection(At))
        );
    }

    [Fact]
    public void AsDatetimeInAZoneIsTheFirstInstantOfTheLocalDay()
    {
        Assert.Equal(
            "[items].Select(i => (i[day]!)).Select(row => (((row.Item1 == null) ? null : (DateTime?)PureQLTime.DatetimeFromParts(row.Item1.Value, 00:00, \"Europe/Moscow\"))))",
            Tree(
                new AsDatetimeDateDatetimeProjection(
                    Day,
                    new LiteralString("Europe/Moscow")
                )
            )
        );
    }

    [Fact]
    public void AsStringIsAFunctionOfThisLibrary()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (((row.Item1 == null) ? null : PureQLConversions.AsString(row.Item1.Value))))",
            Tree(new AsStringDecimalStringNullableProjection(Price))
        );
    }

    [Fact]
    public void TryAsTakesANullableStringAndIsNotLifted()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (PureQLConversions.TryAsInteger(row.Item1)))",
            Tree(new TryAsIntegerProjection(Name))
        );
    }

    [Fact]
    public void AsDecimalOfAnIntegerIsAConversion()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!)).Select(row => ((decimal?)row.Item1))",
            Tree(new AsDecimalIntegerDecimalProjection(Id))
        );
    }

    [Fact]
    public void CountDistinctCountsTheDistinctNonNullValues()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => ((long?)[items].Select(i => (i[name]!)).Where(row => (row.Item1 != null)).Select(row => row.Item1).Distinct().LongCount()))",
            Tree(new CountDistinctProjection(Name))
        );
    }

    [Fact]
    public void FirstIsTheFirstValueInTheAggregatesOwnOrder()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!)).Select(row => ([items].Select(i => (i[id]!, i[name]!)).OrderByDescending(row => row.Item1).Select(row => row.Item2).FirstOrDefault()))",
            Tree(
                new FirstStringProjection(
                    Name,
                    [new OrderItemRow(Id, SortDirection.Desc)]
                )
            )
        );
    }

    [Fact]
    public void ConcatAllJoinsTheNonNullValuesInOrder()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!)).Select(row => (String.Join(\",\", [items].Select(i => (i[id]!, i[name]!)).OrderBy(row => row.Item1).Where(row => (row.Item2 != null)).Select(row => row.Item2).Distinct())))",
            Tree(
                new ConcatAllProjection(
                    new AsNullableStringNullableRow(Name),
                    new LiteralString(","),
                    [new OrderItemRow(Id)],
                    distinct: true
                )
            )
        );
    }

    [Fact]
    public void MedianIsAFunctionOverTheSelectedValues()
    {
        Assert.Equal(
            "[items].Select(i => (i[price])).Select(row => (PureQLAggregates.Median([items].Select(i => (i[price])).Select(row => row.Item1))))",
            Tree(new MedianDecimalNullableProjection(Price))
        );
    }

    [Fact]
    public void PercentileDiscreteTakesItsFraction()
    {
        Assert.Equal(
            "[items].Select(i => (i[name]!)).Select(row => (PureQLAggregates.PercentileDiscrete([items].Select(i => (i[name]!)).Select(row => row.Item1), 0.5m)))",
            Tree(new PercentileDiscreteStringProjection(Name, new LiteralDecimal(0.5m)))
        );
    }

    [Fact]
    public void CorrelationIsAFunctionOverPairs()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[price])).Select(row => (PureQLAggregates.Correlation([items].Select(i => (i[id]!, i[price])).Select(row => (row.Item2, (decimal?)row.Item1)))))",
            Tree(new CorrelationProjection(Price, Id))
        );
    }

    [Fact]
    public void RankIsReadFromTheWindowOfAPassOverTheRows()
    {
        Assert.Equal(
            "PureQLWindows.Over([items].Select(i => (i[name]!, i[price])), row => (row.Item1), partition => partition.OrderByDescending(row => row.Item2), row => (row.Item2), PureQLFrame.Partition, window => (window.Row, window.Rank)).Select(row => ((long?)row.Item2))",
            Tree(
                new RankProjection(
                    new OrderedWindow(
                        [new OrderItemRow(Price, SortDirection.Desc)],
                        [Name]
                    )
                )
            )
        );
    }

    [Fact]
    public void AggregateOverAFrameFoldsTheRowsOfTheWindow()
    {
        Assert.Equal(
            "PureQLWindows.Over([items].Select(i => (i[id]!, i[price])), row => (), partition => partition.OrderBy(row => row.Item1), row => (row.Item1), PureQLFrame.Rows(null, 1L), window => (window.Row, window.Rows.Sum(row => row.Item2))).Select(row => (row.Item2))",
            Tree(
                new SumDecimalProjection(
                    Price,
                    over: new WindowWithFrame(
                        [new OrderItemRow(Id)],
                        new Frame(preceding: new All(), following: 1L)
                    )
                )
            )
        );
    }

    [Fact]
    public void AggregateOverARangeNamesItsDistancesAndUnits()
    {
        Assert.Equal(
            "PureQLWindows.Over([items].Select(i => (i[price], i[day]!)), row => (), partition => partition.OrderBy(row => row.Item2), row => (row.Item2), PureQLFrame.Range(false, 29m, PureQLRangeUnit.Days, 0m, PureQLRangeUnit.Number), window => (window.Row, window.Rows.Sum(row => row.Item1))).Select(row => (row.Item2))",
            Tree(
                new SumDecimalProjection(
                    Price,
                    over: new WindowWithoutFrameWithRangeDate(
                        [new OrderItemDateRow(Day)],
                        new RangeDate(preceding: new DistanceDateDays(29L))
                    )
                )
            )
        );
    }

    [Fact]
    public void PreviousReadsTheRowBeforeOrItsDefault()
    {
        Assert.Equal(
            "PureQLWindows.Over([items].Select(i => (i[id]!)), row => (), partition => partition.OrderBy(row => row.Item1), row => (row.Item1), PureQLFrame.Partition, window => (window.Row, (window.Has(-1L) ? (decimal?)window.At(-1L).Item1 : 0m))).Select(row => (row.Item2))",
            Tree(
                new PreviousDecimalProjection(
                    new AsDecimalIntegerDecimalRow(Id),
                    new LiteralDecimal(0m),
                    new OrderedWindow([new OrderItemRow(Id)])
                )
            )
        );
    }

    [Fact]
    public void NtileTakesItsBuckets()
    {
        Assert.Equal(
            "PureQLWindows.Over([items].Select(i => (i[id]!)), row => (), partition => partition.OrderBy(row => row.Item1), row => (row.Item1), PureQLFrame.Partition, window => (window.Row, window.Ntile(4L))).Select(row => ((long?)row.Item2))",
            Tree(
                new NtileProjection(
                    new LiteralInteger(4),
                    new OrderedWindow([new OrderItemRow(Id)])
                )
            )
        );
    }

    [Fact]
    public void EveryWindowFunctionIsAPassOverTheRowsOfTheOneBefore()
    {
        Assert.Equal(
            "PureQLWindows.Over(PureQLWindows.Over([items].Select(i => (i[id]!, i[name]!)), row => (), partition => partition.OrderBy(row => row.Item1), row => (row.Item1), PureQLFrame.Partition, window => (window.Row, window.RowNumber)), row => (row.Item1.Item2), partition => partition, row => (), PureQLFrame.Partition, window => (window.Row, window.Rows.LongCount())).OrderByDescending(row => row.Item1.Item2).Select(row => ((long?)row.Item1.Item2, (long?)row.Item2))",
            Tree(
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.items", "i"),
                    select:
                    [
                        new SelectItemProjection(
                            "n",
                            new RowNumberProjection(
                                new OrderedWindow([new OrderItemRow(Id)])
                            )
                        ),
                        new SelectItemProjection(
                            "c",
                            new CountProjection(
                                over: new WindowWithoutFrameWithoutRange([Name])
                            )
                        ),
                    ],
                    orderBy: [new OrderItemProjectionColumn("n", SortDirection.Desc)]
                )
            )
        );
    }

    [Fact]
    public void UnionShapesItsInputsIntoTheColumnsOfTheFirst()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!)).Select(row => ((decimal?)row.Item1, row.Item2)).Union([items].Select(i => (i[name]!, i[price])).Select(row => (row.Item2, row.Item1)))",
            Tree(
                new MainCombinedQueryUnion([
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        new FromEntity("t.items", "i"),
                        [
                            new SelectItemProjection("v", Id),
                            new SelectItemProjection("n", Name),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        new FromEntity("t.items", "i"),
                        [
                            new SelectItemProjection("n", Name),
                            new SelectItemProjection("v", Price),
                        ]
                    ),
                ])
            )
        );
    }

    [Fact]
    public void InlineRowsAreAnArrayReadFromTheOneRowSource()
    {
        Assert.Equal(
            "[one row].SelectMany(one => new[] { (1m), (2.5m) }, (one, row) => row)",
            Tree(
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "r",
                            [
                                [new RowItem("k", new LiteralInteger(1))],
                                [new RowItem("k", new LiteralDecimal(2.5m))],
                            ]
                        ),
                    ],
                    from: new FromSubquery("r"),
                    select: [new SelectItemProjection("k", new FieldDecimal("r", "k"))]
                )
            )
        );
    }

    [Fact]
    public void RecursiveSubqueryIsItsSeedAndItsNextOverTheLevelBefore()
    {
        Assert.Equal(
            "PureQLRecursion.Recursive([items].Select(i => (i[id]!)).Where(i => (i.Item1 == 1L)), tree => [items].Select(i => (i[id]!, i[parent])).Join(tree, left => (left.Item2), tree => (tree.Item1), (left, tree) => (left, tree)).Select(row => (row.Item1.Item1)), 5L)",
            Tree(
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRecursive(
                            "tree",
                            new Recursion(
                                new PlainQueryWithoutPaginationWithoutOrderBy(
                                    new FromEntity("t.items", "i"),
                                    [new SelectItemProjection("id", Id)],
                                    where: new EqualDecimalRow(Id, new LiteralInteger(1))
                                ),
                                new PlainNextQuery(
                                    new FromEntity("t.items", "i"),
                                    [new SelectItemRow("id", Id)],
                                    joins:
                                    [
                                        new JoinSubquery(
                                            JoinKind.Inner,
                                            "tree",
                                            new EqualDecimalRow(
                                                new FieldIntegerNullable("i", "parent"),
                                                new FieldInteger("tree", "id")
                                            )
                                        ),
                                    ]
                                ),
                                5L
                            )
                        ),
                    ],
                    from: new FromSubquery("tree"),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("tree", "id")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void PaginationWithOnlySkipOnlySkips()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!)).OrderBy(row => row.Item1).Skip(2)",
            Tree(
                new MainPlainQueryWithPagination(
                    from: new FromEntity("t.items", "i"),
                    pagination: new Pagination(skip: 2L),
                    select: [new SelectItemProjection("id", Id)],
                    orderBy: [new OrderItemProjectionColumn("id")]
                )
            )
        );
    }

    [Fact]
    public void StatisticsAndLastOverAGroupFoldItsRows()
    {
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!, i[price])).GroupBy(row => (row.Item2)).Select(group => (group.Key.Item1, PureQLAggregates.StandardDeviationSample(group.Select(row => row.Item3)), group.OrderByDescending(row => row.Item3).Select(row => row.Item1).FirstOrDefault()))",
            Tree(
                new MainGroupedQueryWithoutPagination(
                    from: new FromEntity("t.items", "i"),
                    groupBy: [new GroupKey("n", Name)],
                    select:
                    [
                        new SelectItemGroup("n", new KeyString("n")),
                        new SelectItemGroup(
                            "sd",
                            new StandardDeviationSampleGroup(Price)
                        ),
                        new SelectItemGroup(
                            "last",
                            new LastIntegerGroup(Id, [new OrderItemRow(Price)])
                        ),
                    ]
                )
            )
        );
    }

    // The tree of a query that selects one expression over the items.
    private static string Tree(ExprProjection expression)
    {
        return Tree(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select: [new SelectItemProjection("x", expression)]
            )
        );
    }

    // The tree of a query below its result rows.
    private static string Tree(PureQLQuery query)
    {
        string tree = TreeText.Of(new PureQLProjection([Items], query).Expression);

        return tree[
            ..tree.LastIndexOf(".Select(row => new ResultRow(", StringComparison.Ordinal)
        ];
    }
}
