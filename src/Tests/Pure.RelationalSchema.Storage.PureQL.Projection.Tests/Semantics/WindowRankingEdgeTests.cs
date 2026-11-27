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

// Edge cases of the ranking functions and of previous / next, every value
// derived from Window evaluation: a partition holds the rows equal on every
// partitionBy key compared with equal (null keys are one partition, 1 and
// 1.0 the same key), it is sorted as the query's orderBy sorts (each type by
// the order of Ordering and equality: strings by code point, uuids by their
// hexadecimal digits, false before true, null first ascending and last
// descending), and rows equal on every key are peers. rank is 1 plus the
// rows before that are not peers, denseRank 1 plus the groups of peers
// before, percentRank (rank - 1) / (n - 1) (0 when n is 1),
// cumulativeDistribution the rows before or peers over n, both divided as
// divide does; ntile fills buckets of n / buckets rows, the first n mod
// buckets of one more. previous / next read the operand offset rows away, a
// null found there as it is, and the default (evaluated in the current row)
// only where the partition has no such row. Every column is read in the
// order of id.
public sealed class WindowRankingEdgeTests
{
    // grp a: 1 (2.5), 2 (2.50), 3 (null), 4 (1), 8 (4); grp b: 5 (3);
    // grp null: 6 (7), 7 (7), 9 (null).
    private static readonly IStoredSchemaDataSet Scores = new InlineTable(
        "t",
        "scores",
        ("id", new LongColumnType()),
        ("grp", new StringColumnType()),
        ("score", new DoubleColumnType()),
        ("flag", new BoolColumnType())
    ).With(
        ["1", "a", "2.5", "True"],
        ["2", "a", "2.50", "False"],
        ["3", "a", "", "True"],
        ["4", "a", "1", ""],
        ["5", "b", "3", "False"],
        ["6", "", "7", "True"],
        ["7", "", "7", "False"],
        ["8", "a", "4", "True"],
        ["9", "", "", ""]
    );

    // Code points: a (U+0061) < U+E000 < U+FF61 < U+1F600 < U+1F601, where
    // UTF-16 code units put both emoji (high surrogate U+D83D) before U+E000.
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

    // In the order of their hexadecimal digits: 6, 4, 3, 2, 1, 5.
    private static readonly IStoredSchemaDataSet Uids = new InlineTable(
        "t",
        "uids",
        ("id", new LongColumnType()),
        ("uid", new UuidColumnType())
    ).With(
        ["1", "00000002-0000-0000-0000-000000000000"],
        ["2", "00000001-0000-0000-0000-000000000001"],
        ["3", "00000001-0000-0000-0000-000000000000"],
        ["4", "00000000-0001-0000-0000-000000000000"],
        ["5", "ff000000-0000-0000-0000-000000000000"],
        ["6", "00000000-0000-0000-0100-000000000000"]
    );

    // Ascending by at, null first: 3, 4, 1, 2.
    private static readonly IStoredSchemaDataSet Moments = new InlineTable(
        "t",
        "moments",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("clock", new TimeColumnType())
    ).With(
        ["1", "2024-01-01T00:00:00Z", "08:00:00"],
        ["2", "2024-01-01T00:00:00.5Z", ""],
        ["3", "", "23:59:59.999999"],
        ["4", "2023-12-31T23:59:59Z", "00:00:00"]
    );

    private const string Third = "0.3333333333333333333333333333";

    private const string TwoThirds = "0.6666666666666666666666666667";

    private static readonly FieldInteger Id = new("t.scores", "id");

    private static readonly FieldStringNullable Grp = new("t.scores", "grp");

    private static readonly FieldDecimalNullable Score = new("t.scores", "score");

    private static readonly FieldBooleanNullable Flag = new("t.scores", "flag");

    private static readonly OrderedWindow PerGrpByScore = new(
        [new OrderItemRow(Score)],
        [Grp]
    );

    private static readonly OrderedWindow PerGrpByScoreDescending = new(
        [new OrderItemRow(Score, SortDirection.Desc)],
        [Grp]
    );

