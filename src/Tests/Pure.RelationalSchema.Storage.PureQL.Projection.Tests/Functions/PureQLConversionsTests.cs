using System.Globalization;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// "Conversion results" in the specification: asString writes a value in the
// one spelling of a literal of its type, and as<Type> / tryAs<Type> of a
// string read exactly that text and nothing else.
public sealed class PureQLConversionsTests
{
    [Theory]
    [InlineData(-42, "-42")]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(1200, "1200")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void AnIntegerIsWrittenAsDigitsWithAMinusBeforeANegativeNumber(
        long operand,
        string text
    )
    {
        Assert.Equal(text, PureQLConversions.AsString(operand));
        Assert.Equal(operand, PureQLConversions.AsInteger(text));
        Assert.Equal(operand, PureQLConversions.TryAsInteger(text));
    }

    [Theory]
    [InlineData("19.9", "19.9")]
    [InlineData("19.90", "19.9")]
    [InlineData("0.125", "0.125")]
    [InlineData("1200", "1200")]
    [InlineData("1200.00", "1200")]
    [InlineData("1.50", "1.5")]
    [InlineData("0", "0")]
    [InlineData("0.000", "0")]
    [InlineData("-0.0", "0")]
    [InlineData("-0.50", "-0.5")]
    [InlineData("100", "100")]
    [InlineData("10.0", "10")]
    [InlineData("-42", "-42")]
    [InlineData("9999999999999999999999999999", "9999999999999999999999999999")]
    [InlineData("-9999999999999999999999999999", "-9999999999999999999999999999")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [InlineData("0.0000001000", "0.0000001")]
    [InlineData("0.3333333333333333333333333333", "0.3333333333333333333333333333")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    public void ADecimalIsWrittenByItsValueInOneSpelling(string operand, string text)
    {
        decimal value = decimal.Parse(operand, CultureInfo.InvariantCulture);

        Assert.Equal(text, PureQLConversions.AsString(value));
    }

    [Fact]
    public void ADecimalIsWrittenByItsValueNotByHowItWasComputed()
    {
        Assert.Equal("0.125", PureQLConversions.AsString(1m / 8m));
        Assert.Equal("19.9", PureQLConversions.AsString(19.90m));
        Assert.Equal("5", PureQLConversions.AsString(2.5m * 2.0m));
        Assert.Equal("0", PureQLConversions.AsString(-0.0m * 1.00m));
    }

    [Fact]
    public void ABooleanIsWrittenAsTrueOrFalse()
    {
        Assert.Equal("true", PureQLConversions.AsString(true));
        Assert.Equal("false", PureQLConversions.AsString(false));
        Assert.True(PureQLConversions.AsBoolean("true"));
        Assert.False(PureQLConversions.AsBoolean("false"));
        Assert.True(PureQLConversions.TryAsBoolean("true"));
        Assert.False(PureQLConversions.TryAsBoolean("false"));
    }

    [Theory]
    [InlineData(987, 1, 31, "0987-01-31")]
    [InlineData(1, 1, 1, "0001-01-01")]
    [InlineData(9999, 12, 31, "9999-12-31")]
    [InlineData(2024, 2, 29, "2024-02-29")]
    [InlineData(2000, 2, 29, "2000-02-29")]
    [InlineData(2024, 10, 5, "2024-10-05")]
    public void ADateIsWrittenWithTheYearInFourDigits(
        int year,
        int month,
        int day,
        string text
    )
    {
        DateOnly date = new(year, month, day);

        Assert.Equal(text, PureQLConversions.AsString(date));
        Assert.Equal(date, PureQLConversions.AsDate(text));
        Assert.Equal(date, PureQLConversions.TryAsDate(text));
    }

    [Theory]
    [InlineData(18, 30, 0, 0, "18:30:00")]
    [InlineData(18, 30, 0, 1250000, "18:30:00.125")]
    [InlineData(0, 0, 0, 0, "00:00:00")]
    [InlineData(23, 59, 59, 9999990, "23:59:59.999999")]
    [InlineData(1, 2, 3, 10, "01:02:03.000001")]
    [InlineData(1, 2, 3, 5000000, "01:02:03.5")]
    [InlineData(1, 2, 3, 1000, "01:02:03.0001")]
    public void ATimeIsWrittenWithItsFractionOnlyWhenItIsNotZero(
        int hour,
        int minute,
        int second,
        long fraction,
        string text
    )
    {
        TimeOnly time = new TimeOnly(hour, minute, second).Add(
            TimeSpan.FromTicks(fraction)
        );

        Assert.Equal(text, PureQLConversions.AsString(time));
        Assert.Equal(time, PureQLConversions.AsTime(text));
        Assert.Equal(time, PureQLConversions.TryAsTime(text));
    }

    [Fact]
    public void ATimeFromStorageIsWrittenWithItsSeventhDigit()
    {
        TimeOnly time = new TimeOnly(18, 30, 0).Add(TimeSpan.FromTicks(1));
        DateTime instant = new(2024, 1, 31, 18, 30, 0, DateTimeKind.Utc);

        Assert.Equal("18:30:00.0000001", PureQLConversions.AsString(time));
        Assert.Equal("23:59:59.9999999", PureQLConversions.AsString(TimeOnly.MaxValue));
        Assert.Equal(
            "2024-01-31T18:30:00.1234567Z",
            PureQLConversions.AsString(instant.AddTicks(1234567))
        );
        Assert.Null(PureQLConversions.TryAsTime("18:30:00.0000001"));
        Assert.Null(PureQLConversions.TryAsDatetime("2024-01-31T18:30:00.1234567Z"));
    }

    [Theory]
    [InlineData("2024-01-31T18:30:00Z", 0)]
    [InlineData("2024-01-31T18:30:00.125Z", 1250000)]
    [InlineData("2024-01-31T18:30:00.000001Z", 10)]
    public void ADatetimeIsWrittenAsTheDateTheTimeAndZ(string text, long fraction)
    {
        DateTime second = new(2024, 1, 31, 18, 30, 0, DateTimeKind.Utc);
        DateTime instant = second.AddTicks(fraction);
        DateTime? read = PureQLConversions.TryAsDatetime(text);

        Assert.Equal(text, PureQLConversions.AsString(instant));
        Assert.Equal(instant, PureQLConversions.AsDatetime(text));
        Assert.Equal(instant, read);
        Assert.Equal(DateTimeKind.Utc, read!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, PureQLConversions.AsDatetime(text).Kind);
    }

    [Fact]
    public void ADatetimeOfAnyKindIsWrittenAsItsTicksInUtc()
    {
        long ticks = new DateTime(2024, 1, 31, 18, 30, 0, DateTimeKind.Utc).Ticks;

        Assert.Equal(
            "2024-01-31T18:30:00Z",
            PureQLConversions.AsString(new DateTime(ticks, DateTimeKind.Local))
        );
        Assert.Equal(
            "2024-01-31T18:30:00Z",
            PureQLConversions.AsString(new DateTime(ticks, DateTimeKind.Unspecified))
        );
        Assert.Equal(
            "0001-01-01T00:00:00Z",
            PureQLConversions.AsString(DateTime.MinValue)
        );
        Assert.Equal(
            "9999-12-31T23:59:59.9999999Z",
            PureQLConversions.AsString(DateTime.MaxValue)
        );
    }

    [Theory]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
    public void AUuidIsWrittenInLowerCase(string text)
    {
        Guid uuid = Guid.ParseExact(text, "D");

        Assert.Equal(text, PureQLConversions.AsString(uuid));
        Assert.Equal(
            text,
            PureQLConversions.AsString(Guid.ParseExact(text.ToUpperInvariant(), "D"))
        );
        Assert.Equal(uuid, PureQLConversions.AsUuid(text));
        Assert.Equal(uuid, PureQLConversions.TryAsUuid(text));
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("12", "12")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("19.99", "19.99")]
    [InlineData("0.125", "0.125")]
    [InlineData("1200", "1200")]
    [InlineData("-42", "-42")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [InlineData("-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [InlineData("0.9999999999999999999999999999", "0.9999999999999999999999999999")]
    [InlineData("9999999999999999999999999999", "9999999999999999999999999999")]
    [InlineData("-9999999999999999999999999999", "-9999999999999999999999999999")]
    [InlineData("1.234567890123456789012345678", "1.234567890123456789012345678")]
    [InlineData("999999999999999999999999999.9", "999999999999999999999999999.9")]
    [InlineData("1000000000000000000000000000", "1000000000000000000000000000")]
    [InlineData("10.01", "10.01")]
    public void ADecimalIsReadFromItsOneSpelling(string text, string value)
    {
        decimal expected = decimal.Parse(value, CultureInfo.InvariantCulture);

        Assert.Equal(expected, PureQLConversions.AsDecimal(text));
        Assert.Equal(expected, PureQLConversions.TryAsDecimal(text));
        Assert.Equal(text, PureQLConversions.AsString(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" 12")]
    [InlineData("12 ")]
    [InlineData("+5")]
    [InlineData("007")]
    [InlineData("01234")]
    [InlineData("00")]
    [InlineData("-0")]
    [InlineData("-")]
    [InlineData("--5")]
    [InlineData("-007")]
    [InlineData("1.50")]
    [InlineData("1.5")]
    [InlineData("5.0")]
    [InlineData("1e3")]
    [InlineData("1E3")]
    [InlineData("1,000")]
    [InlineData("1_000")]
    [InlineData("0x10")]
    [InlineData("12\n")]
    [InlineData("\n12")]
    [InlineData("12a")]
    [InlineData("a")]
    [InlineData("null")]
    [InlineData("١٢")]
    [InlineData("１２")]
    [InlineData("−5")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("18446744073709551616")]
    [InlineData("10000000000000000000")]
    [InlineData("99999999999999999999")]
    [InlineData("100000000000000000000000000000000000000000")]
    public void TextThatIsNotAnIntegerLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsInteger(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsInteger(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" 12")]
    [InlineData("12 ")]
    [InlineData("+5")]
    [InlineData("007")]
    [InlineData("01")]
    [InlineData("00.5")]
    [InlineData("-0")]
    [InlineData("-0.0")]
    [InlineData("0.0")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("-.")]
    [InlineData(".5")]
    [InlineData("-.5")]
    [InlineData("5.")]
    [InlineData("1.50")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("1E3")]
    [InlineData("1.5e3")]
    [InlineData("1,5")]
    [InlineData("1.2.3")]
    [InlineData("1..2")]
    [InlineData("--1.5")]
    [InlineData("1.-5")]
    [InlineData("-01.5")]
    [InlineData("1.5\n")]
    [InlineData("1.5 ")]
    [InlineData("1.٥")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("12345678901234567890123456789")]
    [InlineData("10000000000000000000000000000")]
    [InlineData("-10000000000000000000000000000")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("1.2345678901234567890123456789")]
    [InlineData("1234567890123456789012345678.9")]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("0.12345678901234567890123456789")]
    [InlineData("-0.00000000000000000000000000001")]
    public void TextThatIsNotADecimalLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsDecimal(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsDecimal(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData(" true")]
    [InlineData("true ")]
    [InlineData("true\n")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("t")]
    [InlineData("null")]
    public void TextThatIsNotABooleanLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsBoolean(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsBoolean(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2024-1-5")]
    [InlineData("2024-01-5")]
    [InlineData("2024-1-05")]
    [InlineData("2024-02-30")]
    [InlineData("2023-02-29")]
    [InlineData("1900-02-29")]
    [InlineData("2024-04-31")]
    [InlineData("2024-00-10")]
    [InlineData("2024-13-10")]
    [InlineData("2024-01-00")]
    [InlineData("2024-01-32")]
    [InlineData("0000-01-01")]
    [InlineData("10000-01-01")]
    [InlineData("024-01-01")]
    [InlineData("24-01-01")]
    [InlineData(" 2024-01-31")]
    [InlineData("2024-01-31 ")]
    [InlineData("2024-01-31\n")]
    [InlineData("2024/01/31")]
    [InlineData("2024.01.31")]
    [InlineData("2024-01/31")]
    [InlineData("20240131")]
    [InlineData("2024-01-31T00:00:00Z")]
    [InlineData("31-01-2024")]
    [InlineData("+024-01-31")]
    [InlineData("-024-01-31")]
    [InlineData("2024-0a-31")]
    [InlineData("2024-01-3a")]
    [InlineData("202a-01-31")]
    [InlineData("2024-01-٣1")]
    [InlineData("2024-W05-3")]
    public void TextThatIsNotADateLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsDate(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsDate(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("18:30")]
    [InlineData("18:30:00.500")]
    [InlineData("18:30:00.0")]
    [InlineData("18:30:00.")]
    [InlineData("18:30:00.1234567")]
    [InlineData("18:30:00.0000000")]
    [InlineData("18:30:00,5")]
    [InlineData("18:30:005")]
    [InlineData("24:00:00")]
    [InlineData("23:60:00")]
    [InlineData("23:59:60")]
    [InlineData("1:02:03")]
    [InlineData("01:2:03")]
    [InlineData("01:02:3")]
    [InlineData("01-02-03")]
    [InlineData("01:02-03")]
    [InlineData("010203")]
    [InlineData("0a:02:03")]
    [InlineData("01:0a:03")]
    [InlineData("01:02:0a")]
    [InlineData("01:02:03.5a")]
    [InlineData("01:02:03.a5")]
    [InlineData(" 01:02:03")]
    [InlineData("01:02:03 ")]
    [InlineData("01:02:03\n")]
    [InlineData("01:02:03Z")]
    [InlineData("01:02:03+00:00")]
    [InlineData("6:30:00 PM")]
    [InlineData("-1:02:03")]
    [InlineData("01:02:٣3")]
    public void TextThatIsNotATimeLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsTime(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsTime(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Z")]
    [InlineData("T")]
    [InlineData("2024-01-31TZ")]
    [InlineData("2024-01-31T18:30:00+00:00")]
    [InlineData("2024-01-31T18:30:00-00:00")]
    [InlineData("2024-01-01T03:00:00+03:00")]
    [InlineData("2024-01-31T18:30:00")]
    [InlineData("2024-01-31T18:30:00z")]
    [InlineData("2024-01-31t18:30:00Z")]
    [InlineData("2024-01-31 18:30:00Z")]
    [InlineData("2024-01-31T18:30Z")]
    [InlineData("2024-01-31T18:30:00.500Z")]
    [InlineData("2024-01-31T18:30:00.Z")]
    [InlineData("2024-01-31T18:30:00.1234567Z")]
    [InlineData("2024-01-31T24:00:00Z")]
    [InlineData("2024-01-31T23:59:60Z")]
    [InlineData("2024-02-30T18:30:00Z")]
    [InlineData("2023-02-29T18:30:00Z")]
    [InlineData("0000-01-01T00:00:00Z")]
    [InlineData("2024-1-31T18:30:00Z")]
    [InlineData("2024-01-31T18:30:00ZZ")]
    [InlineData(" 2024-01-31T18:30:00Z")]
    [InlineData("2024-01-31T18:30:00Z ")]
    [InlineData("2024-01-31T18:30:00Z\n")]
    [InlineData("2024-01-31")]
    [InlineData("18:30:00")]
    [InlineData("2024-01-31T18:30:00 UTC")]
    [InlineData("2024-01-31T18:30:00GMT")]
    public void TextThatIsNotADatetimeLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsDatetime(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsDatetime(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("3F2A6C1E-8B4D-4E2A-9C1F-1A2B3C4D5E6F")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6F")]
    [InlineData("3f2a6c1e8b4d4e2a9c1f1a2b3c4d5e6f")]
    [InlineData("{3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f}")]
    [InlineData("(3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f)")]
    [InlineData("urn:uuid:3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f0")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6g")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6 ")]
    [InlineData(" 3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6\n")]
    [InlineData("3f2a6c1e08b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("3f2a6c1e-8b4d04e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("3f2a6c1e-8b4d-4e2a09c1f-1a2b3c4d5e6f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f01a2b3c4d5e6f")]
    [InlineData("3f2a6c1-e8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e/f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e:f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e`f")]
    [InlineData("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e٣f")]
    public void TextThatIsNotAUuidLiteralIsNotRead(string text)
    {
        Assert.Null(PureQLConversions.TryAsUuid(text));
        _ = Assert.Throws<FormatException>(() => PureQLConversions.AsUuid(text));
    }

    [Fact]
    public void NullIsReadAsNullAndNeverFails()
    {
        Assert.Null(PureQLConversions.TryAsInteger(null));
        Assert.Null(PureQLConversions.TryAsDecimal(null));
        Assert.Null(PureQLConversions.TryAsBoolean(null));
        Assert.Null(PureQLConversions.TryAsDate(null));
        Assert.Null(PureQLConversions.TryAsTime(null));
        Assert.Null(PureQLConversions.TryAsDatetime(null));
        Assert.Null(PureQLConversions.TryAsUuid(null));
    }

    [Fact]
    public void AnErrorNamesTheTextAndTheType()
    {
        FormatException error = Assert.Throws<FormatException>(() =>
            PureQLConversions.AsDate("2024-02-30")
        );

        Assert.Equal("The text '2024-02-30' is not a date literal.", error.Message);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("tr-TR")]
    public void TheTextDoesNotDependOnTheCulture(string culture)
    {
        CultureInfo before = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal("-1234567", PureQLConversions.AsString(-1234567L));
            Assert.Equal("-1234567.125", PureQLConversions.AsString(-1234567.1250m));
            Assert.Equal(
                "2024-01-31",
                PureQLConversions.AsString(new DateOnly(2024, 1, 31))
            );
            Assert.Equal(
                "18:30:00.125",
                PureQLConversions.AsString(new TimeOnly(18, 30, 0, 125))
            );
            Assert.Equal(
                "2024-01-31T18:30:00.125Z",
                PureQLConversions.AsString(
                    new DateTime(2024, 1, 31, 18, 30, 0, 125, DateTimeKind.Utc)
                )
            );
            Assert.Equal("true", PureQLConversions.AsString(true));
            Assert.Equal(-1234567.125m, PureQLConversions.AsDecimal("-1234567.125"));
            Assert.Equal(-1234567L, PureQLConversions.AsInteger("-1234567"));
            Assert.Equal(
                new DateOnly(2024, 1, 31),
                PureQLConversions.AsDate("2024-01-31")
            );
            Assert.Equal(
                new TimeOnly(18, 30, 0, 125),
                PureQLConversions.AsTime("18:30:00.125")
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void EveryIntegerIsReadBackFromItsText()
    {
        Random random = new(20240131);

        for (int i = 0; i < 20000; i++)
        {
            long value = random.NextInt64(long.MinValue, long.MaxValue) >> (i % 64);

            Assert.Equal(
                value,
                PureQLConversions.AsInteger(PureQLConversions.AsString(value))
            );
        }
    }

    [Fact]
    public void EveryDecimalIsReadBackFromItsText()
    {
        Random random = new(20240131);

        for (int i = 0; i < 20000; i++)
        {
            // Fewer than 10^28, with 0 to 28 decimal places and either sign.
            decimal value = new(
                random.Next(),
                random.Next(),
                random.Next(0x204FCE5E),
                i % 2 == 1,
                (byte)(i % 29)
            );
            string text = PureQLConversions.AsString(value);
            decimal read = PureQLConversions.AsDecimal(text);

            Assert.Equal(value, read);
            Assert.Equal(text, PureQLConversions.AsString(read));
        }
    }

    [Fact]
    public void EveryDateTimeAndInstantIsReadBackFromItsText()
    {
        Random random = new(20240131);

        for (int i = 0; i < 20000; i++)
        {
            long ticks = random.NextInt64(DateTime.MaxValue.Ticks + 1);
            DateTime instant = new(ticks - (ticks % 10), DateTimeKind.Utc);
            DateOnly date = DateOnly.FromDateTime(instant);
            TimeOnly time = TimeOnly.FromDateTime(instant);

            Assert.Equal(
                instant,
                PureQLConversions.AsDatetime(PureQLConversions.AsString(instant))
            );
            Assert.Equal(
                date,
                PureQLConversions.AsDate(PureQLConversions.AsString(date))
            );
            Assert.Equal(
                time,
                PureQLConversions.AsTime(PureQLConversions.AsString(time))
            );
        }
    }

    [Fact]
    public void EveryUuidIsReadBackFromItsText()
    {
        Random random = new(20240131);
        byte[] bytes = new byte[16];

        for (int i = 0; i < 5000; i++)
        {
            random.NextBytes(bytes);

            Guid uuid = new(bytes);

            Assert.Equal(
                uuid,
                PureQLConversions.AsUuid(PureQLConversions.AsString(uuid))
            );
            Assert.Equal(uuid, PureQLConversions.TryAsUuid(uuid.ToString()));
        }
    }
}
