using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.SelectItems;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// Date math that is not well formed for its type (a unit the type does not
// take, units out of order or repeated, parts that mix signs, malformed
// text), or whose value is out of range, fails when the projection is built,
// with ArgumentException, as other text that is not a value of its type
// does (issue #251).
public sealed class DateMathParameterTests
{
    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    [Theory]
    [InlineData("date", "now-1h")]
    [InlineData("date", "now-1m")]
    [InlineData("date", "now-1s")]
    [InlineData("time", "now-1y")]
    [InlineData("time", "now-1M")]
    [InlineData("time", "now-1d")]
    [InlineData("datetime", "now-1w")]
    [InlineData("date", "now-1d-1M")]
    [InlineData("datetime", "now-1s-1h")]
    [InlineData("date", "now-1d-2d")]
    [InlineData("date", "now-1M+60d")]
    [InlineData("datetime", "now+1h-30m")]
    [InlineData("date", "now-")]
    [InlineData("date", "now-d")]
    [InlineData("date", "now10d")]
    [InlineData("date", "now-1D")]
    [InlineData("date", "now--1d")]
    [InlineData("date", "now-1d ")]
    [InlineData("date", "now/d")]
    [InlineData("date", "nowx")]
    [InlineData("date", "now-1d\n")]
    public void MalformedDateMathIsRejectedWhenTheProjectionIsBuilt(
        string type,
        string dateMath
    )
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            Selecting(new TextParameter(type, dateMath), type)
        );

        Assert.Equal(
            $"Parameter 'v' is bound to '{dateMath}', which is not date math of a "
                + $"{type}, such as now-10d.",
            error.Message
        );
        _ = Assert.IsType<FormatException>(error.InnerException);
    }

    [Theory]
    [InlineData("date", "now-2147483648d")]
    [InlineData("date", "now+8000y")]
    [InlineData("date", "now-2147483647y")]
    [InlineData("date", "now-3000000d")]
    [InlineData("datetime", "now+2147483647d")]
    public void DateMathOutOfRangeIsRejectedWhenTheProjectionIsBuilt(
        string type,
        string dateMath
    )
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            Selecting(new TextParameter(type, dateMath), type)
        );

        Assert.Equal(
            $"Parameter 'v' is bound to '{dateMath}', which is out of the range of a "
                + $"{type}.",
            error.Message
        );
    }


    private static PureQLProjection Selecting(IParameter value, string type)
    {
        object column = type switch
        {
            "date" => new SelectItemProjectionDate(
                "v",
                new DateProjection(new ParamDate("v"))
            ),
            "time" => new SelectItemProjectionTime(
                "v",
                new TimeProjection(new ParamTime("v"))
            ),
            _ => new SelectItemProjectionDatetime(
                "v",
                new DatetimeProjection(new ParamDatetime("v"))
            ),
        };

        return new PureQLProjection(
            [One],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.one")),
                    [
                        new SelectItemProjection(
                            column switch
                            {
                                SelectItemProjectionDate item =>
                                    new SelectItemProjectionNonNullable(item),
                                SelectItemProjectionTime item =>
                                    new SelectItemProjectionNonNullable(item),
                                _ => new SelectItemProjectionNonNullable(
                                    (SelectItemProjectionDatetime)column
                                ),
                            }
                        ),
                    ]
                )
            ),
            new Dictionary<string, IParameter> { ["v"] = value },
            FixedClock.At("2026-10-29T08:30:00Z")
        );
    }
}