    private static readonly OrderedWindow PerGrpById = new([new OrderItemRow(Id)], [Grp]);

    private static readonly OrderedWindow ById = new([new OrderItemRow(Id)]);

    // Partition a sorted: 3 (null), 4 (1), 1 and 2 (2.5, peers), 8 (4).
    [Fact]
    public void RankTreatsDecimalsOfDifferentScaleAsPeersAndNullKeysAsOnePartition()
    {
        Assert.Equal(
            ["3", "3", "1", "2", "1", "2", "2", "5", "1"],
            Values(new RankProjection(PerGrpByScore))
        );
    }

    [Fact]
    public void DenseRankTreatsDecimalsOfDifferentScaleAsOneGroupOfPeers()
    {
        Assert.Equal(
            ["3", "3", "1", "2", "1", "2", "2", "4", "1"],
            Values(new DenseRankProjection(PerGrpByScore))
        );
    }

    [Fact]
    public void PercentRankOfPeersOfDifferentScaleIsTheRankOfTheFirstOfThem()
    {
        Assert.Equal(
            ["0.5", "0.5", "0", "0.25", "0", "0.5", "0.5", "1", "0"],
            Values(new PercentRankProjection(PerGrpByScore))
        );
    }

    [Fact]
    public void CumulativeDistributionCountsPeersOfDifferentScaleAndTheNullPartition()
    {
        Assert.Equal(
            ["0.8", "0.8", "0.2", "0.4", "1", "1", "1", "1", Third],
            Values(new CumulativeDistributionProjection(PerGrpByScore))
        );
    }

    // Descending, null last: partition a is 8, 1 and 2, 4, 3; the null
    // partition is 6 and 7, then 9.
    [Fact]
    public void RankDescendingPutsTheNullKeyLastInEveryPartitionTheNullOneIncluded()
    {
        Assert.Equal(
            ["2", "2", "5", "4", "1", "1", "1", "1", "3"],
            Values(new RankProjection(PerGrpByScoreDescending))
        );
    }

    [Fact]
    public void CumulativeDistributionDescendingCountsThePeersOfTheNullPartition()
    {
        Assert.Equal(
            ["0.6", "0.6", "1", "0.8", "1", TwoThirds, TwoThirds, "0.2", "1"],
            Values(new CumulativeDistributionProjection(PerGrpByScoreDescending))
        );
    }

    // Without a partition: 3 and 9 (null), 4 (1), 1 and 2 (2.5), 5 (3),
    // 8 (4), 6 and 7 (7) over nine rows.
    [Fact]
    public void PercentRankAndCumulativeDistributionOfNineRowsAreRoundedOnce()
    {
        OrderedWindow byScore = new([new OrderItemRow(Score)]);

        Assert.Equal(
            ["0.375", "0.375", "0", "0.25", "0.625", "0.875", "0.875", "0.75", "0"],
            Values(new PercentRankProjection(byScore))
        );
        Assert.Equal(
            [
                "0.5555555555555555555555555556",
                "0.5555555555555555555555555556",
                "0.2222222222222222222222222222",
                Third,
                TwoThirds,
                "1",
                "1",
                "0.7777777777777777777777777778",
                "0.2222222222222222222222222222",
            ],
            Values(new CumulativeDistributionProjection(byScore))
        );
    }

    // Ordering and equality: false before true, null first ascending.
    [Fact]
    public void RankOfABooleanKeyPutsNullThenFalseThenTrue()
    {
        OrderedWindow byFlag = new([new OrderItemRow(Flag)]);

        Assert.Equal(
            ["6", "3", "6", "1", "3", "6", "3", "6", "1"],
            Values(new RankProjection(byFlag))
        );
        Assert.Equal(
            ["3", "2", "3", "1", "2", "3", "2", "3", "1"],
            Values(new DenseRankProjection(byFlag))
        );
    }

