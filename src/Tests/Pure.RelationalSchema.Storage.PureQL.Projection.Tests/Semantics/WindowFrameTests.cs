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

// Aggregates over a frame of rows: the partition is sorted by the window's
// orderBy (null first ascending) and the frame is the current row, up to
// preceding rows before it and up to following rows after it, stopping at
// the ends of the partition; "all" is every row on that side and an absent
// side has no rows. The aggregate is evaluated over the rows of the frame:
// its predicate filters them without moving the frame, and first, last and
// concatAll sort them by their own orderBy. A frame count given as a
// parameter is read once: 0 leaves no rows on that side, and a count below 0
// is rejected. Every column is read in the order of id.
public sealed class WindowFrameTests
{
    // user a: 1, 3, 4, 6, 8; user b: 2, 5, 7.
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType()),
        ("amount", new DoubleColumnType()),
        ("status", new StringColumnType())
    ).With(
        ["1", "a", "10", "1.5", "paid"],
        ["2", "b", "20", "", "new"],
        ["3", "a", "30", "2.5", "paid"],
        ["4", "a", "40", "", "new"],
        ["5", "b", "50", "4", "paid"],
        ["6", "a", "60", "0.5", "new"],
        ["7", "b", "70", "3", "paid"],
        ["8", "a", "80", "", "paid"]
    );

    private static readonly IStoredSchemaDataSet Numbers = new InlineTable(
        "t",
        "numbers",
        ("id", new LongColumnType()),
        ("n", new LongColumnType())
    ).With(["1", "9223372036854775807"], ["2", "1"], ["3", "-2"]);

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString User = new("t.orders", "user");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly FieldDecimalNullable Amount = new("t.orders", "amount");

    private static readonly FieldString Status = new("t.orders", "status");

    private static readonly EqualStringRow Paid = new(Status, new LiteralString("paid"));

    private static readonly EqualStringRow New = new(Status, new LiteralString("new"));

    [Fact]
    public void AllPrecedingRowsIsARunningTotal()
    {
        Assert.Equal(
            ["10", "30", "60", "100", "150", "210", "280", "360"],
            Values(
                new SumIntegerProjection(Total, over: ById(preceding: new All()))
            )
        );
    }

    [Fact]
    public void RunningTotalStartsAgainInEachPartition()
    {
        Assert.Equal(
            ["10", "20", "40", "80", "70", "140", "140", "220"],
            Values(
                new SumIntegerProjection(Total, over: PerUser(preceding: new All()))
            )
        );
    }

    [Fact]
    public void AllFollowingRowsIsTheTotalStillToComeTheCurrentRowIncluded()
    {
        Assert.Equal(
            ["220", "140", "210", "180", "120", "140", "70", "80"],
            Values(
                new SumIntegerProjection(Total, over: PerUser(following: new All()))
            )
        );
    }

    [Fact]
    public void OnePrecedingRowIsTheCurrentRowAndTheRowBefore()
    {
        Assert.Equal(
            ["10", "30", "50", "70", "90", "110", "130", "150"],
            Values(new SumIntegerProjection(Total, over: ById(preceding: 1L)))
        );
    }

    [Fact]
    public void FrameOfARowWithItsNeighboursStopsAtTheEndsOfThePartition()
    {
        Assert.Equal(
            ["40", "70", "80", "130", "140", "180", "120", "140"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: PerUser(preceding: 1L, following: 1L)
                )
            )
        );
    }

    [Fact]
    public void FollowingRowsWithoutPrecedingStartAtTheCurrentRow()
    {
        Assert.Equal(
            ["60", "90", "120", "150", "180", "210", "150", "80"],
            Values(new SumIntegerProjection(Total, over: ById(following: 2L)))
        );
    }

    [Fact]
    public void AllPrecedingRowsWithANumberOfFollowingRows()
    {
        Assert.Equal(
            ["40", "70", "80", "140", "140", "220", "140", "220"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: PerUser(preceding: new All(), following: 1L)
                )
            )
        );
    }

    [Fact]
    public void NumberOfPrecedingRowsWithAllFollowingRows()
    {
        Assert.Equal(
            ["220", "140", "220", "220", "140", "210", "140", "180"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: PerUser(preceding: 2L, following: new All())
                )
            )
        );
    }

    [Fact]
    public void FrameLargerThanThePartitionIsTheWholePartition()
    {
        Assert.Equal(
            ["220", "140", "220", "220", "140", "220", "140", "220"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: PerUser(preceding: 100L, following: 100L)
                )
            )
        );
    }

    [Theory]
    [InlineData(0, "10,20,30,40,50,60,70,80")]
    [InlineData(1, "10,30,50,70,90,110,130,150")]
    [InlineData(2, "10,30,60,90,120,150,180,210")]
    [InlineData(7, "10,30,60,100,150,210,280,360")]
    [InlineData(8, "10,30,60,100,150,210,280,360")]
    [InlineData(long.MaxValue, "10,30,60,100,150,210,280,360")]
    public void PrecedingReadsItsCountFromAParameter(long rows, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new SumIntegerProjection(
                    Total,
                    over: ById(preceding: new ParamInteger("rows"))
                ),
                parameters: Bound(("rows", rows))
            )
        );
    }

    [Theory]
    [InlineData(0, "10,20,30,40,50,60,70,80")]
    [InlineData(1, "30,50,70,90,110,130,150,80")]
    [InlineData(3, "100,140,180,220,260,210,150,80")]
    [InlineData(long.MaxValue, "360,350,330,300,260,210,150,80")]
    public void FollowingReadsItsCountFromAParameter(long rows, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new SumIntegerProjection(
                    Total,
                    over: ById(following: new ParamInteger("rows"))
                ),
                parameters: Bound(("rows", rows))
            )
        );
    }

    [Theory]
    [InlineData(0, 0, "10,20,30,40,50,60,70,80")]
    [InlineData(1, 0, "10,20,40,70,70,100,120,140")]
    [InlineData(0, 2, "80,140,130,180,120,140,70,80")]
    [InlineData(1, 1, "40,70,80,130,140,180,120,140")]
    public void BothSidesReadTheirCountsFromParameters(
        long before,
        long after,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new SumIntegerProjection(
                    Total,
                    over: PerUser(
                        preceding: new ParamInteger("before"),
                        following: new ParamInteger("after")
                    )
                ),
                parameters: Bound(("before", before), ("after", after))
            )
        );
    }

    [Fact]
    public void OneParameterSizesEveryFrameThatReadsIt()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection(
                        "before",
                        new SumIntegerProjection(
                            Total,
                            over: ById(preceding: new ParamInteger("rows"))
                        )
                    ),
                    new SelectItemProjection(
                        "after",
                        new SumIntegerProjection(
                            Total,
                            over: ById(following: new ParamInteger("rows"))
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            Bound(("rows", 2))
        );

        Assert.Equal(
            ["10", "30", "60", "90", "120", "150", "180", "210"],
            ResultColumn.Texts(projection, "before")
        );
        Assert.Equal(
            ["60", "90", "120", "150", "180", "210", "150", "80"],
            ResultColumn.Texts(projection, "after")
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    [InlineData(long.MinValue)]
    public void PrecedingRejectsACountParameterBelowZero(long rows)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(
                    new SumIntegerProjection(
                        Total,
                        over: ById(preceding: new ParamInteger("rows"))
                    ),
                    null
                ),
                Bound(("rows", rows))
            )
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    [InlineData(long.MinValue)]
    public void FollowingRejectsACountParameterBelowZero(long rows)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(
                    new SumIntegerProjection(
                        Total,
                        over: ById(
                            preceding: new All(),
                            following: new ParamInteger("rows")
                        )
                    ),
                    null
                ),
                Bound(("rows", rows))
            )
        );
    }

    [Fact]
    public void FrameFollowsADescendingOrder()
    {
        Assert.Equal(
            ["360", "350", "330", "300", "260", "210", "150", "80"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithFrame(
                        [new OrderItemRow(Id, SortDirection.Desc)],
                        new Frame(preceding: new All())
                    )
                )
            )
        );
    }

    [Fact]
    public void FrameFollowsEveryKeyOfTheOrder()
    {
        Assert.Equal(
            ["40", "70", "70", "100", "120", "140", "80", "80"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithFrame(
                        [
                            new OrderItemRow(User),
                            new OrderItemRow(Id, SortDirection.Desc),
                        ],
                        new Frame(preceding: 1L)
                    )
                )
            )
        );
    }

    [Fact]
    public void FrameFollowsAComputedKey()
    {
        Assert.Equal(
            ["360", "350", "330", "300", "260", "210", "150", "80"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithFrame(
                        [
                            new OrderItemRow(
                                new SubtractIntegerRow([new LiteralInteger(0), Total])
                            ),
                        ],
                        new Frame(preceding: new All())
                    )
                )
            )
        );
    }

    [Fact]
    public void OrderOfTheWindowPutsNullKeysFirstAscending()
    {
        Assert.Equal(
            ["5", "1", "6", "2", "8", "4", "7", "3"],
            Values(
                new CountProjection(
                    over: new WindowWithFrame(
                        [new OrderItemRow(Amount), new OrderItemRow(Id)],
                        new Frame(preceding: new All())
                    )
                )
            )
        );
    }

    [Fact]
    public void OrderOfTheWindowPutsNullKeysLastDescending()
    {
        Assert.Equal(
            ["4", "6", "3", "7", "1", "5", "2", "8"],
            Values(
                new CountProjection(
                    over: new WindowWithFrame(
                        [
                            new OrderItemRow(Amount, SortDirection.Desc),
                            new OrderItemRow(Id),
                        ],
                        new Frame(preceding: new All())
                    )
                )
            )
        );
    }

    [Fact]
    public void CountOverAFrameIsTheNumberOfItsRows()
    {
        Assert.Equal(
            ["2", "2", "3", "3", "3", "3", "2", "2"],
            Values(new CountProjection(over: PerUser(preceding: 1L, following: 1L)))
        );
    }

    [Fact]
    public void PredicateFiltersTheRowsOfTheFrameAndDoesNotMoveIt()
    {
        Assert.Equal(
            ["1", "1", "2", "1", "2", "1", "2", "2"],
            Values(new CountProjection(predicate: Paid, over: ById(preceding: 2L)))
        );
    }

    [Fact]
    public void SumWithAPredicateAddsOnlyTheRowsOfTheFrameThatSatisfyIt()
    {
        Assert.Equal(
            ["10", "10", "40", "30", "80", "50", "120", "150"],
            Values(
                new SumIntegerProjection(
                    Total,
                    predicate: Paid,
                    over: ById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void FrameOfSevenRowsWithAPredicateEveryRowSatisfiesCountsAtMostSeven()
    {
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "7"],
            Values(
                new CountProjection(
                    predicate: new GreaterThanDecimalRow(Total, new LiteralInteger(0)),
                    over: ById(preceding: 6L)
                )
            )
        );
    }

    [Fact]
    public void FrameOfSevenRowsWithAPredicateCountsTheRowsAmongThemThatSatisfyIt()
    {
        Assert.Equal(
            ["0", "1", "1", "2", "2", "3", "3", "3"],
            Values(new CountProjection(predicate: New, over: ById(preceding: 6L)))
        );
    }

    [Fact]
    public void AverageOverAFrameOfANonNullSelectorIsNeverNull()
    {
        Assert.Equal(
            ["15", "20", "30", "40", "50", "60", "70", "75"],
            Values(
                new AverageDecimalProjection(
                    Total,
                    over: ById(preceding: 1L, following: 1L)
                )
            )
        );
    }

    [Fact]
    public void AverageOverAFrameOfOnlyNullValuesIsNull()
    {
        Assert.Equal(
            ["1.5", "", "2", "2.5", "4", "0.5", "3.5", "0.5"],
            Values(
                new AverageDecimalNullableProjection(
                    Amount,
                    over: PerUser(preceding: 1L)
                )
            )
        );
    }

    [Fact]
    public void AverageOverAFrameIsDividedAsDivideDoes()
    {
        Assert.Equal(
            [
                "10",
                "20",
                "26.66666666666666666666666667",
                "43.33333333333333333333333333",
                "60",
            ],
            Values(
                new AverageDecimalProjection(Total, over: ById(preceding: 2L)),
                where: new EqualStringRow(User, new LiteralString("a"))
            )
        );
    }

    [Fact]
    public void MaxOverARunningFrameIsTheGreatestValueSoFar()
    {
        Assert.Equal(
            ["1.5", "", "2.5", "2.5", "4", "2.5", "4", "2.5"],
            Values(
                new MaxDecimalNullableProjection(
                    Amount,
                    over: PerUser(preceding: new All())
                )
            )
        );
    }

    [Fact]
    public void MinOverAFrameOfFollowingRowsSkipsNulls()
    {
        Assert.Equal(
            ["1.5", "2.5", "2.5", "4", "0.5", "0.5", "3", ""],
            Values(new MinDecimalNullableProjection(Amount, over: ById(following: 1L)))
        );
    }

    [Fact]
    public void FirstSortsTheRowsOfTheFrameByItsOwnOrder()
    {
        Assert.Equal(
            ["1", "2", "2", "2", "4", "4", "6", "8"],
            Values(
                new FirstIntegerProjection(
                    Id,
                    [new OrderItemRow(Amount), new OrderItemRow(Id)],
                    over: ById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void LastSortsTheRowsOfTheFrameByItsOwnOrder()
    {
        Assert.Equal(
            ["1", "1", "3", "3", "5", "5", "5", "7"],
            Values(
                new LastIntegerProjection(
                    Id,
                    [new OrderItemRow(Amount), new OrderItemRow(Id)],
                    over: ById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void ConcatAllJoinsTheRowsOfTheFrameInItsOwnOrder()
    {
        Assert.Equal(
            ["a", "ba", "aba", "aab", "baa", "aba", "bab", "aba"],
            Values(
                new ConcatAllProjection(
                    new AsNullableStringNullableRow(User),
                    new LiteralString(""),
                    [new OrderItemRow(Id, SortDirection.Desc)],
                    over: ById(preceding: 2L)
                )
            )
        );
    }

    [Fact]
    public void MedianOverARunningFrameIsTheMiddleOfTheValuesSoFar()
    {
        Assert.Equal(
            ["1.5", "1.5", "2", "2", "2.5", "2", "2.5", "2.5"],
            Values(
                new MedianDecimalNullableProjection(
                    Amount,
                    over: ById(preceding: new All())
                )
            )
        );
    }

    [Fact]
    public void CountDistinctOverAFrameCountsTheDifferentValuesOfItsRows()
    {
        Assert.Equal(
            ["1", "2", "2", "1", "2", "2", "2", "2"],
            Values(new CountDistinctProjection(User, over: ById(preceding: 1L)))
        );
    }

    [Fact]
    public void AnyOverAFrameIsTrueWhenOneOfItsRowsSatisfiesThePredicate()
    {
        Assert.Equal(
            ["True", "True", "True", "True", "True", "True", "False", "False"],
            Values(new AnyProjection(New, over: ById(following: 1L)))
        );
    }

    [Fact]
    public void AllOverAFrameIsTrueWhenEachOfItsRowsSatisfiesThePredicate()
    {
        Assert.Equal(
            ["True", "False", "False", "False", "False", "False", "False", "True"],
            Values(new AllProjection(Paid, over: ById(preceding: 1L)))
        );
    }

    [Fact]
    public void FrameCountsTheRowsLeftByWhere()
    {
        Assert.Equal(
            ["10", "40", "80", "120", "150"],
            Values(
                new SumIntegerProjection(Total, over: ById(preceding: 1L)),
                where: Paid
            )
        );
    }

    [Fact]
    public void SumOverAFrameIsExactWhateverItsPartialSums()
    {
        Assert.Equal(
            ["9223372036854775806", "-1", "-2"],
            NumberSums(new Frame(following: new All()))
        );
    }

    [Fact]
    public void SumOverAFrameBeyondSixtyFourBitsIsAnError()
    {
        _ = Assert.Throws<OverflowException>(() =>
            NumberSums(new Frame(preceding: new All()))
        );
    }

    private static IReadOnlyList<string> NumberSums(Frame frame)
    {
        FieldInteger id = new("t.numbers", "id");
        PureQLProjection projection = new(
            [Numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new SumIntegerProjection(
                            new FieldInteger("t.numbers", "n"),
                            over: new WindowWithFrame([new OrderItemRow(id)], frame)
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(id)]
            )
        );

        return ResultColumn.Texts(projection, "v");
    }

    private static WindowWithFrame ById(
        FrameRows? preceding = null,
        FrameRows? following = null
    )
    {
        return new WindowWithFrame(
            [new OrderItemRow(Id)],
            new Frame(preceding, following)
        );
    }

    private static WindowWithFrame PerUser(
        FrameRows? preceding = null,
        FrameRows? following = null
    )
    {
        return new WindowWithFrame(
            [new OrderItemRow(Id)],
            new Frame(preceding, following),
            [User]
        );
    }

    private static Dictionary<string, IParameter> Bound(
        params (string Name, long Value)[] parameters
    )
    {
        return parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => (IParameter)new IntegerParameter(new Long(parameter.Value))
        );
    }

    private static MainPlainQueryWithoutPagination Query(
        ExprProjection expression,
        BooleanRow? where
    )
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.orders"),
            where: where,
            select: [new SelectItemProjection("v", expression)],
            orderBy: [new OrderItemProjectionExpression(Id)]
        );
    }

    private static IReadOnlyList<string> Values(
        ExprProjection expression,
        BooleanRow? where = null,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        PureQLProjection projection = new(
            [Orders],
            Query(expression, where),
            parameters ?? []
        );

        return ResultColumn.Texts(projection, "v");
    }
}
