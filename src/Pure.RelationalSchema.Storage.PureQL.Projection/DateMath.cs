using System.Globalization;
using System.Text.RegularExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Date math, the relative values of Elasticsearch and Grafana that the current
// and offset parameters of Pure.RelationalSchema.Storage.PureQL.Projection.Parameters
// hold: now, then at most one part per unit in the order y (years), M (months),
// d (days), h (hours), m (minutes) and s (seconds), each +N or -N, all of one
// sign, such as now-1M-5d. A date takes y, M and d, a time h, m and s, a
// datetime all six. Its value at an instant, the UTC instant a query runs
// truncated to the microsecond, the specification's precision: years and
// months move it by calendar months together, the day clamped to the end of a
// shorter month, then the days, hours, minutes and seconds are added; a date is
// the UTC date of the result and a time its UTC time of day, wrapping around
// midnight.
internal sealed partial class DateMath
{
    private const string Now = "now";

    private const string Units = "yMdhms";

    private readonly string _type;

    private readonly int[] _amounts;

    private DateMath(string type, int[] amounts)
    {
        _type = type;
        _amounts = amounts;
    }

    // Whether a value of a parameter of the type is date math: a date, time or
    // datetime whose text starts with now.
    public static bool Is(string type, string text)
    {
        return type is "date" or "time" or "datetime"
            && text.StartsWith(Now, StringComparison.Ordinal);
    }

    // The date math of a value Is says is date math. Throws FormatException
    // when it is not well formed for the type, and OverflowException when an
    // amount is above 2147483647.
    public static DateMath Parse(string type, string text)
    {
        Match match = Expression().Match(text);
        string units = type switch
        {
            "date" => "yMd",
            "time" => "hms",
            _ => Units,
        };
        int[] amounts = new int[Units.Length];
        int last = -1;
        char? sign = null;

        if (!match.Success)
        {
            throw Invalid(text, type);
        }

        for (int i = 0; i < match.Groups["unit"].Captures.Count; i++)
        {
            char unit = match.Groups["unit"].Captures[i].Value[0];
            char part = match.Groups["sign"].Captures[i].Value[0];
            int position = Units.IndexOf(unit, StringComparison.Ordinal);

            if (!units.Contains(unit) || position <= last || (sign ?? part) != part)
            {
                throw Invalid(text, type);
            }

            int amount = int.Parse(
                match.Groups["amount"].Captures[i].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture
            );
            amounts[position] = part == '-' ? -amount : amount;
            last = position;
            sign = part;
        }

        return new DateMath(type, amounts);
    }

    // The value at the instant: a DateOnly, TimeOnly or UTC DateTime. Throws
    // ArgumentOutOfRangeException or OverflowException when it is outside
    // the years 0001 to 9999.
    public object At(DateTime now)
    {
        DateTime instant = new(
            now.Ticks - (now.Ticks % TimeSpan.TicksPerMicrosecond),
            DateTimeKind.Utc
        );

        return _type switch
        {
            "date" => DateOnly
                .FromDateTime(instant)
                .AddMonths(Months())
                .AddDays(_amounts[2]),
            "time" => TimeOnly.FromDateTime(instant).Add(TimeSpan.FromTicks(DayTicks())),
            _ => instant
                .AddMonths(Months())
                .AddDays(_amounts[2])
                .AddHours(_amounts[3])
                .AddMinutes(_amounts[4])
                .AddSeconds(_amounts[5]),
        };
    }

    [GeneratedRegex(@"^now(?:(?<sign>[+-])(?<amount>[0-9]+)(?<unit>[yMdhms]))*\z")]
    private static partial Regex Expression();

    private static FormatException Invalid(string text, string type)
    {
        return new FormatException($"'{text}' is not date math of a {type}.");
    }

    private int Months()
    {
        return checked((12 * _amounts[0]) + _amounts[1]);
    }

    // The hours, minutes and seconds within a day, as ticks: a time wraps
    // around midnight, so whole days do not move it.
    private long DayTicks()
    {
        return (_amounts[3] % 24 * TimeSpan.TicksPerHour)
            + (_amounts[4] % (24 * 60) * TimeSpan.TicksPerMinute)
            + (_amounts[5] % (24 * 60 * 60) * TimeSpan.TicksPerSecond);
    }
}
