using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

// Direct unit tests for the small nullable arithmetic helpers inside
// WhereExpressionBuilder. These helpers are reached only via compiled
// expression trees in production, so exercising them directly raises
// branch coverage of null-propagation paths to 100%.
public sealed record WhereExpressionBuilderHelperTests
{
    private static readonly Type Builder = typeof(PureQLProjection)
        .Assembly.GetType(
            "Pure.RelationalSchema.Storage.PureQL.Projection.WhereExpressionBuilder"
        )!;

    private static T? Invoke<T>(string name, params object?[] args)
        where T : struct
    {
        MethodInfo m = Builder.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        return (T?)m.Invoke(null, args);
    }

    [Theory]
    [InlineData(2.0, 3.0, 5.0)]
    [InlineData(-1.5, 1.5, 0.0)]
    public void AddDoublesAddsValues(double a, double b, double expected)
    {
        Assert.Equal(expected, Invoke<double>("AddDoubles", a, b));
    }

    [Fact]
    public void AddDoublesReturnsNullIfLeftIsNull()
    {
        Assert.Null(Invoke<double>("AddDoubles", null, 1.0));
    }

    [Fact]
    public void AddDoublesReturnsNullIfRightIsNull()
    {
        Assert.Null(Invoke<double>("AddDoubles", 1.0, null));
    }

    [Theory]
    [InlineData(10.0, 3.0, 7.0)]
    [InlineData(0.0, 5.0, -5.0)]
    public void SubtractDoublesSubtractsValues(double a, double b, double expected)
    {
        Assert.Equal(expected, Invoke<double>("SubtractDoubles", a, b));
    }

    [Fact]
    public void SubtractDoublesPropagatesNull()
    {
        Assert.Null(Invoke<double>("SubtractDoubles", null, 1.0));
        Assert.Null(Invoke<double>("SubtractDoubles", 1.0, null));
    }

    [Theory]
    [InlineData(2.0, 3.0, 6.0)]
    [InlineData(0.0, 5.0, 0.0)]
    public void MultiplyDoublesMultipliesValues(double a, double b, double expected)
    {
        Assert.Equal(expected, Invoke<double>("MultiplyDoubles", a, b));
    }

    [Fact]
    public void MultiplyDoublesPropagatesNull()
    {
        Assert.Null(Invoke<double>("MultiplyDoubles", null, 1.0));
        Assert.Null(Invoke<double>("MultiplyDoubles", 1.0, null));
    }

    [Theory]
    [InlineData(10.0, 2.0, 5.0)]
    [InlineData(7.5, 2.5, 3.0)]
    public void DivideDoublesDividesValues(double a, double b, double expected)
    {
        Assert.Equal(expected, Invoke<double>("DivideDoubles", a, b));
    }

    [Fact]
    public void DivideDoublesByZeroReturnsNull()
    {
        Assert.Null(Invoke<double>("DivideDoubles", 1.0, 0.0));
    }

    [Fact]
    public void DivideDoublesPropagatesNull()
    {
        Assert.Null(Invoke<double>("DivideDoubles", null, 1.0));
        Assert.Null(Invoke<double>("DivideDoubles", 1.0, null));
    }

    [Fact]
    public void AddDaysAdvancesDateByGivenDays()
    {
        DateOnly? result = Invoke<DateOnly>(
            "AddDays",
            new DateOnly(2026, 1, 1),
            5.0
        );
        Assert.Equal(new DateOnly(2026, 1, 6), result);
    }

    [Fact]
    public void AddDaysWithNegativeNumberMovesBackwards()
    {
        DateOnly? result = Invoke<DateOnly>(
            "AddDays",
            new DateOnly(2026, 1, 10),
            -3.0
        );
        Assert.Equal(new DateOnly(2026, 1, 7), result);
    }

    [Fact]
    public void AddDaysPropagatesNull()
    {
        Assert.Null(Invoke<DateOnly>("AddDays", null, 1.0));
        Assert.Null(Invoke<DateOnly>("AddDays", new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void DiffDaysReturnsPositiveWhenLeftIsLater()
    {
        Assert.Equal(
            10.0,
            Invoke<double>(
                "DiffDays",
                new DateOnly(2026, 1, 15),
                new DateOnly(2026, 1, 5)
            )
        );
    }

    [Fact]
    public void DiffDaysReturnsNegativeWhenLeftIsEarlier()
    {
        Assert.Equal(
            -10.0,
            Invoke<double>(
                "DiffDays",
                new DateOnly(2026, 1, 5),
                new DateOnly(2026, 1, 15)
            )
        );
    }

    [Fact]
    public void DiffDaysReturnsZeroForSameDate()
    {
        Assert.Equal(
            0.0,
            Invoke<double>(
                "DiffDays",
                new DateOnly(2026, 3, 7),
                new DateOnly(2026, 3, 7)
            )
        );
    }

    [Fact]
    public void DiffDaysPropagatesNull()
    {
        Assert.Null(Invoke<double>("DiffDays", null, new DateOnly(2026, 1, 1)));
        Assert.Null(Invoke<double>("DiffDays", new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void AddSecondsToTimeAdvancesByGivenSeconds()
    {
        TimeOnly? result = Invoke<TimeOnly>(
            "AddSecondsToTime",
            new TimeOnly(10, 0, 0),
            90.0
        );
        Assert.Equal(new TimeOnly(10, 1, 30), result);
    }

    [Fact]
    public void AddSecondsToTimePropagatesNull()
    {
        Assert.Null(Invoke<TimeOnly>("AddSecondsToTime", null, 1.0));
        Assert.Null(Invoke<TimeOnly>(
            "AddSecondsToTime",
            new TimeOnly(0, 0, 0),
            null
        ));
    }

    [Fact]
    public void DiffSecondsTimeReturnsPositiveWhenLeftIsLater()
    {
        Assert.Equal(
            90.0,
            Invoke<double>(
                "DiffSecondsTime",
                new TimeOnly(10, 1, 30),
                new TimeOnly(10, 0, 0)
            )
        );
    }

    [Fact]
    public void DiffSecondsTimePropagatesNull()
    {
        Assert.Null(Invoke<double>(
            "DiffSecondsTime",
            null,
            new TimeOnly(0, 0, 0)
        ));
        Assert.Null(Invoke<double>(
            "DiffSecondsTime",
            new TimeOnly(0, 0, 0),
            null
        ));
    }

    [Fact]
    public void AddSecondsToDateTimeAdvancesByGivenSeconds()
    {
        DateTime? result = Invoke<DateTime>(
            "AddSecondsToDateTime",
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            3600.0
        );
        Assert.Equal(
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
            result
        );
    }

    [Fact]
    public void AddSecondsToDateTimePropagatesNull()
    {
        Assert.Null(Invoke<DateTime>("AddSecondsToDateTime", null, 1.0));
        Assert.Null(Invoke<DateTime>(
            "AddSecondsToDateTime",
            DateTime.UnixEpoch,
            null
        ));
    }

    [Fact]
    public void DiffSecondsDateTimeReturnsLeftMinusRight()
    {
        Assert.Equal(
            3600.0,
            Invoke<double>(
                "DiffSecondsDateTime",
                new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            )
        );
    }

    [Fact]
    public void DiffSecondsDateTimePropagatesNull()
    {
        Assert.Null(Invoke<double>(
            "DiffSecondsDateTime",
            null,
            DateTime.UnixEpoch
        ));
        Assert.Null(Invoke<double>(
            "DiffSecondsDateTime",
            DateTime.UnixEpoch,
            null
        ));
    }
}
