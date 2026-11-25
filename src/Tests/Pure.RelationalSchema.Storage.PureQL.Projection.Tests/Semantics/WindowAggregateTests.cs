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
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Aggregates over a partition: with over naming only partitionBy, an
// aggregate of a plain query runs, for each row, over the rows equal to it on
// every key, compared with equal (null keys are one partition, 1 and 1.0 one
// key), and is evaluated as under Aggregate evaluation: its predicate filters
// those rows, first, last and concatAll sort them by their own orderBy, sums
// and means are exact, and a partition always holds the current row. Windows
// see the rows left by where and never sort the result. Every column is read
// in the order of id.
public sealed class WindowAggregateTests
{
    // user a: 1, 2, 7; user b: 3, 5; no user: 4, 6.
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType()),
        ("amount", new DoubleColumnType()),
        ("status", new StringColumnType()),
        ("rate", new DoubleColumnType()),
        ("placed", new DateColumnType())
    ).With(
        ["1", "a", "10", "1.5", "paid", "1", "2024-01-05"],
        ["2", "a", "20", "", "new", "1.0", "2024-01-01"],
        ["3", "b", "30", "2.5", "paid", "2", "2024-01-03"],
        ["4", "", "40", "4", "paid", "1.00", "2024-01-02"],
        ["5", "b", "50", "", "new", "2.0", "2024-01-09"],
        ["6", "", "60", "0.5", "new", "3", "2024-01-04"],
        ["7", "a", "20", "3", "paid", "1", "2024-01-07"]
    );

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldStringNullable User = new("t.orders", "user");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly FieldDecimalNullable Amount = new("t.orders", "amount");

    private static readonly FieldString Status = new("t.orders", "status");

    private static readonly FieldDecimal Rate = new("t.orders", "rate");

    private static readonly FieldDate Placed = new("t.orders", "placed");

    private static readonly WindowWithoutFrameWithoutRange PerUser = new([User]);

    private static readonly EqualStringRow Paid = new(Status, new LiteralString("paid"));

    private static readonly EqualStringRow New = new(Status, new LiteralString("new"));

    private static readonly GreaterThanDecimalRow Large = new(
        Total,
        new LiteralInteger(45)
    );

    [Fact]
    public void SumRunsOverTheRowsOfThePartitionAndNullKeysAreOnePartition()
    {
        Assert.Equal(
            ["50", "50", "80", "100", "80", "100", "50"],
            Values(new SumIntegerProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void CountIsTheNumberOfRowsOfThePartition()
    {
        Assert.Equal(
            ["3", "3", "2", "2", "2", "2", "3"],
            Values(new CountProjection(over: PerUser))
        );
    }

    [Fact]
    public void DecimalKeysOfDifferentScaleAreTheSamePartition()
    {
        Assert.Equal(
            ["4", "4", "2", "4", "2", "1", "4"],
            Values(
                new CountProjection(over: new WindowWithoutFrameWithoutRange([Rate]))
            )
        );
    }

    [Fact]
    public void PartitionHoldsTheRowsEqualOnEveryKey()
    {
        Assert.Equal(
            ["30", "20", "30", "40", "50", "60", "30"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithoutFrameWithoutRange([User, Status])
                )
            )
        );
    }

    [Fact]
    public void PartitionKeyIsAnyRowExpression()
    {
        Assert.Equal(
            ["50", "50", "180", "180", "180", "180", "50"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithoutFrameWithoutRange([
                        new GreaterThanDecimalRow(Total, new LiteralInteger(25)),
                    ])
                )
            )
        );
    }

    [Fact]
    public void PartitionByAKeyThatIdentifiesTheRowHoldsOnlyTheCurrentRow()
    {
        Assert.Equal(
            ["10", "20", "30", "40", "50", "60", "20"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithoutFrameWithoutRange([Id])
                )
            )
        );
    }

    [Fact]
    public void CountWithAPredicateCountsTheRowsOfThePartitionThatSatisfyIt()
    {
        Assert.Equal(
            ["2", "2", "1", "1", "1", "1", "2"],
            Values(new CountProjection(predicate: Paid, over: PerUser))
        );
    }

    [Fact]
    public void CountWithAPredicateNoRowOfThePartitionSatisfiesIsZero()
    {
        Assert.Equal(
            ["0", "0", "1", "1", "1", "1", "0"],
            Values(new CountProjection(predicate: Large, over: PerUser))
        );
    }

    [Fact]
    public void CountDistinctCountsTheDifferentNonNullValuesOfThePartition()
    {
        Assert.Equal(
            ["2", "2", "1", "2", "1", "2", "2"],
            Values(new CountDistinctProjection(Amount, over: PerUser))
        );
    }

    [Fact]
    public void CountDistinctComparesDecimalsOfDifferentScaleAsEqual()
    {
        Assert.Equal(
            ["1", "1", "1", "2", "1", "2", "1"],
            Values(new CountDistinctProjection(Rate, over: PerUser))
        );
    }

    [Fact]
    public void CountDistinctWithAPredicateCountsTheValuesOfTheRowsThatSatisfyIt()
    {
        Assert.Equal(
            ["2", "2", "1", "1", "1", "1", "2"],
            Values(new CountDistinctProjection(Total, predicate: Paid, over: PerUser))
        );
    }

    [Fact]
    public void SumOfANullableSelectorSkipsNulls()
    {
        Assert.Equal(
            ["4.5", "4.5", "2.5", "4.5", "2.5", "4.5", "4.5"],
            Values(new SumDecimalProjection(Amount, over: PerUser))
        );
    }

    [Fact]
    public void SumOverNoValuesOfThePartitionIsZero()
    {
        Assert.Equal(
            ["0", "0", "0", "0.5", "0", "0.5", "0"],
            Values(new SumDecimalProjection(Amount, predicate: New, over: PerUser))
        );
    }

    [Fact]
    public void AverageOfANonNullSelectorIsDividedAsDivideDoes()
    {
        Assert.Equal(
            [
                "16.66666666666666666666666667",
                "16.66666666666666666666666667",
                "40",
                "50",
                "40",
                "50",
                "16.66666666666666666666666667",
            ],
            Values(new AverageDecimalProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void AverageOfANullableSelectorIsTheMeanOfTheNonNullValues()
    {
        Assert.Equal(
            ["2.25", "2.25", "2.5", "2.25", "2.5", "2.25", "2.25"],
            Values(new AverageDecimalNullableProjection(Amount, over: PerUser))
        );
    }

    [Fact]
    public void AverageOverNoValuesOfThePartitionIsNull()
    {
        Assert.Equal(
            ["", "", "", "0.5", "", "0.5", ""],
            Values(
                new AverageDecimalNullableProjection(
                    Amount,
                    predicate: New,
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void MinIsTheLeastValueOfThePartition()
    {
        Assert.Equal(
            ["10", "10", "30", "40", "30", "40", "10"],
            Values(new MinIntegerProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void MinWithAPredicateIsNullWhereNoRowOfThePartitionSatisfiesIt()
    {
        Assert.Equal(
            ["", "", "50", "60", "50", "60", ""],
            Values(new MinIntegerNullableProjection(Total, predicate: Large, over: PerUser))
        );
    }

    [Fact]
    public void MaxOfANullableSelectorIsTheGreatestNonNullValueOfThePartition()
    {
        Assert.Equal(
            ["3", "3", "2.5", "4", "2.5", "4", "3"],
            Values(new MaxDecimalNullableProjection(Amount, over: PerUser))
        );
    }

    [Fact]
    public void MaxOfADateIsTheLatestDateOfThePartition()
    {
        Assert.Equal(
            [
                "2024-01-07",
                "2024-01-07",
                "2024-01-09",
                "2024-01-04",
                "2024-01-09",
                "2024-01-04",
                "2024-01-07",
            ],
            Values(new MaxDateProjection(Placed, over: PerUser))
        );
    }

    [Fact]
    public void AnyIsTrueWhenSomeRowOfThePartitionSatisfiesThePredicate()
    {
        Assert.Equal(
            ["False", "False", "True", "True", "True", "True", "False"],
            Values(new AnyProjection(Large, over: PerUser))
        );
    }

    [Fact]
    public void AllIsTrueWhenEveryRowOfThePartitionSatisfiesThePredicate()
    {
        Assert.Equal(
            ["True", "True", "False", "False", "False", "False", "True"],
            Values(
                new AllProjection(
                    new LessThanDecimalRow(Total, new LiteralInteger(45)),
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void FirstIsTheSelectorInTheFirstRowOfItsOwnOrder()
    {
        Assert.Equal(
            ["2", "2", "3", "4", "3", "4", "2"],
            Values(new FirstIntegerProjection(Id, [new OrderItemRow(Placed)], over: PerUser))
        );
    }

    [Fact]
    public void LastIsTheSelectorInTheLastRowOfItsOwnOrder()
    {
        Assert.Equal(
            ["7", "7", "5", "6", "5", "6", "7"],
            Values(new LastIntegerProjection(Id, [new OrderItemRow(Placed)], over: PerUser))
        );
    }

    [Fact]
    public void FirstFollowsEveryKeyOfItsOrderAndItsDirection()
    {
        Assert.Equal(
            ["2", "2", "5", "6", "5", "6", "2"],
            Values(
                new FirstIntegerProjection(
                    Id,
                    [new OrderItemRow(Total, SortDirection.Desc), new OrderItemRow(Id)],
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void FirstReturnsTheValueOfItsRowEvenWhenItIsNull()
    {
        Assert.Equal(
            ["", "", "2.5", "4", "2.5", "4", ""],
            Values(
                new FirstDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Placed)],
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void LastReturnsTheValueOfItsRowEvenWhenItIsNull()
    {
        Assert.Equal(
            ["3", "3", "", "0.5", "", "0.5", "3"],
            Values(
                new LastDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Placed)],
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void FirstWithAPredicateSortsOnlyTheRowsThatSatisfyIt()
    {
        Assert.Equal(
            ["1.5", "1.5", "2.5", "4", "2.5", "4", "1.5"],
            Values(
                new FirstDecimalNullableProjection(
                    Amount,
                    [new OrderItemRow(Placed)],
                    predicate: Paid,
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void ConcatAllJoinsTheValuesOfThePartitionInItsOwnOrder()
    {
        Assert.Equal(
            [
                "paid,new,paid",
                "paid,new,paid",
                "new,paid",
                "new,paid",
                "new,paid",
                "new,paid",
                "paid,new,paid",
            ],
            Values(
                new ConcatAllProjection(
                    new AsNullableStringNullableRow(Status),
                    new LiteralString(","),
                    [new OrderItemRow(Id, SortDirection.Desc)],
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void ConcatAllDistinctKeepsEachValueAtItsFirstPosition()
    {
        Assert.Equal(
            [
                "paid,new",
                "paid,new",
                "paid,new",
                "paid,new",
                "paid,new",
                "paid,new",
                "paid,new",
            ],
            Values(
                new ConcatAllProjection(
                    new AsNullableStringNullableRow(Status),
                    new LiteralString(","),
                    [new OrderItemRow(Id)],
                    over: PerUser,
                    distinct: true
                )
            )
        );
    }

    [Fact]
    public void ConcatAllSkipsNullsAndReadsItsSeparatorFromAParameter()
    {
        Assert.Equal(
            ["a + b + a", "a + b", "a + b + a", "a + b + a", "a + b", "a + b", "a + b + a"],
            Values(
                new ConcatAllProjection(
                    User,
                    new ParamString("separator"),
                    [new OrderItemRow(Id)],
                    over: new WindowWithoutFrameWithoutRange([Status])
                ),
                parameters: new Dictionary<string, IParameter>
                {
                    ["separator"] = new StringParameter(new String(" + ")),
                }
            )
        );
    }

    [Fact]
    public void ConcatAllOverNoValuesOfThePartitionIsTheEmptyString()
    {
        Assert.Equal(
            ["", "", "b", "", "b", "", ""],
            Values(
                new ConcatAllProjection(
                    User,
                    new LiteralString(","),
                    [new OrderItemRow(Id)],
                    predicate: Large,
                    over: PerUser
                )
            )
        );
    }

    [Fact]
    public void MedianIsTheMiddleOfTheValuesOfThePartition()
    {
        Assert.Equal(
            ["20", "20", "40", "50", "40", "50", "20"],
            Values(new MedianDecimalProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void MedianOfANullableSelectorSkipsNulls()
    {
        Assert.Equal(
            ["2.25", "2.25", "2.5", "2.25", "2.5", "2.25", "2.25"],
            Values(new MedianDecimalNullableProjection(Amount, over: PerUser))
        );
    }

    [Fact]
    public void ModeIsTheMostFrequentValueOfThePartitionAndTheLeastAmongSeveral()
    {
        Assert.Equal(
            ["20", "20", "30", "40", "30", "40", "20"],
            Values(new ModeIntegerProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void VariancePopulationIsComputedExactlyOverThePartition()
    {
        Assert.Equal(
            [
                "22.22222222222222222222222222",
                "22.22222222222222222222222222",
                "100",
                "100",
                "100",
                "100",
                "22.22222222222222222222222222",
            ],
            Values(new VariancePopulationDecimalProjection(Total, over: PerUser))
        );
    }

    [Fact]
    public void VarianceSampleOfAPartitionOfOneRowIsNull()
    {
        Assert.Equal(
            ["50", "", "", "", "", "", "50"],
            Values(
                new VarianceSampleProjection(
                    Total,
                    over: new WindowWithoutFrameWithoutRange([User, Status])
                )
            )
        );
    }

    [Fact]
    public void WindowSeesOnlyTheRowsLeftByWhere()
    {
        Assert.Equal(
            ["30", "30", "40", "30"],
            Values(new SumIntegerProjection(Total, over: PerUser), where: Paid)
        );
    }

    [Fact]
    public void AggregateWithoutOverRunsOverAllRowsNextToOneOverAPartition()
    {
        Assert.Equal(
            ["180", "180", "150", "130", "150", "130", "180"],
            Values(
                new SubtractIntegerProjection([
                    new SumIntegerProjection(Total),
                    new SumIntegerProjection(Total, over: PerUser),
                ])
            )
        );
    }

    [Fact]
    public void ShareOfThePartitionIsARowValueDividedByAWindowAggregate()
    {
        Assert.Equal(
            ["0.2", "0.4", "0.375", "0.4", "0.625", "0.6", "0.4"],
            Values(
                new DivideDecimalProjection([
                    Total,
                    new SumIntegerProjection(Total, over: PerUser),
                ])
            )
        );
    }

    [Fact]
    public void WindowNeverSortsTheResult()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "v",
                        new SumIntegerProjection(Total, over: PerUser)
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id, SortDirection.Desc)]
            )
        );

        Assert.Equal(
            ["7", "6", "5", "4", "3", "2", "1"],
            ResultColumn.Texts(projection, "id")
        );
        Assert.Equal(
            ["50", "100", "80", "100", "80", "50", "50"],
            ResultColumn.Texts(projection, "v")
        );
    }

    [Fact]
    public void IntegerSumOfAPartitionIsExactWhateverItsPartialSums()
    {
        IStoredSchemaDataSet numbers = new InlineTable(
            "t",
            "numbers",
            ("id", new LongColumnType()),
            ("n", new LongColumnType())
        ).With(
            ["1", "9223372036854775807"],
            ["2", "1"],
            ["3", "-1"],
            ["4", "9223372036854775807"],
            ["5", "-1"],
            ["6", "1"]
        );
        PureQLProjection projection = new(
            [numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new SumIntegerProjection(
                            new FieldInteger("t.numbers", "n"),
                            over: new WindowWithoutFrameWithoutRange([
                                new GreaterThanDecimalRow(
                                    new FieldInteger("t.numbers", "id"),
                                    new LiteralInteger(3)
                                ),
                            ])
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            Enumerable.Repeat("9223372036854775807", 6),
            ResultColumn.Texts(projection, "v")
        );
    }

    [Fact]
    public void IntegerSumOfAPartitionBeyondSixtyFourBitsIsAnError()
    {
        IStoredSchemaDataSet numbers = new InlineTable(
            "t",
            "numbers",
            ("id", new LongColumnType()),
            ("n", new LongColumnType())
        ).With(["1", "9223372036854775807"], ["2", "1"]);
        PureQLProjection projection = new(
            [numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new SumIntegerProjection(
                            new FieldInteger("t.numbers", "n"),
                            over: new WindowWithoutFrameWithoutRange([
                                new LiteralInteger(1),
                            ])
                        )
                    ),
                ]
            )
        );

        _ = Assert.Throws<OverflowException>(() =>
            ResultColumn.Texts(projection, "v")
        );
    }

    [Fact]
    public void DecimalAverageOfAPartitionIsExactAlthoughItsSumIsOutOfRange()
    {
        IStoredSchemaDataSet numbers = new InlineTable(
            "t",
            "numbers",
            ("g", new StringColumnType()),
            ("d", new DoubleColumnType())
        ).With(
            ["x", "9000000000000000000000000000"],
            ["x", "9000000000000000000000000000"]
        );
        PureQLProjection projection = new(
            [numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new AverageDecimalProjection(
                            new FieldDecimal("t.numbers", "d"),
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldString("t.numbers", "g"),
                            ])
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            ["9000000000000000000000000000", "9000000000000000000000000000"],
            ResultColumn.Texts(projection, "v")
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                where: where,
                select: [new SelectItemProjection("v", expression)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            parameters ?? []
        );

        return ResultColumn.Texts(projection, "v");
    }
}
