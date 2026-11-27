using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Edge cases of aggregates over frames and ranges, every value derived from
// Window evaluation and Aggregate evaluation. A frame is the current row with
// up to preceding rows before and following rows after it in the window's
// order, stopping at the ends of the partition. A range holds the rows whose
// key k lies within c - preceding <= k <= c + following of the current key
// c (k = c on an absent side; with a descending key preceding reaches
// towards greater values); a row with a null key has the null-key rows of
// its partition as its window, and a bound outside the type reaches the end
// of the partition on its side. An aggregate's predicate filters the rows of
// the window without moving it; first, last and concatAll sort those rows by
// their own orderBy; null values are skipped by sum, average, min, max,
// countDistinct, median and concatAll, while first and last return the
// value of their row even when it is null. Every column is read in the order
// of id.
public sealed class WindowFrameEdgeTests
{
    // grp x: k 1 (1, 2), 3 (3), 6 (5), null (4); grp null: k 2 (6, 8),
    // null (7). v is a power of two, so a sum of v names the rows of the
    // window; note is read as declared non-null, so an empty cell is the
    // empty string, and "-" is made null with nullIfEqual.
    private static readonly IStoredSchemaDataSet Rows = new InlineTable(
        "t",
        "r",
        ("id", new LongColumnType()),
        ("grp", new StringColumnType()),
        ("k", new LongColumnType()),
        ("v", new LongColumnType()),
        ("amount", new DoubleColumnType()),
        ("note", new StringColumnType())
    ).With(
        ["1", "x", "1", "1", "0.1", "a"],
        ["2", "x", "1", "2", "0.2", ""],
        ["3", "x", "3", "4", "", "-"],
        ["4", "x", "", "8", "0.3", "b"],
        ["5", "x", "6", "16", "0.25", "a"],
        ["6", "", "2", "32", "1", "-"],
        ["7", "", "", "64", "", ""],
        ["8", "", "2", "128", "0.5", "c"]
    );

    private static readonly IStoredSchemaDataSet Days = new InlineTable(
        "t",
        "days",
        ("id", new LongColumnType()),
        ("day", new DateColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "2024-01-01", "1"],
        ["2", "2024-01-02", "2"],
        ["3", "2024-01-02", "4"],
        ["4", "", "8"],
        ["5", "", "16"]
    );

    private static readonly IStoredSchemaDataSet Eras = new InlineTable(
        "t",
        "eras",
        ("id", new LongColumnType()),
        ("day", new DateColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "0001-01-01", "1"],
        ["2", "0001-01-03", "2"],
        ["3", "9999-12-29", "4"],
        ["4", "9999-12-31", "8"]
    );

