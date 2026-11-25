using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Calendar and clock functions of the specification's "Dates and times" and
// "Time zones": the parts .NET has no member for, the start of the period
// that contains a value, whole months between two values, and the local date
// and time of an instant in a time zone and back. A datetime is an instant:
// it is read in UTC, or in the zone a function is given, and every datetime
// returned is in UTC. Arguments are never null: the caller lifts.
public static class PureQLTime
{
    // The months of the years 0001-9999, counted from year 0.
    private const long FirstMonth = 12;
    private const long LastMonth = (9999 * 12) + 11;

    // The quarter of the year: January to March is 1.
    public static long Quarter(DateOnly operand)
    {
        return ((operand.Month - 1) / 3) + 1;
    }

    // The ISO 8601 day of the week: Monday is 1, Sunday is 7.
    public static long DayOfWeek(DateOnly operand)
    {
        return (((int)operand.DayOfWeek + 6) % 7) + 1;
    }

    // The ISO 8601 week: weeks start on Monday, and week 1 is the one with the
    // year's first Thursday.
    public static long Week(DateOnly operand)
    {
        return ISOWeek.GetWeekOfYear(operand.ToDateTime(TimeOnly.MinValue));
    }

    // The ISO 8601 week-numbering year that the week belongs to.
    public static long WeekYear(DateOnly operand)
    {
        return ISOWeek.GetYear(operand.ToDateTime(TimeOnly.MinValue));
    }

    // The fraction of the second in whole microseconds.
    public static long Microsecond(TimeOnly operand)
    {
        return operand.Ticks % TimeSpan.TicksPerSecond / TimeSpan.TicksPerMicrosecond;
    }

    // January 1 of the year.
    public static DateOnly StartOfYear(DateOnly operand)
    {
        return new DateOnly(operand.Year, 1, 1);
    }

    // The first day of January, April, July or October.
    public static DateOnly StartOfQuarter(DateOnly operand)
    {
        return new DateOnly(operand.Year, operand.Month - ((operand.Month - 1) % 3), 1);
    }

    // The first day of the month.
    public static DateOnly StartOfMonth(DateOnly operand)
    {
        return new DateOnly(operand.Year, operand.Month, 1);
    }

    // The Monday on or before the day. It never leaves the range of a date:
    // 0001-01-01 is a Monday.
    public static DateOnly StartOfWeek(DateOnly operand)
    {
        return operand.AddDays(1 - (int)DayOfWeek(operand));
    }

    // Minutes, seconds and the fraction set to zero.
    public static TimeOnly StartOfHour(TimeOnly operand)
    {
        return new TimeOnly(operand.Ticks - (operand.Ticks % TimeSpan.TicksPerHour));
    }

    // Seconds and the fraction set to zero.
    public static TimeOnly StartOfMinute(TimeOnly operand)
    {
        return new TimeOnly(operand.Ticks - (operand.Ticks % TimeSpan.TicksPerMinute));
    }

    // The fraction set to zero.
    public static TimeOnly StartOfSecond(TimeOnly operand)
    {
        return new TimeOnly(operand.Ticks - (operand.Ticks % TimeSpan.TicksPerSecond));
    }

    // Midnight of January 1 of the year, in UTC.
    public static DateTime StartOfYear(DateTime operand)
    {
        return Midnight(StartOfYear(DateOnly.FromDateTime(operand)));
    }

    // Midnight of the first day of the quarter, in UTC.
    public static DateTime StartOfQuarter(DateTime operand)
    {
        return Midnight(StartOfQuarter(DateOnly.FromDateTime(operand)));
    }

    // Midnight of the first day of the month, in UTC.
    public static DateTime StartOfMonth(DateTime operand)
    {
        return Midnight(StartOfMonth(DateOnly.FromDateTime(operand)));
    }

    // Midnight of the Monday on or before the day, in UTC.
    public static DateTime StartOfWeek(DateTime operand)
    {
        return Midnight(StartOfWeek(DateOnly.FromDateTime(operand)));
    }

    // Midnight of the day, in UTC.
    public static DateTime StartOfDay(DateTime operand)
    {
        return Truncated(operand, TimeSpan.TicksPerDay);
    }

    // Minutes, seconds and the fraction set to zero.
    public static DateTime StartOfHour(DateTime operand)
    {
        return Truncated(operand, TimeSpan.TicksPerHour);
    }

    // Seconds and the fraction set to zero.
    public static DateTime StartOfMinute(DateTime operand)
    {
        return Truncated(operand, TimeSpan.TicksPerMinute);
    }

    // The fraction set to zero. A second starts at the same instant in every
    // zone, so there is no form with a zone.
    public static DateTime StartOfSecond(DateTime operand)
    {
        return Truncated(operand, TimeSpan.TicksPerSecond);
    }

