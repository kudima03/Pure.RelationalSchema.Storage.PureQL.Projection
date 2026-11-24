using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// How the tests of PureQLAggregates write values and results as text, one
// spelling each and the same on every machine: a column is a list of values
// separated by spaces, null among them, and a pair is first:second.
internal static class AggregateVectors
{
    private const string DateFormat = "yyyy-MM-dd";

    private const string TimeFormat = "HH:mm:ss.fffffff";

    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

    public static decimal Decimal(string value)
    {
        return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    public static decimal?[] Decimals(string values)
    {
        return Column<decimal?>(values, value => Decimal(value));
    }

    public static long?[] Integers(string values)
    {
        return Column<long?>(
            values,
            value => long.Parse(value, CultureInfo.InvariantCulture)
        );
    }

    public static (decimal?, decimal?)[] Pairs(string pairs)
    {
        return
        [
            .. Items(pairs)
                .Select(pair => Decimals(pair.Replace(':', ' ')))
                .Select(pair => (pair[0], pair[1])),
        ];
    }

    public static DateOnly?[] Dates(string values)
    {
        return Column<DateOnly?>(
            values,
            value => DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture)
        );
    }

    public static TimeOnly?[] Times(string values)
    {
        return Column<TimeOnly?>(
            values,
            value => TimeOnly.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture)
        );
    }

    // Instants as they come from storage: the ticks that are written, with no
    // kind, which an aggregate reads as UTC.
    public static DateTime?[] Instants(string values)
    {
        return Column<DateTime?>(
            values,
            value => DateTime.ParseExact(
                value,
                InstantFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            )
        );
    }

    public static string Text(decimal? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }

    public static string Text(DateOnly? value)
    {
        return value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "null";
    }

    public static string Text(TimeOnly? value)
    {
        return value?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? "null";
    }

    // An instant ends in Z only when it says that it is in UTC.
    public static string Text(DateTime? value)
    {
        return value is not DateTime instant ? "null"
            : instant.ToString(InstantFormat, CultureInfo.InvariantCulture)
                + (instant.Kind == DateTimeKind.Utc ? "Z" : string.Empty);
    }

    // The result of an aggregate, or "overflow" when it is outside the range
    // of its type.
    public static string Text(Func<decimal?> aggregate)
    {
        try
        {
            return Text(aggregate());
        }
        catch (OverflowException)
        {
            return "overflow";
        }
    }

    public static string Text(Func<long?> aggregate)
    {
        try
        {
            return aggregate()?.ToString(CultureInfo.InvariantCulture) ?? "null";
        }
        catch (OverflowException)
        {
            return "overflow";
        }
    }

    private static string[] Items(string values)
    {
        return values.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static T?[] Column<T>(string values, Func<string, T?> read)
    {
        return
        [
            .. Items(values).Select(value => value == "null" ? default : read(value)),
        ];
    }
}