    private static readonly IStoredSchemaDataSet Stamps = new InlineTable(
        "t",
        "stamps",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "2024-01-31T10:00:00Z", "1"],
        ["2", "2024-02-29T10:00:00Z", "2"],
        ["3", "2024-02-29T09:59:59Z", "4"],
        ["4", "", "8"],
        ["5", "2024-03-31T10:00:00Z", "16"]
    );

    // Code points: a < U+E000 < U+FF61 < U+1F600 < U+1F601; UTF-16 code
    // units put both emoji before U+E000.
    private static readonly IStoredSchemaDataSet Tags = new InlineTable(
        "t",
        "tags",
        ("id", new LongColumnType()),
        ("grp", new StringColumnType()),
        ("tag", new StringColumnType())
    ).With(
        ["1", "x", "\U0001F600"],
        ["2", "x", "｡"],
        ["3", "x", "a"],
        ["4", "y", ""],
        ["5", "y", "\U0001F601"]
    );

    private static readonly FieldInteger Id = new("t.r", "id");

    private static readonly FieldStringNullable Grp = new("t.r", "grp");

    private static readonly FieldIntegerNullable Key = new("t.r", "k");

    private static readonly FieldInteger V = new("t.r", "v");

    private static readonly FieldDecimalNullable Amount = new("t.r", "amount");

    private static readonly FieldString Note = new("t.r", "note");

    private static readonly NotEqualDecimalRow HasAmount = new(
        Amount,
        new LiteralDecimalNullable()
    );

    private static readonly FieldString Tag = new("t.tags", "tag");

    private static readonly FieldString TagGroup = new("t.tags", "grp");

    [Fact]
    public void SumWithAPredicateOverARunningRangeSkipsTheRowsItRejects()
    {
        Assert.Equal(
            ["3", "3", "3", "8", "19", "160", "0", "160"],
            Values(
                new SumIntegerProjection(
                    V,
                    predicate: HasAmount,
                    over: IntegerRange(new RangeInteger(preceding: new All()))
                )
            )
        );
    }

    // Descending, following reaches towards smaller keys: the rows k <= c.
    [Fact]
    public void CountWithAPredicateOverADescendingRangeOfAllFollowingRows()
    {
        Assert.Equal(
            ["2", "2", "2", "1", "3", "2", "0", "2"],
            Values(
                new CountProjection(
                    predicate: HasAmount,
                    over: IntegerRange(
                        new RangeInteger(following: new All()),
                        SortDirection.Desc
                    )
                )
            )
        );
    }

    [Fact]
    public void DecimalSumOverADescendingRangeIsExactAndZeroOverOnlyNulls()
    {
        Assert.Equal(
            ["0.3", "0.3", "0.3", "0.3", "0.55", "1.5", "0", "1.5"],
            Values(
                new SumDecimalProjection(
                    Amount,
                    over: IntegerRange(
                        new RangeInteger(following: new All()),
                        SortDirection.Desc
                    )
                )
            )
        );
    }

    [Fact]
    public void AverageWithAPredicateOverAFrameOfNeighboursSkipsNullsAndRejectedRows()
    {
        Assert.Equal(
            ["0.2", "0.2", "0.25", "0.275", "0.275", "1", "0.75", "0.5"],
            Values(
                new AverageDecimalNullableProjection(
                    Amount,
                    predicate: new GreaterThanDecimalRow(V, new LiteralInteger(1)),
                    over: RowsById(preceding: 1L, following: 1L)
                )
            )
        );
    }

    [Fact]
    public void AverageWithAPredicateIsNullWhereNoRowOfTheFrameSatisfiesIt()
    {
        Assert.Equal(
            ["", "0.2", "0.2", "0.3", "0.275", "1", "1", "0.5"],
            Values(
                new AverageDecimalNullableProjection(
                    Amount,
                    predicate: new GreaterThanDecimalRow(V, new LiteralInteger(1)),
                    over: RowsById(preceding: 1L)
                )
            )
        );
    }

    // Descending by amount, preceding 0.1 holds c <= k <= c + 0.1.
    [Fact]
    public void DescendingDecimalRangeReachesTowardsGreaterAmounts()
    {
        Assert.Equal(
            ["3", "26", "4", "8", "24", "32", "64", "128"],
            Values(
                new SumIntegerProjection(
                    V,
                    over: AmountRange(
                        new RangeDecimal(preceding: new LiteralDecimal(0.1m)),
                        SortDirection.Desc
                    )
                )
            )
        );
    }

    [Fact]
    public void DecimalRangeOfAllFollowingRowsLeavesTheNullKeysToThemselves()
    {
        Assert.Equal(
            ["27", "26", "4", "8", "24", "32", "64", "160"],
            Values(
                new SumIntegerProjection(
                    V,
                    over: AmountRange(new RangeDecimal(following: new All()))
                )
            )
        );
    }

    [Fact]
    public void MinAndMaxOverARangeAreNullWhereItsRowsHoldOnlyNulls()
    {
        WindowWithoutFrameWithRangeInteger around = IntegerRange(
            new RangeInteger(preceding: 1L, following: 1L)
        );

        Assert.Equal(
            ["0.1", "0.1", "", "0.3", "0.25", "0.5", "", "0.5"],
            Values(new MinDecimalNullableProjection(Amount, over: around))
        );
        Assert.Equal(
            ["0.2", "0.2", "", "0.3", "0.25", "1", "", "1"],
            Values(new MaxDecimalNullableProjection(Amount, over: around))
        );
    }

    [Fact]
    public void CountDistinctOverARunningRangeCountsTheNonNullValues()
    {
        Assert.Equal(
            ["2", "2", "2", "1", "3", "2", "0", "2"],
            Values(
                new CountDistinctProjection(
                    Amount,
                    over: IntegerRange(new RangeInteger(preceding: new All()))
                )
            )
        );
    }

    [Fact]
    public void AnyAndAllOverARangeOfFollowingKeys()
    {
        GreaterThanDecimalRow large = new(Amount, new LiteralDecimal(0.15m));
        WindowWithoutFrameWithRangeInteger ahead = IntegerRange(
            new RangeInteger(following: 2L)
        );

        Assert.Equal(
            ["True", "True", "False", "True", "True", "True", "False", "True"],
            Values(new AnyProjection(large, over: ahead))
        );
        Assert.Equal(
            ["False", "False", "False", "True", "True", "True", "False", "True"],
            Values(new AllProjection(large, over: ahead))
        );
    }

    // Sorted by amount descending, null last: last is the null where the
    // frame holds one.
    [Fact]
    public void LastReturnsTheNullOfItsLastRowInAFrame()
    {
        Assert.Equal(
            ["0.1", "0.1", "", "", "", "1", "", ""],
            Values(
                new LastDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Amount, SortDirection.Desc)],
                    over: RowsById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void LastWithAPredicateSortsOnlyTheRowsOfTheFrameItKeeps()
    {
        Assert.Equal(
            ["0.1", "0.1", "0.1", "0.2", "0.25", "1", "1", "0.5"],
            Values(
                new LastDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Amount, SortDirection.Desc)],
                    predicate: HasAmount,
                    over: RowsById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void FirstWithAPredicateNoRowOfTheFrameSatisfiesIsNull()
    {
        Assert.Equal(
            Enumerable.Repeat("", 8),
            Values(
                new FirstDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Id)],
                    predicate: new GreaterThanDecimalRow(Amount, new LiteralInteger(5)),
                    over: RowsById(preceding: 2L)
                )
            )
        );
    }

    // nullIfEqual(note, "-"): a, "", null, b, a in x; null, "", c in the
    // null partition. The empty string is a value and is joined in (and is
    // listed once), only null is skipped.
    [Fact]
    public void ConcatAllDistinctOverARunningFrameKeepsEmptyStringsAndSkipsNulls()
    {
        Assert.Equal(
            ["a", "a|", "a|", "a||b", "a||b", "", "", "|c"],
            Values(
                new ConcatAllProjection(
                    Notes,
                    new LiteralString("|"),
                    [new OrderItemRow(Id)],
                    over: RowsById(preceding: new All()),
                    distinct: true
                )
            )
        );
    }

    [Fact]
    public void ConcatAllOverAFrameOfNeighboursJoinsInItsOwnDescendingOrder()
    {
        Assert.Equal(
            ["|a", "|a", "b|", "a|b", "a|b", "", "c|", "c|"],
            Values(
                new ConcatAllProjection(
                    Notes,
                    new LiteralString("|"),
                    [new OrderItemRow(Id, SortDirection.Desc)],
                    over: RowsById(preceding: 1L, following: 1L)
                )
            )
        );
    }

    // The key 0 - k: -1 (1, 2), -3 (3), -6 (5), null (4); -2 (6, 8), null (7).
    [Fact]
    public void RangeOverAComputedKeyMeasuresAlongTheComputedValue()
    {
        Assert.Equal(
            ["7", "7", "4", "8", "16", "160", "64", "160"],
            Values(
                new SumIntegerProjection(
                    V,
                    over: new WindowWithoutFrameWithRangeInteger(
                        [
                            new OrderItemIntegerRow(
                                new SubtractIntegerNullableRow([
                                    new LiteralInteger(0),
                                    Key,
                                ])
                            ),
                        ],
                        new RangeInteger(preceding: 2L),
                        [Grp]
                    )
                )
            )
        );
    }

    [Fact]
    public void AverageOverARangeOfNeighbouringKeysIsNeverNull()
    {
        Assert.Equal(
            ["1.5", "1.5", "4", "8", "16", "80", "64", "80"],
            Values(
                new AverageDecimalProjection(
                    V,
                    over: IntegerRange(new RangeInteger(preceding: 1L, following: 1L))
                )
            )
        );
    }

    [Fact]
    public void MedianOverARunningRangeSkipsNulls()
    {
        Assert.Equal(
            ["0.15", "0.15", "0.15", "0.3", "0.2", "0.75", "", "0.75"],
            Values(
                new MedianDecimalNullableProjection(
                    Amount,
                    over: IntegerRange(new RangeInteger(preceding: new All()))
                )
            )
        );
    }

    [Fact]
    public void FrameOfAllPrecedingAndTwoFollowingRowsInEveryPartition()
    {
        Assert.Equal(
            ["7", "15", "31", "31", "31", "224", "224", "224"],
            Values(
                new SumIntegerProjection(
                    V,
                    over: RowsById(preceding: new All(), following: 2L)
                )
            )
        );
    }

    // Days 2024-01-01 (1), 2024-01-02 (2, 3), null (4, 5). A distance of 0
    // on both sides holds only the rows of the same day; a distance beyond
    // the type is not an error and reaches the ends.
    [Theory]
    [InlineData(0, "1,6,6,24,24")]
    [InlineData(1, "7,7,7,24,24")]
    [InlineData(long.MaxValue, "7,7,7,24,24")]
    public void DateRangeReadsBothSidesFromOneParameter(long days, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Sums(
                Days,
                "days",
                new WindowWithoutFrameWithRangeDate(
                    [new OrderItemDateRow(new FieldDateNullable("t.days", "day"))],
                    new RangeDate(
                        preceding: new DistanceDateDays(new ParamInteger("d")),
                        following: new DistanceDateDays(new ParamInteger("d"))
                    )
                ),
                new Dictionary<string, IParameter>
                {
                    ["d"] = new IntegerParameter(new Long(days)),
                }
            )
        );
    }

    [Fact]
    public void DescendingDateRangeWhoseBoundsFallOutsideTheTypeReachesTheEnds()
    {
        Assert.Equal(
            ["3", "3", "12", "12"],
            EraSums(
                new RangeDate(
                    preceding: new DistanceDateDays(5L),
                    following: new DistanceDateDays(5L)
                )
            )
        );
    }

    // Descending: preceding months reach later dates, following months
    // earlier ones; beyond 9999-12-31 or before 0001-01-01 a side reaches the
    // end of the partition.
    [Fact]
    public void DescendingMonthsBeyondTheTypeReachTheEndsOfThePartition()
    {
        Assert.Equal(
            ["3", "2", "12", "8"],
            EraSums(new RangeDate(preceding: new DistanceDateMonths(1L)))
        );
        Assert.Equal(
            ["1", "3", "4", "12"],
            EraSums(new RangeDate(following: new DistanceDateMonths(1L)))
        );
    }

    // Descending, following a month reaches c minus one month, the day
    // clamped to the end of the month, at the same time of day.
    [Fact]
    public void DescendingDatetimeMonthsFollowingReachTheSameTimeOfDayAMonthEarlier()
    {
        Assert.Equal(
            ["1", "7", "5", "8", "18"],
            Sums(
                Stamps,
                "stamps",
                new WindowWithoutFrameWithRangeDatetime(
                    [
                        new OrderItemDatetimeRow(
                            new FieldDatetimeNullable("t.stamps", "at"),
                            SortDirection.Desc
                        ),
                    ],
                    new RangeDatetime(following: new DistanceDatetimeMonths(1L))
                )
            )
        );
    }

    [Fact]
    public void MinAndMaxOfStringsOverAPartitionCompareCodePoints()
    {
        WindowWithoutFrameWithoutRange perGroup = new([TagGroup]);

        Assert.Equal(
            ["\U0001F600", "\U0001F600", "\U0001F600", "\U0001F601", "\U0001F601"],
            TagValues(new MaxStringProjection(Tag, over: perGroup))
        );
        Assert.Equal(
            ["a", "a", "a", "", ""],
            TagValues(new MinStringProjection(Tag, over: perGroup))
        );
    }

    [Fact]
    public void RunningMaxOfStringsComparesCodePoints()
    {
        Assert.Equal(
            ["\U0001F600", "\U0001F600", "\U0001F600", "\U0001F600", "\U0001F601"],
            TagValues(
                new MaxStringProjection(
                    Tag,
                    over: new WindowWithFrame(
                        [new OrderItemRow(new FieldInteger("t.tags", "id"))],
                        new Frame(preceding: new All())
                    )
                )
            )
        );
    }

    [Fact]
    public void FirstOverAPartitionSortsStringsByCodePoint()
    {
        Assert.Equal(
            ["\U0001F600", "\U0001F600", "\U0001F600", "\U0001F601", "\U0001F601"],
            TagValues(
                new FirstStringProjection(
                    Tag,
                    [new OrderItemRow(Tag, SortDirection.Desc)],
                    over: new WindowWithoutFrameWithoutRange([TagGroup])
                )
            )
        );
    }

    [Fact]
    public void ConcatAllOverAPartitionJoinsStringsInCodePointOrder()
    {
        string x = "a,｡,\U0001F600";
        string y = ",\U0001F601";

        Assert.Equal(
            [x, x, x, y, y],
            TagValues(
                new ConcatAllProjection(
                    Tag,
                    new LiteralString(","),
                    [new OrderItemRow(Tag)],
                    over: new WindowWithoutFrameWithoutRange([TagGroup])
                )
            )
        );
    }

    // Aggregate evaluation: the mean day, a mean halfway between two days
    // being the later one; nulls skipped, and null over only nulls.
    [Fact]
    public void AverageOfDatesOverAFrameRoundsHalfwayToTheLaterDay()
    {
        Assert.Equal(
            ["2024-01-01", "2024-01-02", "2024-01-02", "2024-01-02", ""],
            Column(
                Days,
                "days",
                new AverageDateNullableProjection(
                    new FieldDateNullable("t.days", "day"),
                    over: new WindowWithFrame(
                        [new OrderItemRow(new FieldInteger("t.days", "id"))],
                        new Frame(preceding: 1L)
                    )
                )
            )
        );
    }

    // The mean of 00:00:00 and 00:00:00.000001 is halfway between two
    // microseconds: the later one. 00:00:00.000001 and 23:59:59.999999 are
    // 43200 seconds apart from their mean, without wrapping.
    [Fact]
    public void AverageOfTimesOverAFrameRoundsHalfwayToTheLaterMicrosecond()
    {
        Assert.Equal(
            ["00:00:00", "00:00:00.000001", "12:00:00"],
            Column(
                Clocks,
                "clocks",
                new AverageTimeProjection(
                    new FieldTime("t.clocks", "clock"),
                    over: new WindowWithFrame(
                        [new OrderItemRow(new FieldInteger("t.clocks", "id"))],
                        new Frame(preceding: 1L)
                    )
                )
            )
        );
    }

    private static readonly IStoredSchemaDataSet Clocks = new InlineTable(
        "t",
        "clocks",
        ("id", new LongColumnType()),
        ("clock", new TimeColumnType())
    ).With(["1", "00:00:00"], ["2", "00:00:00.000001"], ["3", "23:59:59.999999"]);

    private static NullIfEqualStringNullableRow Notes =>
        new(Note, new LiteralString("-"));

    private static WindowWithFrame RowsById(
        FrameRows? preceding = null,
        FrameRows? following = null
    )
    {
        return new WindowWithFrame(
            [new OrderItemRow(Id)],
            new Frame(preceding, following),
            [Grp]
        );
    }

    private static WindowWithoutFrameWithRangeInteger IntegerRange(
        RangeInteger range,
        SortDirection direction = SortDirection.Asc
    )
    {
        return new WindowWithoutFrameWithRangeInteger(
            [new OrderItemIntegerRow(Key, direction)],
            range,
            [Grp]
        );
    }

    private static WindowWithoutFrameWithRangeDecimal AmountRange(
        RangeDecimal range,
        SortDirection direction = SortDirection.Asc
    )
    {
        return new WindowWithoutFrameWithRangeDecimal(
            [new OrderItemDecimalRow(Amount, direction)],
            range,
            [Grp]
        );
    }

    private static IReadOnlyList<string> EraSums(RangeDate range)
    {
        return Sums(
            Eras,
            "eras",
            new WindowWithoutFrameWithRangeDate(
                [
                    new OrderItemDateRow(
                        new FieldDate("t.eras", "day"),
                        SortDirection.Desc
                    ),
                ],
                range
            )
        );
    }

    private static IReadOnlyList<string> Sums(
        IStoredSchemaDataSet table,
        string name,
        Window over,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Column(
            table,
            name,
            new SumIntegerProjection(new FieldInteger($"t.{name}", "v"), over: over),
            parameters
        );
    }

    private static IReadOnlyList<string> TagValues(ExprProjection expression)
    {
        return Column(Tags, "tags", expression);
    }

    private static IReadOnlyList<string> Values(ExprProjection expression)
    {
        return Column(Rows, "r", expression);
    }

    private static IReadOnlyList<string> Column(
        IStoredSchemaDataSet table,
        string name,
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        PureQLProjection projection = new(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity($"t.{name}"),
                select: [new SelectItemProjection("v", expression)],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldInteger($"t.{name}", "id")
                    ),
                ]
            ),
            parameters ?? []
        );

        return ResultColumn.Texts(projection, "v");
    }
}
