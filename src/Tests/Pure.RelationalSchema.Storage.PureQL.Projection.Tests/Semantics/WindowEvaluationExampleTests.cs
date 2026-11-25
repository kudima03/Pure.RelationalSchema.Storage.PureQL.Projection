using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The worked example that closes Window evaluation in the specification: over
// five rows with the totals 10, 20, 20, 30, 40, ordered by total, the value of
// every window function is listed row by row. The two rows of 20 are peers,
// whose order is unspecified, so each column is read sorted by the total and
// then by itself: the values the example lists are the same either way.
public sealed class WindowEvaluationExampleTests
{
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("total", new LongColumnType())
    ).With(["1", "30"], ["2", "20"], ["3", "10"], ["4", "40"], ["5", "20"]);

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly OrderedWindow ByTotal = new([new OrderItemRow(Total)]);

    [Fact]
    public void RowNumberIsOneToFive()
    {
        Assert.Equal(["1", "2", "3", "4", "5"], Values(new RowNumberProjection(ByTotal)));
    }

    [Fact]
    public void RankSharesAPositionAmongPeersAndLeavesAGap()
    {
        Assert.Equal(["1", "2", "2", "4", "5"], Values(new RankProjection(ByTotal)));
    }

    [Fact]
    public void DenseRankSharesAPositionAmongPeersWithoutAGap()
    {
        Assert.Equal(["1", "2", "2", "3", "4"], Values(new DenseRankProjection(ByTotal)));
    }

    [Fact]
    public void PercentRankIsTheRankBelowOneOverTheRowsBelowOne()
    {
        Assert.Equal(
            ["0", "0.25", "0.25", "0.75", "1"],
            Values(new PercentRankProjection(ByTotal))
        );
    }

    [Fact]
    public void CumulativeDistributionIsTheShareOfRowsUpToTheRowAndItsPeers()
    {
        Assert.Equal(
            ["0.2", "0.6", "0.6", "0.8", "1"],
            Values(new CumulativeDistributionProjection(ByTotal))
        );
    }

    [Fact]
    public void NtileOfTwoBucketsGivesTheFirstBucketTheOddRow()
    {
        Assert.Equal(
            ["1", "1", "1", "2", "2"],
            Values(new NtileProjection(new LiteralInteger(2), ByTotal))
        );
    }

    [Fact]
    public void SumOverOnePrecedingRowAddsTheRowBefore()
    {
        Assert.Equal(
            ["10", "30", "40", "50", "70"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithFrame(
                        [new OrderItemRow(Total)],
                        new Frame(preceding: 1L)
                    )
                )
            )
        );
    }

    [Fact]
    public void PreviousIsTheTotalOfTheRowBeforeAndNullInTheFirstRow()
    {
        Assert.Equal(
            ["", "10", "20", "20", "30"],
            Values(new PreviousIntegerNullableProjection(Total, ByTotal))
        );
    }

    [Fact]
    public void NextWithADefaultIsTheTotalOfTheRowAfterAndTheDefaultInTheLastRow()
    {
        Assert.Equal(
            ["20", "20", "30", "40", "0"],
            Values(new NextIntegerProjection(Total, new LiteralInteger(0), ByTotal))
        );
    }

    private static IReadOnlyList<string> Values(ExprProjection expression)
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("total", Total),
                    new SelectItemProjection("v", expression),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("total"),
                    new OrderItemProjectionColumn("v"),
                ]
            )
        );

        return ResultColumn.Texts(projection, "v");
    }
}
