namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Date and time arithmetic. dateDiffDays and the *DiffSeconds operators are
// left - right; timeAddSeconds wraps around midnight. Seconds are rounded half
// away from zero to the 100 ns tick, the precision of .NET times. A date or
// datetime outside the representable years raises and fails the query.
internal static class Temporal
{
    public static object? DateAddDays(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (date, days) => DateOnly.FromDayNumber(
                checked((int)(((DateOnly)date).DayNumber + (long)days))
            )
        );
    }

    public static object? DateDiffDays(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (left, right) =>
                (long)(((DateOnly)left).DayNumber - ((DateOnly)right).DayNumber)
        );
    }

    public static object? TimeAddSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (time, seconds) => new TimeOnly(
                PositiveRemainder(
                    ((TimeOnly)time).Ticks + Ticks(seconds),
                    TimeSpan.TicksPerDay
                )
            )
        );
    }

    public static object? TimeDiffSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (left, right) => Seconds(((TimeOnly)left).Ticks - ((TimeOnly)right).Ticks)
        );
    }

    public static object? DatetimeAddSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (instant, seconds) => ((DateTime)instant).AddTicks(Ticks(seconds))
        );
    }

    public static object? DatetimeDiffSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            node,
            scope,
            (left, right) => Seconds(((DateTime)left).Ticks - ((DateTime)right).Ticks)
        );
    }

    private static long Ticks(object seconds)
    {
        return (long)decimal.Round(
            Values.Decimal(seconds) * TimeSpan.TicksPerSecond,
            MidpointRounding.AwayFromZero
        );
    }

    private static decimal Seconds(long ticks)
    {
        return (decimal)ticks / TimeSpan.TicksPerSecond;
    }

    private static long PositiveRemainder(long value, long divisor)
    {
        long remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
