using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Parameters of a window: a frame count, a range distance, an offset and
// buckets given as parameters are read once for the query, and a parameter
// is also read inside the row expressions of a window. Like every parameter
// they need a value of their type: a missing one, one of another type or a
// null for a non-null parameter is rejected when the projection is built,
// as a frame count or a distance below 0 and an offset or buckets below 1
// are.
public sealed class WindowParameterTests
{
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("total", new LongColumnType())
    ).With(
        ["1", "10"],
        ["2", "20"],
        ["3", "30"],
        ["4", "40"],
        ["5", "50"],
        ["6", "60"]
    );

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly OrderedWindow ById = new([new OrderItemRow(Id)]);

    private static readonly ParamInteger Rows = new("n");

    // One window function per place a window reads a parameter.
    private static readonly Dictionary<string, ExprProjection> Functions = new()
    {
        ["preceding"] = new SumIntegerProjection(
            Total,
            over: new WindowWithFrame([new OrderItemRow(Id)], new Frame(preceding: Rows))
        ),
        ["following"] = new SumIntegerProjection(
            Total,
            over: new WindowWithFrame([new OrderItemRow(Id)], new Frame(following: Rows))
        ),
        ["integer"] = new SumIntegerProjection(
            Total,
            over: new WindowWithoutFrameWithRangeInteger(
                [new OrderItemIntegerRow(Total)],
                new RangeInteger(preceding: Rows)
            )
        ),
        ["decimal"] = new SumIntegerProjection(
            Total,
            over: new WindowWithoutFrameWithRangeDecimal(
                [new OrderItemDecimalRow(new AsDecimalIntegerDecimalRow(Total))],
                new RangeDecimal(following: new ParamDecimal("n"))
            )
        ),
        ["previous"] = new PreviousIntegerNullableProjection(Total, ById, offset: Rows),
        ["next"] = new NextIntegerNullableProjection(Total, ById, offset: Rows),
        ["ntile"] = new NtileProjection(Rows, ById),
    };

    [Fact]
    public void OneParameterIsReadByAFrameAnOffsetAndBuckets()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("frame", Functions["preceding"]),
                    new SelectItemProjection("range", Functions["integer"]),
                    new SelectItemProjection("previous", Functions["previous"]),
                    new SelectItemProjection("next", Functions["next"]),
                    new SelectItemProjection("ntile", Functions["ntile"]),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            Bound(new IntegerParameter(new Long(2)))
        );

        Assert.Equal(
            ["10", "30", "60", "90", "120", "150"],
            ResultColumn.Texts(projection, "frame")
        );
        Assert.Equal(
            ["10", "20", "30", "40", "50", "60"],
            ResultColumn.Texts(projection, "range")
        );
        Assert.Equal(
            ["", "", "10", "20", "30", "40"],
            ResultColumn.Texts(projection, "previous")
        );
        Assert.Equal(
            ["30", "40", "50", "60", "", ""],
            ResultColumn.Texts(projection, "next")
        );
        Assert.Equal(
            ["1", "1", "1", "2", "2", "2"],
            ResultColumn.Texts(projection, "ntile")
        );
    }

    [Fact]
    public void ProjectionRunTwiceReadsTheSameParameterValue()
    {
        PureQLProjection projection = new(
            [Orders],
            Query(Functions["preceding"]),
            Bound(new IntegerParameter(new Long(1)))
        );

        Assert.Equal(
            ["10", "30", "50", "70", "90", "110"],
            ResultColumn.Texts(projection, "v")
        );
        Assert.Equal(
            ["10", "30", "50", "70", "90", "110"],
            ResultColumn.Texts(projection, "v")
        );
    }

    [Fact]
    public void ParameterIsReadInsideThePredicateOfAWindowAggregate()
    {
        Assert.Equal(
            ["0", "0", "1", "2", "2", "2"],
            Values(
                new CountProjection(
                    predicate: new GreaterThanDecimalRow(Total, new ParamInteger("n")),
                    over: new WindowWithFrame(
                        [new OrderItemRow(Id)],
                        new Frame(preceding: 1L)
                    )
                ),
                new IntegerParameter(new Long(25))
            )
        );
    }

    [Fact]
    public void ParameterIsTheDefaultOfPrevious()
    {
        Assert.Equal(
            ["-5", "10", "20", "30", "40", "50"],
            Values(
                new PreviousIntegerProjection(Total, new ParamInteger("n"), ById),
                new IntegerParameter(new Long(-5))
            )
        );
    }

    [Fact]
    public void ParameterIsReadInsideAPartitionKey()
    {
        Assert.Equal(
            ["60", "60", "60", "150", "150", "150"],
            Values(
                new SumIntegerProjection(
                    Total,
                    over: new WindowWithoutFrameWithoutRange([
                        new GreaterThanDecimalRow(Total, new ParamInteger("n")),
                    ])
                ),
                new IntegerParameter(new Long(30))
            )
        );
    }

    [Theory]
    [InlineData("preceding")]
    [InlineData("following")]
    [InlineData("integer")]
    [InlineData("decimal")]
    [InlineData("previous")]
    [InlineData("next")]
    [InlineData("ntile")]
    public void WindowParameterGivenNoValueIsRejected(string function)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection([Orders], Query(Functions[function]))
        );
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(Functions[function]),
                new Dictionary<string, IParameter>
                {
                    ["m"] = new IntegerParameter(new Long(2)),
                }
            )
        );
    }

    [Theory]
    [InlineData("preceding")]
    [InlineData("following")]
    [InlineData("integer")]
    [InlineData("decimal")]
    [InlineData("previous")]
    [InlineData("next")]
    [InlineData("ntile")]
    public void WindowParameterGivenAStringIsRejected(string function)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(Functions[function]),
                Bound(new TextParameter("string", "2"))
            )
        );
    }

    [Theory]
    [InlineData("preceding")]
    [InlineData("following")]
    [InlineData("integer")]
    [InlineData("previous")]
    [InlineData("next")]
    [InlineData("ntile")]
    public void IntegerWindowParameterGivenADecimalIsRejected(string function)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(Functions[function]),
                Bound(new DecimalParameter(new Decimal(2m)))
            )
        );
    }

    [Theory]
    [InlineData("preceding", "integer")]
    [InlineData("following", "integer")]
    [InlineData("integer", "integer")]
    [InlineData("decimal", "decimal")]
    [InlineData("previous", "integer")]
    [InlineData("next", "integer")]
    [InlineData("ntile", "integer")]
    public void WindowParameterGivenATypedNullIsRejected(string function, string type)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(Functions[function]),
                Bound(new TextParameter(new TextParameterType(type, true), ""))
            )
        );
    }

    [Theory]
    [InlineData("preceding", -1)]
    [InlineData("following", -1)]
    [InlineData("integer", -1)]
    [InlineData("decimal", -1)]
    [InlineData("previous", 0)]
    [InlineData("next", 0)]
    [InlineData("ntile", 0)]
    public void WindowParameterBelowItsMinimumIsRejected(string function, long value)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                Query(Functions[function]),
                Bound(new IntegerParameter(new Long(value)))
            )
        );
    }

    [Theory]
    [InlineData("preceding", 0, "10,20,30,40,50,60")]
    [InlineData("following", 0, "10,20,30,40,50,60")]
    [InlineData("integer", 0, "10,20,30,40,50,60")]
    [InlineData("decimal", 0, "10,20,30,40,50,60")]
    [InlineData("previous", 1, ",10,20,30,40,50")]
    [InlineData("next", 1, "20,30,40,50,60,")]
    [InlineData("ntile", 1, "1,1,1,1,1,1")]
    public void WindowParameterAtItsMinimumIsAccepted(
        string function,
        long value,
        string expected
    )
    {
        Assert.Equal(
            expected.Split(','),
            Values(Functions[function], new IntegerParameter(new Long(value)))
        );
    }

    private static Dictionary<string, IParameter> Bound(IParameter value)
    {
        return new Dictionary<string, IParameter> { ["n"] = value };
    }

    private static MainPlainQueryWithoutPagination Query(ExprProjection expression)
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.orders"),
            select: [new SelectItemProjection("v", expression)],
            orderBy: [new OrderItemProjectionExpression(Id)]
        );
    }

    private static IReadOnlyList<string> Values(
        ExprProjection expression,
        IParameter value
    )
    {
        PureQLProjection projection = new([Orders], Query(expression), Bound(value));

        return ResultColumn.Texts(projection, "v");
    }
}
