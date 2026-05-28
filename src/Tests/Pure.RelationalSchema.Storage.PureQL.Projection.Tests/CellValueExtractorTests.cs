using System.Reflection;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record CellValueExtractorTests
{
    private static readonly Type ExtractorType = typeof(PureQLProjection)
        .Assembly.GetType(
            "Pure.RelationalSchema.Storage.PureQL.Projection.CellValueExtractor"
        )!;

    private static T? Invoke<T>(string name, IRow row, string fieldName)
    {
        MethodInfo m = ExtractorType.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        return (T?)m.Invoke(null, [row, fieldName]);
    }

    private static IRow OneCellRow(string columnName, string cellValue)
    {
        IColumn column = new Column.Column(new String(columnName), new StringColumnType());
        return new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                [column],
                c => c,
                _ => new Cell(new String(cellValue)),
                c => new ColumnHash(c)
            )
        );
    }

    [Fact]
    public void GetTextValueReturnsCellTextWhenFieldExists()
    {
        IRow row = OneCellRow("name", "hello");
        Assert.Equal("hello", Invoke<string>("GetTextValue", row, "name"));
    }

    [Fact]
    public void GetTextValueReturnsNullWhenFieldDoesNotExist()
    {
        IRow row = OneCellRow("name", "hello");
        Assert.Null(Invoke<string>("GetTextValue", row, "missing"));
    }

    [Fact]
    public void GetDoubleValueParsesValidNumber()
    {
        IRow row = OneCellRow("n", "3.14");
        Assert.Equal(3.14, Invoke<double?>("GetDoubleValue", row, "n"));
    }

    [Fact]
    public void GetDoubleValueReturnsNullForNonNumeric()
    {
        IRow row = OneCellRow("n", "abc");
        Assert.Null(Invoke<double?>("GetDoubleValue", row, "n"));
    }

    [Fact]
    public void GetDoubleValueReturnsNullForMissingField()
    {
        IRow row = OneCellRow("n", "3.14");
        Assert.Null(Invoke<double?>("GetDoubleValue", row, "missing"));
    }

    [Fact]
    public void GetBoolValueParsesTrueAndFalse()
    {
        Assert.True(Invoke<bool?>("GetBoolValue", OneCellRow("b", "true"), "b"));
        Assert.False(Invoke<bool?>("GetBoolValue", OneCellRow("b", "false"), "b"));
    }

    [Fact]
    public void GetBoolValueReturnsNullForBadInput()
    {
        Assert.Null(Invoke<bool?>("GetBoolValue", OneCellRow("b", "yes"), "b"));
    }

    [Fact]
    public void GetDateOnlyValueParsesIsoDate()
    {
        IRow row = OneCellRow("d", "2026-05-28");
        Assert.Equal(
            new DateOnly(2026, 5, 28),
            Invoke<DateOnly?>("GetDateOnlyValue", row, "d")
        );
    }

    [Fact]
    public void GetDateOnlyValueReturnsNullForInvalid()
    {
        Assert.Null(Invoke<DateOnly?>(
            "GetDateOnlyValue",
            OneCellRow("d", "not-a-date"),
            "d"
        ));
    }

    [Fact]
    public void GetDateTimeValueParsesIsoDateTime()
    {
        IRow row = OneCellRow("dt", "2026-05-28T12:34:56");
        Assert.Equal(
            new DateTime(2026, 5, 28, 12, 34, 56),
            Invoke<DateTime?>("GetDateTimeValue", row, "dt")
        );
    }

    [Fact]
    public void GetDateTimeValueReturnsNullForInvalid()
    {
        Assert.Null(Invoke<DateTime?>(
            "GetDateTimeValue",
            OneCellRow("dt", "junk"),
            "dt"
        ));
    }

    [Fact]
    public void GetTimeOnlyValueParsesIsoTime()
    {
        Assert.Equal(
            new TimeOnly(13, 45, 0),
            Invoke<TimeOnly?>("GetTimeOnlyValue", OneCellRow("t", "13:45"), "t")
        );
    }

    [Fact]
    public void GetTimeOnlyValueReturnsNullForInvalid()
    {
        Assert.Null(
            Invoke<TimeOnly?>("GetTimeOnlyValue", OneCellRow("t", "??"), "t")
        );
    }

    [Fact]
    public void GetGuidValueParsesValidGuid()
    {
        Guid g = Guid.NewGuid();
        Assert.Equal(
            g,
            Invoke<Guid?>("GetGuidValue", OneCellRow("u", g.ToString()), "u")
        );
    }

    [Fact]
    public void GetGuidValueReturnsNullForInvalid()
    {
        Assert.Null(
            Invoke<Guid?>("GetGuidValue", OneCellRow("u", "not-a-guid"), "u")
        );
    }
}
