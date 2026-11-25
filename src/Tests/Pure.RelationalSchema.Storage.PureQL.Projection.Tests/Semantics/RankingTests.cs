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

// The ranking functions as Window evaluation defines them, in a partition of
// n rows sorted by the window's orderBy (null first ascending, last
// descending): rowNumber is the position from 1; rank is 1 plus the rows
// before that are not peers and denseRank 1 plus the groups of peers before;
// percentRank is (rank - 1) / (n - 1), 0 when n is 1, and
// cumulativeDistribution the rows before or peers, itself included, over n,
// both divided as divide does; ntile fills buckets of n / buckets rows, the
// first n mod buckets of one more, and numbers the rows when there are more
// buckets than rows. buckets is a literal or a parameter read once, and a
// parameter below 1 is rejected. Every column is read in the order of id.
public sealed class RankingTests
{
    // user a: 10 (2), 20 (6), 30 (1), 30 (3), 40 (9); user b: null (5),
    // 20 (4), 20 (7); user c: 50 (8).
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType())
    ).With(
        ["1", "a", "30"],
        ["2", "a", "10"],
        ["3", "a", "30"],
        ["4", "b", "20"],
        ["5", "b", ""],
        ["6", "a", "20"],
        ["7", "b", "20"],
        ["8", "c", "50"],
        ["9", "a", "40"]
    );

    private const string Third = "0.3333333333333333333333333333";

    private const string TwoThirds = "0.6666666666666666666666666667";

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString User = new("t.orders", "user");

    private static readonly FieldIntegerNullable Total = new("t.orders", "total");

    private static readonly OrderedWindow ByTotal = new([new OrderItemRow(Total)]);

    private static readonly OrderedWindow ByTotalDescending = new([
        new OrderItemRow(Total, SortDirection.Desc),
    ]);

    private static readonly OrderedWindow PerUserByTotal = new(
        [new OrderItemRow(Total)],
        [User]
    );

    private static readonly OrderedWindow PerUserByTotalDescending = new(
        [new OrderItemRow(Total, SortDirection.Desc)],
        [User]
    );

    private static readonly OrderedWindow ById = new([new OrderItemRow(Id)]);

    private static readonly OrderedWindow PerUserById = new(
        [new OrderItemRow(Id)],
        [User]
    );

    [Fact]
    public void RowNumberCountsFromOneInEachPartition()
    {
        Assert.Equal(
            ["3", "1", "4", "2", "1", "2", "3", "1", "5"],
            Values(
                new RowNumberProjection(
                    new OrderedWindow(
                        [new OrderItemRow(Total), new OrderItemRow(Id)],
                        [User]
                    )
                )
            )
        );
    }

    [Fact]
    public void RowNumberWithoutPartitionCountsEveryRowWithNullLastDescending()
    {
        Assert.Equal(
            ["3", "8", "4", "5", "9", "6", "7", "1", "2"],
            Values(
                new RowNumberProjection(
                    new OrderedWindow([
                        new OrderItemRow(Total, SortDirection.Desc),
                        new OrderItemRow(Id),
                    ])
                )
            )
        );
    }

    [Fact]
    public void RowNumberFollowsTheSecondKeyDescendingAmongEqualFirstKeys()
    {
        Assert.Equal(
            ["4", "1", "3", "3", "1", "2", "2", "1", "5"],
            Values(
                new RowNumberProjection(
                    new OrderedWindow(
                        [
                            new OrderItemRow(Total),
                            new OrderItemRow(Id, SortDirection.Desc),
                        ],
                        [User]
                    )
                )
            )
        );
    }

    [Fact]
    public void RankGivesPeersTheFirstPositionAndLeavesAGapInEachPartition()
    {
        Assert.Equal(
            ["3", "1", "3", "2", "1", "2", "2", "1", "5"],
            Values(new RankProjection(PerUserByTotal))
        );
    }

    [Fact]
    public void RankWithoutPartitionPutsTheNullKeyFirst()
    {
        Assert.Equal(
            ["6", "2", "6", "3", "1", "3", "3", "9", "8"],
            Values(new RankProjection(ByTotal))
        );
    }

    [Fact]
    public void RankDescendingPutsTheNullKeyLast()
    {
        Assert.Equal(
            ["2", "5", "2", "1", "3", "4", "1", "1", "1"],
            Values(new RankProjection(PerUserByTotalDescending))
        );
    }

    [Fact]
    public void RankComparesEveryKeyOfTheOrder()
    {
        Assert.Equal(
            ["2", "5", "2", "6", "8", "4", "6", "9", "1"],
            Values(
                new RankProjection(
                    new OrderedWindow([
                        new OrderItemRow(User),
                        new OrderItemRow(Total, SortDirection.Desc),
                    ])
                )
            )
        );
    }

    [Fact]
    public void RankOfRowsThatAreAllPeersIsOne()
    {
        Assert.Equal(
            ["1", "1", "1", "1", "1", "1", "1", "1", "1"],
            Values(new RankProjection(new OrderedWindow([new OrderItemRow(User)], [User])))
        );
    }

    [Fact]
    public void DenseRankCountsTheGroupsOfPeersBeforeInEachPartition()
    {
        Assert.Equal(
            ["3", "1", "3", "2", "1", "2", "2", "1", "4"],
            Values(new DenseRankProjection(PerUserByTotal))
        );
    }

    [Fact]
    public void DenseRankWithoutPartitionHasNoGaps()
    {
        Assert.Equal(
            ["4", "2", "4", "3", "1", "3", "3", "6", "5"],
            Values(new DenseRankProjection(ByTotal))
        );
    }

    [Fact]
    public void DenseRankDescendingCountsFromTheGreatestKey()
    {
        Assert.Equal(
            ["3", "5", "3", "4", "6", "4", "4", "1", "2"],
            Values(new DenseRankProjection(ByTotalDescending))
        );
    }

    [Fact]
    public void PercentRankIsTheRankBelowOneOverTheOtherRowsOfThePartition()
    {
        Assert.Equal(
            ["0.5", "0", "0.5", "0.5", "0", "0.25", "0.5", "0", "1"],
            Values(new PercentRankProjection(PerUserByTotal))
        );
    }

    [Fact]
    public void PercentRankWithoutPartitionDividesByEveryOtherRow()
    {
        Assert.Equal(
            ["0.625", "0.125", "0.625", "0.25", "0", "0.25", "0.25", "1", "0.875"],
            Values(new PercentRankProjection(ByTotal))
        );
    }

    [Fact]
    public void PercentRankDividesAsDivideDoesRoundedOnce()
    {
        // Among the four rows from id 6: 20 (6), 20 (7), 40 (9), 50 (8).
        Assert.Equal(
            ["0", "0", "1", TwoThirds],
            Values(
                new PercentRankProjection(ByTotal),
                new GreaterThanDecimalRow(Id, new LiteralInteger(5))
            )
        );
    }

    [Fact]
    public void PercentRankOfAPartitionOfOneRowIsZero()
    {
        Assert.Equal(
            ["0"],
            Values(
                new PercentRankProjection(PerUserByTotal),
                new EqualStringRow(User, new LiteralString("c"))
            )
        );
    }

    [Fact]
    public void CumulativeDistributionCountsTheRowsBeforeAndThePeersInEachPartition()
    {
        Assert.Equal(
            ["0.8", "0.2", "0.8", "1", Third, "0.4", "1", "1", "1"],
            Values(new CumulativeDistributionProjection(PerUserByTotal))
        );
    }

    [Fact]
    public void CumulativeDistributionDescendingRoundsTwoThirdsHalfAwayFromZero()
    {
        Assert.Equal(
            ["0.6", "1", "0.6", TwoThirds, "1", "0.8", TwoThirds, "1", "0.2"],
            Values(new CumulativeDistributionProjection(PerUserByTotalDescending))
        );
    }

    [Fact]
    public void CumulativeDistributionWithoutPartitionDividesByEveryRow()
    {
        Assert.Equal(
            [
                "0.7777777777777777777777777778",
                "0.2222222222222222222222222222",
                "0.7777777777777777777777777778",
                "0.5555555555555555555555555556",
                "0.1111111111111111111111111111",
                "0.5555555555555555555555555556",
                "0.5555555555555555555555555556",
                "1",
                "0.8888888888888888888888888889",
            ],
            Values(new CumulativeDistributionProjection(ByTotal))
        );
    }

    [Fact]
    public void CumulativeDistributionOfAPartitionOfOneRowIsOne()
    {
        Assert.Equal(
            ["1"],
            Values(
                new CumulativeDistributionProjection(PerUserByTotal),
                new EqualStringRow(User, new LiteralString("c"))
            )
        );
    }

    [Theory]
    [InlineData(1, "1,1,1,1,1,1,1,1,1")]
    [InlineData(2, "1,1,1,1,1,2,2,2,2")]
    [InlineData(3, "1,1,1,2,2,2,3,3,3")]
    [InlineData(4, "1,1,1,2,2,3,3,4,4")]
    [InlineData(5, "1,1,2,2,3,3,4,4,5")]
    [InlineData(7, "1,1,2,2,3,4,5,6,7")]
    [InlineData(9, "1,2,3,4,5,6,7,8,9")]
    public void NtileFillsBucketsOfEqualSizeTheFirstOnesOfOneRowMore(
        long buckets,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            Values(new NtileProjection(new LiteralInteger(buckets), ById))
        );
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(1000)]
    public void NtileOfMoreBucketsThanRowsNumbersTheRows(long buckets)
    {
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8", "9"],
            Values(new NtileProjection(new LiteralInteger(buckets), ById))
        );
    }

    [Fact]
    public void NtileCutsEachPartitionByItself()
    {
        Assert.Equal(
            ["1", "1", "1", "1", "1", "2", "2", "1", "2"],
            Values(new NtileProjection(new LiteralInteger(2), PerUserById))
        );
    }

    [Fact]
    public void NtileNumbersTheRowsOfAPartitionSmallerThanTheBuckets()
    {
        Assert.Equal(
            ["1", "1", "2", "1", "2", "3", "3", "1", "4"],
            Values(new NtileProjection(new LiteralInteger(4), PerUserById))
        );
    }

    [Fact]
    public void NtileFollowsTheOrderOfTheWindow()
    {
        Assert.Equal(
            ["2", "2", "2", "2", "1", "1", "1", "1", "1"],
            Values(
                new NtileProjection(
                    new LiteralInteger(2),
                    new OrderedWindow([new OrderItemRow(Id, SortDirection.Desc)])
                )
            )
        );
    }

    [Theory]
    [InlineData(1, "1,1,1,1,1,1,1,1,1")]
    [InlineData(4, "1,1,1,2,2,3,3,4,4")]
    [InlineData(20, "1,2,3,4,5,6,7,8,9")]
    public void NtileReadsItsBucketsFromAParameter(long buckets, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new NtileProjection(new ParamInteger("buckets"), ById),
                parameters: Bound("buckets", buckets)
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void NtileRejectsABucketsParameterBelowOne(long buckets)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(new NtileProjection(new ParamInteger("buckets"), ById), null),
                Bound("buckets", buckets)
            )
        );
    }

    [Fact]
    public void RankingFunctionsOfOneSelectEachReadTheirOwnWindow()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("rank", new RankProjection(ByTotal)),
                    new SelectItemProjection(
                        "dense",
                        new DenseRankProjection(PerUserByTotalDescending)
                    ),
                    new SelectItemProjection("number", new RowNumberProjection(ById)),
                    new SelectItemProjection(
                        "half",
                        new NtileProjection(new LiteralInteger(2), PerUserById)
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["6", "2", "6", "3", "1", "3", "3", "9", "8"],
            ResultColumn.Texts(projection, "rank")
        );
        Assert.Equal(
            ["2", "4", "2", "1", "2", "3", "1", "1", "1"],
            ResultColumn.Texts(projection, "dense")
        );
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8", "9"],
            ResultColumn.Texts(projection, "number")
        );
        Assert.Equal(
            ["1", "1", "1", "1", "1", "2", "2", "1", "2"],
            ResultColumn.Texts(projection, "half")
        );
    }

    private static Dictionary<string, IParameter> Bound(string name, long value)
    {
        return new Dictionary<string, IParameter>
        {
            [name] = new IntegerParameter(new Long(value)),
        };
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