    // Rows equal on every key are peers: a key that is the same in every row
    // makes every row of the partition a peer of every other.
    [Fact]
    public void KeyThatIsAParameterMakesEveryRowOfThePartitionAPeer()
    {
        OrderedWindow byParameter = new([new OrderItemRow(new ParamInteger("p"))], [Grp]);
        Dictionary<string, IParameter> parameters = new()
        {
            ["p"] = new IntegerParameter(new Long(5)),
        };

        Assert.Equal(
            Enumerable.Repeat("1", 9),
            Values(new RankProjection(byParameter), parameters: parameters)
        );
        Assert.Equal(
            Enumerable.Repeat("1", 9),
            Values(new DenseRankProjection(byParameter), parameters: parameters)
        );
        Assert.Equal(
            Enumerable.Repeat("0", 9),
            Values(new PercentRankProjection(byParameter), parameters: parameters)
        );
        Assert.Equal(
            Enumerable.Repeat("1", 9),
            Values(
                new CumulativeDistributionProjection(byParameter),
                parameters: parameters
            )
        );
    }

    [Fact]
    public void KeyThatIsALiteralMakesEveryRowAPeerAndTheNextKeyOrdersThem()
    {
        Assert.Equal(
            Enumerable.Repeat("1", 9),
            Values(
                new RankProjection(
                    new OrderedWindow([new OrderItemRow(new LiteralString("k"))])
                )
            )
        );
        Assert.Equal(
            ["9", "8", "7", "6", "5", "4", "3", "2", "1"],
            Values(
                new RowNumberProjection(
                    new OrderedWindow([
                        new OrderItemRow(new LiteralString("k")),
                        new OrderItemRow(Id, SortDirection.Desc),
                    ])
                )
            )
        );
    }

    // grp ascending (null first), then score descending (null last): (null,
    // 7) 6 and 7, (null, null) 9, (a, 4) 8, (a, 2.5) 1 and 2, (a, 1) 4,
    // (a, null) 3, (b, 3) 5.
    [Fact]
    public void RankComparesKeysOfMixedDirectionsAndNullsInTurn()
    {
        OrderedWindow mixed = new([
            new OrderItemRow(Grp),
            new OrderItemRow(Score, SortDirection.Desc),
        ]);

        Assert.Equal(
            ["5", "5", "8", "7", "9", "1", "1", "4", "3"],
            Values(new RankProjection(mixed))
        );
        Assert.Equal(
            ["4", "4", "6", "5", "7", "1", "1", "3", "2"],
            Values(new DenseRankProjection(mixed))
        );
    }

    [Fact]
    public void RowNumberCountsInPartitionsOfTwoKeysWithNullKeysEqual()
    {
        Assert.Equal(
            ["1", "1", "2", "1", "1", "1", "1", "3", "1"],
            Values(
                new RowNumberProjection(
                    new OrderedWindow([new OrderItemRow(Id)], [Grp, Flag])
                )
            )
        );
    }

    // Partitions of 5, 1 and 3 rows cut into two buckets: 3 + 2, 1, 2 + 1.
    [Fact]
    public void NtileCutsEveryPartitionByItsOwnSizeTheNullPartitionIncluded()
    {
        Assert.Equal(
            ["1", "1", "1", "2", "1", "1", "1", "2", "2"],
            Values(new NtileProjection(new LiteralInteger(2), PerGrpById))
        );
    }

    // id mod 3 partitions {1, 4, 7}, {2, 5, 8} and {3, 6, 9}, each sorted by
    // id descending and cut into buckets of 2 and 1 rows.
    [Fact]
    public void NtileCutsPartitionsOfAComputedKey()
    {
        Assert.Equal(
            ["2", "2", "2", "1", "1", "1", "1", "1", "1"],
            Values(
                new NtileProjection(
                    new LiteralInteger(2),
                    new OrderedWindow(
                        [new OrderItemRow(Id, SortDirection.Desc)],
                        [new ModuloIntegerRow(Id, new LiteralInteger(3))]
                    )
                )
            )
        );
    }