    // The first instant of the local year that contains the operand: the
    // earliest instant whose local date falls in the same year of the zone.
    public static DateTime StartOfYear(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfYear(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local quarter that contains the operand.
    public static DateTime StartOfQuarter(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfQuarter(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local month that contains the operand.
    public static DateTime StartOfMonth(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfMonth(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local week, from Monday, that contains the
    // operand.
    public static DateTime StartOfWeek(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfWeek(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local day that contains the operand. Where the
    // clocks are set forward at midnight, it is the first time that exists.
    public static DateTime StartOfDay(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfDay(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local hour that contains the operand. Where the
    // clocks are set back, both passes of an hour are the same local hour.
    public static DateTime StartOfHour(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfHour(ZoneClock.Local(operand, clock)), clock);
    }

    // The first instant of the local minute that contains the operand.
    public static DateTime StartOfMinute(DateTime operand, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return ZoneClock.Instant(StartOfMinute(ZoneClock.Local(operand, clock)), clock);
    }

    // The local day of an instant in a zone: the instant shifted by the offset
    // from UTC the zone has at that instant.
    public static DateOnly LocalDate(DateTime operand, string zone)
    {
        return DateOnly.FromDateTime(ZoneClock.Local(operand, ZoneClock.Of(zone)));
    }

    // The local time of day of an instant in a zone.
    public static TimeOnly LocalTime(DateTime operand, string zone)
    {
        return TimeOnly.FromDateTime(ZoneClock.Local(operand, ZoneClock.Of(zone)));
    }

    // The instant at which the clocks of a zone read a date and a time: the
    // first instant at which the local date and time are the given ones or
    // later. A time that occurs twice is its first occurrence, a time that
    // never occurs is the instant the clocks jump over it, and a local day the
    // zone skipped altogether gives the first instant after it.
    public static DateTime DatetimeFromParts(DateOnly date, TimeOnly time, string zone)
    {
        return ZoneClock.Instant(date.ToDateTime(time), ZoneClock.Of(zone));
    }

    // A time of day from its parts: hour 0 to 23, minute 0 to 59 and second
    // from 0 up to but excluding 60, also after it is rounded to microseconds,
    // half away from zero. A part outside its range is an execution error:
    // nothing is clamped and nothing rolls over.
    public static TimeOnly TimeFromParts(long hour, long minute, decimal second)
    {
        if (hour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hour),
                $"The hour {hour} is outside 0 to 23."
            );
        }

        if (minute is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minute),
                $"The minute {minute} is outside 0 to 59."
            );
        }

        decimal rounded = decimal.Round(second, 6, MidpointRounding.AwayFromZero);

        return second >= 0 && rounded < 60
            ? new TimeOnly(
                (hour * TimeSpan.TicksPerHour)
                    + (minute * TimeSpan.TicksPerMinute)
                    + (long)(rounded * TimeSpan.TicksPerSecond)
            )
            : throw new ArgumentOutOfRangeException(
                nameof(second),
                $"The second {second} is outside 0 up to but excluding 60."
            );
    }

    // The whole months from right to left, the inverse of adding months: when
    // left is not earlier, the largest number of months that, added to right
    // with the day clamped to the end of the month, give a date not later than
    // left; otherwise the negative of the months from left to right.
    public static long DiffMonths(DateOnly left, DateOnly right)
    {
        return DiffMonths(
            left.ToDateTime(TimeOnly.MinValue),
            right.ToDateTime(TimeOnly.MinValue)
        );
    }

    // The whole months from right to left in UTC; the time of day counts.
    public static long DiffMonths(DateTime left, DateTime right)
    {
        return left.Ticks >= right.Ticks ? Months(right, left) : -Months(left, right);
    }

    // The whole months from right to left as months are added in the local
    // calendar of a zone; the local time of day counts.
    public static long DiffMonths(DateTime left, DateTime right, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);

        return left.Ticks >= right.Ticks
            ? Months(right, left, clock)
            : -Months(left, right, clock);
    }

    // An instant moved by whole months in the local calendar of a zone: the
    // local date moves, clamped to the end of the month, the local time of day
    // is kept, and the result is the instant of that local date and time.
    public static DateTime AddMonths(DateTime operand, long months, string zone)
    {
        TimeZoneInfo clock = ZoneClock.Of(zone);
        DateTime local = ZoneClock.Local(operand, clock);

        return
            months is >= -LastMonth and <= LastMonth
            && Month(local) + months is >= FirstMonth and <= LastMonth
            ? ZoneClock.Instant(local.AddMonths((int)months), clock)
            : throw new ArgumentOutOfRangeException(
                nameof(months),
                $"{months} months from the local date are outside the years 0001-9999."
            );
    }

    // Whether a text names a time zone the interpreter accepts: a name of the
    // specification's list, or UTC.
    public static bool IsZone(string name)
    {
        return ZoneClock.Names(name);
    }

    // The largest number of months that, added to `from`, give a date and time
    // not later than `to`, which is not earlier than `from`. The months
    // between the two calendar months land in the month of `to`: on or before
    // `to`, or one month too far.
    private static long Months(DateTime from, DateTime to)
    {
        int months = (int)(Month(to) - Month(from));

        return from.AddMonths(months).Ticks <= to.Ticks ? months : months - 1;
    }

    // The same in the local calendar of a zone, for two instants. An instant
    // is reached when the local date and time `from` has that many months
    // later is an instant not later than `to`. The clocks may have been set
    // back before `to`, by less than a month, so they may have read up to one
    // calendar month more than they read at `to`: the count starts there and
    // goes down. No months are always reached.
    private static long Months(DateTime from, DateTime to, TimeZoneInfo clock)
    {
        DateTime local = ZoneClock.Local(from, clock);
        long months = Month(ZoneClock.Local(to, clock)) - Month(local) + 1;

        while (!Reached(local, months, to, clock))
        {
            months--;
        }

        return months;
    }

    private static bool Reached(
        DateTime local,
        long months,
        DateTime to,
        TimeZoneInfo clock
    )
    {
        return Month(local) + months <= LastMonth
            && ZoneClock.InstantTicks(local.AddMonths((int)months).Ticks, clock)
                <= to.Ticks;
    }

    // The calendar month of a date, counted from year 0.
    private static long Month(DateTime date)
    {
        return (date.Year * 12L) + date.Month - 1;
    }

    private static DateTime Midnight(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    private static DateTime Truncated(DateTime operand, long period)
    {
        return new DateTime(operand.Ticks - (operand.Ticks % period), DateTimeKind.Utc);
    }
}
