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

// previous and next as Window evaluation defines them: operand evaluated in
// the row offset positions before, or after, the current row of its ordered
// partition, one position without offset. Where the partition has no such
// row the result is default, evaluated in the current row and only then, or
// null without one; a null found in the other row is returned as it is.
// offset is a literal from 2 or a parameter read once, and a parameter below
// 1 is rejected. Every column is read in the order of id.
public sealed class PreviousNextTests
{
    // user a: 1, 2, 4, 6; user b: 3, 5.
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType()),
        ("note", new StringColumnType()),
        ("amount", new DoubleColumnType()),
        ("price", new DoubleColumnType()),
        ("day", new DateColumnType())
    ).With(
        ["1", "a", "10", "x", "1.5", "1.25", "2024-01-01"],
        ["2", "a", "20", "", "", "2.5", "2024-01-03"],
        ["3", "b", "30", "y", "2.5", "3.75", "2024-01-02"],
        ["4", "a", "40", "z", "4", "5", "2024-01-10"],
        ["5", "b", "50", "", "", "6.5", "2024-01-05"],
        ["6", "a", "60", "w", "0.5", "8", "2024-01-20"]
    );

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString User = new("t.orders", "user");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly FieldStringNullable Note = new("t.orders", "note");

    private static readonly FieldDecimalNullable Amount = new("t.orders", "amount");

    private static readonly FieldDecimal Price = new("t.orders", "price");

    private static readonly FieldDate Day = new("t.orders", "day");

    private static readonly OrderedWindow ById = new([new OrderItemRow(Id)]);

    private static readonly OrderedWindow PerUserById = new(
        [new OrderItemRow(Id)],
        [User]
    );

    [Fact]
    public void PreviousReadsTheRowBeforeAndIsNullInTheFirstRow()
    {
        Assert.Equal(
            ["", "10", "20", "30", "40", "50"],
            Values(new PreviousIntegerNullableProjection(Total, ById))
        );
    }

    [Fact]
    public void NextReadsTheRowAfterAndIsNullInTheLastRow()
    {
        Assert.Equal(
            ["20", "30", "40", "50", "60", ""],
            Values(new NextIntegerNullableProjection(Total, ById))
        );
    }

    [Fact]
    public void PreviousNeverLeavesThePartition()
    {
        Assert.Equal(
            ["", "10", "", "20", "30", "40"],
            Values(new PreviousIntegerNullableProjection(Total, PerUserById))
        );
    }

    [Fact]
    public void NextNeverLeavesThePartition()
    {
        Assert.Equal(
            ["20", "40", "50", "60", "", ""],
            Values(new NextIntegerNullableProjection(Total, PerUserById))
        );
    }

    [Fact]
    public void PreviousYieldsTheDefaultWhereThePartitionHasNoRowBefore()
    {
        Assert.Equal(
            ["-1", "10", "-1", "20", "30", "40"],
            Values(
                new PreviousIntegerProjection(Total, new LiteralInteger(-1), PerUserById)
            )
        );
    }

    [Fact]
    public void NextYieldsTheDefaultWhereThePartitionHasNoRowAfter()
    {
        Assert.Equal(
            ["20", "40", "50", "60", "-1", "-1"],
            Values(new NextIntegerProjection(Total, new LiteralInteger(-1), PerUserById))
        );
    }

    [Fact]
    public void PreviousWithALiteralOffsetReadsThatManyRowsBefore()
    {
        Assert.Equal(
            ["", "", "", "10", "", "20"],
            Values(
                new PreviousIntegerNullableProjection(
                    Total,
                    PerUserById,
                    offset: new LiteralInteger(2)
                )
            )
        );
    }

    [Fact]
    public void NextWithALiteralOffsetReadsThatManyRowsAfter()
    {
        Assert.Equal(
            ["40", "60", "", "", "", ""],
            Values(
                new NextIntegerNullableProjection(
                    Total,
                    PerUserById,
                    offset: new LiteralInteger(2)
                )
            )
        );
    }

    [Fact]
    public void LiteralOffsetBeyondThePartitionYieldsTheDefaultInEveryRow()
    {
        Assert.Equal(
            ["0", "0", "0", "0", "0", "0"],
            Values(
                new NextIntegerProjection(
                    Total,
                    new LiteralInteger(0),
                    PerUserById,
                    offset: new LiteralInteger(4)
                )
            )
        );
    }

    [Theory]
    [InlineData(1, "-1,10,20,30,40,50")]
    [InlineData(2, "-1,-1,10,20,30,40")]
    [InlineData(5, "-1,-1,-1,-1,-1,10")]
    [InlineData(6, "-1,-1,-1,-1,-1,-1")]
    [InlineData(long.MaxValue, "-1,-1,-1,-1,-1,-1")]
    public void PreviousReadsItsOffsetFromAParameter(long offset, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new PreviousIntegerProjection(
                    Total,
                    new LiteralInteger(-1),
                    ById,
                    offset: new ParamInteger("offset")
                ),
                parameters: Bound(offset)
            )
        );
    }

    [Theory]
    [InlineData(1, "20,30,40,50,60,-1")]
    [InlineData(3, "40,50,60,-1,-1,-1")]
    [InlineData(5, "60,-1,-1,-1,-1,-1")]
    [InlineData(6, "-1,-1,-1,-1,-1,-1")]
    [InlineData(long.MaxValue, "-1,-1,-1,-1,-1,-1")]
    public void NextReadsItsOffsetFromAParameter(long offset, string expected)
    {
        Assert.Equal(
            expected.Split(','),
            Values(
                new NextIntegerProjection(
                    Total,
                    new LiteralInteger(-1),
                    ById,
                    offset: new ParamInteger("offset")
                ),
                parameters: Bound(offset)
            )
        );
    }

    [Fact]
    public void ParameterOffsetWithoutDefaultIsNullWhereThereIsNoRow()
    {
        Assert.Equal(
            ["", "", "", "10", "", "20"],
            Values(
                new PreviousIntegerNullableProjection(
                    Total,
                    PerUserById,
                    offset: new ParamInteger("offset")
                ),
                parameters: Bound(2)
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void PreviousRejectsAnOffsetParameterBelowOne(long offset)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(
                    new PreviousIntegerNullableProjection(
                        Total,
                        ById,
                        offset: new ParamInteger("offset")
                    ),
                    null
                ),
                Bound(offset)
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void NextRejectsAnOffsetParameterBelowOne(long offset)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(
                    new NextIntegerProjection(
                        Total,
                        new LiteralInteger(0),
                        ById,
                        offset: new ParamInteger("offset")
                    ),
                    null
                ),
                Bound(offset)
            )
        );
    }

    [Fact]
    public void PreviousReturnsANullOfTheOtherRowAsItIsNotTheDefault()
    {
        Assert.Equal(
            ["none", "x", "", "y", "z", ""],
            Values(
                new PreviousStringNullableProjection(
                    Note,
                    ById,
                    @default: new LiteralString("none")
                )
            )
        );
    }

    [Fact]
    public void NextReturnsANullOfTheOtherRowAsItIsNotTheDefault()
    {
        Assert.Equal(
            ["", "2.5", "4", "", "0.5", "0"],
            Values(
                new NextDecimalNullableProjection(
                    Amount,
                    ById,
                    @default: new LiteralInteger(0)
                )
            )
        );
    }

    [Fact]
    public void NullOfTheOtherRowIsReturnedAtAnOffsetToo()
    {
        Assert.Equal(
            ["none", "none", "x", "", "y", "z"],
            Values(
                new PreviousStringNullableProjection(
                    Note,
                    ById,
                    offset: new LiteralInteger(2),
                    @default: new LiteralString("none")
                )
            )
        );
    }

    [Fact]
    public void DefaultIsEvaluatedInTheCurrentRow()
    {
        Assert.Equal(
            ["1", "10", "3", "20", "30", "40"],
            Values(new PreviousIntegerProjection(Total, Id, PerUserById))
        );
    }

    [Fact]
    public void ComputedDefaultReadsTheFieldsOfTheCurrentRow()
    {
        Assert.Equal(
            ["20", "40", "50", "60", "-45", "-54"],
            Values(
                new NextIntegerProjection(
                    Total,
                    new SubtractIntegerRow([Id, Total]),
                    PerUserById
                )
            )
        );
    }

    [Fact]
    public void PreviousDefaultIsNotEvaluatedWhereTheRowExists()
    {
        Assert.Equal(
            ["1", "1.25", "2.5", "3.75", "5", "6.5"],
            Values(new PreviousDecimalProjection(Price, OneOver(1, 1, 0), ById))
        );
    }

    [Fact]
    public void NextDefaultIsNotEvaluatedWhereTheRowExists()
    {
        Assert.Equal(
            ["2.5", "3.75", "5", "6.5", "8", "1"],
            Values(new NextDecimalProjection(Price, OneOver(6, 1, 0), ById))
        );
    }

    [Fact]
    public void DefaultThatFailsInTheRowWithoutANeighbourFailsTheQuery()
    {
        _ = Assert.Throws<DivideByZeroException>(() =>
            Values(new PreviousDecimalProjection(Price, OneOver(1, 0, 1), ById))
        );
    }

    [Fact]
    public void OperandIsARowExpressionEvaluatedInTheOtherRow()
    {
        Assert.Equal(
            ["", "11", "22", "33", "44", "55"],
            Values(
                new PreviousIntegerNullableProjection(
                    new AddIntegerRow([Total, Id]),
                    ById
                )
            )
        );
    }

    [Fact]
    public void PreviousFollowsEveryKeyOfTheOrderAndItsDirection()
    {
        Assert.Equal(
            ["20", "40", "50", "60", "", "30"],
            Values(
                new PreviousIntegerNullableProjection(
                    Total,
                    new OrderedWindow([
                        new OrderItemRow(User, SortDirection.Desc),
                        new OrderItemRow(Id, SortDirection.Desc),
                    ])
                )
            )
        );
    }

    [Fact]
    public void PreviousSortsNullKeysFirstAscending()
    {
        Assert.Equal(
            ["6", "", "1", "3", "2", "5"],
            Values(
                new PreviousIntegerNullableProjection(
                    Id,
                    new OrderedWindow([new OrderItemRow(Amount), new OrderItemRow(Id)])
                )
            )
        );
    }

    [Fact]
    public void NextSortsNullKeysLastDescending()
    {
        Assert.Equal(
            ["6", "5", "1", "3", "", "2"],
            Values(
                new NextIntegerNullableProjection(
                    Id,
                    new OrderedWindow([
                        new OrderItemRow(Amount, SortDirection.Desc),
                        new OrderItemRow(Id),
                    ])
                )
            )
        );
    }

    [Fact]
    public void PreviousOfADateIsTheDateOfTheRowBefore()
    {
        Assert.Equal(
            ["", "2024-01-01", "", "2024-01-03", "2024-01-02", "2024-01-10"],
            Values(new PreviousDateNullableProjection(Day, PerUserById))
        );
    }

    [Fact]
    public void NextOfADateWithADefaultIsNeverNull()
    {
        Assert.Equal(
            [
                "2024-01-03",
                "2024-01-10",
                "2024-01-05",
                "2024-01-20",
                "9999-12-31",
                "9999-12-31",
            ],
            Values(
                new NextDateProjection(
                    Day,
                    new LiteralDate(new DateOnly(9999, 12, 31)),
                    PerUserById
                )
            )
        );
    }

    [Fact]
    public void NextOfAStringWithADefaultIsTheUserOfTheRowAfter()
    {
        Assert.Equal(
            ["a", "b", "a", "b", "a", "-"],
            Values(new NextStringProjection(User, new LiteralString("-"), ById))
        );
    }

    [Fact]
    public void PreviousOfABooleanExpressionIsItsValueInTheRowBefore()
    {
        Assert.Equal(
            ["", "False", "False", "False", "True", "True"],
            Values(
                new PreviousBooleanNullableProjection(
                    new GreaterThanDecimalRow(Total, new LiteralInteger(35)),
                    ById
                )
            )
        );
    }

    [Fact]
    public void DifferenceFromThePreviousRowIsNullInTheFirstRowOfAPartition()
    {
        Assert.Equal(
            ["", "10", "", "20", "20", "20"],
            Values(
                new SubtractIntegerNullableProjection([
                    Total,
                    new PreviousIntegerNullableProjection(Total, PerUserById),
                ])
            )
        );
    }

    [Fact]
    public void DifferenceFromThePreviousRowWithADefaultIsNeverNull()
    {
        Assert.Equal(
            ["10", "10", "30", "20", "20", "20"],
            Values(
                new SubtractIntegerProjection([
                    Total,
                    new PreviousIntegerProjection(
                        Total,
                        new LiteralInteger(0),
                        PerUserById
                    ),
                ])
            )
        );
    }

    [Fact]
    public void PreviousAndNextOfOneSelectReadBothNeighbours()
    {
        Assert.Equal(
            ["", "30", "", "40", "", ""],
            Values(
                new SubtractIntegerNullableProjection([
                    new NextIntegerNullableProjection(Total, PerUserById),
                    new PreviousIntegerNullableProjection(Total, PerUserById),
                ])
            )
        );
    }

    [Fact]
    public void PreviousSeesOnlyTheRowsLeftByWhere()
    {
        Assert.Equal(
            ["", "10", "20", "40"],
            Values(
                new PreviousIntegerNullableProjection(Total, ById),
                new EqualStringRow(User, new LiteralString("a"))
            )
        );
    }

    // 1 / (then when id = row, otherwise else): fails where the divisor is 0.
    private static DivideDecimalRow OneOver(long row, long then, long @else)
    {
        return new DivideDecimalRow([
            new LiteralInteger(1),
            new IfIntegerRow(
                new LiteralInteger(then),
                new LiteralInteger(@else),
                new EqualDecimalRow(Id, new LiteralInteger(row))
            ),
        ]);
    }

    private static Dictionary<string, IParameter> Bound(long offset)
    {
        return new Dictionary<string, IParameter>
        {
            ["offset"] = new IntegerParameter(new Long(offset)),
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
