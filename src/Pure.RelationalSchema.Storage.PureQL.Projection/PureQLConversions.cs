using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Conversions between a value and its text, the specification's "Conversion
// results". asString writes a value the way a literal of its type is written,
// in its one spelling, so the text is the same whatever the culture. as<Type>
// of a string reads exactly that text and nothing else: no surrounding spaces,
// no "+", no leading zeros, no trailing zeros in a fraction, no exponent, no
// other letter case. Text that is not a literal of the type is an execution
// error for as<Type> and null for tryAs<Type>.
public static class PureQLConversions
{
    // An integer has at most this many digits, a decimal this many significant
    // digits and decimal places, and a time this many digits of a second.
    private const int IntegerDigits = 19;
    private const int DecimalDigits = 28;
    private const int FractionDigits = 6;

    // Decimal digits, "-" before a negative number.
    public static string AsString(long operand)
    {
        return operand.ToString(CultureInfo.InvariantCulture);
    }

    // A decimal is written by its value, not by how it was computed: no
    // exponent, no trailing zeros in the fraction, no "-0", and no point for a
    // whole number.
    public static string AsString(decimal operand)
    {
        if (operand == 0)
        {
            return "0";
        }

        string text = operand.ToString(CultureInfo.InvariantCulture);

        return text.Contains('.') ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    // "true" or "false".
    public static string AsString(bool operand)
    {
        return operand ? "true" : "false";
    }

    // YYYY-MM-DD, the year in four digits.
    public static string AsString(DateOnly operand)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{operand.Year:D4}-{operand.Month:D2}-{operand.Day:D2}"
        );
    }

    // hh:mm:ss, followed by the fraction of the second only when it is not
    // zero, without trailing zeros.
    public static string AsString(TimeOnly operand)
    {
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"{operand.Hour:D2}:{operand.Minute:D2}:{operand.Second:D2}"
        );
        long fraction = operand.Ticks % TimeSpan.TicksPerSecond;

        return fraction == 0
            ? text
            : text
                + "."
                + fraction.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    // The date, "T", the time, "Z": the instant in UTC.
    public static string AsString(DateTime operand)
    {
        return AsString(DateOnly.FromDateTime(operand))
            + "T"
            + AsString(TimeOnly.FromDateTime(operand))
            + "Z";
    }

    // 8-4-4-4-12 hexadecimal digits in lower case.
    public static string AsString(Guid operand)
    {
        return operand.ToString("D");
    }

    // The integer a string spells, within 64 bits.
    public static long AsInteger(string operand)
    {
        return TryAsInteger(operand) ?? throw NotLiteral(operand, "an integer");
    }

    // The decimal a string spells, within 28 digits.
    public static decimal AsDecimal(string operand)
    {
        return TryAsDecimal(operand) ?? throw NotLiteral(operand, "a decimal");
    }

    // The boolean a string spells.
    public static bool AsBoolean(string operand)
    {
        return TryAsBoolean(operand) ?? throw NotLiteral(operand, "a boolean");
    }

    // The date a string spells: a real calendar date from year 0001.
    public static DateOnly AsDate(string operand)
    {
        return TryAsDate(operand) ?? throw NotLiteral(operand, "a date");
    }

    // The time a string spells, to the microsecond.
    public static TimeOnly AsTime(string operand)
    {
        return TryAsTime(operand) ?? throw NotLiteral(operand, "a time");
    }

    // The instant a string spells, in UTC with "Z".
    public static DateTime AsDatetime(string operand)
    {
        return TryAsDatetime(operand) ?? throw NotLiteral(operand, "a datetime");
    }

    // The uuid a string spells, in lower case.
    public static Guid AsUuid(string operand)
    {
        return TryAsUuid(operand) ?? throw NotLiteral(operand, "a uuid");
    }

    // The integer a string spells, or null for text that is not an integer
    // literal: "0", or digits that start with 1 to 9, "-" before a negative
    // number, from -2^63 to 2^63 - 1.
    public static long? TryAsInteger(string? operand)
    {
        if (operand is null)
        {
            return null;
        }

        bool negative = operand.StartsWith('-');
        ReadOnlySpan<char> digits = operand.AsSpan(negative ? 1 : 0);

        if (
            !IsWhole(digits)
            || digits.Length > IntegerDigits
            || (negative && digits is "0")
        )
        {
            return null;
        }

        ulong magnitude = (ulong)Number(digits);
        ulong largest = negative ? 1UL << 63 : long.MaxValue;

        return magnitude > largest ? null
            : negative ? unchecked((long)(0 - magnitude))
            : (long)magnitude;
    }

    // The decimal a string spells, or null for text that is not a decimal
    // literal: a whole part as in an integer, then a fraction that does not
    // end in 0, with at most 28 significant digits and 28 decimal places, and
    // never "-0".
    public static decimal? TryAsDecimal(string? operand)
    {
        if (operand is null)
        {
            return null;
        }

        bool negative = operand.StartsWith('-');
        ReadOnlySpan<char> number = operand.AsSpan(negative ? 1 : 0);
        int point = number.IndexOf('.');
        ReadOnlySpan<char> whole = point < 0 ? number : number[..point];
        ReadOnlySpan<char> fraction = point < 0 ? [] : number[(point + 1)..];
        int significant = whole is "0" ? fraction.Length : whole.Length + fraction.Length;

        if (
            !IsWhole(whole)
            || (point >= 0 && !IsFraction(fraction))
            || significant > DecimalDigits
            || (negative && significant == 0)
        )
        {
            return null;
        }

        UInt128 digits = (Number(whole) * Power(fraction.Length)) + Number(fraction);

        return new decimal(
            unchecked((int)(uint)digits),
            unchecked((int)(uint)(digits >> 32)),
            unchecked((int)(uint)(digits >> 64)),
            negative,
            (byte)fraction.Length
        );
    }

    // The boolean a string spells, or null for any text but "true" and
    // "false".
    public static bool? TryAsBoolean(string? operand)
    {
        return operand switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };
    }

    // The date a string spells, or null for text that is not a date literal:
    // YYYY-MM-DD, a real calendar date from 0001-01-01 to 9999-12-31.
    public static DateOnly? TryAsDate(string? operand)
    {
        return operand is null ? null : Date(operand);
    }

    // The time a string spells, or null for text that is not a time literal:
    // hh:mm:ss, then 1 to 6 digits of the fraction of the second, the last not
    // 0.
    public static TimeOnly? TryAsTime(string? operand)
    {
        return operand is null ? null : Time(operand);
    }

    // The instant a string spells, or null for text that is not a datetime
    // literal: a date, "T", a time, then "Z". Any other offset is not read.
    public static DateTime? TryAsDatetime(string? operand)
    {
        if (
            operand is null
            || operand.Length < 12
            || operand[10] != 'T'
            || operand[^1] != 'Z'
        )
        {
            return null;
        }

        DateOnly? date = Date(operand.AsSpan(0, 10));
        TimeOnly? time = Time(operand.AsSpan(11, operand.Length - 12));

        return date is null || time is null
            ? null
            : date.Value.ToDateTime(time.Value, DateTimeKind.Utc);
    }

    // The uuid a string spells, or null for text that is not a uuid literal:
    // 8-4-4-4-12 hexadecimal digits in lower case.
    public static Guid? TryAsUuid(string? operand)
    {
        if (
            operand is null
            || operand.Length != 36
            || operand[8] != '-'
            || operand[13] != '-'
            || operand[18] != '-'
            || operand[23] != '-'
        )
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[16];

        for (int digit = 0; digit < 32; digit++)
        {
            // The digits come in groups of 8, 4, 4, 4 and 12, a "-" between.
            int dashes = digit < 8 ? 0 : Math.Min((digit - 4) / 4, 4);
            int value = Hexadecimal(operand[digit + dashes]);

            if (value < 0)
            {
                return null;
            }

            bytes[digit / 2] = (byte)((bytes[digit / 2] << 4) | value);
        }

        return new Guid(bytes, bigEndian: true);
    }

    private static DateOnly? Date(ReadOnlySpan<char> text)
    {
        if (text.Length != 10 || text[4] != '-' || text[7] != '-')
        {
            return null;
        }

        int? year = Digits(text[..4]);
        int? month = Digits(text[5..7]);
        int? day = Digits(text[8..]);

        return
            year is >= 1
            && month is >= 1 and <= 12
            && day >= 1
            && day <= DateTime.DaysInMonth(year.Value, month.Value)
            ? new DateOnly(year.Value, month.Value, day.Value)
            : null;
    }

    private static TimeOnly? Time(ReadOnlySpan<char> text)
    {
        if (text.Length < 8 || text[2] != ':' || text[5] != ':')
        {
            return null;
        }

        int? hour = Digits(text[..2]);
        int? minute = Digits(text[3..5]);
        int? second = Digits(text[6..8]);
        long? fraction = text.Length == 8 ? 0 : Fraction(text[8..]);

        return hour <= 23 && minute <= 59 && second <= 59 && fraction is not null
            ? new TimeOnly(
                (hour.Value * TimeSpan.TicksPerHour)
                    + (minute.Value * TimeSpan.TicksPerMinute)
                    + (second.Value * TimeSpan.TicksPerSecond)
                    + fraction.Value
            )
            : null;
    }

    // The ticks of a fraction of a second written as a point and 1 to 6
    // digits, the last not 0, or null for any other text.
    private static long? Fraction(ReadOnlySpan<char> text)
    {
        ReadOnlySpan<char> digits = text[1..];

        return text[0] == '.' && digits.Length <= FractionDigits && IsFraction(digits)
            ? (long)(Number(digits) * Power(FractionDigits - digits.Length))
                * TimeSpan.TicksPerMicrosecond
            : null;
    }

    // The value of a hexadecimal digit in lower case, or -1 for any other
    // character.
    private static int Hexadecimal(char digit)
    {
        return digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            _ => -1,
        };
    }

    // The number a text of two or four digits spells, or null when a character
    // is not a digit.
    private static int? Digits(ReadOnlySpan<char> text)
    {
        return IsDigits(text) ? (int)Number(text) : null;
    }

    // Whether a text is a whole number in its one spelling: "0", or digits
    // that start with 1 to 9.
    private static bool IsWhole(ReadOnlySpan<char> text)
    {
        return IsDigits(text) && (text.Length == 1 || text[0] != '0');
    }

    // Whether a text is the digits of a fraction in its one spelling: at least
    // one digit, the last not 0.
    private static bool IsFraction(ReadOnlySpan<char> text)
    {
        return IsDigits(text) && text[^1] != '0';
    }

    // Whether a text is one or more of the digits 0 to 9, and no other
    // character that Unicode counts as a digit.
    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        foreach (char letter in text)
        {
            if (letter is < '0' or > '9')
            {
                return false;
            }
        }

        return text.Length > 0;
    }

    // The number that digits spell; the empty text spells zero.
    private static UInt128 Number(ReadOnlySpan<char> digits)
    {
        UInt128 number = 0;

        foreach (char digit in digits)
        {
            number = (number * 10) + (uint)(digit - '0');
        }

        return number;
    }

    private static UInt128 Power(int exponent)
    {
        UInt128 power = 1;

        for (int i = 0; i < exponent; i++)
        {
            power *= 10;
        }

        return power;
    }

    private static FormatException NotLiteral(string operand, string type)
    {
        return new FormatException($"The text '{operand}' is not {type} literal.");
    }
}