    [Fact]
    public void NtileOfTheLargestNumberOfBucketsNumbersTheRows()
    {
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8", "9"],
            Values(new NtileProjection(new LiteralInteger(long.MaxValue), ById))
        );
    }

    [Fact]
    public void WindowFunctionsOverNoRowsReturnNoRows()
    {
        PureQLProjection projection = new(
            [Scores],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.scores"),
                where: new LiteralBoolean(false),
                select:
                [
                    new SelectItemProjection("rank", new RankProjection(PerGrpByScore)),
                    new SelectItemProjection(
                        "ntile",
                        new NtileProjection(new LiteralInteger(3), ById)
                    ),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(Id, ById)
                    ),
                    new SelectItemProjection(
                        "spread",
                        new CumulativeDistributionProjection(ById)
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Empty(ResultColumn.Texts(projection, "rank"));
    }

    [Fact]
    public void EveryWindowFunctionOfAPartitionOfOneRow()
    {
        PureQLProjection projection = new(
            [Scores],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.scores"),
                where: new EqualDecimalRow(Id, new LiteralInteger(5)),
                select:
                [
                    new SelectItemProjection("number", new RowNumberProjection(ById)),
                    new SelectItemProjection("rank", new RankProjection(ById)),
                    new SelectItemProjection("dense", new DenseRankProjection(ById)),
                    new SelectItemProjection("percent", new PercentRankProjection(ById)),
                    new SelectItemProjection(
                        "spread",
                        new CumulativeDistributionProjection(ById)
                    ),
                    new SelectItemProjection(
                        "ntile",
                        new NtileProjection(new LiteralInteger(3), ById)
                    ),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(Id, ById)
                    ),
                    new SelectItemProjection(
                        "after",
                        new NextIntegerProjection(Id, new LiteralInteger(0), ById)
                    ),
                ]
            )
        );

        Assert.Equal(["1"], ResultColumn.Texts(projection, "number"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "rank"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "dense"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "percent"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "spread"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "ntile"));
        Assert.Equal([""], ResultColumn.Texts(projection, "before"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "after"));
    }

    // By code point: 3 (a), 4 (U+E000), 2 (U+FF61), 1 (U+1F600), 5 (U+1F601).
    [Fact]
    public void RowNumberOrdersStringsByCodePoint()
    {
        Assert.Equal(
            ["4", "3", "1", "2", "5"],
            TagValues(new RowNumberProjection(new OrderedWindow([new OrderItemRow(Tag)])))
        );
    }

    [Fact]
    public void RankDescendingOrdersStringsByCodePointInEachPartition()
    {
        Assert.Equal(
            ["1", "2", "3", "2", "1"],
            TagValues(
                new RankProjection(
                    new OrderedWindow(
                        [new OrderItemRow(Tag, SortDirection.Desc)],
                        [new FieldString("t.tags", "grp")]
                    )
                )
            )
        );
    }

    [Fact]
    public void PreviousFollowsTheCodePointOrderOfStrings()
    {
        Assert.Equal(
            ["｡", "", "", "a", "\U0001F600"],
            TagValues(
                new PreviousStringNullableProjection(
                    Tag,
                    new OrderedWindow([new OrderItemRow(Tag)])
                )
            )
        );
    }

    // By hexadecimal digits: 6, 4, 3, 2, 1, 5.
    [Fact]
    public void RowNumberOrdersUuidsByTheirHexadecimalDigits()
    {
        PureQLProjection projection = new(
            [Uids],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.uids"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new RowNumberProjection(
                            new OrderedWindow([
                                new OrderItemRow(new FieldUuid("t.uids", "uid")),
                            ])
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.uids", "id")),
                ]
            )
        );

        Assert.Equal(["5", "4", "3", "2", "6", "1"], ResultColumn.Texts(projection, "v"));
    }

    // Ascending by at: 3 (null), 4, 1, 2. The row before 4 is 3, whose at is
    // null: that null is returned, the default only stands for no row.
    [Fact]
    public void PreviousOfADatetimeReturnsTheNullOfTheRowBeforeAndTheDefaultWhereNone()
    {
        Assert.Equal(
            ["2023-12-31T23:59:59Z", "2024-01-01T00:00:00Z", "2000-01-01T00:00:00Z", ""],
            MomentValues(
                new PreviousDatetimeNullableProjection(
                    At,
                    new OrderedWindow([new OrderItemRow(At)]),
                    @default: new LiteralDatetime(
                        new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    )
                )
            )
        );
    }

    // Descending by at, null last: 2, 1, 4, 3.
    [Fact]
    public void NextOfATimeFollowsADescendingDatetimeOrderWithTheNullLast()
    {
        Assert.Equal(
            ["00:00:00", "08:00:00", "", "23:59:59.999999"],
            MomentValues(
                new NextTimeNullableProjection(
                    new FieldTimeNullable("t.moments", "clock"),
                    new OrderedWindow([new OrderItemRow(At, SortDirection.Desc)])
                )
            )
        );
    }

    // grp a by id: 1, 2, 3, 4, 8; b: 5; null: 6, 7, 9.
    [Fact]
    public void PreviousAtAnOffsetStaysInsideEveryPartitionTheNullOneIncluded()
    {
        Assert.Equal(
            ["-1", "-1", "1", "2", "-1", "-1", "-1", "3", "6"],
            Values(
                new PreviousIntegerProjection(
                    Id,
                    new LiteralInteger(-1),
                    PerGrpById,
                    offset: new LiteralInteger(2)
                )
            )
        );
    }

    // The score of the next row as it is (2.50 is 2.5, a null stays null),
    // or ten times the current id where the partition has no next row.
    [Fact]
    public void NextOfANullableDecimalWithAComputedIntegerDefault()
    {
        Assert.Equal(
            ["2.5", "", "1", "4", "50", "7", "", "80", "90"],
            Values(
                new NextDecimalNullableProjection(
                    Score,
                    PerGrpById,
                    @default: new MultiplyIntegerRow([Id, new LiteralInteger(10)])
                )
            )
        );
    }

    [Fact]
    public void PreviousOfALiteralWithoutDefaultIsNullOnlyInTheFirstRowOfEachPartition()
    {
        Assert.Equal(
            ["", "x", "x", "x", "", "", "x", "x", "x"],
            Values(
                new PreviousStringNullableProjection(new LiteralString("x"), PerGrpById)
            )
        );
    }

    // Descending by score, then by id: a is 8, 1, 2, 4, 3; b is 5; the null
    // partition is 6, 7, 9.
    [Fact]
    public void PreviousOrdersPeersOfDifferentScaleByTheNextKey()
    {
        Assert.Equal(
            ["8", "1", "4", "2", "", "", "6", "", "7"],
            Values(
                new PreviousIntegerNullableProjection(
                    Id,
                    new OrderedWindow(
                        [
                            new OrderItemRow(Score, SortDirection.Desc),
                            new OrderItemRow(Id),
                        ],
                        [Grp]
                    )
                )
            )
        );
    }

    private static readonly FieldString Tag = new("t.tags", "tag");

    private static readonly FieldDatetimeNullable At = new("t.moments", "at");

    private static IReadOnlyList<string> TagValues(ExprProjection expression)
    {
        return Column(Tags, "tags", expression);
    }

    private static IReadOnlyList<string> MomentValues(ExprProjection expression)
    {
        return Column(Moments, "moments", expression);
    }

    private static IReadOnlyList<string> Column(
        IStoredSchemaDataSet table,
        string name,
        ExprProjection expression
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
            )
        );

        return ResultColumn.Texts(projection, "v");
    }

    private static IReadOnlyList<string> Values(
        ExprProjection expression,
        BooleanRow? where = null,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        PureQLProjection projection = new(
            [Scores],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.scores"),
                where: where,
                select: [new SelectItemProjection("v", expression)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            parameters ?? []
        );

        return ResultColumn.Texts(projection, "v");
    }
}
